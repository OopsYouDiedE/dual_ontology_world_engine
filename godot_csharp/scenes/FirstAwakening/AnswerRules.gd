extends RefCounted
## The answer menu and submission use the same evidence requirements.

static func available(option: Dictionary, evidence: Dictionary, checks: Dictionary) -> bool:
	var requirements: Dictionary = option.get("requirements", {})
	for id in requirements.get("evidence_all", []):
		if not evidence.has(id): return false
	var any: Array = requirements.get("evidence_any", [])
	if not any.is_empty():
		var found := false
		for id in any:
			if evidence.has(id): found = true
		if not found: return false
	for id in requirements.get("checks_all", []):
		if not checks.has(id): return false
	return true

static func resolution(sections: Array[Dictionary], slots: Dictionary, evidence: Dictionary, checks: Dictionary) -> String:
	var chosen := {}
	for section in sections:
		var slot: String = section.slot
		for option in section.options:
			if option.value == slots[slot]:
				if not available(option, evidence, checks): return "reject_unsupported"
				chosen[slot] = option
				break
		if not chosen.has(slot): return "reject_unsupported"
	for slot in ["place", "door", "outer"]:
		var outcome: String = chosen[slot].get("outcome", "")
		if outcome.begins_with("reject_"): return outcome
	return "ending_newcomer" if chosen.place.get("outcome", "") == "ending_newcomer" else "ending_history"
