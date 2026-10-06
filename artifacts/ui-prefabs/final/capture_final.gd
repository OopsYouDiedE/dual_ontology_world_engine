extends SceneTree
const OUT = "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/ui-prefabs/final/"
func _initialize():
 call_deferred("run")
func run():
 print("EDITOR_HINT=", Engine.is_editor_hint())
 var bench = load("res://scenes/Shared/UI/UIWorkbench.tscn").instantiate()
 root.add_child(bench)
 await process_frame
 for index in [1,4]:
  bench.set("preview_page", index)
  await process_frame
  await process_frame
  RenderingServer.force_draw(false)
  var vp = bench.get_node("Pages").get_child(index).get_node("Viewport")
  var img = vp.get_texture().get_image()
  print("CAPTURE=", index, " size=",img.get_size()," save=",img.save_png(OUT + "editor-page-%02d.png" % index))
 bench.queue_free()
 await process_frame
 await process_frame
 quit()
