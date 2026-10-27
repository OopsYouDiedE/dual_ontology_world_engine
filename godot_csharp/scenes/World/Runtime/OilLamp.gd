extends Node2D
## Authored static lamp body, independent flame and synchronized real 2D light.
var light: PointLight2D
var flame_height := 19.0
var phase := 0.0
var elapsed := 0.0
var base_energy := 0.58
var light_origin := Vector2.ZERO
var flicker := 0.0
var flame_scale := 1.0

func configure(declaration: Dictionary, source_light: PointLight2D) -> void:
	light = source_light
	base_energy = light.energy
	light_origin = light.position
	phase = float(declaration.get("phase", 0.0))
	flame_height = float(declaration.get("flame_height", 19.0))
	var body := Sprite2D.new()
	body.texture = load(str(declaration.texture))
	body.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	body.centered = false
	body.offset = -Vector2(float(declaration.foot[0]), float(declaration.foot[1]))
	var ratio := float(declaration.get("width", 15.0)) / body.texture.get_width()
	body.scale = Vector2.ONE * ratio
	add_child(body)
	# Flame is emissive, while the body still receives the room lighting.
	var flame := Sprite2D.new()
	flame.name = "Flame"
	flame.texture = load(str(declaration.flame_texture))
	flame.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	flame.centered = false
	flame.offset = -Vector2(float(declaration.flame_foot[0]), float(declaration.flame_foot[1]))
	flame_scale = 5.0 / flame.texture.get_height()
	flame.position = Vector2(0, -flame_height)
	flame.scale = Vector2.ONE * flame_scale
	var glow_material := CanvasItemMaterial.new()
	glow_material.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
	flame.material = glow_material
	add_child(flame)
	set_process(true)

func _process(delta: float) -> void:
	elapsed += delta
	# Continuous time, fixed individual phases: no per-frame random strobing.
	var t := elapsed + phase
	flicker = 0.58 * sin(t * 7.1) + 0.27 * sin(t * 11.3 + 1.2) + 0.15 * sin(t * 17.7 + 0.4)
	if is_instance_valid(light):
		light.energy = base_energy * (1.0 + flicker * 0.075)
		light.position = light_origin + Vector2(sin(t * 5.7) * 0.35, flicker * 0.22)
		light.texture_scale = 0.95 * (1.0 + flicker * 0.018)
	var flame := get_node("Flame") as Sprite2D
	flame.scale = Vector2(1.0 - flicker * 0.025, 1.0 + flicker * 0.08) * flame_scale
	flame.rotation = sin(t * 5.7) * 0.035
	flame.modulate.a = 0.94 + flicker * 0.06
