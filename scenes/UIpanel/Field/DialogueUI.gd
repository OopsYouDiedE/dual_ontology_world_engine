extends CanvasLayer

## 双角色对话剧场（DialogueUI.gd）
## 管理立绘差分、证言呈堂、追问打断与双角色对谈回合。

signal dialogue_step
signal choice_selected(id: String)
signal dialogue_finished(last_choice: String)
signal evidence_presented(item_id: String, context: Dictionary)

@onready var dialogue_box: PanelContainer = $BottomContainer/DialogueBox
@onready var speaker_label: Label = $BottomContainer/DialogueBox/MarginContainer/VBoxContainer/SpeakerLabel
@onready var body_label: RichTextLabel = $BottomContainer/DialogueBox/MarginContainer/VBoxContainer/BodyLabel
@onready var choices_container: VBoxContainer = $BottomContainer/DialogueBox/MarginContainer/VBoxContainer/ChoicesContainer
@onready var btn_next: Button = $BottomContainer/DialogueBox/MarginContainer/VBoxContainer/HBoxContainer/NextButton
@onready var btn_present_evidence: Button = $BottomContainer/DialogueBox/MarginContainer/VBoxContainer/HBoxContainer/PresentEvidenceButton

@onready var left_portrait: TextureRect = $LeftPortrait
@onready var right_portrait: TextureRect = $RightPortrait

var _lines: Array = []
var _current_line_idx := 0
var _last_choice := ""

func _ready() -> void:
	layer = 20
	process_mode = Node.PROCESS_MODE_ALWAYS
	btn_next.pressed.connect(advance)
	btn_present_evidence.pressed.connect(_on_present_evidence_pressed)
	hide()

func start_dialogue(dialogue_data: Dictionary) -> void:
	_lines = dialogue_data.get("lines", [])
	_current_line_idx = 0
	_last_choice = ""
	show()
	_show_current_line()

func show_line(speaker: String, text: String, _emotion := "neutral", choices: Array = []) -> void:
	speaker_label.text = speaker
	body_label.text = text

	for child in choices_container.get_children():
		child.queue_free()

	if choices.is_empty():
		btn_next.show()
	else:
		btn_next.hide()
		for ch in choices:
			var ch_id: String = ""
			var ch_text: String = ""
			if ch is Dictionary:
				ch_id = str(ch.get("id", ""))
				ch_text = str(ch.get("text", ch_id))
			else:
				ch_id = str(ch)
				ch_text = ch_id

			var btn := Button.new()
			btn.text = "▶ " + ch_text
			btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
			btn.pressed.connect(func():
				_last_choice = ch_id
				choice_selected.emit(ch_id)
				advance()
			)
			choices_container.add_child(btn)

func advance() -> void:
	dialogue_step.emit()
	_current_line_idx += 1
	if _current_line_idx < _lines.size():
		_show_current_line()
	else:
		close_dialogue()

func _show_current_line() -> void:
	if _current_line_idx >= _lines.size(): return
	var line: Dictionary = _lines[_current_line_idx]
	var speaker: String = str(line.get("speaker", "霍恩侦探"))
	var text: String = str(line.get("text", ""))
	var emotion: String = str(line.get("emotion", "neutral"))
	var choices: Array = line.get("choices", [])
	show_line(speaker, text, emotion, choices)

func _on_present_evidence_pressed() -> void:
	evidence_presented.emit("clue_keycard", {"dialogue_idx": _current_line_idx})

func close_dialogue() -> void:
	hide()
	dialogue_finished.emit(_last_choice)
