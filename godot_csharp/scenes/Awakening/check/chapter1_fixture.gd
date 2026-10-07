extends RefCounted
## 第一章回归检查共用的统一宿主夹具（Chapter1.tscn）：开新宿主、代按对白、读写探针、关闭弹窗。
## 自动检查用，不是游玩验证；对白与表单由脚本代为推进。

const SCENE := "res://scenes/Awakening/Chapter1.tscn"
const CONTENT := "res://scenes/Awakening/Script/generated_awakening_content.json"
const LAYOUT := "res://scenes/Awakening/Script/generated_awakening_layout.json"

var tree: SceneTree
var host: Node
var seen_lines: Array[String] = []
var content: Dictionary = {}
var layout: Dictionary = {}
## 对白推进或选项越界时回报（由检查脚本接上 check）。
var on_error: Callable = func(_message: String) -> void: push_error(_message)

func _init(scene_tree: SceneTree) -> void:
	tree = scene_tree
	content = JSON.parse_string(FileAccess.get_file_as_string(CONTENT))
	layout = JSON.parse_string(FileAccess.get_file_as_string(LAYOUT))

# ------------------------------------------------------------------ content ---

func text(key: String, args: Dictionary = {}) -> String:
	var value: String = content.TEXT[key]
	for name in args: value = value.replace("{%s}" % name, str(args[name]))
	return value

func person_name(who: String) -> String: return str(content.PERSON_NAMES[who])

## 人物 → 他的旧物（地图布局里的 objects）。
func own_object(who: String) -> String:
	for id in layout.objects:
		if str(layout.objects[id].person) == who: return str(id)
	return ""

# ------------------------------------------------------------------ fixture ---

func shutdown() -> void:
	if is_instance_valid(host):
		host.begin_shutdown()
		host.queue_free()
		host = null
		await tree.process_frame
		await tree.process_frame

func fresh(skip_intro := true) -> Node:
	await shutdown()
	host = (load(SCENE) as PackedScene).instantiate()
	if skip_intro: host.set_meta("skip_intro", true)
	tree.root.add_child(host)
	for i in 3: await tree.process_frame
	return host

# ------------------------------------------------------------------- reading ---

func stage() -> String: return str(host.ui.snapshot.get("stage", ""))
func ended() -> bool: return bool(host.ui.snapshot.get("ended", false))
func replay_open() -> bool: return bool(host.ui.snapshot.get("replay", {}).get("open", false))
func progress(group: String) -> Dictionary: return host.engine.Probe("progress", [group])
func state(key: String, fallback = null): return progress("state").get(key, fallback)
func probe(name: String, args: Array = []): return host.engine.Probe(name, args)
func set_progress(group: String, key: String, value) -> void: probe("set_progress", [group, key, value])
func remove_progress(group: String, key: String) -> void: probe("remove_progress", [group, key])
func hooks(who: String) -> int: return int(probe("hooks", [who]))
func life(who: String) -> Dictionary: return progress("life").get(who, {})
func replay_result() -> Dictionary: return progress("replay_result")
func aside() -> Label: return host.ui.hud.get_node("Aside")
func player() -> CharacterBody2D: return host.field.player
func actor(who: String) -> CharacterBody2D: return host.field.actors[who]
func map() -> Node2D: return host.field.adapter
func busy() -> bool: return host.ui.is_busy()
func dialogue_active() -> bool: return host.ui.dialogue.active

func modal_text() -> String:
	var result := ""
	if host.ui.modal_root == null: return result
	for node in host.ui.modal_root.find_children("*", "RichTextLabel", true, false):
		result += str(node.get_parsed_text())
	return result

func modal_buttons() -> Array:
	var labels := []
	if host.ui.modal_root == null: return labels
	for node in host.ui.modal_root.find_children("*", "Button", true, false):
		labels.append(str(node.text))
	return labels

## 屏幕上可见的全部文字（标签、富文本、按钮）。
func visible_text(node: Node = null) -> String:
	if node == null: node = host
	var result := ""
	if node is Control and node.is_visible_in_tree() and (node is Label or node is RichTextLabel or node is Button):
		result += (node.get_parsed_text() if node is RichTextLabel else str(node.text)) + "\n"
	for child in node.get_children(): result += visible_text(child)
	return result

# ----------------------------------------------------------------- driving ---

func frames(count: int) -> void:
	for i in count: await tree.process_frame

func physics(count: int) -> void:
	for i in count: await tree.physics_frame

## 按真实时间等条件成立（物理步与时间同步，走位与计时都以秒计）。
func wait_until(condition: Callable, seconds := 10.0) -> bool:
	var deadline := Time.get_ticks_msec() + int(seconds * 1000.0)
	while Time.get_ticks_msec() < deadline:
		if condition.call(): return true
		await tree.process_frame
	return condition.call()

func jump(state_name: String) -> void:
	host.dispatch({"kind": "debug_jump", "state": state_name})
	await frames(10)
	await wait_until(func(): return not busy(), 10.0)
	await physics(4)

func open_card_event(id: String) -> void:
	host.dispatch({"kind": "card", "card_id": id})

## 选当前弹窗的一个选项（按 id 或 id 结尾）；选到了返回 true。
func select(id: String) -> bool:
	for option in host.ui._modal_event.get("options", []):
		if str(option.id) == id or str(option.id).ends_with(":" + id):
			host.ui._select(host.ui._modal_event, str(option.id))
			return true
	return false

## 合上章节弹窗（证物卡/菜单交还“返回”选项；表单走“取消”；界面自己的弹窗直接合上）。
func close_modal() -> void:
	if host.ui.modal_kind in ["card", "menu", "form"]: host.ui._dismiss()
	else: host.ui.close_modal()

func name_form_answer(name: String) -> void:
	host.ui._form.line_edit_for("name").text = name
	for action in host.ui._modal_event.actions:
		if str(action.id) == "confirm":
			host.ui._form_action(host.ui._modal_event, action)
			return

## 代为推进对白：揭示整行、推进；遇到选项按 choices 依次选择（默认优先“没事了。”，否则第一个）。
## quiet：对白、弹窗与走位都静止这么多帧后返回。
func drain(choices: Array = [], limit := 60.0, quiet := 0.7, name := "试验") -> void:
	var idle_since := -1
	var last_id := ""
	var deadline := Time.get_ticks_msec() + int(limit * 1000.0)
	while Time.get_ticks_msec() < deadline:
		await tree.process_frame
		var d = host.ui.dialogue
		if host.ui.modal_kind == "form" and host.ui._modal_event.has("fields") and str(host.ui._modal_event.fields[0].get("id", "")) == "name":
			name_form_answer(name)
			idle_since = -1
			continue
		if not d.active:
			if _walking(): idle_since = -1
			elif idle_since < 0: idle_since = Time.get_ticks_msec()
			elif Time.get_ticks_msec() - idle_since >= int(quiet * 1000.0): return
			continue
		idle_since = -1
		if d.busy or d.line == null: continue
		if str(d.line.id) != last_id:
			last_id = str(d.line.id)
			seen_lines.append(str(d.line.text))
		if d.body.visible_characters >= 0:
			d.reveal()
			continue
		var allowed: Array = d.line.responses.filter(func(r): return r.is_allowed)
		if allowed.is_empty():
			d.advance()
		else:
			var index := 0
			if not choices.is_empty():
				index = int(choices.pop_front())
			else:
				for option in allowed.size():
					if str(allowed[option].text) == "没事了。":
						index = option
						break
			if index < 0 or index >= allowed.size():
				on_error.call("对白选项越界：%s，选择 %d，可选 %d" % [str(d.line.id), index, allowed.size()])
				return
			d.choose(str(allowed[index].next_id))
	var last = host.ui.dialogue
	on_error.call("对白未在 %.0f 秒内静止：stage=%s modal=%s busy=%s line=%s" % [limit, stage(), host.ui.modal_kind, last.busy, str(last.line.id) if last.line != null else "<waiting>"])

## 有剧情走位在进行（日常走动不算）。
func _walking() -> bool:
	for key in host.field.actors:
		var a: CharacterBody2D = host.field.actors[key]
		if is_instance_valid(a) and a.travelling and not host.field._daily.has(key): return true
	return false

## 推进到证物卡出现且没有对白。
func settle_card(seconds := 60.0) -> bool:
	var deadline := Time.get_ticks_msec() + int(seconds * 1000.0)
	while Time.get_ticks_msec() < deadline:
		await drain()
		if host.ui.modal_kind == "card" and not dialogue_active(): return true
		await tree.process_frame
	return false

## 打开证物卡（第一次打开可回忆的物件时先有一句“不妨一试”），推进到卡片出现。
func open_card(id: String) -> bool:
	open_card_event(id)
	return await settle_card()

## 在证物卡上选一项并推进，直到回到证物卡。
func card_action(kind: String, choices: Array = []) -> bool:
	var picked := select(kind)
	if not picked: return false
	var deadline := Time.get_ticks_msec() + 60000
	while Time.get_ticks_msec() < deadline:
		await drain(choices)
		if host.ui.modal_kind == "card" and not dialogue_active(): return true
		await tree.process_frame
	return false

## 触物回忆：同伴要先走过来，再决定；一直推进到回忆结束、证物卡重新打开为止。
func run_recall(id: String, choices: Array = []) -> bool:
	await open_card(id)
	return await card_action("recall", choices)

## 同伴自己走向旧物的整段过程：对白代为推进，直到他放回旧物。
func run_reach(who: String, choices: Array = []) -> bool:
	var deadline := Time.get_ticks_msec() + 90000
	while Time.get_ticks_msec() < deadline:
		await drain(choices)
		if str(life(who).get("state", "")) == "done" and not dialogue_active(): return true
		await tree.process_frame
	return false

## 一个房间里够得着某个点、又走得到的位置。
func stand_near(room_center: Vector2, point: Vector2, radius: float) -> void:
	player().position = room_center
	await physics(3)
	var spot = host.field._spot_near(point, radius, player())
	if spot != null: player().position = spot
	await physics(3)

## 站到某处，等现场把站位报给章节。
func stand_at(point: Vector2) -> void:
	player().position = point
	await physics(4)

func interact(site: String) -> void:
	host.field.interact(site)
	await frames(2)
