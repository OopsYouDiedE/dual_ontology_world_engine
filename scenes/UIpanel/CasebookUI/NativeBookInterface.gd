extends Node
## Presentation adapter for the formal CharacterSessionHost. All actions use its existing gateway.
const BookScene := preload("res://scenes/UIpanel/CasebookUI.tscn")
const HudScene := preload("res://scenes/UIpanel/FieldHUD.tscn")
const MenuScene := preload("res://scenes/UIpanel/InteractionMenu.tscn")
const QueueScene := preload("res://scenes/UIpanel/FieldHUD/CurrentActionUI.tscn")
const DialogueScene := preload("res://scenes/UIpanel/DialogueUI.tscn")
const Archive := preload("res://scenes/UIpanel/CasebookUI/CasebookData.gd")
var host: Node3D
var book: CanvasLayer
var hud: CanvasLayer
var menu: Control
var queue: Control
var dialogue: CanvasLayer
var archive: RefCounted = Archive.new()
var _layer: CanvasLayer
var _seconds := 0.0
var _game_seconds := 0.0
var _last_room := ""
var _known_rooms: Dictionary = {}
var _known_connections: Array = []
var _menu_actions: Dictionary = {}
var _seen_lines: Dictionary = {}
var _seen_receipts: Dictionary = {}
var _was_paused := false
var _reading := false
var _modal_active := false
var _inspection_open := false
var _last_feedback := ""
var _last_save_feedback := ""
var _pointer := Vector2.ZERO
var _pointer_down := -1.0
var _press_consumed := false
var _native_save_path := "user://character-session.dvsave"
var _autosave_requested := false
var _hover_target := ""
var _scene_thumbnail: Image

func configure(session_host: Node3D, restored: Dictionary = {}) -> void:
	host = session_host
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--dv-save-slot="):
			_native_save_path = "user://sessions/" + argument.trim_prefix("--dv-save-slot=") + "/character-session.dvsave"
	archive.viewer = str(host.definition.viewer)
	if restored.get("casebook") is Dictionary: archive.restore_data(restored.casebook, archive.viewer)
	var knowledge: Dictionary = restored.get("ui_knowledge", {})
	_known_rooms = knowledge.get("rooms", {}).duplicate(true)
	_known_connections = knowledge.get("connections", []).duplicate(true)
	hud = HudScene.instantiate()
	add_child(hud)
	hud.book_open_requested.connect(func(): _open_book("B01"))
	hud.settings_requested.connect(func(): _open_book("B08"))
	hud.perception_inspect_requested.connect(_inspect_perception)
	hud.action_cancelled.connect(func(_id: String): host.interface_command({"command":"cancel"}))
	_layer = CanvasLayer.new()
	_layer.layer = 16
	add_child(_layer)
	menu = MenuScene.instantiate()
	_layer.add_child(menu)
	menu.action_selected.connect(_submit_action)
	menu.blocked_feedback.connect(hud.set_hint)
	menu.cost_preview_requested.connect(hud.show_cost_preview)
	menu.cost_preview_cleared.connect(hud.hide_cost_preview)
	menu.pocket_item_used.connect(func(_target: String, _item: String): hud.set_hint("当前物品没有可执行操作"))
	menu.word_magic_applied.connect(func(_target: String, _formula: String): hud.set_hint("当前没有可施加的言灵"))
	queue = QueueScene.instantiate()
	_layer.add_child(queue)
	queue.action_cancel_requested.connect(func(_id: String): host.interface_command({"command":"cancel"}))
	dialogue = DialogueScene.instantiate()
	add_child(dialogue)
	dialogue.live_advance_requested.connect(func(_line: Dictionary): host.interface_command({"command":"continue"}))
	dialogue.inspection_closed.connect(func(_record: Dictionary):
		_inspection_open = false
		var destination: Vector2 = hud.hud_settings_button.get_global_rect().get_center()
		dialogue.show_archive_flight(Vector2.ZERO, destination)
	)
	dialogue.evidence_presented.connect(_present_evidence)
	book = BookScene.instantiate()
	book.archive = archive
	add_child(book)
	book.ui_event.connect(_on_book_event)
	book.reading_changed.connect(_on_book_reading)
	host.dialogue_layer.visible = false
	if is_instance_valid(host.order_panel):
		host.order_panel.reparent(_layer)
		if host.order_panel.has_signal("validation_requested"):
			host.order_panel.validation_requested.connect(func(order: Array[String]):
				var result: String = host.bridge.CheckActionOrder(JSON.stringify(order))
				host.order_panel.set_validation_result(result)
			)
		host.order_panel.visibility_changed.connect(_sync_modal)
	set_process(true)
	set_process_input(true)

func export_knowledge() -> Dictionary:
	return {"rooms":_known_rooms.duplicate(true), "connections":_known_connections.duplicate(true)}

func _snapshot() -> Dictionary:
	var room: String = host.streaming.RoomAt(host.body.global_position)
	var room_name := _room_name(room)
	var minutes := _game_seconds / 60.0
	var viewer := str(host.definition.viewer)
	var projection: Dictionary = host.bridge.Snapshot()
	var personal: Dictionary = {"id":viewer, "name":_actor_name(viewer), "observations":[]}
	var held: Array = []
	if is_instance_valid(host.props):
		var props: Dictionary = host.props.observation()
		for id: String in props:
			var prop: Dictionary = props[id]
			if not bool(prop.get("visible",false)) or not bool(prop.get("carried",false)) or str(prop.get("holder","")) != viewer: continue
			var label := _fixture_name(id)
			if not label.is_empty(): held.append({"id":id, "title":label, "count":1, "ownership":"carried", "source":"手中持物"})
	for entry: Dictionary in projection.get("knowledge", []):
		var key: Dictionary = entry.get("key", {})
		var fact: Dictionary = entry.get("fact", {})
		var value: Variant = fact.get("Value")
		if str(key.get("Object","")) != viewer or str(key.get("Facet","")) != "phys" or not bool(fact.get("Known",false)): continue
		if value is String and not value.strip_edges().is_empty():
			personal.observations.append({"title":value + (" · 待核" if bool(fact.get("Conflicted",false)) else ""), "source":"自身记录"})
	var native_save := {"available":FileAccess.file_exists(_native_save_path), "native":true, "path":_native_save_path, "title":"最近的记录", "modified":FileAccess.get_modified_time(_native_save_path)}
	return {"viewer":viewer, "self":personal, "needs":projection.get("needs",{}), "inventory":{"items":held},
		"hud":{"room":room_name,"time":minutes}, "game_time":minutes, "logical_location":room_name,
		"current_room":room, "known_rooms":_known_rooms, "known_connections":_known_connections,
		"native_save":native_save, "allow_save":true, "casebook":{"viewer":str(host.definition.viewer)}}

func _room_name(id: String) -> String:
	var authored: Dictionary = host.definition.get("room_labels", {})
	if authored.has(id): return str(authored[id])
	var rooms: Variant = host.presentation_map.get("rooms", [])
	if rooms is Dictionary:
		var info: Variant = rooms.get(id, {})
		if info is Dictionary:
			var label := str(info.get("label",info.get("name", "")))
			if not label.is_empty(): return label
	elif rooms is Array:
		for info in rooms:
			if info is Dictionary and str(info.get("id",info.get("key",""))) == id:
				return str(info.get("label",info.get("name","")))
	return "学院" if id.is_empty() else "当前房间"

func _actor_name(id: String) -> String:
	for actor: Dictionary in host.definition.get("actors", []):
		if str(actor.get("id","")) == id: return str(actor.get("name", "我" if id == str(host.definition.viewer) else "眼前的人"))
	return "我" if id == str(host.definition.viewer) else "眼前的人"

func _target_name(identity: String) -> String:
	if identity == "personal": return "自己"
	if identity.begins_with("actor:"): return _actor_name(identity.trim_prefix("actor:"))
	var id := identity.trim_prefix("object:")
	var label := _fixture_name(id)
	return label if not label.is_empty() else "可交互物件"

func _fixture_name(id: String) -> String:
	for fixture: Dictionary in host.definition.get("fixtures", []):
		if str(fixture.get("id","")) == id and fixture.has("label"): return str(fixture.label)
	return ""

func _process(delta: float) -> void:
	if not is_instance_valid(host) or not host.ready_for_play: return
	if _pointer_down >= 0 and not _press_consumed and not book.is_active():
		if Time.get_ticks_msec() * 0.001 - _pointer_down >= float(_setting("controls","hold_threshold",0.45)):
			_press_consumed = true
			_show_actions(_pointer, true)
	_seconds += delta
	if _seconds < 0.18: return
	_seconds = 0
	var snapshot := _snapshot()
	var room := str(snapshot.current_room)
	if not _known_rooms.has(room): _known_rooms[room] = {"id":room,"label":str(snapshot.hud.room),"visited":true}
	if not _last_room.is_empty() and room != _last_room:
		_known_connections.append({"from_room":_last_room,"to_room":room,"passage_observed":true})
		if str(_setting("gameplay","autosave_policy","chapter")) == "scene": _autosave_requested = true
	_last_room = room
	hud.apply_snapshot(snapshot)
	archive.apply_snapshot(snapshot)
	_update_queue()
	_update_dialogue()
	if not book.is_active() and not _inspection_open: _update_hover()
	if _autosave_requested and host.command.is_empty() and host.conversation.is_empty() and not book.is_active():
		_autosave_requested = false
		host.save_game()
	if host.checkpoint_message.text != _last_save_feedback:
		_last_save_feedback = host.checkpoint_message.text
		if not _last_save_feedback.is_empty(): hud.set_hint(_last_save_feedback)

func _update_queue() -> void:
	var current: Dictionary = {}
	var progress: Dictionary = host.bridge.WorkProgress(str(host.definition.viewer))
	if not host.command.is_empty():
		current = {"id":str(host.command.get("action","")), "label":str(host.command.get("declaration",{}).get("label","行动中")), "progress":float(progress.get("progress",-1))}
	var waiting: Array = []
	for id in host.order_queue:
		waiting.append({"id":id,"label":_action_name(str(id))})
	if not host.pending_action.is_empty(): waiting.append({"id":host.pending_action,"label":_action_name(host.pending_action)})
	var point: Vector2 = host.camera.unproject_position(host.body.global_position + Vector3(0,2.15,0))
	queue.apply_queue_snapshot({"current_action":current,"queued_actions":waiting,"screen_position":point})

func _action_name(id: String) -> String:
	for action: Dictionary in host.actions:
		if str(action.get("id","")) == id: return str(action.get("label","行动中"))
	return "行动中"

func _update_dialogue() -> void:
	var view: Dictionary = host.bridge.Conversation()
	if not view.is_empty() and str(view.get("Phase","")) == "speaking":
		_inspection_open = false
		var line: Dictionary = view.get("Line", {}).duplicate(true)
		line["id"] = "conversation:%s:%s" % [view.get("Id",0), view.get("LineIndex",0)]
		dialogue.show_live_line(line)
		if not _seen_lines.has(line.id):
			_seen_lines[line.id] = true
			archive.record_card({"id":line.id,"category":"testimony","certainty":"subjective_statement", "title":str(line.get("Label","证言")),
				"text":str(line.get("Text","")), "speaker_name":str(line.get("Label","")), "speaker":str(line.get("Speaker","")),
				"source":"当面对话", "source_type":"heard", "game_time":float(host.state.get("game_seconds",0.0))/60.0, "place":_room_name(_last_room)})
	elif not _inspection_open:
		dialogue.close_live_line()

func on_update(update: Dictionary) -> void:
	_game_seconds = float(update.get("game_seconds",_game_seconds))
	for receipt: Dictionary in update.get("receipts", []):
		if str(receipt.get("Actor","")) != str(host.definition.viewer) or not bool(receipt.get("Committed",false)): continue
		var id := "action:%s:%s" % [receipt.get("Execution",receipt.get("ExecutionId",0)), receipt.get("Action","")]
		if _seen_receipts.has(id): continue
		_seen_receipts[id] = true
		for action: Dictionary in host.actions:
			if str(action.id) != str(receipt.get("Action","")): continue
			if not action.has("completion_text") and not action.has("completion_lines"): continue
			var text: String = host.bridge.CompletionText(str(action.id))
			if text.strip_edges().is_empty(): continue
			var motor: Dictionary = action.get("motor", {})
			var observed := motor.has("observe_fixture") or motor.has("observe_actor") or motor.has("observe_group")
			var record := {"id":id,"category":"evidence" if observed else "records","certainty":"observation" if observed else "action_result", "title":str(action.get("label","检视")), "text":text,
				"source":"亲见" if observed else "行动记录", "source_type":"witnessed" if observed else "self_record", "game_time":float(update.get("game_seconds",0.0))/60.0,"place":_room_name(_last_room)}
			archive.record_card(record)
			_inspection_open = true
			dialogue.show_inspection(record)
			if str(_setting("gameplay","autosave_policy","chapter")) == "event": _autosave_requested = true
	# Only observer-delivered sound records are accepted. No off-room actors are inferred.
	for perception in update.get("perceptions", []):
		if perception is Dictionary and str(perception.get("viewer",host.definition.viewer)) == str(host.definition.viewer): hud.present_perception(perception)

func _inspect_perception(record: Dictionary) -> void:
	_inspection_open = true
	dialogue.show_inspection(record)
	var copy := record.duplicate(true)
	copy["text"] = str(record.get("text",record.get("body",record.get("description",""))))
	copy["title"] = str(record.get("title",record.get("label",record.get("name","现场记录"))))
	copy["category"] = "records"
	copy["certainty"] = "subjective_statement" if str(copy.get("kind","")) in ["heard","sound"] else "observation"
	archive.record_card(copy)

func _open_book(page: String) -> void:
	if not host.ready_for_play: return
	_pointer_down = -1.0
	_press_consumed = true
	if is_instance_valid(host.order_panel): host.order_panel.hide()
	if book.is_active():
		book.open_section(page)
		return
	menu.close_menu()
	hud.hide_hover()
	_scene_thumbnail = get_viewport().get_texture().get_image()
	book.configure(_snapshot(), archive)
	book.open(page)

func _on_book_reading(active: bool) -> void:
	_reading = active
	_sync_modal()

func is_modal_active() -> bool:
	return _modal_active

func _sync_modal() -> void:
	var active: bool = _reading or (is_instance_valid(host.order_panel) and host.order_panel.visible)
	if active == _modal_active: return
	_modal_active = active
	_pointer_down = -1.0
	_press_consumed = true
	if active:
		_was_paused = host.player_paused
		host.interface_command({"command":"pause", "paused":true})
		menu.close_menu()
	else:
		host.interface_command({"command":"pause", "paused":_was_paused})
	hud.set_overlay_blocked(active)
	queue.set_overlay_blocked(active)
	dialogue.set_modal_blocked(active)
	dialogue.visible = not active

func _show_reasoning() -> bool:
	if is_instance_valid(host.order_panel) and is_instance_valid(host.order_button) and host.order_button.visible:
		host.order_panel.show()
		_sync_modal()
		return true
	return false

func _on_book_event(event: Dictionary) -> void:
	match str(event.get("kind","")):
		"bookmark_save", "bookmark_overwrite":
			if str(event.get("kind","")) == "bookmark_save" and FileAccess.file_exists(_native_save_path):
				book.book._on_page_event({"kind":"confirm", "title":"覆盖最近的书签？", "confirm_text":"覆盖", "action":{"kind":"bookmark_overwrite", "path":_native_save_path, "native":true}})
				return
			host.save_game()
			_write_save_metadata()
			book.configure(_snapshot(),archive)
			book.show_bookmark_result(host.checkpoint_message.text, host.checkpoint_message.text == "已保存")
		"bookmark_delete":
			var path := str(event.get("path",""))
			if path != _native_save_path: book.show_bookmark_result("书签路径不匹配"); return
			var error := DirAccess.remove_absolute(ProjectSettings.globalize_path(path))
			book.configure(_snapshot(),archive)
			book.show_bookmark_result("已删除" if error == OK else "未能删除", error == OK)
		"bookmark_load": _return_to_title(true)
		"return_title": _return_to_title(false)
		"open_reasoning":
			var available := _show_reasoning()
			book.close()
			if not available: hud.set_hint("尚无可推演的现场")
		"convention_signed", "convention_opinion": book.show_bookmark_result("当前没有可签署的公约")
		_: pass

func _write_save_metadata() -> void:
	if host.checkpoint_message.text != "已保存": return
	var screenshot := _native_save_path + ".png"
	if _scene_thumbnail != null and not _scene_thumbnail.is_empty(): _scene_thumbnail.save_png(screenshot)
	var metadata := {"title":"最近的记录","room":_room_name(_last_room),"game_time":float(host.state.get("game_seconds",0))/60.0,"screenshot":screenshot}
	var file := FileAccess.open(_native_save_path + ".meta.json", FileAccess.WRITE)
	if file != null: file.store_string(JSON.stringify(metadata)); file.close()

func _return_to_title(load_saved: bool) -> void:
	var main: Node = host.get_parent()
	book.begin_shutdown()
	main.return_native_title.call_deferred(load_saved)

func _show_actions(point: Vector2, use_pointer: bool) -> void:
	if book.is_active(): return
	host.refresh_menu(point, use_pointer)
	var options: Array = []
	_menu_actions.clear()
	var anchor := point
	var target := "现场"
	for index in range(host.menu.item_count):
		if host.menu.is_item_separator(index): continue
		var id: int = host.menu.get_item_id(index)
		var action_id := "native:%d" % id
		_menu_actions[action_id] = id
		var option := {"id":action_id,"label":host.menu.get_item_text(index),"available":not host.menu.is_item_disabled(index)}
		if id < host.actions.size():
			var declaration: Dictionary = host.actions[id]
			if options.is_empty():
				anchor = host.camera.unproject_position(host.action_context_position(declaration))
				target = _target_name(host.action_context_identity(declaration))
			if declaration.has("duration_seconds"): option["cost"] = {"time":float(declaration.duration_seconds) / 60.0}
		elif options.is_empty():
			var conversations: Array = host.definition.get("conversations", [])
			var conversation_index: int = id - host.actions.size()
			if conversation_index >= 0 and conversation_index < conversations.size():
				var actor_id := str(conversations[conversation_index].get("target",""))
				target = _actor_name(actor_id)
				var actor: Variant = host.autonomous_actors.get(actor_id)
				if actor != null: anchor = host.camera.unproject_position(actor.body.global_position)
		options.append(option)
	if options.is_empty(): return
	menu.open_for_target({"site":"native_current","target_name":target,"options":options,"screen_position":anchor},anchor)

func _submit_action(id: String, _target: String) -> void:
	if not _menu_actions.has(id): return
	var index := int(_menu_actions[id])
	# select_action rechecks availability and delegates authoritative consumption to Core.
	host.select_action(index)
	_inspection_open = false
	dialogue.close_live_line()
	menu.close_menu()

func _present_evidence(id: String, _context: Dictionary) -> void:
	# Only an actual current presentation action may be submitted.
	for action: Dictionary in host.actions:
		if str(action.get("evidence_id","")) == id and host.action_in_context(action):
			host.interface_command({"command":"action","action_id":str(action.id)})
			return
	hud.set_hint("此刻无法出示这份证物")

func _update_hover() -> void:
	if menu.is_menu_open(): return
	var pointer := get_viewport().get_mouse_position()
	var best := 70.0
	var label := ""
	for action: Dictionary in host.actions:
		if not host.action_in_context(action): continue
		var point: Vector3 = host.action_context_position(action)
		if host.camera.is_position_behind(point): continue
		var distance: float = host.camera.unproject_position(point).distance_to(pointer)
		if distance < best:
			best = distance
			label = str(action.get("target_label",action.get("label","")))
	if label.is_empty(): hud.hide_hover()
	else: hud.show_hover(label, pointer)

func _input(event: InputEvent) -> void:
	if not is_instance_valid(host) or not host.ready_for_play: return
	if _modal_active and event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and not event.pressed:
		_pointer_down = -1.0
	if event.is_action_pressed("notebook"):
		if book.is_active(): book.close()
		else: _open_book("B01")
		get_viewport().set_input_as_handled()
		return
	if event.is_action_pressed("settings"):
		_open_book("B08")
		get_viewport().set_input_as_handled()
		return
	if event.is_action_pressed("campus_map"):
		_open_book("B03")
		get_viewport().set_input_as_handled()
		return
	if book.is_active(): return
	if is_instance_valid(host.order_panel) and host.order_panel.visible: return
	if event.is_action_pressed("manuscript"):
		if not _show_reasoning(): _open_book("B05")
		get_viewport().set_input_as_handled()
		return
	if event.is_action_pressed("interact") and host.conversation.is_empty():
		_show_actions(get_viewport().get_visible_rect().size*0.5,false)
		get_viewport().set_input_as_handled()
		return
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_RIGHT and event.pressed:
		if menu.is_menu_open(): menu.close_menu()
		elif _inspection_open: dialogue.close_live_line(); _inspection_open = false
		get_viewport().set_input_as_handled()
		return
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			if get_viewport().gui_get_hovered_control() != null: return
			_pointer = event.position
			_pointer_down = Time.get_ticks_msec() * 0.001
			_press_consumed = false
		else:
			if _pointer_down >= 0 and not _press_consumed and event.position.distance_to(_pointer) < 12:
				_show_actions(event.position,true)
			_pointer_down = -1.0

func _setting(section: String, key: String, fallback: Variant) -> Variant:
	var settings_node := get_node_or_null("/root/GameSettings")
	var value: Variant = settings_node.get_value(section,key) if settings_node != null else null
	return fallback if value == null else value
