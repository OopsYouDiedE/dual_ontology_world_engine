extends Node3D
const Builder = preload("res://geometry/BuildingFramework.gd")
const Coordinates = preload("res://geometry/DesignCoordinates.gd")
const FirstPerson = preload("res://geometry/FrameworkFirstPerson.gd")
const OpeningClearance = preload("res://geometry/generators/OpeningClearanceDiagnostics.gd")
const FloorPhysics = preload("res://geometry/FloorPhysicsDiagnostics.gd")
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")
const RailingAssembly = preload("res://geometry/RailingAssemblyRenderer.gd")
const SpawnDiagnostics = preload("res://geometry/SpawnPointDiagnostics.gd")
var building: Node3D
var camera: Camera3D
var label: Label
var selected := "1F"
var collision_mode := false
var cutaway := true
var data: Dictionary
var walker: CharacterBody3D
var walking := false
var floor_levels: Dictionary = {}
var panel: VBoxContainer
var door_status := ""
var current_review_door: Node3D
var door_was_moving := false
var door_settled_frame := -1
var clearance_status := ""
var loaded_source_hash := ""
var datum_source_hash := ""
var design_datum := Vector3.INF

func _ready() -> void:
	var reader = load("res://scenes/Host/DvPresentationContent.cs").new()
	var source_file := "95-configurable-building.data.dv"
	var source_id := "space.configurable_building"
	if "--source-framework" in OS.get_cmdline_user_args():
		source_file = "93-main-building-framework.data.dv"
		source_id = "space.main_building_framework"
	var record: Dictionary = reader.ReadPublishedData(source_file,source_id)
	if not record.has("value"):
		push_error("Missing published building framework DV")
		return
	data = record.value
	loaded_source_hash = reader.PublishedDataSha256(source_file)
	if data.has("world_datum_ref"):
		var datum_ref: Dictionary = data.world_datum_ref
		var datum_record: Dictionary = reader.ReadPublishedData(str(datum_ref.file), str(datum_ref.definition))
		if datum_record.has("value") and datum_record.value.has(str(datum_ref.field)):
			var d: Array = datum_record.value[str(datum_ref.field)]
			if d.size() == 3:
				design_datum = Vector3(d[0],d[1],d[2])
				datum_source_hash = reader.PublishedDataSha256(str(datum_ref.file))
	building = Builder.new()
	add_child(building)
	building.build(data)
	var report_name := "floor-study-diagnostics-%s-%s.json" % [OS.get_process_id(), Time.get_ticks_usec()]
	var report_path := ProjectSettings.globalize_path("res://").trim_suffix("/").get_base_dir().path_join("artifacts/acceptance-20261029").path_join(report_name)
	var report := FileAccess.open(report_path, FileAccess.WRITE)
	if report != null:
		print("FLOOR_DIAGNOSTIC_PATH ", report_path)
		report.store_string(JSON.stringify({
			"scope":"study-scene-generated-geometry-diagnostics", "source":source_id, "source_file_sha256":reader.PublishedDataSha256(source_file),
			"content_pack_hash":"unavailable-study-does-not-open-campaign", "engine":Engine.get_version_info(),
			"controller":"not-recorded-by-study", "operation":"load DV and inspect generated floor meshes",
			"source_hashes":{"builder":FileAccess.get_sha256("res://geometry/BuildingFramework.gd"),
				"floor_generator":FileAccess.get_sha256("res://geometry/FloorSurface3D.gd"),
				"diagnostics":FileAccess.get_sha256("res://geometry/MeshPublicationDiagnostics.gd"),
				"wall_generator":FileAccess.get_sha256("res://geometry/generators/WallSurface3D.gd"),
				"ceiling_generator":FileAccess.get_sha256("res://geometry/generators/CeilingSurface3D.gd"),
				"frame_generator":FileAccess.get_sha256("res://geometry/generators/OpeningFrame3D.gd"),
				"door_generator":FileAccess.get_sha256("res://geometry/generators/HingedDoor3D.gd"),
				"stair_generator":FileAccess.get_sha256("res://geometry/generators/StraightStair3D.gd"),
				"coordinate_conversion":FileAccess.get_sha256("res://geometry/DesignCoordinates.gd"),
				"physics_layers":FileAccess.get_sha256("res://geometry/PhysicsLayers.gd"),
				"first_person_controller":FileAccess.get_sha256("res://geometry/FrameworkFirstPerson.gd"),
				"study_scene_controller":FileAccess.get_sha256("res://scenes/Rendering/BuildingFrameworkStudy.gd"),
				"surface_shader":FileAccess.get_sha256("res://rendering/shaders/framework_surface.gdshader")},
			"recorded_unix_time":Time.get_unix_time_from_system(), "build_completed":building.build_completed,
			"floor_collision_counts":building.counts, "reports":building.floor_diagnostics, "wall_reports":building.wall_diagnostics, "ceiling_reports":building.ceiling_diagnostics, "stair_reports":building.stair_diagnostics}, "  "))
	else:
		push_error("Cannot save floor study diagnostics: " + report_path)
	for f in data.floors:
		floor_levels[f.id] = f.z
	var env := WorldEnvironment.new()
	env.environment = Environment.new()
	env.environment.background_mode = Environment.BG_COLOR
	env.environment.background_color = Color("d9e1e8")
	env.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.environment.ambient_light_color = Color("c3d4e3")
	env.environment.ambient_light_energy = 0.65
	env.environment.ssao_enabled = true
	env.environment.ssao_radius = 1.2
	env.environment.ssao_intensity = 2.0
	env.environment.ssil_enabled = true
	add_child(env)
	for light_data in data.get("review_lights",[]):
		var light := OmniLight3D.new()
		light.position = building.point(light_data.position)
		light.omni_range = light_data.range
		light.light_energy = light_data.energy
		light.light_color = Color(light_data.color)
		light.shadow_enabled = true
		light.omni_attenuation = 0.7
		add_child(light)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-55,-30,0)
	sun.light_energy = 1.4
	sun.shadow_enabled = true
	add_child(sun)
	camera = Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.far = 600
	add_child(camera)
	var outline := MeshInstance3D.new()
	outline.name = "DepthNormalOutline"
	var quad := QuadMesh.new()
	quad.size = Vector2(2,2)
	outline.mesh = quad
	outline.extra_cull_margin = 16384
	outline.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var ink := ShaderMaterial.new()
	ink.shader = preload("res://rendering/shaders/framework_outline.gdshader")
	outline.material_override = ink
	add_child(outline)
	var ui := CanvasLayer.new()
	add_child(ui)
	panel = VBoxContainer.new()
	panel.position = Vector2(16,16)
	ui.add_child(panel)
	var row := HBoxContainer.new()
	panel.add_child(row)
	for id in floor_levels.keys() + ["ALL"]:
		var button := Button.new()
		button.text = id
		button.focus_mode = Control.FOCUS_NONE
		button.custom_minimum_size = Vector2(70,40)
		button.pressed.connect(func(): selected=id;walking=false;walker.deactivate();camera.make_current();refresh())
		row.add_child(button)
	for title in ["Collision geometry","Cutaway / full height","Ceiling on / off","Check stair clearance","Measure open doorway","Check coordinates","Check floor physics","Check railing candidate","Check spawn candidates","First person: WASD move; Q/E turn; R/F look; mouse"]:
		var button := Button.new()
		button.text = title
		button.focus_mode = Control.FOCUS_NONE
		button.custom_minimum_size.y = 32
		panel.add_child(button)
		if title.begins_with("Collision"):
			button.pressed.connect(func():collision_mode=not collision_mode;refresh())
		elif title.begins_with("Cutaway"):
			button.pressed.connect(func():cutaway=not cutaway;refresh())
		elif title.begins_with("Ceiling"):
			button.pressed.connect(func():building.ceilings_visible=not building.ceilings_visible;refresh())
		elif title.begins_with("Check stair"):
			button.pressed.connect(check_stair_clearance)
		elif title.begins_with("Measure open"):
			button.pressed.connect(measure_open_doorway)
		elif title.begins_with("Check coordinates"):
			button.pressed.connect(check_coordinates)
		elif title.begins_with("Check floor"):
			button.pressed.connect(check_floor_physics)
		elif title.begins_with("Check railing"):
			button.pressed.connect(check_railing_candidate)
		elif title.begins_with("Check spawn"):
			button.pressed.connect(check_spawn_candidates)
		else:
			button.pressed.connect(func():walking=true;selected="ALL";collision_mode=false;cutaway=false;refresh();walker.activate())
	var rooms_row := HBoxContainer.new()
	panel.add_child(rooms_row)
	for item in [["Hall","F-01","1F"],["Kitchen","F-05","1F"],["Dormitory","R-05","2F"],["Auto dining","F-07","1F"]]:
		var button := Button.new()
		button.text = item[0]
		button.focus_mode = Control.FOCUS_NONE
		button.custom_minimum_size = Vector2(115,36)
		button.pressed.connect(func():inspect_room(item[1],item[2]))
		rooms_row.add_child(button)
	label = Label.new()
	label.add_theme_color_override("font_color",Color.WHITE)
	label.add_theme_color_override("font_shadow_color",Color.BLACK)
	label.add_theme_constant_override("shadow_offset_x",2)
	label.add_theme_constant_override("shadow_offset_y",2)
	label.position = Vector2(16,310)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ui.add_child(label)
	walker = FirstPerson.new()
	walker.name = "ReviewCapsule"
	walker.position = building.point(data.review_spawn)
	add_child(walker)
	walker.configure(data.review_capsule_radius,data.review_capsule_height)
	camera.make_current()
	walking = true
	selected = "ALL"
	cutaway = false
	refresh()
	walker.activate()

func refresh() -> void:
	building.show_floor(selected,collision_mode,cutaway)
	var z: float = floor_levels.get(selected,0)
	var center := Vector3(-22,24,-48) if selected=="4F" else Vector3(0,z,-20)
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 100 if selected=="4F" else 157
	camera.position = center+Vector3(90,150,125)
	camera.look_at(center)
	label.text = "%s | %s\nStatic configured doors and generated furniture have collision. Windows are openings.\nPhysics remains enabled on every floor.\n%s" % [selected,"COLLISION" if collision_mode else "VISIBLE MODEL",str(building.counts.get(selected,building.counts))]

func inspect_room(room_id: String, floor_id: String) -> void:
	walking = false
	walker.deactivate()
	selected = floor_id
	cutaway = true
	collision_mode = false
	refresh()
	camera.make_current()
	for f in data.floors:
		if f.id!=floor_id:continue
		for room in f.rooms:
			if room.id!=room_id:continue
			var r: Array = room.bbox
			var generated: Array = []
			for prop in f.get("furniture",[]):
				if prop.get("room","")==room_id and prop.get("point_source","")=="automatic_author" and prop.kind in ["table","chair"]:
					generated.append(prop)
			if not generated.is_empty():
				var bounds := [INF,INF,-INF,-INF]
				for prop in generated:
					bounds[0] = minf(bounds[0],prop.position[0]-prop.size[0]/2.0)
					bounds[1] = minf(bounds[1],prop.position[1]-prop.size[1]/2.0)
					bounds[2] = maxf(bounds[2],prop.position[0]+prop.size[0]/2.0)
					bounds[3] = maxf(bounds[3],prop.position[1]+prop.size[1]/2.0)
				r = [bounds[0]-0.8,bounds[1]-0.8,bounds[2]+0.8,bounds[3]+0.8]
			var center := Coordinates.to_godot(Vector3((r[0]+r[2])/2.0,(r[1]+r[3])/2.0,f.z))
			camera.size = maxf(r[2]-r[0],r[3]-r[1])*1.5
			camera.position = center+Vector3(9,15,10)
			camera.look_at(center)
			label.text = room.id+" | "+room.name+" | MODEL REVIEW\nGenerated furniture and static door panels have collision; fixed source markers retain source settings."

func check_spawn_candidates() -> void:
	if not data.has("spatial_anchors") or data.spatial_anchors.is_empty():
		clearance_status = "没有出生点候选"
		return
	var reader = load("res://scenes/Host/DvPresentationContent.cs").new()
	var model: Dictionary = reader.ReadRecord("res://Content/world3d.dv", "layouts", "world3d")
	if not model.has_all(["nominal_actor_height","collision_radius"]):
		clearance_status = "缺少角色碰撞尺寸"
		return
	await get_tree().physics_frame
	var reports: Array = []
	var failures := 0
	for spec in data.spatial_anchors:
		var p: Array = spec.position
		var exclusions: Array[RID] = []
		if is_instance_valid(walker): exclusions.append(walker.get_rid())
		var result: Dictionary = SpawnDiagnostics.inspect(building,Vector3(p[0],p[1],p[2]),float(model.nominal_actor_height),float(model.collision_radius),0.01,0.02,PhysicsLayers.SOLID,exclusions)
		result["anchor_id"] = spec.id
		result["expected_floor_id"] = spec.floor_piece_ref
		if str(result.get("floor_object_id", "")) != str(spec.floor_piece_ref): result.valid = false
		if not result.valid: failures += 1
		reports.append(result)
	var report_name := "spawn-candidates-%s-%s.json" % [OS.get_process_id(), Time.get_ticks_usec()]
	var report_path := ProjectSettings.globalize_path("res://").trim_suffix("/").get_base_dir().path_join("artifacts/acceptance-20261029").path_join(report_name)
	var report := FileAccess.open(report_path, FileAccess.WRITE)
	if report == null:
		clearance_status = "出生点报告保存失败"
		return
	report.store_string(JSON.stringify({"scope":"study-spawn-point-candidates", "source_file_sha256":loaded_source_hash,
		"world3d_file_sha256":FileAccess.get_sha256("res://Content/world3d.dv"), "content_pack_hash":"unavailable-study-does-not-open-campaign", "engine":Engine.get_version_info(),
		"controller":"not-recorded-by-study", "operation":"query authored candidate capsule and floor after physics frame",
		"diagnostic_sha256":FileAccess.get_sha256("res://geometry/SpawnPointDiagnostics.gd"),"builder_sha256":FileAccess.get_sha256("res://geometry/BuildingFramework.gd"),
		"failed_count":failures,"reports":reports}, "  "))
	clearance_status = "出生点候选 %d 件，失败 %d；报告 %s" % [reports.size(),failures,report_name]
	print("SPAWN_CANDIDATE_PATH ", report_path)

func check_railing_candidate() -> void:
	if not data.has("railing_assembly_candidate"):
		clearance_status = "没有栏杆候选声明"
		return
	var result: Dictionary = RailingAssembly.create(data.railing_assembly_candidate,building.material(Color("598c85")))
	if result.has("root"):
		result.root.free()
		result.erase("root")
	var report_name := "railing-candidate-%s-%s.json" % [OS.get_process_id(), Time.get_ticks_usec()]
	var report_path := ProjectSettings.globalize_path("res://").trim_suffix("/").get_base_dir().path_join("artifacts/acceptance-20261029").path_join(report_name)
	var report := FileAccess.open(report_path, FileAccess.WRITE)
	if report == null:
		clearance_status = "栏杆候选报告保存失败"
		return
	report.store_string(JSON.stringify({"scope":"study-draft-railing-assembly-validation-only", "source_file_sha256":loaded_source_hash,
		"content_pack_hash":"unavailable-study-does-not-open-campaign", "engine":Engine.get_version_info(), "controller":"not-recorded-by-study",
		"operation":"generate draft members and validate joints and mesh without publication", "renderer_sha256":FileAccess.get_sha256("res://geometry/RailingAssemblyRenderer.gd"),
		"generator_sha256":FileAccess.get_sha256("res://geometry/generators/CurveRailing3D.gd"), "result":result}, "  "))
	clearance_status = "栏杆候选%s；报告 %s" % ["通过组件校验" if result.ok else "未通过组件校验",report_name]
	print("RAILING_CANDIDATE_PATH ", report_path)

func check_floor_physics() -> void:
	if not building.build_completed or building.study_floor_surfaces.is_empty():
		clearance_status = "楼板未完整生成"
		return
	await get_tree().physics_frame
	var results: Array = []
	var failures := 0
	for id in building.study_floor_surfaces:
		var spec: Dictionary = building.study_floor_surfaces[id]
		var r: Array = spec.rect
		var outer := PackedVector2Array([Vector2(r[0],r[1]),Vector2(r[2],r[1]),Vector2(r[2],r[3]),Vector2(r[0],r[3])])
		var holes: Array[PackedVector2Array] = []
		var result: Dictionary = FloorPhysics.inspect(spec.node,outer,holes,spec.elevation,spec.thickness,2.0,0.01,0.005,10000,PhysicsLayers.FLOOR)
		result["floor_id"] = spec.floor_id
		if not result.valid: failures += 1
		results.append(result)
	var void_reports: Array = []
	for void_spec in data.get("voids", []):
		var stair_spec: Dictionary = {}
		for entry in data.effective_stairs:
			if str(entry.id) == str(void_spec.stair_ref): stair_spec = entry; break
		var control_id := str(void_spec.get("diagnostic_control_floor_piece_ref", ""))
		if stair_spec.is_empty() or not building.study_floor_surfaces.has(control_id) or int(void_spec.segment_index) >= stair_spec.segments.size():
			void_reports.append({"valid":false,"void_id":void_spec.id,"reason":"missing-void-or-control-reference"})
			failures += 1
			continue
		var segment: Dictionary = stair_spec.segments[int(void_spec.segment_index)]
		var bounds: Array = segment[str(void_spec.source_field)]
		var ring := PackedVector2Array([Vector2(bounds[0],bounds[1]),Vector2(bounds[2],bounds[1]),Vector2(bounds[2],bounds[3]),Vector2(bounds[0],bounds[3])])
		var control: Dictionary = building.study_floor_surfaces[control_id]
		var cr: Array = control.rect
		var reference := Vector2((float(cr[0])+float(cr[2]))/2.0,(float(cr[1])+float(cr[3]))/2.0)
		var result: Dictionary = FloorPhysics.inspect_void(building,str(void_spec.id),ring,float(control.elevation),2.0,0.005,0.01,10000,PhysicsLayers.FLOOR,reference,control_id)
		if not result.valid: failures += 1
		void_reports.append(result)
	var report_name := "floor-physics-%s-%s.json" % [OS.get_process_id(), Time.get_ticks_usec()]
	var report_path := ProjectSettings.globalize_path("res://").trim_suffix("/").get_base_dir().path_join("artifacts/acceptance-20261029").path_join(report_name)
	var report := FileAccess.open(report_path, FileAccess.WRITE)
	if report == null:
		clearance_status = "楼板物理报告保存失败"
		return
	report.store_string(JSON.stringify({"scope":"study-rectangular-floor-physics", "source_file_sha256":loaded_source_hash,
		"content_pack_hash":"unavailable-study-does-not-open-campaign", "engine":Engine.get_version_info(),
		"controller":"not-recorded-by-study", "operation":"query registered floor bodies after physics frame",
		"builder_sha256":FileAccess.get_sha256("res://geometry/BuildingFramework.gd"),
		"floor_generator_sha256":FileAccess.get_sha256("res://geometry/FloorSurface3D.gd"),
		"diagnostic_sha256":FileAccess.get_sha256("res://geometry/FloorPhysicsDiagnostics.gd"),
		"study_sha256":FileAccess.get_sha256("res://scenes/Rendering/BuildingFrameworkStudy.gd"),
		"physics_layers_sha256":FileAccess.get_sha256("res://geometry/PhysicsLayers.gd"),
		"recorded_unix_time":Time.get_unix_time_from_system(), "floor_count":results.size(),"failed_count":failures,"reports":results,"void_reports":void_reports}, "  "))
	clearance_status = "楼板物理 %d 件，失败 %d；报告 %s" % [results.size(),failures,report_name]
	print("FLOOR_PHYSICS_PATH ", report_path)

func check_coordinates() -> void:
	if not design_datum.is_finite() or not data.has("coordinate_probes"):
		clearance_status = "缺少坐标基准或参考点"
		return
	var results: Array = []
	var all_passed := true
	for probe in data.coordinate_probes:
		var raw: Array = probe.local
		var expected_world_raw: Array = probe.world
		var expected_godot_raw: Array = probe.godot_world
		var local := Vector3(raw[0],raw[1],raw[2])
		var expected_world := Vector3(expected_world_raw[0],expected_world_raw[1],expected_world_raw[2])
		var expected_godot := Vector3(expected_godot_raw[0],expected_godot_raw[1],expected_godot_raw[2])
		var world := Coordinates.local_to_world(local,design_datum)
		var godot := Coordinates.to_godot(world)
		var roundtrip := Coordinates.world_to_local(Coordinates.to_design(godot),design_datum)
		var world_error := world.distance_to(expected_world)
		var godot_error := godot.distance_to(expected_godot)
		var roundtrip_error := roundtrip.distance_to(local)
		var passed := maxf(world_error,maxf(godot_error,roundtrip_error)) <= 0.001
		all_passed = all_passed and passed
		results.append({"id":probe.id,"source":probe.source,"local":raw,"expected_world":expected_world_raw,"expected_godot_world":expected_godot_raw,
			"actual_world":[world.x,world.y,world.z],"actual_godot_world":[godot.x,godot.y,godot.z],"roundtrip_local":[roundtrip.x,roundtrip.y,roundtrip.z],
			"world_error_m":world_error,"godot_error_m":godot_error,"roundtrip_error_m":roundtrip_error,"passed":passed})
	var report_name := "coordinate-diagnostics-%s-%s.json" % [OS.get_process_id(), Time.get_ticks_usec()]
	var report_path := ProjectSettings.globalize_path("res://").trim_suffix("/").get_base_dir().path_join("artifacts/acceptance-20261029").path_join(report_name)
	var report := FileAccess.open(report_path, FileAccess.WRITE)
	if report == null:
		clearance_status = "坐标报告保存失败"
		return
	report.store_string(JSON.stringify({"scope":"study-coordinate-probes", "source_file_sha256":loaded_source_hash, "datum_file_sha256":datum_source_hash,
		"coordinate_module_sha256":FileAccess.get_sha256("res://geometry/DesignCoordinates.gd"),"engine":Engine.get_version_info(),
		"operation":"compare four authored reference points and round trips", "controller":"not-recorded-by-study",
		"content_pack_hash":"unavailable-study-does-not-open-campaign", "tolerance_m":0.001, "passed":all_passed,"probes":results}, "  "))
	clearance_status = "坐标参考点%s；报告 %s" % ["通过" if all_passed else "不通过",report_name]
	print("COORDINATE_DIAGNOSTIC_PATH ", report_path)

func measure_open_doorway() -> void:
	if not is_instance_valid(current_review_door) or current_review_door.moving or current_review_door.motion_blocked or not is_equal_approx(current_review_door.current_angle, float(current_review_door.get_meta("review_open_angle"))) or door_settled_frame < 0 or Engine.get_physics_frames() <= door_settled_frame:
		clearance_status = "门未到开启角或物理状态尚未稳定"
		return
	var spec: Dictionary = current_review_door.get_meta("clearance_measurement")
	var center: Array = spec.center_design
	var axis: Array = spec.lateral_design
	var measured := OpeningClearance.measure(get_world_3d().direct_space_state, str(current_review_door.get_meta("opening_id")), Vector3(center[0],center[1],center[2]), Vector3(axis[0],axis[1],axis[2]), spec.heights_m, float(spec.ray_extent_m), float(current_review_door.get_meta("clear_width")))
	measured["tolerance_m"] = float(spec.tolerance_m)
	measured["door_angle_degrees"] = current_review_door.current_angle
	var report_name := "opening-clearance-%s-%s.json" % [OS.get_process_id(), Time.get_ticks_usec()]
	var report_path := ProjectSettings.globalize_path("res://").trim_suffix("/").get_base_dir().path_join("artifacts/acceptance-20261029").path_join(report_name)
	var report := FileAccess.open(report_path, FileAccess.WRITE)
	if report == null:
		clearance_status = "门洞净宽报告保存失败"
		return
	report.store_string(JSON.stringify({"scope":"study-open-door-clearance", "source_file_sha256":loaded_source_hash,
		"content_pack_hash":"unavailable-study-does-not-open-campaign", "engine":Engine.get_version_info(),
		"controller":"not-recorded-by-study", "operation":"measure open doorway after physics settles",
		"builder_sha256":FileAccess.get_sha256("res://geometry/BuildingFramework.gd"),
		"door_generator_sha256":FileAccess.get_sha256("res://geometry/generators/HingedDoor3D.gd"),
		"measurement_sha256":FileAccess.get_sha256("res://geometry/generators/OpeningClearanceDiagnostics.gd"),
		"recorded_unix_time":Time.get_unix_time_from_system(), "measurement":measured}, "  "))
	clearance_status = "门洞测量%s；报告 %s" % ["完成" if measured.valid else "未完成",report_name]
	print("OPENING_CLEARANCE_PATH ", report_path)

func check_stair_clearance() -> void:
	if building.study_stairs.is_empty():
		clearance_status = "没有声明的梯段"
		return
	var reports: Array = []
	var blocked := 0
	for id in building.study_stairs:
		var stair_node: Node3D = building.study_stairs[id]
		var samples: Array = stair_node.sample_clearance(float(data.review_capsule_height), float(data.review_capsule_radius), float(stair_node.get_meta("clearance_max_spacing")))
		for sample in samples:
			if bool(sample.blocked): blocked += 1
		reports.append({"stair_id":str(id),"samples":samples})
	var report_name := "stair-clearance-%s-%s.json" % [OS.get_process_id(), Time.get_ticks_usec()]
	var report_path := ProjectSettings.globalize_path("res://").trim_suffix("/").get_base_dir().path_join("artifacts/acceptance-20261029").path_join(report_name)
	var report := FileAccess.open(report_path, FileAccess.WRITE)
	if report == null:
		clearance_status = "净空报告保存失败"
		return
	report.store_string(JSON.stringify({"scope":"study-scene-capsule-clearance", "source_file_sha256":loaded_source_hash,
		"content_pack_hash":"unavailable-study-does-not-open-campaign", "engine":Engine.get_version_info(),
		"controller":"not-recorded-by-study", "operation":"sample staircase capsule clearance",
		"builder_sha256":FileAccess.get_sha256("res://geometry/BuildingFramework.gd"),
		"stair_generator_sha256":FileAccess.get_sha256("res://geometry/generators/StraightStair3D.gd"),
		"recorded_unix_time":Time.get_unix_time_from_system(), "blocked_samples":blocked, "reports":reports}, "  "))
	clearance_status = "净空阻挡样本 %d；报告 %s" % [blocked, report_name]
	print("STAIR_CLEARANCE_PATH ", report_path)

func _unhandled_input(event: InputEvent) -> void:
	if not walking or not is_instance_valid(walker) or not event.is_action_pressed("ui_accept"): return
	var nearest: Node3D
	var distance := INF
	for id in building.study_doors:
		var candidate: Node3D = building.study_doors[id]
		var candidate_distance := Vector2(candidate.global_position.x - walker.global_position.x, candidate.global_position.z - walker.global_position.z).length()
		if absf(candidate.global_position.y - (walker.global_position.y - data.review_capsule_height / 2.0)) < 0.4 and candidate_distance < distance:
			distance = candidate_distance
			nearest = candidate
	if nearest == null or distance > float(nearest.get_meta("review_control_radius_m")):
		door_status = "未靠近门的操作范围"
		return
	if nearest.moving:
		door_status = "门正在运动"
		return
	var target: float = float(nearest.get_meta("review_open_angle")) if is_zero_approx(nearest.current_angle) else 0.0
	current_review_door = nearest
	var started: bool = nearest.begin_motion(target, float(nearest.get_meta("review_motion_speed")))
	door_status = "门开始运动" if started else "无法启动门动作"
	get_viewport().set_input_as_handled()

func _physics_process(_delta: float) -> void:
	if not is_instance_valid(walker):return
	if walking:
		if is_instance_valid(current_review_door):
			if door_was_moving and not current_review_door.moving and not current_review_door.motion_blocked:
				door_settled_frame = Engine.get_physics_frames()
			door_was_moving = current_review_door.moving
			if current_review_door.moving:
				door_status = "门正在运动 %.1f°" % current_review_door.current_angle
			elif current_review_door.motion_blocked:
				door_status = "门停止于 %.1f°；阻挡检测：%s" % [current_review_door.current_angle, str(current_review_door.last_blocker)]
			else:
				door_status = "门角度 %.0f°" % current_review_door.current_angle
		panel.visible = Input.mouse_mode != Input.MOUSE_MODE_CAPTURED
		label.position.y = 220 if panel.visible else 16
		label.text = "FIRST PERSON | WASD / arrows move; Q/E turn; R/F look; mouse; Esc releases mouse\nPosition %s | on_floor=%s | contacts=%s\nEnter/Space: study door | %s\n%s" % [walker.position,walker.is_on_floor(),walker.get_slide_collision_count(),door_status,clearance_status]
	else:
		panel.visible = true
		label.position.y = 220
