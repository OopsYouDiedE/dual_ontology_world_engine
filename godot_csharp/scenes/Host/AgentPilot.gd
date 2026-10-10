extends Node
## AI 坐上玩家席位：只读这名角色本人看得到的候选与本人认知，由引擎按其 .story
## 操控规则选出动作，再经正式界面与现场把动作做出来。宿主不替角色挑选、不注入状态、
## 不快进时钟；任何角色都可以用同一套席位。
signal stopped(reason: String, summary: Dictionary)
signal recorded(kind: String, value: Variant)

const PilotNavigation := preload("res://scenes/Host/PilotNavigation.gd")
const ManuscriptPanel := preload("res://scenes/UI/Panels/Manuscript/ReplayManuscript.tscn")
const MOVE_KEYS := {"W": KEY_W, "A": KEY_A, "S": KEY_S, "D": KEY_D}
const TICK := 0.1

## 每句台词停留的秒数；也是菜单等选项落下前的停顿。
var line_seconds := 0.6
var host: Node
var engine: Node
var stopping := false
var _run_label := ""
var _acting := false
var _reply_token := ""
var _reply: Dictionary = {}
var _pressed: Array[int] = []
var _last_decision: Dictionary = {}
var _last_gate := "-"
var _spaces: Dictionary = {}
var _places: Dictionary = {}
var _routes: Dictionary = {}

func attach(main: Node) -> void:
	host = main
	engine = main.engine
	var shared_panel := ManuscriptPanel.instantiate()
	_run_label = shared_panel.get_node("LeftPage/Scroll/Column/Actions/RunPlayback").text
	shared_panel.free()

func start() -> void:
	_loop()

func last_decision() -> Dictionary:
	return _last_decision

# ------------------------------------------------------------------ loop ---

func _loop() -> void:
	while not stopping:
		await get_tree().create_timer(TICK).timeout
		if stopping or not is_instance_valid(host): return
		var gate := _gate()
		if gate != _last_gate:
			_last_gate = gate
			recorded.emit("seat_wait", {"gate": gate})
		if gate != "": continue
		var observation: Dictionary = _observe()
		if stopping: return
		if bool(observation.get("Complete", false)):
			stop("story_complete")
			return
		if observation.get("Candidates", []).is_empty():
			# Stage walks, fades and timed scenes still owe real acknowledgements; only an idle,
			# controllable field without any candidate is a gap in what the seat can do.
			if _field_idle():
				stop("candidate_missing")
				return
			continue
		var decision: Dictionary = JSON.parse_string(engine.AgentDecideJson())
		decision["GameTime"] = JSON.parse_string(engine.AgentTimeJson())
		_last_decision = decision
		recorded.emit("decision", decision)
		if decision.get("Token") == null:
			stop(str(decision.get("Reason", "agent_cannot_assess")))
			return
		_acting = true
		await _act(str(decision.Token))
		_acting = false

## 席位此刻为什么还不能决定（空串表示可以）；记入轨迹，卡住时据此查原因。
func _gate() -> String:
	if _acting: return "acting"
	if not _pressed.is_empty(): return "keys_held"
	if not host.settled(): return "host_draining"
	if not engine.AgentEnabled(): return "seat_not_bound"
	return ""

func _observe() -> Dictionary:
	var field: Node = host.field
	var ui: Node = host.ui
	if is_instance_valid(field) and field.player != null:
		# Opaque coordinate-frame identity; source map IDs remain in the host.
		var map_key: String = field.adapter.map_key()
		if not _spaces.has(map_key): _spaces[map_key] = "space:%d" % (_spaces.size() + 1)
		var located: String = engine.AgentLocate(_spaces[map_key], field.player.position.x, field.player.position.y)
		if located != "":
			stop(located)
			return {}
	var panel := _open_panel()
	if panel != "":
		var code: String = engine.AgentObservePanel(panel, _run_label)
		if code != "": stop(code)
	elif _field_idle():
		var code: String = engine.AgentObserveField(_field_offers(field, ui.snapshot))
		if code != "": stop(code)
	var observation: Dictionary = JSON.parse_string(engine.AgentObservationJson())
	if is_instance_valid(field) and field.player != null:
		observation["Position"] = [field.player.position.x, field.player.position.y]
	recorded.emit("observation", observation)
	return observation

func _open_panel() -> String:
	var ui: Node = host.ui
	if not is_instance_valid(ui): return ""
	match str(ui.modal_kind):
		"notebook": return "notebook"
		"replay": return "manuscript"
		"map": return "campus"
	return ""

func _field_idle() -> bool:
	var field: Node = host.field
	return is_instance_valid(field) and is_instance_valid(host.ui) and not host.ui.is_busy() and field.field_active() \
		and field.player_controlled() and field.click_path.is_empty() and not field.player.travelling

# --------------------------------------------------------------- acting ---

## 把选中的候选按它代表的普通输入做出来，再交回同一个 token。
func _act(token: String) -> void:
	var input: Dictionary = JSON.parse_string(engine.AgentCandidateInputJson(token))
	if input.has("boundary"):
		stop(str(input.boundary))
		return
	recorded.emit("act", {"kind": str(input.get("kind", "")), "token": token})
	var ui: Node = host.ui
	var kind := str(input.get("kind", ""))
	if is_instance_valid(ui) and ui.dialogue.active:
		if kind == "presented":
			# 一句一句读完台词；对白框读完时自己交回的回执就是这次选择。
			_reply_token = token
			_reply = {}
			while not stopping and _reply.is_empty() and ui.dialogue.active:
				await get_tree().create_timer(line_seconds * 0.5).timeout
				if is_instance_valid(ui): ui.dialogue.advance()
			_reply_token = ""
			if stopping: return
			host.submit_token(token)
			return
		# 台词带选项：读到出现选项为止，再落下这一项。
		while not stopping and ui.dialogue.active and not ui.dialogue_choices_shown():
			await get_tree().create_timer(line_seconds * 0.5).timeout
			if is_instance_valid(ui): ui.dialogue.advance()
		if stopping: return
		await get_tree().create_timer(line_seconds).timeout
		ui.release_dialogue()
		host.submit_token(token)
		return
	match kind:
		"form_field", "form_tab":
			if is_instance_valid(ui): ui.show_form_choice(input)
			await get_tree().create_timer(line_seconds * 0.5).timeout
		"select", "form", "navigate", "card", "travel":
			# 与界面自己的按钮一致：落下选项时合上当前弹窗。
			await get_tree().create_timer(line_seconds).timeout
			if is_instance_valid(ui): ui.close_modal()
		"replay", "replay_vars":
			await get_tree().create_timer(line_seconds * 0.5).timeout
	host.submit_token(token)

## 对白框读完的回执：属于正在读的那一项就收下，其余界面输入一律不代替角色做决定。
func take_ui_reply(reply: Dictionary) -> bool:
	if _reply_token == "" or str(reply.get("kind", "")) != "presented": return false
	_reply = reply
	return true

## 引擎回显的现场动作：用正式现场的寻路、碰撞与键盘输入真实走一遍。
func perform_field(input: Dictionary) -> void:
	var field: Node = host.field
	if not is_instance_valid(field): return
	match str(input.get("action", "")):
		"interact": field._approach(str(input.site))
		"move":
			if input.has("keys"): await _keyboard_move(input.keys)
			else: field._route_click(Vector2(float(input.point[0]), float(input.point[1])))
		"wait": await get_tree().create_timer(1.0).timeout

func _keyboard_move(keys: Array) -> void:
	var field: Node = host.field
	var start: Vector2 = field.player.position
	for key in keys:
		var code: int = MOVE_KEYS[str(key)]
		_pressed.append(code)
		Input.parse_input_event(_key(code, true))
	var deadline := Time.get_ticks_msec() + 350
	while not stopping and Time.get_ticks_msec() < deadline:
		await get_tree().process_frame
	_release_keys()
	if is_instance_valid(field) and field.player != null:
		recorded.emit("keyboard_move", {"keys": keys, "start_position": [start.x, start.y], "end_position": [field.player.position.x, field.player.position.y]})

func _release_keys() -> void:
	for code in _pressed: Input.parse_input_event(_key(code, false))
	_pressed.clear()

static func _key(code: int, pressed: bool) -> InputEventKey:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = pressed
	return event

func stop(reason: String) -> void:
	if stopping: return
	stopping = true
	_release_keys()
	var summary := {"reason": reason, "time": JSON.parse_string(engine.AgentTimeJson()), "selections": engine.AgentSelections(), "last_reasoning": _last_decision}
	var field: Node = host.field if is_instance_valid(host) else null
	if is_instance_valid(field) and field.player != null:
		summary["visible_position"] = [field.player.position.x, field.player.position.y]
	engine.CloseAgent()
	stopped.emit(reason, summary)

# --------------------------------------------------------------- offers ---

## 现场此刻对这名角色公开的输入：视线内的调查点、走得到的短步、键盘方向、等待与面板快捷键。
func _field_offers(field: Node, snapshot: Dictionary) -> Array:
	var offers := []
	var space: String = field.adapter.map_key()
	if not _places.has(space): _places[space] = {}
	var remembered: Dictionary = _places[space]
	var visible_labels := {}
	for id in field.sites:
		if not field.site_visible(id): continue
		var point: Vector2 = field.site_point(id)
		var label: String = str(field.sites[id].title)
		visible_labels[label] = true
		remembered[label] = {"point": point, "reach": float(field.sites[id].reach)}
		offers.append({"label": str(field.sites[id].title), "kind": "interact", "new": field.site_is_new(id), "location": [point.x, point.y], "input": {"kind": "field_action", "action": "interact", "site": id}})
	for label in remembered.keys():
		if not visible_labels.has(label) and field.in_sight(remembered[label].point): remembered.erase(label)
	var known: AStarGrid2D = PilotNavigation.remember_grid(field, _routes.get(space))
	_routes[space] = known
	offers.append_array(PilotNavigation.approaches(field, remembered, known))
	offers.append_array(PilotNavigation.offers(field))
	# Keyboard walking is a public input even when its landing room is unseen; the field resolves it.
	for keys in [["W"], ["S"], ["A"], ["D"], ["W", "A"], ["W", "D"], ["S", "A"], ["S", "D"]]:
		# Offered direction for scoring; actual position is observed separately after execution.
		var heading := Vector2.ZERO
		for key in keys: heading += {"W": Vector2.UP, "S": Vector2.DOWN, "A": Vector2.LEFT, "D": Vector2.RIGHT}[key]
		var aim: Vector2 = field.player.position + heading.normalized() * 48.0
		offers.append({"label": "%s %d,%d" % ["+".join(keys), field.player.position.x, field.player.position.y], "kind": "move", "new": false, "location": [aim.x, aim.y], "input": {"kind": "field_action", "action": "move", "keys": keys}})
	var controls: Dictionary = snapshot.get("controls", {})
	if bool(controls.get("wait", false)):
		offers.append({"label": "T", "kind": "wait", "new": false, "input": {"kind": "action", "act": {"cmd": "stay"}}})
	else:
		offers.append({"label": "", "kind": "wait", "new": false, "input": {"kind": "field_action", "action": "wait"}})
	for shortcut in [{"key": "Tab", "panel": "notebook"}, {"key": "R", "panel": "manuscript"}, {"key": "M", "panel": "campus"}]:
		if bool(controls.get(shortcut.panel, false)):
			offers.append({"label": shortcut.key, "kind": "menu", "new": false, "input": {"kind": "player_panel", "panel": shortcut.panel}})
	return offers
