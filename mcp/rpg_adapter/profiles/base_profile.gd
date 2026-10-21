@tool
class_name BaseProfile
extends RefCounted

## Base profile for animation row mapping and metadata extraction.


func get_profile_name() -> String:
	return "Base"


func get_door_animation_names() -> Array[String]:
	return ["row_0", "row_1", "row_2", "row_3"]


func get_chest_animation_names() -> Array[String]:
	return ["row_0", "row_1", "row_2", "row_3"]


func get_door_animation_name(row_index: int) -> String:
	var anims := get_door_animation_names()
	if row_index >= 0 and row_index < anims.size():
		return anims[row_index]
	return "row_%d" % row_index


func get_chest_animation_name(row_index: int) -> String:
	var anims := get_chest_animation_names()
	if row_index >= 0 and row_index < anims.size():
		return anims[row_index]
	return "row_%d" % row_index


func extract_variant_info(filename: String) -> Dictionary:
	return {
		"variant_group": "",
		"variant": ""
	}
