extends SceneTree
const OUT = "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/ui-prefabs/final/"
var failures: Array[String] = []
func _initialize(): call_deferred("run")
func check(ok, label):
 print("PASS " if ok else "FAIL ", label)
 if not ok: failures.append(label)
func settle():
 await process_frame
 await process_frame
func run():
 check(Engine.is_editor_hint(), "true editor mode")
 for kind in ["modal", "dialogue", "manuscript"]:
  var path = {"modal":"res://scenes/Shared/UI/PaperModal.tscn", "dialogue":"res://scenes/Shared/UI/ChapterDialogue.tscn", "manuscript":"res://scenes/Shared/ReplayManuscript.tscn"}[kind]
  var n = load(path).instantiate()
  root.add_child(n)
  await settle()
  if kind == "modal":
   n.set("modal_title", "一次修改保存")
   check(n.title_label.text.contains("一次修改保存"), kind + " immediate set")
  elif kind == "dialogue":
   n.set("preview_body", "一次修改保存对白")
   check(n.body.text == "一次修改保存对白", kind + " immediate set")
  else:
   n.set("default_title", "一次修改保存手稿")
   check(n.get_node("LeftPage/Scroll/Column/Title").text.contains("一次修改保存手稿"), kind + " immediate set")
  var pack = PackedScene.new()
  check(pack.pack(n) == OK, kind + " pack")
  check(ResourceSaver.save(pack, OUT + kind + "-set-fixture.tscn") == OK, kind + " save")
  n.queue_free()
  await settle()
  n = load(OUT + kind + "-set-fixture.tscn").instantiate()
  root.add_child(n)
  await settle()
  if kind == "modal": check(n.title_label.text.contains("一次修改保存"), kind + " reload")
  elif kind == "dialogue": check(n.body.text == "一次修改保存对白", kind + " reload")
  else: check(n.get_node("LeftPage/Scroll/Column/Title").text.contains("一次修改保存手稿"), kind + " reload")
  n.queue_free()
  await settle()
 print("FAILURES=",failures)
 quit(0 if failures.is_empty() else 1)
