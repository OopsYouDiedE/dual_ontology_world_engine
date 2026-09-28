extends SceneTree

const OUTPUT_DIR := "res://../artifacts/first-awakening/"

func _initialize() -> void:
	call_deferred("capture")

func capture() -> void:
	var locale := "zh_CN"
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--locale="):
			locale = arg.trim_prefix("--locale=")
	TranslationServer.set_locale(locale)
	root.content_scale_size = Vector2i(1280, 992)
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)
	var page: Control = load("res://scenes/FirstAwakening/MindNotebook.tscn").instantiate()
	root.add_child(page)
	var language: OptionButton = page.get_node("Language")
	for entry in [["中文", "zh_CN"], ["English", "en"], ["日本語", "ja"]]:
		language.add_item(entry[0])
		if entry[1] == locale:
			language.select(language.item_count - 1)
	if locale == "ja" or locale == "en":
		var print_font := load("res://ShareAsset/External/newtegomin/NewTegomin-Regular.ttf")
		var writing_font := load("res://ShareAsset/External/ZenKurenaido/ZenKurenaido-Regular.ttf") if locale == "ja" else load("res://ShareAsset/External/playwriteustrad/PlaywriteUSTrad[wght].ttf")
		var japanese_theme: Theme = page.theme.duplicate()
		japanese_theme.default_font = print_font
		page.theme = japanese_theme
		for control in page.find_children("*", "Control", true, false):
			if control.has_theme_font_override("font") and control.get_theme_font("font").resource_path.ends_with("Yozai-Regular.ttf"):
				control.add_theme_font_override("font", writing_font)
	var person := page.get_node("Heading/Person")
	var editor := page.get_node("Writing/DraftEditor")
	if locale == "en":
		person.text = "Musician"
		editor.text = "Musician prop door with wedge.\nDetective remove wedge from door.\nEngineer insert inner bolt."
	elif locale == "ja":
		person.text = "音楽家"
		editor.text = "音楽家くさびで扉を支える。\n探偵扉のくさびを外す。\n技師内かんぬきを差し込む。"
	else:
		person.text = "音乐家"
		editor.text = "音乐家用木楔撑开门。\n侦探撤下门底的木楔。\n工程师插入生活翼内销。"
	for i in 8:
		await process_frame
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	var output := OUTPUT_DIR + "mind-i18n-" + locale + ".png"
	var err := image.save_png(output)
	print("MIND FONT CAPTURE: ", err, " ", output)
	quit(0 if err == OK else 1)
