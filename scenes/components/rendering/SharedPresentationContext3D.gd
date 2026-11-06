extends Node3D
## Main-owned camera, environment and immutable presentation-frame distribution.
## The surrounding viewport owns World3D, including isolated execution viewports.
var camera: Camera3D
var field: Node2D
var architecture: Node3D
var characters: Node3D
var session_generation: int
var map_generation: int
var frame: Dictionary = {}
var _snapshot_source: Dictionary = {}
var _frozen_fields: Dictionary = {}
var _room := ""
var _lighting_minute := -1.0

func configure(view: Node2D, map: Node3D, actors: Node3D, session: int, generation: int) -> void:
	field = view
	architecture = map
	characters = actors
	session_generation = session
	map_generation = generation
	camera = preload("res://scenes/components/rendering/TopDownCamera3D.gd").new()
	camera.name = "WorldCamera3D"
	add_child(camera)
	var environment := preload("res://scenes/components/rendering/ObserverEnvironment3D.gd").new()
	environment.name = "ObserverEnvironment3D"
	add_child(environment)

func _ready() -> void:
	camera.current = not field.execution_only

func _freeze(value: Variant) -> void:
	if value is Dictionary:
		if value.is_read_only(): return
		for child in value.values(): _freeze(child)
		value.make_read_only()
	elif value is Array:
		if value.is_read_only(): return
		for child in value: _freeze(child)
		value.make_read_only()

func distribute(snapshot: Dictionary) -> void:
	# Do not call Session.Project here: its observer cursor has already been consumed.
	# Existing host snapshots contain both physical and observer fields. Keeping two
	# named channels does not grant additional observer access to executor data.
	if is_same(snapshot, _snapshot_source): return
	# Core's delta projection retains unchanged field containers. Reuse their
	# frozen copies instead of duplicating the whole world for every clock pulse.
	var data := {}
	for key in snapshot:
		if _snapshot_source.has(key) and is_same(snapshot[key], _snapshot_source[key]) and _frozen_fields.has(key):
			data[key] = _frozen_fields[key]
		else:
			var value: Variant = snapshot[key]
			if value is Dictionary or value is Array:
				value = value.duplicate(true)
				_freeze(value)
			data[key] = value
	_snapshot_source = snapshot
	_frozen_fields = data
	frame = {"session_generation": session_generation, "map_id": str(field.adapter.map_key()),
		"map_generation": map_generation, "content_hash": str(data.get("content_hash", "")),
		"state_version": int(data.get("state_version", -1)), "physical": data, "observer": data}
	_freeze(frame)
	architecture.apply_world_projection(frame)
	characters.apply_world_projection(frame)
	architecture.apply_visibility_projection(frame)
	characters.apply_visibility_projection(frame)

func update_observer(identity: String, feet: Vector3) -> void:
	distribute(field.snapshot)
	if field.execution_only: return
	if identity != _room:
		_room = identity
		_lighting_minute = -1.0
		camera.environment = architecture.presentation_environment(identity)
	var observer_body: CharacterBody3D = characters.prepare_actor(field.player)
	if observer_body == null: return
	var sight_origin: Vector3 = observer_body.get_camera_anchor()
	architecture.apply_observer(identity, feet, frame.observer.get("building_doors", {}), sight_origin)
	camera.follow(feet)
	var minute := float(frame.observer.get("game_time", 720.0))
	if minute != _lighting_minute:
		architecture.apply_lighting(identity, frame.observer)
		_lighting_minute = minute
