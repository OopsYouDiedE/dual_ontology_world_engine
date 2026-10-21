extends RefCounted
## One transition owner. Lifecycle, persistence and concurrent host work have
## independent axes; callers request transitions and read the derived gates.
enum Phase { HOME, OPENING, LIVE, OPENING_FAILED, FAULTED, CLOSING, CLOSED }
enum Persistence { IDLE, REQUESTED, SAVING, LOADING }
enum Work { DRAINING = 1, READING = 2, OPTIONS = 4, BACKGROUND = 8, COMPLETING = 16, BOOK = 32 }
var _phase := Phase.HOME
var _persistence := Persistence.IDLE
var _work := 0
var _session_opened := false

var started: bool:
	get: return _session_opened
var opening: bool:
	get: return _phase == Phase.OPENING
var opening_failed: bool:
	get: return _phase == Phase.OPENING_FAILED
var view_faulted: bool:
	get: return _phase == Phase.FAULTED
var closing: bool:
	get: return _phase in [Phase.CLOSING, Phase.CLOSED]
var shutdown_finished: bool:
	get: return _phase == Phase.CLOSED
var draining: bool:
	get: return (_work & Work.DRAINING) != 0
var reading: bool:
	get: return (_work & Work.READING) != 0
var player_options_open: bool:
	get: return (_work & Work.OPTIONS) != 0
var background_execution: bool:
	get: return (_work & Work.BACKGROUND) != 0
var completion_pending: bool:
	get: return (_work & Work.COMPLETING) != 0
var book_reading: bool:
	get: return (_work & Work.BOOK) != 0
var bookmark_requested: bool:
	get: return _persistence == Persistence.REQUESTED
var bookmark_saving: bool:
	get: return _persistence == Persistence.SAVING
var bookmark_loading: bool:
	get: return _persistence == Persistence.LOADING

const WORK_BITS := {"draining": Work.DRAINING, "reading": Work.READING,
	"player_options_open": Work.OPTIONS, "background_execution": Work.BACKGROUND,
	"completion_pending": Work.COMPLETING, "book_reading": Work.BOOK}
const PERSISTENCE_STATES := {"bookmark_requested": Persistence.REQUESTED,
	"bookmark_saving": Persistence.SAVING, "bookmark_loading": Persistence.LOADING}

func transition(event: String, active := true) -> void:
	if WORK_BITS.has(event):
		var bit: int = WORK_BITS[event]
		_work = (_work | bit) if active else (_work & ~bit)
		return
	if PERSISTENCE_STATES.has(event):
		var state: int = PERSISTENCE_STATES[event]
		if active: _persistence = state
		elif _persistence == state: _persistence = Persistence.IDLE
		return
	match event:
		"started":
			_session_opened = active
			if not active: _phase = Phase.HOME
		"opening":
			_phase = Phase.OPENING if active else (Phase.LIVE if _session_opened else Phase.HOME)
		"opening_failed":
			if active: _phase = Phase.OPENING_FAILED
		"view_faulted":
			_phase = Phase.FAULTED if active else Phase.LIVE
		"closing":
			if active:
				_phase = Phase.CLOSING
				_persistence = Persistence.IDLE
				_work &= ~(Work.OPTIONS | Work.READING)
		"shutdown_finished":
			if active:
				_phase = Phase.CLOSED
				_work = 0

func blocks_input() -> bool:
	return _phase != Phase.LIVE or _persistence != Persistence.IDLE

func allows_background() -> bool:
	return _phase == Phase.LIVE and _persistence == Persistence.IDLE \
		and (player_options_open or reading) and not book_reading \
		and not background_execution and not completion_pending

func allows_selection() -> bool:
	return not blocks_input() and player_options_open and not draining \
		and not completion_pending and not background_execution and not book_reading
