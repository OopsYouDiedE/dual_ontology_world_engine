extends RefCounted
## Wait for a presentation signal or cancellation, disconnecting both subscriptions.
class Gate extends RefCounted:
	signal resumed
	var source: Signal
	var cancellation: Signal
	var done := false
	func finish() -> void:
		if done: return
		done = true
		if source.is_connected(finish): source.disconnect(finish)
		if cancellation.is_connected(finish): cancellation.disconnect(finish)
		resumed.emit()

static func until(source_signal: Signal, cancellation_signal: Signal) -> void:
	var gate := Gate.new()
	gate.source = source_signal
	gate.cancellation = cancellation_signal
	source_signal.connect(gate.finish)
	cancellation_signal.connect(gate.finish)
	await gate.resumed
