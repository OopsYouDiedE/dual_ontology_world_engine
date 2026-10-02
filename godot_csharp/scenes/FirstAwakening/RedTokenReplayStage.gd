extends "res://scenes/FirstAwakening/ReplayStage.gd"
## Every displayed tool location consumes the C# committed snapshot.
var token_figure: Sprite2D
var cloth_figure: Polygon2D
var handle_figure: Line2D
func _build_second_phase_marks() -> void:
	footprint_marks = Node2D.new()
	world.add_child(footprint_marks)
	sweep_mark = Line2D.new()
	world.add_child(sweep_mark)
 
func _ready() -> void:
	super._ready()
	for child in fixtures.find_children("RedMechanism_red_token","Sprite2D",true,false): child.hide()
	for child in fixtures.find_children("RedMechanism_cloth_clip","Sprite2D",true,false): child.hide()
	token_figure = Sprite2D.new()
	token_figure.texture = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/red_red_token.png")
	token_figure.offset = Vector2(0,-8)
	fixtures.add_child(token_figure)
	cloth_figure = Polygon2D.new()
	cloth_figure.polygon = PackedVector2Array([Vector2(-8,-16),Vector2(8,-16),Vector2(8,0),Vector2(-8,0)])
	cloth_figure.color = Color("d4c6a4")
	fixtures.add_child(cloth_figure)
	handle_figure = Line2D.new()
	handle_figure.points = PackedVector2Array([Vector2(-6,-12),Vector2(6,-20)])
	handle_figure.width = 4
	handle_figure.default_color = Color("aa8750")
	fixtures.add_child(handle_figure)
	manual_focus = Vector2(440,660)

func _process(delta: float) -> void:
	super._process(delta)
	if token_figure == null: return
	var snapshot := _state(frame)
	for pair in [[token_figure,"token_location"],[cloth_figure,"cloth_location"],[handle_figure,"handle_location"]]:
		var location: String = snapshot.get(pair[1],"")
		pair[0].visible = location != ""
		if location != "": pair[0].position = _object_point(location)
	cloth_figure.color = Color("75695b") if snapshot.get("gray",false) else Color("d4c6a4")
	soot.visible = snapshot.get("door_gray",false)

const CAPTION_TR := {
	"主厨确认夹滤到位": {"en": "Chef confirms filter cloth clamped in place", "ja": "主厨が濾布の固定完了を確認"},
	"夹牢滤布并确认捕集岗位": {"en": "Clamp filter cloth and confirm collection post", "ja": "濾布を固定し捕集担当を確認"},
	"灰流先经过上游当班捕集片，再落入盆中": {"en": "Ash stream passes upstream collector plate into basin", "ja": "灰流が上流捕集片を経て水盆へ流入"},
	"主厨亲见同号转清": {"en": "Chef personally inspects matching clarity", "ja": "主厨が同番号の流出清浄を目視確認"},
	"主厨向工程师发出具名转清确认": {"en": "Chef issues signed clarity confirmation to engineer", "ja": "主厨が技師へ具名清浄確認を伝達"},
	"取下布擦地，留下地板纤维": {"en": "Remove cloth to wipe floor, leaving fibers", "ja": "布を外して床を拭き、繊維が残留"},
	"厨房清水洗布，盆内留灰": {"en": "Wash cloth with kitchen water, ash remains in basin", "ja": "厨房の清水で布を洗浄、盆に灰が残留"},
	"门底楔在烟灰路中受灰": {"en": "Door wedge collects ash in soot exhaust path", "ja": "扉の木楔が排灰経路で煤を受ける"},
	"撤取门底楔": {"en": "Remove bottom door wedge", "ja": "扉下の木楔を撤去"},
	"包裹同一沾灰楔，纸内形成转印": {"en": "Wrap ashed wedge in paper, creating transfer mark", "ja": "煤付着の木楔を紙で包み、転写痕が残る"},
	"撤楔后配重合门": {"en": "Counterweight closes door after wedge removal", "ja": "木楔撤去後、重りにより扉が閉鎖"},
	"双槽交回后取得白片": {"en": "Receive white ticket after dual-slot deposit", "ja": "二重投入口への返還後に白票を受領"},
	"把洗布后的清水误记为转清确认": {"en": "Misrecorded wash water as clarity confirmation", "ja": "布洗浄の清水を清浄化完了と誤認記録"},
	"打开前抽屉，原封条断开": {"en": "Front drawer opened, breaking original seal", "ja": "前抽出を開け、元の封緘が破断"},
	"从前抽屉后取同一红筹放到椅上": {"en": "Retrieved same red token from drawer and placed on chair", "ja": "前抽出から同一の紅籌を取り出し椅子へ載置"},
	"现实铜口质询索取院务移物单": {"en": "Query copper mouth for academy transfer docket", "ja": "真鍮口への質疑により院務移管伝票を請求"},
	"未归筹直接置椅": {"en": "Unreturned token placed directly on chair", "ja": "未返還の紅籌を直接椅子へ載置"},
	"候选历史尚未提交": {"en": "Hypothesis not yet submitted", "ja": "仮説履歴は未提出"},
	"动作提交": {"en": "Action submitted", "ja": "動作提出"},
}

func _update_caption() -> void:
	var caption := ""
	if frame > 0 and frame <= events.size():
		caption = str(_state(frame).get("caption", events[frame-1]))
	var loc := TranslationServer.get_locale()
	if loc.begins_with("en"):
		caption = CAPTION_TR.get(caption, {}).get("en", caption)
	elif loc.begins_with("ja"):
		caption = CAPTION_TR.get(caption, {}).get("ja", caption)
	caption = caption.split("\n")[0]
	var font := caption_font
	if loc.begins_with("ja"):
		font = JAPANESE_PRINT_FONT
	$Caption.add_theme_font_override("font", font)
	while caption.length() > 1 and font.get_string_size(caption, HORIZONTAL_ALIGNMENT_LEFT, -1, 18).x > 616:
		caption = caption.left(caption.length() - 2) + "…"
	$Caption.text = caption

func _object_point(location: String) -> Vector2:
	var alias := {"工程师霍铎":"工程师","主厨沈鼎":"厨师","音乐家苏泠":"音乐家"}
	var actor: String = alias.get(location,location)
	if figures.has(actor): return figures[actor].position + Vector2(0,-12)
	var sites := {"rack":"E10","receiver":"E13","chair":"E15","clip":"E08","paper":"E07","A":"E13","B":"E03","C":"E09","D":"E06"}
	if sites.has(location): return source.sites[sites[location]].point + Vector2(0,-8)
	return Vector2(320,652)
