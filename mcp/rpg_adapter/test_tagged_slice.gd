@tool
extends SceneTree

const AssetDetectorScript = preload("res://addons/rpg_asset_adapter/detector/asset_detector.gd")
const RegexRulesScript = preload("res://addons/rpg_asset_adapter/detector/regex_rules.gd")
const AnimationConverterScript = preload("res://addons/rpg_asset_adapter/converter/animation_converter.gd")

func _init() -> void:
	print("==================================================")
	print("--- TESTING TAGGED SLICE SPECIFICATION ---")
	print("==================================================")

	var success := true
	success = success and _test_regex_matching()
	success = success and _test_asset_detection()
	success = success and _test_slicing_and_sprite_generation()

	if success:
		print("==================================================")
		print(">>> ALL TAGGED SLICE TESTS PASSED! <<<")
		print("==================================================")
		quit(0)
	else:
		push_error("!!! TAGGED SLICE TESTS FAILED !!!")
		quit(1)


func _test_regex_matching() -> bool:
	print("\n[TEST 1] Testing regex matching on prefix tags...")

	# Case 1: Standard 1-digit
	var m1 := RegexRulesScript.regex_search(RegexRulesScript.REGEX_SLICE_TAG, "w_1_h_1_v_4_2")
	if m1 == null:
		push_error("FAIL: Failed to match w_1_h_1_v_4_2")
		return false
	if m1.get_string(1) != "1" or m1.get_string(2) != "1" or m1.get_string(3).to_lower() != "v" or m1.get_string(4) != "4" or m1.get_string(5) != "2":
		push_error("FAIL: Incorrect capture groups for w_1_h_1_v_4_2: %s" % str(m1.strings))
		return false

	# Case 2: Multi-digit (two digits)
	var m2 := RegexRulesScript.regex_search(RegexRulesScript.REGEX_SLICE_TAG, "w_12_h_10_h_16_24")
	if m2 == null:
		push_error("FAIL: Failed to match two-digit w_12_h_10_h_16_24")
		return false
	if m2.get_string(1) != "12" or m2.get_string(2) != "10" or m2.get_string(3).to_lower() != "h" or m2.get_string(4) != "16" or m2.get_string(5) != "24":
		push_error("FAIL: Incorrect multi-digit captures: %s" % str(m2.strings))
		return false

	# Case 3: Tag with trailing name
	var m3 := RegexRulesScript.regex_search(RegexRulesScript.REGEX_SLICE_TAG, "w_1_h_2_v_4_2_Chest_Left_Right_Lit")
	if m3 == null:
		push_error("FAIL: Failed to match w_1_h_2_v_4_2_Chest_Left_Right_Lit")
		return false
	if m3.get_string(1) != "1" or m3.get_string(2) != "2" or m3.get_string(6) != "Chest_Left_Right_Lit":
		push_error("FAIL: Suffix capture incorrect: %s" % str(m3.strings))
		return false

	# Case 4: Non-matching filenames
	if RegexRulesScript.regex_search(RegexRulesScript.REGEX_SLICE_TAG, "other_w_1_h_1_v_4_2") != null:
		push_error("FAIL: Tag not at start should not match")
		return false

	print("  PASSED: Regex matching verified for single and multi-digits from filename start.")
	return true


func _test_asset_detection() -> bool:
	print("\n[TEST 2] Testing AssetDetector integration...")
	var fake_path := "res://AssetsSource/External/Victorian/w_1_h_2_v_4_2_Chest_Side.png"
	var info := AssetDetectorScript.detect_asset(fake_path, AssetDetectorScript.DetectMode.AUTO, 32)

	if info.kind != AssetDetectorScript.AssetKind.TAGGED_ANIMATION:
		push_error("FAIL: Expected kind TAGGED_ANIMATION, got %d" % info.kind)
		return false

	var tag: Dictionary = info.extra.get("slice_tag", {})
	if tag.get("w_tile") != 1 or tag.get("h_tile") != 2 or tag.get("orientation") != "v" or tag.get("frames") != 4 or tag.get("count") != 2:
		push_error("FAIL: Parsed tag data mismatch: %s" % str(tag))
		return false

	print("  PASSED: AssetDetector correctly categorized as TAGGED_ANIMATION with parsed tag metadata.")
	return true


func _test_slicing_and_sprite_generation() -> bool:
	print("\n[TEST 3] Testing slicing and SpriteFrames generation...")
	var source_path := "res://AssetsSource/External/Victorian/w_1_h_2_v_4_2_Treasure_Chest_Animations_Left_Right_Lit.png"
	var tag_data := {
		"w_tile": 1,
		"h_tile": 2,
		"orientation": "v",
		"frames": 4,
		"count": 2,
		"name": "Chest_Side"
	}

	var res := AnimationConverterScript.slice_tagged_animation(source_path, tag_data, 32, 6.0)
	var objs: Array = res.get("objects", [])
	var scenes: Array = res.get("scenes", [])
	var sf: SpriteFrames = res.get("sprite_frames", null)

	if objs.size() != 2:
		push_error("FAIL: Expected 2 sliced objects, got %d" % objs.size())
		return false

	if scenes.size() != 2:
		push_error("FAIL: Expected 2 scenes, got %d" % scenes.size())
		return false

	if sf == null:
		push_error("FAIL: Missing unified SpriteFrames")
		return false

	var anim_names := sf.get_animation_names()
	print("  Unified SpriteFrames animations: %s" % str(anim_names))

	var required_anims := ["closed", "open", "opening", "opened", "close", "closing", "state_0", "state_3", "item_0_open", "item_1_open"]
	for a in required_anims:
		if not sf.has_animation(a):
			push_error("FAIL: Missing expected animation: %s" % a)
			return false

	var sc_dict: Dictionary = scenes[0]
	var packed: PackedScene = sc_dict["scene"]
	var inst := packed.instantiate()
	if inst.get_node_or_null("AnimatedSprite2D") == null:
		push_error("FAIL: Scene missing AnimatedSprite2D")
		inst.free()
		return false
	if inst.get_node_or_null("StaticBody2D") == null:
		push_error("FAIL: Scene missing StaticBody2D")
		inst.free()
		return false
	if inst.get_node_or_null("TriggerArea") == null:
		push_error("FAIL: Scene missing TriggerArea")
		inst.free()
		return false
	inst.free()

	print("  PASSED: Slicing, SpriteFrames, and PackedScene validated successfully.")
	return true
