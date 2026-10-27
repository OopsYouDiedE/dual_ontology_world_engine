extends "res://scenes/World/Art/PublicHallE.gd"
const LIVING=preload("res://scenes/World/Art/areas/LivingWings.gd")
const PUBLIC=preload("res://scenes/World/Art/areas/PublicRooms.gd")
const SERVICE=preload("res://scenes/World/Art/areas/ServiceRooms.gd")
const RECORDING=preload("res://scenes/World/Art/areas/RecordingStages.gd")
const KNOWLEDGE=preload("res://scenes/World/Art/RoomKnowledgeView.gd")
const OUTDOORS=preload("res://scenes/World/Art/areas/Outdoors.gd")
const YI_REGION=preload("res://scenes/World/Art/areas/YiRegion.gd")
const HALL_REGION=preload("res://scenes/World/Art/areas/PublicHallRegion.gd")
const INTERIOR_FINISH=preload("res://rendering/InteriorFinish.gd")
const RECORDING_IDS=["shura_poet","scholar_relic","chess_god","demon_miracle","kitchen_dumplings","last_transfer"]
## On-demand art library. IDs and furniture anchors come from production recipes.
var catalog: Array=[]
var selected := "cast"
var knowledge_view: Node3D
var _outdoor_camera: Dictionary = {}
var detail_controller: Node
var _review_target := Vector3.ZERO
var _review_offset := Vector3(14, 46, 24)
var _comparison_span := 0.0
var _detail_label: Label
@export var initial_asset := "zone_yi:overview"
@export var show_library_controls := true

func asset_ids() -> Array:
	var ids: Array=["zone_yi:overview","zone_jia:overview","zone_bing:overview","zone_ding:overview","living_hall:overview","cast","knowledge-demo"]
	for entry in catalog: ids.append(entry.id)
	for id in RECORDING_IDS: ids.append("recording:"+id)
	for id in ["campus_nw","campus_n","campus_ne","campus_sw","campus_s","harbor"]: ids.append("outdoor:"+id)
	return ids

func _ready() -> void:
	super._ready()
	panel.hide()
	catalog=preload("res://scenes/Host/DvPresentationContent.cs").new().ReadRecord("res://Content/presentation.dv", "layouts", "interiors").rooms
	detail_controller = preload("res://rendering/RenderDetailController.gd").new()
	detail_controller.name = "RenderDetailController"
	detail_controller.auto_configure = false
	add_child(detail_controller)
	select_asset(initial_asset)
	if show_library_controls and get_viewport()==get_tree().root:
		var controls := CanvasLayer.new()
		add_child(controls)
		var fade_toggle := CheckButton.new()
		fade_toggle.text = "墙体点纹渐隐"
		fade_toggle.position = Vector2(980, 62)
		fade_toggle.button_pressed = true
		fade_toggle.toggled.connect(func(value): occlusion.enabled = value)
		controls.add_child(fade_toggle)
		var picker := OptionButton.new()
		picker.position=Vector2(20,20)
		controls.add_child(picker)
		var ids := asset_ids()
		for id in ids: picker.add_item(id)
		picker.select(ids.find(initial_asset))
		picker.item_selected.connect(func(i): select_asset(ids[i]))
		var contour_toggle := CheckButton.new()
		contour_toggle.text = "细描边"
		contour_toggle.position = Vector2(310, 20)
		contour_toggle.button_pressed = true
		contour_toggle.toggled.connect(set_contour_enabled)
		controls.add_child(contour_toggle)
		# Category shortcuts keep the full library practical to browse.
		var shortcuts := {"公共厅": "living_hall:overview", "室外": "outdoor:campus_nw", "人物": "cast"}
		var shortcut_x := 430.0
		for caption in shortcuts:
			var shortcut := Button.new()
			shortcut.text = caption
			shortcut.position = Vector2(shortcut_x, 20)
			var asset_id: String = shortcuts[caption]
			shortcut.pressed.connect(func():
				picker.select(ids.find(asset_id))
				select_asset(asset_id))
			controls.add_child(shortcut)
			shortcut_x += 95.0
		var time_input := SpinBox.new()
		time_input.position = Vector2(20, 62)
		time_input.custom_minimum_size = Vector2(240, 36)
		time_input.min_value = 0
		time_input.max_value = 1440
		time_input.step = 15
		time_input.prefix = "时间（分钟）"
		time_input.value = game_minutes
		time_input.value_changed.connect(func(value): set_world_time(value, selected.begins_with("outdoor:")))
		controls.add_child(time_input)
		var time_presets := OptionButton.new()
		time_presets.position = Vector2(270, 62)
		for hour in range(0, 24, 3): time_presets.add_item("%02d:00" % hour, hour * 60)
		time_presets.select(int(fposmod(game_minutes, 1440.0) / 180.0))
		time_presets.item_selected.connect(func(index): time_input.value = time_presets.get_item_id(index))
		controls.add_child(time_presets)
		for camera_choice in [["室外总览", false], ["地面细节", true]]:
			var view_button := Button.new()
			view_button.text = camera_choice[0]
			view_button.position = Vector2(410 if not camera_choice[1] else 520, 62)
			view_button.pressed.connect(_set_outdoor_camera.bind(bool(camera_choice[1])))
			controls.add_child(view_button)
		for span in [12.0, 24.0, 6.0]:
			var compare_button := Button.new()
			compare_button.text = "%d米跨度" % span
			compare_button.position = Vector2(640 if span == 12.0 else (750 if span == 24.0 else 860), 62)
			compare_button.pressed.connect(set_review_span.bind(span))
			controls.add_child(compare_button)
		_detail_label = Label.new()
		_detail_label.position = Vector2(20, 106)
		controls.add_child(_detail_label)
		detail_controller.detail_changed.connect(_show_detail_statistics)
		_show_detail_statistics(detail_controller.snapshot())

func set_review_span(span: float) -> void:
	_comparison_span = span
	var target := _review_target
	if selected.begins_with("outdoor:") and _outdoor_camera.has("detail_camera_target"):
		target = _outdoor_camera.detail_camera_target
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.position = target + _review_offset
	camera.look_at(target, Vector3.UP)
	camera.size = span
	var pixels_per_metre := camera.unproject_position(target + camera.global_basis.x).distance_to(camera.unproject_position(target))
	if pixels_per_metre > 0:
		var measured_span := camera.get_viewport().get_visible_rect().size.x / pixels_per_metre
		camera.size *= span / measured_span
	_show_detail_statistics(detail_controller.snapshot())

func _show_detail_statistics(statistics: Dictionary) -> void:
	if not is_instance_valid(_detail_label): return
	_detail_label.text = "%s曲面精度 高:%d 中:%d 低:%d" % [("跨度 %.0f米 | " % _comparison_span) if _comparison_span > 0 else "", statistics.high, statistics.medium, statistics.low]

func _set_outdoor_camera(detail: bool) -> void:
	if not selected.begins_with("outdoor:") or _outdoor_camera.is_empty(): return
	_comparison_span = 0.0
	var prefix := "detail_" if detail and _outdoor_camera.has("detail_camera_position") else ""
	camera.size = _outdoor_camera[prefix + "camera_size"]
	camera.position = _outdoor_camera[prefix + "camera_position"]
	camera.look_at(_outdoor_camera[prefix + "camera_target"], Vector3.UP)

func select_asset(id: String) -> void:
	selected=id
	_comparison_span = 0.0
	_outdoor_camera.clear()
	if is_instance_valid(knowledge_view):
		remove_child(knowledge_view)
		knowledge_view.queue_free()
		knowledge_view=null
	for root in rooms.values():
		remove_child(root)
		root.queue_free()
	rooms.clear()
	room_sizes.clear()
	room_origins.clear()
	local_lights.clear()
	figures.clear()
	fire_materials.clear()
	var camera_target := Vector3(0,0.8,1.1 if id=="cast" else -0.65)
	var camera_position := Vector3(0,22,25)
	camera.projection=Camera3D.PROJECTION_ORTHOGONAL
	$WorldEnvironment.environment.ambient_light_energy=.55
	key_light.light_energy=.72
	if id.begins_with("zone_") or id.begins_with("living_hall:"):
		var yi_size: Vector2=YI_REGION.build(self,catalog,id) if id.begins_with("zone_") else HALL_REGION.build(self,catalog,id)
		camera.projection=Camera3D.PROJECTION_PERSPECTIVE
		camera.fov=22.0
		var aspect: float=float(get_viewport().get_visible_rect().size.x)/maxf(1.0,get_viewport().get_visible_rect().size.y)
		var distance: float=maxf(yi_size.x*1.15/aspect,yi_size.y*.85+3.1)*2.7
		camera_target=Vector3(0,.75,0)
		camera_position=camera_target+Vector3(0,sin(deg_to_rad(58.0)),cos(deg_to_rad(58.0)))*distance
	elif id=="cast":
		content_root=Node3D.new()
		add_child(content_root)
		rooms[id]=content_root
		var index := 0
		for identity in CAST.PEOPLE:
			var x: float=(index%5-2.0)*1.8
			var z: float=floorf(index/5.0)*2.1-3.2
			_character(CAST.PEOPLE[identity][0],x,z,identity,1.0)
			var caption := Label3D.new()
			caption.text=CAST.PEOPLE[identity][0]
			caption.font_size=28
			caption.pixel_size=0.004
			caption.position=Vector3(x,0.02,z+0.25)
			caption.no_depth_test=true
			caption.rotation_degrees.x=-55
			content_root.add_child(caption)
			index+=1
		box(content_root,Vector3(0,-0.11,0),Vector3(10,0.2,9),material("gallery","555d59",2))
		camera.size=11.0
	elif id.begins_with("recording:"):
		content_root=Node3D.new()
		add_child(content_root)
		rooms[id]=content_root
		current_room_id=id
		var result: Dictionary=RECORDING.build(self,id.trim_prefix("recording:"))
		camera.size=result.camera_size
		camera_target=result.target
		for light in local_lights: light.set_meta("room",id)
	elif id.begins_with("outdoor:"):
		content_root=Node3D.new()
		add_child(content_root)
		rooms[id]=content_root
		current_room_id=id
		var result: Dictionary=OUTDOORS.build(self,id.trim_prefix("outdoor:"))
		_outdoor_camera=result
		camera.size=result.camera_size
		camera_target=result.camera_target
		camera_position=result.camera_position
	elif id=="knowledge-demo":
		knowledge_view=KNOWLEDGE.new()
		add_child(knowledge_view)
		for i in range(3):
			var room_id := "demo_"+str(i)
			_room(room_id,Vector3((i-1)*6,0,0),Vector2i(6,8),{})
			_table(Vector3(0,0,0),Vector2(1.8,0.75),0.85)
			_armchair(Vector3(0,0,1.2),0)
			_character("Observer fixture",-1.5,1.0,"detective",1)
			knowledge_view.register_room(room_id,"preview-map",content_root)
			if i<2: knowledge_view.supply_observed_snapshot(room_id,content_root,[[-3,-4,6,8]])
		knowledge_view.apply_observer_view("preview-map","demo_1",["demo_0","demo_1"])
		camera.size=22
	else:
		var entry: Dictionary={}
		for candidate in catalog:
			if candidate.id==id: entry=candidate
		if entry.is_empty(): return
		var size := Vector2i(int(entry.size[0]),int(entry.size[1]))
		select_materials(entry)
		var openings: Dictionary={"south":[size.x/2-1,size.x/2]}
		if size.x>=6 and entry.map!="greenhouse": openings["north"]=[size.x/2-1,size.x/2]
		var formal := preload("res://scenes/World/Art/WorldPortalLayout.gd").grid_openings(id, size)
		if not formal.is_empty(): openings = formal
		_room(id,Vector3.ZERO,size,openings)
		for rug in entry.get("rugs",[]): _rug(Vector3(rug.at[0],0.025,rug.at[1]),Vector2(rug.size[0],rug.size[1]))
		if str(entry.map).begins_with("zone_"): LIVING.furnish(self,entry)
		elif PUBLIC.handles(entry.map): PUBLIC.furnish(self,entry)
		elif entry.map in ["logistics","clinic","granary","greenhouse"]: SERVICE.furnish(self,entry)
		else:
			for prop in entry.furniture: _prop(prop.kind,Vector3(prop.at[0],0,prop.at[1]),prop.name)
		if size.x>=6 and entry.map!="greenhouse": _window(Vector3(-0.5 if size.x%2 else 0.0,0,-size.y*0.5))
		_add_light(Vector3(0,3.1,0),1.2,maxf(size.x,size.y))
		camera.size=maxf(10.0,maxf(size.x*1.18,size.y*0.82+4.5))
	active_room=id
	current_room_id=id
	camera.far=1000.0
	camera.position=camera_position
	camera.look_at(camera_target,Vector3.UP)
	atmosphere.configure(camera_target, id.begins_with("outdoor:"))
	var env: Environment = $WorldEnvironment.environment
	env.ssao_radius = 0.4 if id.begins_with("outdoor:") else 0.55
	env.ssao_intensity = 0.75 if id.begins_with("outdoor:") else 1.0
	env.glow_hdr_threshold = 2.0
	key_light.directional_shadow_max_distance = 180.0 if id.begins_with("outdoor:") else 45.0
	configure_lighting("normal")
	if id.begins_with("zone_") or id.begins_with("living_hall:"):
		var environment: Environment=$WorldEnvironment.environment
		environment.ambient_light_energy=.38
		key_light.light_energy=.8
		for light in local_lights: light.visible=id.ends_with(":overview") or light.get_meta("room")==id
	set_world_time(game_minutes, id.begins_with("outdoor:"))
	occlusion.configure(camera, rooms.values(), camera_target)
	_review_target = camera_target
	detail_controller.configure(camera, rooms.values())
	set_meta("render_metadata",{"asset":id,"scope":"Residential reference redesign in metres; production topology unchanged" if id.begins_with("zone_") or id.begins_with("living_hall:") else "3D art library; production layout anchors, no gameplay migration", "body_template":"E-common-three-head-v1"})

func select_materials(entry: Dictionary) -> void:
	var floor_mat: Material=INTERIOR_FINISH.floor(entry)
	var tiled: bool=entry.get("floor","") in ["whitetile","quarry","flagstone","stone","tile","slate","gravel"]
	var wallpaper: Material
	if entry.map=="greenhouse":
		var glass := StandardMaterial3D.new()
		glass.albedo_color=Color(0.46,0.65,0.60,0.25)
		glass.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA
		glass.roughness=0.24
		wallpaper=glass
	else:
		var finish := ShaderMaterial.new()
		finish.shader=preload("res://rendering/shaders/surfaces/interior_wall.gdshader")
		finish.set_shader_parameter("use_wallpaper",not tiled)
		finish.set_shader_parameter("wallpaper",_painted_texture(preload("res://ShareAsset/Internal/WorldMaterials/wallpaper.png")))
		wallpaper=finish
	var frame: Material=material("greenhouse_iron","465752",0,0.6,0.4) if entry.map=="greenhouse" else material("walnut","493227",1)
	var trim: Material=frame if entry.map=="greenhouse" else material("moulding","735334",1)
	var wall_material: Material=wallpaper
	library=KIT.library(floor_mat,wall_material,frame,trim,3.8)

func _bed(at: Vector3, clinical: bool=false) -> void:
	var root := group("Bed",at)
	var frame := material("bed_frame","5b4433",1) if not clinical else material("iron_bed","b9bdb2",0,0.5,0.3)
	for x in [-0.53,0.53]:
		for z in [-0.85,0.85]: cylinder(root,Vector3(x,0.48,z),0.045,0.96,frame)
	box(root,Vector3(0,0.43,0),Vector3(1.15,0.16,1.9),frame)
	box(root,Vector3(0,0.61,0),Vector3(1.1,0.22,1.86),material("linen","c9bf9f",3))
	box(root,Vector3(0,0.74,0.26),Vector3(1.11,0.06,1.29),material("cover","4a6452",3))
	ellipsoid(root,Vector3(0,0.78,-0.65),Vector3(0.85,0.18,0.35),material("linen","c9bf9f",3))
	box(root,Vector3(0,0.80,-0.91),Vector3(1.17,0.67,0.09),frame)

func _cabinet(at: Vector3, glazed: bool=false) -> void:
	var root := group("Cabinet",at)
	var wood := material("cabinet","543c2d",1)
	box(root,Vector3(0,0.93,0),Vector3(1.1,1.86,0.48),wood)
	for x in [-0.27,0.27]:
		box(root,Vector3(x,1.0,0.25),Vector3(0.46,1.51,0.04),material("cabinet_panel","735334",1))
		if glazed: box(root,Vector3(x,1.18,0.28),Vector3(0.36,0.95,0.02),material("cabinet_glass","708d87",0,0.22))
		ellipsoid(root,Vector3(x*0.25,0.92,0.30),Vector3.ONE*0.045,material("brass","a78d58",0,0.4,0.6))
	box(root,Vector3(0,1.9,0),Vector3(1.2,0.1,0.57),wood)

func _prop(kind: String, at: Vector3, title: String) -> void:
	if kind.begins_with("bookcase") or kind in ["archive_shelf","seal_rack"]: _bookcase(at)
	elif kind in ["bed_0","ward_bed"]: _bed(at,kind=="ward_bed")
	elif kind in ["wardrobe","medicine_cabinet","reagent_cabinet","plan_cabinet","sideboard","dish_rack"]: _cabinet(at,kind in ["medicine_cabinet","reagent_cabinet"])
	elif kind=="fireplace": _fireplace(at)
	elif kind=="floor_lamp": _lamp(at)
	elif kind=="armchair": _armchair(at,0)
	elif kind=="upright_piano": _piano(at)
	elif kind=="chess_table": _chess(at)
	elif kind=="coat_stand": _coatstand(at)
	elif kind in ["plant_stand","seedling_pots"]: _plant(at)
	elif kind in ["chair_back","chair_front","piano_stool"]: _bench(at,0.65)
	elif kind in ["bench","long_bench","shoe_bench"]: _bench(at,2.0 if kind=="long_bench" else 1.2)
	elif kind in ["notice_board","blackboard_large","timetable_board"]: _notice(at)
	elif kind in ["long_table","lecture_desk_row","study_table","worktable","desk_0","copy_desk","doctor_desk","ration_counter"]:
		_table(at,Vector2(3.4 if kind in ["long_table","lecture_desk_row"] else 1.6,0.8),0.86)
	elif kind in ["nightstand","side_table_plain","ledger_stand","lectern","music_stand"]:
		_table(at,Vector2(0.7,0.48),0.9)
	elif kind in ["barrel","laundry_tub","sack","jar"]:
		var root := group(title,at)
		var mat := material("barrel","805c36",1) if kind!="sack" else material("burlap","b4a281",3)
		cylinder(root,Vector3(0,0.43,0),0.33,0.86,mat,0.29)
		for y in [0.15,0.70]: cylinder(root,Vector3(0,y,0),0.335,0.045,material("iron","49504d",0,0.6,0.5))
	elif kind in ["crate","granary_bin","box_pedestal"]:
		var root := group(title,at)
		box(root,Vector3(0,0.4,0),Vector3(0.8,0.8,0.7),material("crate","97754c",1))
		for y in [0.1,0.7]: box(root,Vector3(0,y,0.36),Vector3(0.83,0.09,0.045),material("walnut","493227",1))
	elif kind in ["washstand","wash_trough","basin_row","bathtub"]:
		var root := group(title,at)
		var ceramic := material("ceramic","d5d6c5",0,0.28)
		if kind=="bathtub": _tub_shell(root,ceramic)
		else:
			box(root,Vector3(0,0.67,0),Vector3(1.2,0.16,0.6),ceramic)
			for x in [-0.4,0.4]: cylinder(root,Vector3(x,0.3,0),0.045,0.6,ceramic)
			ellipsoid(root,Vector3(0,0.77,0),Vector3(0.7,0.09,0.4),material("water_basin","64847e",0,0.2))
	elif kind in ["range_stove","kitchen_range","hood_chimney"]:
		var root := group(title,at)
		var iron := material("stove","424b49",0,0.7,0.35)
		box(root,Vector3(0,0.48,0),Vector3(1.4,0.96,0.8),iron)
		for x in [-0.4,0.4]: cylinder(root,Vector3(x,0.99,0),0.21,0.04,iron)
		box(root,Vector3(0,0.50,0.42),Vector3(0.8,0.4,0.04),material("stove_door","6b6253",0))
		cylinder(root,Vector3(0,1.8,-0.3),0.13,1.7,iron)
	elif kind=="planter_bed":
		var root := group(title,at)
		box(root,Vector3(0,0.42,0),Vector3(1.8,0.8,0.85),material("planter","72553a",1))
		for x in [-0.6,0.0,0.6]: _plant(at+Vector3(x,0.8,0))
	elif kind in ["counterweight_engine","valve_station","water_heater","hand_pump","tuning_frame","relay_column","mouthpiece_stand","transmitter"]:
		var root := group(title,at)
		var brass := material("machinery","a58a57",0,0.38,0.7)
		var tall := 1.8 if kind in ["water_heater","counterweight_engine","relay_column"] else 1.0
		cylinder(root,Vector3(0,tall/2,0),0.23,tall,brass)
		for y in [0.2,tall-0.15]: cylinder(root,Vector3(0,y,0),0.27,0.08,brass)
		box(root,Vector3(0,tall*0.7,0.26),Vector3(0.28,0.28,0.08),material("dial","d6cba7",0))
	elif kind=="folding_screen":
		for x in [-0.6,0.0,0.6]: box(content_root,at+Vector3(x,0.95,0),Vector3(0.58,1.9,0.08),material("screen","506b56",3))
	else:
		# Small authored evidence is retained as named geometry, never grants a fact.
		var root := group(title,at)
		box(root,Vector3(0,0.035,0),Vector3(0.20,0.06,0.16),material("small_object","b1a485",2))
		root.set_meta("unfinished_detail",kind)
