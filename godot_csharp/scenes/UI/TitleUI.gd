extends Control
## Native page controls rendered on a physical book in the desk's 3D camera.
## Visual presentation only: emits the same ui_event dictionaries as before.
signal ui_event(event: Dictionary)
signal transition_finished(shown: bool)

const Sound = preload("res://scenes/Shared/Audio/Sound.gd")
const SETTINGS_SCENE = preload("res://scenes/UI/SettingsUI.tscn")
const Transition = preload("res://scenes/UI/UITransition.gd")
const BookProjection = preload("res://scenes/UI/BookProjection3D.gd")
const LeafTurn = preload("res://scenes/UI/BookLeafTurn3D.gd")
const BookSurfaceDetail = preload("res://scenes/UI/BookSurfaceDetail3D.gd")
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


var mode := "title"
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
var _open_amount := 0.0
var _current_page := "cover"
var _turning := false
var _turn_tween: Tween
var _book_leaf: PanelContainer
var _leaf_body: VBoxContainer
var _leaf_back: Button
var _leaf_next: Button
var _leaf_heading: Label
var _settings_instance: Node
var _file_dialog: FileDialog
var _load_error_line: RichTextLabel
var _page_motion = Transition.new()
var _reduce_motion := false
var _labels := {}
var _menu_input_enabled := false
var _request_pending := false
var _leaving := false

func _ready() -> void:
	_projection_surface.texture = _stage_viewport.get_texture()
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
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null:
		_reduce_motion = bool(settings.get_value("accessibility", "reduce_motion"))
		settings.setting_changed.connect(_on_setting_changed)
	_desk.set_reduce_motion(_reduce_motion)
	_page_motion.configure(self, self, _projection_surface, null, false, 1.0)
	_page_motion.completed.connect(_on_page_transition)
	_set_menu_input(false)
	Sound.music("main_theme", 2.5)
	transition_in.call_deferred()

func _resize_stage() -> void:
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
	_leaving = false
	if is_instance_valid(_settings_instance): _settings_instance.set_external_blocked(false)
	_request_pending = false
	_set_menu_input(false)
	var request_id: int = _page_motion.request(true, true, 0.24)
	var completed: bool = await _page_motion.wait_for(request_id)
	if completed and not _leaving:
		_set_menu_input(true)
		if _current_page == "cover": start_button.grab_focus()
		else: _leaf_back.grab_focus()
	return completed and not _leaving

func transition_out() -> bool:
	_leaving = true
	_set_menu_input(false)
	if is_instance_valid(_settings_instance): _settings_instance.set_external_blocked(true)
	var request_id: int = _page_motion.request(false, true, 0.20)
	return await _page_motion.wait_for(request_id)

func _on_page_transition(_request_id: int, shown: bool, cancelled: bool) -> void:
	if not cancelled: transition_finished.emit(shown)

func _on_setting_changed(section: String, key: String, value: Variant) -> void:
	if section == "accessibility" and key == "reduce_motion":
		_reduce_motion = bool(value)
		_desk.set_reduce_motion(_reduce_motion)
		if _reduce_motion:
			_page_motion.finish_current()
			if _turn_tween != null and _turn_tween.is_valid(): _turn_tween.custom_step(10.0)

func _set_menu_input(enabled: bool) -> void:
	_menu_input_enabled = enabled and not _leaving and not _request_pending and not _turning and _current_page == "cover"
	viewport.gui_disable_input = _leaving or _request_pending or _turning
	for button: Button in [start_button, continue_button, settings_button, quit_button]:
		button.disabled = not _menu_input_enabled

func _settings_active() -> bool:
	return _current_page == "settings"

func _exit_tree() -> void:
	if _turn_tween != null and _turn_tween.is_valid(): _turn_tween.kill()
	_page_motion.dispose()
	if is_instance_valid(_settings_instance): _settings_instance.begin_shutdown()

func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSLATION_CHANGED and is_node_ready(): _localize()

func _language() -> String:
	var locale := TranslationServer.get_locale()
	return "ja" if locale.begins_with("ja") else "en" if locale.begins_with("en") else "zh_CN"

func _localize() -> void:
	var texts: Array = TEXTS[_language()]
	title_label.theme_type_variation = TITLE_TYPES[_language()]
	title_label.text = texts[0]
	var buttons: Array[Button] = [start_button, continue_button, settings_button, quit_button]
	for index in buttons.size():
		_labels[buttons[index]] = texts[index + 1]
		_mark(buttons[index], buttons[index].has_focus())

func _process(delta: float) -> void:
	if not visible or not is_instance_valid(_book): return
	_projection.update(_projection_surface.get_local_mouse_position(), delta, _reduce_motion)

func _mark(button: Button, selected: bool) -> void:
	button.text = ("☞ " + str(_labels.get(button, button.text))) if selected else str(_labels.get(button, button.text))

func _hover(button: Button) -> void:
	if _menu_input_enabled and not button.disabled: button.grab_focus()

func _draw_rule() -> void:
	var color := get_theme_color("rule_strong", "Paper")
	rule.draw_rect(Rect2(0, 0, rule.size.x, 2), color)
	rule.draw_rect(Rect2(0, 5, rule.size.x, 1), color)

func open_settings() -> void:
	if not _leaving and not _request_pending and not _turning: _turn_to("settings")

func close_settings() -> void:
	if not _turning: _turn_to("cover")

func open_load_book() -> void:
	if not _leaving and not _request_pending and not _turning: _turn_to("load")

func _input(event: InputEvent) -> void:
	if not is_visible_in_tree() or not is_instance_valid(_book): return
	if event is InputEventMouse or event is InputEventKey or event is InputEventJoypadButton:
		if not _turning and not _leaving: _projection.forward(event)
		if not viewport.is_input_handled(): _unhandled_input(event)
		get_viewport().set_input_as_handled()

func _unhandled_input(event: InputEvent) -> void:
	if not event.is_pressed() or event.is_echo() or _turning or _leaving: return
	if _current_page != "cover" and not _request_pending:
		if event.is_action_pressed("cancel") or event.is_action_pressed("ui_cancel") or event.is_action_pressed("settings"):
			_turn_to("cover")
		return
	if not _menu_input_enabled: return
	if event.is_action_pressed("ui_accept") or event.is_action_pressed("dialogue_continue"):
		_request("start")
	elif event.is_action_pressed("settings"):
		open_settings()
	elif event.is_action_pressed("cancel") or event.is_action_pressed("ui_cancel"):
		_request("quit")

func _request(kind: String, path := "") -> void:
	if _turning or _leaving or _request_pending: return
	if kind == "start":
		if not _menu_input_enabled: return
		await _turn_to("start")
	_request_pending = true
	_set_menu_input(false)
	Sound.sfx("ui_click", -4.0)
	var request := {"kind": kind}
	if path != "": request.path = path
	ui_event.emit(request)

func project_button(button: Control) -> Vector2:
	return _projection_surface.get_global_transform_with_canvas() * _projection.project(button.get_global_rect().get_center())

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
	inside_title.theme = theme
	inside_title.theme_type_variation = &"BookHeading"
	inside_title.text = TEXTS[_language()][0]
	inside_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	inside_title.position = Vector2(54, 110)
	inside_title.size = Vector2(472, 54)
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
	if _turning or _current_page == page: return
	var previous := _current_page
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
	_set_menu_input(true)
	if page == "cover": settings_button.grab_focus()
	else: _leaf_back.grab_focus()

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
	column.add_child(heading)
	column.add_child(HSeparator.new())
	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	column.add_child(scroll)
	_leaf_body = VBoxContainer.new()
	_leaf_body.theme_type_variation = &"BookColumn"
	_leaf_body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(_leaf_body)
	_leaf_back = Button.new()
	_leaf_back.text = "☜ 封面"
	_leaf_back.theme_type_variation = &"TitleMenuButton"
	_leaf_back.pressed.connect(func(): _turn_to("cover"))
	var footer := HBoxContainer.new()
	column.add_child(footer)
	_leaf_back.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	footer.add_child(_leaf_back)
	_leaf_next = Button.new()
	_leaf_next.theme_type_variation = &"TitleMenuButton"
	_leaf_next.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_leaf_next.pressed.connect(func(): _turn_to("load" if _current_page == "settings" else "settings"))
	footer.add_child(_leaf_next)

func _show_leaf(page: String) -> void:
	_cover_root.get_node("Slip").visible = page == "cover"
	_cover_root.get_node("Cloth").visible = page == "cover"
	_cover_root.get_node("PaperGround").visible = page != "cover"
	_book_leaf.visible = page != "cover"
	if page == "cover": return
	_leaf_next.visible = page in ["settings", "load"]
	_leaf_next.text = "续读 ☞" if page == "settings" else "凡例 ☞"
	for child in _leaf_body.get_children():
		_leaf_body.remove_child(child)
		child.queue_free()
	_leaf_heading.text = "凡例与设置" if page == "settings" else "续读 · 书签" if page == "load" else "开始阅读"
	if page == "settings":
		if not is_instance_valid(_settings_instance):
			_settings_instance = SETTINGS_SCENE.instantiate()
			add_child(_settings_instance)
		_settings_instance.populate_page(_leaf_body)
	elif page == "load":
		var directory := DirAccess.open("user://saves")
		var found := false
		if directory != null:
			for file in directory.get_files():
				if not file.ends_with(".dvsave"): continue
				found = true
				var path := ProjectSettings.globalize_path("user://saves/" + file)
				_leaf_button(file.get_basename(), func(): _request("load", path))
		if not found: _leaf_text("尚无书签。请选择已有存档继续阅读。")
		_leaf_button("选择存档…", _choose_save)
	else:
		_leaf_text("案卷正在展开……")

func _leaf_text(text: String) -> RichTextLabel:
	var label := RichTextLabel.new()
	label.fit_content = true
	label.text = text
	_leaf_body.add_child(label)
	return label

func _leaf_button(text: String, action: Callable) -> void:
	var button := Button.new()
	button.text = text
	button.theme_type_variation = &"TitleMenuButton"
	button.pressed.connect(action)
	_leaf_body.add_child(button)

func _choose_save() -> void:
	if not is_instance_valid(_file_dialog):
		_file_dialog = FileDialog.new()
		_file_dialog.use_native_dialog = true
		_file_dialog.access = FileDialog.ACCESS_FILESYSTEM
		_file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
		_file_dialog.filters = PackedStringArray(["*.dvsave ; 游戏存档"])
		_file_dialog.file_selected.connect(func(path: String): _request("load", path))
		add_child(_file_dialog)
	_file_dialog.popup_centered_ratio(0.7)

func show_load_error(message: String) -> void:
	_request_pending = false
	_set_menu_input(true)
	if not is_instance_valid(_load_error_line):
		_load_error_line = _leaf_text("无法续读此书签，请选择其他存档。")
	_load_error_line.tooltip_text = message
	_leaf_back.grab_focus()
