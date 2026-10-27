extends Control
## Cinematic presentation of DV excerpts; no campaign outcomes are committed here.
var stages: Array = []
var kit: Node3D
var view: SubViewport
var subtitle: RichTextLabel
var heading: Label
var condition: Label
var next: Button
var play: Button
var stage_index := 0
var line_index := -1
var playing := false
var busy := false
var elapsed := 0.0
var hold_seconds := 4.0
var generation := 0
var bindings: Dictionary = {}

func _ready() -> void:
	var data: Dictionary = preload("res://scenes/Host/DvPresentationContent.cs").new().ReadRecord("res://Content/presentation.dv", "narratives", "highlights")
	stages = data.stages
	bindings = data.get("actor_bindings", {})
	var column := VBoxContainer.new()
	column.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(column)
	var navigation := HBoxContainer.new()
	column.add_child(navigation)
	for index in stages.size():
		var button := Button.new()
		button.text = stages[index].title
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.pressed.connect(_select_stage.bind(index))
		navigation.add_child(button)
	heading = Label.new()
	heading.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(heading)
	var container := SubViewportContainer.new()
	container.stretch = true
	container.size_flags_vertical = Control.SIZE_EXPAND_FILL
	column.add_child(container)
	view = SubViewport.new()
	view.own_world_3d = true
	view.size = Vector2i(1280,720)
	view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	container.add_child(view)
	kit = preload("res://scenes/World/Art/library.tscn").instantiate()
	kit.show_library_controls = false
	view.add_child(kit)
	condition = Label.new()
	condition.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	column.add_child(condition)
	subtitle = RichTextLabel.new()
	subtitle.custom_minimum_size.y = 92
	subtitle.fit_content = true
	subtitle.add_theme_font_size_override("normal_font_size", 24)
	column.add_child(subtitle)
	var controls := HBoxContainer.new()
	column.add_child(controls)
	play = Button.new()
	play.text = "播放"
	play.pressed.connect(func():
		playing = not playing
		play.text = "暂停" if playing else "播放"
		elapsed = hold_seconds)
	controls.add_child(play)
	next = Button.new()
	next.text = "下一句"
	next.pressed.connect(_advance)
	controls.add_child(next)
	var outlines := CheckButton.new()
	outlines.text = "细描边"
	outlines.button_pressed = true
	outlines.toggled.connect(kit.set_contour_enabled)
	controls.add_child(outlines)
	var dust := CheckButton.new()
	dust.text = "空气微粒"
	dust.button_pressed = true
	dust.toggled.connect(func(value): kit.atmosphere.particles_enabled = value)
	controls.add_child(dust)
	_select_stage(0)

func _select_stage(index: int) -> void:
	generation += 1
	stage_index = index
	line_index = -1
	busy = false
	elapsed = 0.0
	next.disabled = false
	kit.select_asset("recording:" + str(stages[index].id))
	heading.text = stages[index].title + " · " + stages[index].location + " · 三维分镜预览"
	condition.text = stages[index].condition
	subtitle.text = ""

func _process(delta: float) -> void:
	if not playing or busy: return
	elapsed += delta
	if elapsed >= hold_seconds:
		if line_index + 1 < stages[stage_index].lines.size():
			_advance()
		elif stage_index + 1 < stages.size():
			_select_stage(stage_index + 1)
		else:
			playing = false
			play.text = "播放"

func _advance() -> void:
	if busy or line_index + 1 >= stages[stage_index].lines.size(): return
	busy = true
	next.disabled = true
	line_index += 1
	var epoch := generation
	var line: Dictionary = stages[stage_index].lines[line_index]
	subtitle.text = str(line.get("speaker", "")) + "\n" + str(line.text)
	hold_seconds = maxf(3.5, str(line.text).length() / 5.0)
	elapsed = 0.0
	var identity: String = bindings.get(str(line.get("actor", "")), str(line.get("actor", "")))
	for actor in kit.figures:
		if str(actor.get_meta("actor_id", "")) != identity: continue
		var animator := actor.get_node_or_null("AnimationPlayer") as AnimationPlayer
		if animator != null:
			var clip := "use" if str(line.get("cue", "")) == "gesture" else "talk"
			animator.play(clip, 0.15)
			await get_tree().create_timer(1.2).timeout
			if epoch != generation: return
			animator.play("idle", 0.15)
		break
	if epoch != generation: return
	busy = false
	next.disabled = line_index + 1 >= stages[stage_index].lines.size()
