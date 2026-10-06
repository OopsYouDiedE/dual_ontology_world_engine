extends SceneTree
const OUT = "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/ui-prefabs/final/"
var failures: Array[String] = []
func _initialize(): call_deferred("run")
func check(ok, label):
 print("PASS " if ok else "FAIL ", label)
 if not ok: failures.append(label)
func settle():
 for i in 4: await process_frame
 RenderingServer.force_draw(false)
func capture(name):
 await settle()
 print("SCREEN ",name," save=",root.get_texture().get_image().save_png(OUT + name + ".png"))
func release(n):
 n.queue_free()
 await process_frame
 await process_frame
func run():
 print("AUTOMATIC LOCAL SMOKE; DIRECT HOST API AND REPLAY UNLOCK INJECTION; NOT BLIND PLAY")
 for chapter in [1,2]:
  var path = "res://scenes/Main2D.tscn" if chapter == 1 else "res://scenes/Chapter2/Game/Chapter2Game.tscn"
  var holder = load(path).instantiate()
  root.add_child(holder)
  var game = holder.get_node("Awakening") if chapter == 1 else holder
  for i in 180:
   await process_frame
   if game.dialogue.active: break
  check(game.dialogue.active, "chapter %d actual default opening dialogue" % chapter)
  await capture("ch%d-opening" % chapter)
  await release(holder)
  holder = load(path).instantiate()
  game = holder.get_node("Awakening") if chapter == 1 else holder
  game.set_meta("skip_intro",true)
  root.add_child(holder)
  await settle()
  game.open_notebook()
  check(game.modal_kind == "notebook", "chapter %d notebook" % chapter)
  await capture("ch%d-notebook" % chapter)
  game.close_modal()
  await settle()
  game.replay_open = true
  game.open_manuscript()
  check(game.modal_kind == "replay", "chapter %d manuscript injected unlock" % chapter)
  await capture("ch%d-manuscript" % chapter)
  game.close_modal()
  await settle()
  if chapter == 1: game.open_card("E3")
  else:
   for id in game.sites:
    var site = game.sites[id]
    if not site.has("person") and site.has("lore"):
     game.interact(id)
     break
  await settle()
  check(game.modal_kind == "card", "chapter %d host card" % chapter)
  await capture("ch%d-card" % chapter)
  await release(holder)
 print("FAILURES=", failures)
 quit(0 if failures.is_empty() else 1)

