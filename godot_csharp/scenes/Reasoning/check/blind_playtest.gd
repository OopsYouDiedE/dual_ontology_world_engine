extends SceneTree

## Independent blind-playtest harness. It drives only public scene input/UI.
## No case implementation classes, answer data, or evidence state are read.
var lines: Array[String] = []
var out_path := "res://scenes/Reasoning/check/blind-playtest-runtime.txt"

func _init() -> void:
	call_deferred("_play")

func frames(count: int) -> void:
	for i in range(count): await physics_frame

func key(code: Key, down: bool) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = down
	Input.parse_input_event(event)

func visible_text() -> void:
	_collect_visible_text(current_scene, lines)

func _collect_visible_text(node: Node, sink: Array[String]) -> void:
	if node is Control and node.visible:
		if node is Label or node is Button or node is RichTextLabel or node is LineEdit or node is TextEdit:
			var value := str(node.get("text"))
			if not value.is_empty(): sink.append("%s: %s" % [node.get_path(), value.replace("\n", "\\n")])
	for child in node.get_children(): _collect_visible_text(child, sink)

func buttons() -> Array[Button]:
	var result: Array[Button] = []
	for node in current_scene.find_children("*", "Button", true, false):
		if node.visible and not node.disabled: result.append(node as Button)
	return result

func _play() -> void:
	change_scene_to_file("res://scenes/Reasoning/Reasoning.tscn")
	await frames(25)
	lines.append("=== opening visible text ===")
	visible_text()
	var detective := current_scene.find_child("Detective", true, false) as CharacterBody2D
	# Teleport is a debug convenience only; no evidence or state is changed directly.
	for i in range(4):
		var npc := current_scene.find_child("Actor" + str(i), true, false) as CharacterBody2D
		if npc == null: continue
		detective.position = npc.position + Vector2(0, 32)
		await frames(2)
		key(KEY_E, true); key(KEY_E, false)
		await frames(12)
		lines.append("=== Actor%d dialogue ===" % i)
		visible_text()
		var snapshot: Array[Button] = []
		for b in buttons():
			if b.text != "关闭" and b.text != "": snapshot.append(b)
		for b in snapshot:
			if is_instance_valid(b) and b.visible:
				lines.append("CLICK Actor%d: %s" % [i, b.text])
				b.pressed.emit()
				await frames(8)
				visible_text()
		var close := current_scene.find_child("CloseConversation", true, false) as Button
		if close and close.visible: close.pressed.emit()
		await frames(4)
	# Read the note UI through its actual button, then leave it untouched.
	for b in buttons():
		if b.text.contains("札记") or b.name == "NotebookButton":
			lines.append("CLICK notebook: %s" % b.text); b.pressed.emit(); await frames(8); visible_text(); break
	var file := FileAccess.open(out_path, FileAccess.WRITE)
	if file:
		file.store_string("\n".join(lines) + "\n")
		file.close()
	quit()
