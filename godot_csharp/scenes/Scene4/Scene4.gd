class_name Scene4
extends SceneBase

## 第四幕：恶作剧
## 张贴恶作剧规则，约束路人与恶魔绕圈，最终被察觉撕毁

var earl: Dictionary = {}
var rule := false
var phase := "idle"

func _init_scene() -> void:
	hud_act = "ACT IV"
	hud_title = "恶作剧"
	hud_desc = "一张不合时宜的规则，让经过的人都绕起了圈。伯爵也未能幸免。"
	record_text = "公告板空着。这里暂时没有人转圈。[按 F 贴上恶趣味规则]"
	
	actors.clear()
	player = create_person("wilson", 410, 620)
	earl = create_person("maxwell", 985, 450)
	actors.append(player)
	actors.append(earl)
	for i in range(4):
		actors.append(create_person("gray", 545 + i * 75, 520 + (i % 2) * 100))
	
	rule = false
	phase = "idle"
	_update_hud_display()

func _update_scene_logic(dt: float) -> void:
	if rule:
		var grays: Array = []
		for p in actors:
			if p["kind"] == "gray":
				grays.append(p)
		for i in range(grays.size()):
			var a_angle = elapsed * 1.3 + float(i) * PI / 2.0
			move_person(grays[i], 660.0 + cos(a_angle) * 125.0, 545.0 + sin(a_angle) * 68.0, dt, 160.0)

	if phase == "circling":
		if elapsed > 3.0:
			var a_angle = (elapsed - 3.0) * 1.3
			move_person(earl, 660.0 + cos(a_angle) * 125.0, 545.0 + sin(a_angle) * 68.0, dt, 175.0)
		if elapsed > 4.0 and elapsed < 4.2:
			speech_text = "麦斯威尔：……我为什么也在转？"
		if elapsed > 11.0:
			phase = "investigating"
			speech_text = "麦斯威尔：等等。是那张纸。"
			record_text = "伯爵察觉来源，走向规则。"
	elif phase == "investigating":
		if move_person(earl, 700, 415, dt, 135.0):
			phase = "ripping"
			elapsed = 0.0
			earl["pose"] = "rip"
	elif phase == "ripping":
		if elapsed > 0.8:
			rule = false
		if elapsed > 1.8:
			phase = "done"
			speech_text = "麦斯威尔：这是什么垃圾规则！给我适可而止！"
			record_text = "规则被撕毁并废除。所有人停止绕圈。"

	_update_hud_display()

func _update_hud_display() -> void:
	hud_label = "规则效力"
	hud_value = 1.0 if rule else 0.0
	hud_note = "规则正在强行影响房间内所有人" if rule else "规则尚未生效或已被撕毁"

func _draw_scene_custom() -> void:
	if rule:
		draw_rect(Rect2(673, 347, 34, 46), Color(0.82, 0.76, 0.61))

func _on_scene_key_down(code: int) -> void:
	if code == KEY_F and phase == "idle":
		rule = true
		phase = "circling"
		elapsed = 0.0
		record_text = "规则已张贴：'路经此地必须顺时针转三圈'。"
		speech_text = "路过的人们身体不自主地受规则牵引……"
