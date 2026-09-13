class_name Scene5
extends SceneBase

## 第五幕：保护威尔逊
## 伏案侦探深入危险推演，阴影滋生，同伴关键时刻伸手打断

var companion: Dictionary = {}
var danger := 0.0
var phase := "idle"

func _init_scene() -> void:
	hud_act = "ACT V"
	hud_title = "保护威尔逊"
	hud_desc = "伏案的侦探逐渐失去对周围的注意。阴影靠近，同伴必须打断尚未完成的认识。"
	record_text = "同伴在房间另一侧。威尔逊准备开始推理。[按 F 伏案推理 / 按 E 同伴打断]"
	
	actors.clear()
	player = create_person("wilson", 760, 565)
	companion = create_person("gray", 410, 610)
	actors.append(player)
	actors.append(companion)
	
	danger = 0.0
	phase = "idle"
	_update_hud_display()

func _update_scene_logic(dt: float) -> void:
	if phase == "thinking":
		danger = minf(0.94, danger + dt * 0.095)
		if danger > 0.4 and speech_text == "":
			speech_text = "威尔逊：（笔尖游走……线索正在拼合成禁忌的图景）"
		if danger > 0.8:
			record_text = "危险警报：认知侵蚀极高！同伴即将采取行动！"
	elif phase == "interrupting":
		danger = minf(0.94, danger + dt * 0.02)
		if move_person(companion, 865, 565, dt, 190.0):
			phase = "recovering"
			elapsed = 0.0
			companion["pose"] = "interrupt"
			record_text = "同伴伸手按住桌面并呼唤，尚未成立的连接停止延伸。"
	elif phase == "recovering":
		danger = maxf(0.0, danger - dt * 0.65)
		if elapsed > 1.7:
			phase = "done"
			player["pose"] = null
			companion["pose"] = null
			danger = 0.0
			speech_text = "威尔逊：……我听见了。谢谢。"
			record_text = "威尔逊恢复对周围的注意。危险认识没有成立。"

	_update_hud_display()

func _update_hud_display() -> void:
	hud_label = "危险认识"
	hud_value = danger
	if phase == "thinking":
		hud_note = "推理中 · 阴影正在蔓延 [按 E 呼唤打断]"
	elif phase == "interrupting" or phase == "recovering":
		hud_note = "同伴介入中……"
	elif phase == "done":
		hud_note = "连接已中断 · 意识安全恢复"
	else:
		hud_note = "准备就绪 [按 F 开始伏案推理]"

func _draw_scene_custom() -> void:
	if danger > 0.0:
		draw_shadow(player["x"] - 82, player["y"], danger * 0.9)
		draw_shadow(player["x"] + 65, player["y"] + 15, danger)

func _on_scene_key_down(code: int) -> void:
	if code == KEY_F and phase == "idle":
		phase = "thinking"
		elapsed = 0.0
		player["pose"] = "think"
		record_text = "威尔逊坐到书桌前，开始沉浸推理……"
	elif code == KEY_E and phase == "thinking":
		phase = "interrupting"
		elapsed = 0.0
		speech_text = "同伴：威尔逊！醒醒！别看那里！"
		record_text = "同伴察觉到异常阴影，快速冲向书桌！"
