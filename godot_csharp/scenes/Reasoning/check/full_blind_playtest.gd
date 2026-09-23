extends SceneTree

var seq := -1
var ready := false
var busy := false
var cmd_path := "res://../artifacts/tea-reasoning/full-blind-command.json"
var text_path := "res://../artifacts/tea-reasoning/full-blind-visible.txt"
var shot_path := "res://../artifacts/tea-reasoning/full-blind-screen.png"

func _initialize() -> void:
	call_deferred("begin_test")

func begin_test() -> void:
	change_scene_to_file("res://scenes/Reasoning/Reasoning.tscn")
	for i in 40: await physics_frame
	ready = true
	await _snapshot()

func _process(_delta: float) -> bool:
	if not ready or busy: return false
	if not FileAccess.file_exists(cmd_path): return false
	var f := FileAccess.open(cmd_path, FileAccess.READ)
	if f == null: return false
	var raw := f.get_as_text(); f.close()
	# A command file can be briefly incomplete while the player driver writes it.
	var parser := JSON.new()
	if parser.parse(raw) != OK: return false
	var parsed = parser.data
	if typeof(parsed) != TYPE_DICTIONARY: return false
	var n := int(parsed.get("seq", -1))
	if n <= seq: return false
	seq = n
	busy = true
	perform(parsed)
	return false

func perform(parsed: Dictionary) -> void:
	var kind := str(parsed.get("type", ""))
	if kind == "click":
		var p := Vector2(float(parsed.get("x", 0)), float(parsed.get("y", 0))); var m := InputEventMouseMotion.new(); m.position=p; Input.parse_input_event(m)
		var d := InputEventMouseButton.new(); d.button_index=MOUSE_BUTTON_LEFT; d.position=p; d.pressed=true; Input.parse_input_event(d)
		var u := InputEventMouseButton.new(); u.button_index=MOUSE_BUTTON_LEFT; u.position=p; u.pressed=false; Input.parse_input_event(u)
	elif kind == "key":
		for down in [true, false]:
			var e := InputEventKey.new(); e.keycode = OS.find_keycode_from_string(str(parsed.get("key", ""))); e.physical_keycode = e.keycode; e.ctrl_pressed = bool(parsed.get("ctrl", false)); e.pressed = down; Input.parse_input_event(e)
	elif kind == "text":
		for ch in str(parsed.get("text", "")):
			if ch == "\r": continue
			for down in [true, false]:
				var e := InputEventKey.new()
				e.keycode = KEY_ENTER if ch == "\n" else KEY_NONE
				e.unicode = 0 if ch == "\n" else ch.unicode_at(0)
				e.pressed = down; Input.parse_input_event(e)
	elif kind == "hover":
		var e := InputEventMouseMotion.new(); e.position=Vector2(float(parsed.get("x",0)),float(parsed.get("y",0))); Input.parse_input_event(e)
	elif kind == "quit":
		quit(); return
	for i in int(parsed.get("frames", 10)): await physics_frame
	await _snapshot()
	busy = false

func _snapshot() -> void:
	await RenderingServer.frame_post_draw
	var lines: Array[String] = []
	lines.append("Command: " + str(seq))
	_collect(get_root(), lines)
	var f := FileAccess.open(text_path, FileAccess.WRITE); if f: f.store_string("\n".join(lines) + "\n"); f.close()
	var vp := get_root().get_viewport(); var img := vp.get_texture().get_image(); if img: img.save_png(shot_path)

func _collect(node: Node, lines: Array[String]) -> void:
	if node is Control and node.is_visible_in_tree() and (node is Label or node is Button or node is RichTextLabel or node is LineEdit or node is TextEdit):
		var value := str(node.get("text")); if not value.is_empty(): lines.append("%s: %s" % [node.get_path(), value.replace("\n", "\\n")])
	for child in node.get_children(): _collect(child, lines)
