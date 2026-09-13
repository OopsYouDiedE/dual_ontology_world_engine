class_name Scene2
extends SceneBase

## 第二幕：脑内推演
## 用灰色人偶复现行动路线。警惕危险认知词汇，使用安全概念完成推演

var danger := 0.0
var rule := false
var phase := "idle"
var companion: Dictionary = {}
var sim_points: Array[Vector2] = [
	Vector2(320, 640), Vector2(410, 595), Vector2(410, 430), Vector2(650, 430), Vector2(850, 560)
]

func _init_scene() -> void:
	hud_act = "ACT II"
	hud_title = "脑内推演"
	hud_desc = "用灰色人偶复现进入房间的动作。一个词让自我开始模糊；及时停止联想，换一种说法。"
	record_text = "复现场景已展开。灰色人偶等待你的规则。[按 F 尝试危险词 / 按 E 停止联想并推演]"
	
	actors.clear()
	player = create_person("wilson", 590, 580)
	companion = create_person("gray", 320, 640)
	actors.append(player)
	actors.append(companion)
	
	danger = 0.0
	rule = false
	phase = "idle"
	_update_hud_display()

func _update_scene_logic(dt: float) -> void:
	if phase == "danger":
		danger = minf(0.95, danger + dt * 0.15)
		if danger > 0.8:
			speech_text = "威尔逊：不行……不能直视那个名字！"
	elif phase == "simulate":
		var idx: int = mini(3, int(elapsed / 2.3))
		move_person(companion, sim_points[idx].x, sim_points[idx].y, dt, 105.0)
		if elapsed > 10.0:
			phase = "done"
			record_text = "推演完成。人偶沿替代规则行动，未再触及危险词。"
			speech_text = "威尔逊：用'来访者'替代真名，逻辑链条闭合了。"
	
	_update_hud_display()

func _update_hud_display() -> void:
	hud_label = "自我失稳"
	hud_value = danger
	if phase == "danger":
		hud_note = "立即停止联想！[按 E 停止联想并采用替代词]"
	elif rule:
		hud_note = "替代词：来访者 · 推演正常进行中"
	else:
		hud_note = "复现场景 · [按 F 尝试规则 / 按 E 安全推演]"

func _draw_scene_custom() -> void:
	# 青灰色推演心象蒙层与路线
	draw_rect(Rect2(0, 0, 1200, 800), Color(0.08, 0.18, 0.2, 0.66))
	var pts: PackedVector2Array = PackedVector2Array(sim_points)
	draw_polyline(pts, Color(0.76, 0.83, 0.75, 0.35), 2.0)
	
	# 自我失稳危险红光
	if danger > 0.0:
		var alert_alpha = danger * 0.4
		draw_rect(Rect2(0, 0, 1200, 800), Color(0.85, 0.15, 0.1, alert_alpha))

func _on_scene_key_down(code: int) -> void:
	if code == KEY_F:
		phase = "danger"
		elapsed = 0.0
		record_text = "警告：使用了高位阶真名词汇，自我边界开始失稳！"
		speech_text = "视线边缘出现红色重影……"
	elif code == KEY_E:
		if phase == "danger" or phase == "idle":
			danger = 0.0
			phase = "simulate"
			rule = true
			elapsed = 0.0
			speech_text = "威尔逊：停止联想。将对象表征替换为'来访者'。"
			record_text = "规避危险词成功。灰色人偶开始按照安全规则行动。"
