class_name FirstAwakeningDeduction
extends RefCounted
## FirstAwakeningDeduction: 处理脑内推演程序编译、Core回放暂停点生成、提交验证与答辩裁定

const INVESTIGATION_CONTENT: Resource = preload("res://scenes/FirstAwakening/investigation_content.tres")
const AnswerRules = preload("res://scenes/FirstAwakening/AnswerRules.gd")

var scene: Node2D
var state: Resource

func setup(p_scene: Node2D, p_state: Resource) -> void:
	scene = p_scene
	state = p_state

func current_replay_input() -> Dictionary:
	return {
		"drafts": state.mind_drafts.duplicate(true),
		"text": state.mind_text if state.mind_drafts.is_empty() else "",
		"person": state.mind_person if state.mind_drafts.is_empty() else "",
		"actions": state.sim_actions.duplicate(),
		"actor_actions": state.sim_actor_actions.duplicate(true)
	}

func replay_submission_validation() -> Dictionary:
	var reasons: Array[String] = []
	if state.mind_dirty:
		reasons.append(scene.tr("FA_UI_379"))
	if not state.mind_note.is_empty():
		reasons.append(state.mind_note)
	if state.sim_result.is_empty() or state.replay_states.is_empty() or state.simulated_draft_snapshot.is_empty():
		reasons.append(scene.tr("FA_UI_380"))
	elif state.simulated_draft_snapshot != current_replay_input():
		reasons.append(scene.tr("FA_UI_381"))
	if state.replay_playing or state.replay_events.is_empty() or state.replay_index < state.replay_events.size():
		reasons.append(scene.tr("FA_UI_382"))
	if reasons.is_empty():
		var final_state: Dictionary = state.replay_states.back()
		for s in state.replay_states:
			if s.get("action", "") == "RuleLimit":
				reasons.append(scene.tr("FA_UI_383"))
				break
		var ids: Array = final_state.get("ids", [])
		var conflicts: Array = final_state.get("conflicts", [])
		var conflict_kinds: Array = final_state.get("conflict_kinds", [])
		for clue in INVESTIGATION_CONTENT.replay_clues:
			var investigated: bool = state.evidence.has(clue.site)
			if clue.id == "wipe-after-stop":
				investigated = state.evidence.has("stop") and state.evidence.has("floor_wipe")
			if not investigated:
				continue
			for state_id in clue.state_ids:
				if not ids.has(state_id):
					reasons.append(scene.tr("FA_UI_385") % scene.tr(scene.sites[clue.site].title_id))
					break
			if clue.id == "locks" and (not final_state.get("closed", false) or not final_state.get("inner_bolt", false) or not final_state.get("outer_bar", false)):
				reasons.append(scene.tr("FA_UI_384"))
		var investigated_conflicts: Array[String] = []
		if state.evidence.has("hinge") and state.evidence.has("weight"):
			investigated_conflicts.append("CounterweightRequiresBrace")
		if state.evidence.has("trace"):
			investigated_conflicts.append("SootMaskRequiresOpenDoorAndBrace")
		if state.evidence.has("paper"):
			investigated_conflicts.append("PaperTransferRequiresDirtyWedgeBeforeWrapping")
		if state.evidence.has("stop") and state.evidence.has("floor_wipe"):
			investigated_conflicts.append("WipeBandRequiresStopMovedBeforeWiping")
		for conflict_id in investigated_conflicts:
			var conflict_index := conflict_kinds.find(conflict_id)
			if conflict_index >= 0:
				reasons.append(str(conflicts[conflict_index]))
	return {"ready": reasons.is_empty(), "reasons": reasons}

func build_replay_pause_points() -> void:
	state.replay_pause_points.clear()
	state.replay_pause_display_points.clear()
	state.replay_pause_ids.clear()
	var seen := {}
	for index in range(1, state.replay_states.size()):
		var ids: Array = state.replay_states[index].ids
		var titles: Array[String] = []
		var display_titles: Array[String] = []
		var point_ids: Array[String] = []
		for clue in INVESTIGATION_CONTENT.replay_clues:
			if seen.has(clue.id):
				continue
			var present := true
			for state_id in clue.state_ids:
				if not ids.has(state_id):
					present = false
			if present:
				seen[clue.id] = true
				point_ids.append(clue.id)
				titles.append(clue.pause_title)
				display_titles.append(scene.tr(clue.pause_title_id))
		if not titles.is_empty():
			state.replay_pause_points[index] = " / ".join(titles)
			state.replay_pause_display_points[index] = " / ".join(display_titles)
			state.replay_pause_ids[index] = point_ids

func compile_program(bridge: Node) -> Dictionary:
	if not state.sim_actor_actions.is_empty():
		return bridge.CompileActorDrafts(state.sim_actor_actions, replay_context())
	if state.sim_actions.is_empty() or not state.sim_actions[0].contains("|"):
		return bridge.Compile(state.sim_actions)
	return bridge.CompileDraft(state.sim_actions, replay_context())

func replay_context() -> Dictionary:
	var context := {}
	for id in scene.sites:
		var site: Dictionary = scene.sites[id]
		var talk: Array[String] = []
		var speaker: String = site.title
		if id == "transmitter":
			speaker = "铜口"
		if id in ["engineer", "chef", "musician", "transmitter", "meeting"]:
			for entry in scene.dialogue.history:
				if entry.begins_with(speaker + "\n") or (id == "meeting" and state.meeting_introduction_seen):
					talk.append(entry)
		context[id] = {
			"title": site.title,
			"point": site.point,
			"description": site.description if state.evidence.has(id) else "",
			"known_dialogue": "\n".join(talk)
		}
	return context

func reveal_replay_point() -> void:
	if not state.replay_pause_points.has(state.replay_index):
		return
	var point_ids: Array = state.replay_pause_ids[state.replay_index]
	for clue in INVESTIGATION_CONTENT.replay_clues:
		if point_ids.has(clue.id):
			state.replay_reached[clue.canonical] = true

func available_display_id(options: Array, selected_index: int) -> String:
	var index := 0
	for option in options:
		if not option[1]:
			continue
		if index == selected_index:
			return option[2]
		index += 1
	return "FA_UI_166"
