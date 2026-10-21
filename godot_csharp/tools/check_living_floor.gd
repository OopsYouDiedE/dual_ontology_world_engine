extends SceneTree
## Resource/geometry check only: does not open or execute a DV session.
func _initialize() -> void:
	var packed := load("res://scenes/Chapter2/Maps/living_floor.tscn") as PackedScene
	if packed == null:
		quit(1)
		return
	var floor := packed.instantiate()
	var valid := true
	for exit in floor.get_node("Exits").get_children():
		if not exit.get_node_or_null("Shape") is CollisionShape2D:
			push_error("Missing exit shape: " + str(exit.name))
			valid = false
	for shape in floor.get_node("Solids").get_children():
		if not shape is CollisionShape2D or shape.shape == null:
			push_error("Invalid floor collision: " + str(shape.name))
			valid = false
	print("Living floor resource/exit/collision structure: ", valid)
	floor.free()
	quit(0 if valid else 1)
