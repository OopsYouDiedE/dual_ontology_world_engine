extends RefCounted
## Reads one authored observation through Dialogue Manager's real evaluator.
## The caller owns evidence acquisition and commits the returned record.
static func read(dialogue: Node, resource: Resource, title: String, context: Node) -> Dictionary:
	var paragraphs: Array[String] = []
	var line_ids: Array[String] = []
	var visited: Array[String] = []
	var key := title
	while true:
		if key in visited:
			return {"status":"cycle"}
		visited.append(key)
		var line: Variant = await dialogue.manager.get_next_dialogue_line(resource, key, [context])
		if line == null: break
		if not line.responses.is_empty():
			return {"status":"interactive"}
		var speaker := str(dialogue.call("_localized_speaker", line.character))
		paragraphs.append(speaker + "\n" + str(line.text))
		line_ids.append(str(line.id))
		key = str(line.next_id)
	return {"status":"empty" if paragraphs.is_empty() else "complete", "paragraphs":paragraphs, "line_ids":line_ids}
