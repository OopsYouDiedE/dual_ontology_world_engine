extends "res://scenes/Chapter2/Game/check/unified_ui_check.gd"
## One synthetic media presentation using a real formal-map asset; not a story playthrough.
func run():
	await fresh("res://scenes/Chapter2/Game/Chapter2Game.tscn")
	check(await drain(true),"media fixture default formal host idle")
	var sprites: Array = game.map.get_node("Objects").get_children().filter(func(n): return n is Sprite2D and n.texture != null and not n.texture.resource_path.is_empty())
	check(not sprites.is_empty(),"formal current-map original image available")
	if sprites.is_empty(): finish(); return
	var path: String = sprites[0].texture.resource_path
	game.story_media.append({"sequence":10001,"kind":"Image","content":path})
	game._show_story_media(10001)
	await pause()
	check(game.modal_kind == "story_image", "INJECT original asset enters shared story_image renderer")
	await shot("c2-media")
	key(KEY_SPACE)
	await pause()
	check(game.modal_kind == "" and game.story_media.is_empty(),"Space original story_image continuation consumes media and closes")
	var f := FileAccess.open(out + "/unified-ui-media-checks.json",FileAccess.WRITE)
	f.store_string(JSON.stringify(rows,"\t"))
	print("UNIFIED_UI_MEDIA_FINISH ",failed)
	quit(1 if failed else 0)
