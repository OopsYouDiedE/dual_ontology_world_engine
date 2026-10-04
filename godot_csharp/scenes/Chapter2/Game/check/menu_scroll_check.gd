extends SceneTree
var game: Node
var results := []
var output := "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/remake-chapter2/shared-runtime"
func _initialize():
	call_deferred("run")
func pause(seconds := 0.2):
	await create_timer(seconds).timeout
func key(code):
	var e := InputEventKey.new()
	e.keycode = code
	e.pressed = true
	root.push_input(e)
	e = InputEventKey.new()
	e.keycode = code
	e.pressed = false
	root.push_input(e)
func click(point):
	var m := InputEventMouseMotion.new()
	m.position = point
	root.push_input(m)
	var e := InputEventMouseButton.new()
	e.button_index = MOUSE_BUTTON_LEFT
	e.position = point
	e.pressed = true
	root.push_input(e)
	e = InputEventMouseButton.new()
	e.button_index = MOUSE_BUTTON_LEFT
	e.position = point
	e.pressed = false
	root.push_input(e)
func shot(name):
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(output + "/menu-track-" + name + ".png")
func buttons(node):
	var found := []
	if node is Button: found.append(node)
	for child in node.get_children(): found.append_array(buttons(child))
	return found
func inner_scroll():
	for child in game.modal_box.get_children():
		if child is ScrollContainer: return child
	return null
func metric(label, scroll):
	var bar: VScrollBar = scroll.get_v_scroll_bar()
	var entry := {"menu":label,"bar_visible":bar.is_visible_in_tree(),"bar_rect":str(bar.get_global_rect()),"bar_min_size":str(bar.get_combined_minimum_size()),"value":bar.value,"max_value":bar.max_value,"page":bar.page,"grabber_min_size":str(bar.get_theme_stylebox("grabber").get_minimum_size()),"scroll_rect":str(scroll.get_global_rect())}
	results.append(entry)
	print("SCROLL_METRIC ", JSON.stringify(entry))
func scroll_bottom(scroll):
	var bar: VScrollBar = scroll.get_v_scroll_bar()
	# Exercise the original VScrollBar's range; no button callback or hidden-control click.
	bar.value = bar.max_value
	await pause()
func run():
	DirAccess.make_dir_recursive_absolute(output)
	game = load("res://scenes/Chapter2/Game/Chapter2Game.tscn").instantiate()
	root.add_child(game)
	current_scene = game
	for i in range(150):
		await pause(0.25)
		if not game.busy and game.intro_done and game.modal_kind == "" and not game.dialogue.panel.visible and game.state.get("mode", "") == "main": break
		if game.dialogue.panel.visible: key(KEY_SPACE)
		elif game.modal_kind == "story_image": key(KEY_SPACE)
		elif game.modal_kind == "menu":
			var opts = buttons(game.modal_box)
			if not opts.is_empty(): click(opts[0].get_global_rect().get_center())
	print("START_IDLE ", JSON.stringify({"busy":game.busy,"intro_done":game.intro_done,"mode":game.state.get("mode", ""),"modal":game.modal_kind}))
	if game.busy or game.modal_kind != "" or game.dialogue.panel.visible:
		await shot("startup-failure")
		finish(1)
		return
	key(KEY_Q)
	await pause()
	var scroll = inner_scroll()
	if scroll == null:
		results.append({"error":"Q did not open original menu"})
		finish(1)
		return
	metric("Q before", scroll)
	await shot("q-before")
	await scroll_bottom(scroll)
	metric("Q bottom", scroll)
	await shot("q-bottom")
	var opts = buttons(scroll)
	var bottom: Button = opts[-1]
	var point = bottom.get_global_rect().get_center()
	results.append({"menu":"Q bottom target","text":bottom.text,"rect":str(bottom.get_global_rect()),"inside_scroll":scroll.get_global_rect().has_point(point),"inside_viewport":root.get_visible_rect().has_point(point)})
	click(point)
	await pause()
	results.append({"menu":"Q click result","input_visible":game.input_line.is_visible_in_tree(),"input_focused":game.input_line.has_focus(),"modal_closed":game.modal_kind == ""})
	await shot("q-input")
	game.input_line.release_focus()
	game._open_actions()
	await pause()
	var ret: Button = buttons(game.modal_box)[-1]
	click(ret.get_global_rect().get_center())
	await pause()
	results.append({"menu":"Q return","modal_closed":game.modal_kind == ""})
	var person = null
	for p in game.state.room.people:
		if person == null or p.actions.size() > person.actions.size(): person = p
	if person == null:
		results.append({"error":"no original room person"})
		finish(1)
		return
	game._open_person(String(person.name))
	await pause()
	scroll = inner_scroll()
	results.append({"person":person.name,"actions":person.actions.size()})
	metric("person before", scroll)
	await shot("person-before")
	await scroll_bottom(scroll)
	metric("person bottom", scroll)
	await shot("person-bottom")
	opts = buttons(scroll)
	bottom = opts[-1]
	point = bottom.get_global_rect().get_center()
	results.append({"menu":"person bottom target","text":bottom.text,"rect":str(bottom.get_global_rect()),"inside_scroll":scroll.get_global_rect().has_point(point),"inside_viewport":root.get_visible_rect().has_point(point)})
	click(point)
	await pause()
	results.append({"menu":"person click result","modal_kind":game.modal_kind,"busy":game.busy,"current_buttons":buttons(game.modal_box).map(func(b): return b.text) if is_instance_valid(game.modal_box) else []})
	await shot("person-click")
	key(KEY_ESCAPE)
	await pause()
	results.append({"menu":"person return","modal_closed":game.modal_kind == ""})
	finish(0)
func finish(code):
	var f := FileAccess.open(output + "/menu-track-metrics.json", FileAccess.WRITE)
	f.store_string(JSON.stringify(results, "\t"))
	print("MENU_SCROLL_FINISH ", code)
	quit(code)
