extends Node
## Iterates already resolved semantic lines for the shared dialogue view.
## The chapter session owns every choice and consequence.
var lines: Array = []
var position := 0

func set_lines(value: Array) -> void:
	lines = value.duplicate(true)
	for entry in lines:
		entry["speaker"] = str(entry.get("speaker_name", entry.get("speaker", "")))
	position = 0

func StartDialogue(_title: String) -> Dictionary:
	position = 0
	return _packet()

func AdvanceDialogue() -> Dictionary:
	position += 1
	return _packet()

func Choose(_id: String) -> Dictionary:
	push_error("Chapter2Presentation receives lines only; send choices to the chapter session")
	return {"finished": true}

func _packet() -> Dictionary:
	if position >= lines.size():
		lines.clear()
		return {"finished": true, "lines": [], "choices": [], "commands": []}
	return {"finished": false, "lines": [lines[position]], "choices": [], "commands": []}

func clear() -> void:
	lines.clear()
	position = 0
