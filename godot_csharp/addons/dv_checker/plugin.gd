@tool
extends EditorPlugin

const PANEL = preload("res://addons/dv_checker/panel.gd")
var panel: VBoxContainer

func _enter_tree() -> void:
	add_tool_menu_item("Open DV source…", _open_source)

func _open_source() -> void:
	if not is_instance_valid(panel):
		panel = PANEL.new()
		panel.name = "DV"
		panel.configure(get_editor_interface().get_editor_settings())
		add_control_to_bottom_panel(panel, "DV")
	make_bottom_panel_item_visible(panel)
	panel.choose_file()

func _exit_tree() -> void:
	remove_tool_menu_item("Open DV source…")
	if is_instance_valid(panel):
		remove_control_from_bottom_panel(panel)
		panel.shutdown()
		panel.queue_free()
