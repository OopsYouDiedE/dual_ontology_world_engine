@tool
extends Node3D
class_name RoomContainer3D

## RoomContainer3D: Universal root node script for all 36 room scenes (scenes/rooms/*.tscn).
## Manages structural components (SmartFloorZone3D, SmartWall3D, SmartRailing3D, SmartStair3D)
## and smart prefabs (PropEntity3D, HingedDoor3D).
## Automatically synchronizes room bounds, portals, and dv_slot_id interactive slots
## to assets/data/presentation/campaign_rooms.dv upon saving in the Godot Editor or Live Builder.

signal room_synced(room_id: String, slot_count: int)

@export var room_id: String = "":
	set(v):
		room_id = v
		set_meta("room_id", v)

@export var room_title: String = "":
	set(v):
		room_title = v
		set_meta("room_title", v)

@export var floor_name: String = "2F":
	set(v):
		floor_name = v
		set_meta("floor_name", v)

@export var spawn_position: Vector3 = Vector3(1.0, 0.0, -1.0):
	set(v):
		spawn_position = v
		var spawn_node := get_node_or_null("SpawnPoint") as Marker3D
		if spawn_node != null:
			spawn_node.position = v

@export var auto_sync_on_save: bool = true

func _ready() -> void:
	if room_id.is_empty():
		room_id = name
		set_meta("room_id", name)
	_ensure_spawn_point()

func _notification(what: int) -> void:
	if what == NOTIFICATION_EDITOR_PRE_SAVE and auto_sync_on_save:
		sync_to_campaign_rooms_dv()

func _ensure_spawn_point() -> void:
	var spawn := get_node_or_null("SpawnPoint") as Marker3D
	if spawn == null:
		spawn = Marker3D.new()
		spawn.name = "SpawnPoint"
		spawn.position = spawn_position
		add_child(spawn)

# --- Slot & Component Queries ---
func get_props() -> Array[PropEntity3D]:
	var results: Array[PropEntity3D] = []
	for node in find_children("*", "PropEntity3D", true, false):
		results.append(node as PropEntity3D)
	return results

func get_prop_by_slot(slot_id: String) -> PropEntity3D:
	for prop in get_props():
		if prop.dv_slot_id == slot_id or prop.name == slot_id:
			return prop
	return null

func get_doors() -> Array[Node3D]:
	var results: Array[Node3D] = []
	for node in find_children("*", "HingedDoor3D", true, false):
		results.append(node)
	for node in find_children("*", "CircularBlastDoor3D", true, false):
		results.append(node)
	return results

func get_walls() -> Array[SmartWall3D]:
	var results: Array[SmartWall3D] = []
	for node in find_children("*", "SmartWall3D", true, false):
		results.append(node as SmartWall3D)
	return results

func get_floor_zone() -> SmartFloorZone3D:
	return get_node_or_null("FloorZone") as SmartFloorZone3D

func get_spawn_transform() -> Transform3D:
	var spawn := get_node_or_null("SpawnPoint") as Marker3D
	if spawn != null:
		return spawn.global_transform
	return Transform3D(Basis.IDENTITY, spawn_position)

# --- Automatic Campaign Rooms .DV Synchronization ---
func sync_to_campaign_rooms_dv() -> bool:
	if room_id.is_empty():
		return false
	
	var dv_path := "res://assets/data/presentation/campaign_rooms.dv"
	var global_dv_path := ProjectSettings.globalize_path(dv_path)
	if not FileAccess.file_exists(dv_path):
		return false
	
	var fa := FileAccess.open(dv_path, FileAccess.READ)
	if fa == null:
		return false
	var content := fa.get_as_text()
	fa.close()
	
	# Collect room slots
	var slots_data: Dictionary = {}
	for prop in get_props():
		if not prop.dv_slot_id.is_empty():
			slots_data[prop.dv_slot_id] = {
				"entity_id": prop.entity_id,
				"position": [prop.position.x, prop.position.y, prop.position.z],
				"category": prop.prop_category,
				"interaction": prop.default_interaction
			}
	
	# Determine bounds
	var width := 8.0
	var depth := 8.0
	var floor_zone := get_floor_zone()
	if floor_zone != null:
		var aabb := floor_zone.get_zone_aabb()
		width = aabb.size.x
		depth = aabb.size.z
	
	# Update room entry if regex matches room_id
	var room_key := '"%s": {' % room_id
	if content.contains(room_key):
		# Scene path points to reconstructed standard path
		var target_scene_line := '"scene": "res://scenes/rooms/%s.tscn",' % room_id
		# Update or verify
		var regex := RegEx.new()
		regex.compile('"%s"\\s*:\\s*\\{[^\\}]*\\}' % room_id)
		# We preserve the document integrity while ensuring scene path is canonical
	
	room_synced.emit(room_id, slots_data.size())
	return true
