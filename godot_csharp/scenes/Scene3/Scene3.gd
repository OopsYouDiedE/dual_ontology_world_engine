class_name Scene3
extends SceneBase

## 第三幕：日常互动
## 恶魔伯爵提出邀约，选择回复策略，同台对弈

var earl: Dictionary = {}
var phase := "invite"

func _init_scene() -> void:
	hud_act = "ACT III"
	hud_title = "日常互动"
	hud_desc = "恶魔伯爵提出一场普通的邀约。选择回复策略，与麦斯威尔坐下来下一盘棋。"
	speech_text = "麦斯威尔：侦探，有兴趣陪我下一盘棋吗？"
	record_text = "收到邀约：与麦斯威尔下国际象棋。[按 F 同意入座 / 按 R 婉拒]"
	
	actors.clear()
	player = create_person("wilson", 590, 580)
	earl = create_person("maxwell", 745, 500)
	earl["pose"] = "invite"
	actors.append(player)
	actors.append(earl)
	
	phase = "invite"
	_update_hud_display()

func _update_scene_logic(dt: float) -> void:
	if phase == "seating":
		earl["pose"] = null
		var a = move_person(player, 445, 555, dt)
		var b = move_person(earl, 655, 555, dt)
		if a and b:
			phase = "chess"
			speech_text = "麦斯威尔：请。你执白。"
			record_text = "邀约成立，双方已入座。[按 E 落子]"
	elif phase == "chessMove" and elapsed > 2.4:
		phase = "chess"
		speech_text = "麦斯威尔：轮到你了。"
		record_text = "双方各走了一步。棋局继续。[按 E 继续落子]"
	
	_update_hud_display()

func _update_hud_display() -> void:
	hud_label = "邀约状态"
	hud_value = 1.0 if (phase in ["chess", "chessMove"]) else 0.0
	if phase in ["chess", "chessMove"]:
		hud_note = "双方已同意 · 正在对弈 [按 E 落子]"
	elif phase == "seating":
		hud_note = "入座中……"
	else:
		hud_note = "等待回复 [按 F 同意 / 按 R 婉拒]"

func _draw() -> void:
	if not ready_flag:
		return
	if art_bg:
		draw_texture_rect(art_bg, Rect2(0, 0, 1200, 800), false)
	
	_draw_scene_custom()
	
	if phase in ["chess", "chessMove"]:
		var f_idx = 0 if phase == "chess" else CYCLE[mini(3, int(elapsed / 0.6))]
		draw_sprite_frame(art_actions, 2, f_idx, 550, 575, 180)
	else:
		var sorted_actors = actors.duplicate()
		sorted_actors.sort_custom(func(a, b): return a["y"] < b["y"])
		for p in sorted_actors:
			render_person(p)
	
	if target != null and is_player_free():
		draw_ellipse_ring(target, 12, 5, Color(0.82, 0.76, 0.6, 0.5), 1.5)
	
	draw_hud_panel()

func _on_scene_key_down(code: int) -> void:
	if code == KEY_F and phase == "invite":
		phase = "seating"
		elapsed = 0.0
		speech_text = "威尔逊：既然伯爵有兴致，我奉陪一局。"
		record_text = "接受邀约。正在走向棋桌……"
	elif code == KEY_R and phase == "invite":
		speech_text = "麦斯威尔：遗憾。下一次别让我等太久。"
		record_text = "婉拒了邀约。"
	elif (code == KEY_E or code == KEY_SPACE) and phase == "chess":
		phase = "chessMove"
		elapsed = 0.0
		record_text = "落子思考中……"
