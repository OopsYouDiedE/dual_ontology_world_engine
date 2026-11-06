extends RefCounted
## All foreground and offscreen planners share one main-thread physics budget.
## Godot physics queries stay on the main thread; waiting never commits an action.
const SLICE_USEC := 1500
static var _frame := -1
static var _started := 0
static var waits := 0
static var checkpoints := 0

static func checkpoint(tree: SceneTree) -> void:
	checkpoints += 1
	var frame := Engine.get_physics_frames()
	if frame != _frame:
		_frame = frame
		_started = Time.get_ticks_usec()
	while Time.get_ticks_usec() - _started >= SLICE_USEC:
		waits += 1
		await tree.physics_frame
		frame = Engine.get_physics_frames()
		if frame != _frame:
			_frame = frame
			_started = Time.get_ticks_usec()
