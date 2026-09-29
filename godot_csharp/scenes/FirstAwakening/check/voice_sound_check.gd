extends SceneTree

const DIALOGUE_SCRIPT := preload("res://scenes/Story/StoryDialogue.gd")

func _initialize() -> void:
	call_deferred("_test")

func _test() -> void:
	var failures: Array[String] = []
	print("=== Running Dialogue & Audio Verification ===")

	if not root.has_node("GameLocale"):
		var locale_script: GDScript = load("res://scenes/FirstAwakening/LocaleSettings.gd")
		var gl: Node = locale_script.new()
		gl.name = "GameLocale"
		root.add_child(gl)
	if not root.has_node("DialogueManager"):
		var dm_script: GDScript = load("res://addons/dialogue_manager/dialogue_manager.gd")
		var dm: Node = dm_script.new()
		dm.name = "DialogueManager"
		root.add_child(dm)
	await process_frame

	var SCENE: PackedScene = load("res://scenes/FirstAwakening/FirstAwakening.tscn")

	# 1. 验证 5 种语音音频流加载
	for voice in ["voice_detective", "voice_engineer", "voice_chef", "voice_musician", "voice_tube"]:
		var path := "res://ShareAsset/Internal/Characters/voices/%s.wav" % voice
		var stream: AudioStream = load(path)
		if stream == null:
			failures.append("Failed to load voice audio stream: " + path)

	# 2. 验证 StoryDialogue 实例的角色音效分配与逻辑
	var dialogue_node: CanvasLayer = DIALOGUE_SCRIPT.new()
	root.add_child(dialogue_node)
	await process_frame

	var check_voice = func(char_name: String, expected_name: String) -> void:
		var stream: AudioStream = dialogue_node._get_character_voice(char_name)
		if stream == null or not stream.resource_path.ends_with(expected_name + ".wav"):
			failures.append("Voice mismatch for %s: expected %s, got %s" % [char_name, expected_name, str(stream)])

	check_voice.call("侦探", "voice_detective")
	check_voice.call("侦探（心声）", "voice_detective")
	check_voice.call("工程师", "voice_engineer")
	check_voice.call("厨师", "voice_chef")
	check_voice.call("主厨", "voice_chef")
	check_voice.call("音乐家", "voice_musician")
	check_voice.call("铜口", "voice_tube")

	# 3. 验证无角色时的 fallback 干净，绝不出现“中央厅”
	dialogue_node.idle()
	if dialogue_node.speaker.text != "" or dialogue_node.body.text != "":
		failures.append("idle() has unexpected text: speaker='%s', body='%s'" % [dialogue_node.speaker.text, dialogue_node.body.text])

	# 4. 验证等响度音高补偿与多语言音频时长估算
	var comp_high: float = DIALOGUE_SCRIPT.calculate_pitch_loudness_compensation(2.0)
	var comp_low: float = DIALOGUE_SCRIPT.calculate_pitch_loudness_compensation(0.5)
	var comp_unity: float = DIALOGUE_SCRIPT.calculate_pitch_loudness_compensation(1.0)
	if not is_equal_approx(comp_unity, 0.0):
		failures.append("Unity pitch compensation should be 0 dB, got: " + str(comp_unity))
	if comp_high >= 0.0:
		failures.append("Higher pitch (2.0) should attenuate gain (< 0 dB), got: " + str(comp_high))
	if comp_low <= 0.0:
		failures.append("Lower pitch (0.5) should boost gain (> 0 dB), got: " + str(comp_low))
	if not is_equal_approx(comp_high, -3.5) or not is_equal_approx(comp_low, 3.5):
		failures.append("Pitch loudness compensation slope mismatch: high=" + str(comp_high) + ", low=" + str(comp_low))

	# 验证各语言语速估算与音频时长归一化
	var speed_zh: float = DIALOGUE_SCRIPT.get_locale_typing_speed("zh_CN")
	var speed_en: float = DIALOGUE_SCRIPT.get_locale_typing_speed("en")
	var speed_ja: float = DIALOGUE_SCRIPT.get_locale_typing_speed("ja")
	if speed_en <= speed_zh or speed_ja <= speed_zh:
		failures.append("English/Japanese typing speeds must account for character expansion: zh=%.1f, en=%.1f, ja=%.1f" % [speed_zh, speed_en, speed_ja])

	var dur_zh: float = DIALOGUE_SCRIPT.estimate_dialogue_audio_duration("门锁在两小时前自动落下了。", "zh_CN")
	var dur_en: float = DIALOGUE_SCRIPT.estimate_dialogue_audio_duration("The door locks dropped automatically two hours ago.", "en")
	if absf(dur_zh - dur_en) > 0.4:
		failures.append("Audio duration of equivalent sentence across languages diverged excessively: zh=%.2fs, en=%.2fs" % [dur_zh, dur_en])

	# 验证打字机发声机制与等响度增益应用
	dialogue_node.current_voice_stream = dialogue_node._get_character_voice("主厨")
	dialogue_node.line = {"character": "主厨", "text": "你好，世界！这是一段测试台词。"}
	dialogue_node.body.text = dialogue_node.line.text
	dialogue_node.last_voice_char_index = -1
	dialogue_node._play_typewriter_voice(0, 4)
	if dialogue_node.last_voice_char_index < 0:
		failures.append("Typewriter did not trigger sound for non-space characters")
	# 主厨基础增益 (+2.4 dB) 叠加等响度补偿，音量应高于 baseline -6.0 dB
	if dialogue_node.voice_player.volume_db <= -5.0:
		failures.append("Chef voice equal-loudness boost not applied: volume_db=" + str(dialogue_node.voice_player.volume_db))

	# 测试标点与空格跳过
	dialogue_node.body.text = "，，，   "
	dialogue_node.last_voice_char_index = -1
	dialogue_node._play_typewriter_voice(0, 5)
	if dialogue_node.last_voice_char_index != -1:
		failures.append("Typewriter triggered sound on punctuation or spaces")

	dialogue_node.queue_free()

	# 5. 验证整个 Demo 场景实例化并启动音乐，以及角色调查朝向与调查方向
	var scene: Node2D = SCENE.instantiate()
	root.add_child(scene)
	current_scene = scene
	for i in 5:
		await process_frame

	var bgm: AudioStreamPlayer = scene.get_node_or_null("ReasoningMusic")
	if bgm == null:
		failures.append("ReasoningMusic node not found in FirstAwakening scene")
	else:
		print("BGM check: stream=", bgm.stream, " playing=", bgm.playing, " pos=", bgm.get_playback_position())
		if not bgm.playing:
			failures.append("Background music is NOT playing upon demo start!")
		else:
			print("Background music verified: playing=%s, volume_db=%.1f, bus=%s" % [bgm.playing, bgm.volume_db, bgm.bus])

	# 6. 常驻引导已删除：界面不再指示调查方向。
	if scene.get("hud_lead") != null or scene.ui.find_child("InvestigationLead", true, false) != null:
		failures.append("FieldUI should not show an investigation lead")

	# 7. 验证角色调查交互朝向 (Facing Direction)
	var player_actor: CharacterBody2D = scene.player
	var engineer_actor: CharacterBody2D = scene.actors["工程师"]
	player_actor.position = Vector2(400, 420)
	engineer_actor.position = Vector2(450, 420) # 工程师在侦探右侧
	scene._interact("engineer")
	if player_actor.facing != "right":
		failures.append("Detective should face right towards Engineer during investigation, got: " + player_actor.facing)
	if engineer_actor.facing != "left":
		failures.append("Engineer should face left towards Detective during investigation, got: " + engineer_actor.facing)

	# 验证器物调查朝向
	var bed_point: Vector2 = scene._site_point("bed_0")
	player_actor.position = bed_point + Vector2(0, 30) # 侦探在床下方
	scene._interact("bed_0")
	if player_actor.facing != "back":
		failures.append("Detective should face back (up) towards bed during investigation, got: " + player_actor.facing)

	scene.queue_free()

	if failures.is_empty():
		print("VOICE & MUSIC VERIFICATION: PASS (0 failures)")
		quit(0)
	else:
		for f in failures:
			push_error(f)
			print("FAIL: ", f)
		quit(1)
