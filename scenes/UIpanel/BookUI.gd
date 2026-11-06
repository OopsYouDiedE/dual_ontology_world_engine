@tool
extends Control
## A physical, curved book with native text controls mapped through its exact surface.
signal ui_event(event: Dictionary)
signal opened
signal closed
signal page_switched(page_id: String)

const StageScene := preload("res://scenes/components/world/LowpolyBookStage3D.tscn")
const Materials := preload("res://scenes/components/shared/MaterialControls.gd")
const Navigation := preload("res://scenes/UIpanel/UIInput.gd")
const TEXTURE_SIZE := Vector2i(1600, 1040)
const PAGE_NAMES := ["档案", "规则", "日程", "契约", "笔记", "图版", "存档", "设置"]
const PAGE_PATHS := ["EvidenceArchiveUI", "RulebookUI", "EventScheduleUI", "ConventionUI", "HypothesisNoteUI", "GalleryPlateUI", "BookArchiveUI", "BookSettingsSpread"]
const TAB_INKS := ["eadcc5", "eee0c5", "30291e", "e8dfcf", "30291e", "30252c", "eee5cf", "edddce"]
const CAPABILITIES := ["graphics.ui_scale", "audio.page_turn", "controls.wheel_mode", "controls.hold_threshold", "gameplay.autosave_policy"]

var desktop := false
var archive: RefCounted
var snapshot: Dictionary = {}
var stage: Node3D
var stage_viewport: SubViewport
var page_viewport: SubViewport
var page_root: Control
var display: TextureRect
var _matte: ColorRect
var _page_container: Control
var _navigation = Navigation.new()
var _tabs: Array[Button] = []
var _stamps: Array[Button] = []
var _pages: Dictionary = {}
var _page_id := "B01"
var _current_page: Control
var _pinned: Dictionary = {}
var _is_open := false
var _turning := false
var _close_queued := false
var _blocked := false
var _tween: Tween
var _confirm: Control
var _confirm_title: Label
var _confirm_action: Dictionary = {}
var _confirm_accept: Button
var _confirm_cancel: Button
var _notice: Label
var _notice_time := 0.0
var _previous_uv := Vector2.ZERO
var _paper_player: AudioStreamPlayer
var _gallery: Control
var _frame := Rect2()

func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	process_mode = Node.PROCESS_MODE_ALWAYS
	_build_book()
	_build_chrome()
	_build_confirmation()
	_build_paper_sound()
	resized.connect(_resize)
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null: settings.setting_changed.connect(_on_setting_changed)
	_resize()
	_resize.call_deferred()
	if not desktop: hide()
	if get_parent() == get_tree().root:
		open_section("B01")

func get_safe_frame() -> Rect2:
	var available := size
	if available.x < 2.0 or available.y < 2.0:
		available = get_viewport().get_visible_rect().size
	var width := minf(available.x, available.y * 2.0)
	var dimensions := Vector2(width, width * 0.5)
	return Rect2((available - dimensions) * 0.5, dimensions)

func _build_book() -> void:
	_matte = ColorRect.new()
	_matte.name = "CompositionMatte"
	_matte.color = Color("101318")
	_matte.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_matte)
	_matte.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_matte.visible = desktop
	stage_viewport = SubViewport.new()
	stage_viewport.name = "BookStageViewport"
	stage_viewport.own_world_3d = true
	stage_viewport.transparent_bg = not desktop
	stage_viewport.handle_input_locally = false
	stage_viewport.msaa_3d = Viewport.MSAA_4X
	stage_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	stage_viewport.size = Vector2i(1280, 640)
	add_child(stage_viewport)
	stage = StageScene.instantiate()
	stage_viewport.add_child(stage)
	stage.set_context(desktop)
	display = TextureRect.new()
	display.name = "BookProjection"
	display.texture = stage_viewport.get_texture()
	display.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	display.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(display)
	page_viewport = SubViewport.new()
	page_viewport.name = "PaperViewport"
	page_viewport.size = TEXTURE_SIZE
	page_viewport.disable_3d = true
	page_viewport.transparent_bg = true
	page_viewport.gui_embed_subwindows = true
	page_viewport.handle_input_locally = true
	page_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	page_viewport.canvas_item_default_texture_filter = Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_LINEAR
	page_viewport.set_meta("physical_book_view", self)
	add_child(page_viewport)
	page_root = Control.new()
	page_root.name = "PaperControls"
	page_root.size = Vector2(TEXTURE_SIZE)
	page_root.mouse_filter = Control.MOUSE_FILTER_PASS
	page_viewport.add_child(page_root)
	Materials.apply(page_root)
	_page_container = Control.new()
	_page_container.name = "Pages"
	page_root.add_child(_page_container)
	_page_container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	stage.set_page_texture(page_viewport.get_texture())

func _build_chrome() -> void:
	for index in range(PAGE_NAMES.size()):
		var button := _ink_button(PAGE_NAMES[index], Color(TAB_INKS[index]))
		button.name = "Bookmark" + str(index + 1)
		button.tooltip_text = ["证据与档案", "知识与规则", "事件与日程", "契约与公约", "猜想与笔记", "图版", "存取读档", "设置"][index]
		button.pressed.connect(open_section.bind("B%02d" % (index + 1)))
		button.disabled = desktop and index < 6
		button.hide()
		add_child(button)
		_tabs.append(button)
	for index in range(2):
		var button := _ink_button("保存" if index == 0 else "合上", Color("eddec9"))
		button.name = "SaveStamp" if index == 0 else "CloseStamp"
		button.tooltip_text = "进入游戏后保存" if index == 0 and desktop else button.text
		button.disabled = index == 0 and desktop
		if index == 0:
			button.pressed.connect(func(): _on_page_event({"kind": "bookmark_save"}))
		else:
			button.pressed.connect(close_book)
		button.hide()
		add_child(button)
		_stamps.append(button)
	_notice = Label.new()
	_notice.name = "BookmarkMessage"
	_notice.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_notice.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_notice.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_notice.add_theme_color_override("font_color", Color("efe2ca"))
	_notice.add_theme_color_override("font_shadow_color", Color("161b1e"))
	_notice.add_theme_constant_override("shadow_offset_y", 2)
	_notice.hide()
	add_child(_notice)

func _ink_button(caption: String, ink: Color) -> Button:
	var button := Button.new()
	button.text = caption
	button.flat = true
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	button.focus_mode = Control.FOCUS_ALL
	for state in ["normal", "hover", "pressed", "disabled"]:
		button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	var focus := StyleBoxFlat.new()
	focus.bg_color = Color.TRANSPARENT
	focus.border_color = Color("e5d1ac")
	focus.border_width_bottom = 1
	button.add_theme_stylebox_override("focus", focus)
	button.add_theme_color_override("font_color", ink)
	button.add_theme_color_override("font_hover_color", ink.lightened(0.2))
	button.add_theme_color_override("font_focus_color", ink.lightened(0.2))
	button.add_theme_color_override("font_pressed_color", Color("fff5de"))
	button.add_theme_color_override("font_disabled_color", Color(ink, 0.68))
	if page_root.theme != null and page_root.theme.default_font != null:
		button.add_theme_font_override("font", page_root.theme.default_font)
	return button

func _resize() -> void:
	if not is_instance_valid(stage): return
	_frame = get_safe_frame()
	display.position = _frame.position
	display.size = _frame.size
	stage_viewport.size = Vector2i(_frame.size).max(Vector2i(2, 1))
	var ui_scale := 1.0
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null:
		var value: Variant = settings.get_value("graphics", "ui_scale")
		if value != null: ui_scale = float(value)
	stage.resize_view(Vector2(stage_viewport.size), ui_scale)
	_position_controls()

func _position_controls() -> void:
	if not is_instance_valid(stage): return
	var unit := _frame.size.x / 1280.0
	for index in range(_tabs.size()):
		var button := _tabs[index]
		button.size = Vector2(96.0, 44.0) * unit
		button.position = _frame.position + stage.anchor_screen(stage.tab_anchor(index)) - button.size * 0.5
		button.add_theme_font_size_override("font_size", maxi(13, roundi(25.0 * unit)))
		button.visible = _is_open and not _turning
		button.modulate.a = 1.0 if _page_id == "B%02d" % (index + 1) else 0.94
	for index in range(_stamps.size()):
		var button := _stamps[index]
		var side := -1.0 if index == 0 else 1.0
		var anchor := Vector3(side * 5.17, 0.59, 3.44)
		if stage.has_method("stamp_anchor"): anchor = stage.stamp_anchor(index)
		button.size = Vector2(98.0, 44.0) * unit
		button.position = _frame.position + stage.anchor_screen(anchor) - button.size * 0.5
		button.add_theme_font_size_override("font_size", maxi(13, roundi(26.0 * unit)))
		button.visible = _is_open and not _turning
	_notice.position = _frame.position + _frame.size * Vector2(0.20, 0.947)
	_notice.size = _frame.size * Vector2(0.60, 0.048)
	_notice.add_theme_font_size_override("font_size", maxi(13, roundi(22.0 * unit)))

func configure(data: Dictionary, book_data: RefCounted = null) -> void:
	var previous_viewer := str(snapshot.get("viewer", ""))
	if (not previous_viewer.is_empty() and previous_viewer != str(data.get("viewer", ""))) or (archive != null and book_data != null and archive != book_data):
		_pinned.clear()
	snapshot = data
	if book_data != null: archive = book_data
	if is_instance_valid(_current_page) and _current_page.has_method("configure"):
		_current_page.configure(_page_data(), archive)

func _page_data() -> Dictionary:
	var data := snapshot.duplicate()
	data["settings_capabilities"] = CAPABILITIES
	data["title_context"] = desktop
	return data

func _canonical(page: String) -> String:
	if page.begins_with("B") and page.length() == 3:
		return "B%02d" % clampi(int(page.substr(1)), 1, 8)
	if page.begins_with("entry:"): return "B01"
	var aliases := {"index": "B01", "evidence": "B01", "items": "B01", "people": "B01", "archive": "B01", "rules": "B02", "events": "B03", "records": "B03", "schedule": "B03", "convention": "B04", "contracts": "B04", "hypotheses": "B05", "notes": "B05", "gallery": "B06", "load": "B07", "save": "B07", "settings": "B08"}
	return str(aliases.get(page.get_slice("@", 0), "B01"))

func open_section(page: String) -> void:
	if _blocked or _turning: return
	var destination := _canonical(page)
	if desktop and destination not in ["B07", "B08"]: return
	if not _is_open:
		show()
		_is_open = true
		_turning = true
		_set_page(destination, page)
		_new_tween()
		if desktop:
			_tween.tween_method(stage.set_open, 0.0, 1.0, _duration(0.52)).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_IN_OUT)
		else:
			stage.set_open(1.0)
			stage.set_pull(0.0)
			_tween.tween_method(stage.set_pull, 0.0, 1.0, _duration(0.34)).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
		_tween.finished.connect(_finish_open)
	elif destination != _page_id:
		_turning = true
		var old_page := page_viewport.get_texture().get_image()
		if old_page != null and not old_page.is_empty(): stage.begin_turn(ImageTexture.create_from_image(old_page))
		_set_page(destination, page)
		_play_page_sound()
		_new_tween()
		_tween.tween_method(stage.set_turn, 0.0, 1.0, _duration(0.38)).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		_tween.finished.connect(_finish_turn)
	else:
		_set_page(destination, page)
		_focus_page()
	_position_controls()

func _new_tween() -> void:
	if is_instance_valid(_tween): _tween.kill()
	_tween = create_tween()

func _finish_open() -> void:
	_finish_turn()
	opened.emit()

func _finish_turn() -> void:
	_turning = false
	_position_controls()
	_focus_page()
	if _close_queued:
		_close_queued = false
		close_book()

func _set_page(page_id: String, requested: String) -> void:
	if is_instance_valid(_current_page): _current_page.hide()
	_page_id = page_id
	if not _pages.has(page_id):
		var index := clampi(int(page_id.substr(1)) - 1, 0, 7)
		var scene: PackedScene = load("res://scenes/UIpanel/" + PAGE_PATHS[index] + ".tscn")
		var page: Control = scene.instantiate()
		_page_container.add_child(page)
		page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		if page.has_signal("ui_event"): page.ui_event.connect(_on_page_event)
		if page.has_signal("pin_requested"): page.pin_requested.connect(_on_pin)
		_pages[page_id] = page
	_current_page = _pages[page_id]
	_current_page.show()
	if _current_page.has_method("configure"): _current_page.configure(_page_data(), archive)
	if _current_page.has_method("set_pinned"): _current_page.set_pinned(_pinned)
	if requested.begins_with("entry:") and _current_page.has_method("select_entry"):
		_current_page.select_entry(requested.trim_prefix("entry:"))
	if requested == "settings@1" and _current_page.has_method("show_pair"): _current_page.show_pair(1)
	page_switched.emit(page_id)

func close_book() -> void:
	if not _is_open: return
	if _turning:
		_close_queued = true
		return
	if _confirm.visible:
		_confirm.hide()
		return
	_turning = true
	_position_controls()
	_new_tween()
	if desktop:
		_tween.tween_method(stage.set_open, 1.0, 0.0, _duration(0.45)).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_IN_OUT)
	else:
		_tween.tween_method(stage.set_pull, 1.0, 0.0, _duration(0.28)).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_IN)
	_tween.finished.connect(func():
		_is_open = false
		_turning = false
		if not desktop: hide()
		_position_controls()
		closed.emit()
	)

func _duration(seconds: float) -> float:
	var settings := get_node_or_null("/root/GameSettings")
	return 0.02 if settings != null and bool(settings.get_value("accessibility", "reduce_motion")) else seconds

func _on_pin(data: Dictionary) -> void:
	_pinned = data.duplicate(true)
	if is_instance_valid(_current_page) and _current_page.has_method("set_pinned"):
		_current_page.set_pinned(_pinned)

func _on_page_event(event: Dictionary) -> void:
	if _blocked: return
	match str(event.get("kind", "")):
		"confirm":
			_confirm_action = event.get("action", {}).duplicate(true)
			_confirm_title.text = str(event.get("title", "确认？"))
			_confirm_accept.text = str(event.get("confirm_text", "确认"))
			_confirm.show()
			_confirm_cancel.grab_focus()
		"unpin": _on_pin({})
		"open_section": open_section(str(event.get("page", "B01")))
		"book_page":
			open_section(str(event.get("page", "B01")))
			if _current_page.has_method("select_entry") and event.has("entry_id"):
				_current_page.select_entry(str(event.entry_id))
			if _current_page.has_method("open_note") and (event.has("entry_id") or event.has("related")):
				_current_page.open_note(str(event.get("entry_id", "")), event.get("related", []))
		"gallery_fullscreen": _show_gallery(event)
		_: ui_event.emit(event)

func _build_confirmation() -> void:
	_confirm = Control.new()
	_confirm.name = "Confirmation"
	add_child(_confirm)
	_confirm.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var shade := ColorRect.new()
	shade.color = Color(0.06, 0.07, 0.09, 0.62)
	_confirm.add_child(shade)
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var center := CenterContainer.new()
	_confirm.add_child(center)
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var panel := PanelContainer.new()
	panel.custom_minimum_size = Vector2(490, 200)
	var paper := Materials._box(Color("e3cfba"), 40, 30)
	paper.border_color = Color("9b8168")
	paper.set_border_width_all(1)
	paper.shadow_color = Color(0, 0, 0, 0.28)
	paper.shadow_size = 12
	panel.add_theme_stylebox_override("panel", paper)
	center.add_child(panel)
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 28)
	panel.add_child(column)
	Materials.apply(column)
	_confirm_title = Label.new()
	_confirm_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_confirm_title.add_theme_color_override("font_color", Color("29221c"))
	column.add_child(_confirm_title)
	var actions := HBoxContainer.new()
	actions.alignment = BoxContainer.ALIGNMENT_CENTER
	actions.add_theme_constant_override("separation", 40)
	column.add_child(actions)
	_confirm_cancel = Button.new()
	_confirm_cancel.text = "取消"
	_confirm_cancel.custom_minimum_size = Vector2(108, 46)
	_confirm_cancel.pressed.connect(func(): _confirm.hide(); _focus_page())
	actions.add_child(_confirm_cancel)
	_confirm_accept = Button.new()
	_confirm_accept.text = "确认"
	_confirm_accept.custom_minimum_size = Vector2(108, 46)
	_confirm_accept.pressed.connect(func():
		_confirm.hide()
		var action := _confirm_action.duplicate(true)
		_confirm_action.clear()
		ui_event.emit(action)
	)
	actions.add_child(_confirm_accept)
	_confirm.hide()

func _show_gallery(data: Dictionary) -> void:
	var path := str(data.get("image", ""))
	var texture: Texture2D
	if path.begins_with("res://") and ResourceLoader.exists(path): texture = load(path)
	elif FileAccess.file_exists(path):
		var source := Image.load_from_file(path)
		if source != null and not source.is_empty(): texture = ImageTexture.create_from_image(source)
	if texture == null: return
	if is_instance_valid(_gallery): _gallery.queue_free()
	_gallery = Control.new()
	_gallery.name = "GalleryFullscreen"
	add_child(_gallery)
	_gallery.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var shade := ColorRect.new()
	shade.color = Color("11151a")
	_gallery.add_child(shade)
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var picture := TextureRect.new()
	picture.texture = texture
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_gallery.add_child(picture)
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	picture.offset_left = 55
	picture.offset_right = -55
	picture.offset_top = 55
	picture.offset_bottom = -55
	var back := _ink_button("返回", Color("eddfc8"))
	back.position = Vector2(24, 16)
	back.size = Vector2(90, 42)
	back.add_theme_font_size_override("font_size", 24)
	back.pressed.connect(func(): _gallery.hide())
	_gallery.add_child(back)
	back.grab_focus()

func _build_paper_sound() -> void:
	_paper_player = AudioStreamPlayer.new()
	_paper_player.bus = &"PageTurn"
	_paper_player.volume_db = -27.0
	var sound := AudioStreamWAV.new()
	sound.format = AudioStreamWAV.FORMAT_16_BITS
	sound.mix_rate = 22050
	var samples := PackedByteArray()
	samples.resize(8800)
	var random := RandomNumberGenerator.new()
	random.seed = 11807
	var filtered := 0.0
	for index in range(4400):
		var time := float(index) / 4400.0
		filtered = filtered * 0.55 + random.randf_range(-1.0, 1.0) * 0.45
		var envelope := sin(PI * pow(time, 0.62)) * (0.7 + sin(time * 24.0) * 0.15)
		samples.encode_s16(index * 2, int(filtered * envelope * 22000.0))
	sound.data = samples
	_paper_player.stream = sound
	add_child(_paper_player)

func _play_page_sound() -> void:
	if is_instance_valid(_paper_player): _paper_player.play()

func _process(delta: float) -> void:
	if not is_visible_in_tree(): return
	_position_controls()
	if _is_open and not _turning and not _blocked and not _confirm.visible:
		_navigation.tick(page_root, delta)
	if _notice_time > 0.0:
		_notice_time -= delta
		_notice.visible = _notice_time > 0.0

func _turn_section(direction: int) -> void:
	var index := clampi(int(_page_id.substr(1)) - 1 + direction, 6 if desktop else 0, 7)
	open_section("B%02d" % (index + 1))

func _local_point(point: Vector2) -> Vector2:
	return get_global_transform_with_canvas().affine_inverse() * point

func _paper_point(point: Vector2) -> Vector2:
	return stage.page_point(_local_point(point) - _frame.position, Vector2(TEXTURE_SIZE))

func _input(event: InputEvent) -> void:
	if not is_visible_in_tree() or not _is_open or Engine.is_editor_hint(): return
	if is_instance_valid(_current_page) and _current_page.has_method("is_capturing_input") and _current_page.is_capturing_input():
		var capture := event.duplicate() as InputEvent
		if capture is InputEventMouse:
			capture.position = _paper_point(event.position)
			capture.global_position = capture.position
		page_viewport.push_input(capture, true)
		get_viewport().set_input_as_handled()
		return
	if Navigation.is_back(event):
		if is_instance_valid(_gallery) and _gallery.visible: _gallery.hide()
		elif _confirm.visible: _confirm.hide()
		else: close_book()
		get_viewport().set_input_as_handled()
		return
	if _blocked or _turning:
		get_viewport().set_input_as_handled()
		return
	if _confirm.visible or (is_instance_valid(_gallery) and _gallery.visible): return
	if event.is_action_pressed("book_prev") or event.is_action_pressed("book_next"):
		_turn_section(-1 if event.is_action_pressed("book_prev") else 1)
		get_viewport().set_input_as_handled()
		return
	if event is InputEventMouseButton and event.pressed and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN]:
		var settings := get_node_or_null("/root/GameSettings")
		if settings != null and str(settings.get_value("controls", "wheel_mode")) == "pages":
			_turn_section(-1 if event.button_index == MOUSE_BUTTON_WHEEL_UP else 1)
			get_viewport().set_input_as_handled()
			return
	var copy := event.duplicate() as InputEvent
	if copy is InputEventMouse:
		var local := _local_point(event.position)
		for button in _tabs + _stamps:
			if button.visible and button.get_rect().has_point(local): return
		var uv: Vector2 = stage.page_point(local - _frame.position, Vector2(TEXTURE_SIZE))
		copy.position = uv
		copy.global_position = uv
		if copy is InputEventMouseMotion: copy.relative = uv - _previous_uv
		_previous_uv = uv
		if not Rect2(Vector2.ZERO, Vector2(TEXTURE_SIZE)).has_point(uv):
			if copy is InputEventMouseMotion: page_viewport.push_input(copy, true)
			return
	if _navigation.handle(page_root, copy):
		get_viewport().set_input_as_handled()
		return
	page_viewport.push_input(copy, true)
	if page_viewport.is_input_handled() or event is InputEventMouse:
		get_viewport().set_input_as_handled()

func _focus_page() -> void:
	if is_instance_valid(_current_page) and _current_page.has_method("restore_focus"):
		_current_page.restore_focus()

func screen_rect_for_control(control: Control) -> Rect2:
	var rect := control.get_global_rect()
	var corners := [rect.position, rect.end, Vector2(rect.end.x, rect.position.y), Vector2(rect.position.x, rect.end.y)]
	var minimum := Vector2(INF, INF)
	var maximum := Vector2(-INF, -INF)
	var canvas := get_global_transform_with_canvas()
	for point: Vector2 in corners:
		var projected: Vector2 = _frame.position + stage.page_screen(point, Vector2(TEXTURE_SIZE))
		projected = canvas * projected
		minimum = minimum.min(projected)
		maximum = maximum.max(projected)
	return Rect2(minimum, maximum - minimum)

func _on_setting_changed(section: String, key: String, _value: Variant) -> void:
	if section == "graphics" and key == "ui_scale": _resize()
	if (section == "accessibility" and key in ["high_contrast", "plain_fonts", "text_scale"]) or (section == "graphics" and key == "ui_scale"):
		Materials.apply(page_root)
		for page: Control in _pages.values(): Materials.apply(page)
		for button: Button in _tabs + _stamps:
			if page_root.theme.default_font != null:
				button.add_theme_font_override("font", page_root.theme.default_font)
		_position_controls()

func is_active() -> bool:
	return _is_open or _turning

func transition_state() -> String:
	return "turning" if _turning else "open" if _is_open else "closed"

func set_external_blocked(value: bool) -> void:
	_blocked = value

func show_bookmark_message(message: String) -> void:
	_notice.text = message
	_notice.show()
	_notice_time = 4.5
	if _page_id == "B07" and is_instance_valid(_current_page) and _current_page.has_method("configure"):
		_current_page.configure(_page_data(), archive)

func page_trace_context() -> Dictionary:
	return {"panel": "title" if desktop else "casebook", "page": _page_id, "transition": transition_state(), "pinned": not _pinned.is_empty()}

func capture_replay_state() -> Dictionary:
	return {"page": _page_id, "pinned": _pinned.duplicate(true)}

func restore_replay_state(state: Dictionary) -> void:
	_pinned = state.get("pinned", {}).duplicate(true)
	open_section(str(state.get("page", "B01")))
