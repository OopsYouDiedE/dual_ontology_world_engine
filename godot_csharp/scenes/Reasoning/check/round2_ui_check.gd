extends SceneTree
var failures: Array[String] = []
func _initialize(): call_deferred("run")
func frames(n: int):
 for i in n: await physics_frame
func click(p: Vector2):
 var m := InputEventMouseMotion.new(); m.position=p; Input.parse_input_event(m)
 for down in [true,false]:
  var e := InputEventMouseButton.new(); e.button_index=MOUSE_BUTTON_LEFT; e.position=p; e.pressed=down; Input.parse_input_event(e)
func button(node: Control): click(node.get_global_rect().get_center())
func key(code: Key):
 for down in [true,false]:
  var e := InputEventKey.new(); e.keycode=code; e.physical_keycode=code; e.pressed=down; Input.parse_input_event(e)
func require(ok: bool, message: String):
 if not ok: failures.append(message); print("FAIL: ",message)
func shot(name: String):
 await RenderingServer.frame_post_draw
 root.get_texture().get_image().save_png("res://../artifacts/tea-reasoning/round2-qa-"+name+".png")
func world_click(s: Node,p: Vector2): click(s.find_child("RoomStage",true,false).global_position+p*2)
func run():
 change_scene_to_file("res://scenes/Reasoning/Reasoning.tscn")
 await frames(30)
 var s := current_scene
 await shot("room-marker")
 key(KEY_E); await frames(40)
 require(s.find_child("ThoughtNotebook",true,false).visible,"入口E检查")
 button(s.find_child("CloseNotebook",true,false)); await frames(5)
 # QA-only travel convenience: gameplay inspection still supplies every piece of evidence.
 s.find_child("DebugTeleport",true,false).button_pressed=true
 world_click(s,Vector2(84,178)); await frames(40)
 require(s.find_child("EvidencePicker",true,false).text.contains("椅布"),"椅布独立检查热点")
 await shot("cloth")
 button(s.find_child("CloseNotebook",true,false)); await frames(5)
 world_click(s,Vector2(544,57)); await frames(40)
 require(s.find_child("EvidencePicker",true,false).text.contains("器具柜"),"柜子整个可见范围可检查")
 button(s.find_child("CloseNotebook",true,false)); await frames(5)
 button(s.find_child("MindButton",true,false)); await frames(10)
 require(s.find_child("PeoplePicker",true,false).text == "主厨","默认显示当前编辑人物")
 var organize: Button = s.find_child("OrganizeBehavior",true,false)
 if organize == null:
  for b in s.find_children("*","Button",true,false):
   if b.text == "组织行为": organize=b
 require(organize != null,"组织器入口")
 if organize:
  button(organize); await frames(10); await shot("organizer")
  button(s.find_child("AppendOrganizedTask",true,false)); await frames(10)
  require(s.find_child("TeaEditor",true,false).text.contains("准备时"),"表单生成任务")
 s.find_child("TeaEditor",true,false).text=""
 button(s.find_child("RunTea",true,false)); await frames(600)
 for b in s.find_children("*","Button",true,false):
  if b.text == "证据对照" and b.is_visible_in_tree(): button(b); break
 await frames(10)
 require(s.find_child("EvidenceComparisonModal",true,false).visible,"完整差异面板")
 require(s.find_child("EvidenceComparisonText",true,false).text.contains("椅面"),"显示已知痕迹差异")
 await shot("comparison")
 print("PASS: round2 UI" if failures.is_empty() else "FAILED: "+str(failures))
 quit(0 if failures.is_empty() else 1)

