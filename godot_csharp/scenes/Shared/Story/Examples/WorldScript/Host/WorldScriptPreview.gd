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
# STORY-HARDCODE[CSH-038][schema][preview]: 作者预览固定以侦探与内门闩控件代表编译故事示例中的实体。
@onready var detective: Label = $Margin/Column/RoomView/Detective
@onready var target: Button = $Margin/Column/RoomView/Bolt

var entity_names: Dictionary = {}
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
	target.pressed.connect(_request_candidates)
	_consume(engine.OpenDocument(compiled_document))
	opened = engine.CurrentChapter() != ""
	if opened:
		entity_names = engine.EntityNames()
		_observe_room()
	_refresh_controls()

func _process(delta: float) -> void:
	# Advance the real action clock with elapsed host time, never prewrite outcomes.
	if opened and presentation.is_empty() and dialogue_queue.is_empty() and not executions.is_empty():
		_consume(engine.Dispatch({"kind": "advance", "elapsed": delta}))

func _actor_id() -> String:
	return str(detective.get_meta("entity_id"))

func _observe_room() -> void:
	if not opened: return
	_clear_choices()
	var entities: Array = []
	# These are the actual visible scene bindings in the actors' current room.
	entities.append(str(room.get_meta("entity_id")))
	var visible_actors: Array = []
	for node in room.get_children():
		if node is CanvasItem and node.is_visible_in_tree() and node.has_meta("entity_id"):
			entities.append(str(node.get_meta("entity_id")))
			if bool(node.get_meta("actor", false)): visible_actors.append(str(node.get_meta("entity_id")))
	for actor in visible_actors:
		_consume(engine.Dispatch({"kind": "perception", "actor": actor, "entities": entities}))
	_request_candidates()

func _request_candidates() -> void:
	if not opened: return
	_clear_choices()
	_consume(engine.Dispatch({"kind": "candidates", "actor": _actor_id(), "target": str(target.get_meta("entity_id"))}))

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
	if action_finished and _idle(): _request_candidates()

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
	var can_input := opened and _idle()
	chapter_button.disabled = not can_input or not engine.CanLoadNextChapter()
	refresh_button.disabled = not can_input
	observe_button.disabled = not can_input
	target.disabled = not can_input
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
