extends "res://scenes/Awakening/check/playtest_session.gd"
## Local automated UI regression. Inputs remain the generic driver protocol.
## Passive host inspection records modal type and texture; no story state is written.
func _publish(seq: int, label: String, error: String) -> void:
	await super._publish(seq, label, error)
	var game := current_scene
	var evidence := {
		"seq": seq, "name": label, "elapsed_ms": Time.get_ticks_msec() - started_ms,
		"day": game.state.get("day", -1), "busy": game.busy,
		"action_playing": game.action_playing, "modal_kind": game.modal_kind,
		"info_title": game.info_title, "card_texture_present": game.card_texture != null,
	}
	var file := FileAccess.open(directory.path_join("ui-observations.jsonl"), FileAccess.READ_WRITE if FileAccess.file_exists(directory.path_join("ui-observations.jsonl")) else FileAccess.WRITE)
	file.seek_end()
	file.store_line(JSON.stringify(evidence))
