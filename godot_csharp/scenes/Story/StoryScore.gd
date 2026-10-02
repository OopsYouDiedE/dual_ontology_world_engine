@tool
extends RefCounted
## Authored blocking lives beside dialogue, as comments understood by both editor and host.
const PREFIX := "# @stage "
const DIRECTIONS := ["front", "back", "left", "right"]
const KINDS := ["safe", "inspect", "perform", "cancel"]

static func parse(text: String) -> Dictionary:
	var beats: Array[Dictionary] = []
	var errors: Array[String] = []
	var ids := {}
	var title := ""
	var lines := text.split("\n")
	var triggers := {}
	var pattern := RegEx.new()
	pattern.compile('^do story_beat\\("([^"\\\\]+)"\\)$')
	for index in lines.size():
		var line := lines[index].strip_edges()
		if line.begins_with("~ "): title = line.substr(2).strip_edges()
		if title.begins_with("OBS_") and line.begins_with("do story_"):
			errors.append("第 %d 行：观察记录不能启动剧情调度。" % (index + 1))
		var anchor := pattern.search(line)
		if anchor != null:
			var key := anchor.get_string(1)
			if not triggers.has(key): triggers[key] = []
			triggers[key].append(index + 1)
		if not line.begins_with(PREFIX): continue
		var json := JSON.new()
		if json.parse(line.substr(PREFIX.length())) != OK or not json.data is Dictionary:
			errors.append("第 %d 行：编排必须是单行 JSON 对象。" % (index + 1))
			continue
		var beat: Dictionary = json.data
		var problems := validate(beat)
		for problem in problems: errors.append("第 %d 行：%s" % [index + 1, problem])
		var id := str(beat.get("id", ""))
		if ids.has(id): errors.append("第 %d 行：节点编号重复 %s。" % [index + 1, id])
		ids[id] = true
		beat["_line"] = index + 1
		beat["_title"] = title
		beats.append(beat)
	for beat in beats: beat["_trigger_lines"] = triggers.get(str(beat.get("id", "")), [])
	for id in triggers:
		if not ids.has(id): errors.append("台词调用了未定义的编排节点 %s。" % id)
	return {"beats": beats, "errors": errors}

static func validate(beat: Dictionary) -> Array[String]:
	var errors: Array[String] = []
	var id: Variant = beat.get("id")
	if not id is String or str(id).is_empty() or '"' in str(id) or "\\" in str(id):
		errors.append("id 必须是非空字符串，不含引号或反斜杠。")
	for key in beat:
		if key not in ["id", "tracks", "wait", "mechanism", "require", "label"] and not str(key).begins_with("_"):
			errors.append("未知节点字段 %s。" % key)
	if beat.has("wait") and not beat.wait is bool: errors.append("wait 必须是布尔值。")
	var tracks: Variant = beat.get("tracks", {})
	if not tracks is Dictionary:
		errors.append("tracks 必须是逐人物的对象。")
	else:
		for actor in tracks:
			var track: Variant = tracks[actor]
			if not track is Dictionary:
				errors.append("%s 的轨道必须是对象。" % actor)
				continue
			for key in track:
				if key not in ["path", "speed", "face", "pose", "seat", "stand"]: errors.append("%s：未知轨道字段 %s。" % [actor, key])
			var path: Variant = track.get("path", [])
			if not path is Array:
				errors.append("%s：path 必须是途经点数组。" % actor)
			else:
				for point in path:
					if not point is Array or point.size() != 2:
						errors.append("%s：每个途经点必须是 [x,y]。" % actor)
					elif not _number(point[0]) or not _number(point[1]): errors.append("%s：坐标必须是有限数值。" % actor)
			if track.has("speed") and (not _number(track.speed) or float(track.speed) <= 0): errors.append("%s：speed 必须大于零。" % actor)
			if track.has("face") and track.face not in DIRECTIONS: errors.append("%s：face 必须是 front/back/left/right。" % actor)
			if track.has("pose") and not track.pose is String: errors.append("%s：pose 必须是动画名称。" % actor)
			if track.has("seat") and (not _number(track.seat) or float(track.seat) < 0): errors.append("%s：seat 必须是非负座高。" % actor)
			if track.has("stand") and not track.stand is bool: errors.append("%s：stand 必须是布尔值。" % actor)
			if track.has("seat") and track.get("pose", "idle") != "idle": errors.append("%s：坐姿节点不能同时指定其他pose。" % actor)
	var requirement: Variant = beat.get("require", {})
	if not requirement is Dictionary:
		errors.append("require 必须是对象。")
	else:
		for key in requirement:
			if key != "known": errors.append("未知前提字段 %s。" % key)
		var known: Variant = requirement.get("known", [])
		if not known is Array:
			errors.append("require.known 必须是材料编号数组。")
		else:
			for item in known:
				if not item is String: errors.append("材料编号必须是字符串。")
	if beat.has("mechanism"):
		var command: Variant = beat.mechanism
		if not command is Dictionary:
			errors.append("mechanism 必须是对象。")
		else:
			if command.get("kind", "") not in KINDS: errors.append("机制类型须为 safe/inspect/perform/cancel。")
			for key in command:
				if key not in ["kind", "operation", "actor", "action", "station", "id"] or not command[key] is String:
					errors.append("机制字段 %s 不受支持，或不是字符串。" % key)
			var kind := str(command.get("kind", ""))
			var required: Array = {"safe": ["operation", "actor"], "inspect": ["id"], "perform": ["actor", "action"], "cancel": []}.get(kind, [])
			for key in required:
				if str(command.get(key, "")).is_empty(): errors.append("%s 机制缺少 %s。" % [kind, key])
		if not beat.get("wait", true): errors.append("有机制提交的节点必须等待演员到位（wait=true）。")
	return errors

static func _number(value: Variant) -> bool:
	return (value is int or value is float) and is_finite(float(value))

static func with_beat(text: String, beat: Dictionary, source_line: int = 0) -> String:
	var clean := beat.duplicate(true)
	for key in clean.keys():
		if str(key).begins_with("_"): clean.erase(key)
	var record := PREFIX + JSON.stringify(clean)
	var lines := text.split("\n")
	if source_line > 0 and source_line <= lines.size() and lines[source_line - 1].strip_edges().begins_with(PREFIX):
		lines[source_line - 1] = record
		return "\n".join(lines)
	return text.trim_suffix("\n") + "\n\n" + record + "\n"

static func markdown(text: String) -> String:
	var score := parse(text)
	var actors: Array[String] = []
	for beat in score.beats:
		var tracks: Variant = beat.get("tracks", {})
		if tracks is Dictionary:
			for actor in tracks:
				if str(actor) not in actors: actors.append(str(actor))
	var output := "# 剧情编排查验表\n\n由当前台词源码静态导出；分支不展平，未运行机制或动画，不认证碰撞可达性。\n\n"
	if not score.errors.is_empty():
		return output + "格式问题：\n\n- " + "\n- ".join(score.errors) + "\n\n```text\n" + text.replace("```", "` ` `") + "\n```\n"
	output += "| 节点／台词位置 | " + " | ".join(actors) + " | 同步与机制 |\n"
	output += "| --- | " + " | ".join(actors.map(func(_a): return "---")) + " | --- |\n"
	for beat in score.beats:
		var cells: Array[String] = [_cell("%s · %s · 定义L%d · 调用%s" % [beat.get("id", ""), beat._title, beat._line, str(beat._trigger_lines)])]
		var tracks: Variant = beat.get("tracks", {})
		for actor in actors:
			var track: Variant = tracks.get(actor, {}) if tracks is Dictionary else {}
			var detail := "保持已有状态（未另编排）"
			if track is Dictionary and not track.is_empty():
				detail = "当前实际位置 → " + str(track.get("path", [])) if not track.get("path", []).is_empty() else "留在原位"
				detail += "；速度%s；朝向%s；姿态%s" % [str(track.get("speed", "演员原速")), track.get("face", "保持"), "坐姿" if track.has("seat") else track.get("pose", "idle")]
				if track.has("seat"): detail += "；坐下，座高%s" % track.seat
				if track.get("stand", false): detail += "；起立"
			cells.append(_cell(detail))
		var sync := "所有轨道同时开始；等全员到位" if beat.get("wait", true) else "所有轨道同时开始；台词继续，下一节点先汇合"
		if beat.has("require"): sync += "；前提" + JSON.stringify(beat.require)
		if beat.has("mechanism"): sync += "；到位后请求" + JSON.stringify(beat.mechanism) + "；结果由C#返回"
		cells.append(_cell(sync))
		output += "| " + " | ".join(cells) + " |\n"
	output += "\n## 台词、分支与触发原文\n\n```text\n" + text.replace("```", "` ` `") + "\n```\n"
	return output

static func _cell(value: String) -> String:
	return value.replace("|", "\\|").replace("\n", "<br>")
