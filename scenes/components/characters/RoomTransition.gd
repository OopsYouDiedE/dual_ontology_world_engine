extends CanvasLayer
## A presentation-only doorway cover. The host owns room changes and time.
## Await cover(), report completed loading milestones, then await reveal().
signal covered
signal revealed

@export_range(0.0, 1.0, 0.01) var cover_seconds := 0.18
@export_range(0.0, 1.0, 0.01) var reveal_seconds := 0.22
@onready var veil: Control = $Veil
@onready var origin_label: Label = $Veil/Center/Folio/Pages/Origin
@onready var destination_label: Label = $Veil/Center/Folio/Pages/Destination
@onready var stage_label: Label = $Veil/Center/Folio/Pages/Stage
@onready var progress_bar: ProgressBar = $Veil/Center/Folio/Pages/Progress

var active := false
var _from := ""
var _to := ""
var _progress := 0.0
var _stage := ""
var _tween: Tween

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	veil.modulate.a = 0.0
	veil.hide()
	set_process_input(false)

func cover(from_label: String, to_label: String) -> void:
	if not is_node_ready(): await ready
	_stop_tween()
	active = true
	_from = from_label
	_to = to_label
	origin_label.text = "离开 " + _from if not _from.is_empty() else "沿通路前行"
	destination_label.text = "前往 " + _to if not _to.is_empty() else "穿过门扉"
	set_progress(0.0, "正在开门")
	veil.show()
	set_process_input(true)
	_tween = create_tween().set_pause_mode(Tween.TWEEN_PAUSE_PROCESS)
	_tween.tween_property(veil, "modulate:a", 1.0, cover_seconds).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	await _tween.finished
	covered.emit()

func set_progress(value: float, message := "") -> void:
	_progress = clampf(value, 0.0, 1.0)
	if not message.is_empty(): _stage = message
	if is_instance_valid(progress_bar): progress_bar.value = _progress
	if is_instance_valid(stage_label): stage_label.text = _stage

func reveal() -> void:
	if not active: return
	_stop_tween()
	set_progress(1.0, "已抵达")
	# The fully covered frame lets the destination appear behind the cover.
	await get_tree().process_frame
	_tween = create_tween().set_pause_mode(Tween.TWEEN_PAUSE_PROCESS)
	_tween.tween_property(veil, "modulate:a", 0.0, reveal_seconds).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	await _tween.finished
	veil.hide()
	active = false
	set_process_input(false)
	revealed.emit()

func snapshot() -> Dictionary:
	return {"active": active, "from": _from, "to": _to, "progress": _progress, "stage": _stage}

func _stop_tween() -> void:
	if is_instance_valid(_tween): _tween.kill()

func _input(_event: InputEvent) -> void:
	if active: get_viewport().set_input_as_handled()

func _exit_tree() -> void:
	_stop_tween()
