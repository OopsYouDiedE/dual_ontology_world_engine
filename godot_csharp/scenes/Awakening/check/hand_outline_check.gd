extends SceneTree
## Targeted UI regression with explicit state injection; not blind play.
const OUT := "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/remake-chapter2/shared-runtime/"
var failures := 0
var checks := 0
func _initialize() -> void: call_deferred("run")
func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok: failures += 1
	print(("PASS " if ok else "FAIL ") + label)
func visible_text(node: Node) -> String:
	var result := ""
	if node is Control and node.is_visible_in_tree() and (node is Label or node is RichTextLabel or node is Button):
		result += str(node.text) + "\n"
	for child in node.get_children(): result += visible_text(child)
	return result
func run() -> void:
	var holder: Node = load("res://scenes/Main2D.tscn").instantiate()
	var game: Node = holder.get_node("Awakening")
	game.set_meta("skip_intro", true)
	root.add_child(holder)
	for i in 8: await process_frame
	game.jump_to("investigate")
	var content: Dictionary = game.engine.Content()
	check(content.EVIDENCE.E11.experiment == "比对手掌轮廓", "formal assembly E11 experiment caption")
	check(content.HELP_NOTES.E11 == "在痕迹旁比对手掌轮廓：半枚手印，比四个人的手掌都窄。", "formal assembly HELP_NOTES side comparison and preserved size conclusion")
	var story := str(game.STORY.get("lines"))
	check(game.STORY.get("titles").has("help_E11") and story.contains("把手掌停在痕迹旁，没有碰灰") and story.contains("在半枚手印旁摊开手掌"), "runtime STORY has revised help_E11 side comparison")
	check(not story.contains("比手？行") and not story.contains("把巴掌按在"), "runtime STORY excludes old gesture wording")
	game.open_card("E11")
	for i in 8: await process_frame
	check(game.modal_kind == "card" and visible_text(holder).contains("比对手掌轮廓"), "actual E11 card displays revised action button")
	game.close_modal()
	game.engine.SetProgress("experiments", "E11", true)
	game.open_card("E11")
	for i in 8: await process_frame
	check(visible_text(holder).contains(content.HELP_NOTES.E11), "actual E11 result card displays revised HELP_NOTES")
	await RenderingServer.frame_post_draw
	check(root.get_texture().get_image().save_png(OUT + "hand-outline-card.png") == OK, "windowed gl_compatibility screenshot saved")
	print("TOTAL %d checks, %d failed" % [checks, failures])
	quit(0 if failures == 0 else 1)
