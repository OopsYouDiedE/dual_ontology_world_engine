extends Camera3D
## Camera presentation only. Main forwards the measured physical focus.
var orbit_yaw := PI * 0.25
func _ready() -> void:
	projection = Camera3D.PROJECTION_ORTHOGONAL
	size = 9.5
	near = 0.1
	far = 500.0

func follow(focus: Vector3) -> void:
	var target := focus + Vector3.UP * 0.65
	# 45-degree azimuth from +Z and 45-degree downward elevation.
	global_position = target + Vector3(sin(orbit_yaw) * 9.05, 9.05, cos(orbit_yaw) * 9.05)
	look_at(target, Vector3.UP)

func adjust_view(orbit_delta: float, zoom_delta: float) -> void:
	orbit_yaw = wrapf(orbit_yaw + orbit_delta, -PI, PI)
	size = clampf(size + zoom_delta, 2.5, 14.0)

