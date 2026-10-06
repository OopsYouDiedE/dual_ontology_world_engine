extends SceneTree
## Generated-source / compiled-assembly boundary check; migration parity is checked separately.
const EngineScript := preload("res://scenes/Awakening/AwakeningEngine.cs")
const Content := preload("res://scenes/Awakening/AwakeningContent.gd")
func _initialize() -> void:
	var engine := EngineScript.new()
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://scenes/Awakening/Script/generated_awakening_content.json"))
	var runtime: Dictionary = engine.Content()
	var failed := 0
	for key in data:
		var ok := runtime.has(key) and runtime[key] == data[key]
		if not ok: failed += 1
		print(("PASS " if ok else "FAIL ") + "generated content equals compiled runtime " + key)
	Content.apply_content(runtime)
	for site_id in Content.SITES:
		var ok: bool = runtime.EVIDENCE.has(Content.SITES[site_id].evidence) and runtime.SITE_INTERACTIONS.has(site_id)
		if not ok: failed += 1
		print(("PASS " if ok else "FAIL ") + "host hotspot resolves compiled evidence " + site_id)
	for site_id in Content.EXTRA_SITES:
		if not runtime.SITE_INTERACTIONS.has(site_id): failed += 1
	for trace_id in Content.LIFE_TRACES:
		if Content.LIFE_TRACES[trace_id].facts != runtime.LIFE_TRACES[trace_id].facts: failed += 1
	engine.free()
	print("TOTAL generated-content integration failures: %d" % failed)
	call_deferred("finish", 0 if failed == 0 else 1)

func finish(exit_code: int) -> void:
	await preload("res://scenes/Shared/Audio/Sound.gd").quit_cleanly(exit_code)
