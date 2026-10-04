extends SceneTree
func _initialize() -> void:
	var original := GDScript.new()
	original.source_code = FileAccess.get_file_as_string("C:/Users/iii/Desktop/dual_ontology_world_engine/tmp/original-awakening-content.gd")
	if original.reload() != OK: quit(1); return
	var extra := GDScript.new()
	extra.source_code = FileAccess.get_file_as_string("C:/Users/iii/Desktop/dual_ontology_world_engine/tmp/final-original-extra-content.gd")
	if extra.reload() != OK: quit(1); return
	var constants: Dictionary = original.get_script_constant_map()
	constants.merge(extra.get_script_constant_map())
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://scenes/Awakening/AwakeningContent.json"))
	var failed := 0
	for key in data:
		if key == "LIFE_TRACES": continue
		var ok := JSON.stringify(constants[key]) == JSON.stringify(data[key])
		if not ok: failed += 1
		print(("PASS " if ok else "FAIL ") + "source text exact migration " + key)
	var original_host := FileAccess.get_file_as_string("C:/Users/iii/Desktop/dual_ontology_world_engine/tmp/final-original-host.gd")
	for key in data.LIFE_TRACES:
		var trace: Dictionary = data.LIFE_TRACES[key]
		var ok := original_host.contains(trace.title) and original_host.contains(trace.picture)
		for line in str(trace.facts).split("\n"): ok = ok and original_host.contains(line)
		if not ok: failed += 1
		print(("PASS " if ok else "FAIL ") + "source original life trace text " + key)
	print("TOTAL %d content checks, %d failed" % [data.size() - 1 + data.LIFE_TRACES.size(), failed])
	quit(0 if failed == 0 else 1)
