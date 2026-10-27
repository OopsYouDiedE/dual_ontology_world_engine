extends Node3D
## Read-only presentation of supplied game minutes; never advances Core time.
@export var game_minutes := 720.0
@export var outdoors := false
var _profile: Dictionary = {}

func _ready() -> void:
	_profile = preload("res://scenes/Host/DvPresentationContent.cs").new().ReadRecord("res://Content/presentation.dv", "layouts", "daylight")
	apply_time(game_minutes, outdoors)

func apply_time(minutes: float, exterior: bool) -> void:
	if not is_finite(minutes): return
	game_minutes = fposmod(minutes, 1440.0)
	outdoors = exterior
	if _profile.is_empty(): return
	var keys: Array = _profile.get("keys", [])
	if keys.size() < 2: return
	var before: Dictionary = keys[0]
	var after: Dictionary = keys[-1]
	for index in range(keys.size() - 1):
		if game_minutes >= float(keys[index].minute) and game_minutes <= float(keys[index + 1].minute):
			before = keys[index]
			after = keys[index + 1]
			break
	var weight := inverse_lerp(float(before.minute), float(after.minute), game_minutes)
	var world := get_parent()
	var sun := world.get_node_or_null("Daylight") as DirectionalLight3D
	var holder := world.get_node_or_null("WorldEnvironment") as WorldEnvironment
	var env: Environment = holder.environment if holder != null else world.get_meta("presentation_environment", null)
	if sun == null or env == null: return
	if holder != null and not holder.has_meta("time_environment_unique"):
		holder.environment = holder.environment.duplicate(true)
		holder.set_meta("time_environment_unique", true)
		env = holder.environment
	var exposure := 1.0 if outdoors else float(_profile.interior_sun_scale)
	sun.rotation_degrees = Vector3(lerpf(float(before.pitch), float(after.pitch), weight), lerpf(float(before.yaw), float(after.yaw), weight), 0)
	sun.light_color = Color(str(before.sun)).lerp(Color(str(after.sun)), weight)
	sun.light_energy = lerpf(float(before.energy), float(after.energy), weight) * exposure
	env.ambient_light_color = Color(str(before.ambient)).lerp(Color(str(after.ambient)), weight)
	env.ambient_light_energy = lerpf(float(before.fill), float(after.fill), weight) * (1.0 if outdoors else float(_profile.interior_fill_scale))
	env.background_color = Color(str(before.sky)).srgb_to_linear().lerp(Color(str(after.sky)).srgb_to_linear(), weight)
	env.background_energy_multiplier = float(_profile.get("background_energy", 1.0))
	set_meta("time_sample", {"game_minutes": game_minutes, "outdoors": outdoors, "sun_energy": sun.light_energy, "ambient_energy": env.ambient_light_energy})

func apply_snapshot(snapshot: Dictionary) -> void:
	if snapshot.has("game_time"):
		apply_time(float(snapshot.game_time), outdoors)
