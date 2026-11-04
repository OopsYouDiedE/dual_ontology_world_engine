extends WorldEnvironment
func _ready() -> void:
	environment = Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color.BLACK
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("d8d0c0")
	environment.ambient_light_energy = 0.42
	environment.tonemap_mode = Environment.TONE_MAPPER_AGX
	environment.ssao_enabled = true
	environment.ssao_radius = 0.25
	environment.ssao_intensity = 0.45
	var sun := DirectionalLight3D.new()
	sun.name = "ObserverKeyLight"
	sun.light_color = Color("fff2dc")
	sun.light_energy = 0.95
	sun.shadow_enabled = true
	sun.shadow_opacity = 0.65
	sun.rotation_degrees = Vector3(-52.0, -28.0, 0.0)
	add_child(sun)
