@tool
extends Node3D

## 3D Mental Deduction Temporary Visual Layer (Task 7.D5, 7.D6, 7.D8)
## Renders 3D relationship lines between spatial clues and hypotheses.
## Displays red conflict lines for mutual contradictions.
## Strictly temporary; cleanup() clears all nodes and restores original scene without side-effects.

const COLOR_CONNECTION := Color(0.2, 0.8, 0.7, 0.85) # Cyan/teal for supported links
const COLOR_CONFLICT := Color(0.9, 0.2, 0.2, 0.95)   # Red for contradiction links

var _line_mesh_instance: MeshInstance3D
var _immediate_mesh: ImmediateMesh
var _material_connection: StandardMaterial3D
var _material_conflict: StandardMaterial3D
var _spatial_clues: Dictionary = {} # clue_id -> Vector3
var _connections: Array = [] # [{"from": Vector3, "to": Vector3, "is_conflict": bool}]

func _ready() -> void:
	if not Engine.is_editor_hint(): name = "DeductionVisual3D_Layer"
	_setup_materials()
	_line_mesh_instance = MeshInstance3D.new()
	_immediate_mesh = ImmediateMesh.new()
	_line_mesh_instance.mesh = _immediate_mesh
	_line_mesh_instance.name = "DeductionLineMesh"
	add_child(_line_mesh_instance)
	if Engine.is_editor_hint():
		register_spatial_clue("preview", Vector3(0, 1.5, 0))
		set_lines([{}, {"to_id": "preview", "is_conflict": true}])

func _setup_materials() -> void:
	_material_connection = StandardMaterial3D.new()
	_material_connection.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_material_connection.albedo_color = COLOR_CONNECTION
	_material_connection.vertex_color_use_as_albedo = true
	_material_connection.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA

	_material_conflict = StandardMaterial3D.new()
	_material_conflict.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_material_conflict.albedo_color = COLOR_CONFLICT
	_material_conflict.vertex_color_use_as_albedo = true
	_material_conflict.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA

## Registers spatial clue 3D positions
func register_spatial_clue(clue_id: String, pos: Vector3) -> void:
	_spatial_clues[clue_id] = pos

## Sets relationship lines to render in 3D
## lines format:
## [
##   {"from_id": "ev_01", "to_id": "hypo_focus", "is_conflict": false},
##   {"from_id": "ev_01", "to_id": "ev_02", "is_conflict": true}
## ]
func set_lines(lines: Array, default_focus: Vector3 = Vector3.ZERO) -> void:
	_connections.clear()
	for line in lines:
		if not line is Dictionary:
			continue
		var from_id: String = str(line.get("from_id", ""))
		var to_id: String = str(line.get("to_id", ""))
		var is_conflict: bool = bool(line.get("is_conflict", false))
		
		var p_from: Vector3 = _spatial_clues.get(from_id, default_focus + Vector3(-1.0, 0.5, 0.0))
		var p_to: Vector3 = _spatial_clues.get(to_id, default_focus + Vector3(1.0, 0.5, 0.0))
		
		_connections.append({
			"from": p_from,
			"to": p_to,
			"is_conflict": is_conflict
		})
	_redraw_3d_lines()

func _redraw_3d_lines() -> void:
	if _immediate_mesh == null:
		return
	_immediate_mesh.clear_surfaces()
	
	if _connections.is_empty():
		return
		
	# 1. Draw regular connection lines
	var regular_conns: Array = _connections.filter(func(c): return not c.is_conflict)
	if not regular_conns.is_empty():
		_immediate_mesh.surface_begin(Mesh.PRIMITIVE_LINES, _material_connection)
		for conn in regular_conns:
			var p1: Vector3 = conn.from
			var p2: Vector3 = conn.to
			_immediate_mesh.surface_set_color(COLOR_CONNECTION)
			_immediate_mesh.surface_add_vertex(p1)
			_immediate_mesh.surface_set_color(COLOR_CONNECTION)
			_immediate_mesh.surface_add_vertex(p2)
		_immediate_mesh.surface_end()
	
	# 2. Draw contradiction conflict lines (Task 7.D6)
	var conflict_conns: Array = _connections.filter(func(c): return c.is_conflict)
	if not conflict_conns.is_empty():
		_immediate_mesh.surface_begin(Mesh.PRIMITIVE_LINES, _material_conflict)
		for conn in conflict_conns:
			var p1: Vector3 = conn.from
			var p2: Vector3 = conn.to
			var mid: Vector3 = (p1 + p2) * 0.5 + Vector3(0.0, 0.2, 0.0) # slightly arched
			_immediate_mesh.surface_set_color(COLOR_CONFLICT)
			_immediate_mesh.surface_add_vertex(p1)
			_immediate_mesh.surface_set_color(COLOR_CONFLICT)
			_immediate_mesh.surface_add_vertex(mid)
			
			_immediate_mesh.surface_set_color(COLOR_CONFLICT)
			_immediate_mesh.surface_add_vertex(mid)
			_immediate_mesh.surface_set_color(COLOR_CONFLICT)
			_immediate_mesh.surface_add_vertex(p2)
		_immediate_mesh.surface_end()

## Thorough cleanup without leaving residue or orphan nodes (Task 7.D8)
func cleanup() -> void:
	_connections.clear()
	_spatial_clues.clear()
	if _immediate_mesh != null:
		_immediate_mesh.clear_surfaces()
	if is_instance_valid(_line_mesh_instance):
		_line_mesh_instance.queue_free()
	queue_free()
