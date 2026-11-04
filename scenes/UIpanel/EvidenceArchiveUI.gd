@tool
extends "res://scenes/UIpanel/CasebookUI/BookPageLayout.gd"
## Reference layout: self examination and vertically arranged belongings.
signal entry_selected(entry_id: String, category: String)
signal pin_toggled(pinned_entry_id: String, is_pinned: bool)
signal pin_requested(data: Dictionary)
const Icons := preload("res://scenes/UIpanel/FieldHUD/FieldChrome.gd")
const PORTRAIT_PATH := "res://scenes/UIpanel/EvidenceArchiveUI/detective_portrait.png"
const CATEGORIES := {"self":"自检", "evidence":"证物", "documents":"文献", "people":"人物", "items":"随身"}
const CATALOG_SIZE := 5
@export var current_category := "self"
var _selected_id := "self"
var _selected: Dictionary = {}
var _pinned: Dictionary = {}
var _catalog_page := 0

func set_archive_data(data: Dictionary) -> void:
	configure({"casebook":data,"protagonist":data.get("protagonist",{})},archive)

func select_entry(id: String) -> void:
	var record: Dictionary = _entry(id)
	if record.is_empty(): return
	_selected = record
	_selected_id = id
	current_category = "people" if str(record.get("category","")) == "people" else "documents" if _is_document(record) else "evidence"
	if is_node_ready(): _build_content()

func set_pinned(data: Dictionary) -> void:
	_pinned = data.duplicate(true)
	if is_node_ready(): _build_content()

func _build_content() -> void:
	if current_category == "self":
		_begin("自检","随身")
		_render_self()
		_render_pocket()
		return
	for record: Dictionary in _catalog():
		if str(record.get("id","")) == _selected_id: _selected = record; break
	if not _pinned.is_empty():
		_begin(_title(_pinned),_title(_selected,"档案"))
		_render_document(left_body,_pinned)
		_render_document(right_body,_selected)
		_spacer(left_body,0,true)
		_button(left_body,"取下图钉",_unpin).add_theme_font_size_override("font_size",_scaled(24))
		return
	_begin(str(CATEGORIES.get(current_category,"档案")),_title(_selected,""))
	_render_catalog()
	_render_document(right_body,_selected)
	if not _selected.is_empty():
		_spacer(right_body,0,true)
		var row := _row(right_body,30)
		_button(row,"钉选",_pin_selection).add_theme_font_size_override("font_size",_scaled(26))
		_button(row,"记一笔",func(): ui_event.emit({"kind":"book_page","page":"B05","related":[_selected_id]})).add_theme_font_size_override("font_size",_scaled(26))

func _self_record() -> Dictionary:
	var book_data := _dict(snapshot.get("casebook",{}))
	var record := _dict(book_data.get("protagonist",book_data.get("self",snapshot.get("protagonist",snapshot.get("self",{})))))
	var viewer := str(snapshot.get("viewer",""))
	if record.has("id") and not viewer.is_empty() and str(record.id) != viewer: return {}
	return record

func _render_self() -> void:
	var person := _self_record()
	var portrait_row := _row(left_body,26)
	portrait_row.custom_minimum_size.y = 300
	var portrait := _texture(str(person.get("image",person.get("portrait",""))))
	if portrait == null and str(snapshot.get("viewer",person.get("id",""))) == "detective": portrait = _texture(PORTRAIT_PATH)
	if portrait != null:
		var picture := _picture(portrait_row,portrait,290)
		picture.custom_minimum_size.x = 235
		picture.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	else:
		portrait_row.add_child(Icons.item_icon({"kind":"portrait","name":_title(person,"我")},Vector2(235,290)))
	var states := _column(portrait_row,24)
	states.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	var shown := 0
	for item in _array(person.get("observations",[])):
		var text := _title(item,"") if item is Dictionary else str(item)
		if text.strip_edges().is_empty(): continue
		_status(states,text,Color("b1ae93") if shown == 0 else Color("c9af8e"))
		shown += 1
		if shown == 2: break
	for item in _array(person.get("abnormals",snapshot.get("abnormals",[]))):
		if shown >= 2: break
		var text := _title(item,"") if item is Dictionary else str(item)
		if text.is_empty(): continue
		_status(states,text,Color("c5a28f"))
		shown += 1
	if shown == 0: _text(states,_title(person,str(snapshot.get("viewer_name","我"))),35)
	_rule(left_body)
	_spacer(left_body,6)
	var contracts: Array = _array(person.get("contracts",snapshot.get("active_contracts",[])))
	if contracts.is_empty(): _text(left_body,"契约：尚无记录",36)
	else:
		for contract in contracts:
			var text := _title(contract) if contract is Dictionary else str(contract)
			_button(left_body,"契约："+text,func(): ui_event.emit({"kind":"book_page","page":"B04"})).add_theme_font_size_override("font_size",_scaled(36))
	_spacer(left_body,0,true)
	if not _entries("evidence").is_empty() or not _entries("people").is_empty() or not _entries("records").is_empty():
		_button(left_body,"查阅档案  ›",func(): _choose_category("evidence")).add_theme_font_size_override("font_size",_scaled(25))

func _status(parent: Node, text: String, color: Color) -> void:
	var badge := Control.new()
	badge.custom_minimum_size = Vector2(245,76)
	parent.add_child(badge)
	badge.draw.connect(func():
		var w := badge.size.x
		var h := badge.size.y
		var c := 12.0
		badge.draw_colored_polygon(PackedVector2Array([Vector2(c,0),Vector2(w-c,0),Vector2(w,c),Vector2(w,h-c),Vector2(w-c,h),Vector2(c,h),Vector2(0,h-c),Vector2(0,c)]),color)
	)
	var label := Label.new()
	label.text = text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size",_scaled(32))
	label.add_theme_color_override("font_color",_ink)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	badge.add_child(label)
	label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)

func _render_pocket() -> void:
	var items := _pocket()
	if items.is_empty():
		_spacer(right_body,40)
		_text(right_body,"暂无随身物品",32,true)
		return
	for value: Dictionary in items:
		var row := _row(right_body,36)
		row.custom_minimum_size.y = 140
		row.add_child(Icons.item_icon(value,Vector2(140,140)))
		var caption := _title(value)
		if int(value.get("count",1)) > 1: caption += " ×%d" % int(value.count)
		var button := _button(row,caption,func(): current_category="items"; _select(value))
		button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		button.add_theme_font_size_override("font_size",_scaled(37))
		button.tooltip_text = _body(value)
	_spacer(right_body,0,true)

func _render_document(parent: VBoxContainer, record: Dictionary) -> void:
	if record.is_empty(): return
	_spacer(parent,38)
	var illustration := _texture(str(record.get("image","")))
	if illustration != null: _picture(parent,illustration,220)
	var content := _scroll(parent)
	_prose(content,_body(record),40)
	_spacer(parent,40)
	var source := _text(parent,"—— 来源："+str(record.get("source_label",record.get("source",_source(record)))),30)
	source.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT

func _choose_category(category: String) -> void:
	current_category=category
	_catalog_page=0
	if category == "self":
		_selected_id="self"
		_selected={}
	else:
		var rows := _catalog()
		_selected=rows[0] if not rows.is_empty() else {}
		_selected_id=str(_selected.get("id",""))
	_build_content()

func _render_catalog() -> void:
	var links := HFlowContainer.new()
	left_body.add_child(links)
	for key in CATEGORIES:
		var category := str(key)
		var button := _button(links,str(CATEGORIES[key]),func(): _choose_category(category),category==current_category)
		button.add_theme_font_size_override("font_size",_scaled(24))
		button.size_flags_horizontal=Control.SIZE_SHRINK_BEGIN
	var entries := _catalog()
	if entries.is_empty():
		_text(left_body,"尚未收录",32,true)
		return
	var count := maxi(1,ceili(entries.size()/float(CATALOG_SIZE)))
	_catalog_page=clampi(_catalog_page,0,count-1)
	for index in range(_catalog_page*CATALOG_SIZE,mini(entries.size(),(_catalog_page+1)*CATALOG_SIZE)):
		var value: Dictionary=entries[index]
		_button(left_body,_title(value),func(): _select(value),str(value.get("id",""))==_selected_id).add_theme_font_size_override("font_size",_scaled(34))
	_spacer(left_body,0,true)
	_pager(left_body,_catalog_page,count,func(index: int): _catalog_page=index; _build_content())

func _select(value: Dictionary) -> void:
	_selected=value
	_selected_id=str(value.get("id",""))
	_build_content()
	entry_selected.emit(_selected_id,current_category)

func _is_document(value: Dictionary) -> bool:
	return str(value.get("kind",value.get("type",""))) in ["document","literature","text","book"] or str(value.get("category",""))=="documents"

func _catalog() -> Array:
	var result: Array=[]
	match current_category:
		"people": return _entries("people")
		"items": return _pocket()
		"documents":
			for value: Dictionary in _entries("evidence")+_entries("records"):
				if _is_document(value): result.append(value)
			for value in _array(_dict(snapshot.get("casebook",{})).get("documents",[])):
				if value is Dictionary and bool(value.get("acquired",false)): result.append(value)
		_:
			for value: Dictionary in _entries("evidence")+_entries("testimony"):
				if not _is_document(value): result.append(value)
	return result

func _pocket() -> Array:
	var result: Array=[]
	var seen: Dictionary={}
	var inventory := _dict(snapshot.get("inventory",{}))
	for value in _array(inventory.get("items",_self_record().get("pocket",[]))):
		if not value is Dictionary or int(value.get("count",1))<=0: continue
		var id := str(value.get("id",""))
		if not seen.has(id):
			result.append(value)
			seen[id]=true
	for value: Dictionary in _entries("items"):
		if str(value.get("ownership","")) not in ["held","possessed","carried"]: continue
		var id := str(value.get("id",""))
		if not seen.has(id):
			result.append(value)
			seen[id]=true
	return result

func _pin_selection() -> void:
	if _selected.is_empty(): return
	_pinned=_selected.duplicate(true)
	pin_requested.emit(_pinned)
	pin_toggled.emit(_selected_id,true)
	_build_content()

func _unpin() -> void:
	_pinned={}
	pin_requested.emit({})
	pin_toggled.emit("",false)
	_build_content()

func _reset_view() -> void:
	_selected_id="self"
	_selected={}
	_pinned={}
	current_category="self"
	_catalog_page=0
