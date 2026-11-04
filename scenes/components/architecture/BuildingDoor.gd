extends Node3D
## Authoring output adapter. Main connects requests and supplies accepted door state.
## Opening a mesh never grants passage or changes the Core lock state.
signal interaction_requested(portal_id: String)
signal pose_settled(portal_id: String, opened: bool)

@export var portal_id := ""
@export_range(0.0, 180.0, 1.0) var opening_degrees := 95.0
@export_range(0.0, 2.0, 0.05) var animation_seconds := 0.35
var _opened := false
var _locked := false
var _revision := -1
var _transition: Tween
@onready var _leaf: Node3D = $Leaf
@onready var _shape: CollisionShape3D = $Leaf/Body/Shape

func request_use() -> void:
	interaction_requested.emit(portal_id)

func apply_state(state: Dictionary, immediate := false) -> void:
	if str(state.get("portal_id", portal_id)) != portal_id:
		return
	var revision := int(state.get("revision", _revision + 1))
	if revision < _revision:
		return
	_revision = revision
	_locked = bool(state.get("locked", false))
	_opened = bool(state.get("open", false))
	if _transition != null:
		_transition.kill()
	var target := -deg_to_rad(opening_degrees) if _opened else 0.0
	# Collision follows the accepted closed/open state.
	_shape.set_deferred("disabled", _opened)
	if immediate or animation_seconds <= 0.0:
		_leaf.rotation.y = target
		_finish_pose(revision)
		return
	_transition = create_tween()
	_transition.tween_property(_leaf, "rotation:y", target, animation_seconds).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	_transition.tween_callback(_finish_pose.bind(revision))

func _finish_pose(revision: int) -> void:
	if revision != _revision:
		return
	_shape.set_deferred("disabled", _opened)
	pose_settled.emit(portal_id, _opened)

func presentation_state() -> Dictionary:
	return {"portal_id": portal_id, "open": _opened, "locked": _locked, "revision": _revision}
