extends Node3D
## A maintained observer host for stage-one structure acceptance. No characters or cognitive session.
const STREAMING = preload("res://scenes/components/architecture/RoomStreamingController.cs")
const ENVIRONMENT = preload("res://scenes/components/rendering/ObserverEnvironment3D.gd")
const READER = preload("res://scenes/main/DvPresentationContent.cs")
var streaming: Node3D
var camera: Camera3D
var rig: CharacterBody3D
var speed := 4.0
var camera_height := 1.6
var last_inputs: Dictionary = {}
var ready_for_observation := false
var warmup_rect: ColorRect

func _ready() -> void:
	var reader = READER.new()
	var scope: Dictionary = reader.ReadRecord("res://assets/data/stage1-zone-b.dv","data","stage1.zone_b").value
	var path := "res://.runtime/generated/stage1/bundles/manifest.json"
	if not FileAccess.file_exists(path):
		push_error("Compile the DV room bundles before opening the observer host.")
		return
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(path))
	var lighting := ENVIRONMENT.new()
	add_child(lighting)
	var sky_material := ProceduralSkyMaterial.new()
	sky_material.sky_top_color = Color(str(scope.reflection_sky_top))
	sky_material.sky_horizon_color = Color(str(scope.reflection_horizon))
	sky_material.ground_bottom_color = Color(str(scope.reflection_ground))
	sky_material.ground_horizon_color = Color(str(scope.reflection_horizon))
	sky_material.sky_energy_multiplier = float(scope.reflection_energy)
	sky_material.ground_energy_multiplier = float(scope.reflection_energy)
	var sky := Sky.new()
	sky.sky_material = sky_material
	lighting.environment.sky = sky
	if str(scope.environment_background) == "sky": lighting.environment.background_mode = Environment.BG_SKY
	lighting.environment.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	camera = Camera3D.new()
	camera.name = "ObserverCamera"
	camera.fov = float(scope.camera_fov_degrees)
	camera.near = .08
	camera.far = 70
	rig = CharacterBody3D.new()
	rig.name = "ObserverRig"
	rig.collision_layer = preload("res://scenes/components/world/PhysicsLayers.gd").ACTOR
	rig.collision_mask = 1
	rig.motion_mode = CharacterBody3D.MOTION_MODE_FLOATING
	add_child(rig)
	var shape := CollisionShape3D.new()
	var sphere := SphereShape3D.new()
	sphere.radius = .18
	shape.shape = sphere
	rig.add_child(shape)
	rig.add_child(camera)
	camera.current = true
	speed = float(scope.camera_speed_mps)
	camera_height = float(scope.camera_height_m)
	for opening in manifest.openings:
		if opening.id == scope.initial_opening:
			var r: Array = opening.rect
			rig.position = Vector3(float(r[2])+1.2,float(manifest.elevation)+camera_height,-(float(r[1])+float(r[3]))*.5)
			camera.look_at(camera.global_position+Vector3.LEFT)
	streaming = STREAMING.new()
	streaming.name = "RoomStreaming"
	add_child(streaming)
	streaming.Configure(path,camera)
	# Hold the first reveal until initial resources and renderer pipelines have been prepared.
	var cover := CanvasLayer.new()
	add_child(cover)
	warmup_rect = ColorRect.new()
	warmup_rect.color = Color("101819")
	warmup_rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	cover.add_child(warmup_rect)
	get_window().title = "Dual Ontology · Zone B"
	finish_initial_reveal()

func finish_initial_reveal() -> void:
	while is_inside_tree():
		await get_tree().process_frame
		if streaming.IsInitialReady():
			await RenderingServer.frame_post_draw
			await RenderingServer.frame_post_draw
			warmup_rect.get_parent().queue_free()
			ready_for_observation = true
			streaming.BeginMeasuredRun()
			return

func _physics_process(delta: float) -> void:
	if not ready_for_observation: return
	var forward := -camera.global_basis.z
	forward.y = 0
	var right := camera.global_basis.x
	var direction := forward * Input.get_axis("ui_down","ui_up") + right * Input.get_axis("ui_left","ui_right")
	if direction.length_squared() > 0:
		rig.velocity = direction.normalized()*speed
	else:
		rig.velocity = Vector3.ZERO
	rig.move_and_slide()
	if rig.get_slide_collision_count() > 0: streaming.RecordCollision(rig.get_slide_collision(0).get_collider())
	var yaw := Input.get_axis("ui_page_down","ui_page_up")
	if yaw != 0: camera.rotate_y(yaw*delta*1.5)
	for action in ["ui_up","ui_down","ui_left","ui_right","ui_page_up","ui_page_down"]:
		var pressed := Input.is_action_pressed(action)
		if pressed != bool(last_inputs.get(action,false)):
			streaming.RecordInput(action+(" press" if pressed else " release"))
			last_inputs[action] = pressed

func _input(event: InputEvent) -> void:
	if not ready_for_observation or not event.is_action("ui_accept") or event.is_echo(): return
	streaming.RecordInput("ui_accept press" if event.is_pressed() else "ui_accept release")
	if event.is_pressed(): streaming.OperateNearestDoor()

func streaming_observation() -> Dictionary:
	var result: Dictionary = streaming.Observation() if is_instance_valid(streaming) else {}
	result["ready"] = ready_for_observation
	result["controls"] = ["ui_up", "ui_down", "ui_left", "ui_right", "ui_page_up", "ui_page_down", "ui_accept"]
	if is_instance_valid(streaming): streaming.MarkCapture()
	return result
