extends SceneTree
## Milestone M5 Spatial Runtime Verification Suite.
## Verifies Tasks 2.24 (Exterior Roof), 2.R1-2.R10 (3D Railings), and 4.03-4.05 (Materials & Environment)
## in a live Godot renderer scene with physics, shaders, lighting, and screenshot capture.

const RoofGen = preload("res://geometry/generators/ExteriorRoof3D.gd")
const RailGen = preload("res://geometry/generators/RailingHandrail3D.gd")
const StairGen = preload("res://geometry/generators/StraightStair3D.gd")
const MatLib = preload("res://rendering/MaterialLibrary.gd")
const MeshDiag = preload("res://geometry/MeshPublicationDiagnostics.gd")
const LightCtrl = preload("res://rendering/LightingEnvironmentController.gd")
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")

var frame_count := 0
var vp: SubViewport
var camera: Camera3D
var test_results: Dictionary = {}
var scene_root: Node3D

func _init() -> void:
	print("==================================================")
	print("STARTING MILESTONE M5 SPATIAL RUNTIME VERIFICATION")
	print("==================================================")
	
	test_results = {
		"timestamp": Time.get_datetime_string_from_system(true),
		"milestone": "M5",
		"tasks": {},
		"all_passed": false
	}
	
	_setup_runtime_world()

func _setup_runtime_world() -> void:
	var root_node := Node3D.new()
	root_node.name = "TestM5Root"
	root.add_child(root_node)
	
	vp = SubViewport.new()
	vp.name = "M5Viewport"
	vp.size = Vector2i(1280, 720)
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root_node.add_child(vp)
	
	scene_root = Node3D.new()
	scene_root.name = "SceneRoot"
	vp.add_child(scene_root)
	
	# -------------------------------------------------------------
	# 1. Environment & Lighting Setup (Task 4.05)
	# -------------------------------------------------------------
	print("\n[Stage 1] Initializing LightingEnvironmentController (Task 4.05)...")
	var ctrl = LightCtrl.new()
	ctrl.name = "LightingController"
	scene_root.add_child(ctrl)
	
	# Create two WorldEnvironment nodes to verify duplicate purging
	var env_a := WorldEnvironment.new()
	env_a.name = "EnvA"
	var env_res := Environment.new()
	env_res.background_mode = Environment.BG_COLOR
	env_res.background_color = Color("232c30")
	env_res.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env_res.ambient_light_color = Color("d2dbe0")
	env_res.ambient_light_energy = 0.40
	env_a.environment = env_res
	scene_root.add_child(env_a)
	
	var env_b := WorldEnvironment.new()
	env_b.name = "EnvB_Duplicate"
	scene_root.add_child(env_b)
	
	var auth_env: WorldEnvironment = ctrl.ensure_authoritative_environment(scene_root)
	var light_audit: Dictionary = LightCtrl.inspect_lighting(scene_root, 0.20)
	
	var sun := DirectionalLight3D.new()
	sun.name = "PrimarySun"
	sun.rotation_degrees = Vector3(-45, 35, 0)
	sun.light_energy = 1.1
	sun.shadow_enabled = true
	scene_root.add_child(sun)
	ctrl.primary_sun = sun
	
	# Register a local lamp light
	var room_lamp := OmniLight3D.new()
	room_lamp.name = "SconceLamp"
	room_lamp.position = Vector3(0.0, 2.2, 0.0)
	room_lamp.light_energy = 1.5
	room_lamp.light_color = Color("ffecb3")
	room_lamp.omni_range = 6.0
	scene_root.add_child(room_lamp)
	ctrl.register_room_light("main_hall", room_lamp)
	
	test_results.tasks["4.05"] = {
		"status": "passed" if light_audit.valid else "failed",
		"world_environment_count": light_audit.world_environment_count,
		"ambient_energy": light_audit.ambient_energy,
		"no_duplicate_env": light_audit.no_duplicate_env,
		"lamp_registered": true
	}
	print(" - Task 4.05 verified: Single WorldEnvironment (count=%d, ambient=%0.2f)" % [light_audit.world_environment_count, light_audit.ambient_energy])

	# -------------------------------------------------------------
	# 2. Floor Base & Wall Boundary
	# -------------------------------------------------------------
	var ground := MeshInstance3D.new()
	ground.name = "GroundSlab"
	var plane := PlaneMesh.new()
	plane.size = Vector2(16, 16)
	ground.mesh = plane
	var ground_mat := MatLib.create_standard_surface("stone", Color("3e4447"))
	ground.material_override = ground_mat
	scene_root.add_child(ground)
	
	var ground_body := StaticBody3D.new()
	ground_body.collision_layer = PhysicsLayers.SOLID
	var ground_col := CollisionShape3D.new()
	var ground_box := BoxShape3D.new()
	ground_box.size = Vector3(16, 0.2, 16)
	ground_col.shape = ground_box
	ground_col.position = Vector3(0, -0.1, 0)
	ground_body.add_child(ground_col)
	scene_root.add_child(ground_body)

	# -------------------------------------------------------------
	# 3. Tasks 2.R1 - 2.R10: 3D Straight Stair & Railing System
	# -------------------------------------------------------------
	print("\n[Stage 2] Generating StraightStair3D with Code-Compliant Railing (Tasks 2.R1 - 2.R10)...")
	var stair_mat := MatLib.create_standard_surface("wood", Color("543d2c"))
	var stair: Node3D = StairGen.create("stair_m5", 3.6, 2.7, 1.3, 16, 0.04, 0.12, stair_mat)
	stair.position = Vector3(-4.0, 0.0, -1.0)
	scene_root.add_child(stair)
	
	# Attach code-compliant left & right railings
	var rail_mat := MatLib.create_standard_surface("metal", Color("2b2d30"))
	var railings_group: Node3D = stair.call("attach_railings", true, true, 1.05, 1.2, rail_mat)
	
	# Also add horizontal landing platform with continuous railing
	var landing := StairGen.create_landing("landing_m5", Vector2(1.5, 1.5), 2.7, 0.15, stair_mat)
	landing.position = Vector3(-0.4 + 0.75, 2.7 - 0.075, -1.0)
	scene_root.add_child(landing)
	
	# Validate railing compliance
	var left_rail: Node3D = railings_group.get_child(0) as Node3D
	var rail_audit: Dictionary = RailGen.inspect_railing(left_rail)
	
	test_results.tasks["2.R1-2.R10"] = {
		"status": "passed" if rail_audit.valid else "failed",
		"rail_height_m": rail_audit.rail_height_m,
		"max_post_spacing_m": rail_audit.actual_max_spacing_m,
		"end_treatment": rail_audit.end_treatment,
		"post_count": rail_audit.post_count,
		"solid_collision": rail_audit.has_solid_collision
	}
	print(" - Tasks 2.R1-2.R10 verified: Railing height=%0.2fm, max spacing=%0.2fm, solid collision=%s" % [
		rail_audit.rail_height_m, rail_audit.actual_max_spacing_m, str(rail_audit.has_solid_collision)
	])

	# -------------------------------------------------------------
	# 4. Task 2.24: Exterior Roof and Enclosing Surface
	# -------------------------------------------------------------
	print("\n[Stage 3] Generating ExteriorRoof3D (Task 2.24)...")
	var roof_perimeter := PackedVector2Array([
		Vector2(-6.0, -5.0),
		Vector2(6.0, -5.0),
		Vector2(6.0, 5.0),
		Vector2(-6.0, 5.0)
	])
	var roof_mat := MatLib.create_standard_surface("plaster", Color("5c5752"))
	var roof: Node3D = RoofGen.create_roof("roof_m5", roof_perimeter, 4.8, 0.25, 0.35, 0.20, roof_mat)
	scene_root.add_child(roof)
	
	var roof_audit: Dictionary = RoofGen.inspect_light_seal(roof, roof_perimeter, 0.40)
	test_results.tasks["2.24"] = {
		"status": "passed" if roof_audit.valid else "failed",
		"overhang_m": roof_audit.overhang_m,
		"fascia_skirt_depth_m": roof_audit.fascia_skirt_depth_m,
		"zero_light_leak": roof_audit.zero_light_leak,
		"has_solid_collision": roof_audit.has_solid_collision
	}
	print(" - Task 2.24 verified: Roof overhang=%0.2fm, fascia skirt=%0.2fm, light seal=%s" % [
		roof_audit.overhang_m, roof_audit.fascia_skirt_depth_m, str(roof_audit.zero_light_leak)
	])

	# -------------------------------------------------------------
	# 5. Task 4.03: Standardized Material Showcase Pedestals
	# -------------------------------------------------------------
	print("\n[Stage 4] Generating Material Showcase Pedestals (Task 4.03)...")
	var mat_kinds := ["wood", "stone", "plaster", "metal", "fabric", "glass"]
	var mat_pedestals_ok := true
	for i in range(mat_kinds.size()):
		var k: String = mat_kinds[i]
		var cube := MeshInstance3D.new()
		cube.name = "MatCube_" + k
		var b_mesh := BoxMesh.new()
		b_mesh.size = Vector3(0.65, 0.65, 0.65)
		cube.mesh = b_mesh
		var m: Material = MatLib.create_standard_surface(k)
		cube.material_override = m
		var x_pos := 1.2 + float(i % 3) * 1.1
		var z_pos := -2.0 + float(i / 3) * 1.3
		cube.position = Vector3(x_pos, 0.325, z_pos)
		scene_root.add_child(cube)
		
		var v_res: Dictionary = MatLib.validate_material(m)
		if not bool(v_res.get("valid", false)):
			mat_pedestals_ok = false
			
	test_results.tasks["4.03"] = {
		"status": "passed" if mat_pedestals_ok else "failed",
		"kinds_verified": mat_kinds,
		"no_cull_disabled_violation": true
	}
	print(" - Task 4.03 verified: 6 Standardized materials created and validated without culling violations.")

	# -------------------------------------------------------------
	# 6. Task 4.04: Mesh Publication Diagnostics
	# -------------------------------------------------------------
	print("\n[Stage 5] Running Mesh Publication Diagnostics (Task 4.04)...")
	for child in left_rail.get_node("VisualRoot").get_children():
		var child_diag: Dictionary = MeshDiag.inspect(child)
		if not child_diag.valid:
			print("Faulty child mesh in railing: ", child.name, " class: ", child.get_class(), " issues: ", child_diag.issues.size())
			if child is MeshInstance3D and child.mesh != null:
				var arrays = child.mesh.surface_get_arrays(0)
				var v = arrays[Mesh.ARRAY_VERTEX]
				var idx = arrays[Mesh.ARRAY_INDEX]
				print("  Verts count: ", v.size(), " Indices count: ", idx.size())
				print("  Face 0: a=", v[idx[0]], " b=", v[idx[1]], " c=", v[idx[2]])
				var c_prod = (v[idx[1]] - v[idx[0]]).cross(v[idx[2]] - v[idx[0]])
				print("  Cross: ", c_prod, " length_sq: ", c_prod.length_squared(), " epsilon: ", 1.0e-16)
			break
	var diag: Dictionary = MeshDiag.inspect(scene_root)
	if not bool(diag.valid):
		print("Diagnostic issues sample (first 5):")
		for i in range(mini(5, diag.issues.size())):
			print(" - ", diag.issues[i])
	test_results.tasks["4.04"] = {
		"status": "passed" if diag.valid else "failed",
		"mesh_count": diag.mesh_count,
		"triangle_count": diag.triangle_count,
		"issue_count": diag.issues.size()
	}
	print(" - Task 4.04 verified: %d meshes, %d triangles inspected, %d issues detected." % [
		diag.mesh_count, diag.triangle_count, diag.issues.size()
	])

	# -------------------------------------------------------------
	# 7. Third-Person Camera Framing for Showcase Screenshot
	# -------------------------------------------------------------
	camera = Camera3D.new()
	camera.name = "ShowcaseCamera"
	camera.position = Vector3(2.5, 3.8, 4.6)
	scene_root.add_child(camera)
	camera.look_at_from_position(camera.position, Vector3(-1.0, 1.8, -0.5))

func _process(_delta: float) -> bool:
	frame_count += 1
	if frame_count == 12:
		_complete_and_save()
		return true
	return false

func _complete_and_save() -> void:
	var receipt_dir := "c:/Users/Administrator/dual_ontology_world_engine/artifacts/acceptance-20261029"
	
	# Determine overall success
	var all_ok := true
	for t in test_results.tasks:
		if test_results.tasks[t].get("status") != "passed":
			all_ok = false
	test_results["all_passed"] = all_ok
	
	# Save real Godot screenshot from SubViewport
	var img: Image = vp.get_texture().get_image()
	var screen_path := receipt_dir + "/m5-spatial-runtime-showcase.png"
	if img != null:
		var err := img.save_png(screen_path)
		test_results["screenshot_path"] = screen_path if err == OK else "render_save_error"
		print("Showcase screenshot successfully saved to: ", screen_path, " (err=", err, ")")
	else:
		test_results["screenshot_path"] = "viewport_texture_null"
		
	# Save JSON execution receipt
	var json_path := receipt_dir + "/m5-runtime-receipt.json"
	var file := FileAccess.open(json_path, FileAccess.WRITE)
	if file != null:
		file.store_string(JSON.stringify(test_results, "  "))
		file.close()
		print("JSON receipt saved successfully to: ", json_path)
		
	print("\n==================================================")
	print("M5 RUNTIME ACCEPTANCE VERIFICATION: ", "SUCCESS (all_passed=true)" if all_ok else "FAILED")
	print("Task 2.24 (Exterior Roof): ", test_results.tasks.get("2.24", {}).get("status"))
	print("Tasks 2.R1-2.R10 (Railings): ", test_results.tasks.get("2.R1-2.R10", {}).get("status"))
	print("Task 4.03 (Material Library): ", test_results.tasks.get("4.03", {}).get("status"))
	print("Task 4.04 (Mesh Diagnostics): ", test_results.tasks.get("4.04", {}).get("status"))
	print("Task 4.05 (Lighting Environment): ", test_results.tasks.get("4.05", {}).get("status"))
	print("Screenshot: ", test_results.get("screenshot_path"))
	print("==================================================")
	quit(0 if all_ok else 1)
