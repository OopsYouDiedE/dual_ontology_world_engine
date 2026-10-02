extends HBoxContainer
## A manuscript replay transport: each seek lands on an existing Core state.
const BEAT_SECONDS := 0.65
const FAST_FACTOR := 4.0
var source: Node2D
var dragging := false

@onready var reset_button: Button = $Reset
@onready var previous_button: Button = get_node_or_null("Previous")
@onready var play_button: Button = $Play
@onready var next_button: Button = get_node_or_null("Next")
@onready var fast_button: Button = $Fast
@onready var seek: HSlider = $Seek
@onready var clock_label: Label = $Clock

func setup(scene: Node2D) -> void:
	source = scene
	if is_node_ready(): refresh()

func _ready() -> void:
	reset_button.pressed.connect(func(): seek_to(0))
	if previous_button:
		previous_button.pressed.connect(func(): seek_to(source.replay_index - 1) if is_instance_valid(source) else null)
	if next_button:
		next_button.pressed.connect(func(): seek_to(source.replay_index + 1) if is_instance_valid(source) else null)
	play_button.pressed.connect(_toggle_play)
	fast_button.toggle_mode = true
	fast_button.toggled.connect(_toggle_fast)
	seek.step = 1.0
	seek.min_value = 0.0
	seek.allow_greater = false
	seek.allow_lesser = false
	seek.focus_mode = Control.FOCUS_ALL
	seek.drag_started.connect(_begin_seek)
	seek.drag_ended.connect(_end_seek)
	seek.value_changed.connect(func(value: float): seek_to(roundi(value)))
	seek.gui_input.connect(_seek_key)
	reset_button.tooltip_text = tr("FA_MIND_078")
	if previous_button:
		previous_button.tooltip_text = tr("FA_MIND_079")
	if next_button:
		next_button.tooltip_text = tr("FA_MIND_080")
	seek.tooltip_text = tr("FA_MIND_081")
	clock_label.tooltip_text = tr("FA_MIND_103") if is_instance_valid(source) and source.state.event_mode else tr("FA_MIND_082")
	clock_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	refresh()

func _process(_delta: float) -> void:
	if is_visible_in_tree(): refresh()

func _has_replay() -> bool:
	return is_instance_valid(source) and not source.replay_events.is_empty() and source.replay_states.size() == source.replay_events.size() + 1

func _pause() -> void:
	source.replay_playing = false
	source.replay_accum = 0.0

func seek_to(index: int) -> void:
	if not _has_replay(): return
	_pause()
	var bounded := clampi(index, 0, source.replay_events.size())
	# This also updates trace availability and keeps dirty-manuscript checks intact.
	source._replay_step(bounded - source.replay_index)
	refresh()

func _begin_seek() -> void:
	dragging = true
	if _has_replay():
		_pause()
		source._replay_step(0)

func _end_seek(_changed: bool) -> void:
	dragging = false
	refresh()

func _seek_key(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.keycode in [KEY_HOME, KEY_END] and _has_replay():
		seek_to(0 if event.keycode == KEY_HOME else source.replay_events.size())
		seek.accept_event()

func _toggle_play() -> void:
	if not _has_replay(): return
	if source.replay_playing:
		_pause()
		source._replay_step(0)
	else:
		# The real scene restarts at zero after reaching the final state.
		source._start_replay(source.replay_fast)
	refresh()

func _toggle_fast(enabled: bool) -> void:
	if not is_instance_valid(source): return
	# Preserve the in-flight fraction when changing speed; never start playback here.
	var before := BEAT_SECONDS / (FAST_FACTOR if source.replay_fast else 1.0)
	var fraction := clampf(source.replay_accum / before, 0.0, 1.0)
	source.replay_fast = enabled
	var after := BEAT_SECONDS / (FAST_FACTOR if enabled else 1.0)
	source.replay_accum = fraction * after
	refresh()

func elapsed_seconds() -> float:
	if not _has_replay(): return 0.0
	var elapsed: float = source.replay_index * BEAT_SECONDS
	if source.replay_playing and source.replay_index < source.replay_events.size():
		var duration := BEAT_SECONDS / (FAST_FACTOR if source.replay_fast else 1.0)
		elapsed += clampf(source.replay_accum / duration, 0.0, 1.0) * BEAT_SECONDS
	return elapsed

const BASE_SECONDS := 26400.0 # 07:20:00 (7 * 3600 + 20 * 60)

func get_base_seconds() -> float:
	if is_instance_valid(source) and ("minute" in source):
		return float(source.minute) * 60.0
	return BASE_SECONDS

static func format_time(seconds: float, base_seconds: float = BASE_SECONDS) -> String:
	var total_sec := maxi(0, roundi(base_seconds + seconds))
	var hours := (total_sec / 3600) % 24
	var mins := (total_sec / 60) % 60
	var secs := total_sec % 60
	return "%02d:%02d:%02d" % [hours, mins, secs]

func refresh() -> void:
	if not is_node_ready(): return
	var available := _has_replay()
	var count: int = source.replay_events.size() if available else 0
	var index: int = source.replay_index if available else 0
	var playing: bool = source.replay_playing if available else false
	reset_button.disabled = not available or (index == 0 and not playing)
	if previous_button:
		previous_button.disabled = not available or index <= 0
	if next_button:
		next_button.disabled = not available or index >= count
	play_button.disabled = not available
	fast_button.disabled = not available
	seek.editable = available
	seek.max_value = maxf(1.0, count)
	if not dragging: seek.set_value_no_signal(index)
	fast_button.set_pressed_no_signal(source.replay_fast if is_instance_valid(source) else false)
	play_button.set("glyph", "pause" if playing else "play")
	play_button.tooltip_text = tr("FA_MIND_066" if playing else "FA_MIND_067" if index >= count and available else "FA_MIND_065") + tr("FA_MIND_085")
	fast_button.tooltip_text = tr("FA_MIND_083" if fast_button.button_pressed else "FA_MIND_084") + tr("FA_MIND_086")
	if is_instance_valid(source) and source.state.event_mode:
		clock_label.text = tr("FA_MIND_104") % [index, count]
	else:
		var base_sec := get_base_seconds()
		clock_label.text = format_time(elapsed_seconds(), base_sec) + " / " + format_time(count * BEAT_SECONDS, base_sec)

