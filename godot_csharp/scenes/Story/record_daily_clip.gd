extends SceneTree
## Movie Maker entry point. Example: --script res://scenes/Story/record_daily_clip.gd -- SmallLesson

const DURATIONS := {"SmallLesson": 4.0, "CentralHall": 7.0, "Breakfast": 7.2, "ParlorGathering": 4.0}

func _initialize() -> void:
	call_deferred("record")

func record() -> void:
	var arguments := OS.get_cmdline_user_args()
	if arguments.is_empty() or not DURATIONS.has(arguments[0]):
		push_error("Expected one of: SmallLesson, CentralHall, Breakfast, ParlorGathering")
		quit(1)
		return
	var room_name: String = arguments[0]
	var scene: PackedScene = load("res://scenes/Story/" + room_name + ".tscn")
	var room := scene.instantiate()
	root.add_child(room)
	current_scene = room
	room.play_montage()
	await create_timer(float(DURATIONS[room_name]) - 0.05).timeout
	quit()
