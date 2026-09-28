extends CanvasLayer
const PATHS := ["res://scenes/Story/CentralHall.tscn", "res://scenes/Story/Breakfast.tscn", "res://scenes/Story/ParlorGathering.tscn"]
var buttons: Array[Button] = []
var row: HBoxContainer

func _ready() -> void:
	layer = 11
	row = HBoxContainer.new()
	row.position = Vector2(808, 774)
	add_child(row)
	var font := SystemFont.new()
	font.font_names = PackedStringArray(["Microsoft YaHei UI", "Noto Sans CJK SC"])
	for i in 3:
		var button := Button.new()
		button.text = ["F6 中央厅", "F7 早餐厅", "F8 娱乐室"][i]
		button.add_theme_font_override("font", font)
		button.add_theme_font_size_override("font_size", 13)
		button.pressed.connect(open_room.bind(i))
		row.add_child(button)
		buttons.append(button)

func _process(_delta: float) -> void:
	var room := get_parent()
	var blocked: bool = room.dialogue.active or room.dialogue.history_panel.visible or room.montage_playing
	row.visible = not room.montage_playing
	for i in buttons.size():
		buttons[i].disabled = blocked or room.scene_file_path == PATHS[i]

func open_room(index: int) -> void:
	var room := get_parent()
	if not room.dialogue.active and not room.dialogue.history_panel.visible and not room.montage_playing and not buttons[index].disabled:
		get_tree().change_scene_to_file(PATHS[index])

func _unhandled_input(event: InputEvent) -> void:
	var room := get_parent()
	if room.dialogue.active or room.dialogue.history_panel.visible or room.montage_playing:
		return
	if event is InputEventKey and event.pressed and not event.echo and event.keycode in [KEY_F6, KEY_F7, KEY_F8]:
		get_viewport().set_input_as_handled()
		open_room(event.keycode - KEY_F6)
