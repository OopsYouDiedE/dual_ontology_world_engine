extends Node2D
## Permanent sprites, feet and collision come from the native art recipe.
const DATA: Resource = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/red_mechanisms.tres")
const ASSETS := "res://ShareAsset/Internal/Props/FirstAwakeningInterior/red_"
var source: Node
var test_state := {}
var mounted := {}
func _ready() -> void:
	z_index = -1
	for old_name in DATA.data.replaced:
		var old := get_parent().get_node_or_null(old_name)
		if old:
			old.hide()
			_disable_old_collision(old)
	for item in DATA.data.fixtures:
		var sprite := Sprite2D.new()
		sprite.name = "RedMechanism_" + item.name
		sprite.texture = load(ASSETS + item.name + ".png")
		sprite.centered = false
		sprite.position = Vector2(item.position[0],item.position[1])
		if item.get("floor_layer",false): sprite.z_index = -1
		sprite.offset = Vector2(-sprite.texture.get_width()/2.0,-sprite.texture.get_height())
		if item.footprint != null:
			var r: Array = item.footprint
			var body := StaticBody2D.new()
			body.position = Vector2(r[0]+r[2]/2.0,r[1]+r[3]/2.0) - sprite.position
			var shape := CollisionShape2D.new()
			var rect := RectangleShape2D.new()
			rect.size = Vector2(r[2],r[3])
			shape.shape = rect
			body.add_child(shape)
			sprite.add_child(body)
		if item.has("owner") and mounted.has(item.owner):
			var support: Sprite2D = mounted[item.owner]
			sprite.position -= support.position
			support.call_deferred("add_child",sprite)
		else:
			get_parent().call_deferred("add_child",sprite)
		mounted[item.name] = sprite
func _disable_old_collision(node: Node) -> void:
	for child in node.get_children():
		if child is CollisionShape2D: child.set_deferred("disabled",true)
		else: _disable_old_collision(child)
func _process(_delta: float) -> void:
	if mounted.has("spring_handle"):
		mounted.spring_handle.texture = load(ASSETS + ("spring_handle_pressed" if test_state.get("pressed",false) else "spring_handle") + ".png")
	if mounted.has("reward_cabinet"):
		mounted.reward_cabinet.modulate = Color(1.08,1.08,1.0) if int(test_state.get("rewards_verified",0)) > 0 else Color.WHITE
	queue_redraw()
func _draw() -> void:
	# Only transient sound and test output marks are drawn here.
	if test_state.get("gap_sound",false): draw_arc(Vector2(604,910),12,-1.0,1.0,8,Color("9ab0bd"),2)
	if test_state.get("pipe_sound",false): draw_arc(Vector2(456,704),12,-1.0,1.0,8,Color("9ab0bd"),2)
	if test_state.get("red_received",false): draw_rect(Rect2(536,699,4,5),Color("b84242"))
	if test_state.get("white",false): draw_rect(Rect2(542,722,12,8),Color("e3d4af"))
