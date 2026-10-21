extends Node

var host: Node
var previous_draw := 0
var locations: Dictionary = {}
var first_visible: Dictionary = {}
var process_begin := 0
var render_begin := 0
var last_input_state: Dictionary = {}
const FRAME_GAP_US := 50000

func configure(value: Node, enabled := false) -> void:
	host = value
	process_priority = -10000
	set_enabled(enabled)

func set_enabled(enabled: bool) -> void:
	set_process(enabled)
	if enabled:
		if not RenderingServer.frame_pre_draw.is_connected(_before_render): RenderingServer.frame_pre_draw.connect(_before_render)
		if not RenderingServer.frame_post_draw.is_connected(_drawn): RenderingServer.frame_post_draw.connect(_drawn)
	else:
		if RenderingServer.frame_pre_draw.is_connected(_before_render): RenderingServer.frame_pre_draw.disconnect(_before_render)
		if RenderingServer.frame_post_draw.is_connected(_drawn): RenderingServer.frame_post_draw.disconnect(_drawn)
		previous_draw = 0

func _exit_tree() -> void:
	set_enabled(false)

func _process(_delta: float) -> void:
	if is_instance_valid(host): process_begin = host.engine.TimingClockUs()

func _before_render() -> void:
	if is_instance_valid(host): render_begin = host.engine.TimingClockUs()

func _drawn() -> void:
	if not is_instance_valid(host) or host.flow.closing: return
	var now: int = host.engine.TimingClockUs()
	if previous_draw > 0 and now - previous_draw >= FRAME_GAP_US:
		host._timing("frame_gap", {"begin_us": previous_draw, "end_us": now, "duration_us": now - previous_draw, "threshold_us": FRAME_GAP_US, "process_begin_us": process_begin, "render_begin_us": render_begin})
	previous_draw = now
	if not is_instance_valid(host.field): return
	var field: Node2D = host.field
	if not is_instance_valid(field.adapter): return
	var spatial: Node = field.spatial
	var input_state := {"map": str(field.adapter.map_key()), "room": str(field.snapshot.get("execution_room", "")),
		"ui_busy": is_instance_valid(host.ui) and host.ui.is_busy(), "reading": host.flow.reading,
		"background_execution": host.flow.background_execution, "draining": host.flow.draining,
		"queued_events": host._queue.size(), "map_switching": field.adapter.switching(),
		"submitted": is_instance_valid(spatial) and spatial.submitted,
		"options": spatial.options.size() if is_instance_valid(spatial) else 0,
		"input_enabled": spatial.enabled() if is_instance_valid(spatial) else false,
		"formal_selection_ready": host._formal_selection_ready(),
		"intent_actor": str(host._dv_intent.get("actor", "")), "intent_action": str(host._dv_intent.get("action", {}).get("id", "")),
		"physical_jobs": field._daily.keys()}
	if input_state != last_input_state:
		last_input_state = input_state.duplicate(true)
		host._timing("input_gate_state", input_state)
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

