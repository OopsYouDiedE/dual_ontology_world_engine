extends Node2D

var source: Node2D
var clock := 0.0

func _ready() -> void:
	var unlit := CanvasItemMaterial.new()
	unlit.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
	material = unlit

func _process(delta: float) -> void:
	clock = fmod(clock + delta, 22.0)
	queue_redraw()

func _draw() -> void:
	if source == null or source.modal_open or source.dialogue.active or source.ended:
		return
	for id in source.sites:
		if not source._site_accessible(id): continue
		if source.has_method("inspection_hint_visible") and not source.inspection_hint_visible(id): continue
		if not source._has_new_at(id):
			continue
		var point: Vector2 = source.sites[id].point
		var actor_name: String = source.sites[id].title
		if source.actors.has(actor_name):
			point = source.actors[actor_name].position
		point.y -= 46 if source.actors.has(actor_name) else 12
		point = point.round()
		if source.steady_investigation_hints():
			# The investigation host needs a visible, steady mouse target.
			_spark(point,4)
			draw_rect(Rect2(point-Vector2(8,8),Vector2(17,17)),Color("d2bc85"),false,1)
			continue
		# Stagger crisp pixel glints instead of pulsing every object together.
		var phase := fmod(clock + float(posmod(id.hash(), 220)) / 100.0, 2.2)
		var frame := int(phase * 8.0)
		if frame < 8:
			var radius: int = [0,1,2,4,3,2,1,0][frame]
			if radius > 0: _spark(point, radius)
	if source.nearest != "":
		var point: Vector2 = source.sites[source.nearest].point
		var actor_name: String = source.sites[source.nearest].title
		if source.actors.has(actor_name):
			point = source.actors[actor_name].position
		draw_line(point+Vector2(-8,2), point+Vector2(8,2),Color(0.8,0.79,0.57,0.5),1)

func _spark(point: Vector2, radius: int) -> void:
	var edge := Color("756044")
	var light := Color("f4df9d")
	draw_rect(Rect2(point + Vector2(-radius, -1), Vector2(radius * 2 + 1, 3)), edge)
	draw_rect(Rect2(point + Vector2(-1, -radius), Vector2(3, radius * 2 + 1)), edge)
	draw_rect(Rect2(point + Vector2(-radius, 0), Vector2(radius * 2 + 1, 1)), light)
	draw_rect(Rect2(point + Vector2(0, -radius), Vector2(1, radius * 2 + 1)), light)
	draw_rect(Rect2(point, Vector2.ONE), Color("fff2c9"))
	if radius >= 3:
		draw_rect(Rect2(point + Vector2(5,-5), Vector2.ONE), light)
