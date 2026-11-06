extends Node
## Two live native pages on the existing physical book; no flattened UI image.
const BookProjection = preload("res://scenes/components/world/BookProjection3D.gd")
const Leaf = preload("res://scenes/components/world/BookLeafTurn3D.gd")
const PageStack = preload("res://scenes/components/world/BookPageStack3D.gd")
const SettingsSpread = preload("res://scenes/UIpanel/BookSettingsSpread.gd")
const Transition = preload("res://scenes/UIpanel/UITransition.gd")
const MASK_SHADER = preload("res://assets/materials/manuscript_mask.gdshader")
const ROOT := "res://assets/ui/manuscript/"
const PAPER := "res://assets/ui/reference-paper.webp"
const FONTS := {
	"zh_CN": preload("res://assets/external/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf"),
	"ja": preload("res://assets/external/yujisyuku/YujiSyuku-Regular.ttf"),
	"en": preload("res://assets/external/Almendra/Almendra-Regular.tres")}
const OPEN_PAPER_CURVE := 0.32
const ENGLISH_TITLE_FONT = preload("res://assets/external/Almendra/Almendra-Bold.tres")
var host: Control
var left_view: SubViewport
var left_mask: SubViewport
var right_mask: SubViewport
var left_root: Control
var left_projection = BookProjection.new()
var illustration: TextureRect
var illustration_mask: TextureRect
var right_ink_mask: TextureRect
var hand: TextureRect
var hand_mask: TextureRect
var subtitle: Label
var ink_title: RichTextLabel
var ink_subtitle: Label
var ink_buttons: Array[Label] = []
var pointer_tween: Tween
var selected_button: Button
var pointer_base := Vector2.ZERO
var pointer_time := 0.0
var right_leaf: Node3D
var active_left := true
var title_ornament: TextureRect
var title_ornament_mask: TextureRect
var settings_spread: Variant
var _title_nodes: Array[Node] = []
var _title_masks: Array[Node] = []
var _left_ink_mask: TextureRect

func _viewport(label: String) -> SubViewport:
	var result := SubViewport.new()
	result.name = label
	result.size = Vector2i(580, 820)
	result.disable_3d = true
	result.transparent_bg = true
	result.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(result)
	return result

func _picture(parent: Node, texture: Texture2D, rect: Rect2) -> TextureRect:
	var item := TextureRect.new()
	item.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	item.texture = texture
	item.position = rect.position
	item.size = rect.size
	item.stretch_mode = TextureRect.STRETCH_SCALE
	item.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(item)
	return item

func _art(parent: Node, mask: Node, file: String, rect: Rect2, miniature := false) -> TextureRect:
	var texture: Texture2D = load(ROOT + file)
	var art := _picture(parent, texture, rect)
	var data := _picture(mask, texture, rect)
	var material := ShaderMaterial.new()
	material.shader = MASK_SHADER
	material.set_shader_parameter("miniature", miniature)
	material.set_shader_parameter("allow_gold", file != "manicule.png")
	data.material = material
	return art

func configure(owner_ui: Control) -> void:
	host = owner_ui
	left_view = _viewport("TitleLeftPage")
	left_mask = _viewport("TitleLeftMaterials")
	right_mask = _viewport("TitleRightMaterials")
	left_root = Control.new()
	left_root.size = Vector2(left_view.size)
	left_root.theme = host.theme
	left_view.add_child(left_root)
	_picture(left_root, load(PAPER), Rect2(0, 0, 580, 820))
	_art(left_root, left_mask, "border.png", Rect2(16, 25, 540, 770))
	title_ornament = _art(left_root, left_mask, "emblem.png", Rect2(90, 68, 174, 190))
	title_ornament_mask = left_mask.get_child(left_mask.get_child_count() - 1)
	# Preserve the original native controls and their already connected actions.
	host._slip.reparent(left_root)
	host._slip.material = null
	host._slip.position = Vector2.ZERO
	host._slip.size = Vector2(580, 820)
	host._slip.add_theme_stylebox_override("panel", StyleBoxEmpty.new())
	host._slip.get_node("Column").hide()
	var content := Control.new()
	content.mouse_filter = Control.MOUSE_FILTER_IGNORE
	host._slip.add_child(content)
	host.title_label.reparent(content)
	host.title_label.position = Vector2(263, 80)
	host.title_label.size = Vector2(280, 190)
	host.title_label.custom_minimum_size = Vector2.ZERO
	subtitle = Label.new()
	subtitle.position = Vector2(112, 270)
	subtitle.size = Vector2(410, 50)
	subtitle.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	subtitle.add_theme_color_override("font_color", Color("8a3324"))
	content.add_child(subtitle)
	ink_title = RichTextLabel.new()
	ink_title.position = host.title_label.position
	ink_title.size = host.title_label.size
	ink_title.scroll_active = false
	left_mask.add_child(ink_title)
	ink_subtitle = Label.new()
	ink_subtitle.position = subtitle.position
	ink_subtitle.size = subtitle.size
	ink_subtitle.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	ink_subtitle.add_theme_color_override("font_color", Color.GREEN)
	left_mask.add_child(ink_subtitle)
	var buttons: Array[Button] = [host.start_button, host.continue_button, host.settings_button, host.quit_button]
	for i in buttons.size():
		var button := buttons[i]
		button.reparent(content)
		button.position = Vector2(188, 364 + i * 77)
		button.size = Vector2(330, 58)
		button.theme_type_variation = &"TitleMenuButton"
		for state in ["normal", "hover", "pressed", "focus", "disabled"]:
			button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
		for state in ["font_color", "font_hover_color", "font_focus_color", "font_pressed_color"]:
			button.add_theme_color_override(state, Color("33261d"))
		var ink := Label.new()
		ink.position = button.position
		ink.size = button.size
		ink.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		ink.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		ink.add_theme_color_override("font_color", Color.GREEN)
		left_mask.add_child(ink)
		ink_buttons.append(ink)
		if i < 3:
			for target in [content, left_mask]:
				var line := ColorRect.new()
				line.position = Vector2(212, 430 + i * 77)
				line.size = Vector2(280, 1)
				line.color = Color("ad8745") if target == content else Color.RED
				line.mouse_filter = Control.MOUSE_FILTER_IGNORE
				target.add_child(line)
	hand = _art(content, left_mask, "manicule.png", Rect2(118, 378, 64, 32))
	hand_mask = left_mask.get_child(left_mask.get_child_count() - 1)
	host._cover_root.get_node("Cloth").hide()
	host._cover_root.get_node("PaperGround").show()
	illustration = _art(host._cover_root, right_mask, "miniature.png", Rect2(42, 85, 496, 650), true)
	illustration_mask = right_mask.get_child(right_mask.get_child_count() - 1)
	# Dynamic right-page glyph coverage is separate from the miniature material IDs.
	right_ink_mask = _picture(right_mask, host.viewport.get_texture(), Rect2(0, 0, 580, 820))
	var ink_material := ShaderMaterial.new()
	ink_material.shader = MASK_SHADER
	ink_material.set_shader_parameter("ink_only", true)
	right_ink_mask.material = ink_material
	right_ink_mask.hide()
	host._cover_root.move_child(illustration, 1)
	# The open title's left page is paper, not the closed book's rigid board.
	host._cover_leaf.configure(left_view.get_texture(), left_view.get_texture(), host.PAGE_SIZE)
	host._cover_leaf.set_surface_maps(host._paper_normal_map, host._paper_roughness_map, host._paper_normal_map, host._paper_roughness_map)
	host._cover_leaf.set_material_masks(left_mask.get_texture(), left_mask.get_texture())
	host._cover_leaf.set_progress(1.0)
	host._cover_leaf.rotation.z = 0.0
	host._cover_leaf.position.y = host.OPEN_PAGE_Y
	host._cover_leaf.leaf_material.set_shader_parameter("resting_curve", OPEN_PAPER_CURVE)
	host._book.get_node("Binding").scale.y = 0.72
	host._add_box("LeftPageBlock", Vector3(4.79, 0.175, 6.79), Vector3(-2.4, 0.184, 0), host._book.get_node("PageBlock").material_override)
	host._add_box("LeftLowerCover", Vector3(4.96, 0.065, 6.96), Vector3(-2.4, 0.065, 0), host._book.get_node("LowerCover").material_override)
	_curve_page_block(host._book.get_node("PageBlock"))
	_curve_page_block(host._book.get_node("LeftPageBlock"))
	# Settled right page uses the same closed, subdivided mesh and PBR shader as turns.
	host._right_surface.hide()
	right_leaf = Leaf.new()
	host._book.add_child(right_leaf)
	right_leaf.position.y = host.OPEN_PAGE_Y
	right_leaf.configure(host.viewport.get_texture(), host.viewport.get_texture(), host.PAGE_SIZE)
	right_leaf.set_surface_maps(host._paper_normal_map, host._paper_roughness_map, host._paper_normal_map, host._paper_roughness_map)
	right_leaf.set_material_masks(right_mask.get_texture(), right_mask.get_texture())
	right_leaf.leaf_material.set_shader_parameter("resting_curve", OPEN_PAPER_CURVE)
	host._book.position.x = host._desk.BOOK_READING_OFFSET
	var ribbon := host._desk.get_node("DeskGeometry/Ribbon") as MeshInstance3D
	ribbon.global_position = host._book.to_global(Vector3(0.0, 0.018, 3.95))
	var ribbon_material := ShaderMaterial.new()
	ribbon_material.shader = preload("res://assets/materials/manuscript_ribbon.gdshader")
	ribbon.material_override = ribbon_material
	host._open_amount = 1.0
	host._desk.title_spread = true
	host._desk.set_open_amount(1.0)
	left_projection.configure(host._desk, host._projection_surface, left_view, host._book)
	left_projection.resting_curve = OPEN_PAPER_CURVE
	host._projection.resting_curve = OPEN_PAPER_CURVE
	left_projection.page_origin_x = -host.PAGE_SIZE.x
	left_projection.paper_y = host.OPEN_PAGE_Y + 0.006
	host._projection.paper_y = host.OPEN_PAGE_Y + 0.006
	for child in left_root.get_children():
		if child != left_root.get_child(0): _title_nodes.append(child)
	for child in left_mask.get_children(): _title_masks.append(child)
	_left_ink_mask = _picture(left_mask, left_view.get_texture(), Rect2(0, 0, 580, 820))
	var left_ink_material := ShaderMaterial.new()
	left_ink_material.shader = MASK_SHADER
	left_ink_material.set_shader_parameter("ink_only", true)
	_left_ink_mask.material = left_ink_material
	_left_ink_mask.hide()
	settings_spread = SettingsSpread.new()
	settings_spread.configure(host, left_root, host._cover_root)

func localize(language: String) -> void:
	var font: Font = FONTS[language]
	var ornament: Texture2D = load(ROOT + ("initial-b-script.png" if language == "en" else "emblem.png"))
	var ornament_rect := Rect2(90, 68, 174, 190) if language == "en" else Rect2(100, 86, 150, 150)
	for artwork in [title_ornament, title_ornament_mask]:
		artwork.texture = ornament
		artwork.position = ornament_rect.position
		artwork.size = ornament_rect.size
	var title: String = {"zh_CN": "安眠者\n有福", "ja": "眠る者に\n福あれ", "en": "lessed Are\nThose Who Sleep"}[language]
	for label in [host.title_label, ink_title]:
		label.clear()
		label.add_theme_font_override("normal_font", ENGLISH_TITLE_FONT if language == "en" else font)
		label.add_theme_font_size_override("normal_font_size", 54 if language != "en" else 44)
		label.add_theme_color_override("default_color", Color("33261d") if label == host.title_label else Color.GREEN)
		label.add_text(title)
	subtitle.text = "崇正格致大本堂纪事" if language == "zh_CN" else ""
	ink_subtitle.text = subtitle.text
	for label in [subtitle, ink_subtitle]:
		label.add_theme_font_override("font", font)
		label.add_theme_font_size_override("font_size", 23)
	var buttons: Array[Button] = [host.start_button, host.continue_button, host.settings_button, host.quit_button]
	for i in buttons.size():
		buttons[i].add_theme_font_override("font", font)
		buttons[i].add_theme_font_size_override("font_size", 32)
		ink_buttons[i].add_theme_font_override("font", font)
		ink_buttons[i].add_theme_font_size_override("font_size", 32)
		ink_buttons[i].text = buttons[i].text
	if settings_spread != null: settings_spread.localize()

func mark(button: Button, selected: bool) -> void:
	if not selected: return
	selected_button = button
	pointer_time = 0.0
	var destination := Vector2(118, button.position.y + 13)
	if pointer_tween != null and pointer_tween.is_valid(): pointer_tween.kill()
	if host._reduce_motion:
		pointer_base = destination
	else:
		pointer_tween = create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)
		pointer_tween.tween_property(self, "pointer_base", destination, 0.18)

func _process(delta: float) -> void:
	if Engine.is_editor_hint():
		return
	if hand == null: return
	var buttons: Array[Button] = [host.start_button, host.continue_button, host.settings_button, host.quit_button]
	for i in buttons.size():
		if ink_buttons[i].text != buttons[i].text: ink_buttons[i].text = buttons[i].text
	pointer_time += delta
	var nudge := 0.0 if host._reduce_motion else sin(pointer_time * 2.0) * 2.0
	hand.position = pointer_base + Vector2(nudge, 0)
	hand_mask.position = hand.position

func forward(event: InputEvent) -> bool:
	if host._current_page.get_slice("@", 0) == "settings" and (event.is_action_pressed("ui_focus_next") or event.is_action_pressed("ui_focus_prev")):
		var current_view: SubViewport = left_view if active_left else host.viewport
		var root: Control = settings_spread.left_root if active_left else settings_spread.right_root
		var controls: Array[Control] = Transition.focusable_controls(root)
		var focused := current_view.gui_get_focus_owner()
		var index := controls.find(focused)
		var backwards := event.is_action_pressed("ui_focus_prev")
		if (backwards and index == 0) or (not backwards and index == controls.size() - 1):
			var other_root: Control = settings_spread.right_root if active_left else settings_spread.left_root
			var others: Array[Control] = Transition.focusable_controls(other_root)
			if not others.is_empty():
				if focused != null: focused.release_focus()
				active_left = not active_left
				others[-1 if backwards else 0].grab_focus()
				return true
	if event is InputEventMouse:
		var screen: Vector2 = host._projection_surface.get_global_transform_with_canvas().affine_inverse() * event.position
		var left_pixel: Vector2 = left_projection.unproject(screen)
		active_left = Rect2(Vector2.ZERO, Vector2(left_view.size)).has_point(left_pixel)
	return left_projection.forward(event) if active_left else host._projection.forward(event)

func show_right(page: String) -> void:
	var settings_page := page.get_slice("@", 0) == "settings"
	for node in _title_nodes: node.visible = not settings_page
	for node in _title_masks: node.visible = not settings_page
	_left_ink_mask.visible = settings_page
	if settings_page: settings_spread.show_pair(1 if page == "settings@1" else 0)
	else: settings_spread.hide()
	illustration.visible = page == "cover"
	illustration_mask.visible = page == "cover"
	right_ink_mask.visible = page != "cover"
	host._book_leaf.visible = page != "cover" and not settings_page
	if page != "cover" and not settings_page: host._show_leaf(page)
	host._slip.visible = not settings_page
	host._cover_root.get_node("Cloth").hide()
	host._cover_root.get_node("PaperGround").show()

func turn_to(page: String) -> void:
	var previous: String = host._current_page
	var order := ["cover", "settings", "settings@1", "load"]
	var reverse := order.find(page) < order.find(previous)
	host._capture_page_state()
	host._turning = true
	host._set_menu_input(false)
	# Freeze both visible leaves before changing native Controls. No new spread
	# can flash underneath the old page while its layout and masks are updating.
	var old_left := ImageTexture.create_from_image(left_view.get_texture().get_image())
	var old_right := ImageTexture.create_from_image(host.viewport.get_texture().get_image())
	var old_left_mask := ImageTexture.create_from_image(left_mask.get_texture().get_image())
	var old_right_mask := ImageTexture.create_from_image(right_mask.get_texture().get_image())
	host._cover_leaf.set_textures(old_left, old_left)
	host._cover_leaf.set_material_masks(old_left_mask, old_left_mask)
	right_leaf.set_textures(old_right, old_right)
	right_leaf.set_material_masks(old_right_mask, old_right_mask)
	host._current_page = page
	show_right(page)
	await get_tree().process_frame
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	if not is_inside_tree(): return
	var new_left := ImageTexture.create_from_image(left_view.get_texture().get_image())
	var new_right := ImageTexture.create_from_image(host.viewport.get_texture().get_image())
	var new_left_mask := ImageTexture.create_from_image(left_mask.get_texture().get_image())
	var new_right_mask := ImageTexture.create_from_image(right_mask.get_texture().get_image())
	var leaf := Leaf.new()
	host._book.add_child(leaf)
	leaf.position.y = host.OPEN_PAGE_Y + 0.014
	leaf.configure(new_right if reverse else old_right, old_left if reverse else new_left, host.PAGE_SIZE, reverse)
	leaf.leaf_material.set_shader_parameter("resting_curve", OPEN_PAPER_CURVE)
	leaf.set_surface_maps(host._paper_normal_map, host._paper_roughness_map, host._paper_normal_map, host._paper_roughness_map)
	leaf.set_material_masks(new_right_mask if reverse else old_right_mask, old_left_mask if reverse else new_left_mask)
	# Forward: old right is the front, next left is its back. Backward is the
	# inverse. The other exposed page remains stationary until it is covered.
	if reverse:
		host._cover_leaf.set_textures(left_view.get_texture(), left_view.get_texture())
		host._cover_leaf.set_material_masks(left_mask.get_texture(), left_mask.get_texture())
	else:
		right_leaf.set_textures(host.viewport.get_texture(), host.viewport.get_texture())
		right_leaf.set_material_masks(right_mask.get_texture(), right_mask.get_texture())
	if not host._reduce_motion:
		host._turn_tween = create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		host._turn_tween.tween_method(_turn_progress.bind(leaf), 0.0, 1.0, 0.95)
		await host._turn_tween.finished
		host._turn_tween = null
	host._desk.set_turn_lift(0.0)
	host._cover_leaf.set_textures(left_view.get_texture(), left_view.get_texture())
	host._cover_leaf.set_material_masks(left_mask.get_texture(), left_mask.get_texture())
	right_leaf.set_textures(host.viewport.get_texture(), host.viewport.get_texture())
	right_leaf.set_material_masks(right_mask.get_texture(), right_mask.get_texture())
	leaf.queue_free()
	host._turning = false
	host._set_menu_input(true)
	active_left = page != "load"
	if page.get_slice("@", 0) == "settings": settings_spread.restore_focus()
	else: await host._restore_page_state()

func _turn_progress(value: float, leaf: Node3D) -> void:
	leaf.set_progress(value)
	host._desk.set_turn_lift(sin(value * PI))

func request_adjacent_page(direction: int) -> bool:
	var order := ["cover", "settings", "settings@1", "load"]
	var current := order.find(host._current_page)
	if current < 0: return false
	var target := clampi(current + direction, 0, order.size() - 1)
	if target != current:
		host._page_history.clear()
		host._turn_to(order[target])
	return true

func _curve_page_block(block: MeshInstance3D) -> void:
	block.mesh = PageStack.build(Vector3(4.79, 0.175, 6.79), OPEN_PAPER_CURVE)
	block.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var material := ShaderMaterial.new()
	material.shader = preload("res://assets/materials/book_page_stack.gdshader")
	block.material_override = material
