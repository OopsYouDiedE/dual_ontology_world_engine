extends Control
## Native page controls rendered on a physical book in the desk's 3D camera.
## Visual presentation only: emits the same ui_event dictionaries as before.
signal ui_event(event: Dictionary)
signal transition_finished(shown: bool)
signal close_requested
signal field_reveal_finished(cancelled: bool)

const Sound = preload("res://scenes/Shared/Audio/Sound.gd")
const BookSettingsPage = preload("res://scenes/UI/BookSettingsPage.gd")
const ReplayPreferences = preload("res://scenes/UI/ReplayPreferences.gd")
const BookArchivePage = preload("res://scenes/UI/BookArchivePage.gd")
const UIInput = preload("res://scenes/Shared/Input/UIInput.gd")
const Transition = preload("res://scenes/UI/UITransition.gd")
const BookProjection = preload("res://scenes/UI/BookProjection3D.gd")
const LeafTurn = preload("res://scenes/UI/BookLeafTurn3D.gd")
const BookSurfaceDetail = preload("res://scenes/UI/BookSurfaceDetail3D.gd")
const FIELD_REVEAL_SHADER: Shader = preload("res://scenes/UI/title_reveal.gdshader")
const PAGE_SIZE := Vector2(4.8, 6.8)
const PAGE_Y := 0.325
const OPEN_PAGE_Y := 0.2775
const ASSET_ROOT := "res://ShareAsset/Internal/Props/UIDesk/Painterly3D/"
const TEXTS := {
	"zh_CN": ["安眠者有福", "开始阅读", "续读", "凡例", "合卷"],
	"en": ["Blessed Are Those Who Sleep", "Begin reading", "Continue", "Settings", "Close the book"],
	"ja": ["眠る者に福あれ", "読みはじめる", "続きから", "設定", "本を閉じる"],
}
const TITLE_TYPES := {"zh_CN": &"PixelDisplay", "en": &"PixelMast", "ja": &"PixelMast"}
const LOADING_TEXTS := {
	"zh_CN": {"session": "正在准备……", "load": "正在读取书签……", "map": "正在准备现场……", "cast": "正在准备人物……", "frame": "即将展开……", "failed": "准备未完成", "restart": "请关闭后重新打开游戏。"},
	"en": {"session": "Preparing…", "load": "Reading bookmark…", "map": "Preparing the scene…", "cast": "Preparing the cast…", "frame": "Ready to unfold…", "failed": "Unable to begin", "restart": "Please close and reopen the game."},
	"ja": {"session": "準備しています…", "load": "しおりを読み込んでいます…", "map": "舞台を整えています…", "cast": "人物を準備しています…", "frame": "まもなく開きます…", "failed": "準備できませんでした", "restart": "ゲームを終了してから、もう一度起動してください。"},
}


var mode := "title"
var replay_mode := false
var _replay_preferences: Node
var _replay_bookmarks: Array[String] = []
var _listed_bookmarks: Array[String] = []
var initial_page := "index"
var archive: RefCounted
var _external_blocked := false
var _leaf_index: Button
var _leaf_scroll: ScrollContainer
var _bookmark_status: RichTextLabel
@onready var start_button: Button = %StartButton
@onready var continue_button: Button = %ContinueButton
@onready var settings_button: Button = %SettingsButton
@onready var quit_button: Button = %QuitButton
@onready var title_label: Label = %TitleLabel
@onready var rule: Control = %Rule
@onready var viewport: SubViewport = $CoverViewport
@onready var _cover_root: Control = $CoverViewport/CoverRoot
@onready var _slip: PanelContainer = $CoverViewport/CoverRoot/Slip
@onready var _stage_viewport: SubViewport = $StageViewport
@onready var _desk: Node3D = $StageViewport/DeskWorld
@onready var _projection_surface: TextureRect = $DeskProjection
var _projection = BookProjection.new()
var _book: Node3D
var _cover_leaf: Node3D
var _page_leaf: Node3D
var _right_surface: MeshInstance3D
var _cloth_normal_map: Texture2D
var _cloth_roughness_map: Texture2D
var _paper_normal_map: Texture2D
var _paper_roughness_map: Texture2D
var _snapshot_viewport: SubViewport
var _snapshot_display: TextureRect
var _cover_snapshot_viewport: SubViewport
var _cover_snapshot_display: TextureRect
var _endpaper_viewport: SubViewport
var _endpaper_title: Label
var _open_amount := 0.0
var _current_page := "cover"
var _frozen_page := ""
var _turning := false
var _turn_tween: Tween
var _book_leaf: PanelContainer
var _leaf_body: VBoxContainer
var _leaf_back: Button
var _leaf_next: Button
var _leaf_heading: Label
var _leaf_rule: HSeparator
var _leaf_content: Control
var _page_history: Array[String] = []
var _page_states: Dictionary = {}
var _bookmark_confirming := false
var _file_dialog: FileDialog
var _load_error_line: RichTextLabel
var _page_motion = Transition.new()
var _reduce_motion := false
var _labels := {}
var _menu_input_enabled := false
var _request_pending := false
var _leaving := false
var _opening_kind := ""
var _loading_stage := ""
var _opening_error := ""
var _startup_failed := false
var _startup_error_dialog: AcceptDialog
var _loading_status: RichTextLabel
var _field_reveal_material: ShaderMaterial
var _field_reveal_tween: Tween
var _field_revealing := false
var _field_reveal_cancelled := false
var _field_reveal_complete := false

func _ready() -> void:
	if replay_mode:
		_replay_preferences = ReplayPreferences.new()
		_replay_preferences.restore_data(ReplayPreferences.capture(get_node_or_null("/root/GameSettings")))
		add_child(_replay_preferences)
	_projection_surface.texture = _stage_viewport.get_texture()
	if mode == "title":
		# Compile the reveal shader with the normal title, before gameplay starts.
		_field_reveal_material = ShaderMaterial.new()
		_field_reveal_material.shader = FIELD_REVEAL_SHADER
		_field_reveal_material.set_shader_parameter("paper_texture", load(ASSET_ROOT + "reference-paper.webp"))
		_field_reveal_material.set_shader_parameter("paper_tint", get_theme_color("page_light", "Title"))
		_projection_surface.material = _field_reveal_material
	_projection_surface.resized.connect(_resize_stage)
	_resize_stage()
	_slip.resized.connect(_update_paper_mount_size)
	_update_paper_mount_size()
	_desk.apply_material_atlas(load(ASSET_ROOT + "reference-material-atlas.webp"))
	_desk.apply_material_maps(_load_optional_material_map("reference-normal.png"), _load_optional_material_map("reference-roughness.png"))
	_setup_leaf()
	_setup_book()
	_projection.configure(_desk, _projection_surface, viewport, _book)
	rule.draw.connect(_draw_rule)
	_localize()
	for button: Button in [start_button, continue_button, settings_button, quit_button]:
		button.focus_entered.connect(_mark.bind(button, true))
		button.focus_exited.connect(_mark.bind(button, false))
		button.mouse_entered.connect(_hover.bind(button))
	start_button.pressed.connect(_request.bind("start"))
	quit_button.pressed.connect(_request.bind("quit"))
	settings_button.pressed.connect(open_settings)
	continue_button.pressed.connect(open_load_book)
	var settings := _replay_preferences if replay_mode else get_node_or_null("/root/GameSettings")
	if settings != null:
		_reduce_motion = bool(settings.get_value("accessibility", "reduce_motion"))
		settings.setting_changed.connect(_on_setting_changed)
	_desk.set_reduce_motion(_reduce_motion)
	_page_motion.configure(self, self, _projection_surface, null, false, 1.0)
	_page_motion.completed.connect(_on_page_transition)
	_set_menu_input(false)
	if mode == "title": Sound.music("main_theme", 2.5)
	transition_in.call_deferred()
	if mode == "casebook": open_section.bind(initial_page).call_deferred()

func _resize_stage() -> void:
	# Keep the frozen GPU texture intact if the window changes during the reveal.
	# The anchored display and normalized shader coordinates still fill the window.
	if _field_revealing: return
	# Preserve the window's aspect ratio instead of stretching the whole desk.
	var dimensions := _projection_surface.size.max(Vector2(320, 240))
	var resolution_scale := minf(1.0, 1600.0 / maxf(dimensions.x, dimensions.y))
	_stage_viewport.size = Vector2i((dimensions * resolution_scale).round())

func _update_paper_mount_size() -> void:
	var mount := _slip.material as ShaderMaterial
	if mount != null:
		mount.set_shader_parameter("paper_size", _slip.size)
	if is_instance_valid(_cover_leaf) and _cover_leaf.leaf_material != null:
		var dimensions := Vector2(viewport.size).max(Vector2.ONE)
		var minimum := _slip.position / dimensions
		var maximum := (_slip.position + _slip.size) / dimensions
		_cover_leaf.leaf_material.set_shader_parameter("front_paper_rect", Vector4(minimum.x, minimum.y, maximum.x, maximum.y))

func _load_optional_material_map(file_name: String) -> Texture2D:
	var path := ASSET_ROOT + file_name
	if not ResourceLoader.exists(path):
		return null
	return load(path) as Texture2D

func transition_in() -> bool:
	if _field_revealing or _field_reveal_complete or _leaving: return false
	_leaving = false

	_set_menu_input(false)
	var request_id: int = _page_motion.request(true, true, 0.24)
	var completed: bool = await _page_motion.wait_for(request_id)
	if completed and not _leaving and not _request_pending:
		_set_menu_input(true)
		if not _request_pending: _restore_page_state()
	return completed and not _leaving

func transition_out() -> bool:
	if _field_revealing: _finish_field_reveal(true)
	_leaving = true
	_set_menu_input(false)
	var request_id: int = _page_motion.request(false, true, 0.20)
	return await _page_motion.wait_for(request_id)

## Loading owns the current printed page. Only the host may reveal the field.
func begin_opening(kind := "start") -> void:
	if mode != "title" or _leaving or _startup_failed or _request_pending: return
	_opening_kind = kind
	_opening_error = ""
	_loading_stage = "session"
	_request_pending = true
	# Auto-start may arrive before the initial fade; cover the field completely.
	_page_motion.request(true, false)
	_page_motion.finish_current()
	_set_menu_input(false)
	_refresh_loading_status()

func set_loading_stage(stage: String) -> void:
	if not _request_pending or _leaving or not LOADING_TEXTS["zh_CN"].has(stage): return
	_loading_stage = stage
	_refresh_loading_status()

func _loading_text() -> String:
	var key := "load" if _loading_stage == "session" and _opening_kind == "load" else _loading_stage
	return str(LOADING_TEXTS[_language()].get(key, ""))

func _refresh_loading_status() -> void:
	_mark(start_button, start_button.has_focus())
	start_button.tooltip_text = _opening_error
	if _current_page == "load" and _request_pending and _opening_kind != "":
		if not is_instance_valid(_loading_status):
			_loading_status = _leaf_text("")
			_loading_status.theme_type_variation = &"BookCaption"
		_loading_status.text = _loading_text()
		_loading_status.show()
	elif is_instance_valid(_loading_status):
		_loading_status.hide()

func cancel_opening() -> void:
	if _field_revealing: _finish_field_reveal(true)
	_field_reveal_complete = false
	_leaving = false
	_request_pending = false
	_opening_kind = ""
	_loading_stage = ""
	_opening_error = ""
	_startup_failed = false
	if _field_reveal_material != null: _field_reveal_material.set_shader_parameter("progress", 0.0)
	_stage_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	_resize_stage()
	_set_menu_input(true)
	_refresh_loading_status()

func show_start_error(message: String, retryable := false) -> void:
	cancel_opening()
	_startup_failed = not retryable
	_opening_error = message
	_set_menu_input(true)
	_refresh_loading_status()
	if _current_page == "cover":
		if _startup_failed: quit_button.grab_focus()
		else: start_button.grab_focus()
	else:
		if not is_instance_valid(_load_error_line):
			_load_error_line = _leaf_text("")
		_load_error_line.text = str(LOADING_TEXTS[_language()].failed)
		_load_error_line.tooltip_text = message
		_leaf_back.grab_focus()
	if _startup_failed:
		if not is_instance_valid(_startup_error_dialog):
			_startup_error_dialog = AcceptDialog.new()
			_startup_error_dialog.theme = theme
			_startup_error_dialog.process_mode = Node.PROCESS_MODE_ALWAYS
			add_child(_startup_error_dialog)
			_startup_error_dialog.confirmed.connect(_close_start_error)
			_startup_error_dialog.close_requested.connect(_close_start_error)
		_startup_error_dialog.title = str(LOADING_TEXTS[_language()].failed)
		_startup_error_dialog.dialog_text = message + "\n\n" + str(LOADING_TEXTS[_language()].restart)
		_startup_error_dialog.popup_centered(Vector2i(660, 280))

func _close_start_error() -> void:
	if is_instance_valid(_startup_error_dialog): _startup_error_dialog.hide()
	if not is_inside_tree() or _leaving: return
	if _current_page == "cover": quit_button.grab_focus()
	else: _leaf_back.grab_focus()

func transition_to_field() -> bool:
	if mode != "title" or not is_inside_tree() or _startup_failed: return false
	if _field_reveal_complete: return not _field_reveal_cancelled
	if _field_revealing:
		while _field_revealing: await field_reveal_finished
		return _field_reveal_complete and not _field_reveal_cancelled
	_field_revealing = true
	_field_reveal_cancelled = false
	_leaving = true
	_request_pending = true
	_set_menu_input(false)
	# Settle a possible initial fade without allowing its continuation to unlock.
	var shown_request: int = _page_motion.request(true, false)
	_page_motion.finish_current()
	await _page_motion.wait_for(shown_request)
	if not is_inside_tree() or not _field_revealing: return _field_reveal_complete and not _field_reveal_cancelled
	# The completed preparation leaves no loading message printed on the leaf.
	# Native controls may regain their normal ink while viewport input stays locked.
	_opening_kind = ""
	_loading_stage = ""
	_refresh_loading_status()
	for button: Button in [start_button, continue_button, settings_button, quit_button]:
		button.disabled = false
	viewport.gui_disable_input = true
	# This is an existing viewport texture, frozen on the GPU once. The world
	# underneath has already been drawn by Main; no preview or image readback.
	var rendered := DisplayServer.get_name() != "headless"
	if rendered:
		# The book controls feed a texture inside the desk's own viewport. Let
		# the updated inner texture reach the desk before freezing that texture.
		await RenderingServer.frame_post_draw
		if not is_inside_tree() or not _field_revealing: return _field_reveal_complete and not _field_reveal_cancelled
		await RenderingServer.frame_post_draw
	else: await get_tree().process_frame
	if not is_inside_tree() or not _field_revealing: return _field_reveal_complete and not _field_reveal_cancelled
	_stage_viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
	viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
	if _reduce_motion or not rendered:
		_finish_field_reveal(false)
	else:
		_field_reveal_tween = Transition.real_tween(self)
		_field_reveal_tween.set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_IN_OUT)
		_field_reveal_tween.tween_method(_set_field_reveal, 0.0, 1.0, 0.82)
		_field_reveal_tween.finished.connect(_finish_field_reveal.bind(false))
	while _field_revealing: await field_reveal_finished
	return _field_reveal_complete and not _field_reveal_cancelled

func _set_field_reveal(value: float) -> void:
	if _field_reveal_material != null:
		_field_reveal_material.set_shader_parameter("progress", value)

func _finish_field_reveal(cancelled: bool) -> void:
	if not _field_revealing: return
	_field_revealing = false
	_field_reveal_cancelled = cancelled
	_field_reveal_complete = not cancelled
	if _field_reveal_tween != null:
		_field_reveal_tween.kill()
		_field_reveal_tween = null
	if not cancelled: _set_field_reveal(1.0)
	field_reveal_finished.emit(cancelled)

func _on_page_transition(_request_id: int, shown: bool, cancelled: bool) -> void:
	if not cancelled: transition_finished.emit(shown)

func _on_setting_changed(section: String, key: String, value: Variant) -> void:
	if section == "language" and replay_mode: _localize()
	if section == "accessibility" and key == "reduce_motion":
		_reduce_motion = bool(value)
		_desk.set_reduce_motion(_reduce_motion)
		if _reduce_motion:
			_page_motion.finish_current()
			if _turn_tween != null and _turn_tween.is_valid(): _turn_tween.custom_step(10.0)
			if _field_revealing: _finish_field_reveal(false)

func _set_menu_input(enabled: bool) -> void:
	_menu_input_enabled = enabled and not _external_blocked and not _leaving and not _request_pending and not _turning and _current_page == "cover"
	viewport.gui_disable_input = _external_blocked or _leaving or _request_pending or _turning
	for button: Button in [start_button, continue_button, settings_button, quit_button]:
		button.disabled = not _menu_input_enabled
	if _request_pending and _opening_kind != "" and _current_page == "cover":
		# The viewport and request owner already block interaction. Keep the
		# loading status in the theme's normal ink so it remains readable.
		start_button.disabled = false
	if _startup_failed:
		start_button.disabled = true
		continue_button.disabled = true
		if _current_page == "load":
			for child in _leaf_body.get_children():
				if child is BaseButton: child.disabled = true

func _settings_active() -> bool:
	return _current_page == "settings"

func _exit_tree() -> void:
	_finish_field_reveal(true)
	if _turn_tween != null and _turn_tween.is_valid(): _turn_tween.kill()
	_page_motion.dispose()

func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSLATION_CHANGED and is_node_ready(): _localize()
	elif what == NOTIFICATION_APPLICATION_FOCUS_OUT and _projection != null:
		_projection.reset_navigation()

func _language() -> String:
	if replay_mode and _replay_preferences != null: return _replay_preferences.current_language()
	var locale := TranslationServer.get_locale()
	return "ja" if locale.begins_with("ja") else "en" if locale.begins_with("en") else "zh_CN"

func _localize() -> void:
	var texts: Array = TEXTS[_language()]
	title_label.theme_type_variation = TITLE_TYPES[_language()]
	title_label.text = texts[0]
	if is_instance_valid(_endpaper_title):
		_endpaper_title.text = texts[0]
		_endpaper_viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	var buttons: Array[Button] = [start_button, continue_button, settings_button, quit_button]
	for index in buttons.size():
		_labels[buttons[index]] = texts[index + 1]
		_mark(buttons[index], buttons[index].has_focus())
	_localize_leaf()
	_refresh_loading_status()

func _process(delta: float) -> void:
	if not visible or not is_instance_valid(_book) or _field_revealing or _field_reveal_complete: return
	_projection.update(_projection_surface.get_local_mouse_position(), delta, _reduce_motion)

func _mark(button: Button, selected: bool) -> void:
	if button == start_button and _request_pending and _opening_kind != "":
		button.text = _loading_text()
		return
	if button == start_button and _startup_failed:
		button.text = str(LOADING_TEXTS[_language()].failed)
		return
	button.text = ("☞ " + str(_labels.get(button, button.text))) if selected else str(_labels.get(button, button.text))

func _hover(button: Button) -> void:
	if _menu_input_enabled and not button.disabled: button.grab_focus()

func _draw_rule() -> void:
	var color := get_theme_color("rule_strong", "Paper")
	rule.draw_rect(Rect2(0, 0, rule.size.x, 2), color)
	rule.draw_rect(Rect2(0, 5, rule.size.x, 1), color)

func set_external_blocked(blocked: bool) -> void:
	_external_blocked = blocked
	_set_menu_input(not blocked)

func open_section(page: String) -> void:
	if not is_inside_tree(): return
	if _turning:
		return
	if not _leaving and not _request_pending:
		if _current_page == page:
			_capture_page_state()
			_show_leaf(page)
			_restore_page_state.call_deferred()
		else:
			if mode != "casebook" or _current_page != "cover": _page_history.append(_current_page)
			_turn_to(page)

func _leave_book() -> void:
	if _turning or _leaving or _request_pending or _external_blocked: return
	if _bookmark_confirming:
		_show_leaf("load")
		_restore_page_state.call_deferred()
		return
	if not _page_history.is_empty():
		_turn_to(_page_history.pop_back())
	elif mode == "casebook":
		if _current_page != "index": _turn_to("index")
		else: close_requested.emit()
	elif _current_page != "cover": _turn_to("cover")

func _localize_leaf() -> void:
	if not is_instance_valid(_leaf_heading): return
	_leaf_back.text = tr("返回书签") if _bookmark_confirming else tr("返回") if not _page_history.is_empty() else tr("返回现场") if mode == "casebook" and _current_page == "index" else tr("返回目录") if mode == "casebook" else tr("☜ 封面")
	_leaf_index.text = tr("目录")
	_leaf_next.text = tr("续读 ☞") if _current_page == "settings" else tr("凡例 ☞")
	var headings := {"settings": "凡例与设置", "load": "续读 · 书签", "index": "案卷目录",
		"evidence": "证据", "items": "物品", "people": "人物立像", "gallery": "图版", "hypotheses": "猜想", "records": "记录"}
	_leaf_heading.text = tr(headings.get(_current_page.get_slice("@", 0), "个人批注" if _current_page.begins_with("write:") else "案卷记录"))
	if is_instance_valid(_leaf_body):
		for child in _leaf_body.get_children():
			if child is Control and child.has_meta("bookmark_display_path"):
				_bind_bookmark_label(child, str(child.get_meta("bookmark_display_path")))

func open_settings() -> void:
	open_section("settings")

func open_load_book() -> void:
	if not _turning and not _leaving and not _request_pending and not _startup_failed: open_section("load")

func _input(event: InputEvent) -> void:
	if is_instance_valid(_startup_error_dialog) and _startup_error_dialog.visible: return
	if not is_visible_in_tree() or not is_instance_valid(_book): return
	if _external_blocked or _request_pending or _turning or _leaving:
		get_viewport().set_input_as_handled()
		return
	if event is InputEventMouse or event is InputEventKey or event is InputEventJoypadButton or event is InputEventJoypadMotion:
		# Back belongs to the book, before a slider/editor can consume its
		# right-click. Handle the release here too so it cannot leak to the field.
		if UIInput.is_back(event, false) or event.is_action("notebook"):
			if event.is_pressed() and not event.is_echo(): _leave_book()
		elif event.is_action("book_prev") or event.is_action("book_next"):
			if event.is_pressed() and not event.is_echo(): _flip_adjacent_page(-1 if event.is_action("book_prev") else 1)
		elif not _turning and not _leaving and not _request_pending:
			if not _projection.forward(event): _unhandled_input(event)
		get_viewport().set_input_as_handled()

func _unhandled_input(event: InputEvent) -> void:
	if is_instance_valid(_startup_error_dialog) and _startup_error_dialog.visible: return
	if not event.is_pressed() or event.is_echo() or _turning or _leaving: return
	if _current_page != "cover" and not _request_pending:
		if event.is_action_pressed("cancel") or event.is_action_pressed("ui_cancel") or event.is_action_pressed("settings"):
			_leave_book()
		return
	if not _menu_input_enabled: return
	if event.is_action_pressed("ui_accept") or event.is_action_pressed("dialogue_continue"):
		_request("start")
	elif event.is_action_pressed("settings"):
		open_settings()

func _flip_adjacent_page(direction: int) -> void:
	if _turning or _leaving or _request_pending or _external_blocked: return
	var focused := viewport.gui_get_focus_owner()
	if focused is TextEdit or focused is LineEdit: return
	if is_instance_valid(_leaf_content) and _leaf_content.has_method("request_adjacent_page"):
		if _leaf_content.request_adjacent_page(direction): return
	if _current_page in ["settings", "load"]:
		open_section("load" if _current_page == "settings" else "settings")

func _request(kind: String, path := "") -> void:
	if _turning or _leaving or _request_pending or _external_blocked: return
	if mode == "casebook":
		if kind == "load": _confirm_bookmark(path)
		elif kind == "quit": close_requested.emit()
		return
	if _startup_failed and kind in ["start", "load"]: return
	if kind == "start":
		if not _menu_input_enabled: return
	if kind in ["start", "load"]:
		begin_opening(kind)
	else:
		_request_pending = true
		_set_menu_input(false)
	Sound.sfx("ui_click", -4.0)
	var request := {"kind": kind}
	if path != "": request.path = path
	ui_event.emit(request)

func _setup_book() -> void:
	_book = Node3D.new()
	_book.name = "PhysicalBook"
	_book.rotation_degrees.y = -7.32
	_desk.add_child(_book)
	var cloth: Texture2D = load(ASSET_ROOT + "reference-cloth.webp")
	var paper: Texture2D = load(ASSET_ROOT + "reference-paper.webp")
	_cloth_normal_map = _load_optional_material_map("reference-cloth-normal.png")
	_cloth_roughness_map = _load_optional_material_map("reference-cloth-roughness.png")
	_paper_normal_map = _load_optional_material_map("reference-paper-normal.png")
	_paper_roughness_map = _load_optional_material_map("reference-paper-roughness.png")
	var binding_material := StandardMaterial3D.new()
	binding_material.albedo_texture = cloth
	binding_material.albedo_color = Color(0.85, 0.85, 0.85)
	binding_material.roughness = 0.91
	if _cloth_normal_map != null:
		binding_material.normal_enabled = true
		binding_material.normal_texture = _cloth_normal_map
		binding_material.normal_scale = 0.25
	if _cloth_roughness_map != null:
		binding_material.roughness = 1.0
		binding_material.roughness_texture = _cloth_roughness_map
		binding_material.roughness_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_RED
	_add_box("LowerCover", Vector3(4.96, 0.065, 6.96), Vector3(2.4, 0.065, 0), binding_material)
	var edge_material := ShaderMaterial.new()
	edge_material.shader = load("res://scenes/UI/book_edges.gdshader")
	edge_material.set_shader_parameter("paper_texture", paper)
	edge_material.set_shader_parameter("paper_normal_texture", _paper_normal_map)
	edge_material.set_shader_parameter("paper_roughness_texture", _paper_roughness_map)
	edge_material.set_shader_parameter("has_paper_normal", _paper_normal_map != null)
	edge_material.set_shader_parameter("has_paper_roughness", _paper_roughness_map != null)
	_add_box("PageBlock", Vector3(4.79, 0.175, 6.79), Vector3(2.4, 0.184, 0), edge_material)
	_add_box("Binding", Vector3(0.11, 0.245, 6.9), Vector3(-0.045, 0.174, 0), binding_material)
	var page_mesh := PlaneMesh.new()
	page_mesh.size = PAGE_SIZE
	_right_surface = MeshInstance3D.new()
	_right_surface.name = "InteractivePage"
	_right_surface.mesh = page_mesh
	_right_surface.position = Vector3(PAGE_SIZE.x * 0.5, PAGE_Y, 0)
	var page_material := StandardMaterial3D.new()
	page_material.albedo_texture = viewport.get_texture()
	page_material.albedo_color = Color.WHITE
	page_material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR
	page_material.roughness = 0.96
	if _paper_normal_map != null:
		page_material.normal_enabled = true
		page_material.normal_texture = _paper_normal_map
		page_material.normal_scale = 0.12
	if _paper_roughness_map != null:
		page_material.roughness = 1.0
		page_material.roughness_texture = _paper_roughness_map
		page_material.roughness_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_RED
	page_material.metallic_specular = 0.08
	_right_surface.material_override = page_material
	_book.add_child(_right_surface)
	_cover_snapshot_viewport = _make_snapshot("CoverFrozen")
	_cover_snapshot_display = _cover_snapshot_viewport.get_child(0)
	_snapshot_viewport = _make_snapshot("PageFrozen")
	_snapshot_display = _snapshot_viewport.get_child(0)
	_endpaper_viewport = SubViewport.new()
	_endpaper_viewport.name = "Endpaper"
	_endpaper_viewport.size = viewport.size
	_endpaper_viewport.disable_3d = true
	_endpaper_viewport.transparent_bg = true
	_endpaper_viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	add_child(_endpaper_viewport)
	var background := TextureRect.new()
	background.texture = paper
	background.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	background.size = Vector2(viewport.size)
	_endpaper_viewport.add_child(background)
	var inside_title := Label.new()
	_endpaper_title = inside_title
	inside_title.theme = theme
	inside_title.theme_type_variation = &"BookHeading"
	inside_title.text = TEXTS[_language()][0]
	inside_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	inside_title.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	inside_title.position = Vector2(54, 110)
	inside_title.size = Vector2(472, 100)
	_endpaper_viewport.add_child(inside_title)
	var illustration := TextureRect.new()
	illustration.texture = load("res://ShareAsset/External/CampiPhlegraei/campi_phlegraei_plate_vi.jpg")
	illustration.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	illustration.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	illustration.position = Vector2(65, 240)
	illustration.size = Vector2(450, 310)
	illustration.modulate = Color(0.78, 0.78, 0.75, 0.78)
	_endpaper_viewport.add_child(illustration)
	# Draw the actual closed board from startup, including its sides. This also
	# compiles the leaf shader before the first user-triggered opening.
	_cover_leaf = LeafTurn.new()
	_book.add_child(_cover_leaf)
	_cover_leaf.position.y = PAGE_Y - 0.0275
	_cover_leaf.configure(viewport.get_texture(), _endpaper_viewport.get_texture(), PAGE_SIZE, false, true)
	_cover_leaf.leaf_material.set_shader_parameter("surface_tint", Color.WHITE)
	_cover_leaf.set_surface_maps(_cloth_normal_map, _cloth_roughness_map, _paper_normal_map, _paper_roughness_map, true)
	_update_paper_mount_size()
	_right_surface.hide()

func _add_box(label: String, dimensions: Vector3, position_value: Vector3, material: Material) -> void:
	var instance := MeshInstance3D.new()
	instance.name = label
	var bevel := 0.012
	if label == "PageBlock": bevel = 0.006
	elif label == "Binding": bevel = 0.018
	var surface := SurfaceTool.new()
	surface.create_from(BookSurfaceDetail.beveled_box(dimensions, bevel), 0)
	surface.generate_tangents()
	instance.mesh = surface.commit()
	instance.position = position_value
	instance.material_override = material
	_book.add_child(instance)

func _make_snapshot(label: String) -> SubViewport:
	var frozen := SubViewport.new()
	frozen.name = label
	frozen.size = viewport.size
	frozen.disable_3d = true
	frozen.transparent_bg = true
	frozen.render_target_update_mode = SubViewport.UPDATE_DISABLED
	add_child(frozen)
	var image := TextureRect.new()
	image.texture = viewport.get_texture()
	image.size = Vector2(viewport.size)
	image.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	frozen.add_child(image)
	return frozen

func _freeze_page(frozen: SubViewport) -> void:
	# Copy once on the GPU; the frozen viewport owns its rendered page.
	frozen.render_target_update_mode = SubViewport.UPDATE_ONCE
	await RenderingServer.frame_post_draw
	frozen.render_target_update_mode = SubViewport.UPDATE_DISABLED

func _set_open_amount(value: float) -> void:
	_open_amount = value
	_book.position.x = value * (2.0 - value) * _desk.BOOK_READING_OFFSET
	_desk.set_open_amount(value)

func _set_page_height(value: float) -> void:
	_right_surface.position.y = value
	_projection.paper_y = value

func _animate_leaf(value: float, leaf: Node3D, closing: bool, rigid: bool) -> void:
	leaf.set_progress(value)
	_desk.set_turn_lift(sin(value * PI))
	var opened := 1.0 - value if closing else value
	# The binding remains raised; the left board's outer edge rests on the desk.
	leaf.rotation.z = atan(0.27 / PAGE_SIZE.x) * opened
	if not rigid: leaf.position.y = lerpf(OPEN_PAGE_Y - 0.006, 0.33, value)

func _turn_to(page: String) -> void:
	if mode == "title" and (page not in ["cover", "settings", "load"] or (_startup_failed and page == "load")): return
	if _turning or _current_page == page: return
	_capture_page_state()
	_projection.reset_navigation()
	var previous := _current_page
	_frozen_page = previous
	var opening := previous == "cover"
	var closing := page == "cover"
	_turning = true
	# Lock input without changing the page's printed button colors before capture.
	viewport.gui_disable_input = true
	if opening:
		await _freeze_page(_cover_snapshot_viewport)
		if not is_inside_tree(): return
		_cover_leaf.set_textures(_cover_snapshot_viewport.get_texture(), _endpaper_viewport.get_texture())
		_cover_leaf.set_direction(false)
	elif closing:
		if is_instance_valid(_cover_leaf):
			_cover_leaf.set_direction(true)
	else:
		await _freeze_page(_snapshot_viewport)
		if not is_inside_tree(): return
		_page_leaf = LeafTurn.new()
		_book.add_child(_page_leaf)
		_page_leaf.position.y = OPEN_PAGE_Y - 0.006
		_page_leaf.configure(_snapshot_viewport.get_texture(), _endpaper_viewport.get_texture(), PAGE_SIZE, false, false)
		_page_leaf.leaf_material.set_shader_parameter("surface_tint", Color.WHITE)
		_page_leaf.set_surface_maps(_paper_normal_map, _paper_roughness_map, _paper_normal_map, _paper_roughness_map)
	_current_page = page
	_set_menu_input(false)
	if not closing:
		_show_leaf(page)
		_set_page_height(OPEN_PAGE_Y)
		_right_surface.show()
	var moving_leaf: Node3D = _cover_leaf if opening or closing else _page_leaf
	if _reduce_motion:
		if is_instance_valid(moving_leaf): _animate_leaf(1.0, moving_leaf, closing, opening or closing)
		_set_open_amount(0.0 if closing else 1.0)
	else:
		_turn_tween = Transition.real_tween(self)
		_turn_tween.set_parallel(true).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_IN_OUT)
		if is_instance_valid(moving_leaf): _turn_tween.tween_method(_animate_leaf.bind(moving_leaf, closing, opening or closing), 0.0, 1.0, 0.88 if opening or closing else 0.72)
		if opening or closing: _turn_tween.tween_method(_set_open_amount, _open_amount, 0.0 if closing else 1.0, 0.88)
		await _turn_tween.finished
		_turn_tween = null
	if closing:
		_show_leaf("cover")
		await RenderingServer.frame_post_draw
		_right_surface.hide()
		_set_page_height(PAGE_Y)
		_cover_leaf.set_textures(viewport.get_texture(), _endpaper_viewport.get_texture())
		_cover_leaf.set_direction(false)
		_cover_leaf.rotation.z = 0.0
	elif not opening:
		if is_instance_valid(_page_leaf): _page_leaf.queue_free()
		_page_leaf = null
	_turning = false
	_frozen_page = ""
	_set_menu_input(true)
	if not _request_pending and not _leaving: _restore_page_state()

func _capture_page_state() -> void:
	if not is_instance_valid(_leaf_scroll) or _bookmark_confirming: return
	var state := {"scroll": _leaf_scroll.scroll_vertical, "focus": -1}
	var focused := viewport.gui_get_focus_owner()
	var root: Control = _slip if _current_page == "cover" else _book_leaf
	if is_instance_valid(focused): state.focus = Transition.focusable_controls(root).find(focused)
	if is_instance_valid(_leaf_content) and _leaf_content.has_method("capture_view_state"):
		state.content = _leaf_content.capture_view_state()
	_page_states[_current_page] = state

func _restore_page_state() -> void:
	if not is_inside_tree() or _turning or _leaving or _request_pending: return
	var page := _current_page
	# Container layout is deferred; focus should scroll to the real, final row.
	await get_tree().process_frame
	if not is_inside_tree() or page != _current_page or _turning or _leaving or _request_pending: return
	var state: Dictionary = _page_states.get(page, {})
	var root: Control = _slip if page == "cover" else _book_leaf
	var controls: Array[Control] = Transition.focusable_controls(root)
	if not controls.is_empty():
		var index := int(state.get("focus", 0))
		controls[clampi(index, 0, controls.size() - 1)].grab_focus()
	if page != "cover": _leaf_scroll.set_deferred("scroll_vertical", int(state.get("scroll", 0)))

func _setup_leaf() -> void:
	_book_leaf = PanelContainer.new()
	_book_leaf.name = "BookLeaf"
	_book_leaf.theme_type_variation = &"BookLeaf"
	_book_leaf.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_book_leaf.offset_left = 10.0
	_book_leaf.offset_top = 12.0
	_book_leaf.offset_right = -10.0
	_book_leaf.offset_bottom = -12.0
	_cover_root.add_child(_book_leaf)
	_book_leaf.hide()
	var column := VBoxContainer.new()
	column.name = "LeafColumn"
	column.theme_type_variation = &"BookColumn"
	_book_leaf.add_child(column)
	var heading := Label.new()
	_leaf_heading = heading
	heading.name = "LeafHeading"
	heading.theme_type_variation = &"BookHeading"
	heading.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	column.add_child(heading)
	_leaf_rule = HSeparator.new()
	column.add_child(_leaf_rule)
	var scroll := ScrollContainer.new()
	_leaf_scroll = scroll
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	scroll.follow_focus = true
	column.add_child(scroll)
	_leaf_body = VBoxContainer.new()
	_leaf_body.theme_type_variation = &"BookColumn"
	_leaf_body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(_leaf_body)
	_leaf_back = Button.new()
	_leaf_back.text = tr("☜ 封面")
	_leaf_back.theme_type_variation = &"TitleMenuButton"
	_leaf_back.pressed.connect(_leave_book)
	var footer := HBoxContainer.new()
	column.add_child(footer)
	_leaf_back.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	footer.add_child(_leaf_back)
	_leaf_index = Button.new()
	_leaf_index.theme_type_variation = &"BookOption"
	_leaf_index.text = tr("目录")
	_leaf_index.pressed.connect(func():
		_page_history.clear()
		_turn_to("index")
	)
	footer.add_child(_leaf_index)
	_leaf_next = Button.new()
	_leaf_next.theme_type_variation = &"TitleMenuButton"
	_leaf_next.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_leaf_next.pressed.connect(func(): open_section("load" if _current_page == "settings" else "settings"))
	footer.add_child(_leaf_next)

func _show_leaf(page: String) -> void:
	_cover_root.get_node("Slip").visible = page == "cover"
	_cover_root.get_node("Cloth").visible = page == "cover"
	_cover_root.get_node("PaperGround").visible = page != "cover"
	_book_leaf.visible = page != "cover"
	if page == "cover": return
	_bookmark_confirming = false
	_leaf_heading.visible = page not in ["start", "field"]
	_leaf_rule.visible = _leaf_heading.visible
	_leaf_back.visible = page != "field" and (page != "start" or not _request_pending)

	_leaf_next.visible = page in ["settings", "load"] and not (_startup_failed and page == "settings")
	_leaf_index.visible = mode == "casebook" and page != "index"
	_localize_leaf()
	for child in _leaf_body.get_children():
		_leaf_body.remove_child(child)
		child.queue_free()
	_bookmark_status = null
	_load_error_line = null
	_loading_status = null
	_leaf_content = null
	_leaf_scroll.scroll_vertical = 0
	if page == "settings":
		var settings_page := BookSettingsPage.new()
		if replay_mode:
			settings_page.settings_endpoint = _replay_preferences
			settings_page.locale_endpoint = _replay_preferences
		_leaf_body.add_child(settings_page)
	elif page == "load":
		_populate_bookmarks()
	else:
		var content := BookArchivePage.new()
		_leaf_content = content
		content.archive = archive
		content.section = page.get_slice("@", 0)
		content.page_index = int(page.get_slice("@", 1)) if "@" in page else 0
		content.restore_view_state(_page_states.get(page, {}).get("content", {}))
		content.page_requested.connect(open_section)
		content.ui_event.connect(func(event: Dictionary): ui_event.emit(event))
		_leaf_body.add_child(content)

func _populate_bookmarks() -> void:
	if mode == "casebook":
		_leaf_button(tr("保存书签"), func():
			show_bookmark_message(tr("正在保存书签……"))
			ui_event.emit({"kind": "bookmark_save"})
		)
	var paths: Array[String] = []
	if replay_mode:
		paths.assign(_replay_bookmarks)
	else:
		var directory := DirAccess.open("user://saves")
		if directory != null:
			for file in directory.get_files():
				if file.ends_with(".dvsave"): paths.append("user://saves/" + file)
		# Normal shutdown saves remain valid bookmarks; list without rewriting them.
		var sessions := DirAccess.open("user://dv-sessions")
		if sessions != null:
			for folder in sessions.get_directories():
				var path := "user://dv-sessions/" + folder + "/save.dvsave"
				if FileAccess.file_exists(path): paths.append(path)
		paths.sort()
		paths.reverse()
	_listed_bookmarks.assign(paths)
	if paths.is_empty(): _leaf_text(tr("尚无书签。请选择已有存档继续阅读。"))
	for path in paths:
		var actual := ProjectSettings.globalize_path(path)
		var button := _leaf_button("", func(): _request("load", actual))
		_bind_bookmark_label(button, path)
	_leaf_button(tr("选择存档…"), _choose_save)

func _bookmark_display_name(path: String) -> String:
	var normalized := path.replace("\\", "/")
	var basename := normalized.get_file().get_basename().strip_edges()
	# These two formats are written with DateTime.UtcNow by the host. Preserve
	# that timezone explicitly; arbitrary external names carry no time promise.
	var bookmark_pattern := RegEx.new()
	bookmark_pattern.compile("^([0-9]{8})T([0-9]{6})[0-9]{7}Z-(bookmark|before-load)-[0-9a-fA-F]{32}$")
	var bookmark := bookmark_pattern.search(basename)
	if bookmark != null:
		var stamp := _bookmark_utc_stamp(bookmark.get_string(1), bookmark.get_string(2))
		return tr("载入前备份 · %s") % stamp if bookmark.get_string(3) == "before-load" else tr("书签 · %s") % stamp
	var parent := normalized.get_base_dir()
	if basename == "save" and parent.get_base_dir().get_file() == "dv-sessions":
		var session_pattern := RegEx.new()
		session_pattern.compile("^([0-9]{8})-([0-9]{6})-[0-9]{4}$")
		var session := session_pattern.search(parent.get_file())
		if session != null:
			return tr("会话存档 · %s") % _bookmark_utc_stamp(session.get_string(1), session.get_string(2))
	if basename.is_empty(): return tr("游戏存档")
	return basename if basename.length() <= 48 else basename.left(45) + "…"

func _bookmark_utc_stamp(day: String, clock: String) -> String:
	return "%s-%s-%s %s:%s:%s UTC" % [day.substr(0, 4), day.substr(4, 2), day.substr(6, 2),
		clock.substr(0, 2), clock.substr(2, 2), clock.substr(4, 2)]

func _bind_bookmark_label(control: Control, path: String) -> void:
	control.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	control.set_meta("bookmark_display_path", path)
	control.set("text", _bookmark_display_name(path))
	control.tooltip_text = ProjectSettings.globalize_path(path)

func _confirm_bookmark(path: String) -> void:
	# Review in the book. The host preserves the current session before loading.
	_capture_page_state()
	_bookmark_confirming = true
	_localize_leaf()
	for child in _leaf_body.get_children():
		_leaf_body.remove_child(child)
		child.queue_free()
	_leaf_text(tr("载入书签将切换当前进度，当前案卷会先保存为新的书签。"))
	var file_label := _leaf_text("")
	_bind_bookmark_label(file_label, path)
	var confirm_button := _leaf_button(tr("载入此书签"), func():
		show_bookmark_message(tr("正在载入书签……"))
		ui_event.emit({"kind": "bookmark_load", "path": path})
	)
	_leaf_button(tr("返回书签"), _leave_book)
	confirm_button.grab_focus()

func show_bookmark_message(message: String) -> void:
	if _current_page != "load": return
	if not is_instance_valid(_bookmark_status):
		_bookmark_status = _leaf_text(message)
	else:
		_bookmark_status.text = message

func _leaf_text(text: String) -> RichTextLabel:
	var label := RichTextLabel.new()
	label.fit_content = true
	label.theme_type_variation = &"BookBody"
	label.text = text
	_leaf_body.add_child(label)
	return label

func _leaf_button(text: String, action: Callable) -> Button:
	var button := Button.new()
	button.text = text
	button.theme_type_variation = &"BookOption"
	button.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	button.pressed.connect(action)
	_leaf_body.add_child(button)
	return button

func _choose_save() -> void:
	if replay_mode:
		show_bookmark_message(tr("回放中的文件选择保持只读。"))
		return
	if not is_instance_valid(_file_dialog):
		_file_dialog = FileDialog.new()
		_file_dialog.use_native_dialog = true
		_file_dialog.access = FileDialog.ACCESS_FILESYSTEM
		_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
		_file_dialog.filters = PackedStringArray(["*.dvsave ; " + tr("游戏存档")])
		_file_dialog.file_selected.connect(func(path: String): _request("load", path))
		add_child(_file_dialog)
	_file_dialog.popup_centered_ratio(0.7)

func show_load_error(message: String) -> void:
	cancel_opening()
	if not is_instance_valid(_load_error_line):
		_load_error_line = _leaf_text(tr("无法续读此书签，请选择其他存档。"))
	_load_error_line.tooltip_text = message
	_leaf_back.grab_focus()

func page_trace_context() -> Dictionary:
	return {"panel": "title" if mode == "title" else "casebook", "page": _current_page,
		"projected_3d": true, "turning": _turning, "opening": _request_pending,
		"page_texture": "frozen" if _turning else "live", "frozen_source_page": _frozen_page, "occlusion_evidence": "rendered-frame"}

## Read-only page navigation data for the isolated replay viewport. Frozen-page
## texture membership remains distinct from actual screen visibility evidence.
func capture_replay_state() -> Dictionary:
	_capture_page_state()
	return {"page": _current_page, "history": _page_history.duplicate(),
		"page_states": _page_states.duplicate(true), "turning": _turning,
		"frozen_source_page": _frozen_page, "bookmarks": _listed_bookmarks.duplicate(),
		"preferences": _replay_preferences.capture_data() if replay_mode else ReplayPreferences.capture(get_node_or_null("/root/GameSettings"))}

func restore_replay_state(state: Dictionary) -> void:
	var page := str(state.get("page", "cover" if mode == "title" else "index"))
	if replay_mode:
		_replay_bookmarks.assign(state.get("bookmarks", []))
		if state.has("preferences"): _replay_preferences.restore_data(state.preferences)
	_page_states = state.get("page_states", {}).duplicate(true)
	if page != _current_page: await _turn_to(page)
	_page_history.assign(state.get("history", []))
	_restore_page_state()
