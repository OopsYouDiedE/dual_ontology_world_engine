extends Control

## Material 3 Convention Interface (Task 7.14)
## Displays current version, clauses, viewer opinion, and signatory status.
## Revisions strictly invalidate prior signatures.
## Proxy signing is strictly prohibited (only the local viewer can sign for self).

signal opinion_updated(clause_id: String, opinion: String)
signal convention_signed(viewer_id: String, version: String)
signal convention_closed

const MaterialControls := preload("res://scenes/UI/Components/MaterialControls.gd")

@onready var modal_backdrop: ColorRect = %Backdrop
@onready var main_card: PanelContainer = %MainCard
@onready var title_label: Label = %TitleLabel
@onready var version_label: Label = %VersionLabel
@onready var close_btn: Button = %CloseButton
@onready var clauses_list: VBoxContainer = %ClausesList
@onready var signatories_list: VBoxContainer = %SignatoriesList
@onready var self_sign_btn: Button = %SelfSignButton
@onready var invalidation_notice: Label = %InvalidationNotice

var _is_open: bool = false
var _convention_data: Dictionary = {}
var _viewer_id: String = ""
var _viewer_signed: bool = false

func _ready() -> void:
	visible = false
	MaterialControls.apply(self)
	MaterialControls.prepare_button(close_btn)
	MaterialControls.prepare_button(self_sign_btn)
	close_btn.pressed.connect(close_convention)
	self_sign_btn.pressed.connect(_on_self_sign_pressed)

func is_convention_open() -> bool:
	return _is_open

## convention_data format:
## {
##   "convention_id": "medical_triage_pact",
##   "title": "战地医疗与救治优先权公约",
##   "version": "v1.2",
##   "is_revised_since_last_sign": true,
##   "viewer": "林晚",
##   "clauses": [
##     {
##       "id": "c01",
##       "title": "第一条：重症优先原则",
##       "content": "生命体征危重者享有最高抢救优先度，不受身份与阵营限制。",
##       "viewer_opinion": "agree" # "agree" | "reserved" | "object"
##     },
##     {
##       "id": "c02",
##       "title": "第二条：剩余物资共管原则",
##       "content": "药品与无菌耗材统一封存配膳台，任何单方挪用视为违约。",
##       "viewer_opinion": "reserved"
##     }
##   ],
##   "signatories": [
##     {
##       "actor": "苏泠",
##       "signed_version": "v1.1",
##       "status": "signed" # but if signed_version != version, it is invalidated
##     },
##     {
##       "actor": "林晚",
##       "signed_version": "v1.2",
##       "status": "signed"
##     },
##     {
##       "actor": "沈鼎",
##       "signed_version": "",
##       "status": "unsigned"
##     }
##   ]
## }
func open_convention(data: Dictionary) -> void:
	_convention_data = data.duplicate(true)
	_viewer_id = str(data.get("viewer", "林晚"))
	var version_str: String = str(data.get("version", "v1.0"))
	var is_revised: bool = bool(data.get("is_revised_since_last_sign", false))
	
	title_label.text = str(data.get("title", "行动公约草案"))
	version_label.text = "当前公约版本: %s" % version_str
	
	invalidation_notice.visible = is_revised
	if is_revised:
		invalidation_notice.text = "【注意】文本发生修订，所有基于旧版本的签署已按规则失效，须重新审阅并签署！"
		
	_rebuild_clauses(data.get("clauses", []))
	_rebuild_signatories(data.get("signatories", []), version_str)
	
	visible = true
	_is_open = true
	await get_tree().process_frame
	close_btn.grab_focus()

func _rebuild_clauses(clauses: Array) -> void:
	for child in clauses_list.get_children():
		clauses_list.remove_child(child)
		child.queue_free()
		
	for clause in clauses:
		if not clause is Dictionary:
			continue
		var cid: String = str(clause.get("id", ""))
		var ctitle: String = str(clause.get("title", "条款"))
		var ccontent: String = str(clause.get("content", ""))
		var opinion: String = str(clause.get("viewer_opinion", "agree"))
		
		var card := PanelContainer.new()
		var box := StyleBoxFlat.new()
		box.bg_color = MaterialControls.COLOR_SURFACE_VARIANT
		box.corner_radius_top_left = 8
		box.corner_radius_top_right = 8
		box.corner_radius_bottom_right = 8
		box.corner_radius_bottom_left = 8
		box.content_margin_left = 16.0
		box.content_margin_top = 10.0
		box.content_margin_right = 16.0
		box.content_margin_bottom = 10.0
		card.add_theme_stylebox_override("panel", box)
		
		var vbox := VBoxContainer.new()
		vbox.add_theme_constant_override("separation", 6)
		
		var htop := HBoxContainer.new()
		var title_lbl := Label.new()
		title_lbl.text = ctitle
		title_lbl.add_theme_font_size_override("font_size", 16)
		title_lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		htop.add_child(title_lbl)
		vbox.add_child(htop)
		
		var content_lbl := Label.new()
		content_lbl.text = ccontent
		content_lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		content_lbl.add_theme_font_size_override("font_size", 14)
		content_lbl.add_theme_color_override("font_color", MaterialControls.COLOR_ON_SURFACE)
		vbox.add_child(content_lbl)
		
		# Viewer opinion segmented selector
		var op_row := HBoxContainer.new()
		op_row.add_theme_constant_override("separation", 8)
		
		var op_caption := Label.new()
		op_caption.text = "本人意见 (只能本人表决):"
		op_caption.add_theme_font_size_override("font_size", 13)
		op_caption.add_theme_color_override("font_color", MaterialControls.COLOR_OUTLINE)
		op_row.add_child(op_caption)
		
		var btn_agree := Button.new()
		MaterialControls.prepare_button(btn_agree)
		btn_agree.text = "赞同" if opinion != "agree" else "[已选: 赞同]"
		btn_agree.pressed.connect(func():
			opinion_updated.emit(cid, "agree")
		)
		op_row.add_child(btn_agree)
		
		var btn_reserved := Button.new()
		MaterialControls.prepare_button(btn_reserved)
		btn_reserved.text = "保留" if opinion != "reserved" else "[已选: 保留]"
		btn_reserved.pressed.connect(func():
			opinion_updated.emit(cid, "reserved")
		)
		op_row.add_child(btn_reserved)
		
		var btn_object := Button.new()
		MaterialControls.prepare_button(btn_object)
		btn_object.text = "反对" if opinion != "object" else "[已选: 反对]"
		btn_object.pressed.connect(func():
			opinion_updated.emit(cid, "object")
		)
		op_row.add_child(btn_object)
		
		vbox.add_child(op_row)
		card.add_child(vbox)
		clauses_list.add_child(card)

func _rebuild_signatories(signatories: Array, current_version: String) -> void:
	for child in signatories_list.get_children():
		signatories_list.remove_child(child)
		child.queue_free()
		
	_viewer_signed = false
	for person in signatories:
		if not person is Dictionary:
			continue
		var actor: String = str(person.get("actor", "参与者"))
		var signed_ver: String = str(person.get("signed_version", ""))
		var raw_status: String = str(person.get("status", "unsigned"))
		var is_viewer: bool = (actor == _viewer_id)
		
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 12)
		
		var name_lbl := Label.new()
		name_lbl.text = actor + (" (本人)" if is_viewer else "")
		name_lbl.custom_minimum_size = Vector2(120, 0)
		name_lbl.add_theme_font_size_override("font_size", 14)
		row.add_child(name_lbl)
		
		# Validation check: if signed_version != current_version, old signature is INVALIDATED
		var status_badge: PanelContainer
		if raw_status == "signed":
			if signed_ver == current_version:
				status_badge = MaterialControls.create_badge("已签署 (" + signed_ver + ")", "safe")
				if is_viewer:
					_viewer_signed = true
			else:
				status_badge = MaterialControls.create_badge("[条款已修订，旧确认失效]", "critical")
		else:
			status_badge = MaterialControls.create_badge("尚未签署", "warning")
		row.add_child(status_badge)
		
		# Anti-proxy signing notice
		if not is_viewer:
			var non_proxy_lbl := Label.new()
			non_proxy_lbl.text = "不可代签"
			non_proxy_lbl.add_theme_font_size_override("font_size", 12)
			non_proxy_lbl.add_theme_color_override("font_color", MaterialControls.COLOR_OUTLINE)
			row.add_child(non_proxy_lbl)
			
		signatories_list.add_child(row)
		
	# Update self sign button
	if _viewer_signed:
		self_sign_btn.text = "本人已签署本版公约"
		self_sign_btn.disabled = true
	else:
		self_sign_btn.text = "本人确认并签署公约"
		self_sign_btn.disabled = false

func _on_self_sign_pressed() -> void:
	if _viewer_signed:
		return
	var version_str: String = str(_convention_data.get("version", "v1.0"))
	convention_signed.emit(_viewer_id, version_str)
	_viewer_signed = true
	self_sign_btn.text = "本人已签署本版公约"
	self_sign_btn.disabled = true

func close_convention() -> void:
	if not _is_open:
		return
	_is_open = false
	visible = false
	convention_closed.emit()

func _gui_input(event: InputEvent) -> void:
	if not _is_open:
		return
	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("cancel"):
		close_convention()
		get_viewport().set_input_as_handled()
