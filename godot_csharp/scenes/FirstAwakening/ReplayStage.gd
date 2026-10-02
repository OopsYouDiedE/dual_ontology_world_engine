extends Control
## Same room, separate world. Trace visuals come from Core prefix results.
var source: Node2D
var events: Array[String] = []
var frame := 0
var progress := 0.0
var highlight := ""
var figures: Dictionary = {}
var initial_points: Dictionary = {}
var viewport: SubViewport
var world: Node2D
var view_camera: Camera2D
var fixtures: Node2D
var last_frame := -1
var bolt: CanvasItem
var wedge: Polygon2D
var soot: Polygon2D
var outside_bar: Polygon2D
var stop: Node2D
var stop_before := Vector2.ZERO
var stop_now := Vector2.ZERO
var wipe: Line2D
var footprint_marks: Node2D
var sweep_mark: Line2D
var water_stream: Line2D
var focus := Vector2.ZERO
var manual_focus := Vector2.ZERO
var dragging := false
var marker: Line2D
var paths: Dictionary = {}
var caption_font: Font = preload("res://ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf")
const JAPANESE_PRINT_FONT := preload("res://ShareAsset/External/newtegomin/NewTegomin-Regular.ttf")
const FieldNavigation = preload("res://scenes/FirstAwakening/FieldNavigation.gd")
const CLOSED_DORM_DOOR := preload("res://scenes/FirstAwakening/ReplayDormDoor.tscn")
const ACTIONS := ["BraceDoorWithWedge", "RemoveWedge", "CloseDoor", "SlideInnerBolt", "WrapWedge", "MoveStop", "WipeFloor", "OuterBar"]

func _ready() -> void:
	_update_caption_font()
	viewport = $SubViewport
	viewport.world_2d = World2D.new()
	world = $SubViewport/World
	soot = $SubViewport/World/Soot
	wipe = $SubViewport/World/Wipe
	water_stream = $SubViewport/World/WaterStream
	wedge = $SubViewport/World/Wedge
	outside_bar = $SubViewport/World/OutsideBar
	marker = $SubViewport/World/TraceMarker
	if source == null: source = get_tree().current_scene
	var architecture: Node = source.get_node("LifeWingArchitecture").duplicate(0)
	world.add_child(architecture)
	world.move_child(architecture, 0)
	_remove_bodies(architecture)
	fixtures = source.scene_items.duplicate(0)
	_remove_bodies(fixtures)
	world.add_child(fixtures)
	# Living actors and moving doors belong to the field; the replay shows
	# its own figures and closed dormitory doors.
	for child in fixtures.get_children():
		if child is CharacterBody2D or child.name.begins_with("DormitoryDoor"):
			child.free()
	for original in source.scene_items.get_children():
		if original.get_script() == preload("res://scenes/FirstAwakening/RoomDoor.gd"):
			var leaves := CLOSED_DORM_DOOR.instantiate() as Node2D
			leaves.position = original.position
			fixtures.add_child(leaves)
			# Remembered geometry comes from rooms actually seen with their owner.
			# Removing the live door must not reveal its private room in the replay.
			if not source.known_dorms.has(original.dorm_owner):
				for original_cover in original.privacy_covers:
					var cover := original_cover.duplicate(0) as Polygon2D
					cover.visible = true
					leaves.add_child(cover)
	stop = fixtures.get_node("StopBase")
	stop_now = Vector2(source.layout.stop.now[0], source.layout.stop.now[1])
	stop_before = Vector2(source.layout.stop.before[0], source.layout.stop.before[1])
	var door: Node = fixtures.get_node("MechanicalIsolationDoor")
	bolt = door.get_node("InnerBolt")
	water_stream.position = fixtures.get_node("Washstand").position
	# Floor marks lie under the figures; the wedge and outer bar sit on the gate.
	world.move_child(fixtures, wedge.get_index())
	_build_second_phase_marks()
	for actor in $SubViewport/World/FigureTemplates.get_children():
		initial_points[actor.name] = actor.position
		actor.reparent(fixtures)
		actor.play("idle_front")
		figures[actor.name] = actor
	world.move_child(marker, world.get_child_count() - 1)
	view_camera = $SubViewport/ViewCamera
	focus = view_camera.position
	view_camera.position = focus
	view_camera.make_current()
	var drawing: TextureRect = $Drawing
	drawing.texture = viewport.get_texture()

func _build_second_phase_marks() -> void:
	footprint_marks = Node2D.new()
	footprint_marks.name = "FourRoomFootprints"
	world.add_child(footprint_marks)
	world.move_child(footprint_marks, fixtures.get_index())
	# The inspected tracks end at the four thresholds; private interiors stay covered.
	for index in 4:
		var target: Vector2 = source.sites["room_" + str(index)].point + Vector2(0, 28)
		var route := FieldNavigation.route(Vector2(640 + (index - 2) * 8, 932), target, source.floors, source.obstacle_rects, Vector2i(source.layout.rows[0].length(), source.layout.rows.size()))
		for step in range(0, route.size(), 2):
			var heel := Polygon2D.new()
			heel.polygon = PackedVector2Array([Vector2(-1, -3), Vector2(1, -3), Vector2(1, 3), Vector2(-1, 3)])
			heel.position = route[step] + Vector2(index * 2 - 3, 0)
			heel.color = Color(0.23, 0.21, 0.24, lerpf(0.85, 0.18, float(step) / maxf(1.0, route.size() - 1.0)))
			footprint_marks.add_child(heel)
	sweep_mark = Line2D.new()
	sweep_mark.name = "FullDoorSweep"
	sweep_mark.width = 2
	sweep_mark.default_color = Color("59515d")
	for step in 17:
		var angle := float(step) * PI / 32.0
		sweep_mark.add_point(Vector2(606, 960) + Vector2(cos(angle), sin(angle)) * 76)
	world.add_child(sweep_mark)
	world.move_child(sweep_mark, fixtures.get_index())
	footprint_marks.hide()
	sweep_mark.hide()

func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSLATION_CHANGED:
		_update_caption_font()

func _update_caption_font() -> void:
	caption_font = JAPANESE_PRINT_FONT if TranslationServer.get_locale().begins_with("ja") or TranslationServer.get_locale().begins_with("en") else preload("res://ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf")
	$Caption.add_theme_font_override("font", caption_font)

func _remove_bodies(node: Node) -> void:
	for child in node.get_children():
		if child is PhysicsBody2D: child.free()
		else: _remove_bodies(child)

func _state(count: int) -> Dictionary:
	if source.replay_states.is_empty(): return source.bridge.Run([])
	return source.replay_states[clampi(count,0,source.replay_states.size()-1)]

func _locations(count: int) -> Dictionary:
	var points := initial_points.duplicate()
	var current := _state(count)
	if current.has("positions"):
		for person in points:
			var key: String = str(current.positions.get(person,""))
			points[person] = _target_point(key, current, person, points[person])
		return points
	for i in mini(count, events.size()):
		var state := _state(i+1)
		if not state.get("succeeded",false): continue
		match str(state.get("action","")):
			"GoToDoor", "BraceDoorWithWedge": points["音乐家"] = $SubViewport/World/ActionTargets/MusicianDoor.position
			"RemoveWedge": points["侦探"] = $SubViewport/World/ActionTargets/DetectiveWedge.position
			"SlideInnerBolt": points["工程师"] = $SubViewport/World/ActionTargets/EngineerBolt.position
			"WrapWedge": points["侦探"] = $SubViewport/World/ActionTargets/DetectivePaper.position
			"MoveStop": points["音乐家"] = $SubViewport/World/ActionTargets/MusicianStop.position
			"WipeFloor": points["厨师"] = $SubViewport/World/ActionTargets/ChefWipe.position
	return points

func _target_point(key: String, state: Dictionary, person: String, fallback: Vector2) -> Vector2:
	if key in ["outside","outer_bar"] or state.get("sides",{}).get(person,"") == "outside": return initial_points["门外的人"]
	var aliases := {"wedge":"paper", "inner_bolt":"door", "floor":"floor_wipe", "cloth":"wash"}
	if key == "wedge" and state.get("braced",false): key = "door"
	key = aliases.get(key,key)
	if not source.sites.has(key): return fallback
	var at: Vector2 = source.sites[key].point
	var preference: Vector2 = at + {"侦探":Vector2(-48,-12),"工程师":Vector2(48,-12),"厨师":Vector2(-16,-12),"音乐家":Vector2(16,-12)}.get(person,Vector2.ZERO)
	# Pick a walkable standing point rather than placing feet inside a furniture sprite.
	var best := fallback
	var distance := INF
	for y in range(-4,5):
		for x in range(-4,5):
			var cell := Vector2i(floori(at.x / 16)+x,floori(at.y / 16)+y)
			var candidate := Vector2(cell)*16+Vector2(8,8)
			if FieldNavigation.walkable(candidate, source.floors, source.obstacle_rects) and candidate.distance_squared_to(preference) < distance:
				best = candidate
				distance = candidate.distance_squared_to(preference)
	return best

func _process(_delta: float) -> void:
	if viewport == null: return
	if not is_visible_in_tree():
		viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
		return
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	var points := _locations(frame)
	var next := _locations(frame + 1)
	var moving_focus := Vector2.ZERO
	for person in points:
		var actor: AnimatedSprite2D = figures[person]
		if person == "门外的人":
			actor.visible = false
			for index in range(1,mini(frame+2,source.replay_states.size())):
				if source.replay_states[index].get("actor","") == person:
					actor.visible = true
					break
		var start: Vector2 = points[person]
		var finish: Vector2 = next[person]
		var direction := finish - start
		var at := start
		if progress > 0 and direction.length() > 1:
			var key := str(start) + str(finish)
			if not paths.has(key): paths[key] = FieldNavigation.route(start, finish, source.floors, source.obstacle_rects, Vector2i(source.layout.rows[0].length(), source.layout.rows.size()))
			var route: PackedVector2Array = paths[key]
			if route.size() > 1:
				var step := progress * (route.size() - 1)
				var index := mini(int(step), route.size() - 2)
				at = route[index].lerp(route[index + 1], step - index)
				direction = route[index + 1] - route[index]
			else: at = start.lerp(finish, progress)
			moving_focus = at
		actor.position = at.round()
		var facing := "front" if direction.y >= 0 else "back"
		if absf(direction.x) > absf(direction.y): facing = "left"
		actor.flip_h = facing == "left" and direction.x > 0
		actor.play(("walk_" if progress > 0 and start != finish else "idle_") + facing)
	if frame != last_frame:
		last_frame = frame
		var state := _state(frame)
		var ids: Array = state.ids
		if bolt: bolt.visible = state.inner_bolt
		fixtures.get_node("MechanicalIsolationDoor/DoorGap").visible = not state.closed
		wedge.visible = state.get("braced",false)
		soot.visible = ids.has("door-mask")
		outside_bar.visible = state.outer_bar
		wipe.visible = state.get("wiped",false)
		footprint_marks.visible = ids.has("four-room-footprints")
		sweep_mark.visible = ids.has("full-door-sweep")
		water_stream.visible = state.get("valve_open",false)
		stop.position = stop_now if state.get("stop_moved",false) else stop_before
		var active: String = str(state.get("actor",""))
		focus = points[active] if points.has(active) else $SubViewport/World/ViewTargets/Hall.position
	view_camera.position = (manual_focus if manual_focus != Vector2.ZERO else moving_focus if moving_focus != Vector2.ZERO else focus).round()
	marker.visible = not highlight.is_empty()
	marker.position = $SubViewport/World/ClueTargets/Paper.position if highlight.contains("paper-inner-transfer") else $SubViewport/World/ClueTargets/Wipe.position if highlight.contains("wipe-after-stop") else $SubViewport/World/ClueTargets/Door.position
	_update_caption()

func set_view(place: String) -> void:
	manual_focus = {
		"门厅": $SubViewport/World/ViewTargets/Hall.position,
		"宿舍": $SubViewport/World/ViewTargets/Dormitory.position,
		"研习室": $SubViewport/World/ViewTargets/Study.position,
		"备餐间": $SubViewport/World/ViewTargets/Kitchen.position
	}.get(place, Vector2.ZERO)

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed and event.position.y < 384:
			dragging = true
			manual_focus = view_camera.position
			accept_event()
		elif not event.pressed:
			dragging = false
	elif dragging and event is InputEventMouseMotion:
		manual_focus -= event.relative / 2.0
		manual_focus.x = clampf(manual_focus.x, 160.0, 1120.0)
		manual_focus.y = clampf(manual_focus.y, 96.0, 928.0)
		accept_event()

func _update_caption() -> void:
	var caption := ""
	if frame > 0 and frame <= events.size():
		caption = str(_state(frame).get("caption",events[frame-1]))
	caption = caption.split("\n")[0]
	while caption.length() > 1 and caption_font.get_string_size(caption, HORIZONTAL_ALIGNMENT_LEFT, -1, 18).x > 616:
		caption = caption.left(caption.length() - 2) + "…"
	$Caption.text = caption

