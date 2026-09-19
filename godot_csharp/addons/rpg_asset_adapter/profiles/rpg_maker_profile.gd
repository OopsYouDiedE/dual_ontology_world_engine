@tool
class_name RpgMakerProfile
extends "res://addons/rpg_asset_adapter/profiles/base_profile.gd"

## Standard RPG Maker asset profile.


func get_profile_name() -> String:
	return "RPG Maker"


func get_door_animation_names() -> Array[String]:
	return ["row_0", "row_1", "row_2", "row_3"]


func get_chest_animation_names() -> Array[String]:
	return ["row_0", "row_1", "row_2", "row_3"]
