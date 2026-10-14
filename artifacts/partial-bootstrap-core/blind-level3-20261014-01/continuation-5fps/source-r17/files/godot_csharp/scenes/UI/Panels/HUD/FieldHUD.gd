@tool
extends CanvasLayer
## 场景 HUD：标题、时段、底部提示、悬停提示、旁白、整屏渐变与章节闪标题。
## 所有章节共用；布局、颜色、字体在场景节点里改。编辑器里 Editor Preview 字段直接驱动同一组 set_* 方法。

const Paper := preload("res://scenes/UI/Components/Paper.gd")
const HOVER_OFFSET := Vector2(16, -34)
const PresentationWait := preload("res://scenes/Shared/PresentationWait.gd")

signal fade_cancelled

@export_group("Editor Preview")
@export var preview_title := "地点 · 房间":
	set(value):
		preview_title = value
		_refresh_preview()
@export var preview_time := "":
	set(value):
		preview_time = value
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
	for name in ["Vignette", "Bar", "Rule", "BottomShade", "Title"]:
		get_node(name).visible = _bar_visible
	$Time.visible = _bar_visible and $Time.text != ""
	$Hint.visible = _bar_visible and $Hint.text != ""
	$HoverTip.visible = _bar_visible and _hover_on
	$Aside.visible = _bar_visible and _aside_on

## 日期栏（设计规范第六部分 §4.7）：地点居中用标题字，右端钟点。
func set_title(t: String) -> void:
	$Title.text = t
	_layout()

func set_time(t: String) -> void:
	$Time.text = t
	_sync()
	_layout()

func _layout() -> void:
	var view_width := get_viewport().get_visible_rect().size.x if is_inside_tree() else 1280.0
	var title: Control = $Title
	title.size = title.get_combined_minimum_size()
	title.position.x = roundf((view_width - title.size.x) / 2.0)
	var time: Label = $Time
	time.size.x = time.get_minimum_size().x
	time.position.x = view_width - 24.0 - time.size.x

## 近身提示：纸签随文字伸缩，居中在屏底。
func set_hint(t: String) -> void:
	var hint: Label = $Hint
	hint.text = t
	hint.reset_size()
	var view_width := get_viewport().get_visible_rect().size.x if is_inside_tree() else 1280.0
	hint.position.x = roundf((view_width - hint.size.x) / 2.0)
	_sync()

func show_hover(t: String, screen_pos: Vector2) -> void:
	var tip: Label = $HoverTip
	tip.text = t
	tip.reset_size()
	var at := screen_pos + HOVER_OFFSET
	var view_size := get_viewport().get_visible_rect().size
	tip.position = Vector2(clampf(at.x, 8, maxf(8, view_size.x - 8 - tip.size.x)), clampf(at.y, 64, maxf(64, view_size.y - 32 - tip.size.y)))
	_hover_on = true
	_sync()

func hide_hover() -> void:
	_hover_on = false
	_sync()

## 心声：笔迹写在一张纸条上，随文字伸缩，居中在近身提示上方。
func show_aside(t: String, seconds := 3.0) -> void:
	var aside: Label = $Aside
	aside.text = t
	aside.reset_size()
	var view_width := get_viewport().get_visible_rect().size.x if is_inside_tree() else 1280.0
	aside.position.x = roundf((view_width - aside.size.x) / 2.0)
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
		fade_cancelled.emit()
		_fade_tween.kill()
	if seconds <= 0.0 or not is_inside_tree():
		fade.color.a = alpha
		return
	_fade_tween = create_tween()
	_fade_tween.tween_property(fade, "color:a", alpha, seconds)
	await PresentationWait.until(_fade_tween.finished, fade_cancelled)

func _exit_tree() -> void:
	fade_cancelled.emit()
	for tween in [_flash_tween, _fade_tween]:
		if tween != null and tween.is_valid(): tween.kill()

## 过场回放时整条 HUD 收起（黑幕与闪标题不受影响）。
func set_bar_visible(v: bool) -> void:
	_bar_visible = v
	_sync()
