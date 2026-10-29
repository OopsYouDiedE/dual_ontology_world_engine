extends Control

## Material 3 Inventory Interface (Task 7.09)
## Shows item name, count, holder, usage, and handover/inspect actions.
## Strictly matches Core inventory counts; strictly prevents client-side item duplication.

signal handover_requested(item_id: String, target_character: String)
signal item_inspect_requested(item_id: String)
signal inventory_closed

const MaterialControls := preload("res://scenes/UI/Components/MaterialControls.gd")

@onready var modal_backdrop: ColorRect = %Backdrop
@onready var main_card: PanelContainer = %MainCard
@onready var title_label: Label = %TitleLabel
@onready var close_btn: Button = %CloseButton
@onready var items_list: VBoxContainer = %ItemsList
@onready var items_scroll: ScrollContainer = %ItemsScroll
@onready var empty_tip: Label = %EmptyTip

var _is_open: bool = false
var _items_cache: Array = []
var _characters_nearby: Array[String] = []

func _ready() -> void:
	visible = false
	MaterialControls.apply(self)
	MaterialControls.prepare_button(close_btn)
	close_btn.pressed.connect(close_inventory)

func is_inventory_open() -> bool:
	return _is_open

## Opens inventory with authoritative Core data:
## data format:
## {
##   "items": [
##     {"id": "item_key_2f", "name": "2F 通行钥匙", "count": 1, "holder": "侦探", "usage": "可开启 2F 隔离门与病房", "can_handover": true},
##     {"id": "item_bandage", "name": "干净纱布", "count": 3, "holder": "侦探", "usage": "包扎伤口与止血", "can_handover": true}
##   ],
##   "nearby_characters": ["苏泠", "沈鼎"]
## }
func open_inventory(data: Dictionary) -> void:
	_items_cache = data.get("items", []).duplicate(true)
	_characters_nearby.assign(data.get("nearby_characters", []))
	
	_rebuild_inventory_list()
	
	visible = true
	_is_open = true
	
	await get_tree().process_frame
	close_btn.grab_focus()

func _rebuild_inventory_list() -> void:
	for child in items_list.get_children():
		items_list.remove_child(child)
		child.queue_free()
		
	if _items_cache.is_empty():
		empty_tip.visible = true
		return
	empty_tip.visible = false
	
	for it in _items_cache:
		if not it is Dictionary:
			continue
		var item_id: String = str(it.get("id", ""))
		var item_name: String = str(it.get("name", it.get("title", item_id)))
		# Authoritative count from Core, never locally modified (Task 7.09)
		var count: int = int(it.get("count", 1))
		var holder: String = str(it.get("holder", "本人"))
		var usage: String = str(it.get("usage", it.get("desc", "未知用途")))
		var can_handover: bool = bool(it.get("can_handover", false))
		
		var card := PanelContainer.new()
		var box := StyleBoxFlat.new()
		box.bg_color = MaterialControls.COLOR_SURFACE_VARIANT
		box.corner_radius_top_left = 8
		box.corner_radius_top_right = 8
		box.corner_radius_bottom_right = 8
		box.corner_radius_bottom_left = 8
		box.content_margin_left = 16.0
		box.content_margin_top = 10.0
		box.content_margin_right = 16.0
		box.content_margin_bottom = 10.0
		card.add_theme_stylebox_override("panel", box)
		
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 16)
		
		var col := VBoxContainer.new()
		col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		col.add_theme_constant_override("separation", 4)
		
		var title_row := HBoxContainer.new()
		var name_lbl := Label.new()
		name_lbl.text = item_name
		name_lbl.add_theme_font_size_override("font_size", 16)
		title_row.add_child(name_lbl)
		
		var count_badge := MaterialControls.create_badge("×%d" % count, "primary")
		title_row.add_child(count_badge)
		col.add_child(title_row)
		
		var meta_lbl := Label.new()
		meta_lbl.text = "持有人：%s  ·  用途：%s" % [holder, usage]
		meta_lbl.add_theme_font_size_override("font_size", 13)
		meta_lbl.add_theme_color_override("font_color", MaterialControls.COLOR_OUTLINE)
		col.add_child(meta_lbl)
		
		row.add_child(col)
		
		# Inspect button
		var inspect_btn := Button.new()
		MaterialControls.prepare_button(inspect_btn)
		inspect_btn.text = "检视"
		inspect_btn.custom_minimum_size = Vector2(64, 40)
		inspect_btn.pressed.connect(func(): _on_inspect_pressed(item_id))
		row.add_child(inspect_btn)
		
		# Handover button (Task 7.09: Handover action)
		if can_handover and not _characters_nearby.is_empty():
			var handover_btn := MenuButton.new()
			handover_btn.text = "交接"
			handover_btn.custom_minimum_size = Vector2(64, 40)
			var popup := handover_btn.get_popup()
			for char_name in _characters_nearby:
				popup.add_item("交接给 " + char_name)
			popup.id_pressed.connect(func(idx: int):
				if idx >= 0 and idx < _characters_nearby.size():
					_on_handover_selected(item_id, _characters_nearby[idx])
			)
			row.add_child(handover_btn)
			
		card.add_child(row)
		items_list.add_child(card)

func _on_inspect_pressed(item_id: String) -> void:
	item_inspect_requested.emit(item_id)

func _on_handover_selected(item_id: String, target_char: String) -> void:
	# Dispatches request to Core; does not mutate local numbers to prevent item duplication
	handover_requested.emit(item_id, target_char)

func close_inventory() -> void:
	if not _is_open:
		return
	_is_open = false
	visible = false
	inventory_closed.emit()

func _gui_input(event: InputEvent) -> void:
	if not _is_open:
		return
	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("cancel"):
		close_inventory()
		get_viewport().set_input_as_handled()
