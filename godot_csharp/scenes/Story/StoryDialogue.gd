extends CanvasLayer
## Shared Dialogue Manager presentation; story content stays in .dialogue resources.
signal finished
signal line_shown(character: String)

const INK := Color("191e23")
const PAPER := Color("e8dfca")
const GOLD := Color("b59a68")
var manager: Node
var resource: Resource
var context: Node
var line
var busy := false
var active := false
var speaker: Label
var body: RichTextLabel
var choices: HBoxContainer
var next_button: Button
var hint: Label
var heading: Label
var panel: Panel
var history_panel: Panel
var history_text: RichTextLabel
var history: Array[String] = []
var elapsed := 0.0

func _ready() -> void:
	layer = 10
	manager = get_node("/root/DialogueManager")
	var font := SystemFont.new()
	font.font_names = PackedStringArray(["Microsoft YaHei UI", "Noto Sans CJK SC"])
	var theme := Theme.new()
	theme.default_font = font
	theme.default_font_size = 18
	theme.set_color("font_color", "Label", PAPER)
	theme.set_color("default_color", "RichTextLabel", PAPER)
	for state in ["normal", "hover", "pressed", "focus"]:
		var box := StyleBoxFlat.new()
		box.bg_color = Color("353630") if state == "hover" else INK
		box.border_color = GOLD if state in ["hover", "focus"] else Color("555044")
		box.set_border_width_all(1)
		box.content_margin_left = 16
		box.content_margin_right = 16
		box.content_margin_top = 9
		box.content_margin_bottom = 9
		theme.set_stylebox(state, "Button", box)
	theme.set_color("font_color", "Button", PAPER)
	panel = Panel.new()
	panel.theme = theme
	panel.position = Vector2(0, 576)
	panel.size = Vector2(1024, 224)
	var background := StyleBoxFlat.new()
	background.bg_color = INK
	background.border_color = GOLD
	background.border_width_top = 2
	panel.add_theme_stylebox_override("panel", background)
	panel.gui_input.connect(on_panel_input)
	add_child(panel)
	heading = label("序幕  /  中央厅 · 清晨", Vector2(36, 18), 13, GOLD)
	speaker = label("", Vector2(36, 50), 22, PAPER)
	body = RichTextLabel.new()
	body.position = Vector2(206, 48)
	body.size = Vector2(774, 84)
	body.bbcode_enabled = false
	body.scroll_active = false
	body.mouse_filter = Control.MOUSE_FILTER_IGNORE
	body.add_theme_font_size_override("normal_font_size", 20)
	panel.add_child(body)
	var rule := ColorRect.new()
	rule.position = Vector2(180, 52)
	rule.size = Vector2(1, 72)
	rule.color = Color("655943")
	rule.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.add_child(rule)
	choices = HBoxContainer.new()
	choices.position = Vector2(206, 143)
	choices.add_theme_constant_override("separation", 12)
	panel.add_child(choices)
	next_button = Button.new()
	next_button.text = "继续  ›"
	next_button.position = Vector2(860, 151)
	next_button.size = Vector2(128, 40)
	next_button.pressed.connect(advance)
	panel.add_child(next_button)
	hint = label("", Vector2(36, 196), 12, Color("9b998d"))
	var history_button := Button.new()
	history_button.text = "回看"
	history_button.position = Vector2(904, 10)
	history_button.pressed.connect(toggle_history)
	panel.add_child(history_button)
	history_panel = Panel.new()
	history_panel.theme = theme
	history_panel.position = Vector2(160, 72)
	history_panel.size = Vector2(704, 464)
	history_panel.add_theme_stylebox_override("panel", background)
	add_child(history_panel)
	history_text = RichTextLabel.new()
	history_text.position = Vector2(24, 24)
	history_text.size = Vector2(656, 378)
	history_panel.add_child(history_text)
	var close := Button.new()
	close.text = "返回对话  ·  H"
	close.position = Vector2(470, 411)
	close.pressed.connect(toggle_history)
	history_panel.add_child(close)
	history_panel.hide()
	idle("钟声将起", "走近音乐家，或先看看机械钟。")

func label(text: String, at: Vector2, size: int, color: Color) -> Label:
	var result := Label.new()
	result.text = text
	result.mouse_filter = Control.MOUSE_FILTER_IGNORE
	result.position = at
	result.add_theme_font_size_override("font_size", size)
	result.add_theme_color_override("font_color", color)
	panel.add_child(result)
	return result

func idle(title: String, text: String) -> void:
	speaker.text = title
	body.text = text
	body.visible_characters = -1
	next_button.hide()
	hint.text = "WASD / 方向键  行走     E / Enter  交谈或查看     H  回看     R  重演"

func start(story: Resource, title: String, state: Node) -> void:
	if active:
		return
	resource = story
	context = state
	active = true
	await show_line(title)

func show_line(key: String) -> void:
	busy = true
	next_button.hide()
	for child in choices.get_children():
		choices.remove_child(child)
		child.queue_free()
	line = await manager.get_next_dialogue_line(resource, key, [context])
	busy = false
	if line == null:
		active = false
		finished.emit()
		return
	speaker.text = line.character if not line.character.is_empty() else "中央厅"
	body.text = line.text
	body.visible_characters = 0
	elapsed = 0
	history.append(speaker.text + "\n" + line.text)
	line_shown.emit(line.character)
	hint.text = "空格 / Enter / 点击  继续     H  回看"
	for response in line.responses:
		if not response.is_allowed:
			continue
		var button := Button.new()
		button.text = response.text
		button.set_meta("dialogue_next_id", response.next_id)
		button.pressed.connect(_on_choice_pressed.bind(button))
		choices.add_child(button)
	choices.hide()

func choose(key: String) -> void:
	if busy or not active or history_panel.visible:
		return
	await show_line(key)

func _on_choice_pressed(button: Button) -> void:
	choose(str(button.get_meta("dialogue_next_id")))

func advance() -> void:
	if busy or not active or history_panel.visible:
		return
	if choices.visible and choices.get_child_count() > 0:
		return
	if body.visible_characters >= 0:
		reveal()
	elif line.responses.is_empty():
		await show_line(line.next_id)

func reveal() -> void:
	body.visible_characters = -1
	choices.show()
	next_button.visible = line.responses.is_empty()
	if choices.get_child_count() > 0:
		choices.get_child(0).grab_focus()

func _process(delta: float) -> void:
	if active and not busy and not history_panel.visible and body.visible_characters >= 0:
		elapsed += delta
		body.visible_characters = int(elapsed * 32)
		if body.visible_characters >= body.get_total_character_count():
			reveal()

func toggle_history() -> void:
	history_panel.visible = not history_panel.visible
	history_text.text = "对话记录\n\n" + "\n\n".join(history)

func on_panel_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		advance()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_H:
			toggle_history()
			get_viewport().set_input_as_handled()
		elif active and event.keycode in [KEY_SPACE, KEY_ENTER]:
			if event.keycode == KEY_ENTER and choices.visible and choices.get_child_count() > 0:
				var focused := get_viewport().gui_get_focus_owner()
				var choice: Button
				if focused is Button and choices.is_ancestor_of(focused):
					choice = focused
				else:
					choice = choices.get_child(0) as Button
				choose(str(choice.get_meta("dialogue_next_id")))
			else:
				advance()
			get_viewport().set_input_as_handled()
	elif active and event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT and event.position.y >= 576:
		advance()
		get_viewport().set_input_as_handled()
