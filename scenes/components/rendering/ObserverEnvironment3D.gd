extends WorldEnvironment

## 现场观察环境光：三点布光（暖色主光 + 冷色补光 + 轮廓光）、柔和接触阴影、SSAO / SSIL、
## AgX 色调映射、轻微辉光与对比度校正；随游戏时刻在夜 / 晨 / 昼 / 暮色板之间平滑过渡。
## 软件渲染（llvmpipe）自动关闭屏幕空间效果以保持帧率。宿主调用 set_time_of_day(分钟)。

const PALETTES := [
	# 时刻(分钟), 主光色, 主光强度, 补光色, 环境色, 环境强度, 背景色, 主光仰角(度)
	[0.0,    Color("6f7fb8"), 0.20, Color("33406e"), Color("4a5274"), 0.32, Color("0d1018"), 28.0],
	[330.0,  Color("ffb98a"), 0.55, Color("7f8fc0"), Color("b8a89a"), 0.36, Color("2a2420"), 14.0],
	[480.0,  Color("fff1da"), 1.00, Color("9fb4d8"), Color("d8d0c0"), 0.42, Color("1e1b18"), 46.0],
	[900.0,  Color("fff4e2"), 1.05, Color("a5b8d8"), Color("dcd4c4"), 0.44, Color("1e1b18"), 56.0],
	[1080.0, Color("ffa66e"), 0.72, Color("7c88b8"), Color("c4a890"), 0.37, Color("261e1a"), 16.0],
	[1230.0, Color("ffcf94"), 0.40, Color("4a5590"), Color("6a6482"), 0.32, Color("12131b"), 30.0],
	[1440.0, Color("6f7fb8"), 0.20, Color("33406e"), Color("4a5274"), 0.32, Color("0d1018"), 28.0],
]

var sun: DirectionalLight3D
var fill: DirectionalLight3D
var rim: DirectionalLight3D
var _software := false
var _minutes := 540.0

func _ready() -> void:
	_software = RenderingServer.get_video_adapter_name().to_lower().contains("llvmpipe")
	environment = Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.tonemap_mode = Environment.TONE_MAPPER_AGX
	environment.tonemap_exposure = 1.05
	environment.ssao_enabled = not _software
	environment.ssao_radius = 0.6
	environment.ssao_intensity = 1.1
	environment.ssao_power = 1.4
	environment.ssao_detail = 0.6
	environment.ssil_enabled = not _software
	environment.ssil_radius = 2.5
	environment.ssil_intensity = 0.55
	environment.glow_enabled = not _software
	environment.glow_intensity = 0.35
	environment.glow_bloom = 0.04
	environment.glow_hdr_threshold = 1.2
	environment.adjustment_enabled = true
	environment.adjustment_contrast = 1.06
	environment.adjustment_saturation = 1.04
	sun = _light("KeyLight", true)
	sun.light_angular_distance = 1.2
	sun.shadow_blur = 1.5
	sun.shadow_opacity = 0.78
	sun.shadow_bias = 0.04
	sun.shadow_normal_bias = 1.2
	sun.directional_shadow_max_distance = 40.0
	fill = _light("FillLight", false)
	fill.light_specular = 0.1
	rim = _light("RimLight", false)
	rim.light_specular = 0.6
	set_time_of_day(_minutes)

func _light(light_name: String, shadows: bool) -> DirectionalLight3D:
	var light := DirectionalLight3D.new()
	light.name = light_name
	light.shadow_enabled = shadows
	add_child(light)
	return light

## 游戏时刻（分钟，可越过 1440，内部取模）。
func set_time_of_day(minutes: float) -> void:
	if not is_instance_valid(sun): return
	_minutes = fposmod(minutes, 1440.0)
	var lower: Array = PALETTES[0]
	var upper: Array = PALETTES[PALETTES.size() - 1]
	for index in range(PALETTES.size() - 1):
		if _minutes >= float(PALETTES[index][0]) and _minutes <= float(PALETTES[index + 1][0]):
			lower = PALETTES[index]
			upper = PALETTES[index + 1]
			break
	var span := maxf(1.0, float(upper[0]) - float(lower[0]))
	var t := smoothstep(0.0, 1.0, (_minutes - float(lower[0])) / span)
	var key_color: Color = (lower[1] as Color).lerp(upper[1], t)
	var key_energy := lerpf(float(lower[2]), float(upper[2]), t)
	var elevation := deg_to_rad(lerpf(float(lower[7]), float(upper[7]), t))
	var azimuth := deg_to_rad(lerpf(-70.0, 70.0, clampf((_minutes - 330.0) / 900.0, 0.0, 1.0)) + 35.0)
	sun.light_color = key_color
	sun.light_energy = key_energy
	sun.rotation = Vector3(-elevation, azimuth, 0.0)
	fill.light_color = (lower[3] as Color).lerp(upper[3], t)
	fill.light_energy = key_energy * 0.32 + 0.08
	fill.rotation = Vector3(deg_to_rad(-35.0), azimuth + PI * 0.85, 0.0)
	rim.light_color = key_color.lerp(Color.WHITE, 0.4)
	rim.light_energy = key_energy * 0.45
	rim.rotation = Vector3(deg_to_rad(-20.0), azimuth + PI, 0.0)
	environment.ambient_light_color = (lower[4] as Color).lerp(upper[4], t)
	environment.ambient_light_energy = lerpf(float(lower[5]), float(upper[5]), t)
	environment.background_color = (lower[6] as Color).lerp(upper[6], t)
