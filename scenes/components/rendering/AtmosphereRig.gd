extends Node3D
## Presentation-only lighting. World weather is supplied by the host separately.
@export var particles_enabled := true:
	set(value):
		particles_enabled = value
		if is_instance_valid(motes): motes.emitting = value
var motes: GPUParticles3D

func _ready() -> void:
	motes = $Dust
	motes.emitting = particles_enabled

func configure(camera_target: Vector3, outdoors: bool) -> void:
	position = camera_target + Vector3(0, 1.0, 0)
	if not is_instance_valid(motes): return
	motes.amount = 48 if outdoors else 24
	motes.visibility_aabb = AABB(Vector3(-8,-3,-8), Vector3(16,8,16))
	motes.emitting = particles_enabled
