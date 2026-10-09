extends SceneTree
## Presentation replay, not a blind playtest or a collision/playthrough acceptance run.
## Requires the production imported resources and a Godot renderer (not --headless).
## Godot --path godot_csharp --script res://tools/capture_player_transcript.gd
## Optional auxiliary screenshots: first review draw_player_transcript_layout.py plans,
## then append -- --screenshots --layout-reviewed. Level-one acceptance needs no screenshots.
## No GameEngine instance, Dispatch call, state injection into Core, or save-file writes.
## Room/near-site contexts lack coordinates: positions are reconstructed from real map
## geometry via the production WorldView's public stage/scene commands.

const UI_SCENE := preload("res://scenes/UI/UISystem.tscn")
const FIELD_SCRIPT := preload("res://scenes/Shared/Field/WorldView.gd")
const VIEW_TRACE := preload("res://scenes/Host/PlayerViewTrace.gd")
const VIEW_RESOURCES := {
	"living": "res://scenes/Awakening/Awakening.tscn",
	"campus": "res://scenes/Chapter2/Field/CampusMapView.tscn",
}
const SOURCE := "res://../artifacts/basic-ui-system/player-traces/engine-full-output.jsonl"
const OUTPUT := "res://../artifacts/basic-ui-system/player-traces/ui-full-output.jsonl"
const SHOTS := "res://../artifacts/basic-ui-system/full-ui/"
const UI_EVENTS := ["lines", "candidates", "card", "form", "ending", "aside", "title",
	"music", "sound", "duck", "close", "interaction_closed", "manuscript"]
const NONBLOCKING_MODALS := ["card", "form", "candidates", "ending"]
# Exact host map-to-world-place relationship from
# scenes/Chapter2/Prototype/Chapter2SessionView.cs, MapPlaces.
# STORY-HARDCODE[CSH-025][schema][tool]: 回放工具硬编码学园场景地图与项目地点的对应关系。
const HOST_MAP_PLACES := {
	"central_hall": "Hall", "lecture": "Lecture", "logistics": "Logistics", "granary": "Logistics", "clinic": "Clinic",
	"greenhouse": "Greenhouse", "archive": "ArchiveFront", "counterweight": "ArchiveFront", "campus_s": "Oath", "living_hall": "DormLife",
	"zone_yi": "DormLife", "zone_jia": "DormW", "zone_bing": "DormS", "zone_ding": "DormL", "resonance": "Resonance",
}
const DEADLINE_SECONDS := 90.0

class ReplayHost extends Node:
	var ui: Node
	var field: Node2D
	var title_layer: CanvasLayer

var capture_viewport: SubViewport
var host: ReplayHost
var observer: Node
var packets: Array = []
var output_file: FileAccess
var source_sequence := 0
var output_index := -1
var snapshot: Dictionary = {}
var _failed := false
var _closing := false
var _present_done := false
var _present_reply: Dictionary = {}
var _control_reply: Dictionary = {}
var _last_visible: Dictionary = {}
var _segments: Array[String] = []
var _visible_frames := 0
var _ending_drawn := false
var _final_ending_drawn := false
var _shots_saved: Dictionary = {}
var _view_resource := ""
var _active_presentation := ""
var _packet_snapshot: Dictionary = {}
var _defer_draw_until := -1
var _started_ms := 0
var _pending_modal: Dictionary = {}
var _pending_modal_index := -1

func _initialize() -> void:
	_started_ms = Time.get_ticks_msec()
	root.hide()
	_run.call_deferred()

func _run() -> void:
	if "--screenshots" in OS.get_cmdline_user_args() and not "--layout-reviewed" in OS.get_cmdline_user_args():
		_fail("The independent operator must run godot_csharp/tools/draw_player_transcript_layout.py, view the layout plans, and pass --layout-reviewed before screenshots.")
		return
	if DisplayServer.get_name() == "headless":
		_fail("A real renderer is required; --headless cannot produce rendered player-visible evidence.")
		return
	if not _read_packets(): return
	var target := ProjectSettings.globalize_path(OUTPUT)
	if DirAccess.make_dir_recursive_absolute(target.get_base_dir()) != OK:
		_fail("Cannot create the output directory.")
		return
	if DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(SHOTS)) != OK:
		_fail("Cannot create the screenshot directory.")
		return
	output_file = FileAccess.open(target, FileAccess.WRITE)
	if output_file == null:
		_fail("Cannot write " + target)
		return
	_write("render_started", {"finished": false, "source": "artifacts/basic-ui-system/player-traces/engine-full-output.jsonl", "packets": packets.size(),
		"capture_type": "production_ui_render_replay", "blind_playtest": false,
		"geometry_acceptance": false, "world_redispatched": false,
		"position_source": "recorded room/near_sites reconstructed using actual adapter geometry",
		"position_reconstruction": "room/near_sites anchors",
		"layout_plans_reviewed_by_operator": "--layout-reviewed" in OS.get_cmdline_user_args(),
		"auxiliary_screenshots": "--screenshots" in OS.get_cmdline_user_args(), "viewport": [1280, 992]})
	TranslationServer.set_locale("zh_CN")
	capture_viewport = SubViewport.new()
	capture_viewport.size = Vector2i(1280, 992)
	capture_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(capture_viewport)
	host = ReplayHost.new()
	host.name = "TranscriptReplay"
	capture_viewport.add_child(host)
	observer = VIEW_TRACE.new()
	host.add_child(observer)
	observer.configure(host, _visible_sink, _input_sink)
	for packet in packets:
		if _failed: break
		source_sequence = int(packet.sequence)
		output_index = -1
		_packet_snapshot = {}
		_defer_draw_until = -1
		for ev in packet.output:
			if str(ev.get("kind", "")) == "state": _packet_snapshot = ev.snapshot
		# The production observer deduplicates frames; a new source packet is itself
		# a provenance boundary even when its rendered field happens to be unchanged.
		observer._last_frame = ""
		await _apply_context(packet.input)
		if _failed: break
		for index in packet.output.size():
			output_index = index
			if index >= _defer_draw_until: _defer_draw_until = -1
			var replacement := _superseding_output(packet.output, index)
			if not replacement.is_empty(): _defer_draw_until = int(replacement.index)
			elif str(packet.output[index].get("kind", "")) in NONBLOCKING_MODALS:
				_defer_draw_until = _modal_draw_boundary(packet.output, index)
			await _output(packet.output[index], replacement)
			if _failed: break
		if _failed: break
		_defer_draw_until = -1
		await _flush_pending_modal()
		if _failed: break
		await _draw()
		_write("packet_rendered", {"source_sequence": source_sequence,
			"output_events": packet.output.size(), "visible_frames": _visible_frames})
	if not _failed:
		if not bool(snapshot.get("ended", false)) or not _final_ending_drawn:
			_fail("Source exhausted without an ended snapshot and a drawn final production ending component.")
		else:
			_write("render_finished", {"finished": true, "source_sequence": source_sequence,
				"ended_component_drawn": true, "ended_displayed": true, "engine_ended": true, "ending_component": "EndingCard",
				"final_panel": str(host.ui.modal_kind), "snapshots_ended": true,
				"capture_type": "production_ui_render_replay", "visible_frames": _visible_frames,
				"screenshots": _shots_saved})
	await _shutdown()
	quit(2 if _failed else 0)

func _read_packets() -> bool:
	var file := FileAccess.open(SOURCE, FileAccess.READ)
	if file == null:
		_fail("Cannot read source packet transcript: " + SOURCE)
		return false
	var previous := 0
	while not file.eof_reached():
		var line := file.get_line().strip_edges()
		if line.is_empty(): continue
		var value: Variant = JSON.parse_string(line)
		if not value is Dictionary or not value.get("output") is Array or not value.get("input") is Dictionary:
			_fail("Malformed source packet after sequence " + str(previous))
			return false
		if int(value.get("sequence", -1)) != previous + 1:
			_fail("Source sequence is missing, duplicated, or unordered.")
			return false
		previous = int(value.sequence)
		packets.append(value)
	if packets.is_empty():
		_fail("Source contains no packets.")
		return false
	return true

func _superseding_output(events: Array, index: int) -> Dictionary:
	if not str(events[index].get("kind", "")) in NONBLOCKING_MODALS: return {}
	for next in range(index + 1, events.size()):
		var later: Dictionary = events[next]
		var kind := str(later.get("kind", ""))
		# Production lines actually yield while being read; its preceding modal
		# remains on screen and must be captured. Scene/travel/playback also yield.
		if kind == "lines" and not later.get("lines", []).is_empty(): return {}
		if kind in ["scene", "travel", "replay_play"]: return {}
		if kind in NONBLOCKING_MODALS or kind in ["view", "home", "close", "interaction_closed"]:
			return {"index": next, "kind": kind, "trace_id": str(later.get("trace_id", "")),
				"presentation_id": str(later.get("presentation_id", ""))}
	return {}

func _modal_draw_boundary(events: Array, index: int) -> int:
	for next in range(index + 1, events.size()):
		var later: Dictionary = events[next]
		var kind := str(later.get("kind", ""))
		if kind == "lines" and not later.get("lines", []).is_empty(): return next
		if kind in ["scene", "travel", "replay_play"]: return next
	return events.size()

func _output(ev: Dictionary, replacement: Dictionary = {}) -> void:
	var kind := str(ev.get("kind", ""))
	_active_presentation = str(ev.get("presentation_id", ""))
	if not replacement.is_empty():
		if not _require_views(kind): return
		# These production presenters return synchronously. The normal host drains
		# the replacement before its next frame; do not make unseen content visible.
		await host.ui.present(ev)
		_write("superseded", {"source_sequence": source_sequence, "output_index": output_index,
			"event_kind": kind, "trace_id": str(ev.get("trace_id", "")),
			"presentation_id": _active_presentation, "replacement": replacement,
			"drawn": false, "reason": "production nonblocking modal replaced before a frame"})
		return
	if kind in ["scene", "travel", "replay_play"] and not _pending_modal.is_empty():
		await _flush_pending_modal()
		if _failed: return
	if kind == "view":
		await _view(ev)
	elif kind == "state":
		snapshot = ev.snapshot
		if not _require_views(kind): return
		host.ui.apply_snapshot(snapshot)
		host.field.apply_snapshot(snapshot)
		await _draw()
	elif kind == "scene" or kind == "stage" or (kind == "replay_play" and str(ev.get("mode", "")) == "stage"):
		if not _require_views(kind): return
		if kind == "scene" and not _map_exists(str(ev.get("map", ""))): return
		if kind == "stage" and _defer_draw_until > output_index:
			# The real host does not await stage. Preserve same-frame modal
			# replacement even if a stage operation starts an asynchronous motion.
			host.field.execute(ev)
		else:
			await host.field.execute(ev)
		await _draw()
	elif kind == "travel":
		if not _require_views(kind): return
		if not _map_exists(str(ev.get("node", {}).get("map", ""))): return
		await host.field.travel_to_node(ev.node)
		await _draw()
	elif kind == "save":
		# GameEngine filters this event before the production GUI. Its recorded data
		# is intentionally neither displayed nor written to user:// in this replay.
		_write("host_event", {"source_sequence": source_sequence, "output_index": output_index,
			"event_kind": "save", "disposition": "nonvisual host event; no save side effect"})
	elif kind in UI_EVENTS or kind == "replay_play":
		if not _require_views(kind): return
		if kind == "lines" and not _packet_snapshot.is_empty():
			# Dispatch has already processed the packet's entire state in the real
			# engine. Its Snapshot is applied to UI here, but not yet to the field.
			host.ui.apply_snapshot(_packet_snapshot)
		if kind == "card" and str(ev.get("picture", "")) != "" and not ResourceLoader.exists(str(ev.picture)):
			_fail("Card picture cannot be rendered: " + str(ev.picture))
			return
		if kind in NONBLOCKING_MODALS:
			await host.ui.present(ev)
			_pending_modal = ev
			_pending_modal_index = output_index
			return
		await _presentation(ev)
	elif kind == "error":
		_fail("Recorded engine error: " + str(ev.get("message", "")))
	else:
		_fail("Unhandled output kind; nothing may be silently omitted: " + kind)

func _view(ev: Dictionary) -> void:
	var requested := str(ev.get("resource", ""))
	_view_resource = requested
	var path := str(VIEW_RESOURCES.get(requested, requested))
	var packed := load(path) as PackedScene
	if packed == null:
		_fail("Cannot load the production view: " + path)
		return
	if is_instance_valid(host.ui):
		host.ui.begin_shutdown()
		host.remove_child(host.ui)
		host.ui.queue_free()
	if is_instance_valid(host.field):
		host.field.begin_shutdown()
		host.remove_child(host.field)
		host.field.queue_free()
	host.ui = UI_SCENE.instantiate()
	host.ui.name = "UI"
	host.add_child(host.ui)
	host.ui.ui_event.connect(_control_event)
	host.field = Node2D.new()
	host.field.set_script(FIELD_SCRIPT)
	host.field.name = "WorldView"
	host.field.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	host.add_child(host.field)
	host.field.ui_busy = host.ui.is_busy
	host.field.fader = host.ui.hud
	host.field.hover_changed.connect(host.ui.show_hover)
	host.field.hint_changed.connect(host.ui.set_hint)
	host.field.setup(packed.instantiate())
	if str(ev.get("map", "")) != "":
		if not _map_exists(str(ev.map)): return
		host.field.load_map(str(ev.map), str(ev.get("spawn", "")))
	var view_snapshot: Dictionary = _packet_snapshot if not _packet_snapshot.is_empty() else snapshot
	if not view_snapshot.is_empty():
		# Host/Main._view invokes _apply(engine.Snapshot()) before drawing.
		host.ui.apply_snapshot(view_snapshot)
		host.field.apply_snapshot(view_snapshot)
	await _draw()
	_write("view_loaded", {"source_sequence": source_sequence, "resource": path})

func _require_views(kind: String) -> bool:
	if is_instance_valid(host.ui) and is_instance_valid(host.field): return true
	_fail("No production UI/field exists for output " + kind)
	return false

func _map_exists(key: String) -> bool:
	if key == "awakening" or ResourceLoader.exists("res://scenes/Chapter2/Maps/%s.tscn" % key): return true
	_fail("Recorded map has no production scene: " + key)
	return false

# STORY-HARDCODE[CSH-026][logic][tool]: 用正式快照中的人物/地点（包括侦探及实际热点）复原场景坐标，只重放不执行剧情答案。
func _apply_context(input: Dictionary) -> void:
	if not is_instance_valid(host.field): return
	var context: Dictionary = input.get("context", {})
	var map_key := str(input.get("map", context.get("map", "")))
	var npc_route := _npc_map_reconstruction(input, map_key)
	if _failed: return
	if not npc_route.is_empty(): map_key = str(npc_route.target_map)
	if map_key != "" and map_key != host.field.adapter.map_key():
		if not _map_exists(map_key): return
		await host.field.execute({"kind": "scene", "map": map_key,
			"spawn": str(npc_route.get("spawn", ""))})
	if not npc_route.is_empty():
		_write("position_reconstruction", {"source_sequence": source_sequence,
			"kind": "same_place_internal_map_to_actual_npc", "target": str(input.target),
			"recorded_map": str(npc_route.recorded_map), "rendered_map": map_key,
			"place": str(npc_route.place), "place_evidence_sequence": int(npc_route.place_evidence_sequence),
			"place_relation_source": "scenes/Chapter2/Prototype/Chapter2SessionView.cs:MapPlaces",
			"target_map_source": "previous original snapshot.people.map",
			"snapshot_modified": false, "npc_fabricated": false, "core_redispatched": false})
	var facts: Dictionary = context.get("actors", {})
	if host.field.adapter.map_key() == "awakening":
		for who in facts:
			if not host.field.actors.has(who):
				_fail("Context actor is absent from the actual view: " + str(who))
				return
			var actor: CharacterBody2D = host.field.actors[who]
			var room := str(facts[who].get("room", ""))
			if room != "" and host.field.adapter.room_of(actor.position) != room:
				var point: Variant = _context_point(facts[who], actor)
				if point == null:
					_fail("Cannot reconstruct a walkable position for recorded room " + room)
					return
				await host.field.execute({"kind": "stage", "op": "place", "who": who,
					"to": {"point": [point.x, point.y]}})
	# A recorded interaction identifies a real hotspot or person; approach its
	# actual point by a public placement command without inventing a world input.
	if str(input.get("kind", "")) == "interact":
		var target: Variant = null
		var site := str(input.get("site", ""))
		if site != "":
			var geometry: Dictionary = host.field.adapter.site_geometry(snapshot.get("sites", {}))
			if geometry.has(site): target = geometry[site].point
		elif str(input.get("target", "")) != "":
			var who := str(input.target)
			if host.field.actors.has(who): target = host.field.actors[who].position
		if target == null:
			_fail("Recorded interaction has no actual map anchor: " + JSON.stringify(input.get("site", input.get("target", ""))))
			return
		var spot: Variant = _near_walkable(target, host.field.player)
		if spot == null:
			_fail("Cannot reconstruct a walkable interaction position.")
			return
		await host.field.execute({"kind": "stage", "op": "place", "who": "detective",
			"to": {"point": [spot.x, spot.y]}})
		if site != "" and host.field.adapter.anchor("site:" + site) != null:
			await host.field.execute({"kind": "stage", "op": "face", "who": "detective", "toward": "site:" + site})
	await _draw()

func _npc_map_reconstruction(input: Dictionary, requested_map: String) -> Dictionary:
	if str(input.get("kind", "")) != "interact" or str(input.get("target", "")) == "" or str(input.get("site", "")) != "": return {}
	if host.field.adapter.map_key() == "awakening": return {}
	var who := str(input.target)
	var person: Dictionary = {}
	for entry in snapshot.get("people", []):
		if str(entry.get("key", "")) == who:
			person = entry
			break
	if person.is_empty():
		_fail("Recorded NPC interaction is absent from the original public snapshot: " + who)
		return {}
	var actual_map := str(person.get("map", ""))
	if actual_map == "" or not bool(person.get("present", true)) or not bool(person.get("displayable", true)):
		_fail("Recorded NPC is not publicly present/displayable on an actual map: " + who)
		return {}
	if requested_map == "": requested_map = str(host.field.adapter.map_key())
	var location := _recorded_map_place(requested_map)
	if _failed: return {}
	var person_place := str(person.get("place", ""))
	var recorded_place := str(location.get("place", ""))
	var actual_place := str(HOST_MAP_PLACES.get(actual_map, ""))
	if recorded_place == "" or person_place == "" or recorded_place != person_place or actual_place != person_place:
		_fail("NPC/map place mismatch; cannot reconstruct " + who + ": recorded map " + requested_map
			+ " belongs to " + recorded_place + ", snapshot NPC map " + actual_map + " belongs to " + actual_place
			+ ", snapshot NPC place is " + person_place)
		return {}
	if requested_map == actual_map: return {}
	# Validate that the publicly stated person place actually points to the
	# person's native map, rather than using the shared place as a free teleport.
	var nodes: Dictionary = snapshot.get("campus", {}).get("nodes", {})
	var node: Dictionary = nodes.get(person_place, {})
	var native_location := _recorded_map_place(actual_map)
	if _failed: return {}
	if str(node.get("map", "")) != actual_map and str(native_location.get("place", "")) != person_place:
		_fail("NPC public map/place has no matching recorded or campus-map relationship: " + who)
		return {}
	if not _map_exists(actual_map): return {}
	return {"recorded_map": requested_map, "target_map": actual_map, "place": person_place,
		"spawn": str(node.get("spawn", "")), "place_evidence_sequence": int(location.sequence)}

func _recorded_map_place(map_key: String) -> Dictionary:
	var host_place := str(HOST_MAP_PLACES.get(map_key, ""))
	# Verify prior raw location evidence against the actual host's public map
	# relationship. Internal maps such as granary retain the place Logistics.
	for index in range(mini(source_sequence, packets.size()) - 1, -1, -1):
		var packet: Dictionary = packets[index]
		var input: Dictionary = packet.input
		if str(input.get("kind", "")) == "move" and str(input.get("map", "")) == map_key and str(input.get("place", "")) != "":
			if str(input.place) != host_place:
				_fail("Recorded map/place differs from the production host MapPlaces: " + map_key + " / " + str(input.place))
				return {}
			return {"place": host_place, "sequence": int(packet.sequence)}
	return {"place": host_place, "sequence": -1}

func _context_point(fact: Dictionary, actor: CharacterBody2D) -> Variant:
	var geometry: Dictionary = host.field.adapter.site_geometry(snapshot.get("sites", {}))
	for site in fact.get("near_sites", []):
		if not geometry.has(site): continue
		var point: Variant = _near_walkable(geometry[site].point, actor)
		if point != null and host.field.adapter.room_of(point) == str(fact.get("room", "")): return point
	return _room_point(str(fact.get("room", "")), actor)

func _room_point(room: String, actor: CharacterBody2D) -> Variant:
	var adapter: Node2D = host.field.adapter
	# Native walkable floor-cell centers and room IDs come from the actual map.
	var candidates: Array[Vector2] = []
	var center := Vector2.ZERO
	for cell in adapter.floors:
		var point := Vector2(cell) * 32.0 + Vector2(16, 16)
		if adapter.room_of(point) == room and adapter.walkable(point, actor):
			candidates.append(point)
			center += point
	if candidates.is_empty(): return null
	center /= candidates.size()
	var nearest: Vector2 = candidates[0]
	for point in candidates:
		if point.distance_squared_to(center) < nearest.distance_squared_to(center): nearest = point
	return nearest

func _near_walkable(point: Vector2, actor: CharacterBody2D) -> Variant:
	var adapter: Node2D = host.field.adapter
	for distance in [32.0, 48.0, 64.0, 16.0, 0.0]:
		for direction in [Vector2.DOWN, Vector2.UP, Vector2.LEFT, Vector2.RIGHT]:
			var spot: Vector2 = point + direction * distance
			if adapter.walkable(spot, actor): return spot
	return null

func _presentation(ev: Dictionary) -> void:
	var kind := str(ev.kind)
	_segments.clear()
	_control_reply = {}
	_present_done = false
	_present_reply = {}
	_start_present(ev)
	if kind == "lines":
		if not ev.get("lines", []).is_empty() and not _pending_modal.is_empty():
			# The real host starts this blocking dialogue while the preceding
			# nonblocking card still exists. Draw both before its recorded reply.
			await _draw()
			await _flush_pending_modal()
			if _failed: return
		await _drive_dialogue(ev)
	else:
		var deadline := Time.get_ticks_msec() + int(DEADLINE_SECONDS * 1000.0)
		while not _present_done and not _failed:
			await _draw()
			if Time.get_ticks_msec() > deadline:
				_fail("Production presentation did not finish: " + kind)
				return
		if _defer_draw_until > output_index: return
		if kind in ["aside", "title"]:
			await _draw()
			if _last_visible.get("texts", []).is_empty(): _fail("Transient UI output produced no visible text.")

func _flush_pending_modal() -> void:
	if _failed or _pending_modal.is_empty(): return
	var ev: Dictionary = _pending_modal
	var saved_index := output_index
	var saved_presentation := _active_presentation
	output_index = _pending_modal_index
	_active_presentation = str(ev.get("presentation_id", ""))
	_pending_modal = {}
	_pending_modal_index = -1
	_segments.clear()
	_control_reply = {}
	observer._last_frame = ""
	await _draw()
	await _sweep(host.ui.modal_root)
	if not _failed:
		var kind := str(ev.kind)
		if kind == "form": await _drive_form(ev)
		elif kind in ["card", "candidates"]: await _drive_modal_choice(ev)
		elif kind == "ending":
			if str(_last_visible.get("panel", "")) != "ending" or _last_visible.get("texts", []).is_empty():
				_fail("Ending component did not yield rendered visible text.")
			else:
				_ending_drawn = true
				if source_sequence == int(packets.back().sequence): _final_ending_drawn = true
				await _capture_stage(ev)
				var navigation := _next_input("navigate", "")
				if not navigation.is_empty():
					await _press_label(_action_label(ev, str(navigation.to)))
					_verify_reply(navigation, _control_reply, ["kind", "to"])
	output_index = saved_index
	_active_presentation = saved_presentation

func _start_present(ev: Dictionary) -> void:
	_present_reply = await host.ui.present(ev)
	_present_done = true

func _drive_dialogue(ev: Dictionary) -> void:
	var dialogue: CanvasLayer = host.ui.dialogue
	var deadline := Time.get_ticks_msec() + int(DEADLINE_SECONDS * 1000.0)
	var previous_serial := -1
	while not _present_done and not _failed:
		await _draw()
		if Time.get_ticks_msec() > deadline:
			_fail("Dialogue controls did not finish presentation " + _active_presentation)
			return
		if not dialogue.active or dialogue.busy or dialogue.line == null: continue
		if dialogue.packet_serial == previous_serial: continue
		previous_serial = dialogue.packet_serial
		_segments.clear()
		observer._last_frame = ""
		if dialogue.body.visible_characters >= 0:
			# This is the production panel-click continue/reveal handler, not a
			# direct assignment of hidden cached text or visible_characters.
			var click := InputEventMouseButton.new()
			click.button_index = MOUSE_BUTTON_LEFT
			click.pressed = true
			dialogue.panel.gui_input.emit(click)
			_write("replay_control", {"source_sequence": source_sequence, "output_index": output_index,
				"control": "DialoguePanel.gui_input", "operation": "production continue/reveal"})
			await _draw()
		await _sweep(dialogue.panel)
		if _failed: return
		if not _text_covered(dialogue.body.get_parsed_text()):
			_fail("A full dialogue line could not be read in rendered visible segments.")
			return
		await _capture_stage(ev)
		if not dialogue.line.responses.is_empty():
			var answer := _next_input("select", str(ev.get("presentation_id", "")))
			if answer.is_empty():
				_fail("Recorded dialogue choice has no source response.")
				return
			var button: Button = null
			for child in dialogue.choices.get_children():
				if child is Button and str(child.get_meta("dialogue_next_id", "")) == str(answer.choice_id): button = child
			await _press(button)
		else:
			await _press(dialogue.next_button)
	if _failed: return
	var expected := _next_input("select" if not ev.get("choices", []).is_empty() else "presented", str(ev.get("presentation_id", "")))
	if not expected.is_empty():
		_verify_reply(expected, _present_reply, ["kind", "presentation_id", "choice_id"])
	_write("presentation_rendered", {"source_sequence": source_sequence, "output_index": output_index,
		"presentation_id": str(ev.get("presentation_id", "")), "event_kind": "lines"})

func _drive_modal_choice(ev: Dictionary) -> void:
	await _capture_stage(ev)
	var answer := _next_input("select", str(ev.get("presentation_id", "")))
	if answer.is_empty():
		_write("presentation_rendered", {"source_sequence": source_sequence, "output_index": output_index,
			"presentation_id": str(ev.get("presentation_id", "")), "event_kind": str(ev.kind),
			"response": "no recorded response; subsequent production presentation replaces this panel"})
		return
	var label := ""
	for option in ev.get("options", []):
		if str(option.id) == str(answer.choice_id): label = str(option.label)
	if label == "":
		_fail("Recorded choice is not present in the rendered event.")
		return
	await _press_label(label)
	_verify_reply(answer, _control_reply, ["kind", "presentation_id", "choice_id"])

func _drive_form(ev: Dictionary) -> void:
	var answer := _next_input("form", str(ev.get("presentation_id", "")))
	if answer.is_empty():
		_fail("Form has no recorded values/action; values may not be invented.")
		return
	var form: Control = host.ui._form
	var values: Dictionary = answer.get("values", {})
	for field in ev.get("fields", []):
		var id := str(field.id)
		if str(ev.get("layout", "")) == "tabs":
			var tab_label := ""
			for action in ev.get("actions", []):
				if str(action.get("tab", "")) == id: tab_label = str(action.label)
			if tab_label == "":
				_fail("A form tab cannot be opened by an actual control: " + id)
				return
			await _press_label(tab_label)
		await _sweep(host.ui.modal_root)
		if not values.has(id):
			_fail("Recorded form response lacks field " + id)
			return
		var edit: LineEdit = form.line_edit_for(id)
		if edit != null:
			await _ensure_visible(edit)
			if _can_focus(edit): edit.grab_focus()
			edit.text = str(values[id])
			edit.text_changed.emit(edit.text)
			await _draw()
		else:
			var button: Button = form.button_for(id, str(values[id]))
			await _press(button)
		if _failed: return
		await _sweep(host.ui.modal_root)
	await _capture_stage(ev)
	await _press_label(_action_label(ev, str(answer.action)))
	_verify_reply(answer, _control_reply, ["kind", "presentation_id", "action", "values"])

func _action_label(ev: Dictionary, id: String) -> String:
	for action in ev.get("actions", []):
		if str(action.id) == id: return str(action.label)
	_fail("Recorded action has no actual button: " + id)
	return ""

func _next_input(kind: String, presentation: String) -> Dictionary:
	# Only future recorded responses are used; output events never synthesize a
	# choice. Presentation IDs allow several queued modal events to be shown.
	for packet in packets:
		if int(packet.sequence) <= source_sequence: continue
		var input: Dictionary = packet.input
		if str(input.get("kind", "")) != kind: continue
		if presentation != "" and str(input.get("presentation_id", "")) != presentation: continue
		return input
	return {}

func _control_event(event: Dictionary) -> void:
	_control_reply = event.duplicate(true)

func _verify_reply(expected: Dictionary, actual: Dictionary, keys: Array) -> void:
	if _failed: return
	for key in keys:
		if expected.has(key) and expected[key] != actual.get(key):
			_fail("Actual UI control response differs from recorded response at " + str(key))
			return

func _press_label(label: String) -> void:
	if _failed: return
	var buttons: Array[Button] = []
	_find_buttons(host.ui.modal_root, label, buttons)
	if buttons.size() != 1:
		_fail("Rendered action button is missing or ambiguous: " + label)
		return
	await _press(buttons[0])

func _find_buttons(node: Node, label: String, found: Array[Button]) -> void:
	if not is_instance_valid(node): return
	if node is CanvasItem and not node.is_visible_in_tree(): return
	if node is Button and node.text == label: found.append(node)
	for child in node.get_children(): _find_buttons(child, label, found)

func _press(button: Button) -> void:
	if _failed: return
	if not is_instance_valid(button) or not button.is_visible_in_tree() or button.disabled:
		_fail("Cannot activate a missing, hidden, or disabled production control.")
		return
	await _ensure_visible(button)
	if _failed: return
	if _can_focus(button): button.grab_focus()
	button.pressed.emit()
	await _draw()

func _can_focus(control: Control) -> bool:
	if not control.is_inside_tree() or not control.is_visible_in_tree() or control.focus_mode == Control.FOCUS_NONE: return false
	# Newer Godot versions include inherited focus-behavior restrictions. Use
	# their effective focus mode when available, without requiring that version.
	if control.has_method("get_focus_mode_with_override"):
		return int(control.call("get_focus_mode_with_override")) != Control.FOCUS_NONE
	return true

func _ensure_visible(control: Control) -> void:
	var ancestor := control.get_parent()
	while ancestor != null:
		if ancestor is ScrollContainer:
			ancestor.ensure_control_visible(control)
		ancestor = ancestor.get_parent()
	await _draw()
	var bounds := control.get_global_rect()
	var clip := Rect2(Vector2.ZERO, Vector2(capture_viewport.size))
	ancestor = control.get_parent()
	while ancestor != null:
		if ancestor is Control and (ancestor.clip_contents or ancestor is ScrollContainer): clip = clip.intersection(ancestor.get_global_rect())
		ancestor = ancestor.get_parent()
	if not clip.encloses(bounds): _fail("Production control cannot fit fully into its visible clipping region.")

func _sweep(node: Node) -> void:
	if _failed or not is_instance_valid(node): return
	await _draw()
	var scrolls: Array[Range] = []
	_collect_scrolls(node, scrolls)
	for bar in scrolls:
		if not is_instance_valid(bar): continue
		var bottom := maxf(bar.min_value, bar.max_value - bar.page)
		bar.value = bar.min_value
		await _draw()
		var step := maxf(1.0, bar.page * 0.7)
		while bar.value < bottom - 0.5:
			var previous := bar.value
			bar.value = minf(bottom, bar.value + step)
			await _draw()
			if bar.value <= previous:
				_fail("Production scrollbar could not expose the rest of a page.")
				return
		bar.value = bar.min_value
		await _draw()
	var labels: Array[RichTextLabel] = []
	_collect_labels(node, labels)
	for label in labels:
		if not _text_covered(label.get_parsed_text()):
			_fail("Visible production text could not be completely exposed by scrolling: " + str(host.get_path_to(label)))
			return

func _collect_scrolls(node: Node, found: Array[Range]) -> void:
	if node is CanvasItem and not node.is_visible_in_tree(): return
	if node is ScrollContainer: found.append(node.get_v_scroll_bar())
	if node is RichTextLabel and node.scroll_active: found.append(node.get_v_scroll_bar())
	for child in node.get_children(): _collect_scrolls(child, found)

func _collect_labels(node: Node, found: Array[RichTextLabel]) -> void:
	if node is CanvasItem and not node.is_visible_in_tree(): return
	if node is RichTextLabel and node.visible_characters < 0: found.append(node)
	for child in node.get_children(): _collect_labels(child, found)

func _compact(text: String) -> String:
	return RegEx.create_from_string("\\s+").sub(text, "", true)

func _text_covered(text: String) -> bool:
	var expected := _compact(text)
	if expected == "": return true
	var covered := PackedByteArray()
	covered.resize(expected.length())
	covered.fill(0)
	for segment in _segments:
		var fragment := _compact(segment)
		if fragment == "": continue
		var from := 0
		while true:
			var offset := expected.find(fragment, from)
			if offset < 0: break
			for index in range(offset, offset + fragment.length()): covered[index] = 1
			from = offset + 1
	return not covered.has(0)

func _draw() -> void:
	if _closing or _failed: return
	if _defer_draw_until > output_index: return
	# Layout/container sizes may settle a frame after data or scrollbar changes.
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw

func _visible_sink(data: Dictionary) -> void:
	if _closing or _failed: return
	var captured := data.duplicate(true)
	captured.source_sequence = source_sequence
	captured.output_index = output_index
	captured.capture_type = "production_ui_render_replay"
	_last_visible = captured
	_visible_frames += 1
	for item in captured.get("texts", []):
		var value := str(item.get("text", ""))
		if value != "" and not _segments.has(value): _segments.append(value)
	_write("ui_visible", captured)

func _input_sink(data: Dictionary) -> void:
	if _closing or _failed: return
	var captured := data.duplicate(true)
	captured.source_sequence = source_sequence
	captured.output_index = output_index
	_write("ui_input", captured)

# STORY-HARDCODE[CSH-027][schema][tool]: 可选截图阶段按E1、say字段、support选项及两场景对白识别剧情节点。
func _screenshot_stage(ev: Dictionary) -> String:
	# Match production event structure, never dispatch offsets or translated prose.
	var kind := str(ev.get("kind", ""))
	if kind == "ending" and source_sequence == int(packets.back().sequence):
		return "06-ending"
	if _view_resource == "living":
		if kind == "lines": return "01-wake"
		if kind == "card" and str(ev.get("id", "")) == "E1":
			return "02-living-investigation"
		if kind == "form":
			for field in ev.get("fields", []):
				if str(field.get("id", "")) == "say": return "03-confrontation"
	elif _view_resource == "campus":
		if kind == "lines": return "04-campus"
		if kind == "candidates":
			for option in ev.get("options", []):
				if str(option.get("id", "")).begins_with("support:"): return "05-council"
	return ""

func _capture_stage(ev: Dictionary) -> void:
	if not "--screenshots" in OS.get_cmdline_user_args(): return
	var stage := _screenshot_stage(ev)
	if _failed or stage == "" or _shots_saved.has(stage): return
	await _draw()
	var path := ProjectSettings.globalize_path(SHOTS + stage + ".png")
	var rendered: Image = capture_viewport.get_texture().get_image()
	if rendered == null or rendered.is_empty():
		_fail("The hidden SubViewport did not yield rendered pixels.")
		return
	if rendered.save_png(path) != OK:
		_fail("Cannot save rendered screenshot: " + path)
		return
	_shots_saved[stage] = path
	_write("screenshot", {"stage": stage, "source_sequence": source_sequence, "path": path,
		"panel": str(_last_visible.get("panel", "")), "capture_type": "production_ui_render_replay"})

func _write(kind: String, data: Dictionary) -> void:
	if output_file == null: return
	output_file.store_line(JSON.stringify({"kind": kind, "elapsed_ms": Time.get_ticks_msec() - _started_ms, "data": data}))
	output_file.flush()

func _fail(message: String) -> void:
	if _failed: return
	_failed = true
	_write("render_failed", {"finished": false, "source_sequence": source_sequence,
		"output_index": output_index, "message": message, "ended_component_drawn": _final_ending_drawn})
	push_error(message)
	if host == null: quit(2)

func _shutdown() -> void:
	_closing = true
	if is_instance_valid(observer): observer.begin_shutdown()
	if is_instance_valid(host):
		if is_instance_valid(host.ui): host.ui.begin_shutdown()
		if is_instance_valid(host.field): host.field.begin_shutdown()
		capture_viewport.remove_child(host)
		host.queue_free()
	for frame in range(4): await process_frame
	if output_file != null:
		output_file.flush()
		output_file.close()
