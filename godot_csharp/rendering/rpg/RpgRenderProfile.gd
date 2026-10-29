extends Resource
## Presentation parameters only. One Godot unit is one metre.
@export var title := "Daylight"
@export var sun_rotation := Vector3(-32, -32, 0)
@export var sun_color := Color(1.0, 0.95, 0.87)
@export_range(0.0, 8.0) var sun_energy := 1.8
@export var sky_top := Color(0.19, 0.34, 0.53)
@export var sky_horizon := Color(0.7, 0.76, 0.8)
@export_range(0.0, 2.0) var sky_energy := 0.65
@export_range(0.01, 4.0) var exposure := 1.0
@export_range(0.0, 0.1) var fog_density := 0.008
@export var fog_color := Color(0.65, 0.73, 0.8)
@export_range(0.0, 1.0) var bloom := 0.18
@export_range(10.0, 300.0) var shadow_distance := 80.0
@export_range(0.1, 2.0) var gi_cell_size := 0.25
