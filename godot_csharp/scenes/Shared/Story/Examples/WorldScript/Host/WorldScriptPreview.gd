extends Control
## Author preview: Godot renders semantic outputs and reports visible local entities.
## Candidate applicability, NPC policy and action execution belong to WorldSession.
@export_file("*.json") var compiled_document := "res://scenes/Shared/Story/Examples/WorldScript/generated_world.json"

@onready var engine: Node = $WorldSessionEngine
@onready var status: Label = $Margin/Column/Status
@onready var transcript: RichTextLabel = $Margin/Column/Transcript
@onready var choices: VBoxContainer = $Margin/Column/ChoiceScroll/Choices
@onready var next_button: Button = $Margin/Column/Next
@onready var chapter_button: Button = $Margin/Column/Chapter2
@onready var refresh_button: Button = $Margin/Column/Actions/Refresh
@onready var observe_button: Button = $Margin/Column/Actions/Observe
@onready var room: HBoxContainer = $Margin/Column/RoomView

var entity_names: Dictionary = {}
var entities: Array = []
var selected_actor := ""
var selected_target := ""
var opened := false
var executions: Dictionary = {}
var dialogue_queue: Array = []
var presentation: Dictionary = {}
var line_index := 0
var last_error := ""

func _ready() -> void:
	get_window().title = "世界脚本 · 作者预览"
	get_window().size = Vector2i(1100, 820)
	next_button.pressed.connect(_advance_line)
	chapter_button.pressed.connect(_load_next)
	refresh_button.pressed.connect(_request_candidates)
	observe_button.pressed.connect(_observe_room)
	_clear_room()
	_consume(engine.OpenDocument(compiled_document))
	if opened: _observe_room()
	_refresh_controls()

func _process(delta: float) -> void:
	# Advance the real action clock with elapsed host time, never prewrite outcomes.
	if opened and presentation.is_empty() and dialogue_queue.is_empty() and not executions.is_empty():
		_consume(engine.Dispatch({"kind": "advance", "elapsed": delta}))

func _actor_id() -> String:
	return selected_actor

func _clear_room() -> void:
	for child in room.get_children():
		room.remove_child(child)
		child.queue_free()

func _actor_room() -> String:
	for entity in entities:
		if str(entity.id) == selected_actor: return str(entity.room)
	return ""

func _bind_entities() -> void:
	entities = engine.Entities()
	entity_names = engine.EntityNames()
	var actors: Array = entities.filter(func(entity: Dictionary) -> bool: return str(entity.kind) == "character")
	if not actors.any(func(entity: Dictionary) -> bool: return str(entity.id) == selected_actor):
		selected_actor = str(actors[0].id) if not actors.is_empty() else ""
	var local_room := _actor_room()
	var targets: Array = entities.filter(func(entity: Dictionary) -> bool: return str(entity.kind) == "object" and str(entity.room) == local_room)
	if not targets.any(func(entity: Dictionary) -> bool: return str(entity.id) == selected_target):
		selected_target = str(targets[0].id) if not targets.is_empty() else ""
	_clear_room()
	var location := Label.new()
	location.text = _entity_name(local_room) if local_room != "" else "没有已加载的角色"
	room.add_child(location)
	for entity in actors:
		var button := Button.new()
		button.text = str(entity.name)
		button.toggle_mode = true
		button.button_pressed = str(entity.id) == selected_actor
		button.set_meta("entity_id", str(entity.id))
		button.set_meta("actor", true)
		button.pressed.connect(_select_actor.bind(str(entity.id)))
		room.add_child(button)
	for entity in targets:
		var button := Button.new()
		button.text = str(entity.name)
		button.toggle_mode = true
		button.button_pressed = str(entity.id) == selected_target
		button.set_meta("entity_id", str(entity.id))
		button.pressed.connect(_select_target.bind(str(entity.id)))
		room.add_child(button)

func _select_actor(id: String) -> void:
	selected_actor = id
	selected_target = ""
	_bind_entities()
	_observe_room()

func _select_target(id: String) -> void:
	selected_target = id
	_bind_entities()
	_request_candidates()

func _observe_room() -> void:
	if not opened or selected_actor == "": return
	_clear_choices()
	_bind_entities()
	var visible: Array = []
	var local_room := _actor_room()
	var visible_actors: Array = []
	# This declaration preview verifies the live room, including held objects.
	# Author controls for actors in other rooms do not grant perception of them.
	for entity in entities:
		if str(entity.id) == local_room or str(entity.room) == local_room:
			visible.append(str(entity.id))
			if str(entity.kind) == "character": visible_actors.append(str(entity.id))
	for actor in visible_actors:
		_consume(engine.Dispatch({"kind": "perception", "actor": actor, "entities": visible}))
	_request_candidates()

func _request_candidates() -> void:
	if not opened or selected_actor == "": return
	_clear_choices()
	var input := {"kind": "candidates", "actor": _actor_id()}
	if selected_target != "": input["target"] = selected_target
	_consume(engine.Dispatch(input))

func _select_candidate(token: String) -> void:
	_clear_choices()
	_consume(engine.Dispatch({"kind": "select", "token": token}))

func _load_next() -> void:
	_clear_choices()
	_consume(engine.LoadNextChapter())
	if engine.CurrentChapter() != "": _observe_room()

func _consume(events: Array) -> void:
	var action_finished := false
	for event in events:
		match str(event.kind):
			"chapter_loaded":
				opened = true
				_bind_entities()
				status.text = "当前章节：" + str(event.chapter)
				_log("加载章节：" + str(event.chapter))
			"candidates":
				_show_candidates(event.choices)
			"action_started":
				executions[str(event.execution_id)] = true
				_log("行动开始：" + str(event.candidate_id))
			"action_finished":
				executions.erase(str(event.execution_id))
				action_finished = true
				_log("行动完成：" + str(event.candidate_id) if bool(event.succeeded) else "行动失败：" + str(event.failure))
			"dialogue":
				dialogue_queue.append(event)
			"no_candidate":
				_log("当前没有适用的反应：" + _entity_name(str(event.actor)))
			"error":
				last_error = str(event.message)
				status.text = last_error
				_log("错误：" + last_error)
			_: push_error("Unsupported semantic output: " + str(event.kind))
	if presentation.is_empty() and not dialogue_queue.is_empty(): _begin_dialogue()
	_refresh_controls()
	if action_finished:
		_bind_entities()
		_refresh_controls()
		if _idle(): _request_candidates()

func _show_candidates(options: Array) -> void:
	_clear_choices()
	for option in options:
		var button := Button.new()
		button.text = str(option.label)
		button.tooltip_text = str(option.id)
		button.pressed.connect(_select_candidate.bind(str(option.token)))
		choices.add_child(button)
	if options.is_empty():
		var empty := Label.new()
		empty.text = "核心当前没有提供可选择的行动。"
		choices.add_child(empty)

func _clear_choices() -> void:
	for child in choices.get_children():
		choices.remove_child(child)
		child.queue_free()

func _begin_dialogue() -> void:
	presentation = dialogue_queue.pop_front()
	line_index = 0
	if presentation.lines.is_empty():
		_finish_dialogue()
		return
	_show_line()

func _show_line() -> void:
	var line: Dictionary = presentation.lines[line_index]
	_log((_entity_name(str(line.speaker)) + "：" if str(line.speaker) != "" else "") + str(line.text))
	next_button.show()
	next_button.grab_focus()

func _advance_line() -> void:
	if presentation.is_empty(): return
	line_index += 1
	if line_index < presentation.lines.size(): _show_line()
	else: _finish_dialogue()

func _finish_dialogue() -> void:
	var id := str(presentation.presentation_id)
	presentation = {}
	next_button.hide()
	_consume(engine.Dispatch({"kind": "presentation_completed", "presentation_id": id}))
	if _idle(): _request_candidates()

func _idle() -> bool:
	return executions.is_empty() and presentation.is_empty() and dialogue_queue.is_empty()

func _refresh_controls() -> void:
	var can_input := opened and _idle() and last_error == ""
	chapter_button.disabled = not can_input or not engine.CanLoadNextChapter()
	refresh_button.disabled = not can_input or selected_actor == ""
	observe_button.disabled = not can_input or selected_actor == ""
	for child in room.get_children():
		if child is Button: child.disabled = not can_input
	for child in choices.get_children():
		if child is Button: child.disabled = not can_input

func _entity_name(id: String) -> String:
	if entity_names.has(id): return str(entity_names[id])
	if id.begins_with("world:") or id.begins_with("chapter:"):
		push_error("Missing display-name binding for entity: " + id)
		return "未绑定名称的角色"
	return id

func _log(text: String) -> void:
	transcript.append_text(text + "\n\n")
