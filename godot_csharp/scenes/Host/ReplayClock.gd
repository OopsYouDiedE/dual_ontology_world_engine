extends RefCounted
## The presentation mock uses the same frame shape and event clock as SmartReplay.
## Binary records are read by the production bridge; this clock owns sample data.
var frames: Array = []
var cursor := 0
var remaining := 0.0
var playing := false
var speed := 1.0

func load_frames(value: Array) -> void:
	frames = value.duplicate(true)
	cursor = 0
	remaining = 0.0
	playing = false

func play(value: float) -> void:
	speed = value
	playing = true

func pause() -> void:
	playing = false

func step() -> Dictionary:
	playing = false
	if cursor >= frames.size(): return {}
	var frame: Dictionary = frames[cursor]
	cursor += 1
	remaining = float(frame.duration)
	return frame

func advance(seconds: float) -> Array:
	var ready: Array = []
	if not playing: return ready
	remaining -= seconds * speed
	while remaining <= 0.0 and cursor < frames.size():
		var frame: Dictionary = frames[cursor]
		cursor += 1
		ready.append(frame)
		remaining += float(frame.duration)
	if cursor >= frames.size() and remaining <= 0.0: playing = false
	return ready
