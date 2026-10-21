extends VBoxContainer
## A chapter flyleaf in the shared physical book. It owns presentation only;
## the host decides when a real session and its first field are ready.
signal quit_requested

var phase := "opening"
var failure := ""
var _recoverable := false
var _chapter: Label
var _title: Label
var _caption: Label
var _status: Label
var _detail: RichTextLabel
var _quit: Button

func _ready() -> void:
	name = "OpeningPage"
	theme_type_variation = &"BookColumn"
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	custom_minimum_size.y = 500.0
	_spacer(48.0)
	_chapter = _label(&"BookCaptionLabel")
	_spacer(16.0)
	_title = _label(&"BookHeading")
	_spacer(20.0)
	var rule := HSeparator.new()
	rule.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	rule.custom_minimum_size.x = 104.0
	add_child(rule)
	_spacer(24.0)
	_caption = _label(&"BookSectionHeading")
	_spacer(64.0)
	_status = _label(&"BookCaptionLabel")
	_detail = RichTextLabel.new()
	_detail.name = "OpeningFailure"
	_detail.theme_type_variation = &"BookCaption"
	_detail.fit_content = true
	_detail.bbcode_enabled = false
	_detail.scroll_active = false
	_detail.selection_enabled = true
	_detail.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	add_child(_detail)
	_quit = Button.new()
	_quit.name = "CloseAfterOpeningFailure"
	_quit.theme_type_variation = &"BookOption"
	_quit.pressed.connect(func(): quit_requested.emit())
	add_child(_quit)
	_refresh()

func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSLATION_CHANGED and is_node_ready(): _refresh()

func set_phase(value: String) -> void:
	phase = value
	if is_node_ready(): _refresh()

func show_failure(message: String, recoverable := false) -> void:
	failure = message
	_recoverable = recoverable
	phase = "failed"
	if is_node_ready(): _refresh()

func _refresh() -> void:
	_chapter.text = "I"
	_title.text = tr("第一章")
	_caption.text = tr("新的一页")
	var messages := {
		"opening": "案卷正在展开……",
		"preparing": "正在铺开这一页……",
		"ready": "从这里，开始新的记录。",
		"failed": "未能打开这一页。请合卷后重新打开游戏。",
	}
	_status.text = tr(str(messages.get(phase, messages.opening)))
	if phase == "failed" and _recoverable: _status.text = tr("暂时无法打开这一页，可以返回封面后再试。")
	_detail.text = failure
	_detail.visible = not failure.is_empty()
	_quit.text = tr("合卷")
	_quit.visible = not failure.is_empty()

func _label(variation: StringName) -> Label:
	var label := Label.new()
	label.theme_type_variation = variation
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	add_child(label)
	return label

func _spacer(height: float) -> void:
	var space := Control.new()
	space.custom_minimum_size.y = height
	space.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(space)
