class_name Scene1
extends SceneBase

## 第一幕：言灵发动
## 佩戴有效凭证后发动言灵，拨开认知遮蔽，看清客观异常

var credential := false
var reveal := 0.0
var phase := "idle"
var companion: Dictionary = {}

func _init_scene() -> void:
	hud_act = "ACT I"
	hud_title = "言灵发动"
	hud_desc = "举起有效凭证，让言灵短暂拨开认知的遮蔽。事物始终在那里，变化的是你看见的现实。"
	record_text = "书房一切如常。至少，你所见的如此。[按 F 佩戴凭证 / 按 E 发动言灵]"
	
	actors.clear()
	player = create_person("wilson", 590, 580)
	companion = create_person("gray", 825, 460)
	actors.append(player)
	actors.append(companion)
	
	credential = false
	reveal = 0.0
	phase = "idle"
	_update_hud_display()

func _update_scene_logic(dt: float) -> void:
	if phase == "casting" and elapsed > 1.2:
		phase = "revealed"
		player["pose"] = null
		reveal = 7.0
		record_text = "凭证生效。七秒内可看清灰色来客身后的异常投影。"
	
	if reveal > 0.0:
		reveal = maxf(0.0, reveal - dt)
		if reveal == 0.0:
			phase = "idle"
			speech_text = "视线重新蒙上薄雾。"
			record_text = "客观事物没有改变，短暂的现实辨识结束。"
	
	_update_hud_display()

func _update_hud_display() -> void:
	hud_label = "现实辨识"
	hud_value = reveal / 7.0
	hud_note = "凭证有效 · [按 E 发动言灵]" if credential else "[按 F 佩戴有效凭证]"

func _draw_scene_custom() -> void:
	if reveal > 0.0:
		draw_rect(Rect2(0, 0, 1200, 800), Color(0.47, 0.55, 0.52, 0.13))
		if not companion.is_empty():
			draw_shadow(companion["x"], companion["y"], 0.9)
		draw_ellipse_ring(Vector2(player["x"], player["y"]), 95, 38, Color(0.82, 0.76, 0.6, 0.5), 2.0)

func _on_scene_key_down(code: int) -> void:
	if code == KEY_F:
		credential = true
		record_text = "已佩戴有效凭证。现在可以发动言灵。"
		speech_text = ""
	elif code == KEY_E or code == KEY_SPACE:
		if not credential:
			record_text = "没有佩戴凭证，无法发动言灵。[请先按 F 佩戴凭证]"
			speech_text = "威尔逊：缺少锚定现实的凭证……"
		elif phase == "idle":
			phase = "casting"
			elapsed = 0.0
			player["pose"] = "cast"
			speech_text = "sina ante ala e ijo. sina ante e nasin lukin mi taso."
			record_text = "言灵发动：你没有改变事物，你改变的只是我看待它的方式。"
