extends SceneTree
const Pilot = preload("res://scenes/Host/AgentPilot.gd")
const Field = preload("res://scenes/Shared/Field/WorldView.gd")
const Actor = preload("res://scenes/Story/StoryActor.gd")
const Wire = preload("res://GroundWireProbe.cs")
class Adapter extends Node2D:
 var field
 var unseen_queries=0
 var queries=0
 func map_key(): return "neutral"
 func sight_size(): return Vector2i(12,12)
 func walkable(p,a):
  queries+=1
  if not field.in_sight(p): unseen_queries+=1
  return true
 func switching(): return false
 func physics_update(_d): pass
 func seen_cells(): return {Vector2i(2,2):true}
class TestField extends Field:
 var visible_rect=Rect2(0,0,96,96)
 func in_sight(p:Vector2)->bool:return visible_rect.has_point(p)
var failures=0
var checks=0
func check(label,ok):
 checks+=1
 print(("PASS " if ok else "FAIL ")+label)
 if not ok: failures+=1
func _initialize():call_deferred("run")
func run():
 var field=TestField.new()
 field.adapter=Adapter.new()
 field.adapter.field=field
 field.add_child(field.adapter)
 field.follow_camera=false
 field.snapshot={"field_enabled":true}
 var actor=Actor.new()
 actor.standard=SpriteFrames.new()
 for action in ["idle","walk"]:
  for direction in ["front","back","left"]:actor.standard.add_animation(action+"_"+direction)
 field.player=actor
 field.add_child(actor)
 root.add_child(field)
 field.set_process(false)
 actor.position=Vector2(80,80)
 var pilot=Pilot.new()
 root.add_child(pilot)
 var visible=pilot._field_offers(field,{})
 check("visible_move_ground_known",visible.filter(func(o):return o.kind=="move" and field.in_sight(Vector2(o.location[0],o.location[1]))).all(func(o):return o.known_ground))
 check("visible_and_unknown_candidates",visible.any(func(o):return o.kind=="move" and o.known_ground) and visible.any(func(o):return o.kind=="move" and not o.known_ground))
 field.visible_rect=Rect2(0,0,32,32)
 var remembered=pilot._field_offers(field,{})
 check("previously_seen_now_hidden_ground_known",remembered.any(func(o):return o.kind=="move" and o.known_ground and not field.in_sight(Vector2(o.location[0],o.location[1]))))
 var fresh=Pilot.new()
 root.add_child(fresh)
 var fresh_offers=fresh._field_offers(field,{})
 check("pilot_memory_not_shared",fresh_offers.filter(func(o):return o.kind=="move" and not field.in_sight(Vector2(o.location[0],o.location[1]))).all(func(o):return not o.known_ground))
 check("unknown_geometry_never_queried",field.adapter.unseen_queries==0 and field.adapter.queries>0)
 var helper=Wire.new()
 root.add_child(helper)
 var engine=helper.MakeEngine()
 root.add_child(engine)
 check("official_offer_call_accepts",engine.AgentObserveField(remembered)=="")
 var observation=JSON.parse_string(engine.AgentObservationJson())
 var moves=observation.Candidates.filter(func(c):return c.Kind=="move")
 check("wire_candidate_count",moves.size()==remembered.filter(func(o):return o.kind=="move").size())
 var preserved=true
 for i in remembered.size():
  var offer=remembered[i]
  var candidate=observation.Candidates[i]
  preserved=preserved and candidate.KnownGround==offer.get("known_ground",false)
 check("wire_preserves_each_known_ground",preserved and moves.any(func(c):return c.KnownGround))
 var old=observation.Candidates[0].Token
 remembered[0].known_ground=not remembered[0].get("known_ground",false)
 check("wire_metadata_update_accepts",engine.AgentObserveField(remembered)=="")
 var updated=JSON.parse_string(engine.AgentObservationJson())
 check("wire_metadata_token_invalidated",updated.Candidates[0].Token!=old)
 check("wire_old_token_rejected",JSON.parse_string(engine.AgentCandidateInputJson(old)).get("boundary","")=="unknown_consumed_or_expired_token")
 print("MEASURE ",JSON.stringify({"geometry_queries":field.adapter.queries,"unseen_queries":field.adapter.unseen_queries,"offers":remembered.size()}))
 print("Ground wire checks: ",checks," total, ",failures," failed")
 quit(1 if failures else 0)
