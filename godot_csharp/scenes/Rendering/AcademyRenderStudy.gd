extends Node3D
## Isolated presentation assembly; no campaign state or forced renderer overrides.
func _ready() -> void:
	$View/Viewport/CameraRig.camera.make_current()
	var layer := CanvasLayer.new()
	add_child(layer)
	var row := HBoxContainer.new()
	row.position = Vector2(20,20)
	layer.add_child(row)
	for spec in [["全景",0],["门与桌椅",1],["像素密度",2]]:
		var b := Button.new()
		b.text = spec[0]
		b.pressed.connect(_view.bind(spec[1]))
		row.add_child(b)
	var label := Label.new()
	label.text = "程序化建筑样场 · 独立场景 · " + RenderingServer.get_current_rendering_method()
	row.add_child(label)

func _view(index: int) -> void:
	var rig = $View/Viewport/CameraRig
	if index == 2:
		$View.set_resolution(Vector2i(640,360) if $View.base_resolution.x == 480 else Vector2i(480,270))
		return
	rig.position = Vector3(-0.4,2.0,1.3) if index == 0 else Vector3(-1.8,1.7,1.0)
	rig.yaw = 18.0 if index == 0 else 8.0
	rig.pitch = 16.0 if index == 0 else 12.0
	rig.distance = 11.2 if index == 0 else 7.0
