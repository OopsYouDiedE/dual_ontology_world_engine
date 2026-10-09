extends SceneTree
## Rendered input-session server for tools/blind_playtest.py (chapter one remake, 《门的这一侧》).
## Session folders: artifacts/remake-belief-replay/blind-<session>/.
##
## Launch (windowed, not headless):
##   godot --path godot_csharp --rendering-method gl_compatibility \
##     --script res://scenes/Awakening/check/playtest_session.gd -- --session=<fresh-id> --agent=<name>
##
## It only injects generic mouse/keyboard input into the running default scene
## and publishes what a player can see: a screenshot, visible text and the
## clickable controls. It never reads or writes story state.

const GAME_SCENE := "res://scenes/Host/Main.tscn"
const SETTLE_FRAMES := 6

var session := ""
var scene_path: String = GAME_SCENE
var agent := ""
var run_id := ""
var directory := ""
var ack_seq := 0
var started_ms := 0
var controls_by_id := {}
var frames := []
var render_stalled := false

func _initialize() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--session="):
			session = arg.trim_prefix("--session=")
		elif arg.begins_with("--agent="):
			agent = arg.trim_prefix("--agent=")
		elif arg.begins_with("--scene="):
			scene_path = arg.trim_prefix("--scene=")
	var valid := RegEx.create_from_string("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$")
	if valid.search(session) == null:
		push_error("playtest_session: pass -- --session=<fresh-id>")
		quit(2)
		return
	directory = ProjectSettings.globalize_path("res://").path_join("../artifacts/remake-belief-replay/blind-" + session).simplify_path()
	if DirAccess.dir_exists_absolute(directory) and FileAccess.file_exists(directory.path_join("metadata.json")):
		push_error("playtest_session: session directory already exists; use a fresh id")
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(directory)
	run_id = "%d-%d" % [Time.get_unix_time_from_system() * 1000.0, randi()]
	started_ms = Time.get_ticks_msec()
	call_deferred("_start")

func _start() -> void:
	var scene: Node = load(scene_path).instantiate()
	root.add_child(scene)
	current_scene = scene
	_write_json("metadata.json", {
		"session": session, "agent": agent, "run_id": run_id,
		"scene": scene_path,
		"godot": Engine.get_version_info().string,
		"started_utc": Time.get_datetime_string_from_system(true),
		"viewport": [root.get_visible_rect().size.x, root.get_visible_rect().size.y],
	})
	for i in 30:
		await process_frame
	await _publish(0, "start", "")
	_poll()

func _poll() -> void:
	while true:
		var path := directory.path_join("command.json")
		if FileAccess.file_exists(path):
			var text := FileAccess.get_file_as_string(path)
			var command = JSON.parse_string(text)
			if command is Dictionary and int(command.get("seq", -1)) > ack_seq:
				await _run(command)
		await create_timer(0.05).timeout

func _run(command: Dictionary) -> void:
	var seq := int(command.get("seq", -1))
	if str(command.get("session", "")) != session or str(command.get("run_id", "")) != run_id:
		_reject(seq, "command belongs to another session or run")
		return
	if seq != ack_seq + 1:
		_reject(seq, "seq must be ack_seq + 1")
		return
	_status_pending(seq)
	var error := ""
	frames = []
	var actions: Array = command.get("actions", [])
	for i in actions.size():
		var action = actions[i]
		if not action is Dictionary:
			error = "input must be an object"
			break
		error = await _act(action)
		if error != "":
			break
		# A snapshot inside a batch records what the player saw at that point.
		if str(action.get("type", "")) == "snapshot" and i < actions.size() - 1:
			await _capture_frame(seq, frames.size() + 1)
	for i in SETTLE_FRAMES:
		await process_frame
	await _publish(seq, str(command.get("name", "input")), error)

## Durations accept "ms", "seconds" or "frames" (default one frame) for both hold and wait.
func _hold_for(action: Dictionary) -> void:
	if action.has("ms") or action.has("seconds"):
		var seconds := float(action.get("ms", 0)) / 1000.0 if action.has("ms") else float(action.get("seconds", 0))
		await create_timer(maxf(seconds, 0.0)).timeout
		return
	for i in maxi(1, int(action.get("frames", 1))):
		await process_frame

const ACTION_FIELDS := {
	"click": ["x", "y", "button"], "wheel": ["x", "y", "direction", "steps"], "tap": ["key"],
	"hold": ["key", "ms", "seconds", "frames"], "wait": ["ms", "seconds", "frames"], "text": ["text"],
	"click_control": ["control_id"], "text_control": ["control_id", "text", "replace"], "snapshot": [],
}

func _act(action: Dictionary) -> String:
	# Unknown fields are reported instead of silently ignored, so a mistyped duration cannot pass as one frame.
	var allowed: Array = ACTION_FIELDS.get(str(action.get("type", "")), [])
	for field in action:
		if field != "type" and field not in allowed and ACTION_FIELDS.has(str(action.get("type", ""))):
			return "unknown field for %s: %s" % [action.get("type"), field]
	match str(action.get("type", "")):
		"click":
			await _click(Vector2(float(action.get("x", 0)), float(action.get("y", 0))), str(action.get("button", "left")))
		"wheel":
			var steps := maxi(1, int(action.get("steps", 1)))
			var button := MOUSE_BUTTON_WHEEL_DOWN if str(action.get("direction", "down")) == "down" else MOUSE_BUTTON_WHEEL_UP
			for i in steps:
				await _mouse_button(Vector2(float(action.get("x", 0)), float(action.get("y", 0))), button, true)
				await _mouse_button(Vector2(float(action.get("x", 0)), float(action.get("y", 0))), button, false)
		"tap":
			var code := OS.find_keycode_from_string(str(action.get("key", "")))
			if code == KEY_NONE:
				return "unknown key name: " + str(action.get("key", ""))
			await _key(code, 0, true)
			await _key(code, 0, false)
		"hold":
			var held := OS.find_keycode_from_string(str(action.get("key", "")))
			if held == KEY_NONE:
				return "unknown key name: " + str(action.get("key", ""))
			await _key(held, 0, true)
			await _hold_for(action)
			await _key(held, 0, false)
		"text":
			for ch in str(action.get("text", "")):
				await _key(KEY_NONE, ch.unicode_at(0), true)
				await _key(KEY_NONE, ch.unicode_at(0), false)
		"wait":
			await _hold_for(action)
		"click_control":
			var control: Control = controls_by_id.get(str(action.get("control_id", "")))
			if not is_instance_valid(control) or not control.is_visible_in_tree():
				return "control is no longer displayed"
			await _click(control.get_global_rect().get_center(), "left")
		"text_control":
			var field: Control = controls_by_id.get(str(action.get("control_id", "")))
			if not is_instance_valid(field) or not field.is_visible_in_tree():
				return "control is no longer displayed"
			await _click(field.get_global_rect().get_center(), "left")
			if bool(action.get("replace", false)):
				await _key(KEY_A, 0, true, true)
				await _key(KEY_A, 0, false, true)
				await _key(KEY_BACKSPACE, 0, true)
				await _key(KEY_BACKSPACE, 0, false)
			for ch in str(action.get("text", "")):
				await _key(KEY_NONE, ch.unicode_at(0), true)
				await _key(KEY_NONE, ch.unicode_at(0), false)
		"snapshot":
			pass
		_:
			return "unknown input type"
	return ""

func _click(position: Vector2, button_name: String) -> void:
	var motion := InputEventMouseMotion.new()
	motion.position = position
	motion.global_position = position
	root.push_input(motion, true)
	await process_frame
	var button := MOUSE_BUTTON_RIGHT if button_name == "right" else MOUSE_BUTTON_LEFT
	await _mouse_button(position, button, true)
	await _mouse_button(position, button, false)

func _mouse_button(position: Vector2, button: MouseButton, pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.position = position
	event.global_position = position
	event.button_index = button
	event.pressed = pressed
	event.button_mask = (1 << (button - 1)) if pressed else 0
	root.push_input(event, true)
	await process_frame

func _key(code: Key, unicode: int, pressed: bool, ctrl := false) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.unicode = unicode
	event.pressed = pressed
	event.ctrl_pressed = ctrl
	# Through Input so held keys update Input.is_physical_key_pressed, which movement polls;
	# Viewport.push_input alone delivers the event but never marks the key as held.
	Input.parse_input_event(event)
	await process_frame

# ---------------------------------------------------------------- publish ---

## 快照前等镜头稳定：画布变换连续若干帧不变（最多约 4 秒）。否则人物还在走、镜头还在平移时拍下的画面，
## 与之后点击时的画面坐标不一致，会造成“点击偏移”。这是驱动的时机问题，不读写任何剧情状态。
const STABLE_FRAMES := 8
const MAX_STABLE_WAIT := 240

## 批次中途的快照还要等可见文字不再变化（打字机走完），最多约 12 秒。
const TEXT_STABLE_CAP_MS := 12000
const TEXT_STILL_MS := 500

## 远程桌面断开/重连时渲染设备可能停摆，frame_post_draw 不再触发；限时等待，超时写入错误而不是挂起会话。
const DRAW_TIMEOUT_MS := 5000

func _await_drawn() -> bool:
	var drawn := [false]
	var mark := func() -> void: drawn[0] = true
	RenderingServer.frame_post_draw.connect(mark, CONNECT_ONE_SHOT)
	var deadline := Time.get_ticks_msec() + DRAW_TIMEOUT_MS
	while not drawn[0] and Time.get_ticks_msec() < deadline:
		await create_timer(0.02).timeout
	if not drawn[0]:
		RenderingServer.frame_post_draw.disconnect(mark)
	return drawn[0]

func _visible_texts() -> Array:
	# Keeps the IDs from the last published snapshot valid for the rest of the batch.
	var kept := controls_by_id.duplicate()
	var texts := []
	_scan(root, [], texts)
	controls_by_id = kept
	return texts

func _wait_text_stable() -> void:
	var deadline := Time.get_ticks_msec() + TEXT_STABLE_CAP_MS
	var last := JSON.stringify(_visible_texts())
	var same := 0
	var since := Time.get_ticks_msec()
	while (same < STABLE_FRAMES or Time.get_ticks_msec() - since < TEXT_STILL_MS) and Time.get_ticks_msec() < deadline:
		await process_frame
		var now := JSON.stringify(_visible_texts())
		if now == last:
			same += 1
		else:
			same = 0
			since = Time.get_ticks_msec()
		last = now

func _capture_frame(seq: int, index: int) -> void:
	await _wait_camera_stable()
	await _wait_text_stable()
	if not await _await_drawn():
		render_stalled = true
	var shot := "%04d-%02d.png" % [seq, index]
	root.get_texture().get_image().save_png(directory.path_join(shot))
	var frame := {"screenshot": shot, "visible_text": _visible_texts(),
		"elapsed_ms": Time.get_ticks_msec() - started_ms}
	frames.append(frame)
	_append({"event": "frame", "seq": seq, "frame": frame})

func _wait_camera_stable() -> void:
	var last := root.get_canvas_transform()
	var still := 0
	for i in MAX_STABLE_WAIT:
		await process_frame
		var now := root.get_canvas_transform()
		if now.is_equal_approx(last):
			still += 1
			if still >= STABLE_FRAMES:
				return
		else:
			still = 0
		last = now

func _publish(seq: int, name: String, error: String) -> void:
	await _wait_camera_stable()
	if seq > 0:
		await _wait_text_stable()
	if not await _await_drawn() or render_stalled:
		render_stalled = false
		error = (error + "; " if error != "" else "") + "render stalled: no frame drawn within %d ms (display disconnected?)" % DRAW_TIMEOUT_MS
	var shot := "%04d-%s.png" % [seq, name.validate_filename()]
	root.get_texture().get_image().save_png(directory.path_join(shot))
	controls_by_id.clear()
	var controls := []
	var texts := []
	_scan(root, controls, texts)
	ack_seq = seq
	var status := {
		"session": session, "run_id": run_id, "ack_seq": seq, "command_seq": seq,
		"accepted": true, "error": error, "name": name, "screenshot": shot,
		"elapsed_ms": Time.get_ticks_msec() - started_ms,
		"controls": controls, "visible_text": texts, "frames": frames,
		"pending": {"command_running": false},
	}
	_write_json("status.json", status)
	_append({"event": "snapshot", "status": status})

func _status_pending(seq: int) -> void:
	_append({"event": "input_started", "seq": seq, "elapsed_ms": Time.get_ticks_msec() - started_ms})

func _reject(seq: int, message: String) -> void:
	var status := {
		"session": session, "run_id": run_id, "ack_seq": ack_seq, "command_seq": seq,
		"accepted": false, "error": message, "pending": {"command_running": false},
	}
	_write_json("status.json", status)
	_append({"event": "rejected", "status": status})

func _scan(node: Node, controls: Array, texts: Array) -> void:
	if node is CanvasItem and not (node as CanvasItem).is_visible_in_tree():
		return
	if node is Control:
		var control := node as Control
		var rect := control.get_global_rect()
		var label := _text_of(control)
		var interactive := control is BaseButton or control is LineEdit or control is TextEdit or control is ItemList or control is Slider
		if interactive and not (control is BaseButton and (control as BaseButton).disabled and label == ""):
			var id := "c%d" % (controls.size() + 1)
			controls_by_id[id] = control
			controls.append({
				"control_id": id, "kind": control.get_class(), "text": label,
				"disabled": control is BaseButton and (control as BaseButton).disabled,
				"rect": [roundi(rect.position.x), roundi(rect.position.y), roundi(rect.size.x), roundi(rect.size.y)],
			})
		elif label != "" and (control is Label or control is RichTextLabel):
			texts.append({"text": label, "rect": [roundi(rect.position.x), roundi(rect.position.y), roundi(rect.size.x), roundi(rect.size.y)]})
	for child in node.get_children():
		_scan(child, controls, texts)

func _text_of(control: Control) -> String:
	if control is RichTextLabel:
		var rich := control as RichTextLabel
		var text := rich.get_parsed_text()
		if rich.visible_characters >= 0:
			text = text.substr(0, rich.visible_characters)
		return text.strip_edges()
	if control is Label:
		var label := control as Label
		var shown := label.atr(label.text)
		if label.visible_characters >= 0:
			shown = shown.substr(0, label.visible_characters)
		return shown.strip_edges()
	if control is Button:
		return control.atr((control as Button).text).strip_edges()
	if control is LineEdit:
		return (control as LineEdit).text
	if control is TextEdit:
		return (control as TextEdit).text
	return ""

func _write_json(file: String, value: Dictionary) -> void:
	var temporary := directory.path_join(file + ".tmp")
	var stream := FileAccess.open(temporary, FileAccess.WRITE)
	stream.store_string(JSON.stringify(value, "\t"))
	stream.close()
	DirAccess.rename_absolute(temporary, directory.path_join(file))

func _append(row: Dictionary) -> void:
	var path := directory.path_join("timeline.jsonl")
	var stream := FileAccess.open(path, FileAccess.READ_WRITE) if FileAccess.file_exists(path) else FileAccess.open(path, FileAccess.WRITE)
	stream.seek_end()
	stream.store_line(JSON.stringify(row))
	stream.close()
