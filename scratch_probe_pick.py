import subprocess
from pathlib import Path

GODOT_BIN = r"C:\Users\iii\Desktop\dual_ontology_world_engine\.runtime\godot\4.7.2\Godot_v4.7.2-stable_mono_win64_console.exe"
RECONSTRUCT_DIR = r"c:\Users\iii\Desktop\dual_ontology_world_reconstruct"

gd_code = """
extends SceneTree

func _init() -> void:
	call_deferred("_start")

func _start() -> void:
	var root = get_root()
	var main_scene = load("res://scenes/main/Main.tscn").instantiate()
	root.add_child(main_scene)
	await process_frame
	await process_frame
	await main_scene.start_new_game()
	await process_frame
	await process_frame
	
	var field = main_scene.field
	print("Player before pick: ", field.body.position)
	print("controller_ai: ", field.controller_ai)
	print("activity: ", field._activity())
	
	var point = Vector2(450, 340)
	var origin = field.camera.project_ray_origin(point)
	var normal = field.camera.project_ray_normal(point)
	var query = PhysicsRayQueryParameters3D.create(origin, origin + normal * 150.0, 7)
	var hit = field.get_world_3d().direct_space_state.intersect_ray(query)
	print("HIT result: ", hit)
	if not hit.is_empty():
		print("HIT collider: ", hit.collider.name, " parent: ", hit.collider.get_parent().name)
		print("HIT position: ", hit.position)
		var route = field._route(field.body.position, hit.position)
		print("ROUTE: ", route)
	
	print("Calling field._pick(Vector2(450, 340))...")
	field._pick(Vector2(450, 340))
	print("field._path after pick: ", field._path)
	
	# Simulate 10 physics frames
	for i in range(10):
		field._move_player(0.016)
	print("Player after 10 physics frames: ", field.body.position)
	quit()
"""

probe = Path(RECONSTRUCT_DIR) / "scratch_probe_pick.gd"
probe.write_text(gd_code, encoding="utf-8")

cmd = [GODOT_BIN, "--headless", "--path", RECONSTRUCT_DIR, "-s", "scratch_probe_pick.gd"]
proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8")
print(proc.stdout)
if proc.stderr:
	print("STDERR:", proc.stderr)
probe.unlink(missing_ok=True)
