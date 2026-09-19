@tool
class_name KrProfile
extends "res://addons/rpg_asset_adapter/profiles/base_profile.gd"

## Kokoro Reflections (KR) asset profile.
## Provides specific animation row naming for doors and chests, plus variant extraction.

const RegexRulesScript = preload("res://addons/rpg_asset_adapter/detector/regex_rules.gd")


func get_profile_name() -> String:
	return "KR (Kokoro Reflections)"


func get_door_animation_names() -> Array[String]:
	return ["closed", "opening", "open", "closing"]


func get_chest_animation_names() -> Array[String]:
	return ["closed", "opening", "open", "opened"]


func extract_variant_info(filename: String) -> Dictionary:
	var stem := filename.get_file().get_basename()
	var m := RegexRulesScript.regex_search(RegexRulesScript.REGEX_VARIANT, stem)
	if m:
		var var_raw := m.get_string(1).to_lower()
		var variant := "Gray" if (var_raw == "grey" or var_raw == "gray") else var_raw.capitalize()
		var start := m.get_start()
		var group := stem.substr(0, start).rstrip("_- ")
		return {
			"variant_group": group,
			"variant": variant
		}
	return {
		"variant_group": stem,
		"variant": ""
	}
