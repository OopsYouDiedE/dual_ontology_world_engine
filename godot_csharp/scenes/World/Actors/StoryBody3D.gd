extends CharacterBody3D
## Retained metre-scale collision body; WorldStage3D executes measured movement.
var visual: Node3D

func configure(model: Dictionary, height: float, radius: float) -> bool:
	collision_layer = 2
	collision_mask = 3
	floor_snap_length = 0.2
	var collider := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = radius
	capsule.height = height
	collider.shape = capsule
	collider.position.y = height * 0.5
	add_child(collider)
	visual = Node3D.new()
	visual.name = "Visual"
	add_child(visual)
	return preload("res://scenes/World/Actors/ImportedCharacter.gd").attach(visual, str(model.path), height, float(model.get("yaw_degrees", 0)))

