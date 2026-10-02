@tool
extends EditorPlugin

const StoryEditor = preload("res://addons/story_editor/editor.gd")
var screen: Control

func _enter_tree() -> void:
	screen = StoryEditor.new()
	EditorInterface.get_editor_main_screen().add_child(screen)
	_make_visible(false)

func _exit_tree() -> void:
	if is_instance_valid(screen):
		screen.queue_free()

func _has_main_screen() -> bool:
	return true

func _get_plugin_name() -> String:
	return "剧情"

func _get_plugin_icon() -> Texture2D:
	return EditorInterface.get_base_control().get_theme_icon("Animation", "EditorIcons")

func _make_visible(visible: bool) -> void:
	if is_instance_valid(screen):
		screen.visible = visible
