@tool
extends RefCounted
## Presentation archive. Inputs are delivered observer prose, never world state.
## Personal notes are UI data; they cannot grant evidence or submit DV actions.
signal changed

const SECTIONS := ["evidence", "testimony", "items", "people", "gallery", "hypotheses", "records"]
var viewer := ""
var entries: Array[Dictionary] = []
var notes: Array[Dictionary] = []
var _cast: Dictionary = {}
var _snapshot: Dictionary = {}
var _next_note := 1

func apply_snapshot(snapshot: Dictionary) -> void:
	var next_viewer := str(snapshot.get("viewer", ""))
	if viewer != "" and next_viewer != viewer:
		# A viewpoint switch must not retain another person's private archive.
		entries.clear()
		notes.clear()
		_next_note = 1
	viewer = next_viewer
	_snapshot = snapshot
	_cast = snapshot.get("cast", {})
	# Only explicitly observer-filtered archive fields are imported. In particular,
	# snapshot.people contains geometry placements and is NOT a notebook source.
	var projected: Variant = snapshot.get("casebook", {})
	if projected is Dictionary and str(projected.get("viewer", viewer)) == viewer:
		for section in SECTIONS:
			for value in projected.get(section, []):
				if value is Dictionary and bool(value.get("acquired", false)):
					var item: Dictionary = value.duplicate(true)
					item["category"] = section
					_upsert(item)
	changed.emit()

func record_presentation(result: Dictionary, lines: Array) -> void:
	if not bool(result.get("visible", false)) or str(result.get("status", "")) != "applied" or lines.is_empty(): return
	var event := str(result.get("execution_event", ""))
	if event == "": return
	var source := str(result.get("action_id", ""))
	var action_kind := str(result.get("action_kind", ""))
	var voice := str(result.get("voice", ""))
	var texts: Array[String] = []
	for line in lines:
		if line is Dictionary and str(line.get("text", "")) != "": texts.append(str(line.text))
	if texts.is_empty(): return
	var category := "records"
	var certainty := "objective_fact"
	if action_kind == "testimony" or voice == "testimony":
		category = "testimony"
		certainty = "subjective_statement" # Subjective testimony is never treated as objective fact
	elif voice == "visible-inspection" or action_kind in ["inspect", "observe"]:
		category = "evidence"
		certainty = "objective_fact"
	elif action_kind in ["deduce", "hypothesis", "reasoning"]:
		category = "hypotheses"
		certainty = "inference" # Inferences are never treated as settled facts
	var title := str(result.get("title", ""))
	if title == "": title = texts[0].left(36)
	var entry := {
		"id": event, "category": category, "certainty": certainty, "title": title, "text": "\n\n".join(texts),
		"source": source, "execution_event": event, "lines": lines.duplicate(true), "source_refs": result.get("source_refs", []).duplicate(true),
		"speaker": str(result.get("actor", "")), "speaker_name": str(result.get("speaker_name", "")),
		"game_time": result.get("game_time", _snapshot.get("game_time", "")),
		"place": str(_snapshot.get("hud", {}).get("room", _snapshot.get("logical_location", ""))),
		"action_kind": action_kind, "voice": voice, "related": [], "acquired": true,
	}
	_upsert(entry)
	# Inspected objects have a dossier, not a claim that the player possesses them.
	if category == "evidence" and (voice == "visible-inspection" or action_kind == "inspect"):
		var object_record: Dictionary = entry.duplicate(true)
		object_record.id = "object:" + source
		object_record.category = "items"
		object_record.related = [event]
		object_record.ownership = "inspected"
		_upsert(object_record)
	var speaker := str(result.get("actor", ""))
	var display_name := str(result.get("speaker_name", ""))
	if speaker != "" and display_name != "" and _cast.has(speaker):
		var portrait := "res://assets/characters/DialogueCast/%s_calm.tres" % str(_cast[speaker])
		var person := {"id": "person:" + speaker, "category": "people", "title": display_name,
			"text": "", "image": portrait, "related": [event], "speaker": speaker, "acquired": true,
			"game_time": entry.game_time, "place": entry.place, "source": source}
		var previous := find_entry(str(person.id))
		if not previous.is_empty():
			person.related = previous.get("related", []).duplicate()
			if not person.related.has(event): person.related.append(event)
		_upsert(person)
	# CG/image records enter via an actually opened card or acquired projection.
	changed.emit()

func record_card(card: Dictionary) -> String:
	var text := str(card.get("text", card.get("body", "")))
	var id := str(card.get("id", card.get("execution_event", "")))
	if id == "": id = "card:" + (str(card.get("title", "")) + "\n" + text).sha256_text()
	var item: Dictionary = card.duplicate(true)
	item.id = id
	item.category = str(card.get("category", "evidence"))
	if not item.category in SECTIONS: item.category = "evidence"
	item.title = str(card.get("title", "证物卡片"))
	item.text = text
	item.acquired = true
	item.game_time = card.get("game_time", _snapshot.get("game_time", ""))
	item.place = str(card.get("place", _snapshot.get("logical_location", "")))
	item.related = card.get("related", [])
	_upsert(item)
	changed.emit()
	return id

func _upsert(item: Dictionary) -> void:
	if str(item.get("id", "")) == "": return
	for i in entries.size():
		if str(entries[i].get("id", "")) == str(item.id):
			if str(item.get("category", "")) in ["items", "gallery"]:
				var links: Array = entries[i].get("related", []).duplicate()
				for link in item.get("related", []):
					if not links.has(link): links.append(link)
				item.related = links
			entries[i] = item
			return
	entries.append(item)

func list_section(section: String) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for entry in entries:
		if str(entry.get("category", "")) == section: result.append(entry)
	if section == "hypotheses":
		for note in notes: result.append(note)
	return result

func find_entry(id: String) -> Dictionary:
	for entry in entries:
		if str(entry.get("id", "")) == id: return entry
	for note in notes:
		if str(note.get("id", "")) == id: return note
	return {}

func add_note(text: String, related: Array = [], title := "", id := "") -> String:
	if text.strip_edges() == "": return ""
	if id == "":
		id = "note:%d" % _next_note
		_next_note += 1
	var note := {"id": id, "category": "hypotheses", "title": title if title != "" else text.strip_edges().left(28),
		"text": text, "related": related.duplicate(), "personal": true, "game_time": _snapshot.get("game_time", ""),
		"place": str(_snapshot.get("logical_location", ""))}
	for i in notes.size():
		if str(notes[i].get("id", "")) == id:
			notes[i] = note
			changed.emit()
			return id
	notes.append(note)
	changed.emit()
	return id

func export_data() -> Dictionary:
	return {"version": 1, "viewer": viewer, "entries": entries.duplicate(true), "notes": notes.duplicate(true), "next_note": _next_note}

func restore_data(data: Dictionary, expected_viewer: String) -> void:
	entries.clear()
	notes.clear()
	viewer = expected_viewer
	_next_note = 1
	if int(data.get("version", 0)) != 1 or str(data.get("viewer", "")) != expected_viewer:
		changed.emit()
		return
	for entry in data.get("entries", []):
		if entry is Dictionary and str(entry.get("category", "")) in SECTIONS and bool(entry.get("acquired", false)):
			entries.append(entry.duplicate(true))
	for note in data.get("notes", []):
		if note is Dictionary and bool(note.get("personal", false)):
			notes.append(note.duplicate(true))
	_next_note = maxi(1, int(data.get("next_note", notes.size() + 1)))
	changed.emit()

func time_text(value: Variant) -> String:
	var text := str(value)
	if text.is_valid_float():
		var minute := floori(float(text))
		return "%02d:%02d" % [posmod(minute / 60, 24), posmod(minute, 60)]
	return text if text != "" else "—"
