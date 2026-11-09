extends Control

## 说服与采纳博弈演出（左下角卡片栈）：提议者 → 对象、提议内容、出示的证据、
## “权衡中…”过渡、采纳 / 婉拒结果与对方原话。依双本体原则：提议者只看到对方答复、
## 自己出示的证据与对方说出的理由类别，不显示对方私有的分数、阈值、记忆与顾虑。

const FONT := preload("res://assets/external/NotoSerifSC/NotoSerifSC.ttf")
const INK := Color("efe4cc")
const MUTED := Color("a99c82")
const GOLD := Color("d9b56c")
const MAX_CARDS := 3
const CARD_SECONDS := 10.0
const REASONS := {
	"evidence_missing": "对方认为证据还不够",
	"independent_opinion_rejected": "对方权衡利弊后没有同意",
	"independent_opinion_accepted": "对方被说服了",
	"contradicts_own_memory": "提议与对方亲眼所见相矛盾",
}

var _stack: VBoxContainer
var _seen: Dictionary = {}

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_stack = VBoxContainer.new()
	_stack.name = "PersuasionStack"
	_stack.anchor_top = 1.0
	_stack.anchor_bottom = 1.0
	_stack.offset_left = 18.0
	_stack.offset_right = 470.0
	_stack.offset_top = -130.0
	_stack.offset_bottom = -130.0
	_stack.grow_vertical = Control.GROW_DIRECTION_BEGIN
	_stack.alignment = BoxContainer.ALIGNMENT_END
	_stack.add_theme_constant_override("separation", 8)
	_stack.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_stack)

func present(record: Dictionary) -> void:
	var key := "%s|%s|%s" % [str(record.get("execution", "")), str(record.get("speaker", "")), str(record.get("label", ""))]
	if _seen.has(key): return
	_seen[key] = true
	var accepted := bool(record.get("accepted", false))
	var own := bool(record.get("own", false))
	var card := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.07, 0.062, 0.055, 0.9)
	style.border_color = Color(GOLD, 0.5)
	style.border_width_left = 3
	style.set_corner_radius_all(8)
	style.content_margin_left = 14; style.content_margin_right = 14
	style.content_margin_top = 10; style.content_margin_bottom = 10
	card.add_theme_stylebox_override("panel", style)
	card.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 3)
	card.add_child(column)
	var head := _label(column, 15, GOLD)
	head.text = "%s → %s%s" % [str(record.get("speaker_name", "")), str(record.get("target_name", "")), "" if own else "（旁观）"]
	var body := _label(column, 14, INK)
	body.text = str(record.get("label", ""))
	if own:
		var evidence: Array = record.get("evidence", []) if record.get("evidence") is Array else []
		var missing := int(record.get("missing_count", 0))
		var ev := _label(column, 13, MUTED)
		ev.text = "出示证据 %d 条%s" % [evidence.size(), "；仍缺 %d 条" % missing if missing > 0 else ""]
	var verdict := _label(column, 17, INK)
	verdict.text = "权衡中…"
	var reply := _label(column, 13, MUTED)
	_stack.add_child(card)
	while _stack.get_child_count() > MAX_CARDS:
		var oldest := _stack.get_child(0)
		_stack.remove_child(oldest)
		oldest.queue_free()
	card.modulate.a = 0.0
	var tween := card.create_tween()
	tween.tween_property(card, "modulate:a", 1.0, 0.25)
	tween.tween_interval(0.9)
	tween.tween_callback(func(): _reveal(verdict, reply, style, record, accepted))
	tween.tween_interval(CARD_SECONDS)
	tween.tween_property(card, "modulate:a", 0.0, 0.6)
	tween.tween_callback(card.queue_free)

func _reveal(verdict: Label, reply: Label, style: StyleBoxFlat, record: Dictionary, accepted: bool) -> void:
	if not is_instance_valid(verdict): return
	var color := Color("8fd18a") if accepted else Color("e07b62")
	verdict.text = "◎ 采纳" if accepted else "× 婉拒"
	verdict.add_theme_color_override("font_color", color)
	style.border_color = color
	var reason := str(REASONS.get(str(record.get("reason", "")), ""))
	var words := str(record.get("reply", "")).strip_edges()
	if words.length() > 90: words = words.left(90) + "…"
	reply.text = reason + ("\n" + words if not words.is_empty() else "")

func _label(parent: Node, size: int, color: Color) -> Label:
	var label := Label.new()
	label.add_theme_font_override("font", FONT)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(label)
	return label
