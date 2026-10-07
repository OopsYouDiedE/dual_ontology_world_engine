@tool
extends CanvasLayer
## 场景 HUD：标题、时段、按键提示、底部提示、悬停提示、旁白、整屏渐变与章节闪标题。
## 所有章节共用；布局、颜色、字体在场景节点里改。编辑器里 Editor Preview 字段直接驱动同一组 set_* 方法。

const Paper := preload("res://scenes/Shared/UI/Common/Paper.gd")
const VIEW_SIZE := Vector2(1280, 992)
const HOVER_OFFSET := Vector2(16, -34)

@export_group("Editor Preview")
@export var preview_title := "生活翼 · 活动厅":
	set(value):
		preview_title = value
		_refresh_preview()
@export var preview_time := "":
	set(value):
		preview_time = value
		_refresh_preview()
@export var preview_keys: PackedStringArray = PackedStringArray(["E / 点击  检视", "Tab  笔记本", "M  学园图"]):
	set(value):
		preview_keys = value
		_refresh_preview()
@export var preview_hint := "":
	set(value):
		preview_hint = value
		_refresh_preview()
@export var preview_hover := "":
	set(value):
		preview_hover = value
		_refresh_preview()
@export var preview_aside := "":
	set(value):
		preview_aside = value
		_refresh_preview()
@export var preview_flash := "":
	set(value):
		preview_flash = value
		_refresh_preview()
@export_range(0.0, 1.0, 0.01) var preview_fade := 0.0:
	set(value):
		preview_fade = value
		_refresh_preview()

var _bar_visible := true
var _hover_on := false
var _aside_on := false
var _aside_left := 0.0
var _flash_tween: Tween
var _fade_tween: Tween

func _ready() -> void:
	if Engine.is_editor_hint():
		_refresh_preview()
	else:
		_sync()

func _process(delta: float) -> void:
	if Engine.is_editor_hint() or _aside_left <= 0.0:
		return
	_aside_left -= delta
	if _aside_left <= 0.0:
		_aside_on = false
		_sync()

func _refresh_preview() -> void:
	if not Engine.is_editor_hint() or not is_node_ready():
		return
	set_title(preview_title)
	set_time(preview_time)
	set_keys(Array(preview_keys))
	set_hint(preview_hint)
	if preview_hover.is_empty():
		hide_hover()
	else:
		show_hover(preview_hover, Vector2(480, 400) - HOVER_OFFSET)
	_aside_on = not preview_aside.is_empty()
	$Aside.text = preview_aside
	$Flash.text = "[center]%s[/center]" % Paper.story_text(preview_flash)
	$Flash.modulate.a = 1.0
	$Flash.visible = not preview_flash.is_empty()
	$Fade.color.a = preview_fade
	_sync()

func _sync() -> void:
	for name in ["Vignette", "TopShade", "BottomShade", "Title"]:
		get_node(name).visible = _bar_visible
	$Time.visible = _bar_visible and $Time.text != ""
	$Keys.visible = _bar_visible and $Keys.text != ""
	$Hint.visible = _bar_visible and $Hint.text != ""
	$HoverTip.visible = _bar_visible and _hover_on
	$Aside.visible = _bar_visible and _aside_on

func set_title(t: String) -> void:
	$Title.text = t

func set_time(t: String) -> void:
	$Time.text = t
	_sync()

func set_keys(keys: Array) -> void:
	$Keys.text = "　　".join(PackedStringArray(keys.map(func(k): return str(k))))
	_sync()

func set_hint(t: String) -> void:
	$Hint.text = t
	_sync()

func show_hover(t: String, screen_pos: Vector2) -> void:
	var tip: Label = $HoverTip
	tip.text = t
	tip.reset_size()
	var at := screen_pos + HOVER_OFFSET
	tip.position = Vector2(clampf(at.x, 8, VIEW_SIZE.x - 8 - tip.size.x), clampf(at.y, 64, VIEW_SIZE.y - 32))
	_hover_on = true
	_sync()

func hide_hover() -> void:
	_hover_on = false
	_sync()

func show_aside(t: String, seconds := 3.0) -> void:
	$Aside.text = t
	_aside_on = true
	_aside_left = seconds
	_sync()

func flash_title(t: String) -> void:
	var flash: RichTextLabel = $Flash
	flash.text = "[center]%s[/center]" % Paper.story_text(t)
	flash.modulate.a = 1.0
	flash.visible = true
	if _flash_tween != null and _flash_tween.is_valid():
		_flash_tween.kill()
	if Engine.is_editor_hint():
		return
	_flash_tween = create_tween()
	_flash_tween.tween_property(flash, "modulate:a", 0.0, 1.6).set_delay(0.9)
	_flash_tween.tween_callback(flash.hide)

## 整屏黑幕渐变到 alpha；可 await。
func fade_to(alpha: float, seconds: float) -> void:
	var fade: ColorRect = $Fade
	if _fade_tween != null and _fade_tween.is_valid():
		_fade_tween.kill()
	if seconds <= 0.0 or not is_inside_tree():
		fade.color.a = alpha
		return
	_fade_tween = create_tween()
	_fade_tween.tween_property(fade, "color:a", alpha, seconds)
	await _fade_tween.finished

## 过场回放时整条 HUD 收起（黑幕与闪标题不受影响）。
func set_bar_visible(v: bool) -> void:
	_bar_visible = v
	_sync()
