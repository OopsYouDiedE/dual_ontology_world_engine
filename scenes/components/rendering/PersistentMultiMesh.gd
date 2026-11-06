extends MultiMeshInstance3D
## Keep authoring instance data in scene properties, including headless exports.
## Rendering-server-only MultiMesh buffers are not retained by the dummy renderer.
@export var saved_transforms: Array[Transform3D] = []
@export var saved_custom_data := PackedColorArray()

func _ready() -> void:
	if multimesh == null or saved_transforms.is_empty(): return
	# Each placement owns transforms while geometry and materials remain shared.
	var restored := MultiMesh.new()
	restored.transform_format = MultiMesh.TRANSFORM_3D
	restored.mesh = multimesh.mesh
	restored.use_custom_data = not saved_custom_data.is_empty()
	restored.instance_count = saved_transforms.size()
	multimesh = restored
	for index in range(saved_transforms.size()):
		multimesh.set_instance_transform(index, saved_transforms[index])
		if index < saved_custom_data.size():
			multimesh.set_instance_custom_data(index, saved_custom_data[index])
