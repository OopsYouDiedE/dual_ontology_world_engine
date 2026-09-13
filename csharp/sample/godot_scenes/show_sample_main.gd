class_name ShowSampleMain
extends Control

## show_sample 统一导航与交互主入口

const WinterScene = preload("res://show_sample/winter_scene.gd")
const MindScene = preload("res://show_sample/mind_scene.gd")
const BookNotebook = preload("res://show_sample/book_notebook.gd")
const MechanismsScene = preload("res://show_sample/mechanisms/mechanisms_scene.gd")

var current_scene_node: Node = null
var current_mode: String = ""

var winter_node: Node2D = null
var mind_node: Node2D = null
var book_node: Control = null
var mech_node: Node2D = null

var content_container: Control
var action_panel: HBoxContainer
var title_label: Label

func _ready() -> void:
	anchor_right = 1.0
	anchor_bottom = 1.0
	setup_ui()
	switch_to("winter")

func setup_ui() -> void:
	var root_vbox = VBoxContainer.new()
	root_vbox.set_anchors_preset(Control.PRESET_FULL_RECT)
	root_vbox.add_theme_constant_override("separation", 4)
	add_child(root_vbox)
	
	# 顶部导航栏
	var nav_bar = PanelContainer.new()
	nav_bar.custom_minimum_size = Vector2(0, 48)
	root_vbox.add_child(nav_bar)
	
	var nav_hbox = HBoxContainer.new()
	nav_hbox.add_theme_constant_override("separation", 15)
	nav_bar.add_child(nav_hbox)
	
	title_label = Label.new()
	title_label.text = "【展示样本集 Show Sample】"
	title_label.add_theme_font_size_override("font_size", 16)
	nav_hbox.add_child(title_label)
	
	var btn_winter = Button.new()
	btn_winter.text = "1. 冬日荒野 (Winter)"
	btn_winter.pressed.connect(func(): switch_to("winter"))
	nav_hbox.add_child(btn_winter)
	
	var btn_mind = Button.new()
	btn_mind.text = "2. 脑内推演 (Mind)"
	btn_mind.pressed.connect(func(): switch_to("mind"))
	nav_hbox.add_child(btn_mind)
	
	var btn_mech = Button.new()
	btn_mech.text = "3. 庄园机制五幕 (Mechanisms)"
	btn_mech.pressed.connect(func(): switch_to("mechanisms"))
	nav_hbox.add_child(btn_mech)
	
	var btn_book = Button.new()
	btn_book.text = "4. 符文手记 (Notebook)"
	btn_book.pressed.connect(func(): switch_to("book"))
	nav_hbox.add_child(btn_book)
	
	# 次级操作按钮栏（用于触发场景内特定动作）
	action_panel = HBoxContainer.new()
	action_panel.custom_minimum_size = Vector2(0, 36)
	action_panel.add_theme_constant_override("separation", 8)
	root_vbox.add_child(action_panel)
	
	# 中间主内容展示区
	content_container = Control.new()
	content_container.size_flags_vertical = Control.SIZE_EXPAND_FILL
	content_container.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content_container.clip_contents = true
	root_vbox.add_child(content_container)

func clear_current() -> void:
	if current_scene_node:
		current_scene_node.queue_free()
		current_scene_node = null
	for c in action_panel.get_children():
		c.queue_free()

func switch_to(mode: String) -> void:
	clear_current()
	current_mode = mode
	
	if mode == "winter":
		title_label.text = "【冬日荒野】WASD移动，靠近空格采集，P暂停"
		winter_node = WinterScene.new()
		content_container.add_child(winter_node)
		current_scene_node = winter_node
		setup_winter_actions()
	elif mode == "mind":
		title_label.text = "【未发生之事：脑内推演】点击下方词语槽位或施放言灵"
		mind_node = MindScene.new()
		content_container.add_child(mind_node)
		current_scene_node = mind_node
		setup_mind_actions()
	elif mode == "mechanisms":
		title_label.text = "【庄园机制五幕剧】点击下方选择幕次与交互动作"
		mech_node = MechanismsScene.new()
		content_container.add_child(mech_node)
		current_scene_node = mech_node
		setup_mech_actions()
	elif mode == "book":
		title_label.text = "【符文与手记】点击符文查看详情，Space开合，左右翻页"
		book_node = BookNotebook.new()
		content_container.add_child(book_node)
		current_scene_node = book_node

func setup_winter_actions() -> void:
	var reset_btn = Button.new()
	reset_btn.text = "重置营地与物资"
	reset_btn.pressed.connect(func(): if winter_node: winter_node.reset())
	action_panel.add_child(reset_btn)
	
	var pause_btn = Button.new()
	pause_btn.text = "暂停 / 继续 (P)"
	pause_btn.pressed.connect(func(): if winter_node: winter_node.set_pause(not winter_node.paused))
	action_panel.add_child(pause_btn)

func setup_mind_actions() -> void:
	var b_rule = Button.new()
	b_rule.text = "写入/移除规则(潮湿遇火→蒸汽)"
	b_rule.pressed.connect(func():
		if mind_node:
			mind_node.rule_pieces = [true, true, true]
			mind_node.write_rule()
	)
	action_panel.add_child(b_rule)
	
	var b_seli = Button.new()
	b_seli.text = "言灵: 火焰 (sina o seli)"
	b_seli.pressed.connect(func():
		if mind_node:
			mind_node.pick_word("sina")
			mind_node.pick_word("o")
			mind_node.pick_word("seli")
			mind_node.cast()
	)
	action_panel.add_child(b_seli)
	
	var b_telo = Button.new()
	b_telo.text = "言灵: 水 (sina o telo)"
	b_telo.pressed.connect(func():
		if mind_node:
			mind_node.pick_word("sina")
			mind_node.pick_word("o")
			mind_node.pick_word("telo")
			mind_node.cast()
	)
	action_panel.add_child(b_telo)
	
	var b_lete = Button.new()
	b_lete.text = "言灵: 霜 (sina o lete)"
	b_lete.pressed.connect(func():
		if mind_node:
			mind_node.pick_word("sina")
			mind_node.pick_word("o")
			mind_node.pick_word("lete")
			mind_node.cast()
	)
	action_panel.add_child(b_lete)
	
	var b_restore = Button.new()
	b_restore.text = "还原心象"
	b_restore.pressed.connect(func(): if mind_node: mind_node.restore())
	action_panel.add_child(b_restore)
	
	var b_reset = Button.new()
	b_reset.text = "重新推演"
	b_reset.pressed.connect(func(): if mind_node: mind_node.reset_mind())
	action_panel.add_child(b_reset)

func setup_mech_actions() -> void:
	for i in range(5):
		var act_btn = Button.new()
		act_btn.text = "第%s幕: %s" % [["一","二","三","四","五"][i], MechanismsScene.CHAPTERS[i][0]]
		act_btn.pressed.connect(func():
			if mech_node:
				mech_node.select_scene(i)
				refresh_mech_sub_actions(i)
		)
		action_panel.add_child(act_btn)
	refresh_mech_sub_actions(0)

func refresh_mech_sub_actions(act_index: int) -> void:
	while action_panel.get_child_count() > 5:
		action_panel.get_child(5).queue_free()
	
	if act_index == 0:
		var cred_btn = Button.new()
		cred_btn.text = "佩戴/取下有效凭证"
		cred_btn.pressed.connect(func():
			if mech_node:
				mech_node.credential = not mech_node.credential
				mech_node.record_text = "凭证与言灵建立联系。" if mech_node.credential else "凭证已取下。"
				mech_node.update_hud()
		)
		action_panel.add_child(cred_btn)
		
		var cast_btn = Button.new()
		cast_btn.text = "发动言灵"
		cast_btn.pressed.connect(func():
			if mech_node:
				if not mech_node.credential:
					mech_node.record_text = "言灵没有响应。你需要一件有效凭证。"
					return
				mech_node.set_phase("casting")
				mech_node.player["pose"] = "cast"
				mech_node.speech_text = "sina ante ala e ijo.\nsina ante e nasin lukin mi taso."
				mech_node.record_text = "你没有改变事物。你改变的，只是我看待它的方式。"
		)
		action_panel.add_child(cast_btn)
	
	elif act_index == 1:
		var danger_btn = Button.new()
		danger_btn.text = "使用危险词"
		danger_btn.pressed.connect(func():
			if mech_node:
				mech_node.set_phase("danger")
				mech_node.record_text = "不是规则的结果。这个词本身正在侵蚀自我。"
				mech_node.speech_text = "不对……这个词不能继续想下去。"
		)
		action_panel.add_child(danger_btn)
		
		var stop_btn = Button.new()
		stop_btn.text = "立刻停止联想"
		stop_btn.pressed.connect(func():
			if mech_node:
				mech_node.set_phase("stopped")
				mech_node.danger = 0.0
				mech_node.speech_text = "停下来。换一种说法。"
				mech_node.record_text = "联想已经中断，危险词不再参与推演。"
		)
		action_panel.add_child(stop_btn)
		
		var sim_btn = Button.new()
		sim_btn.text = "以「来访者」替换推演"
		sim_btn.pressed.connect(func():
			if mech_node:
				mech_node.set_phase("simulate")
				mech_node.danger = 0.0
				mech_node.companion["x"] = 320.0
				mech_node.companion["y"] = 640.0
				mech_node.rule = true
				mech_node.speech_text = "来访者 → 经过门口 → 沿边缘行走"
				mech_node.record_text = "替代词已写入。灰色人偶开始复现进入时的动作。"
		)
		action_panel.add_child(sim_btn)
	
	elif act_index == 2:
		var accept_btn = Button.new()
		accept_btn.text = "同意邀约 · 坐下对弈"
		accept_btn.pressed.connect(func():
			if mech_node:
				mech_node.set_phase("seating")
				mech_node.speech_text = "威尔逊：好，让我们开始。"
				mech_node.record_text = "回复策略：同意。双方走向棋桌。"
		)
		action_panel.add_child(accept_btn)
		
		var move_btn = Button.new()
		move_btn.text = "落下一枚棋子"
		move_btn.pressed.connect(func():
			if mech_node:
				mech_node.set_phase("chessMove")
				mech_node.record_text = "威尔逊落子。麦斯威尔思索后回应。"
		)
		action_panel.add_child(move_btn)
	
	elif act_index == 3:
		var trick_btn = Button.new()
		trick_btn.text = "贴上恶作剧规则"
		trick_btn.pressed.connect(func():
			if mech_node:
				mech_node.set_phase("circling")
				mech_node.rule = true
				mech_node.record_text = "规则已贴上。经过的人开始绕圈。"
				mech_node.speech_text = "威尔逊：应该……不会有什么大问题。"
		)
		action_panel.add_child(trick_btn)
	
	elif act_index == 4:
		var think_btn = Button.new()
		think_btn.text = "开始伏案推理"
		think_btn.pressed.connect(func():
			if mech_node:
				mech_node.set_phase("thinking")
				mech_node.player["pose"] = "think"
				mech_node.record_text = "线索正在脑内靠拢；具体连接不予展示。"
				mech_node.speech_text = "威尔逊：只差一点……"
		)
		action_panel.add_child(think_btn)
		
		var int_btn = Button.new()
		int_btn.text = "同伴靠近 · 立即打断"
		int_btn.pressed.connect(func():
			if mech_node:
				mech_node.set_phase("interrupting")
				mech_node.speech_text = "同伴：威尔逊！看着我！"
				mech_node.record_text = "同伴发现异状，立刻上前打断。"
		)
		action_panel.add_child(int_btn)
