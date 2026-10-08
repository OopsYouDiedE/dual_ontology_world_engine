extends "res://scenes/Awakening/check/chapter1_fixture.gd"
## 第二章自动检查：复用 Main 夹具，代按正式 UI；只读状态使用 ChapterProbe。
## 不手动回执引擎事件，不重建继承存档；这不是游玩验证。

const Autoplay := preload("res://scenes/Shared/check/chapter_autoplay.gd")
const CAMPUS_VIEW := "res://scenes/Chapter2/Field/CampusMapView.gd"
var auto: RefCounted
var latest_lines: Array[String] = []
var latest_ids: Dictionary = {}
var seen_ids: Dictionary = {}
var choose: Callable = func(options: Array) -> String:
	for option in options:
		if bool(option.get("available", true)): return str(option.id)
	return ""
var _last_line

func prepare() -> Node:
	await fresh()
	auto = Autoplay.new(host)
	return host

func clear_observed() -> void:
	latest_lines.clear()
	latest_ids.clear()
	_last_line = null

func _observe() -> void:
	var d = host.ui.dialogue
	if not d.active or d.line == null or d.busy or d.line == _last_line: return
	_last_line = d.line
	latest_lines.append(str(d.line.text))
	var id := str(d.line.source.get("line_id", ""))
	if id != "":
		latest_ids[id] = true
		seen_ids[id] = true

func saw_sequence(id: String, latest := false) -> bool:
	var ids := latest_ids if latest else seen_ids
	return ids.keys().any(func(key): return str(key).begins_with("chapter2." + id + "."))

func campus_loaded() -> bool:
	return is_instance_valid(host.field) and is_instance_valid(host.field.adapter) \
		and host.field.adapter.get_script().resource_path == CAMPUS_VIEW and host.field.player != null

## 等 Main 的事件队列与真实对白回执静止。章节菜单按当前候选的凭证选择。
func settle(resolve_choices := false, seconds := 60.0, until := Callable()) -> bool:
	var deadline := Time.get_ticks_msec() + int(seconds * 1000.0)
	var idle_since := -1
	while Time.get_ticks_msec() < deadline:
		await tree.process_frame
		if not is_instance_valid(host.ui) or not is_instance_valid(host.field):
			on_error.call("Main 缺少 UI 或现场")
			return false
		_observe()
		if host.ui.modal_kind == "form" and str(host.ui._modal_event.get("fields", [{}])[0].get("id", "")) == "name":
			name_form_answer("flow2-fixture")
		elif host.ui.dialogue.active:
			auto.step()
		elif host.ui.modal_kind == "card":
			if not select("close"):
				on_error.call("证物卡没有 close 候选")
				return false
		elif host.ui.modal_kind == "menu" and resolve_choices:
			var options: Array = host.ui._modal_event.get("options", [])
			var picked := str(choose.call(options))
			if picked == "" or not select(picked):
				on_error.call("菜单没有计划中的可用候选：" + str(options))
				return false
		elif host.ui.modal_kind == "playback" and is_instance_valid(host.ui._campus):
			# 和玩家一样按 Escape 略过已开始的回放，由正式 UI 完成回执。
			var key := InputEventKey.new()
			key.keycode = KEY_ESCAPE
			key.pressed = true
			host.ui._campus._input(key)
		if host._draining or not host._queue.is_empty() or host.ui.dialogue.active or host.ui.modal_kind == "playback" or _walking() \
			or (until.is_valid() and not until.call()):
			idle_since = -1
		elif idle_since < 0: idle_since = Time.get_ticks_msec()
		elif Time.get_ticks_msec() - idle_since >= 100: return true
	on_error.call("Main 流程未在 %.0f 秒内静止：stage=%s modal=%s draining=%s queue=%d" % [seconds, stage(), host.ui.modal_kind, host._draining, host._queue.size()])
	return false

func send(input: Dictionary, resolve_choices := false) -> bool:
	clear_observed()
	host.dispatch(input)
	return await settle(resolve_choices)

## 只用于自动检查的换图；load_map 发出的 move 仍经过 Main.dispatch。
func move_to_map(key: String, spawn: String) -> bool:
	var path := "res://scenes/Chapter2/Maps/%s.tscn" % key
	if not ResourceLoader.exists(path) or (load(path) as PackedScene) == null:
		await _fatal("无法加载第二章地图：" + path)
		return false
	host.field.load_map(key, spawn)
	await physics(4)
	return await settle()

func move_to_person(who: String, spawns: Dictionary) -> bool:
	for person in host.ui.snapshot.get("people", []):
		if str(person.key) != who: continue
		var key := str(person.get("map", ""))
		if key == "": return false
		if not await move_to_map(key, str(spawns.get(key, ""))): return false
		var target: CharacterBody2D = host.field.actors.get(who)
		if target == null: return false
		player().position = target.position + Vector2(24, 0)
		await physics(6)
		return true
	return false
