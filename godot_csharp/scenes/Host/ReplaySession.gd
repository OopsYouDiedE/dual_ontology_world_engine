extends Node
## A read-only replay endpoint. Recorded snapshots and presentations are its
## only state; UI commands never prepare, commit, or edit the live session.
var _directions := {"move_left": 0.0, "move_right": 0.0, "move_up": 0.0, "move_down": 0.0}

signal EventsReady(events: Array)
signal TraceClosed

var _snapshot: Dictionary = {}
var _casebook: Dictionary = {}
var _closed := false

# Main shares this endpoint contract with the live bridge. Replay never submits
# authoritative work, records evidence, or writes bookmarks.
func SetDiagnosticsEnabled(_enabled: bool) -> void: pass
func IsDispatchIdle() -> bool: return true
func CanBackgroundTick() -> bool: return false
func RecordTiming(_value: Dictionary) -> void: pass
func RecordVisible(_value: Dictionary) -> void: pass
func RecordInput(_value: Dictionary) -> void: pass
func TimingClockUs() -> int: return Time.get_ticks_usec()
func Snapshot() -> Dictionary: return _snapshot.duplicate(false)
func UpdateCasebook(value: Dictionary) -> void: _casebook = value.duplicate(true)
func Dispatch(_input: Dictionary) -> Array: return []
func RecordHostFailure(message: String) -> void:
	push_error(message)
	EventsReady.emit([{"kind": "error", "message": message}])
func CloseTrace() -> void:
	if _closed: return
	_closed = true
	TraceClosed.emit.call_deferred()

func SetSnapshot(value: Dictionary) -> void:
	_snapshot = value.duplicate(true)
	_snapshot.view_kind = str(value.get("view_kind", "world"))
	_snapshot.field_enabled = true
	_snapshot.active = true
	_snapshot.player_control = true
	_snapshot.people = value.get("people", [])
	_snapshot.initial_placements = value.get("initial_placements", [])
	_snapshot.presentation = {"player_actor": str(value.viewer)}
	_snapshot.pending = value.get("pending", {"status": "awaiting_input", "actor": str(value.viewer), "options": []})
	_snapshot.execution_room = str(value.get("execution_room", value.get("room", "")))
	_snapshot.replay = value.get("replay", {})
	_snapshot.map_knowledge = value.get("map_knowledge", {})
	_snapshot.access = value.get("access", {})
	_snapshot.traces = value.get("traces", [])

func InputDirection() -> Vector2:
	return Vector2(float(_directions.move_right) - float(_directions.move_left),
		float(_directions.move_down) - float(_directions.move_up)).limit_length()

func ReplayInput(event: InputEvent, has_positions: bool) -> void:
	# New records carry real body coordinates; their recorded segments drive
	# collision movement. Legacy inputs without coordinates retain held keys.
	if has_positions:
		for action in _directions: _directions[action] = 0.0
		return
	for action in _directions:
		if InputMap.event_is_action(event, str(action)):
			_directions[action] = event.get_action_strength(str(action))

func ClearInput() -> void:
	for action in _directions: _directions[action] = 0.0

func SetInteraction(phase: String, actor: String, action: String, options: Array) -> void:
	_snapshot.replay_interaction = {"phase": phase, "actor": actor,
		"selected": "" if phase == "cancel" else action, "options": options.duplicate(true)}
