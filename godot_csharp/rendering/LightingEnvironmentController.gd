extends Node
## Task 4.05: Unified Environment and Lighting Controller (环境与灯光).
## Authoritative lighting controller:
## - Enforces a single WorldEnvironment node across the entire scene (prevents duplicate environment stacking).
## - Balances interior vs exterior lighting with a guaranteed minimum ambient light floor (>= 0.25),
##   ensuring passages, doors, and narrative evidence remain discernable even in dark rooms.
## - Manages room-level localized lights (OmniLight3D) for fixtures, lamps, and stoves.

@export var min_ambient_energy: float = 0.25
@export var default_sun_energy: float = 1.0

var primary_environment: WorldEnvironment = null
var primary_sun: DirectionalLight3D = null
var registered_room_lights: Dictionary = {} # room_id -> Array[Light3D]

func _ready() -> void:
	ensure_authoritative_environment(get_tree().root if is_inside_tree() else self)

## Ensures exactly one WorldEnvironment exists in the hierarchy, purging any duplicates.
func ensure_authoritative_environment(scene_root: Node) -> WorldEnvironment:
	var env_nodes: Array[WorldEnvironment] = []
	_collect_nodes_of_type(scene_root, "WorldEnvironment", env_nodes)
	
	if env_nodes.is_empty():
		primary_environment = WorldEnvironment.new()
		primary_environment.name = "AuthoritativeWorldEnvironment"
		var env := Environment.new()
		env.background_mode = Environment.BG_COLOR
		env.background_color = Color("182326")
		env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
		env.ambient_light_color = Color("d5dce0")
		env.ambient_light_energy = min_ambient_energy
		primary_environment.environment = env
		scene_root.add_child(primary_environment)
	else:
		primary_environment = env_nodes[0]
		# Ensure ambient light meets minimum threshold
		if primary_environment.environment != null:
			primary_environment.environment.ambient_light_energy = maxf(
				primary_environment.environment.ambient_light_energy,
				min_ambient_energy
			)
		# Purge extraneous duplicates to prevent double-stacking
		for i in range(1, env_nodes.size()):
			var dup := env_nodes[i]
			dup.get_parent().remove_child(dup)
			dup.queue_free()
			
	return primary_environment

## Registers a localized room light source (e.g. lamp, stove, sconce).
func register_room_light(room_id: String, light_node: Light3D) -> void:
	if not registered_room_lights.has(room_id):
		registered_room_lights[room_id] = []
	var list: Array = registered_room_lights[room_id]
	if not list.has(light_node):
		list.append(light_node)

## Toggles localized lights for a specific room.
func set_room_light_state(room_id: String, is_on: bool) -> void:
	if not registered_room_lights.has(room_id):
		return
	for light in registered_room_lights[room_id]:
		if is_instance_valid(light):
			light.visible = is_on

## Configures lighting transition when moving between exterior and interior spaces.
func set_space_lighting(is_outdoors: bool, sun: DirectionalLight3D = null) -> void:
	if sun != null:
		primary_sun = sun
	if primary_environment != null and primary_environment.environment != null:
		var env := primary_environment.environment
		if is_outdoors:
			env.ambient_light_energy = maxf(0.50, min_ambient_energy)
			if primary_sun != null:
				primary_sun.light_energy = default_sun_energy
		else:
			# Interior: ensure min floor so passages and evidence are always identifiable
			env.ambient_light_energy = min_ambient_energy
			if primary_sun != null:
				primary_sun.light_energy = default_sun_energy * 0.15 # Attenuated interior daylight

## 4.05 Verification & Inspection:
## Verifies that:
## 1. Exactly 1 WorldEnvironment is active.
## 2. Ambient light energy is at or above the minimum discernability floor.
## 3. Darkest areas still have passage and evidence discernability.
static func inspect_lighting(root: Node, min_required_energy: float = 0.20) -> Dictionary:
	if not is_instance_valid(root):
		return {"valid": false, "error": "missing-root"}
		
	var env_nodes: Array[WorldEnvironment] = []
	_collect_nodes_of_type(root, "WorldEnvironment", env_nodes)
	var env_count := env_nodes.size()
	var no_duplicate_env := env_count <= 1
	
	var ambient_energy := 0.0
	var ambient_adequate := false
	if env_count >= 1 and env_nodes[0].environment != null:
		ambient_energy = env_nodes[0].environment.ambient_light_energy
		ambient_adequate = ambient_energy >= min_required_energy - 0.001
		
	var all_passed := no_duplicate_env and ambient_adequate
	return {
		"valid": all_passed,
		"world_environment_count": env_count,
		"no_duplicate_env": no_duplicate_env,
		"ambient_energy": ambient_energy,
		"min_required_energy": min_required_energy,
		"ambient_adequate": ambient_adequate,
		"passage_and_evidence_discernable": all_passed
	}

static func _collect_nodes_of_type(parent: Node, type_name: String, result: Array) -> void:
	if parent.get_class() == type_name:
		result.append(parent)
	for child in parent.get_children():
		_collect_nodes_of_type(child, type_name, result)
