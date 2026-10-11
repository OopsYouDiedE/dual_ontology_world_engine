extends SceneTree
const Pilot = preload("res://scenes/Host/PilotNavigation.gd")
const View = preload("res://scenes/Shared/Field/WorldView.gd")
class Adapter extends Node2D:
	var calls := 0
	var unseen_calls := 0
	var objects := Node2D.new()
	func objects_root(): return objects
	func sight_size(): return Vector2i(16,16)
	func sight_terrain_fog(): return true
	func walkable(point, _actor):
		calls += 1
		if point.y < 256: unseen_calls += 1
		return true
class Shade extends Node2D:
	var alpha := 1.0
	func terrain_shade(_cell): return Color(0,0,0,alpha)
class Door extends Node2D:
	var left: Texture2D
	var openness := 0.0
	func door_click_rect(): return Rect2()
class Field extends RefCounted:
	var adapter := Adapter.new()
	var player := CharacterBody2D.new()
	func player_visible_doorways(): return [Vector2(256,256)]
	func in_sight(point): return point.y >= 256
var passes := 0
func check(label, success):
	if not success:
		push_error("FAIL " + label)
		quit(1)
		return false
	passes += 1
	print("PASS " + label)
	return true
func _initialize(): call_deferred("run")
func run():
	var field := Field.new()
	var known = Pilot.remember_grid(field)
	check("unknown_ground_never_queried",field.adapter.calls > 0 and field.adapter.unseen_calls == 0)
	for sign in [-1,1]:
		var grid := AStarGrid2D.new()
		grid.region=Rect2i(0,0,64,64)
		grid.cell_size=Vector2(8,8)
		grid.offset=Vector2(4,4)
		grid.diagonal_mode=AStarGrid2D.DIAGONAL_MODE_NEVER
		grid.update()
		for y in 64:
			for x in 64: grid.set_point_solid(Vector2i(x,y), (y<32 if sign==1 else y>=32))
		for offset in [-24,0,24]:
			field.player.position=Vector2(256,256+offset)
			var offers=Pilot.doorways(field,grid)
			check("crossing_direction_%d_%d" % [sign,offset],offers.size()==1 and offers[0].input.keys==(["W"] if sign==1 else ["S"]))
		field.player.position=Vector2(100,256+sign*100)
		var approach=Pilot.doorways(field,grid)
		check("known_approach_%d" % sign,approach.size()==1 and approach[0].input.has("point") and not grid.is_point_solid(Vector2i((Vector2(approach[0].location[0],approach[0].location[1])/8).floor())))
		for y in 64: grid.set_point_solid(Vector2i(24,y),true)
		check("disconnected_known_path_not_offered_%d" % sign,Pilot.doorways(field,grid).is_empty())
		grid.set_point_solid(Vector2i(32,24),false)
		grid.set_point_solid(Vector2i(32,40),false)
		check("both_sides_seen_not_exploration_%d" % sign,Pilot.doorways(field,grid).is_empty())
	check("doorways_never_query_hidden_geometry",field.adapter.unseen_calls==0)
	var view=View.new()
	view.set_process(false)
	view.set_physics_process(false)
	view.adapter=field.adapter
	view.player=field.player
	view.sight=Shade.new()
	root.add_child(view)
	view.add_child(field.adapter)
	field.adapter.add_child(field.adapter.objects)
	view.add_child(view.sight)
	var door=Door.new()
	door.left=ImageTexture.create_from_image(Image.create(32,64,false,Image.FORMAT_RGBA8))
	door.position=Vector2(128,128)
	field.adapter.objects.add_child(door)
	check("fully_fogged_door_not_public",view.player_visible_doorways().is_empty())
	view.sight.alpha=0.0
	check("exposed_door_threshold_only",view.player_visible_doorways()==[Vector2(128,128)])
	door.hide()
	check("hidden_door_not_public",view.player_visible_doorways().is_empty())
	door.show()
	door.modulate.a=0
	check("transparent_door_not_public",view.player_visible_doorways().is_empty())
	door.modulate.a=1
	door.position=Vector2(-1000,-1000)
	check("offscreen_door_not_public",view.player_visible_doorways().is_empty())
	view.free()
	field.player.free()
	print("COMPLETE passes=",passes)
	quit(0)

