class_name FirstAwakeningEnsemble
extends RefCounted
## FirstAwakeningEnsemble: 管理演员朝向、对白目光跟随、闲聊轮替与进门/会议演出

const ZHAOHUA_FONT: Font = preload("res://ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf")

var scene: Node2D
var actors: Dictionary = {}
var player: CharacterBody2D
var conversation_focus: String = ""
var conversation_look_targets: Dictionary = {}
var ambient_elapsed: float = 2.5
var ambient_speaker_index: int = -1
var ambient_murmurs: Dictionary = {}

func setup(p_scene: Node2D, p_actors: Dictionary, p_player: CharacterBody2D) -> void:
	scene = p_scene
	actors = p_actors
	player = p_player

func is_actor_active(actor: CharacterBody2D) -> bool:
	if scene == null or not is_instance_valid(actor):
		return false
	return scene.call("_conversation_actor_active", actor)

func on_dialogue_line_shown(speaker: String, dialogue: CanvasLayer) -> void:
	for actor in actors.values():
		if not actor.travelling and is_actor_active(actor):
			actor.pose("idle")
	var canonical_speaker := "厨师" if speaker == "主厨" else speaker
	var focus: String = conversation_focus if not conversation_focus.is_empty() else canonical_speaker
	if focus in ["铜口", "transmitter"]:
		face_group("transmitter")
	elif actors.has(focus):
		var target_actor: Node2D = actors[focus]
		for actor in actors.values():
			if actor != target_actor and not actor.travelling and is_actor_active(actor):
				actor.face_point(target_actor.position)
		if conversation_focus.is_empty() and canonical_speaker != "侦探" and focus != "侦探" and not target_actor.travelling:
			target_actor.face_point(player.position)
	for actor in conversation_look_targets:
		if not actor.travelling and is_actor_active(actor):
			actor.face_point(conversation_look_targets[actor].position)
	if actors.has(canonical_speaker) and not actors[canonical_speaker].travelling and is_actor_active(actors[canonical_speaker]):
		actors[canonical_speaker].pose("talk")
	if dialogue.has_method("layout_compact_line"):
		dialogue.layout_compact_line()

func _get_actor_canonical_name(target: CharacterBody2D) -> String:
	for name in actors:
		if actors[name] == target:
			return name
	return ""

func face_group(focus: String) -> void:
	conversation_focus = focus
	conversation_look_targets.clear()
	var point: Vector2
	if focus == "transmitter":
		point = scene.sites["transmitter"].point
	elif actors.has(focus):
		point = actors[focus].position
	else:
		return
	for actor in actors.values():
		if not actor.travelling and is_actor_active(actor):
			actor.face_point(point)

func face_actor(who: String, target: String) -> void:
	var canonical_who: String = "厨师" if who == "主厨" else who
	if not actors.has(canonical_who):
		return
	var actor: CharacterBody2D = actors[canonical_who]
	var target_actor: Node2D
	if target == "transmitter":
		target_actor = scene.scene_items.get_node("CopperTransmitter")
	else:
		var canonical_target: String = "厨师" if target == "主厨" else target
		target_actor = actors.get(canonical_target, null)
	if target_actor != null:
		conversation_look_targets[actor] = target_actor
		actor.face_point(target_actor.position)

func follow_speaker() -> void:
	conversation_focus = ""
	conversation_look_targets.clear()

func update_ambient_discussion(delta: float) -> void:
	ambient_elapsed += delta
	if ambient_elapsed < 2.5:
		if ambient_speaker_index >= 0:
			var speaker: CharacterBody2D = [actors["工程师"], actors["厨师"], actors["音乐家"]][ambient_speaker_index]
			if not speaker.sprite.is_playing():
				speaker.pose("talk")
		return
	ambient_elapsed = 0.0
	ambient_speaker_index = (ambient_speaker_index + 1) % 3
	var party := [actors["工程师"], actors["厨师"], actors["音乐家"]]
	var speaker: CharacterBody2D = party[ambient_speaker_index]
	for actor in party:
		if not ambient_murmurs.has(actor):
			var fragment := Label.new()
			fragment.position = Vector2(-66, -78)
			fragment.size = Vector2(132, 26)
			fragment.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			fragment.add_theme_font_override("font", ZHAOHUA_FONT)
			fragment.add_theme_font_size_override("font_size", 14)
			fragment.add_theme_color_override("font_color", Color(0.93, 0.89, 0.78, 0.8))
			fragment.add_theme_color_override("font_shadow_color", Color(0.03, 0.02, 0.05, 0.55))
			fragment.add_theme_constant_override("shadow_offset_y", 1)
			actor.add_child(fragment)
			ambient_murmurs[actor] = fragment
		if actor != speaker:
			actor.face_point(speaker.position)
		else:
			actor.face_point(party[(ambient_speaker_index + 1) % 3].position)
		actor.pose("talk" if actor == speaker else "idle")
		ambient_murmurs[actor].visible = (actor == speaker)
		ambient_murmurs[actor].text = scene.tr(["FA_UI_358", "FA_UI_359", "FA_UI_360"][ambient_speaker_index])

func stage_arrival() -> void:
	for actor in [actors["厨师"], actors["工程师"], actors["音乐家"]]:
		actor.pose("idle")
		actor.face_point(player.position)
		await scene.get_tree().create_timer(0.3).timeout
	await scene.call("_walk_actor_to", player, scene.get_node("MeetingSpots/Detective").position)
	player.face_point(actors["厨师"].position)

func place_party_at_table() -> void:
	actors["工程师"].position = scene.get_node("MeetingSpots/Engineer").position
	actors["厨师"].position = scene.get_node("MeetingSpots/Chef").position
	actors["音乐家"].position = scene.get_node("MeetingSpots/Musician").position
	actors["工程师"].pose("idle", "right")
	actors["厨师"].pose("idle", "back")
	actors["音乐家"].pose("idle", "left")

func clear_ambient() -> void:
	for fragment in ambient_murmurs.values():
		if is_instance_valid(fragment):
			fragment.visible = false
