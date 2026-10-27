extends "res://scenes/World/Art/HallCraft.gd"
## Permanent character art studio; no map construction or gameplay state.
@export var inspect_imports := false
var camera: Camera3D

func _ready() -> void:
	get_viewport().msaa_3d = Viewport.MSAA_4X
	var environment := WorldEnvironment.new()
	environment.environment = Environment.new()
	environment.environment.background_mode = Environment.BG_COLOR
	environment.environment.background_color = Color("252c2e")
	environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.environment.ambient_light_color = Color("d5dce0")
	environment.environment.ambient_light_energy = 0.35
	environment.environment.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	environment.environment.ssao_enabled = true
	environment.environment.ssao_radius = 0.3
	environment.environment.ssao_intensity = 0.7
	add_child(environment)
	var light := DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-55,-25,0)
	light.light_energy = 0.8
	light.light_color = Color("fff0dc")
	light.shadow_enabled = true
	add_child(light)
	camera = Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 11.0
	camera.position = Vector3(0,7,15)
	add_child(camera)
	camera.look_at(Vector3(0,0.6,0))
	camera.current = true
	content_root = Node3D.new()
	add_child(content_root)
	var index := 0
	for identity in (range(1,18) if inspect_imports else CAST.PEOPLE.keys()):
		var x: float = (index%5-2.0)*1.8
		var z: float = floorf(index/5.0)*2.1-3.15
		if inspect_imports:
			var root := group(str(identity), Vector3(x,0,z))
			preload("res://scenes/World/Actors/ImportedCharacter.gd").attach(root, "res://ShareAsset/Internal/Characters/UserModels/model_%02d.glb" % identity, 1.32, -90)
		else:
			_character(CAST.PEOPLE[identity][0],x,z,identity,1.0)
		var caption := Label3D.new()
		caption.text = str(identity) if inspect_imports else CAST.PEOPLE[identity][0]
		caption.position = Vector3(x,0.015,z+0.45)
		caption.rotation_degrees.x = -55
		caption.font_size = 30
		caption.pixel_size = 0.0035
		caption.no_depth_test = true
		content_root.add_child(caption)
		index += 1
	box(content_root,Vector3(0,-0.11,0),Vector3(10,0.2,9),material("studio_floor","65706a",2))
