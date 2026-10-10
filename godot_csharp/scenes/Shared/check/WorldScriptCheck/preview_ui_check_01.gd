extends SceneTree
var host: Control
var started := Time.get_ticks_msec()
var document_path := "res://scenes/Shared/Story/Examples/WorldScript/generated_world.json"
func _initialize() -> void:
	call_deferred("run")
func fail(message: String) -> void:
	print("FAIL ", message)
	quit(1)
func run() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--document="): document_path = argument.trim_prefix("--document=")
	var document: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(document_path))
	host = load("res://scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.tscn").instantiate()
	host.compiled_document = document_path
	root.add_child(host)
	await process_frame
	if not host.opened:
		fail("document did not open: " + host.last_error)
		return
	var loaded_ids: Array = []
	for declaration in document.chapters:
		var chapter := str(declaration.id)
		if host.engine.CurrentChapter() != chapter:
			fail("declaration chapter did not load")
			return
		for entity in declaration.entities:
			var id := str(entity.id)
			if not id.begins_with("world:") and not id.begins_with("chapter:"):
				id = "world:" + id if str(entity.scope) == "world" else "chapter:" + chapter + ":" + id
			if not loaded_ids.has(id): loaded_ids.append(id)
		if host.entity_names.keys().size() != loaded_ids.size():
			fail("preview exposes unloaded entities or omits declarations")
			return
		for id in loaded_ids:
			if not host.entity_names.has(id):
				fail("missing declared entity: " + str(id))
				return
		var declared_actors: Array = host.entities.filter(func(entity: Dictionary) -> bool: return str(entity.kind) == "character")
		var actor_controls: Array = host.room.get_children().filter(func(node: Node) -> bool: return node.has_meta("actor"))
		if actor_controls.size() != declared_actors.size():
			fail("actor controls are not declaration-driven")
			return
		var selected_actor: String = host._actor_id()
		for actor in declared_actors:
			host._select_actor(str(actor.id))
			if host._actor_id() != str(actor.id):
				fail("declared actor cannot be selected")
				return
			var local_targets: Array = host.entities.filter(func(entity: Dictionary) -> bool: return str(entity.kind) == "object" and str(entity.room) == str(actor.room))
			var target_controls: Array = host.room.get_children().filter(func(node: Node) -> bool: return node.has_meta("entity_id") and not node.has_meta("actor"))
			if target_controls.size() != local_targets.size():
				fail("target controls include another room or omit declared objects")
				return
			for target in local_targets:
				var bound: Array = target_controls.filter(func(node: Node) -> bool: return str(node.get_meta("entity_id")) == str(target.id))
				if bound.size() != 1 or str(bound[0].text) != str(target.name):
					fail("object control is not bound to its declaration")
					return
			if str(host.room.get_child(0).text) != str(host.entity_names[str(actor.room)]):
				fail("actor selection did not change the displayed room")
				return
		host._select_actor(selected_actor)
		var refresh: Button = host.get_node("Margin/Column/Actions/Refresh")
		refresh.pressed.emit()
		await process_frame
		var choices: VBoxContainer = host.get_node("Margin/Column/ChoiceScroll/Choices")
		if choices.get_child_count() == 0 or not choices.get_child(0) is Button:
			fail("no actual candidate button")
			return
		var chosen: Button = null
		for child in choices.get_children():
			if child is Button and child.tooltip_text.begins_with(chapter + ":"):
				chosen = child
				break
		if chosen == null:
			fail("missing chapter candidate")
			return
		print("UI CANDIDATE ", chapter, " ", chosen.tooltip_text)
		chosen.pressed.emit()
		while not host._idle():
			await process_frame
			if Time.get_ticks_msec() - started > 55000:
				fail("UI timed out")
				return
			if not host.presentation.is_empty():
				print("CORE PRESENTATION ", JSON.stringify(host.presentation))
				host.get_node("Margin/Column/Next").pressed.emit()
		if host.last_error != "":
			fail(host.last_error)
			return
		print("UI TRANSCRIPT ", chapter, " ", host.transcript.get_parsed_text())
		if host.engine.CanLoadNextChapter():
			host.get_node("Margin/Column/Chapter2").pressed.emit()
			await process_frame
	print("PASS automatic author-preview transport, declared entities and chapters, actual Core output; not UI acceptance")
	quit(0)
