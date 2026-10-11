extends SceneTree
const Pilot = preload("res://scenes/Host/AgentPilot.gd")
const Field = preload("res://scenes/Shared/Field/WorldView.gd")
const Actor = preload("res://scenes/Story/StoryActor.gd")
class Adapter extends Node2D:
	func map_key(): return "neutral"
	func sight_size(): return Vector2i(2,2)
	func walkable(_p, _a): return true
	func switching(): return false
	func physics_update(_d): pass
	func seen_cells(): return {Vector2i(0,0):true}
class TestField extends Field:
	func in_sight(p: Vector2) -> bool: return p.x < 32 and p.x >= 0 and p.y < 32 and p.y >= 0
class StubEngine extends Node:
	func AgentInteractionFeedback(_token,_ok): return false
	func AgentTimeJson(): return "{}"
	func AgentSelections(): return 0
	func CloseAgent(): pass
class Host extends Node:
	var field: Node
var failures = 0
var checks = 0
func check(label, ok):
	checks += 1
	print(("PASS " if ok else "FAIL ")+label)
	if not ok: failures += 1
func _initialize(): call_deferred("run")
func run():
	var field = TestField.new()
	field.adapter = Adapter.new()
	field.add_child(field.adapter)
	field.follow_camera = false
	field.snapshot = {"field_enabled":true}
	var actor = Actor.new()
	actor.standard = SpriteFrames.new()
	for action in ["idle", "walk"]:
		for direction in ["front", "back", "left"]: actor.standard.add_animation(action+"_"+direction)
	field.player = actor
	field.add_child(actor)
	root.add_child(field)
	field.set_process(false)
	var host = Host.new()
	host.field=field
	root.add_child(host)
	var engine=StubEngine.new()
	root.add_child(engine)
	var pilot=Pilot.new()
	pilot.host=host
	pilot.engine=engine
	root.add_child(pilot)
	for rate in [1.0,4.0]:
		Engine.time_scale=rate
		Engine.physics_ticks_per_second=int(60*rate)
		Engine.max_physics_steps_per_frame=int(8*rate)
		actor.position=Vector2(16,16)
		await physics_frame
		await physics_frame
		var offers=pilot._field_offers(field,{})
		var keyboard=[]
		for offer in offers:
			if offer.input.has("keys"): keyboard.append(offer)
		check("all_key_candidates_length_"+str(rate),keyboard.all(func(o): return abs(Vector2(o.location[0],o.location[1]).distance_to(actor.position)-actor.speed*0.35)<0.001))
		check("frontier_key_candidate_present_"+str(rate),keyboard.any(func(o): return o.get("purpose","")=="frontier"))
		for keys in [["D"],["W","D"]]:
			actor.position=Vector2(200,200)
			var start=actor.position
			await pilot._keyboard_move(keys)
			var actual=actor.position.distance_to(start)
			var tolerance=actor.speed*rate/Engine.physics_ticks_per_second+0.01
			print("MEASURE ", JSON.stringify({"rate":rate,"keys":keys,"distance":actual,"expected":actor.speed*0.35,"step":tolerance,"delta":pilot.get_physics_process_delta_time()}))
			check("movement_"+str(rate)+str(keys),abs(actual-actor.speed*0.35)<=tolerance)
			var released=actor.position
			await physics_frame
			await physics_frame
			check("released_"+str(rate)+str(keys), actor.position.distance_to(released)<0.001 and pilot._pressed.is_empty() and not Input.is_action_pressed("move_right"))
		var wall=StaticBody2D.new()
		var shape=CollisionShape2D.new()
		var rectangle=RectangleShape2D.new()
		rectangle.size=Vector2(10,200)
		shape.shape=rectangle
		wall.add_child(shape)
		wall.position=Vector2(225,200)
		field.add_child(wall)
		actor.position=Vector2(200,200)
		await physics_frame
		await pilot._keyboard_move(["D"])
		check("collision_"+str(rate),actor.position.x<=212.1 and actor.position.x>200)
		wall.queue_free()
		await physics_frame
	Engine.time_scale=1
	actor.position=Vector2(200,200)
	pilot._keyboard_move(["D"])
	await physics_frame
	await physics_frame
	pilot.stop("neutral_stop")
	await physics_frame
	var stopped=actor.position
	await physics_frame
	check("stop_releases",pilot._pressed.is_empty() and not Input.is_action_pressed("move_right") and actor.position.distance_to(stopped)<0.001)
	print("Neutral physics checks: ",checks," total, ",failures," failed")
	quit(1 if failures else 0)

