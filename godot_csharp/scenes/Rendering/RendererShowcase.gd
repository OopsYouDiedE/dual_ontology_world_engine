extends Node3D
const DAY = preload("res://rendering/rpg/Daylight.tres")
const NIGHT = preload("res://rendering/rpg/BlueHour.tres")
const ReviewWalker = preload("res://geometry/FrameworkFirstPerson.gd")
@onready var renderer = $View/Viewport/Renderer
@onready var rig = $View/Viewport/CameraRig
@onready var map = $View/Viewport/Map
var status: Label
var _night := false
var _time := 0.0
var walker: CharacterBody3D
var following := false

func _ready() -> void:
	rig.camera.make_current()
	walker = ReviewWalker.new()
	walker.name = "CameraReviewWalker"
	walker.position = Vector3(6, 0.9, 0)
	$View/Viewport.add_child(walker)
	walker.configure(0.35, 1.8)
	# The calibration court uses layer 2 for its terrain.
	walker.collision_mask = 3
	var marker := MeshInstance3D.new()
	var capsule := CapsuleMesh.new()
	capsule.radius = 0.35
	capsule.height = 1.8
	marker.mesh = capsule
	walker.add_child(marker)
	var layer := CanvasLayer.new()
	add_child(layer)
	var panel := PanelContainer.new()
	panel.position = Vector2(20,20)
	layer.add_child(panel)
	var margin := MarginContainer.new()
	for side in ["left","top","right","bottom"]: margin.add_theme_constant_override("margin_"+side,14)
	panel.add_child(margin)
	var column := VBoxContainer.new()
	margin.add_child(column)
	var title := Label.new()
	title.text = "RPG RENDER LAB  /  材质与光照庭院"
	title.add_theme_font_size_override("font_size",22)
	column.add_child(title)
	status = Label.new()
	column.add_child(status)
	var row := HBoxContainer.new()
	column.add_child(row)
	button(row,"日光 / 蓝调",_toggle_light)
	button(row,"高画质 / 均衡",_toggle_quality)
	button(row,"环绕 +30°",func(): rig.orbit(30,0))
	button(row,"拉近",func(): rig.zoom(-2))
	button(row,"复位",_reset_camera)
	button(row,"跟随检查",_toggle_follow)
	button(row,"灯光开关",func(): renderer.enabled = not renderer.enabled)
	button(row,"像素密度",func(): $View.set_resolution(Vector2i(640,496) if $View.base_resolution.x == 320 else Vector2i(320,248)))
	var help := Label.new()
	help.text = "右键拖动环绕 · 滚轮缩放 · 跟随检查使用方向键移动 · 独立研究场景"
	column.add_child(help)
	_update_status()

func button(row: HBoxContainer, title: String, action: Callable) -> void:
	var item := Button.new()
	item.text = title
	item.focus_mode = Control.FOCUS_NONE
	item.pressed.connect(action)
	row.add_child(item)

func _toggle_light() -> void:
	_night = not _night
	renderer.apply_profile(NIGHT if _night else DAY)
	map.refresh_probes()
	_update_status()

func _toggle_quality() -> void:
	renderer.set_quality(1 - renderer.quality)
	_update_status()

func _reset_camera() -> void:
	if following:
		_toggle_follow()
	rig.position = Vector3(0,1.2,0)
	rig.yaw = 55
	rig.pitch = 20
	rig.distance = 11

func _toggle_follow() -> void:
	following = not following
	if following:
		walker.active = true
		rig.target = walker
		rig.target_offset = Vector3(0, 0.35, 0)
		rig.collision_mask = 1
		rig.distance = 5.0
		rig.yaw = 0
		rig.pitch = 10
		rig.refresh_exclusions()
	else:
		walker.active = false
		rig.target = null
		rig.refresh_exclusions()
	rig.camera.make_current()

func _process(delta: float) -> void:
	_time += delta
	if _time >= 1.0:
		_time = 0
		_update_status()

func _update_status() -> void:
	status.text = "%s · %s · %s · %d FPS (即时)\nGI / AO · %s · 相机臂 %.2f / %.2f m" % [
		RenderingServer.get_current_rendering_method(), "高画质" if renderer.quality == 1 else "均衡",
		"蓝调" if _night else "日光", Engine.get_frames_per_second(),
		"SSR / SSIL / 体积雾" if renderer.quality == 1 else "基础光照",
		rig.spring_arm.get_hit_length(), rig.distance]
	if not renderer.supported: status.text = "渲染后端不符合要求：需要 Forward+，请重启宿主。"
	elif not renderer.enabled: status.text = "渲染灯光组件已停用；地图与相机继续独立运行。"
	for arcade in map.arcades:
		if arcade.build_error != "":
			status.text += "\n" + arcade.build_error
		elif not arcade.foundation_heights.is_empty():
			status.text += "\n%s 基础 %.2f–%.2f m" % [arcade.name,arcade.foundation_heights.min(),arcade.foundation_heights.max()]
	status.text += "\n世界 %d×%d · 最近邻 %d× · UI 原生分辨率" % [$View/Viewport.size.x,$View/Viewport.size.y,$View.pixel_scale]
	if following:
		status.text += "\n目标 %s · 相机实际距离 %.2f m" % [str(walker.position), rig.camera.position.length()]
