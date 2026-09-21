extends SceneTree

func _initialize() -> void:
	push_error("请使用 tools/resized48/rebuild.ps1 重建两个布局及 TileSet。")
	quit(1)
