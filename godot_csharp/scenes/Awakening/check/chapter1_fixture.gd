extends RefCounted
## 第一章回归检查共用的统一宿主夹具（Main.tscn）：开新宿主、代按对白、读写探针、关闭弹窗。
## 自动检查用，不是游玩验证；对白与表单由脚本代为推进。

const SCENE := "res://scenes/Host/Main.tscn"
const ProbeBridge := preload("res://scenes/Shared/check/ChapterProbe.cs")
const CONTENT := "res://scenes/Awakening/Script/generated_awakening_content.json"
const LAYOUT := "res://scenes/Awakening/Script/generated_awakening_layout.json"

var tree: SceneTree
var host: Node
var _probe := ProbeBridge.new()
var seen_lines: Array[String] = []
var content: Dictionary = {}
var layout: Dictionary = {}
var dialogue: Dictionary = {}
## 对白推进或选项越界时回报（由检查脚本接上 check）。
var on_error: Callable = func(_message: String) -> void: push_error(_message)

func _init(scene_tree: SceneTree) -> void:
	tree = scene_tree
	content = JSON.parse_string(FileAccess.get_file_as_string(CONTENT))
	layout = JSON.parse_string(FileAccess.get_file_as_string(LAYOUT))
	dialogue = JSON.parse_string(FileAccess.get_file_as_string("res://scenes/Awakening/Script/generated_awakening_dialogue.json"))

# ------------------------------------------------------------------ content ---

func text(key: String, args: Dictionary = {}) -> String:
	var value: String = content.TEXT[key]
	for name in args: value = value.replace("{%s}" % name, str(args[name]))
	return value

## 用稳定行号核对本轮剧本原文，不把旧文案当成机制断言。
func line_text(id: String) -> String:
	for steps in dialogue.entries.values():
		var result := _find_line(steps, id)
		if result != "": return result
	on_error.call("剧本行号不存在：" + id)
	return ""

func _find_line(steps: Array, id: String) -> String:
	for step in steps:
		if str(step.get("line_id", "")) == id: return str(step.text)
		for key in ["then", "else", "body", "options"]:
			if step.has(key):
				var result := _find_line(step[key], id)
				if result != "": return result
	return ""

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

func fresh(skip_intro := true, scene_path := SCENE) -> Node:
	await shutdown()
	if not ResourceLoader.exists(scene_path):
		await _fatal("场景不存在：" + scene_path)
		return null
	var scene := load(scene_path) as PackedScene
	if scene == null:
		await _fatal("无法加载宿主：" + scene_path)
		return null
	host = scene.instantiate()
	if host == null:
		await _fatal("无法实例化宿主：" + scene_path)
		return null
	tree.root.add_child(host)
	if not is_instance_valid(host.engine) or not is_instance_valid(host.title_layer):
		await _fatal("Main 初始化失败：缺少引擎或标题页")
		return null
	# 与标题页相同的 start 输入；Main 自己完成 view_ready 握手。
	host.title_layer.get_child(0).start_button.pressed.emit()
	await frames(3)
	if not is_instance_valid(host.ui) or not is_instance_valid(host.field) or host.field.player == null:
		await _fatal("Main 开始第一章失败：缺少 UI、现场或玩家")
		return null
	# 旧 skip_intro 元数据已没有读取者；检查实际推进醒来对白后再注入局面。
	if skip_intro:
		await drain()
		if stage() != "roam" or dialogue_active():
			await _fatal("醒来对白未完成：stage=" + stage())
			return null
	return host

func _fatal(message: String) -> void:
	push_error("chapter1 fixture: " + message)
	tree.quit(1)
	# quit 在本帧结束时生效；停住调用链，避免 process_frame 先恢复后续检查。
	await tree.create_timer(60.0).timeout # 树退出时销毁计时器，不持有夹具自身的信号循环。

# ------------------------------------------------------------------- reading ---

func stage() -> String: return str(host.ui.snapshot.get("stage", ""))
func ended() -> bool: return bool(host.ui.snapshot.get("ended", false))
func replay_open() -> bool: return bool(host.ui.snapshot.get("replay", {}).get("open", false))
func progress(group: String) -> Dictionary: return probe("progress", [group])
func state(key: String, fallback = null): return progress("state").get(key, fallback)
func probe(name: String, args: Array = []): return _probe.Run(host.engine, name, args)
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
	await drain()
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

## 打开证物卡，推进到卡片出现。
func open_card(id: String) -> bool:
	open_card_event(id)
	return await settle_card()

## 终局手印分支的前置：在真实架沿旁逐人选择比对，不直接注入试验完成。
func compare_hands() -> bool:
	await stand_near(Vector2(930, 200), host.field.site_point("E11a"), 64.0)
	for who in ["detective", "huo", "shen", "su"]:
		if who != "detective":
			host.field.execute({"kind": "stage", "op": "place", "who": who, "to": {"point": player().position + Vector2(24, 0)}})
			await physics(4)
		await open_card("E11")
		var completed := await card_action("compare:" + who)
		close_modal()
		await frames(2)
		if not completed or not progress("hand_comparisons").has(who): return false
	return progress("experiments").has("E11")

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
