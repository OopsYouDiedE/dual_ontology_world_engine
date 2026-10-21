extends Node

signal EventsReady(events: Array)
signal TraceClosed

const REPLAY_CLOCK := preload("res://scenes/Host/ReplayClock.gd")
const PACK_PATH := "res://Contracts/presentation_mock_pack.json"
var _pack: Dictionary = {}
var _snapshot: Dictionary = {}
var _pending: Dictionary = {}
var _casebook: Dictionary = {}
var _sequence := 0
var _version := 0
var _closed := false
var _queued_dispatches := 0
var _replay_clock := REPLAY_CLOCK.new()
var _timing_enabled := false
var _timings: Array = []

func _ready() -> void:
	_pack = JSON.parse_string(FileAccess.get_file_as_string(PACK_PATH))
	_snapshot = _pack.snapshot.duplicate(true)
	_options()

func ConfigureSession(_options: Dictionary) -> void: pass
func SetPlayerControlled(_value: bool) -> void: pass
func SetDiagnosticsEnabled(enabled: bool) -> void: _timing_enabled = enabled
func IsDeveloperMode() -> bool: return false
func CanInspect() -> bool: return true
func HasReplaySample() -> bool: return true
func CanBackgroundTick() -> bool: return false
func IsDispatchIdle() -> bool: return _queued_dispatches == 0
func RecordHostFailure(message: String) -> void:
	push_error(message)
	EventsReady.emit([{"kind": "error", "message": message}])
func Snapshot() -> Dictionary: return _snapshot.duplicate(false)
func ReadMilestoneProgressJson() -> String: return JSON.stringify(_snapshot)
func TimingClockUs() -> int: return Time.get_ticks_usec()
func PollTimings() -> Array:
	var result := _timings
	_timings = []
	return result
func RecordTiming(value: Dictionary) -> void:
	if _timing_enabled:
		_timings.append(JSON.stringify({"real_us": TimingClockUs(), "stage": value.stage,
			"command_id": 0, "interaction_id": value.get("interaction_id", ""), "details": JSON.stringify(value)}))
func RecordVisible(_value: Dictionary) -> void: pass
func RecordInput(_value: Dictionary) -> void: pass
func UpdateCasebook(value: Dictionary) -> void: _casebook = value.duplicate(true)
func ReadCasebook() -> Dictionary: return _casebook.duplicate(true)
func IsBookmarkBoundaryReady() -> bool: return false

func SaveBookmark(_data: Dictionary) -> void:
	EventsReady.emit([{"kind": "bookmark_error", "operation": "save", "message": "当前为独立演出样本；正式存档由游戏会话生成。"}])

func LoadBookmark(_path: String, _data: Dictionary) -> void:
	EventsReady.emit([{"kind": "bookmark_error", "operation": "load", "message": "请从正式会话续读书签；当前为独立演出样本。"}])

func CloseTrace() -> void:
	if _closed: return
	_closed = true
	TraceClosed.emit.call_deferred()

func Dispatch(input: Dictionary) -> Array:
	_queued_dispatches += 1
	_dispatch.call_deferred(input.duplicate(true))
	return []

func _dispatch(input: Dictionary) -> void:
	_queued_dispatches -= 1
	if _closed: return
	match str(input.get("kind", "")):
		"start":
			EventsReady.emit([{"kind": "view", "snapshot": Snapshot(), "casebook": _pack.get("casebook", {})}])
		"load": EventsReady.emit([{"kind": "load_error", "message": "演出样本请使用“开始”进入。"}])
		"view_ready", "dv_next":
			if _pending.is_empty():
				_options()
				EventsReady.emit([{"kind": "state", "snapshot": Snapshot()}, {"kind": "dv_action", "intent": _snapshot.pending, "execution_snapshot": {}}])
		"prepare_ahead": EventsReady.emit([{"kind": "prepared_ahead", "choice_id": input.choice_id, "token": "mock:%d:%s" % [_version, input.choice_id], "state_version": _version}])
		"select": _select(str(input.get("choice_id", "")))
		"dv_complete": _complete(input.receipt)

func _options() -> void:
	var room := str(_snapshot.execution_room)
	var choices: Array = _pack.options.get(room, []).duplicate(true)
	_snapshot.pending = {"status": "awaiting_input", "actor": _snapshot.viewer, "options": choices}
	_snapshot.state_version = _version

func _select(choice: String) -> void:
	var selection: Dictionary = {}
	if choice.begins_with("{"):
		selection = JSON.parse_string(choice)
		choice = str(selection.action_id)
	for option: Dictionary in _snapshot.pending.options:
		if str(option.id) != choice: continue
		_sequence += 1
		var action: Dictionary = _pack.actions[choice].duplicate(true)
		_pending = {"status": "prepared", "actor": _snapshot.viewer, "action_event": "mock-action-%d" % _sequence,
			"action": action, "sample_selection": selection, "execution_cost": {}, "destination": option.get("interaction", {}).get("destination", {}).duplicate(true)}
		_snapshot.pending = _pending
		EventsReady.emit([{"kind": "state", "snapshot": Snapshot()}, {"kind": "dv_action", "intent": _pending, "execution_snapshot": Snapshot()}])
		return

func _complete(receipt: Dictionary) -> void:
	if _pending.is_empty() or str(receipt.get("action_event", "")) != str(_pending.action_event): return
	var action: Dictionary = _pending.action
	var outcome := str(receipt.outcome)
	var event_id := str(_pending.action_event)
	if outcome == "applied" and str(action.kind) == "move":
		var destination: Dictionary = _pending.destination
		_snapshot.room = destination.room
		_snapshot.execution_room = destination.room
		_snapshot.logical_location = destination.logical_location
		_snapshot.location_title = _pack.room_titles[destination.room]
	var result: Dictionary = _pack.presentations.get(str(action.id), _pack.presentations["preview.read"]).duplicate(true)
	result.execution_event = "mock-presentation-%d" % _sequence
	result.presentation_id = result.execution_event
	result.action_event = event_id
	result.action_id = action.id
	result.action_kind = action.kind
	if not _pending.get("sample_selection", {}).is_empty(): result.lines.append("样本表单已接收：" + str(_pending.sample_selection.hypothesis_id))
	result.speaker = str(result.get("actor", _snapshot.viewer))
	result.actor = _snapshot.viewer
	result.completed_actor = _snapshot.viewer
	result.completed_action = action.id
	result.outcome = outcome
	result.status = outcome
	if outcome != "applied":
		result.surface = "演出样本：这次动作未完成，人物已回到动作前的位置。"
		result.lines = []
	_pending = {}
	_version += 1
	_options()
	EventsReady.emit([{"kind": "dv_commit", "action_event": event_id, "actor": _snapshot.viewer, "outcome": outcome},
		{"kind": "state", "snapshot": Snapshot()}, {"kind": "dv_present", "presentation": result}])

func DevCommand(command: String) -> void:
	if command.strip_edges().begins_with("inspect"):
		var data: Dictionary = _pack.inspector.duplicate(true)
		data.physical.game_time = _snapshot.game_time
		data.physical.positions = [{"actor": _snapshot.viewer, "map": _snapshot.map, "room": _snapshot.execution_room}]
		data.state_version = _version
		EventsReady.emit.call_deferred([{"kind": "dev_inspect", "data": data}])
	else:
		EventsReady.emit([{"kind": "dev_error", "message": "演出样本：位置与时钟由样本片段控制。正式开发编辑请使用 --dv-dev 会话。"}])

func LoadReplaySample() -> void:
	var frames: Array = _pack.replay_frames.duplicate(true)
	frames[0].data.snapshot = _pack.snapshot.duplicate(true)
	frames[0].data.casebook = _pack.casebook.duplicate(true)
	_replay_clock.load_frames(frames)
	EventsReady.emit([{"kind": "replay_loaded", "count": frames.size()}])

func LoadReplay(_path: String) -> void:
	EventsReady.emit([{"kind": "dev_error", "message": "纯 GDScript 样本工作区使用“演出样本”；.dvlog / .dvdag 由正式宿主的共享 SmartReplay 读取。"}])

func ReplayPlay(speed := 1.0) -> void: _replay_clock.play(speed)
func ReplayPause() -> void: _replay_clock.pause()
func ReplayStep() -> Dictionary: return _replay_clock.step()
func ReplayAdvance(seconds: float) -> Array: return _replay_clock.advance(seconds)
