extends RefCounted
## Layered action queue with priority scheduling and reservation release (Task 5.12).

enum Priority {
	OBSERVATION = 20,
	NEED = 40,
	COMMITTED = 60,
	PLAYER = 80,
	EMERGENCY = 100
}

signal action_started(action: Dictionary)
signal action_cancelled(action: Dictionary, reason: String)
signal action_finished(action: Dictionary)

var current_action: Dictionary = {}
var queue: Array[Dictionary] = []
var owner_actor: Object = null

func _init(actor: Object = null) -> void:
	owner_actor = actor

func enqueue(action_id: String, priority: int, data: Dictionary = {}, execute_callback: Callable = Callable(), cancel_callback: Callable = Callable()) -> bool:
	var entry := {
		"id": action_id,
		"priority": priority,
		"data": data,
		"execute": execute_callback,
		"cancel": cancel_callback,
		"status": "queued",
		"timestamp": Time.get_ticks_msec()
	}
	
	# Normal observation never preempts or reshuffles higher priority work
	if priority <= Priority.OBSERVATION and not current_action.is_empty() and current_action.priority > Priority.OBSERVATION:
		# Append to end of queue without disturbing committed tasks
		queue.append(entry)
		return true
	
	# Emergency preempts lower priority active actions cleanly
	if priority >= Priority.EMERGENCY and not current_action.is_empty() and current_action.priority < Priority.EMERGENCY:
		cancel_current("preempted_by_emergency")
		current_action = entry
		_start_action(current_action)
		return true
	
	# Insert sorted by priority descending
	var inserted := false
	for i in queue.size():
		if queue[i].priority < priority:
			queue.insert(i, entry)
			inserted = true
			break
	if not inserted:
		queue.append(entry)
	
	if current_action.is_empty():
		advance_next()
	return true

func advance_next() -> Dictionary:
	if not current_action.is_empty():
		return current_action
	if queue.is_empty():
		return {}
	current_action = queue.pop_front()
	_start_action(current_action)
	return current_action

func _start_action(act: Dictionary) -> void:
	act["status"] = "running"
	action_started.emit(act)
	if act.execute.is_valid():
		act.execute.call()

func finish_current() -> void:
	if current_action.is_empty(): return
	var finished = current_action
	finished["status"] = "completed"
	current_action = {}
	action_finished.emit(finished)
	advance_next()

func cancel_current(reason := "cancelled") -> void:
	if current_action.is_empty(): return
	var cancelled = current_action
	cancelled["status"] = "cancelled"
	current_action = {}
	if cancelled.cancel.is_valid():
		cancelled.cancel.call(reason)
	action_cancelled.emit(cancelled, reason)
	advance_next()

func cancel_by_id(action_id: String, reason := "cancelled") -> bool:
	if not current_action.is_empty() and current_action.id == action_id:
		cancel_current(reason)
		return true
	for i in range(queue.size() - 1, -1, -1):
		if queue[i].id == action_id:
			var cancelled = queue[i]
			queue.remove_at(i)
			if cancelled.cancel.is_valid():
				cancelled.cancel.call(reason)
			action_cancelled.emit(cancelled, reason)
			return true
	return false

func clear_all(reason := "cleared") -> void:
	if not current_action.is_empty():
		cancel_current(reason)
	while not queue.is_empty():
		var cancelled = queue.pop_front()
		if cancelled.cancel.is_valid():
			cancelled.cancel.call(reason)
		action_cancelled.emit(cancelled, reason)

func get_current() -> Dictionary:
	return current_action

func get_queue_size() -> int:
	return queue.size()
