class_name Scene6
extends Node3D

## 第六幕：透视空间推演
## 采用透视视角（Perspective View），以学院花纹瓷砖为地面纹理，呈现 2.5D 可移动的威尔逊。

const CYCLE := [0, 1, 2, 2]
const DIRS := ["南", "西南", "西", "西北", "北", "东北", "东", "东南"]

var time := 0.0
var elapsed := 0.0
var keys := {}
var target_pos = null # Vector3 or null
var moving := false
var current_dir := 0 # 0..7

## 相机死区配置：角色在此矩形范围内（X与Z轴向）移动时不推动摄像头
@export var deadzone_half := Vector2(1.8, 1.4)
var cam_focus := Vector3.ZERO
var in_deadzone := true

# 角色与视觉节点
@onready var camera: Camera3D = $Camera3D
@onready var ground_mesh: MeshInstance3D = $Ground
@onready var wall_mesh: MeshInstance3D = $Wall
@onready var wilson_root: Node3D = $Wilson
@onready var wilson_sprite: Sprite3D = $Wilson/Sprite3D
@onready var shadow_mesh: MeshInstance3D = $Wilson/Shadow
@onready var click_indicator: MeshInstance3D = $ClickIndicator
@onready var hud: CanvasLayer = $HUD

var wilson_texture: Texture2D
var wilson_frames: Array = [] # 3行 x 8列的 Rect2 字典
var ready_flag := false

# HUD 文本引用
var label_title: Label
var label_desc: Label
var label_status: Label
var label_footer: Label

func _ready() -> void:
	setup_environment_and_ground()
	setup_wilson_spritesheet()
	setup_hud()
	
	cam_focus = wilson_root.position
	var camera_offset := Vector3(0.0, 7.8, 8.8)
	camera.position = cam_focus + camera_offset
	camera.look_at(cam_focus + Vector3(0.0, 0.8, 0.0), Vector3.UP)
	
	ready_flag = true

func setup_environment_and_ground() -> void:
	# 地面纹理材质
	var tile_tex = load("res://ShareAsset/assets/学院花纹瓷砖.png") as Texture2D
	if tile_tex:
		var mat = StandardMaterial3D.new()
		mat.albedo_texture = tile_tex
		mat.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		mat.roughness = 0.45
		mat.specular = 0.2
		ground_mesh.material_override = mat
	
	# 学院墙壁材质
	var wall_tex = load("res://ShareAsset/assets/学院墙壁.png") as Texture2D
	if wall_tex:
		var wall_mat = StandardMaterial3D.new()
		wall_mat.albedo_texture = wall_tex
		wall_mat.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		wall_mat.roughness = 0.75
		wall_mat.specular = 0.1
		wall_mesh.material_override = wall_mat

func setup_wilson_spritesheet() -> void:
	var path := "res://ShareAsset/assets/wilson-walk.png"
	var im = Image.load_from_file(path)
	if im == null:
		im = Image.load_from_file(ProjectSettings.globalize_path(path))
	
	if im:
		var cols := 8
		var rows := 3
		var w := im.get_width()
		var h := im.get_height()
		
		wilson_frames.clear()
		for r in range(rows):
			var row_frames: Array = []
			for c in range(cols):
				var left := int(float(c * w) / float(cols))
				var right := int(float((c + 1) * w) / float(cols))
				var top := int(float(r * h) / float(rows))
				var bottom := int(float((r + 1) * h) / float(rows))
				
				var x0 := right
				var y0 := bottom
				var x1 := left
				var y1 := top
				
				for y in range(top, bottom):
					for x in range(left, right):
						if im.get_pixel(x, y).a > (100.0 / 255.0):
							x0 = mini(x0, x)
							x1 = maxi(x1, x)
							y0 = mini(y0, y)
							y1 = maxi(y1, y)
				
				var fw := maxi(1, x1 - x0 + 1)
				var fh := maxi(1, y1 - y0 + 1)
				row_frames.append({
					"rect": Rect2(x0, y0, fw, fh),
					"w": float(fw),
					"h": float(fh)
				})
			wilson_frames.append(row_frames)
		
		wilson_texture = ImageTexture.create_from_image(im)
		wilson_sprite.texture = wilson_texture
		wilson_sprite.region_enabled = true
		update_wilson_sprite(1, 0)

func update_wilson_sprite(row: int, col: int) -> void:
	if wilson_frames.is_empty():
		return
	var f = wilson_frames[row][col]
	wilson_sprite.region_rect = f["rect"]
	# 高度标准化约为 1.85 米，根据帧高度计算 pixel_size
	var target_height := 1.85
	var px_size: float = target_height / float(f["h"])
	wilson_sprite.pixel_size = px_size
	wilson_sprite.offset.y = float(f["h"]) * 0.5

func direction_from(dx: float, dz: float) -> int:
	# dx: +X(东), -X(西)
	# dz: +Z(南), -Z(北)
	return int((round(atan2(-dx, dz) / (PI / 4.0)) + 8.0)) % 8

func _process(delta: float) -> void:
	if not ready_flag:
		return
	
	var dt := minf(delta, 0.05)
	time += dt
	elapsed += dt
	
	process_movement(dt)
	update_animation()
	update_camera(dt)
	update_click_indicator(dt)
	update_hud_display()

func process_movement(dt: float) -> void:
	var move_speed := 4.2
	var dir_x := 0.0
	var dir_z := 0.0
	
	if keys.get("w", false) or keys.get("up", false): dir_z -= 1.0
	if keys.get("s", false) or keys.get("down", false): dir_z += 1.0
	if keys.get("a", false) or keys.get("left", false): dir_x -= 1.0
	if keys.get("d", false) or keys.get("right", false): dir_x += 1.0
	
	var input_vec := Vector2(dir_x, dir_z)
	if input_vec.length_squared() > 0.0:
		target_pos = null # 键盘输入打断鼠标寻路
		input_vec = input_vec.normalized()
		var move_dir3 := Vector3(input_vec.x, 0.0, input_vec.y)
		wilson_root.position += move_dir3 * move_speed * dt
		current_dir = direction_from(input_vec.x, input_vec.y)
		moving = true
	elif target_pos != null:
		var current_pos: Vector3 = wilson_root.position
		var delta_pos: Vector3 = target_pos - current_pos
		delta_pos.y = 0.0
		var dist: float = delta_pos.length()
		if dist < 0.08:
			target_pos = null
			moving = false
		else:
			var move_dir := delta_pos / dist
			var step: float = minf(move_speed * dt, dist)
			wilson_root.position += move_dir * step
			current_dir = direction_from(move_dir.x, move_dir.z)
			moving = true
	else:
		moving = false
	
	# 限制在学院瓷砖地面活动范围内（北侧留出长椅与茶几安全距离）
	wilson_root.position.x = clampf(wilson_root.position.x, -6.5, 6.5)
	wilson_root.position.z = clampf(wilson_root.position.z, -5.2, 6.2)
	wilson_root.position.y = 0.0

func update_animation() -> void:
	var f: int = int(CYCLE[int(time * 6.0) % 4])
	var row_idx := f if moving else 1
	var col_idx := current_dir
	update_wilson_sprite(row_idx, col_idx)

func update_camera(dt: float) -> void:
	var wilson_pos: Vector3 = wilson_root.position
	var diff_x: float = wilson_pos.x - cam_focus.x
	var diff_z: float = wilson_pos.z - cam_focus.z
	
	var pushed := false
	if absf(diff_x) > deadzone_half.x:
		cam_focus.x = wilson_pos.x - signf(diff_x) * deadzone_half.x
		pushed = true
	
	if absf(diff_z) > deadzone_half.y:
		cam_focus.z = wilson_pos.z - signf(diff_z) * deadzone_half.y
		pushed = true
	
	in_deadzone = not pushed
	
	# 镜头以平滑阻尼跟随锚点，在死区内锚点不动则镜头完全静止
	var camera_offset := Vector3(0.0, 7.8, 8.8)
	var desired_pos := cam_focus + camera_offset
	camera.position = camera.position.lerp(desired_pos, 5.0 * dt)
	camera.look_at(cam_focus + Vector3(0.0, 0.8, 0.0), Vector3.UP)

func update_click_indicator(dt: float) -> void:
	if target_pos != null:
		click_indicator.visible = true
		click_indicator.position = Vector3(target_pos.x, 0.015, target_pos.z)
		var scale_factor: float = 1.0 + 0.08 * sin(time * 8.0)
		click_indicator.scale = Vector3(scale_factor, 1.0, scale_factor)
	else:
		click_indicator.visible = false

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey:
		var code = event.keycode
		var pressed = event.pressed
		if code == KEY_W: keys["w"] = pressed
		elif code == KEY_S: keys["s"] = pressed
		elif code == KEY_A: keys["a"] = pressed
		elif code == KEY_D: keys["d"] = pressed
		elif code == KEY_UP: keys["up"] = pressed
		elif code == KEY_DOWN: keys["down"] = pressed
		elif code == KEY_LEFT: keys["left"] = pressed
		elif code == KEY_RIGHT: keys["right"] = pressed
		
		if pressed:
			if code == KEY_1: change_to_scene(1)
			elif code == KEY_2: change_to_scene(2)
			elif code == KEY_3: change_to_scene(3)
			elif code == KEY_4: change_to_scene(4)
			elif code == KEY_5: change_to_scene(5)
			elif code == KEY_6: change_to_scene(6)
	
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var mouse_pos = event.position
		var hit = get_ground_click_position(mouse_pos)
		if hit != null:
			var target_clamped = Vector3(
				clampf(hit.x, -6.5, 6.5),
				0.0,
				clampf(hit.z, -5.2, 6.2)
			)
			target_pos = target_clamped

func get_ground_click_position(mouse_pos: Vector2):
	var ray_origin = camera.project_ray_origin(mouse_pos)
	var ray_normal = camera.project_ray_normal(mouse_pos)
	var ground_plane = Plane(Vector3.UP, 0.0)
	return ground_plane.intersects_ray(ray_origin, ray_normal)

func change_to_scene(scene_num: int) -> void:
	var path := "res://scenes/Scene%d/Scene%d.tscn" % [scene_num, scene_num]
	if ResourceLoader.exists(path):
		get_tree().change_scene_to_file(path)

func setup_hud() -> void:
	var root_control = Control.new()
	root_control.set_anchors_preset(Control.PRESET_FULL_RECT)
	root_control.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.add_child(root_control)
	
	# 顶部指示面板
	var panel_top = Panel.new()
	panel_top.position = Vector2(20, 20)
	panel_top.size = Vector2(560, 80)
	panel_top.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root_control.add_child(panel_top)
	
	label_title = Label.new()
	label_title.position = Vector2(15, 8)
	label_title.size = Vector2(530, 26)
	label_title.text = "ACT VI · 透视空间推演  [按键 1-6 换幕]"
	label_title.add_theme_color_override("font_color", Color(0.95, 0.9, 0.75))
	label_title.add_theme_font_size_override("font_size", 15)
	panel_top.add_child(label_title)
	
	label_desc = Label.new()
	label_desc.position = Vector2(15, 38)
	label_desc.size = Vector2(530, 36)
	label_desc.text = "学院瓷砖地面与立面 · 镜头死区机制（Camera Deadzone）\n角色在死区（±%.1fm × ±%.1fm）内移动时镜头静止；移出边缘时平滑推移。" % [deadzone_half.x, deadzone_half.y]
	label_desc.add_theme_color_override("font_color", Color(0.85, 0.88, 0.82))
	label_desc.add_theme_font_size_override("font_size", 12)
	panel_top.add_child(label_desc)
	
	# 指标状态面板
	var panel_status = Panel.new()
	panel_status.position = Vector2(20, 110)
	panel_status.size = Vector2(560, 48)
	panel_status.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root_control.add_child(panel_status)
	
	label_status = Label.new()
	label_status.position = Vector2(15, 14)
	label_status.size = Vector2(530, 24)
	label_status.add_theme_color_override("font_color", Color(0.9, 0.82, 0.65))
	label_status.add_theme_font_size_override("font_size", 13)
	panel_status.add_child(label_status)
	
	# 底部操作记录栏
	var panel_footer = Panel.new()
	panel_footer.position = Vector2(20, 750)
	panel_footer.size = Vector2(960, 36)
	panel_footer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root_control.add_child(panel_footer)
	
	label_footer = Label.new()
	label_footer.position = Vector2(15, 8)
	label_footer.size = Vector2(930, 22)
	label_footer.text = "操作指引: [W/A/S/D 或 方向键移动] [鼠标左键点击瓷砖地面寻路] [按键 1-5 切换前五幕 / 6 重置]"
	label_footer.add_theme_color_override("font_color", Color(0.8, 0.85, 0.8))
	label_footer.add_theme_font_size_override("font_size", 13)
	panel_footer.add_child(label_footer)

func update_hud_display() -> void:
	if label_status:
		var pos := wilson_root.position
		var cam_state := "死区内 (镜头静止)" if in_deadzone else "推移镜头"
		var status_str := "移动中" if moving else "停驻观察"
		label_status.text = "坐标: (%.2f, %.2f) · 朝向: %s · 角色: %s · 镜头: %s" % [pos.x, pos.z, DIRS[current_dir], status_str, cam_state]
