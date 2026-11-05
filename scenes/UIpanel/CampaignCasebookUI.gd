extends CanvasLayer
## The casebook displays the detective's Core knowledge and proof certificates.
## Checking a card submits its evidence ID to Core; this UI never judges a proof.
var host: Node3D
var root: Control
var clock_label: Label
var room_label: Label
var mode_label: Label
var feedback_label: Label
var progress: ProgressBar
var context_title: Label
var path_title: Label
var path_status: Label
var physiology_summary: Label
var context_hint: Label
var targets: VBoxContainer
var actions: VBoxContainer
var pause_button: Button
var control_button: Button
var return_title_button: Button
var book: PanelContainer
var left_page: VBoxContainer
var right_page: VBoxContainer
var book_title: Label
var _tab := "evidence"
var _last_signature := ""
var _book_signature := ""
var _selected_deduction := ""
var _selected_evidence: Dictionary = {}
var _preview: Dictionary = {}
var _selected_incantation := ""
var _selected_opinion := ""
var _page_text: Array = []
var _journal_day := 0
var _journal_expanded: Dictionary = {}
var _journal_cache_signature := ""
var _journal_cache: Array = []
const INK = Color("312c24")
const PAPER = Color("e8d6af")
const GOLD = Color("c8a56e")
const NIGHT = Color("201d1a")
const PERSONAL_MOTIVE_LABELS = {"curiosity": "求知满足", "purpose": "专业责任满足", "agency": "自主与掌控满足"}

func configure(owner: Node3D) -> void:
	host = owner
	root = Control.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	if ResourceLoader.exists("res://assets/ui/OverlayTheme.tres"): root.theme = load("res://assets/ui/OverlayTheme.tres")
	add_child(root)
	var header := PanelContainer.new()
	header.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	header.offset_left = 22; header.offset_top = 18; header.offset_right = -22; header.offset_bottom = 82
	header.add_theme_stylebox_override("panel", _style(Color("211d19e8"), GOLD, 12, 1))
	root.add_child(header)
	var bar := HBoxContainer.new()
	bar.add_theme_constant_override("separation", 18)
	header.add_child(bar)
	var names := VBoxContainer.new()
	names.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bar.add_child(names)
	room_label = _label("", 23, Color("f1e7d2"))
	names.add_child(room_label)
	clock_label = _label("", 16, GOLD)
	names.add_child(clock_label)
	mode_label = _label("", 15, Color("d2c4aa"))
	bar.add_child(mode_label)
	pause_button = _button("暂停", func(): host.set_paused(not host.player_paused); refresh(true))
	bar.add_child(pause_button)
	for speed in [1, 16, 128, 1024]:
		var value := float(speed)
		bar.add_child(_button(str(speed) + "×", func(): host.set_speed(value); refresh(true)))
	control_button = _button("", func(): host.set_control_ai(not host.controller_ai); refresh(true))
	bar.add_child(control_button)
	bar.add_child(_button("案卷  Tab", func(): toggle_book("evidence")))
	bar.add_child(_button("保存", func(): host.save_game(); refresh(true)))
	var context := PanelContainer.new()
	context.anchor_left = 1.0; context.anchor_right = 1.0; context.anchor_top = 0.0; context.anchor_bottom = 1.0
	context.offset_left = -358; context.offset_top = 102; context.offset_right = -22; context.offset_bottom = -128
	context.add_theme_stylebox_override("panel", _style(Color("25211de8"), Color("756047"), 16, 1))
	root.add_child(context)
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 10)
	context.add_child(column)
	path_title = _label("当前意向", 17, Color("d7bd8c"))
	column.add_child(path_title)
	path_status = _label("等待新的现场信息。", 14, Color("d1c4aa"))
	path_status.max_lines_visible = 3
	column.add_child(path_status)
	physiology_summary = _label("", 14, Color("d1c4aa"))
	physiology_summary.max_lines_visible = 2
	column.add_child(physiology_summary)
	column.add_child(_button("意向与状态", func(): toggle_book("intent", true)))
	column.add_child(HSeparator.new())
	context_title = _label("观察与行动", 21, Color("f1e7d2"))
	column.add_child(context_title)
	context_hint = _label("点击场景中的人物或家具，也可从下方选择。", 14, Color("d1c4aa"))
	column.add_child(context_hint)
	var target_scroll := ScrollContainer.new()
	target_scroll.custom_minimum_size.y = 80
	target_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	column.add_child(target_scroll)
	targets = VBoxContainer.new()
	targets.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	target_scroll.add_child(targets)
	column.add_child(HSeparator.new())
	var action_scroll := ScrollContainer.new()
	action_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	column.add_child(action_scroll)
	actions = VBoxContainer.new()
	actions.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_theme_constant_override("separation", 8)
	action_scroll.add_child(actions)
	var footer := PanelContainer.new()
	footer.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	footer.offset_left = 22; footer.offset_top = -110; footer.offset_right = -22; footer.offset_bottom = -18
	footer.add_theme_stylebox_override("panel", _style(Color("201c18ed"), Color("756047"), 12, 1))
	root.add_child(footer)
	var footer_column := VBoxContainer.new()
	footer.add_child(footer_column)
	feedback_label = _label("WASD 行走 · 点击物件或按 E 查看行动 · 滚轮缩放 · Tab 案卷 · 右键返回", 17, Color("f0e6cf"))
	feedback_label.max_lines_visible = 3
	footer_column.add_child(feedback_label)
	progress = ProgressBar.new()
	progress.custom_minimum_size.y = 8
	progress.max_value = 1.0
	progress.show_percentage = false
	footer_column.add_child(progress)
	return_title_button = _button("返回标题", _return_to_title)
	return_title_button.visible = false
	footer_column.add_child(return_title_button)
	_build_book()
	refresh(true)

func _return_to_title() -> void:
	var main := get_tree().current_scene
	if is_instance_valid(main) and main.has_method("return_native_title"):
		main.call_deferred("return_native_title")

func _style(color: Color, border: Color, margin: int, width: int = 1) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.border_color = border
	style.set_border_width_all(width)
	style.set_content_margin_all(margin)
	style.set_corner_radius_all(4)
	return style

func _label(text: String, size: int = 18, color: Color = INK) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return label

func _button(text: String, callback: Callable, paper := false) -> Button:
	var button := Button.new()
	button.text = text
	button.custom_minimum_size.y = 36
	button.add_theme_font_size_override("font_size", 17)
	button.add_theme_color_override("font_color", INK if paper else Color("f1e8d4"))
	button.add_theme_color_override("font_hover_color", Color("191612") if paper else Color.WHITE)
	button.add_theme_color_override("font_focus_color", INK if paper else Color.WHITE)
	button.add_theme_stylebox_override("normal", _style(Color("ddc69a") if paper else Color("393026"), Color("b39360") if paper else Color("716047"), 9))
	button.add_theme_stylebox_override("hover", _style(Color("f3e5c7") if paper else Color("665036"), GOLD, 9))
	button.add_theme_stylebox_override("pressed", _style(Color("c9b182") if paper else Color("181613"), GOLD, 9))
	button.add_theme_stylebox_override("focus", _style(Color(0, 0, 0, 0), GOLD, 9, 2))
	button.pressed.connect(callback)
	return button

func _build_book() -> void:
	book = PanelContainer.new()
	book.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	book.offset_left = 65; book.offset_top = 104; book.offset_right = -65; book.offset_bottom = -28
	book.add_theme_stylebox_override("panel", _style(Color("392b20"), GOLD, 12, 2))
	root.add_child(book)
	var all_pages := VBoxContainer.new()
	all_pages.add_theme_constant_override("separation", 10)
	book.add_child(all_pages)
	var toolbar := HBoxContainer.new()
	toolbar.add_theme_constant_override("separation", 7)
	all_pages.add_child(toolbar)
	for tab in [["evidence", "证据与认知"], ["deduction", "脑内推演"], ["incantation", "言灵"], ["opinion", "协商"], ["intent", "意向与状态"], ["map", "空间与动线"], ["journal", "事件记录"]]:
		var id := str(tab[0])
		toolbar.add_child(_button(str(tab[1]), func(): _tab = id; _book_signature = ""; _render_book()))
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	toolbar.add_child(spacer)
	toolbar.add_child(_button("合上案卷  ×", close_book))
	var spread := HBoxContainer.new()
	spread.add_theme_constant_override("separation", 3)
	spread.size_flags_vertical = Control.SIZE_EXPAND_FILL
	all_pages.add_child(spread)
	for side in range(2):
		var page := PanelContainer.new()
		page.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		page.size_flags_stretch_ratio = 1.0
		page.add_theme_stylebox_override("panel", _style(PAPER if side == 0 else Color("e6d2a8"), Color("b69b6a"), 22, 0))
		spread.add_child(page)
		var scroll := ScrollContainer.new()
		scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
		page.add_child(scroll)
		var column := VBoxContainer.new()
		column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		column.add_theme_constant_override("separation", 12)
		scroll.add_child(column)
		if side == 0: left_page = column
		else: right_page = column
		if side == 0:
			var spine := ColorRect.new()
			spine.color = Color("9a8058")
			spine.custom_minimum_size.x = 13
			spread.add_child(spine)
	book.hide()

func _clear(node: Node) -> void:
	for child in node.get_children():
		node.remove_child(child)
		child.queue_free()

func refresh(force := false) -> void:
	if not is_instance_valid(host): return
	var minutes := int(float(host.state.get("game_seconds", 0.0)) / 60.0)
	room_label.text = host.room_label(host.room_id)
	clock_label.text = "第 %d 日   %02d:%02d   ·   %s" % [minutes / 1440 + 1, (minutes / 60) % 24, minutes % 60, "暂停" if host.player_paused else str(int(host.time_scale)) + "×"]
	mode_label.text = "全员自主运行" if host.controller_ai else "侦探接管"
	if _open_arrangement(): mode_label.text += " · " + str(_autonomy_phase().get("label", "自主安排"))
	pause_button.text = "继续" if host.player_paused else "暂停"
	control_button.text = "接管侦探" if host.controller_ai else "交给 AI"
	return_title_button.visible = not bool(host.state.get("active", true))
	feedback_label.text = host.feedback if not str(host.feedback).is_empty() else "WASD 行走 · 点击物件或按 E 查看行动 · 滚轮缩放 · Tab 案卷 · 右键返回"
	var work: Dictionary = host.bridge.WorkProgress(str(host.definition.viewer))
	progress.visible = not work.is_empty()
	progress.value = float(work.get("progress", 0.0))
	var choices: Array = host.available_actions()
	_refresh_path_summary()
	var signature: String = str(host.room_id) + "|" + str(host.selected_focus) + "|" + str(host.controller_ai) + "|" + str(choices.map(func(a): return a.id)) + "|" + _path_signature() + "|" + _presence_signature()
	if force or signature != _last_signature:
		_last_signature = signature
		_render_context(choices)
	if book.visible:
		# Other people keep acting while a page is open. Rebuild a page only
		# when the detective's own evidence, outcomes or choices change.
		var new_signature: String = _tab + "|" + _knowledge_signature() + "|" + str(host.state.get("mechanic_history", []).size()) + "|" + signature
		if _tab == "journal": new_signature += "|" + str(host.journal.size()) + "|" + str(host.get("journal_dropped_total"))
		if force or new_signature != _book_signature:
			_book_signature = new_signature
			_render_book()
	if bool(host.state.get("active", true)) == false:
		var campaign := _campaign_progress()
		if str(campaign.get("mode", "facts")) == "observation_window":
			mode_label.text = "观察窗口结束"
			feedback_label.text = str(campaign.get("label", "本次观察已结束")) + "。可以在事件记录中回看已经发生的选择与后果。"
			var end_minutes := int(campaign.get("end_minutes", minutes))
			if end_minutes > 0 and end_minutes % 1440 == 0:
				clock_label.text = "记录至第 %d 日   24:00" % [end_minutes / 1440]
		else:
			mode_label.text = "五日完成" if bool(host.state.get("campaign_complete", false)) else "本次五日已结束"
			feedback_label.text = str(host.definition.get("completion", {}).get("label", "五日完成。")) if bool(host.state.get("campaign_complete", false)) else "五日已结束，案卷中仍有未解决事项。可读取存档继续调查。"

func _render_context(choices: Array) -> void:
	_clear(targets)
	_clear(actions)
	var visible: Array = host.visible_targets()
	if visible.is_empty():
		targets.add_child(_label(_waiting_text(), 16, Color("d1c4aa")))
	for target: Dictionary in visible:
		var id := str(target.id)
		var caption := str(target.label)
		caption += "  ·  " + str(target.actions.size()) + " 项" if not target.actions.is_empty() else "  ·  在场"
		var button := _button(caption, func(): host.select_focus(id))
		button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		targets.add_child(button)
	var selected := str(host.selected_focus)
	context_title.text = host.focus_label(selected) if not selected.is_empty() else "观察与行动"
	if selected.is_empty():
		actions.add_child(_label("选择人物或物件；你所知道的事情会决定这里出现哪些行动。", 16, Color("d1c4aa")))
		return
	var person_here: bool = host._bodies.has(selected) and selected != str(host.definition.viewer) and host._actor_visible(selected)
	if person_here:
		actions.add_child(_label("眼下正在：" + str(host.visible_activity_label(selected)), 15, Color("d1c4aa")))
	var count := 0
	for action: Dictionary in choices:
		if not host.action_matches_focus(action, selected): continue
		count += 1
		var id := str(action.id)
		var category := str(action.category)
		var label := str(action.label)
		if category == "deduction" and not label.begins_with("脑内推演"): label = "脑内推演 · " + label
		elif category == "incantation" and not label.begins_with("言灵"): label = "言灵 · " + label
		elif category == "opinion" and not label.begins_with("协商"): label = "协商 · " + label
		var button := _button(label, func(): _choose_action(id, category))
		button.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		button.disabled = host.controller_ai
		actions.add_child(button)
		actions.add_child(_label("约 " + str(float(action.minutes)) + " 分钟", 14, Color("bdac8e")))
	if count == 0:
		var empty_text := "目前没有可以向这位同伴提出的已知话题。可以先观察现场，或在案卷中核对已有陈述。" if person_here else (_waiting_text() if choices.is_empty() else "此目标暂时没有可用行动；可以查看其他人物或物件。")
		actions.add_child(_label(empty_text, 16, Color("d1c4aa")))

func _paths() -> Dictionary:
	var value: Variant = host.state.get("player_paths", {})
	return value if value is Dictionary else {}

func _presence_signature() -> String:
	var people: Array[String] = []
	for actor in host._bodies:
		if actor == str(host.definition.viewer) or not host._actor_visible(actor): continue
		people.append(str(actor) + ":" + str(host.visible_activity_label(actor)))
	return "|".join(people)

func _knowledge_signature() -> String:
	# A second witness or a conflicting account matters even if the retained
	# text did not change. Only the viewer's public knowledge enters this key.
	var records: Array = []
	for item: Dictionary in host.state.get("knowledge", []):
		var key: Dictionary = item.get("key", {})
		var fact: Dictionary = item.get("fact", {})
		if str(key.get("Object", "")).begins_with("event:") or str(key.get("Field", "")) in ["room", "spot", "asleep", "arrived"]: continue
		if not fact.get("Value") is String and str(key.get("Facet", "")) != "infer": continue
		records.append([key, fact.get("Value"), fact.get("Known", false), fact.get("Conflicted", false), fact.get("Confidence", 0.0), item.get("source_ids", []), item.get("sources", [])])
	return str(records.hash())

func _campaign_progress() -> Dictionary:
	var value: Variant = host.state.get("campaign_progress", {})
	return value if value is Dictionary else {}

func _autonomy_phase() -> Dictionary:
	var value: Variant = _campaign_progress().get("phase", {})
	return value if value is Dictionary else {}

func _open_arrangement() -> bool:
	var phase := _autonomy_phase()
	return bool(phase.get("active", false)) and not bool(phase.get("task_guidance", true))

func _path_signature() -> String:
	var paths := _paths()
	var task: Dictionary = paths.get("task", {})
	var physiology: Dictionary = paths.get("physiology", {})
	var parts: Array[String] = [str(paths.get("selected", "none")), str(task.get("id", "")), str(task.get("status", "none")), str(task.get("completed_steps", 0)), str(task.get("reason", "")), str(task.get("blockers", []))]
	parts.append(str(_autonomy_phase().hash()))
	parts.append(str(host.state.get("active", true)))
	var active_intent: Variant = host.state.get("active_intent")
	parts.append(str(active_intent.hash()) if active_intent is Dictionary else "idle")
	var route: Variant = physiology.get("route")
	parts.append(str(physiology.get("status", "")) + ":" + str(physiology.get("route_is_preview", false)) + ":" + str(physiology.get("recent_routes", []).size()))
	if route is Dictionary: parts.append(str(route.get("id", "")) + ":" + str(route.get("step", 0)) + ":" + str(route.get("returning", false)) + ":" + str(route.get("completed_actions", []).size()))
	for recommendation: Dictionary in physiology.get("recommended_actions", []): parts.append(str(recommendation.get("id", "")))
	for need: Dictionary in physiology.get("needs", []):
		parts.append(str(need.get("id", "")) + ":" + str(int(roundf(float(need.get("value", 0.0))))) + ":" + str(need.get("urgency", "normal")))
	var personal_needs: Dictionary = host.state.get("needs", {})
	for need_id in PERSONAL_MOTIVE_LABELS:
		if personal_needs.has(need_id): parts.append(str(need_id) + ":" + str(int(roundf(float(personal_needs[need_id])))))
	return "|".join(parts)

func _task_status(status: String) -> String:
	match status:
		"active": return "进行中"
		"blocked": return "等待条件"
		"interrupted": return "照料身体后续接"
		"complete": return "此项已完成"
	return "尚无已知任务"

func _refresh_path_summary() -> void:
	var paths := _paths()
	var task: Dictionary = paths.get("task", {})
	var physiology: Dictionary = paths.get("physiology", {})
	var open_arrangement := _open_arrangement()
	var phase := _autonomy_phase()
	var label := str(task.get("label", ""))
	path_title.text = str(phase.get("label", "自主安排")) if open_arrangement else (label if not label.is_empty() else "当前意向")
	path_title.max_lines_visible = 2
	var total := int(task.get("total_steps", 0))
	var done := int(task.get("completed_steps", 0))
	var status := _task_status(str(task.get("status", "none")))
	path_status.text = str(phase.get("description", "根据自己的见闻与需要安排接下来的行动。")) if open_arrangement else status + (" · %d/%d" % [done, total] if total > 0 else "")
	var reason := "" if open_arrangement else str(paths.get("reason", task.get("reason", "")))
	var active_intent: Variant = host.state.get("active_intent")
	if active_intent is Dictionary:
		path_status.text = "实际正在执行：" + _intent_caption(active_intent) + "\n" + path_status.text
		reason = str(active_intent.get("Reason", ""))
	elif not reason.is_empty(): reason = "当前建议：" + reason
	if not reason.is_empty(): path_status.text += "\n" + reason
	var urgent: Array[String] = []
	for need: Dictionary in physiology.get("needs", []):
		if str(need.get("urgency", "normal")) in ["urgent", "low"]:
			urgent.append(str(need.get("label", "身体状态")) + " " + str(int(roundf(float(need.get("value", 0.0))))))
	physiology_summary.text = "身体状态平稳" if urgent.is_empty() else "需要照料 · " + "、".join(urgent)
	var route: Variant = physiology.get("route")
	if route is Dictionary:
		if bool(physiology.get("route_is_preview", false)): physiology_summary.text = "建议 · " + str(route.get("label", "补给往返"))
		elif bool(route.get("returning", false)): physiology_summary.text = "返回出发地点 · 补给路线 %d/%d" % [int(route.get("step", 0)), int(route.get("total_steps", 0))]
		else: physiology_summary.text = "补给途中 · " + str(route.get("label", "照料身体"))
	physiology_summary.visible = not physiology.is_empty()
	physiology_summary.add_theme_color_override("font_color", Color("e6b08e") if str(physiology.get("status", "")) == "urgent" else Color("d1c4aa"))
	context_hint.text = "由你选择行动与证据；意向栏只提供当前建议。" if not host.controller_ai else "人物会依已知任务和身体状态安排接下来的行动。"
	if open_arrangement:
		context_hint.text = "由你根据现场和自身需要选择行动。" if not host.controller_ai else "人物会依自己的见闻、身体需要与职责选择行动。"

func _waiting_text() -> String:
	if not bool(host.state.get("active", true)):
		return "观察窗口已经结束，可以查看事件记录、保存并返回标题。" if str(_campaign_progress().get("mode", "facts")) == "observation_window" else "此轮调查已经结束，可以保存并返回标题。"
	var work: Dictionary = host.bridge.WorkProgress(str(host.definition.viewer))
	if not work.is_empty():
		var id := str(work.get("action", ""))
		return "正在" + str(host._actions.get(id, {}).get("label", "完成手边行动")) + "。可以暂停查看案卷。"
	var pending: Variant = host.get("_pending_action")
	var approaching: String = pending if pending is String else ""
	if not approaching.is_empty(): return "正在走向交互位置；到达后再开始行动。"
	if _open_arrangement(): return "可以观察人物与现场变化，也可以照料自己的身体、查阅案卷或继续时间。"
	var paths := _paths()
	var task: Dictionary = paths.get("task", {})
	var blockers: Array = task.get("blockers", [])
	if not blockers.is_empty():
		var lines: Array[String] = []
		for item in blockers.slice(0, 3): lines.append(str(item))
		return "当前等待：\n" + "\n".join(lines)
	var reason := str(task.get("reason", paths.get("reason", "")))
	if not reason.is_empty(): return reason
	return "暂时没有新的可用行动。留意同伴到场、交谈和现场变化；可以暂停查阅案卷，也可以继续时间。"

func _render_intent() -> void:
	var open_arrangement := _open_arrangement()
	var phase := _autonomy_phase()
	_heading(left_page, str(phase.get("label", "自主安排")) if open_arrangement else "已知任务", str(phase.get("description", "根据自己的见闻与需要安排接下来的行动。")) if open_arrangement else "只展示已经获知的目标、当前进度与可用行动。")
	_heading(right_page, "状态与日常", "低值表示相应需要尚未满足；身体感受、求知与责任都会影响自己的选择。")
	var paths := _paths()
	var task: Dictionary = paths.get("task", {})
	var physiology: Dictionary = paths.get("physiology", {})
	var active_intent: Variant = host.state.get("active_intent")
	if active_intent is Dictionary: _card(left_page, "实际正在执行 · " + _intent_caption(active_intent), str(active_intent.get("Reason", "")))
	var label := str(task.get("label", ""))
	if open_arrangement:
		left_page.add_child(_label("可根据已知线索与现场变化自行选择行动；人物会继续处理各自的需要和事务。"))
	elif label.is_empty():
		left_page.add_child(_label("目前尚无明确的已知任务。观察现场或听取同伴的信息，会让下一步逐渐清楚。"))
	else:
		var summary := _task_status(str(task.get("status", "none")))
		var total := int(task.get("total_steps", 0))
		if total > 0: summary += " · 已完成 %d/%d 项" % [int(task.get("completed_steps", 0)), total]
		var reason := str(task.get("reason", ""))
		if not reason.is_empty(): summary += "\n" + reason
		_card(left_page, label, summary)
	if not open_arrangement:
		for blocker in task.get("blockers", []): _card(left_page, "当前阻碍", str(blocker))
		_render_recommendations(left_page, task.get("recommended_actions", []))
	_render_recovery_route(right_page, physiology)
	var needs: Array = physiology.get("needs", [])
	if needs.is_empty(): right_page.add_child(_label("尚无可显示的身体状态记录。"))
	for need: Dictionary in needs:
		var value := clampf(float(need.get("value", 0.0)), 0.0, 100.0)
		var urgency := str(need.get("urgency", "normal"))
		var need_label := str(need.get("label", "身体状态"))
		var state_text := "急需照料" if urgency == "urgent" else ("偏低" if urgency == "low" else "平稳")
		var color := Color("8a3324") if urgency == "urgent" else (Color("7e6038") if urgency == "low" else Color("3f5a3a"))
		right_page.add_child(_label("%s  %.0f/100 · %s" % [need_label, value, state_text], 18, color))
		var meter := ProgressBar.new()
		meter.max_value = 100.0
		meter.value = value
		meter.show_percentage = false
		meter.custom_minimum_size.y = 8
		meter.add_theme_stylebox_override("background", _style(Color("d0bd97"), Color("d0bd97"), 0, 0))
		meter.add_theme_stylebox_override("fill", _style(color, color, 0, 0))
		right_page.add_child(meter)
	_render_personal_motives(right_page)
	_render_recommendations(right_page, physiology.get("recommended_actions", []))
	_render_recovery_history(right_page, physiology)
	left_page.add_child(_label("行动与证据由你亲自选择；事件记录会保留实际发生的结果。" if open_arrangement else "接管时由你选择行动，任务提示不会替你勾选证据或发动言灵。", 15, Color("76613f")))

func _render_personal_motives(page: VBoxContainer) -> void:
	var needs: Dictionary = host.state.get("needs", {})
	var shown := false
	for need_id in PERSONAL_MOTIVE_LABELS:
		if not needs.has(need_id): continue
		if not shown:
			page.add_child(HSeparator.new())
			page.add_child(_label("自己的兴趣与责任", 20))
			page.add_child(_label("亲身学习、完成工作与作出选择，会改变这些感受。", 15, Color("76613f")))
			shown = true
		var value := clampf(float(needs[need_id]), 0.0, 100.0)
		var color := Color("655442")
		page.add_child(_label("%s  %.0f/100" % [str(PERSONAL_MOTIVE_LABELS[need_id]), value], 18, color))
		var meter := ProgressBar.new()
		meter.max_value = 100.0
		meter.value = value
		meter.show_percentage = false
		meter.custom_minimum_size.y = 8
		meter.add_theme_stylebox_override("background", _style(Color("d0bd97"), Color("d0bd97"), 0, 0))
		meter.add_theme_stylebox_override("fill", _style(color, color, 0, 0))
		page.add_child(meter)

func _intent_caption(value: Variant) -> String:
	if not value is Dictionary: return ""
	var path := str(value.get("Path", "")).to_lower()
	var label := str({"task": "任务推进", "physiology": "身体需要", "player": "本人选择", "daily": "日常行动", "goal": "自主目标"}.get(path, "已记录的行动"))
	if value.get("PhysiologicalRoute") != null: label += " · 补给往返"
	return label

func _render_recovery_route(page: VBoxContainer, physiology: Dictionary) -> void:
	var route: Variant = physiology.get("route")
	if not route is Dictionary: return
	var preview := bool(physiology.get("route_is_preview", false))
	var returning := bool(route.get("returning", false))
	var title := "建议补给路线" if preview else ("返回出发地点" if returning else "补给途中")
	var lines: Array[String] = [str(route.get("label", "照料身体"))]
	if preview:
		lines.append("尚未出发。可从当前可用行动中自行选择。")
	else:
		lines.append("当前第 %d/%d 步；只在实际完成行动后记录进度。" % [int(route.get("step", 0)), int(route.get("total_steps", 0))])
		if route.get("started_at") != null: lines.append("开始于 " + _log_clock(float(route.started_at)))
		if route.get("recovered_at") != null: lines.append("实际恢复记录：" + _log_clock(float(route.recovered_at)))
		var completed: Array[String] = []
		for step: Dictionary in route.get("completed_actions", []):
			var action_id := str(step.get("Action", ""))
			completed.append(str(host._actions.get(action_id, {}).get("label", "已完成行动")))
		if not completed.is_empty(): lines.append("已经完成：" + "、".join(completed))
	_card(page, title, "\n".join(lines))

func _render_recovery_history(page: VBoxContainer, physiology: Dictionary) -> void:
	var receipts: Array = physiology.get("recent_routes", [])
	if receipts.is_empty(): return
	page.add_child(HSeparator.new())
	page.add_child(_label("近期补给记录 · 保留最近 %d 条（上限 16 条）" % receipts.size(), 16, Color("76613f")))
	var labels: Dictionary = {}
	for need: Dictionary in physiology.get("needs", []): labels[str(need.get("id", ""))] = str(need.get("label", "身体状态"))
	for index in range(receipts.size() - 1, -1, -1):
		var receipt: Dictionary = receipts[index]
		var outcome := "补给往返的全部步骤已完成。" if str(receipt.get("outcome", "")) == "completed" else "已改选其他行动；此前完成的步骤保留在记录中。"
		var label := str(labels.get(str(receipt.get("need", "")), "身体状态"))
		outcome += "\n%s：%.1f → %.1f" % [label, float(receipt.get("need_before", 0.0)), float(receipt.get("need_after", 0.0))]
		if receipt.get("recovered_at") != null: outcome += "\n实际恢复于 " + _log_clock(float(receipt.recovered_at))
		else: outcome += "\n这条记录没有实际恢复回执。"
		var refs: Array = []
		for step: Dictionary in receipt.get("steps", []): refs.append("execution/" + str(step.get("Execution", "")))
		_journal_card(page, {"time": float(receipt.get("finished_at", 0.0)), "first_time": float(receipt.get("started_at", 0.0)), "actor_name": str(host._actors.get(str(host.definition.viewer), {}).get("name", "侦探")), "label": label + "补给往返", "outcome": outcome, "source_count": receipt.get("steps", []).size(), "source_refs": refs, "details": receipt.duplicate(true), "entry_id": "recovery/" + str(receipt.get("route", "")) + "/" + str(receipt.get("started_at", 0.0))})

func _render_recommendations(page: VBoxContainer, suggestions: Array) -> void:
	for suggestion: Dictionary in suggestions:
		var label := str(suggestion.get("label", ""))
		if label.is_empty(): continue
		_card(page, "当前可选 · " + label, str(suggestion.get("reason", "可从场景中的相关人物或物件选择。")))

func _choose_action(id: String, category: String) -> void:
	if category == "deduction":
		present_deduction(id); return
	if category == "incantation":
		_selected_incantation = id; toggle_book("incantation", true); return
	if category == "opinion":
		_selected_opinion = id; toggle_book("opinion", true); return
	var error: String = host.request_action(id)
	if not error.is_empty(): host.feedback = error
	refresh(true)

func present_deduction(id: String) -> void:
	_selected_deduction = id
	_selected_evidence.clear()
	toggle_book("deduction", true)

func toggle_book(tab := "evidence", force_open := false) -> void:
	if book.visible and _tab == tab and not force_open: close_book(); return
	_tab = tab
	book.show()
	_book_signature = ""
	_render_book()

func close_book() -> void:
	book.hide()

func book_open() -> bool:
	return is_instance_valid(book) and book.visible

func book_state() -> Dictionary:
	var public_preview: Dictionary = _preview.duplicate(true) if _tab == "deduction" else {}
	public_preview.erase("rules")
	return {"open": book_open(), "tab": _tab, "deduction_action": _selected_deduction, "selected_evidence": _selected_evidence.keys(), "deduction_preview": public_preview, "intent": _paths() if _tab == "intent" else {}, "journal_day": _journal_day}

func _heading(page: VBoxContainer, title: String, subtitle := "") -> void:
	page.add_child(_label(title, 25))
	if not subtitle.is_empty(): page.add_child(_label(subtitle, 16, Color("76613f")))
	page.add_child(HSeparator.new())

func _card(page: VBoxContainer, title: String, text: String) -> void:
	var frame := PanelContainer.new()
	frame.add_theme_stylebox_override("panel", _style(Color("f0e3c5"), Color("bda373"), 12, 1))
	page.add_child(frame)
	var content := VBoxContainer.new()
	content.add_theme_constant_override("separation", 7)
	frame.add_child(content)
	content.add_child(_label(title, 18))
	if not text.is_empty(): content.add_child(_label(text, 16, Color("5e503b")))

func _render_book() -> void:
	_clear(left_page); _clear(right_page)
	match _tab:
		"deduction": _render_deduction()
		"incantation": _render_mechanic("incantation")
		"opinion": _render_mechanic("opinion")
		"intent": _render_intent()
		"map": _render_map()
		"journal": _render_journal()
		_: _render_evidence()

func _render_evidence() -> void:
	_heading(left_page, "见闻与陈述", "记下来源与获知时间，保留彼此尚未解释的矛盾。")
	_heading(right_page, "推演与待核问题", "结论保留适用范围；新的证据可以改变判断。")
	var observation_count := 0
	var inference_count := 0
	for item: Dictionary in host.state.get("knowledge", []):
		var key: Dictionary = item.get("key", {})
		var fact: Dictionary = item.get("fact", {})
		var object_id := str(key.get("Object", ""))
		var facet := str(key.get("Facet", ""))
		var field := str(key.get("Field", ""))
		if object_id.begins_with("event:") or field in ["room", "spot", "asleep", "arrived"]: continue
		if not bool(fact.get("Known", false)) and not bool(fact.get("Conflicted", false)): continue
		var value = fact.get("Value")
		if not value is String and facet != "infer": continue
		var reference := object_id + "." + facet + "." + field
		var label := str(host.settings.get("fact_labels", {}).get(reference, host.definition.get("fact_labels", {}).get(reference, "已知线索" if facet != "infer" else "推演结论")))
		var text := _knowledge_text(item) + "\n\n" + _knowledge_source_text(item)
		if facet == "infer":
			inference_count += 1
			_card(right_page, label, text)
		else:
			observation_count += 1
			_card(left_page, label, text)
	if observation_count == 0: left_page.add_child(_label("还没有调查记录。先观察自己的房间与随身物。"))
	if inference_count == 0: right_page.add_child(_label("前往“脑内推演”，将调查所得组合为可以检验的解释。"))
	_render_latest_result(right_page, "deduction")

func _knowledge_sources(item: Dictionary) -> Array:
	var sources: Array = item.get("sources", []).duplicate(true)
	var present: Array[String] = []
	for source: Dictionary in sources: present.append(str(source.get("id", "")))
	for source_id in item.get("source_ids", []):
		if not present.has(str(source_id)):
			sources.append({"id": str(source_id)})
			present.append(str(source_id))
	return sources

func _knowledge_text(item: Dictionary) -> String:
	var fact: Dictionary = item.get("fact", {})
	var value: Variant = fact.get("Value")
	var text := str(value) if value is String else ("当前证据支持" if value == true else ("记录为否" if value == false else str(value)))
	if bool(fact.get("Conflicted", false)):
		return "记录存在冲突，仍需核对。\n当前保留的说法：" + text
	if float(fact.get("Confidence", 1.0)) < 1.0: text += "\n这项记录仍需进一步核对。"
	return text

func _knowledge_source_text(item: Dictionary) -> String:
	var key: Dictionary = item.get("key", {})
	var fact: Dictionary = item.get("fact", {})
	var sources := _knowledge_sources(item)
	var origin := _provenance_caption(sources, str(key.get("Facet", "")), str(key.get("Object", "")))
	var time := float(fact.get("ObservedAt", 0.0))
	if sources.is_empty(): origin = "既有记忆（未附来源）" if time <= 0.0 else "来源未注明"
	return "来源：" + origin + "\n" + ("开局已有记录" if time <= 0.0 else "获知于 " + _log_clock(time)) + (" · %d 条来源记录" % sources.size() if not sources.is_empty() else "")

func _category_actions(category: String) -> Array:
	return host.available_actions().filter(func(action): return str(action.category) == category)

func _render_deduction() -> void:
	var choices := _category_actions("deduction")
	_heading(left_page, "脑内推演", "选定问题，勾选你认为能够支持结论的证据。")
	_heading(right_page, "检验范围", "核对证言来源、痕迹顺序与空间关系。")
	if choices.is_empty():
		left_page.add_child(_label("目前还没有可开展的推演。新的线索会解锁相应问题。"))
		_render_latest_result(right_page, "deduction")
		return
	if not choices.any(func(action): return str(action.id) == _selected_deduction):
		_selected_deduction = str(choices[0].id); _selected_evidence.clear()
	for choice: Dictionary in choices:
		var id := str(choice.id)
		left_page.add_child(_button(("› " if id == _selected_deduction else "") + str(choice.label), func(): _selected_deduction = id; _selected_evidence.clear(); _render_book(), true))
	_preview = host.deduction_preview(_selected_deduction)
	if _preview.has("error"):
		right_page.add_child(_label(str(_preview.error)))
		return
	left_page.add_child(HSeparator.new())
	var known_ids: Array[String] = []
	for evidence: Dictionary in _preview.get("evidence", []):
		if bool(evidence.get("known", false)): known_ids.append(str(evidence.id))
	for selected in _selected_evidence.keys():
		if not known_ids.has(str(selected)): _selected_evidence.erase(selected)
	var known_count := 0
	for evidence: Dictionary in _preview.get("evidence", []):
		if not bool(evidence.get("known", false)): continue
		known_count += 1
		var id := str(evidence.id)
		var check := CheckBox.new()
		var evidence_text := _evidence_label(id)
		var descriptions: Array[String] = []
		for value: Dictionary in evidence.get("values", []):
			if value.get("value") is String: descriptions.append(str(value.value))
		if not descriptions.is_empty(): evidence_text = "\n".join(descriptions)
		check.text = evidence_text
		check.add_theme_color_override("font_color", INK)
		check.add_theme_color_override("font_hover_color", INK)
		check.add_theme_color_override("font_pressed_color", INK)
		check.add_theme_font_size_override("font_size", 17)
		check.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		check.button_pressed = bool(_selected_evidence.get(id, false))
		check.toggled.connect(func(value):
			if value: _selected_evidence[id] = true
			else: _selected_evidence.erase(id))
		left_page.add_child(check)
		var fields: Array = evidence.get("fields", [])
		if descriptions.is_empty() and not fields.is_empty(): left_page.add_child(_label(_field_descriptions(fields), 15, Color("76613f")))
		var sources: Array = evidence.get("source_ids", [])
		left_page.add_child(_label("已取得 · " + _deduction_evidence_origin(evidence) + (" · %d 条来源" % sources.size() if not sources.is_empty() else ""), 14, Color("76613f")))
	if known_count == 0: left_page.add_child(_label("尚未得到可用于这项推演的证据。"))
	# Authored solver rules include the exact successful evidence combination.
	# The player chooses from acquired observations; only completed reasoning
	# may reveal its actual support or refutation through the result below.
	if _preview.has("conclusion"): _card(right_page, "待检验的结论", _conclusion_label())
	if _preview.has("qualification"): right_page.add_child(_label(str(_preview.qualification), 16, Color("76613f")))
	var submit := _button("提交选中的证据并推演", func():
		var ids: Array[String] = []
		for id in _selected_evidence: ids.append(str(id))
		var error: String = host.request_action(_selected_deduction, ids, true)
		if not error.is_empty(): host.feedback = error
		refresh(true), true)
	submit.disabled = host.controller_ai
	right_page.add_child(submit)
	right_page.add_child(_label("证据不足或推演失败会留下实际反馈，可以补充调查后重新尝试。", 15, Color("76613f")))
	_render_latest_result(right_page, "deduction")

func _deduction_evidence_origin(evidence: Dictionary) -> String:
	var refs: Array = evidence.get("source_ids", [])
	var sources: Array = []
	var subject := ""
	var facet := ""
	for value: Dictionary in evidence.get("values", []):
		var key: Dictionary = value.get("key", {})
		if subject.is_empty(): subject = str(key.get("Object", ""))
		if facet.is_empty(): facet = str(key.get("Facet", ""))
	for item: Dictionary in host.state.get("knowledge", []):
		for source: Dictionary in _knowledge_sources(item):
			if refs.has(str(source.get("id", ""))): sources.append(source)
	if sources.is_empty():
		for source_id in refs: sources.append({"id": str(source_id)})
	return _provenance_caption(sources, facet, subject) if not sources.is_empty() else "既有记录，未附来源"

func _evidence_label(id: String) -> String:
	for evidence: Dictionary in _preview.get("evidence", []):
		if str(evidence.get("id", "")) != id: continue
		for value: Dictionary in evidence.get("values", []):
			if value.get("value") is String: return str(value.value)
			var key: Dictionary = value.get("key", {})
			var path := str(key.get("Object", "")) + "." + str(key.get("Facet", "")) + "." + str(key.get("Field", ""))
			var fact_label := str(host.settings.get("fact_labels", {}).get(path, ""))
			if not fact_label.is_empty(): return fact_label + "：" + (("是" if value.value else "否") if value.get("value") is bool else str(value.get("value")))
		var label := str(evidence.get("label", "已知证据"))
		return "已确认的现场条件" if label.contains(".") else label
	for rule: Dictionary in _preview.get("rules", []):
		if str(rule.get("conclusion", "")) == id: return str(rule.get("label", "中间结论"))
	return "已知证据"

func _conclusion_label(action_id := "") -> String:
	var action: Dictionary = host._actions.get(_selected_deduction if action_id.is_empty() else action_id, {})
	var reasoning: Dictionary = action.get("deduction", {})
	var label := str(reasoning.get("conclusion_label", action.get("reason", "")))
	return label if not label.is_empty() else str(reasoning.get("title", _preview.get("title", "当前解释获得支持")))

func _field_descriptions(fields: Array) -> String:
	var known: Dictionary = host._knowledge_index()
	var lines: Array[String] = []
	for field in fields:
		var path := str(field)
		if field is Dictionary: path = str(field.get("object", field.get("Object", ""))) + "." + str(field.get("facet", field.get("Facet", ""))) + "." + str(field.get("field", field.get("Field", "")))
		if known.get(path) is String: lines.append(str(known[path]))
	return "\n".join(lines)

func _render_mechanic(category: String) -> void:
	var is_spell := category == "incantation"
	_heading(left_page, "言灵" if is_spell else "意见与协作", "理解规则后选择明确的语句。" if is_spell else "对方会独立评估证据、风险与自身意图。")
	_heading(right_page, "约束与结果" if is_spell else "对方的判断", "语句会带来代价，并在限定时间内生效。" if is_spell else "证据、信任、职责与风险都会影响对方是否配合。")
	var choices := _category_actions(category)
	if choices.is_empty(): left_page.add_child(_label("当前没有可在这里再次施加的言灵。" if is_spell else "当前没有可以提出的协作请求。"))
	for action: Dictionary in choices:
		var id := str(action.id)
		var declared: Dictionary = host._actions.get(id, {})
		var mechanic: Dictionary = declared.get("incantation", declared.get("utterance", {})) if is_spell else declared.get("suggestion", declared.get("opinion", declared.get("persuasion", {})))
		_card(left_page, str(action.label), str(mechanic.get("formula", mechanic.get("proposal", action.get("reason", "")))))
		if is_spell and mechanic.has("cost"): left_page.add_child(_label("代价：" + str(mechanic.cost), 16, Color("8b422c")))
		var button := _button("施加这句言灵" if is_spell else "提出这一建议", func():
			var error: String = host.request_action(id)
			if not error.is_empty(): host.feedback = error
			refresh(true), true)
		button.disabled = host.controller_ai
		left_page.add_child(button)
	_render_latest_result(right_page, category)
	if not is_spell:
		for actor in host._bodies:
			if actor == str(host.definition.viewer): continue
			if host.has_method("_actor_visible") and not host._actor_visible(actor): continue
			_card(right_page, str(host._actors[actor].get("name", actor)), "眼下正在：" + str(host.visible_activity_label(actor)))

func _render_latest_result(page: VBoxContainer, kind: String) -> void:
	var history: Array = host.state.get("mechanic_history", [])
	var count := 0
	for index in range(history.size() - 1, -1, -1):
		var record: Dictionary = history[index]
		if str(record.get("kind", "")) != ("suggestion" if kind == "opinion" else kind) or str(record.get("actor", "")) != str(host.definition.viewer): continue
		_card(page, "最近一次结果 · " + ("成立" if bool(record.get("success", false)) else "未成立"), _result_text(record.get("details", {})))
		count += 1
		if count >= 3: break
	if count == 0:
		var current = host.state.get(kind)
		if current is Dictionary and str(current.get("actor", "")) == str(host.definition.viewer): _card(page, "最近一次结果", _result_text(current))
		else: page.add_child(_label("这里会保留实际执行结果与依据。", 16, Color("76613f")))

func _result_text(value: Variant) -> String:
	if not value is Dictionary: return str(value)
	var result: Array[String] = []
	var names := {"status": "结论", "conclusion": "推得命题", "qualification": "适用范围", "formula": "言灵", "score": "采纳评分", "threshold": "所需评分", "accepted": "是否接受", "cost": "付出", "resource_remaining": "言灵余量", "backlash": "反噬", "rule": "生效规则", "expires": "失效时刻", "reason": "原因", "missing": "缺少证据", "title": "问题"}
	for key in names:
		if not value.has(key): continue
		var detail = value[key]
		if key == "accepted": detail = "接受" if bool(detail) else "尚未接受"
		elif key == "conclusion": detail = _conclusion_label(str(value.get("action", "")))
		elif key == "status": detail = {"proved": "当前证据支持这项结论", "unknown": "证据仍不足", "undetermined": "证据仍不足", "refuted": "现有证据反驳这项结论", "both": "证据相互冲突"}.get(str(detail), str(detail))
		elif key == "reason": detail = {"evidence_missing": "仍有必要证据尚未出示", "independent_opinion_accepted": "对方独立评估后愿意配合", "independent_opinion_rejected": "现有证据与理由尚不足以克服对方的顾虑"}.get(str(detail), str(detail))
		elif key == "backlash" and detail is Dictionary: detail = "身体负担 +" + str(float(detail.get("amount", 0))) + "，累计 " + str(float(detail.get("total", 0)))
		elif key == "rule" and detail is Dictionary: detail = "已施加语句中明确的临时约束"
		elif key == "expires":
			var until := int(float(detail))
			detail = "第%d日 %02d:%02d" % [until / 1440 + 1, (until / 60) % 24, until % 60]
		elif key == "missing" and detail is Array:
			if detail.is_empty(): continue
			detail = "%d 项必要信息仍未确认" % detail.size()
		result.append(str(names[key]) + "：" + str(detail))
	if value.has("contributions"):
		var labels := {"trust": "对你的信任", "evidence": "已出示证据", "cost": "行动代价", "risk": "风险顾虑", "value:duty": "职责", "value:care": "照护", "value:fairness": "公平", "value:order": "秩序", "value:truth": "求真", "value:freedom": "自主", "value:survival": "生存"}
		if value.contributions is Dictionary:
			for key in value.contributions: result.append(str(labels.get(key, "情境影响" if str(key).begins_with("context:") else key)) + "：%+.2f" % float(value.contributions[key]))
		for reason in value.get("context", []): result.append(str(reason))
	if value.get("support") is Dictionary:
		var support: Dictionary = value.support
		var evidence_labels: Array[String] = []
		for evidence: Dictionary in value.get("evidence", []):
			var description := _field_descriptions(evidence.get("fields", []))
			evidence_labels.append(description if not description.is_empty() else "已确认的现场条件")
		result.append("选用证据：" + "、".join(evidence_labels))
		result.append("证明已核验 · %d 条证据 · %d 条规则" % [support.get("Axioms", []).size(), support.get("Rules", []).size()])
	if value.has("proof"):
		var proof = value.proof
		if proof is Dictionary:
			for key in ["conclusion", "evidence", "used_evidence", "steps", "status"]:
				if proof.has(key): result.append("证明 " + key + "：" + str(proof[key]))
		else: result.append("证明：" + str(proof))
	if result.is_empty(): result.append(JSON.stringify(value, "  "))
	return "\n".join(result)

func _render_map() -> void:
	_heading(left_page, "走过的空间", "从实际建筑拓扑读取房间身份；未探索的房间不会展开。")
	_heading(right_page, "眼下的动线", "跨房间移动必须通过当前可用的正式行动。")
	for id in host.visited:
		var spec: Dictionary = host.settings.rooms.get(id, {})
		_card(left_page, host.room_label(id) + (" · 当前位置" if id == host.room_id else ""), str(spec.get("floor", "")) + "\n" + str(spec.get("topology_note", "")))
	for action: Dictionary in host.available_actions():
		if str(action.kind) != "walk": continue
		var id := str(action.id)
		var travel := _button(str(action.label), func():
			var error: String = host.request_action(id)
			if not error.is_empty(): host.feedback = error
			refresh(true), true)
		right_page.add_child(travel)
	for clue: Dictionary in host.state.get("knowledge", []):
		var fact: Dictionary = clue.get("fact", {})
		var key: Dictionary = clue.get("key", {})
		if str(key.get("Field", "")) in ["exit", "paths", "route", "lead_window", "vent_grate"] and fact.get("Value") is String:
			_card(right_page, "已知通路", str(fact.Value))

func _render_journal() -> void:
	_heading(left_page, "调查与见闻", "按实际发生或获知的时间记录；缺失记录不表示事情未发生。")
	_heading(right_page, "推演与协商", "失败、代价与规则到期均保留实际回执。")
	var rows := _readable_journal_rows()
	var days: Array[int] = []
	for row: Dictionary in rows:
		var day := int(float(row.time) / 1440.0) + 1
		if not days.has(day): days.append(day)
	days.sort()
	var filter_bar := HBoxContainer.new()
	filter_bar.add_theme_constant_override("separation", 5)
	left_page.add_child(filter_bar)
	filter_bar.add_child(_button("全部", func(): _journal_day = 0; _render_book(), true))
	for day in days:
		var selected_day := day
		filter_bar.add_child(_button("第%d日" % day, func(): _journal_day = selected_day; _render_book(), true))
	var dropped_value: Variant = host.get("journal_dropped_total")
	var dropped := int(dropped_value) if dropped_value != null else 0
	var coverage := "保存的公开行动回执 %d 条；机制记录 %d 条。" % [host.journal.size(), host.state.get("mechanic_history", []).size()]
	if not host.journal.is_empty(): coverage += "\n行动覆盖 " + _log_clock(float(host.journal.front().get("time", 0.0))) + " 至 " + _log_clock(float(host.journal.back().get("time", 0.0))) + "。"
	if dropped > 0: coverage += "\n更早的 %d 条已移出此页，不补写为新的事件。" % dropped
	left_page.add_child(_label(coverage, 14, Color("76613f")))
	var shown_left := 0
	var shown_right := 0
	for row: Dictionary in rows:
		if _journal_day > 0 and int(float(row.time) / 1440.0) + 1 != _journal_day: continue
		var mechanism := str(row.kind) in ["deduction", "opinion", "incantation", "world_event"]
		_journal_card(right_page if mechanism else left_page, row)
		if mechanism: shown_right += 1
		else: shown_left += 1
	if shown_left == 0: left_page.add_child(_label("这个日期范围内没有保存的公开行动或证据记录。"))
	if shown_right == 0: right_page.add_child(_label("这个日期范围内没有已记录的推演或协商结果。"))
	right_page.add_child(_label("本页显示 %d 项见闻与 %d 项机制结果；生活记录只在同一天内归并，原始条数与来源可展开查看。" % [shown_left, shown_right], 14, Color("76613f")))

func _log_clock(value: float) -> String:
	var minutes := int(value)
	return "第%d日 %02d:%02d" % [minutes / 1440 + 1, (minutes / 60) % 24, minutes % 60]

func _readable_journal_rows() -> Array:
	var history: Array = host.state.get("mechanic_history", [])
	var last: Dictionary = host.journal.back() if not host.journal.is_empty() else {}
	var known: Array = host.state.get("knowledge", [])
	var signature := str(host.journal.size()) + "|" + str(last.get("time", "")) + "|" + str(history.size()) + "|" + str(known.hash()) + "|" + str(host.get("journal_dropped_total"))
	if signature == _journal_cache_signature: return _journal_cache
	_journal_cache_signature = signature
	var rows: Array = []
	var mechanic_executions: Dictionary = {}
	var narrated_sources: Dictionary = {}
	var viewer := str(host.definition.viewer)
	var action_receipts: Dictionary = {}
	for receipt: Dictionary in host.journal:
		for source_id in receipt.get("narrative_source_ids", []): narrated_sources[str(source_id)] = true
		var receipt_actor := str(receipt.get("actor", ""))
		var receipt_execution := str(receipt.get("execution", ""))
		if receipt_actor == viewer and not receipt_execution.is_empty(): action_receipts[receipt_actor + "/" + receipt_execution] = receipt
	for event: Dictionary in history:
		var actor := str(event.get("actor", ""))
		var kind := str(event.get("kind", ""))
		if actor != viewer and kind != "world_event": continue
		var action := str(event.get("action", ""))
		var execution := str(event.get("execution", ""))
		if not execution.is_empty() and kind != "incantation_expired": mechanic_executions[actor + "/" + execution] = true
		var details: Dictionary = event.get("details", {})
		var label := str(host._actions.get(action, {}).get("label", details.get("label", "现场变化")))
		if kind == "incantation_expired": label = "言灵规则到期"
		var normalized_kind := "opinion" if kind == "suggestion" else ("incantation" if kind == "incantation_expired" else ("action" if kind == "strategy" else kind))
		var source_refs: Array = []
		if not execution.is_empty(): source_refs.append("execution/" + execution)
		for evidence: Dictionary in details.get("evidence", []):
			for source in evidence.get("source_ids", []):
				if not source_refs.has(source): source_refs.append(source)
		var original_action: Dictionary = action_receipts.get(actor + "/" + execution, {}) if kind != "incantation_expired" else {}
		for source_id in original_action.get("narrative_source_ids", []):
			if not source_refs.has(source_id): source_refs.append(source_id)
		var outcome := _mechanic_summary(event)
		if original_action.get("intent") is Dictionary: outcome += "\n这次行动的路径：" + _intent_caption(original_action.intent)
		var completion := str(original_action.get("completion_text", ""))
		if bool(original_action.get("committed", false)) and not completion.is_empty(): outcome += "\n" + completion
		var originals: Dictionary = {"mechanic": event.duplicate(true)}
		if not original_action.is_empty(): originals["action_receipt"] = original_action.duplicate(true)
		rows.append({"time": float(event.get("time", 0.0)), "actor_name": str(host._actors.get(actor, {}).get("name", "现场")), "kind": normalized_kind, "label": label, "outcome": outcome, "source_count": 1 if original_action.is_empty() else 2, "source_refs": source_refs, "details": originals, "entry_id": actor + "/" + execution + "/" + kind, "first_time": float(event.get("time", 0.0))})
	var routine_groups: Dictionary = {}
	for index in range(host.journal.size()):
		var event: Dictionary = host.journal[index]
		var actor := str(event.get("actor", ""))
		var own := actor == viewer
		var execution := str(event.get("execution", ""))
		if own and not execution.is_empty() and mechanic_executions.has(actor + "/" + execution): continue
		var kind := str(event.get("kind", "interact"))
		if not own and kind in ["deduce", "strategy"]: continue
		var label := str(event.get("label", "现场行动"))
		var succeeded := bool(event.get("committed", false))
		var outcome := "已完成" if succeeded else "未完成"
		if not succeeded and own:
			var interruption_label := str(event.get("interruption_label", ""))
			outcome = "现场机会变化，行动未完成：" + interruption_label if not interruption_label.is_empty() else outcome + "：" + host.failure_label(str(event.get("reason", "")))
		if own and event.get("intent") is Dictionary: outcome += "\n这次行动的路径：" + _intent_caption(event.intent)
		var completion := str(event.get("completion_text", ""))
		if succeeded and not completion.is_empty(): outcome += "\n" + completion
		var time := float(event.get("time", 0.0))
		var source := "execution/" + execution if not execution.is_empty() else "saved-journal/" + str(index)
		var routine := kind in ["rest", "sleep", "eat", "drink", "wash"] or label in ["歇息", "饮水", "洗漱", "吃晚饭", "上床入睡"]
		var group_key := str(int(time / 1440.0)) + "/" + actor + "/" + label + "/" + outcome
		if routine and routine_groups.has(group_key):
			var existing: Dictionary = routine_groups[group_key]
			existing.time = maxf(float(existing.time), time)
			existing.first_time = minf(float(existing.first_time), time)
			existing.source_count = int(existing.source_count) + 1
			existing["occurrences"] = int(existing.get("occurrences", 1)) + 1
			existing.source_refs.append(source)
			existing.details.append(event.duplicate(true))
			continue
		var source_refs: Array = [source]
		for narrative_source in event.get("narrative_source_ids", []):
			if not source_refs.has(narrative_source): source_refs.append(narrative_source)
		var record := {"time": time, "actor_name": str(event.get("actor_name", host._actors.get(actor, {}).get("name", "现场人物"))), "kind": "routine" if routine else "action", "label": label, "outcome": outcome, "source_count": 1, "source_refs": source_refs, "details": [event.duplicate(true)], "entry_id": source + "/" + actor, "first_time": time}
		rows.append(record)
		if routine: routine_groups[group_key] = record
	rows.append_array(_known_evidence_rows(narrated_sources))
	rows.sort_custom(func(a: Dictionary, b: Dictionary): return float(a.time) > float(b.time))
	_journal_cache = rows
	return rows

func _known_evidence_rows(narrated_sources: Dictionary = {}) -> Array:
	var rows: Array = []
	for item: Dictionary in host.state.get("knowledge", []):
		var source_refs: Array = item.get("source_ids", []).duplicate()
		var sources := _knowledge_sources(item)
		for source: Dictionary in sources:
			var id := str(source.get("id", ""))
			if not id.is_empty() and not source_refs.has(id): source_refs.append(id)
		# Older snapshots do not expose provenance; do not invent it from a value.
		if source_refs.is_empty(): continue
		var already_narrated := true
		for source_id in source_refs:
			if not narrated_sources.has(str(source_id)): already_narrated = false; break
		if already_narrated: continue
		var key: Dictionary = item.get("key", {})
		var fact: Dictionary = item.get("fact", {})
		if not bool(fact.get("Known", false)) and not bool(fact.get("Conflicted", false)): continue
		var object := str(key.get("Object", ""))
		var field := str(key.get("Field", ""))
		var facet := str(key.get("Facet", ""))
		if object.begins_with("event:") or field in ["room", "spot", "asleep", "arrived"]: continue
		var value: Variant = fact.get("Value")
		if not value is String and facet != "infer": continue
		var path := object + "." + facet + "." + field
		var label := str(host.settings.get("fact_labels", {}).get(path, host.definition.get("fact_labels", {}).get(path, "获得线索")))
		var provenance := _provenance_caption(sources, facet, object)
		var outcome := _knowledge_text(item)
		rows.append({"time": float(fact.get("ObservedAt", 0.0)), "actor_name": str(host._actors.get(str(host.definition.viewer), {}).get("name", "侦探")), "kind": "evidence", "label": provenance + " · " + label, "outcome": outcome, "source_count": 1, "source_refs": source_refs, "details": item.duplicate(true), "entry_id": "evidence/" + path, "first_time": float(fact.get("ObservedAt", 0.0))})
	return rows

func _provenance_caption(sources: Array, facet: String, subject := "") -> String:
	var captions: Array[String] = []
	for source: Dictionary in sources:
		var kind := str(source.get("method", source.get("kind", ""))).to_lower()
		var source_id := str(source.get("id", ""))
		var caption := ""
		var speaker := str(source.get("speaker", ""))
		var speaker_name := str(source.get("speaker_label", host._actors.get(speaker, {}).get("name", "")))
		# Method=observation also covers delivered reports; preserve the real
		# receipt origin instead of labelling every remembered fact firsthand.
		if source_id.begins_with("utterance/") or kind == "speech":
			caption = "亲闻发言" + ("（" + speaker_name + "）" if not speaker_name.is_empty() else "") + "，内容另行核对"
		elif source_id.begins_with("handoff/") or source_id.begins_with("report/") or source_id.begins_with("dialogue/") or kind in ["hearsay", "handoff", "tell", "communication"]:
			if not speaker.is_empty() and speaker == subject:
				caption = speaker_name + "本人自述" if not speaker_name.is_empty() else "本人自述"
			else:
				caption = "他人转述" + ("（" + speaker_name + "）" if not speaker_name.is_empty() else "")
			if facet == "infer": caption += "的推断"
		elif str(source.get("channel", "")).to_lower() == "self" or kind in ["self", "self_perception"]: caption = "自身感受与随身检查"
		elif source_id.begins_with("proof/") or kind in ["deduction", "proof", "inference"]: caption = "推演认知"
		elif source_id.begins_with("action/") or source_id.begins_with("perception/") or kind in ["inspection", "perception", "action", "deliberateobservation"]: caption = "亲见或亲闻"
		if not caption.is_empty() and not captions.has(caption): captions.append(caption)
	return "、".join(captions) if not captions.is_empty() else ("已有推断，来源未注明" if facet == "infer" else "已有记录，来源未注明")

func _mechanic_summary(event: Dictionary) -> String:
	var kind := str(event.get("kind", ""))
	var details: Dictionary = event.get("details", {})
	var success := bool(event.get("success", false))
	if kind == "deduction":
		if success: return "证明成立，认知已经更新。\n" + str(details.get("qualification", ""))
		return "所选证据不足，尚未形成结论。可以补充调查后重试。" if str(details.get("status", "")) == "unknown" else "当前推演未获支持；原始证据与结果已保留。"
	if kind == "suggestion":
		var target := str(details.get("target", ""))
		var name := str(host._actors.get(target, {}).get("name", "对方"))
		return name + ("接受了建议。" if bool(details.get("accepted", false)) else "仍有顾虑，建议未获采纳。")
	if kind == "incantation_expired": return "临时规则已自然到期；已完成的其他工作保留各自结果。"
	if kind == "incantation":
		if not success: return "言灵没有生效，原因保留在记录详情中。"
		var line := "「" + str(details.get("formula", "言灵")) + "」已生效。"
		if details.has("cost"): line += "\n意志消耗 " + str(details.cost)
		if details.get("backlash") is Dictionary:
			line += " · 身体负担 +%s（累计 %s）" % [str(details.backlash.get("amount", 0)), str(details.backlash.get("total", 0))]
		elif details.has("backlash"): line += " · 身体负担 +" + str(details.backlash)
		if details.has("expires"): line += "\n有效至 " + _log_clock(float(details.expires)) + "。"
		return line
	if kind == "strategy": return "已按当前认知确定行动方案。" if success else "尚未形成可行方案。"
	return str(details.get("label", details.get("message", "现场变化已被记录。")))

func _journal_card(page: VBoxContainer, entry: Dictionary) -> void:
	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", _style(Color("e3cfa6"), Color("b79b68"), 12, 1))
	page.add_child(panel)
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 8)
	panel.add_child(column)
	var count := int(entry.get("source_count", 1))
	var occurrences := int(entry.get("occurrences", 1))
	var time_text := _log_clock(float(entry.time))
	if float(entry.get("first_time", entry.time)) < float(entry.time): time_text = _log_clock(float(entry.first_time)) + " 至 " + time_text
	column.add_child(_label(time_text + " · " + str(entry.actor_name), 14, Color("76613f")))
	column.add_child(_label(str(entry.label) + (" · %d次" % occurrences if occurrences > 1 else ""), 19))
	column.add_child(_label(str(entry.outcome), 17))
	column.add_child(_label("对应 %d 条原记录" % count, 14, Color("76613f")))
	var detail_box := VBoxContainer.new()
	var id := str(entry.entry_id)
	detail_box.visible = bool(_journal_expanded.get(id, false))
	var toggle: Button = _button("收起来源与原记录" if detail_box.visible else "展开来源与原记录", func():
		detail_box.visible = not detail_box.visible
		_journal_expanded[id] = detail_box.visible, true)
	toggle.pressed.connect(func(): toggle.text = "收起来源与原记录" if detail_box.visible else "展开来源与原记录")
	column.add_child(toggle)
	column.add_child(detail_box)
	var refs: Array = entry.get("source_refs", [])
	if not refs.is_empty(): detail_box.add_child(_label("来源：" + ", ".join(refs), 14, Color("5a4c3b")))
	detail_box.add_child(_label(JSON.stringify(entry.get("details", {}), "  "), 14, Color("5a4c3b")))
