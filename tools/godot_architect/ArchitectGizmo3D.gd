@tool
extends EditorNode3DGizmoPlugin
class_name ArchitectGizmo3D

## ArchitectGizmo3D: 3D Viewport gizmo handles for SmartWall3D, SmartStair3D, and SmartRailing3D.
## Provides intuitive drag handles for wall endpoints, stair rises, and railing anchors.

const HANDLE_COLOR := Color(0.15, 0.75, 1.0, 0.9)
const LINE_COLOR := Color(0.2, 0.9, 0.4, 0.8)
const COLLISION_ALERT_COLOR := Color(1.0, 0.2, 0.2, 0.8)

func _init() -> void:
	create_material("main_lines", LINE_COLOR)
	create_material("handles", HANDLE_COLOR)
	create_handle_material("handles")

func _get_gizmo_name() -> String:
	return "ArchitectGizmo3D"

func _has_gizmo(for_node_3d: Node3D) -> bool:
	return for_node_3d is SmartWall3D or for_node_3d is SmartStair3D or for_node_3d is SmartRailing3D

func _redraw(gizmo: EditorNode3DGizmo) -> void:
	gizmo.clear()
	var node := gizmo.get_node_3d()
	
	if node is SmartWall3D:
		_draw_wall_gizmo(gizmo, node)
	elif node is SmartStair3D:
		_draw_stair_gizmo(gizmo, node)
	elif node is SmartRailing3D:
		_draw_railing_gizmo(gizmo, node)

func _draw_wall_gizmo(gizmo: EditorNode3DGizmo, wall: SmartWall3D) -> void:
	var lines := PackedVector3Array([
		wall.start_point, wall.end_point,
		wall.start_point, wall.start_point + Vector3.UP * wall.wall_height,
		wall.end_point, wall.end_point + Vector3.UP * wall.wall_height,
		wall.start_point + Vector3.UP * wall.wall_height, wall.end_point + Vector3.UP * wall.wall_height
	])
	gizmo.add_lines(lines, get_material("main_lines", gizmo), false)
	
	var handles := PackedVector3Array([wall.start_point, wall.end_point])
	gizmo.add_handles(handles, get_material("handles", gizmo), [])

func _draw_stair_gizmo(gizmo: EditorNode3DGizmo, stair: SmartStair3D) -> void:
	var lines := PackedVector3Array([
		stair.start_point, stair.end_point,
		stair.start_point, stair.start_point + Vector3.UP * (stair.end_point.y - stair.start_point.y),
		stair.start_point + Vector3.UP * (stair.end_point.y - stair.start_point.y), stair.end_point
	])
	gizmo.add_lines(lines, get_material("main_lines", gizmo), false)
	
	var handles := PackedVector3Array([stair.start_point, stair.end_point])
	gizmo.add_handles(handles, get_material("handles", gizmo), [])

func _draw_railing_gizmo(gizmo: EditorNode3DGizmo, railing: SmartRailing3D) -> void:
	if railing.curve == null or railing.curve.point_count < 2:
		return
	var handles := PackedVector3Array()
	for i in range(railing.curve.point_count):
		handles.append(railing.curve.get_point_position(i))
	gizmo.add_handles(handles, get_material("handles", gizmo), [])

func _get_handle_name(gizmo: EditorNode3DGizmo, handle_id: int, secondary: bool) -> String:
	var node := gizmo.get_node_3d()
	if node is SmartWall3D or node is SmartStair3D:
		return "Start" if handle_id == 0 else "End"
	return "ControlPoint_%d" % handle_id

func _get_handle_value(gizmo: EditorNode3DGizmo, handle_id: int, secondary: bool) -> Variant:
	var node := gizmo.get_node_3d()
	if node is SmartWall3D:
		return node.start_point if handle_id == 0 else node.end_point
	if node is SmartStair3D:
		return node.start_point if handle_id == 0 else node.end_point
	if node is SmartRailing3D and node.curve != null:
		return node.curve.get_point_position(handle_id)
	return Vector3.ZERO

func _set_handle(gizmo: EditorNode3DGizmo, handle_id: int, secondary: bool, camera: Camera3D, point: Vector2) -> void:
	var node := gizmo.get_node_3d()
	var ray_origin := camera.project_ray_origin(point)
	var ray_dir := camera.project_ray_normal(point)
	
	# Raycast onto horizontal ground plane Y = 0 (or node Y)
	var plane := Plane(Vector3.UP, node.global_position.y)
	var hit = plane.intersects_ray(ray_origin, ray_dir)
	if hit == null:
		return
	var local_hit: Vector3 = node.global_transform.affine_inverse() * hit
	
	if node is SmartWall3D:
		if handle_id == 0:
			node.start_point = local_hit
		else:
			node.end_point = local_hit
	elif node is SmartStair3D:
		if handle_id == 0:
			node.start_point = local_hit
		else:
			node.end_point = local_hit
	elif node is SmartRailing3D and node.curve != null:
		node.curve.set_point_position(handle_id, local_hit)
	
	gizmo.get_node_3d().update_gizmos()
