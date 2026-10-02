extends "res://scenes/FirstAwakening/FirstAwakening.gd"
## Chapter coordinator: field input, dialogue lifecycle and module composition.
const ObservationReader = preload("res://scenes/Story/ObservationReader.gd")
const Content = preload("res://scenes/FirstAwakening/RedTokenContent.gd")
const RED_DIALOGUE = preload("res://scenes/FirstAwakening/red_token.dialogue")
const RedBridge = preload("res://scenes/FirstAwakening/RedTokenBridge.cs")
const EvidenceDetail = preload("res://scenes/FirstAwakening/RedTokenEvidenceView.gd")
const DefenseView = preload("res://scenes/FirstAwakening/RedTokenDefenseView.gd")
const Director = preload("res://scenes/Story/StoryDirector.gd")
const RED_LAYOUT: Resource = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/red_mechanisms.tres")
var red_defense_view = DefenseView.new()
var red: Node
var round_number := 1
var drafts := [{}, {}, {}]
var verified := [false, false, false]
var red_seen := {}
var red_companions_met := false
var receipt_checked := false
var red_after := ""
var red_dialogue_queue: Array[String] = []
var red_exit_open := false
var red_details := {}
var red_notebook: Control
var red_notebook_box: VBoxContainer
var replay_summary: Label
var deduction_view: Control
var red_visuals: Node2D
var safe_done := {}
var red_interaction_results := {}
var red_feedback := {}
var red_notes := {}
var red_note_id := ""
var red_note_history_start := 0
var red_inspection_loading := false
var red_defense_answer := {}
var red_defense_section := "open"
var red_defense_hints := false
var red_opening_active := false
var room_seen := {}
var story_director: Node
var story_ok := true
var story_message := ""
var story_has_beats := false
var story_safe_holder: String:
	get: return str(red.call("State").get("safe_holder", "")) if is_instance_valid(red) else ""
const STORY_ACTORS := {"侦探":"侦探", "工程师":"工程师霍铎", "厨师":"主厨沈鼎", "音乐家":"音乐家苏泠"}
var room_sites = Content.ROOM_SITES.duplicate(true)

var red_sites = Content.RED_SITES.duplicate(true)

var fields = Content.FIELDS.duplicate(true)

const WORDS = Content.WORDS

var investigation_phase: String = "investigation"
var conversation_staged: bool = false
var replay_states: Array = []
var replay_events: Array[String] = []
var replay_index: int = 0
var replay_accum: float = 0.0
var replay_fast: bool = false
var replay_playing: bool = false
var replay_stage: Node

func _setup_case_runtime() -> void:
	red_defense_view.setup(self)
	red = RedBridge.new()
	add_child(red)
	# The inherited scene retains the legacy node for old-case checks.
	# Red Token owns its own session and does not initialize the old deduction UI.

func _load_sites() -> void:
	sites.clear()
	for id in red_sites:
		var item: Array = red_sites[id]
		if RED_LAYOUT.data.sites.has(id):
			var at: Array = RED_LAYOUT.data.sites[id]
			item[1] = Vector2(at[0],at[1])
		sites[id] = {"id":id,"title":item[0],"title_id":item[0],"point":item[1],"reach":72.0,"diagram":"","description_id":""}
	for id in room_sites:
		var item: Array = room_sites[id]
		sites[id] = {"id":id,"title":item[0],"title_id":item[0],"point":item[1],"reach":48.0,"diagram":"","description_id":""}
	sites["companions"] = {"id":"companions","title":"同伴与凭证","title_id":"同伴与凭证","point":Vector2(280,556),"reach":96.0}

func _start_case() -> void:
	investigation_phase = "investigation"
	conversation_staged = false
	# Preserve the existing party placement and seated opening.
	_place_party_at_table()
	$HallTraceDecals.hide()
	get_window().title = "安眠者有福 · 未归的红筹 · Demo"
	hud_time.position = Vector2(650,18)
	hud_time.size = Vector2(270,34)
	hud_time.add_theme_font_size_override("font_size",18)
	hud_time.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	red_visuals = preload("res://scenes/FirstAwakening/RedTokenMechanisms.gd").new()
	red_visuals.source = self
	scene_items.add_child(red_visuals)
	story_director = Director.new()
	add_child(story_director)
	story_director.setup(self, actors, RED_DIALOGUE)
	story_director.busy_changed.connect(func(value: bool):
		story_lock = value
		if value: _cancel_click_navigation())
	_begin_dialogue("opening")

func steady_investigation_hints() -> bool:
	return true

func _build_geometry() -> void:
	super._build_geometry()
	for item in layout.furniture:
		if item.name in RED_LAYOUT.data.replaced and item.footprint != null:
			var footprint: Array = item.footprint
			obstacle_rects.erase(Rect2(footprint[0],footprint[1],footprint[2],footprint[3]))
	for item in RED_LAYOUT.data.fixtures:
		if item.footprint != null:
			var r: Array = item.footprint
			obstacle_rects.append(Rect2(r[0],r[1],r[2],r[3]))

func _begin_dialogue(title: String, _after := "", _keep_scene := false) -> void:
	if dialogue == null: return
	if dialogue.active or story_lock: return
	story_ok = true
	story_message = ""
	story_has_beats = false
	if title in ["opening", "S01"]: red_opening_active = true
	if title == "S28": _open_red_exit()
	if is_instance_valid(red_notebook): red_notebook.hide()
	if modal: modal.hide()
	modal_open = false
	mode = ""
	dialogue.panel.show()
	dialogue.heading.text = "未归的红筹 · 现场"
	dialogue.confirm_completed_keyboard_line = true
	dialogue.start(RED_DIALOGUE, "S01" if title == "opening" else title, self)

func dialogue_wake_look(direction: String) -> void:
	if red_opening_active and direction in ["front", "back", "left", "right"]:
		player.sit_at(direction, 4.0)

func story_beat(id: String) -> void:
	story_has_beats = true
	var result: Dictionary = await story_director.play(id)
	story_ok = result.get("ok", false)
	story_message = str(result.get("message", ""))

func story_wait() -> void:
	var result: Dictionary = await story_director.join()
	story_ok = result.get("ok", false)
	story_message = str(result.get("message", ""))

func story_require(requirement: Dictionary) -> Dictionary:
	var known: Array = red.call("State").get("known", [])
	for id in requirement.get("known", []):
		if id not in known: return {"ok":false, "message":"尚未取得现场材料：" + str(id)}
	return {"ok":true}

func story_can_move(actor: String) -> Dictionary:
	if actor == story_safe_holder:
		return {"ok":false, "message":actor + "仍在持续持柄，请先松开试柄。"}
	if actor == str(red.call("State").get("holder", "")):
		return {"ok":false, "message":actor + "仍占用现场持续岗位，请先完成确认或取消。"}
	return {"ok":true}

func story_commit(command: Dictionary) -> Dictionary:
	var kind := str(command.get("kind", ""))
	var result: Dictionary
	var actor := str(command.get("actor", ""))
	if kind in ["safe", "perform"] and not STORY_ACTORS.has(actor):
		return {"ok":false, "message":"没有对应的机制行动者：" + actor}
	match kind:
		"safe":
			var operation := str(command.get("operation", ""))
			if operation not in ["reward", "press", "release", "gap", "pipe", "close", "red", "handle", "push_red", "push_handle", "white"]:
				return {"ok":false, "message":"没有该安全试件动作。"}
			result = red.call("SafeTest", operation + ":" + str(STORY_ACTORS[actor]))
			if result.get("ok", false):
				var key := "reward:" + str(STORY_ACTORS[actor]) if operation == "reward" else operation
				safe_done[key] = true
			if is_instance_valid(red_visuals): red_visuals.test_state = result.get("safe_state", {})
		"inspect":
			var id := str(command.get("id", ""))
			result = red.call("Inspect", id)
			if result.get("ok", false): _accept_red_inspection(id, result)
		"perform":
			result = red.call("Perform", STORY_ACTORS[actor], str(command.get("action", "")), str(command.get("station", "")))
		"cancel":
			result = red.call("Cancel")
			if is_instance_valid(red_visuals): red_visuals.test_state = result.get("safe_state", {})
		_:
			return {"ok":false, "message":"剧情机制命令不受支持。"}
	return result

func _follow_companion() -> void:
	if not paired_companion.is_empty() and not story_can_move(paired_companion).get("ok", false): return
	super._follow_companion()

func _on_dialogue_finished() -> void:
	if story_has_beats and is_instance_valid(story_director):
		var result: Dictionary = await story_director.join()
		if not result.get("ok", true):
			story_ok = false
			story_message = str(result.get("message", ""))
	if not story_ok:
		red_opening_active = false
		dialogue.panel.hide()
		_show_message("剧情编排未完成", story_message + "\n已提交的机制结果保留；本段未完成的演出不算抵达。")
		return
	if red_opening_active:
		red_opening_active = false
		_leave_bed()
	var inspected := red_note_id
	if red_note_id != "":
		red_notes[red_note_id] = dialogue.history.slice(red_note_history_start)
		red_note_id = ""
	dialogue.panel.hide()
	if inspected != "" and not ended:
		if room_sites.has(inspected): _open_room_interaction(inspected)
		else: _open_red_interaction("E09" if inspected.begins_with("E09.") else inspected)
	elif red_after != "":
		var next := red_after
		red_after = ""
		_begin_dialogue(next)
	elif not red_dialogue_queue.is_empty():
		_begin_dialogue(red_dialogue_queue.pop_front())
	elif ended:
		_open_modal("本 Demo 完成 · 证物与未知保留", "red")
		_label(modal_box,"红筹案已结，本区交接记录留存。外闩已升起，四人带着证物走向门厅；封存的疑问将带入主楼。下一章尚未开放。",21)
		modal.add_footer([["重新开始",func(): get_tree().reload_current_scene()]])

func dialogue_confirm_red_companions() -> void:
	# S02 has completed its introductions and, when available, actual signature checks.
	# Refresh only the saved E14 record; its first reading remains in dialogue history.
	if red_companions_met: return
	red_companions_met = true
	if not red_notes.has("E14"): return
	var observation_line: Variant = await dialogue.manager.get_next_dialogue_line(RED_DIALOGUE, "OBS_E14", [self])
	if observation_line == null: return
	var speaker_name := str(dialogue.call("_localized_speaker", observation_line.character))
	red_notes["E14"] = [speaker_name + "\n" + str(observation_line.text)]

func _open_red_exit() -> void:
	if red_exit_open: return
	red_exit_open = true
	paired_companion = ""
	click_path.clear()
	var door := scene_items.get_node("MechanicalIsolationDoor")
	door.get_node("DoorFace").hide()
	door.get_node("DoorGap").show()
	bolt_visual.hide()
	var sign := PanelContainer.new()
	sign.name = "RedExitPassSign"
	sign.position = Vector2(704,970)
	sign.custom_minimum_size = Vector2(64,28)
	var paper := StyleBoxFlat.new()
	paper.bg_color = Color("214d3b")
	paper.content_margin_left = 12
	paper.content_margin_right = 12
	sign.add_theme_stylebox_override("panel",paper)
	sign.theme = active_locale_theme
	scene_items.add_child(sign)
	var pass_label := _label(sign,"通行",20,Color("f4ecd4"))
	pass_label.autowrap_mode = TextServer.AUTOWRAP_OFF
	pass_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	# Per-person exit paths now belong to S28 in the dialogue score.

func _process(delta: float) -> void:
	if hud_hint: hud_hint.visible = not modal_open and not dialogue.active
	if replay_playing and is_instance_valid(replay_stage):
		replay_accum += delta
		var beat := 0.65 / (4.0 if replay_fast else 1.0)
		while replay_accum >= beat and replay_index < replay_events.size():
			replay_accum -= beat
			replay_index += 1
		if replay_index >= replay_events.size(): replay_playing = false
		replay_stage.frame = replay_index
		replay_stage.progress = replay_accum / beat if replay_playing else 0.0

func _site_accessible(id: String) -> bool:
	if id == "E16": return red_seen.has("E13") and red_seen.has("E15")
	return sites.has(id)

func _pick_nearest() -> void:
	if not is_instance_valid(player): return
	var scores: Array = []
	for id in sites:
		if not _site_accessible(id): continue
		var distance: float = player.position.distance_to(_site_point(id))
		if distance <= float(sites[id].reach): scores.append([distance,id])
	scores.sort_custom(func(a,b): return a[0] < b[0])
	reachable.clear()
	for candidate in scores: reachable.append(candidate[1])
	nearest = "" if reachable.is_empty() else reachable[cycle_offset % reachable.size()]
	if hud_hint: hud_hint.text = "" if nearest == "" else "E 检视 · " + str(sites[nearest].title)

func _has_new_at(id: String) -> bool:
	return not room_seen.has(id) if room_sites.has(id) else not red_seen.has(id)

func _update_status() -> void:
	var count := 0
	for id in red_sites:
		if red_seen.has(id): count += 1
	if hud_title: hud_title.text = "未归的红筹"
	if hud_time: hud_time.text = "J 证物　F 假说　R 重启"

func _apply_locale_theme() -> void:
	super._apply_locale_theme()
	active_locale_theme = preload("res://scenes/FirstAwakening/field_theme_zh.tres")
	if is_instance_valid(dialogue): dialogue.apply_locale_font(preload("res://ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf"),active_locale_theme)
	if is_instance_valid(modal): modal.theme = active_locale_theme

func _on_language_selected(index: int) -> void:
	super._on_language_selected(index)
	if index != 0:
		_show_message("Translation coverage / 翻訳範囲","The new Red Token case is currently available in Chinese. Language selection still applies to shared controls and name entry.\n新しい事件本文は現在中国語のみです。共有操作と姓名入力の言語設定は保持されます。\n本轮新案未提供完整英日译文，旧案译文不会用作新案对白。")

func _open_modal(title: String, next_mode: String) -> void:
	_restore_red_scroll()
	modal_locale_refresh = Callable()
	if is_instance_valid(red_notebook): red_notebook.hide()
	replay_playing = false
	mode = next_mode
	modal_open = true
	modal.open_content(title,next_mode,active_locale_theme)
	modal.call_deferred("_fit_height",modal.regular_bounds)

func _interact(id: String) -> void:
	click_path.clear()
	if room_sites.has(id):
		player.face_point(_site_point(id))
		_open_room_interaction(id)
		return
	if id == "companions":
		_open_modal("四人的凭证与同行", "red")
		_button(modal_box,"核对来意与姓名",func(): _begin_dialogue("S02"))
		for actor_name in ["工程师","厨师","音乐家"]:
			_button(modal_box,"邀请%s同行" % actor_name,func(): paired_companion = actor_name; _close_modal())
		_button(modal_box,"停止同行",func(): paired_companion = ""; _close_modal())
		return
	_open_red_interaction(id)

func _open_room_interaction(id: String) -> void:
	_open_modal(str(room_sites[id][0]), "red")
	_button(modal_box, "检视" if not room_seen.has(id) else "再次查看调查记录", func(): _inspect_room(id))
	modal.add_footer([["返回现场",_close_modal]])

func _inspect_room(id: String) -> void:
	if red_inspection_loading: return
	if red_notes.has(id):
		_open_recorded_evidence(id, false)
		return
	await _show_red_inspection_record(id, id)

func _return_to_inspection(id: String) -> void:
	if room_sites.has(id): _open_room_interaction(id)
	else: _open_red_interaction("E09" if id.begins_with("E09.") else id)

func _open_red_interaction(id: String, keep_scroll := false) -> void:
	if not red_sites.has(id): return
	var scroll: ScrollContainer = modal_box.get_parent()
	var scroll_position := scroll.scroll_vertical if keep_scroll else 0
	var result: Dictionary = red_interaction_results.get(id, {})
	var trial: Dictionary = red_visuals.test_state if is_instance_valid(red_visuals) else result.get("safe_state", {})
	_open_modal(red_sites[id][0], "red")
	_device_preview(modal_box,id,trial)
	if not result.is_empty():
		# A successful reward message is represented by that person's single
		# delivery below; failures and other safe actions keep their result text.
		var reward_result: bool = id == "E01" and result.get("ok", false)
		if not reward_result: _label(modal_box,str(result.get("message","试件未动作。")),18)
		_red_safe_status(id,trial)
	_button(modal_box,"检视",func(): _inspect_red(id))
	if id == "E01":
		var known: Array = red.call("State").get("known", [])
		if "E13" in known and "E15" in known:
			var query := _button(modal_box, "就现场材料向接待者提问" + (" · 已检视，可复核" if "E16" in known else ""), _open_red_inquiry)
			query.custom_minimum_size.y = 54
			query.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			query.add_theme_font_size_override("font_size", 22)
		else:
			_label(modal_box, "铜口位于领取柜上方；现实质询须先检视归还匣与椅上红筹的现场材料。", 18)
		var deliveries: Dictionary = Content.DELIVERIES
		for person in ["侦探","工程师霍铎","主厨沈鼎","音乐家苏泠"]:
			var verified_person: bool = safe_done.get("reward:" + person, false)
			_button(modal_box,"%s · %s" % [person, "已核验交付" if verified_person else "核验交付"],func(): _safe_red("reward:"+person,"本人凭证与约定交付"))
			if verified_person:
				_label(modal_box, str(deliveries[person]), 18)
		_label(modal_box, "四人各自核验可信凭证与约定交付后，自愿决定留下参加；拒绝教学不损随时离校终止权。", 18)
		_button(modal_box,"听取安全与去留说明",func(): _begin_dialogue("S04"))
		_button(modal_box,"拒绝教学，安全结束本 Demo",_safe_exit)
	for option in Content.SAFE_INTERACTIONS.get(id, []):
		_red_safe_button(option[0],option[1],option[2])
	for option in Content.FOLLOWUP_INSPECTIONS.get(id, []):
		_button(modal_box,option[1],func(): _inspect_red(option[0]))
	if id in ["E10", "E11"]:
		_label(modal_box, "触物入口原在试柄旁；记录的是两人的转述，内容以检视后证物页为准。", 18)
	if id == "E01":
		_pin_red_footer([["返回现场",_close_modal],["提交答辩",_open_red_defense]])
	else:
		modal.add_footer([["返回现场",_close_modal]])
	if keep_scroll: scroll.set_deferred("scroll_vertical",scroll_position)

func _red_safe_button(operation: String, caption: String, title: String) -> void:
	var mark := " · 已核验" if safe_done.get(operation,false) else ""
	_button(modal_box,caption + mark,func(): _safe_red(operation,title))

func _safe_red(operation: String, _title: String) -> void:
	if story_lock: return
	var result: Dictionary
	if operation in ["press", "release"]:
		_open_modal("空载试件 · 人物操作", "red")
		_label(modal_box, "工程师正在按剧情编排操作试柄……", 20)
		modal.add_footer([["取消本次走位", func(): story_director.cancel()]])
		result = await story_director.play("safe_" + operation)
	else:
		result = red.call("SafeTest",operation)
	if result.get("ok",false): safe_done[operation] = true
	if is_instance_valid(red_visuals) and result.has("safe_state"): red_visuals.test_state = result.safe_state
	var id: String = Content.safe_interaction_site(operation)
	red_interaction_results[id] = result
	_open_red_interaction(id,true)

func _red_safe_status(id: String, trial: Dictionary) -> void:
	if id == "E01":
		_label(modal_box,"凭证与约定交付：已核验 %d / 4 人。" % int(trial.get("rewards_verified", 0)),18)
	elif id == "E03":
		_label(modal_box,"空载试柄当前%s。" % ("按住，安全回路保持" if trial.get("pressed", false) else "已松开，弹簧回位停流"),18)
	elif id == "E04":
		_label(modal_box,"本次声路试件：门缝声%s；管壁振颤声%s。" % ["有" if trial.get("gap_sound", false) else "无", "有" if trial.get("pipe_sound", false) else "无"],18)
	elif id == "E13":
		_label(modal_box,"筹槽%s；柄槽%s；白片%s。试件只核验交接机构，不证明排灰完成。" % ["已接收" if trial.get("red_received", false) else "未接收", "已接收" if trial.get("handle_received", false) else "未接收", "已领取" if safe_done.get("white", false) else "可领取" if trial.get("white", false) else "尚未解锁"],18)

func _device_preview(parent: Node, id: String, trial := {}) -> void:
	var detail_id: String = red_details.get(id,id)
	if id != "E13" or not red_interaction_results.has("E13"):
		if _add_red_detail(parent,detail_id): return
	var assets := {"E01":"reward_cabinet","E02":"assignment_board","E03":"spring_handle","E08":"cloth_clip","E09":"inspect_basin","E10":"handle_rack","E11":"assignment_board","E13":"inspect_box","E14":"sign_strip","E15":"red_token"}
	if not assets.has(id): return
	var asset: String = assets[id]
	if id == "E03" and trial.get("pressed",false): asset = "spring_handle_pressed"
	if id == "E13" and trial.get("red_received",false): asset = "inspect_box_received"
	if id == "E13" and safe_done.get("push_red",false): asset = "inspect_box_stamped"
	if id == "E13" and trial.get("white",false): asset = "inspect_box_white"
	var texture: Texture2D = load("res://ShareAsset/Internal/Props/FirstAwakeningInterior/red_"+asset+".png")
	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	parent.add_child(row)
	var view := TextureRect.new()
	view.texture = texture
	view.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	view.custom_minimum_size = texture.get_size() * (3.0 if id in ["E01","E09"] else 4.0)
	view.stretch_mode = TextureRect.STRETCH_SCALE
	view.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(view)
	if red_seen.has(id) and EvidenceDetail.has_detail(id):
		_button(parent,"查看已记录原物细节",func(): _open_recorded_evidence(id,false))

func _add_red_detail(parent: Node, id: String) -> bool:
	if not red_seen.has(id) or not EvidenceDetail.has_detail(id): return false
	var detail := EvidenceDetail.new()
	parent.add_child(detail)
	detail.setup(id)
	return true

func _inspect_red(id: String, from_inquiry := false) -> void:
	if red_inspection_loading: return
	if red_notes.has(id):
		_open_recorded_evidence(id, false)
		return
	if id == "E16" and not from_inquiry:
		_open_red_inquiry()
		return
	if id == "E16": red.call("Perform","侦探","open_copper","receiver")
	var result: Dictionary = red.call("Inspect",id)
	if not result.get("ok",false):
		_show_message("材料尚未成立",str(result.get("message","请先核对材料")))
		return
	_accept_red_inspection(id, result)
	var title: String = Content.observation_title(id)
	await _show_red_inspection_record(id, title)

func _accept_red_inspection(id: String, result: Dictionary) -> void:
	red_seen[id] = result
	red_details["E09" if id.begins_with("E09.") else id] = id
	evidence[id] = true
	evidence_sources[id] = "具名转述" if id == "E12" else "亲见"
	if id == "E01": receipt_checked = true
	if id == "E09.deep": red_seen["E09"] = result; evidence["E09"] = true

func _show_red_inspection_record(id: String, title: String) -> void:
	# Use the same dialogue resource and state evaluator as the spoken view.
	# Only the successfully inspected object's observation is read here.
	red_inspection_loading = true
	_open_modal("正在核对现场材料", "red")
	_label(modal_box, "正在整理本次检视……", 20)
	var record: Dictionary = await ObservationReader.read(dialogue, RED_DIALOGUE, title, self)
	red_inspection_loading = false
	if record.status == "interactive":
		red_note_id = id
		if room_sites.has(id): room_seen[id] = true
		red_note_history_start = dialogue.history.size()
		_begin_dialogue(title)
		return
	if record.status != "complete":
		_show_message("检视尚未完成", "本次记录未能完整展开，请重新检视。" if record.status == "cycle" else "未取得本次材料记录，请重新检视。")
		return
	var paragraphs: Array[String] = record.paragraphs
	var line_ids: Array[String] = record.line_ids
	red_notes[id] = paragraphs
	if room_sites.has(id): room_seen[id] = true
	for index in paragraphs.size():
		dialogue.history.append(paragraphs[index])
		dialogue.history_line_ids.append(line_ids[index])
	_open_recorded_evidence(id, false)

func _open_journal() -> void:
	_open_modal("证物页：事实、来源与未知", "red")
	for id in room_sites:
		if room_seen.has(id):
			_button(modal_box,str(room_sites[id][0]),func(): _open_recorded_evidence(id, false))
	for id in red_sites:
		if red_seen.has(id):
			_button(modal_box,str(red_sites[id][0]),func(): _open_recorded_evidence(id, false))
	if red_seen.has("E09.deep"):
		_button(modal_box,"E09 · 已补查上游捕集片",func(): _open_recorded_evidence("E09.deep", false))
	if red_seen.has("E09.completion"):
		_button(modal_box,"E09 · 已核同号转清与停流联",func(): _open_recorded_evidence("E09.completion", false))
	_pin_red_footer([["返回现场",_close_modal],["编辑假说",_request_flow]])

func _open_recorded_evidence(id: String, return_to_notebook: bool) -> void:
	if not red_seen.has(id) and not room_seen.has(id): return
	var title: String = str(room_sites[id][0]) if room_sites.has(id) else str(red_sites[id][0]) if red_sites.has(id) else "上游捕集片" if id == "E09.deep" else "同号转清与停流联"
	_open_modal(id + " · " + title, "red")
	_add_red_detail(modal_box,id)
	for paragraph in red_notes.get(id, []):
		_label(modal_box,str(paragraph),18)
	if not red_notes.has(id): _label(modal_box,"检视对白尚未读完，请回到现场完成本次检视。",18)
	_pin_red_footer([["返回推演",_open_red_notebook]] if return_to_notebook else [["返回检视",func(): _return_to_inspection(id)],["返回证物页",_open_journal],["返回现场",_close_modal]])

func _open_red_feedback() -> void:
	_open_modal("推演差异与已读材料", "red")
	_label(modal_box,str(red_feedback.get("message", "尚未执行推演。")),20)
	for message in red_feedback.get("conflicts", []):
		_label(modal_box,"冲突 · " + str(message),18)
	for message in red_feedback.get("unproven", []):
		_label(modal_box,"待证 · " + str(message),18)
	_label(modal_box,"查阅已经读过的材料",20)
	for id in red_notes:
		var title: String = str(room_sites[id][0]) if room_sites.has(id) else str(red_sites[id][0]) if red_sites.has(id) else "上游捕集片" if id == "E09.deep" else "同号转清与停流联"
		_button(modal_box,id + " · " + title,func(): _open_recorded_evidence(id, true))
	_pin_red_footer([["返回推演",_open_red_notebook]])

func _request_flow() -> void:
	_open_red_round(round_number)

func _open_red_round(number: int) -> void:
	round_number = number
	_open_red_notebook()
	if is_instance_valid(deduction_view):
		deduction_view.current_mode = "motivation" if number == 2 else "sequence"
		deduction_view.refresh()

func _evaluate_red(number: int) -> void:
	var candidate := {}
	for fragment in drafts: candidate.merge(fragment,true)
	var result: Dictionary = red.call("EvaluateRound",number,candidate)
	red_feedback = result
	verified[number-1] = result.get("ok",false)
	set_meta("last_predictions", result.get("predictions", {}))
	if is_instance_valid(replay_summary):
		replay_summary.text = str(result.get("message","这一片段尚无材料可验证。"))
	_install_red_snapshots(result.get("snapshots",[]))
	if is_instance_valid(deduction_view):
		deduction_view.refresh()

func _load_competitor(kind: String) -> void:
	drafts = [
		{"cloth_before_flow":"yes" if kind == "H1" else "no","flow":"ash" if kind == "H1" else "water","wipe":"after" if kind == "H1" else "before","wedge":"dirty_then_wrap"},
		{"engineer_exception":"read","holder":"engineer","cook_filter":"yes" if kind == "H1" else "no","sound_gap":"quiet","sound_pipe":"ringing","release":"after_confirmation","completion_record":"actual" if kind == "H1" else "mistaken"},
		{"returned":"no" if kind == "H1" else "yes","take":"never" if kind == "H1" else "after_return","return_before_room":"no" if kind == "H1" else "yes","identity":"unknown"}
	]
	verified = [false,false,false]
	_evaluate_red(3)
	if is_instance_valid(replay_summary):
		replay_summary.text = "H1：排灰后清理，未交回红筹，带着未结工作返房。接收印与签条不相容。" if kind == "H1" else "H2：先擦地后清水洗布，误记完成，器具交回后开屉再取。上游捕集片灰印与此冲突。"

func _open_red_notebook() -> void:
	modal_locale_refresh = Callable()
	if not is_instance_valid(red_notebook):
		red_notebook = preload("res://scenes/FirstAwakening/MindNotebook.tscn").instantiate()
		for child in red_notebook.get_children():
			if child is CanvasItem: child.visible = child.name in ["Paper","StageMount","Transport"]
		ui.add_child(red_notebook)
		red_notebook.get_node("Transport").setup(self)
		replay_states = [{"ids":[],"closed":true,"inner_bolt":false,"outer_bar":false,"caption":"候选历史尚未提交"}]
		replay_stage = preload("res://scenes/FirstAwakening/ReplayStage.tscn").instantiate()
		replay_stage.set_script(preload("res://scenes/FirstAwakening/RedTokenReplayStage.gd"))
		replay_stage.source = self
		red_notebook.get_node("StageMount").add_child(replay_stage)
		
		# Unified Deduction View (Event Sequence & Motivation Deduction)
		deduction_view = preload("res://scenes/FirstAwakening/RedTokenDeductionView.gd").new()
		red_notebook.add_child(deduction_view)
		deduction_view.setup(self)
		deduction_view.return_requested.connect(_close_modal)
		replay_summary = deduction_view.finding_label
		red_notebook_box = deduction_view.right_content
	modal.hide()
	red_notebook.show()
	modal_open = true
	mode = "rednotebook"
	if is_instance_valid(deduction_view):
		deduction_view.refresh()

func _install_red_snapshots(snapshots: Array) -> void:
	replay_events.clear()
	replay_states.clear()
	for snapshot in snapshots:
		var visible: Dictionary = snapshot.duplicate(true)
		visible["valve_open"] = visible.get("flowing",false)
		for field in {"ids":[],"closed":true,"inner_bolt":false,"outer_bar":false}:
			if not visible.has(field): visible[field] = {"ids":[],"closed":true,"inner_bolt":false,"outer_bar":false}[field]
		replay_states.append(visible)
		if replay_states.size() > 1: replay_events.append(str(visible.get("caption","动作提交")))
	if replay_states.is_empty(): replay_states.append({"ids":[],"closed":true,"inner_bolt":false,"outer_bar":false})
	replay_index = 0
	replay_accum = 0.0
	replay_fast = false
	replay_playing = not replay_events.is_empty()
	replay_stage.events = replay_events
	replay_stage.frame = replay_index
	replay_stage.last_frame = -1
	replay_stage.progress = 0.0

func _replay_step(amount: int, _mark_reached := true) -> void:
	replay_index = clampi(replay_index + amount,0,replay_events.size())
	if is_instance_valid(replay_stage): replay_stage.frame = replay_index; replay_stage.progress = 0.0

func _start_replay(fast := false) -> void:
	replay_fast = fast
	if replay_index >= replay_events.size(): replay_index = 0
	replay_accum = 0.0
	replay_playing = not replay_events.is_empty()

func _open_red_actions() -> void:
	_open_modal("安全模型：身体、工具与持续岗位", "red")
	for operation in [["工程师读并接受具名特例","工程师霍铎","read_exception","B"],["工程师从器材架取柄","工程师霍铎","take_handle","B"],["主厨先夹滤布","主厨沈鼎","clip_filter","C"],["主厨确认夹牢","主厨沈鼎","confirm_filter","C"],["工程师持柄保持排灰","工程师霍铎","hold","B"],["主厨检查当班捕集灰","主厨沈鼎","capture","C"],["主厨盆侧观察出水转清并确认","主厨沈鼎","inspect_clear","C"],["工程师释放柄","工程师霍铎","release","B"],["工程师去门厅插内销","工程师霍铎","bolt","D"],["工程师交回柄","工程师霍铎","deposit_other","A"],["值守者交回红筹","工程师霍铎","deposit_red","A"]]:
		_button(modal_box,operation[0],func(): _perform_red(operation.slice(1)))
	_button(modal_box,"取消本次持续动作（保留已提交灰印）",func(): red.call("Cancel"); _show_message("持续动作已取消","水流停止，已留下的灰与已提交交接历史保留。"))
	_pin_red_footer([["返回假说",_request_flow],["返回现场",_close_modal]])

func _perform_red(pair: Array) -> void:
	var result: Dictionary = red.call("Perform",pair[0],pair[1],pair[2])
	_show_message("动作提交",str(result.get("message","本次动作没有提交。")))

func _restore_red_scroll() -> void:
	if not is_instance_valid(modal) or not modal.has_node("RedPageLayout"): return
	var layout: VBoxContainer = modal.get_node("RedPageLayout")
	var scroll: ScrollContainer = modal_box.get_parent()
	layout.remove_child(scroll)
	modal.remove_child(layout)
	modal.add_child(scroll)
	scroll.size_flags_vertical = Control.SIZE_FILL
	layout.queue_free()

func _pin_red_footer(actions: Array, status := "") -> void:
	# Only long Red Token pages use this wrapper. The body scrolls independently.
	var scroll: ScrollContainer = modal_box.get_parent()
	modal.remove_child(scroll)
	var layout := VBoxContainer.new()
	layout.name = "RedPageLayout"
	layout.custom_minimum_size.y = modal.regular_bounds.size.y - modal.get_theme_stylebox("panel").get_minimum_size().y
	layout.add_theme_constant_override("separation", 10)
	modal.add_child(layout)
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	layout.add_child(scroll)
	var footer := VBoxContainer.new()
	footer.name = "RedFixedFooter"
	layout.add_child(footer)
	var rule := ColorRect.new()
	rule.color = FieldModal.RULE
	rule.custom_minimum_size.y = 1
	footer.add_child(rule)
	if status != "": _label(footer, status, 17)
	var row := GridContainer.new()
	row.columns = 2
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("h_separation", 12)
	row.add_theme_constant_override("v_separation", 8)
	footer.add_child(row)
	for action in actions:
		_button(row, action[0], action[1]).size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_layout_red_fixed_page.call_deferred()

func _layout_red_fixed_page() -> void:
	# Run after FieldModal's short-card fitting so long pages keep their bounds.
	await get_tree().process_frame
	await get_tree().process_frame
	if not modal.has_node("RedPageLayout"): return
	modal.position = modal.regular_bounds.position
	modal.size = modal.regular_bounds.size

func _red_defense_sections() -> Array:
	return Content.defense_sections()

func _red_defense_statements() -> Dictionary:
	return Content.defense_statements()

func _defense_option_text(options: Array, value: String) -> String:
	return red_defense_view._defense_option_text(options, value)

func _open_red_defense() -> void:
	red_defense_view._open_red_defense()

func _red_defense_status() -> String:
	return red_defense_view._red_defense_status()

func _style_red_statement(button: Button, selected: bool) -> void:
	red_defense_view._style_red_statement(button, selected)

func _defense_choice(key: String, options: Array, answer: Dictionary, parent: Node = null, caption := "") -> void:
	red_defense_view._defense_choice(key, options, answer, parent, caption)

func _open_red_inquiry() -> void:
	var known: Array = red.call("State").get("known", [])
	if "E16" in known:
		_open_recorded_evidence("E16", false)
		return
	if not ("E13" in known and "E15" in known):
		_show_message("现场材料未齐", "请先检视归还匣与椅上红筹，再就实际痕迹发问。")
		return
	_open_modal("铜口 · 就现场痕迹发问", "red")
	_label(modal_box, "归还匣与椅上的痕迹已经记下。选择你要请接待者核验的问题；他的回答仍须与原物、当班记录相对照。", 20)
	_button(modal_box, "归还匣的钢印留着同一缺角。\n“你能查到接收后，谁还接触过前屉吗？”", func(): _request_red_inquiry_record("归还匣接收后的接触记录"))
	_button(modal_box, "前封已断，拉手留下擦痕，椅上筹带较窄半手印。\n“这些是一次什么样的移物？请拿现场记录核对。”", func(): _request_red_inquiry_record("断封、拉手与椅上筹的移物记录"))
	if "E14" in known:
		_button(modal_box, "四人的返房签条已有记录。\n“能否查一查返房完成之后的巡检？”", func(): _request_red_inquiry_record("返房之后的巡检记录"))
	_pin_red_footer([["返回领取柜",func(): _open_red_interaction("E01")],["返回现场",_close_modal]])

func _request_red_inquiry_record(question: String) -> void:
	# Several material-based questions request the same real record. The existing
	# Inspect path owns availability and acquisition; no host-created evidence.
	dialogue.history.append("侦探\n请求核验：" + question)
	dialogue.history_line_ids.append("")
	_inspect_red("E16", true)

func _safe_exit() -> void:
	if is_instance_valid(story_director): story_director.cancel()
	red.call("Cancel")
	ended = true
	_open_modal("安全结束本 Demo", "red")
	_label(modal_box,"生活支路已停流，实验动力保持断联。饮水与食物已确认。四人可选择休息或由院务安排后勤返航；本 Demo 在生活翼结束布置，无需认罪或过题。",21)
	modal.add_footer([["重新开始",func(): get_tree().reload_current_scene()]])

func _unhandled_input(event: InputEvent) -> void:
	if story_lock: return
	if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_R and not dialogue.active:
		red.call("Cancel")
		get_tree().reload_current_scene()
		return
	super._unhandled_input(event)

func _close_modal() -> void:
	modal_locale_refresh = Callable()
	modal.hide()
	if is_instance_valid(red_notebook): red_notebook.hide()
	replay_playing = false
	modal_open = false
	mode = ""

func _exit_tree() -> void:
	if is_instance_valid(story_director): story_director.cancel()
	if is_instance_valid(red): red.call("Cancel")

func _button(parent: Node, words: String, action: Callable, disabled := false) -> Button:
	var button := super._button(parent,words,action,disabled)
	button.clip_text = false
	button.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var paper := preload("res://scenes/FirstAwakening/paper_button_theme.tres")
	for state in ["normal","hover","pressed","disabled","focus"]:
		button.add_theme_stylebox_override(state,paper.get_stylebox(state,"Button"))
	return button
