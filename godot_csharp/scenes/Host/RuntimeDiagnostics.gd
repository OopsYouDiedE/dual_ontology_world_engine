extends Node
## Passive wall-clock observations. No world inputs, decisions or state writes.
var host: Node
var previous_draw := 0
var locations: Dictionary = {}
var first_visible: Dictionary = {}
var process_begin := 0
var render_begin := 0
const FRAME_GAP_US := 50000

func configure(value: Node) -> void:
	host = value
	process_priority = -10000
	RenderingServer.frame_pre_draw.connect(_before_render)
	RenderingServer.frame_post_draw.connect(_drawn)

func _process(_delta: float) -> void:
	if is_instance_valid(host): process_begin = host.engine.TimingClockUs()

func _before_render() -> void:
	if is_instance_valid(host): render_begin = host.engine.TimingClockUs()

func _drawn() -> void:
	if not is_instance_valid(host) or host._closing: return
	var now: int = host.engine.TimingClockUs()
	if previous_draw > 0 and now - previous_draw >= FRAME_GAP_US:
		host._timing("frame_gap", {"begin_us": previous_draw, "end_us": now, "duration_us": now - previous_draw, "threshold_us": FRAME_GAP_US, "process_begin_us": process_begin, "render_begin_us": render_begin})
	previous_draw = now
	if not is_instance_valid(host.field): return
	var field: Node2D = host.field
	if not is_instance_valid(field.adapter): return
	var viewport_rect := field.get_viewport_rect()
	for key in field.actors:
		var actor = field.actors[key]
		if not is_instance_valid(actor): continue
		var room := str(field.adapter.entered_room(actor.position)) if field.adapter.has_method("entered_room") else ""
		var location := str(field.adapter.map_key()) + "/" + room
		if room != "" and locations.get(key, "") != location:
			locations[key] = location
			host._timing("physical_location", {"actor": key, "location": location, "position": [actor.position.x, actor.position.y], "initial": not first_visible.has("located:" + str(key))})
			first_visible["located:" + str(key)] = true
		var screen: Vector2 = actor.get_global_transform_with_canvas().origin
		if actor.is_visible_in_tree() and viewport_rect.has_point(screen):
			var visible_key := str(key) + "@" + location
			if not first_visible.has(visible_key):
				first_visible[visible_key] = true
				host._timing("actor_in_viewport", {"actor": key, "location": location, "screen": [screen.x, screen.y], "scope": "visible node and viewport bounds; occlusion/recognition require PNG review"})
