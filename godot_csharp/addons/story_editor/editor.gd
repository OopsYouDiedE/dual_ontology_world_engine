@tool
extends VBoxContainer
## Game Designer & Story Editor: unified visual blocking, mechanism composer and dialogue coordinator.

const StoryScore = preload("res://scenes/Story/StoryScore.gd")
const StageCanvas = preload("res://addons/story_editor/stage_canvas.gd")

const ACTOR_NAMES := ["侦探", "工程师", "厨师", "音乐家"]
const FACINGS := ["front", "back", "left", "right"]
const POSES := ["idle", "walk", "sit", "talk", "think", "work", "look"]
const MECHANISM_KINDS := ["(无机制)", "safe", "inspect", "perform", "cancel"]
const SAFE_OPERATIONS := ["reward", "press", "release", "gap", "pipe", "close", "red", "handle", "push_red", "push_handle", "white"]

var source: CodeEdit
var nodes: ItemList
var tabs: TabContainer
var json_editor: CodeEdit
var canvas: StageCanvas
var status: Label
var file_label: Label

# Visual Inspector Controls
var ui_id: LineEdit
var ui_wait: CheckBox
var ui_mech_enabled: CheckBox
var ui_mech_kind: OptionButton
var ui_mech_op: OptionButton
var ui_mech_actor: OptionButton
var ui_mech_id: LineEdit
var ui_track_actor: OptionButton
var ui_track_face: OptionButton
var ui_track_pose: OptionButton
var ui_track_speed: SpinBox
var ui_track_seat: SpinBox
var ui_track_stand: CheckBox
var ui_points_label: Label

var open_dialog: FileDialog
var save_dialog: FileDialog
var reference_dialog: FileDialog
var export_dialog: FileDialog
var discard_dialog: ConfirmationDialog

var file_path: String = ""
var disk_text: String = ""
var saved_text: String = ""
var beats: Array = []
var selected_line: int = 0
var updating: bool = false
var syncing_form: bool = false
var pending_open: String = ""
var json_baseline: String = ""
var selected_source: String = ""
var current_beat: Dictionary = {}

func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	size_flags_vertical = Control.SIZE_EXPAND_FILL

	_build_toolbar()

	var split: HSplitContainer = HSplitContainer.new()
	split.size_flags_vertical = Control.SIZE_EXPAND_FILL
	add_child(split)

	_build_source_view(split)
	_build_designer_view(split)

	status = Label.new()
	status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	add_child(status)

	_build_dialogs()
	_refresh()

func _build_toolbar() -> void:
	var toolbar: HBoxContainer = HBoxContainer.new()
	add_child(toolbar)
	_button(toolbar, "打开 .dialogue", func() -> void: open_dialog.popup_centered_ratio(0.75))
	_button(toolbar, "保存", _save)
	_button(toolbar, "另存为", func() -> void: save_dialog.popup_centered_ratio(0.75))
	_button(toolbar, "导出 Markdown", _choose_export)
	toolbar.add_child(VSeparator.new())
	_button(toolbar, "插入 beat 宏到光标", _insert_beat_macro_at_caret)
	file_label = Label.new()
	file_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	toolbar.add_child(file_label)

func _build_source_view(parent: Node) -> void:
	source = CodeEdit.new()
	source.custom_minimum_size.x = 450
	source.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	source.gutters_draw_line_numbers = true
	source.text_changed.connect(_source_changed)
	parent.add_child(source)

func _build_designer_view(parent: Node) -> void:
	var right: VBoxContainer = VBoxContainer.new()
	right.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	parent.add_child(right)

	var header: HBoxContainer = HBoxContainer.new()
	right.add_child(header)
	_button(header, "新建编排节点", _new_beat)
	_button(header, "更新节点 → 源文", _apply_current_beat)
	_button(header, "复制选中节点", _duplicate_beat)

	nodes = ItemList.new()
	nodes.custom_minimum_size.y = 110
	nodes.item_selected.connect(_select_beat)
	right.add_child(nodes)

	tabs = TabContainer.new()
	tabs.custom_minimum_size.y = 190
	tabs.size_flags_vertical = Control.SIZE_EXPAND_FILL
	right.add_child(tabs)

	_build_visual_inspector_tab(tabs)
	_build_raw_json_tab(tabs)

	var stage_tools: HBoxContainer = HBoxContainer.new()
	right.add_child(stage_tools)

	_label(stage_tools, "当前绘制角色: ")
	var actors_sel := OptionButton.new()
	for act in ACTOR_NAMES:
		actors_sel.add_item(act)
	actors_sel.item_selected.connect(func(idx: int) -> void:
		var act_name := actors_sel.get_item_text(idx)
		canvas.actor = act_name
		ui_track_actor.select(idx)
		_refresh_form_track_ui())
	stage_tools.add_child(actors_sel)

	_button(stage_tools, "撤销上一点", func() -> void: _remove_point(canvas.actor))
	_button(stage_tools, "清空角色路径", _clear_path)
	_button(stage_tools, "场景参考", func() -> void: reference_dialog.popup_centered_ratio(0.75))

	var zoom_ctrl := SpinBox.new()
	zoom_ctrl.min_value = 0.25
	zoom_ctrl.max_value = 3.0
	zoom_ctrl.step = 0.25
	zoom_ctrl.value = 0.75
	zoom_ctrl.prefix = "缩放 "
	zoom_ctrl.value_changed.connect(func(v: float) -> void:
		canvas.zoom = v
		canvas.queue_redraw())
	stage_tools.add_child(zoom_ctrl)

	canvas = StageCanvas.new()
	canvas.size_flags_vertical = Control.SIZE_EXPAND_FILL
	canvas.waypoint_added.connect(_add_point)
	canvas.waypoint_removed.connect(_remove_point)
	right.add_child(canvas)

	var note := Label.new()
	note.text = "32px网格 · 左键添加途经点 · 右键撤销点 · 中键拖动画布 · 滚轮缩放。预览不运行机制。"
	note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	right.add_child(note)

func _build_visual_inspector_tab(parent: TabContainer) -> void:
	var container := VBoxContainer.new()
	container.name = "可视化设计器"
	parent.add_child(container)

	var row1 := HBoxContainer.new()
	container.add_child(row1)
	_label(row1, "节点ID: ")
	ui_id = LineEdit.new()
	ui_id.custom_minimum_size.x = 130
	ui_id.text_changed.connect(func(_t: String) -> void: _sync_form_to_beat())
	row1.add_child(ui_id)

	ui_wait = CheckBox.new()
	ui_wait.text = "阻塞等待到位 (Wait)"
	ui_wait.toggled.connect(func(_b: bool) -> void: _sync_form_to_beat())
	row1.add_child(ui_wait)

	container.add_child(HSeparator.new())

	# Mechanism row
	var row_mech := HBoxContainer.new()
	container.add_child(row_mech)
	ui_mech_enabled = CheckBox.new()
	ui_mech_enabled.text = "提交机制 (Mechanism)"
	ui_mech_enabled.toggled.connect(func(_b: bool) -> void: _sync_form_to_beat())
	row_mech.add_child(ui_mech_enabled)

	_label(row_mech, "类型: ")
	ui_mech_kind = OptionButton.new()
	for k in MECHANISM_KINDS:
		ui_mech_kind.add_item(k)
	ui_mech_kind.item_selected.connect(func(_idx: int) -> void: _sync_form_to_beat())
	row_mech.add_child(ui_mech_kind)

	_label(row_mech, "动作: ")
	ui_mech_op = OptionButton.new()
	for op in SAFE_OPERATIONS:
		ui_mech_op.add_item(op)
	ui_mech_op.item_selected.connect(func(_idx: int) -> void: _sync_form_to_beat())
	row_mech.add_child(ui_mech_op)

	_label(row_mech, "行动者: ")
	ui_mech_actor = OptionButton.new()
	for a in ACTOR_NAMES:
		ui_mech_actor.add_item(a)
	ui_mech_actor.item_selected.connect(func(_idx: int) -> void: _sync_form_to_beat())
	row_mech.add_child(ui_mech_actor)

	_label(row_mech, "目标/参数: ")
	ui_mech_id = LineEdit.new()
	ui_mech_id.custom_minimum_size.x = 80
	ui_mech_id.text_changed.connect(func(_t: String) -> void: _sync_form_to_beat())
	row_mech.add_child(ui_mech_id)

	container.add_child(HSeparator.new())

	# Actor track row
	var row_act := HBoxContainer.new()
	container.add_child(row_act)
	_label(row_act, "配置角色: ")
	ui_track_actor = OptionButton.new()
	for a in ACTOR_NAMES:
		ui_track_actor.add_item(a)
	ui_track_actor.item_selected.connect(func(_idx: int) -> void:
		canvas.actor = ui_track_actor.get_item_text(ui_track_actor.selected)
		_refresh_form_track_ui())
	row_act.add_child(ui_track_actor)

	_label(row_act, "朝向: ")
	ui_track_face = OptionButton.new()
	for f in FACINGS:
		ui_track_face.add_item(f)
	ui_track_face.item_selected.connect(func(_idx: int) -> void: _sync_form_to_beat())
	row_act.add_child(ui_track_face)

	_label(row_act, "姿态: ")
	ui_track_pose = OptionButton.new()
	for p in POSES:
		ui_track_pose.add_item(p)
	ui_track_pose.item_selected.connect(func(_idx: int) -> void: _sync_form_to_beat())
	row_act.add_child(ui_track_pose)

	_label(row_act, "速度: ")
	ui_track_speed = SpinBox.new()
	ui_track_speed.min_value = 10
	ui_track_speed.max_value = 300
	ui_track_speed.step = 5
	ui_track_speed.value = 90
	ui_track_speed.value_changed.connect(func(_v: float) -> void: _sync_form_to_beat())
	row_act.add_child(ui_track_speed)

	ui_track_stand = CheckBox.new()
	ui_track_stand.text = "起立"
	ui_track_stand.toggled.connect(func(_b: bool) -> void: _sync_form_to_beat())
	row_act.add_child(ui_track_stand)

	_label(row_act, "座高: ")
	ui_track_seat = SpinBox.new()
	ui_track_seat.min_value = -1
	ui_track_seat.max_value = 32
	ui_track_seat.value = -1
	ui_track_seat.prefix = ""
	ui_track_seat.value_changed.connect(func(_v: float) -> void: _sync_form_to_beat())
	row_act.add_child(ui_track_seat)

	ui_points_label = Label.new()
	ui_points_label.text = "途经点: 0"
	row_act.add_child(ui_points_label)

func _build_raw_json_tab(parent: TabContainer) -> void:
	json_editor = CodeEdit.new()
	json_editor.name = "原始 JSON"
	json_editor.custom_minimum_size.y = 150
	json_editor.size_flags_vertical = Control.SIZE_EXPAND_FILL
	json_editor.gutters_draw_line_numbers = true
	json_editor.text_changed.connect(_on_json_editor_changed)
	parent.add_child(json_editor)

func _build_dialogs() -> void:
	open_dialog = _dialog(FileDialog.FILE_MODE_OPEN_FILE, "*.dialogue ; Dialogue 源文件")
	open_dialog.file_selected.connect(_request_open)
	save_dialog = _dialog(FileDialog.FILE_MODE_SAVE_FILE, "*.dialogue ; Dialogue 源文件")
	save_dialog.file_selected.connect(_save_to)
	reference_dialog = _dialog(FileDialog.FILE_MODE_OPEN_FILE, "*.tscn ; 只读场景参考")
	reference_dialog.file_selected.connect(func(p: String) -> void: status.text = canvas.load_reference(p))
	export_dialog = _dialog(FileDialog.FILE_MODE_SAVE_FILE, "*.md ; Markdown")
	export_dialog.file_selected.connect(_export)
	discard_dialog = ConfirmationDialog.new()
	discard_dialog.title = "切换源文件"
	discard_dialog.dialog_text = "当前源文或编排尚未保存。切换将丢弃这些修改。"
	discard_dialog.confirmed.connect(func() -> void: _open_file(pending_open))
	add_child(discard_dialog)

func _label(parent: Control, text: String) -> Label:
	var label := Label.new()
	label.text = text
	parent.add_child(label)
	return label

func _button(parent: Control, label: String, action: Callable) -> Button:
	var btn := Button.new()
	btn.text = label
	btn.pressed.connect(action)
	parent.add_child(btn)
	return btn

func _dialog(dialog_mode: FileDialog.FileMode, filter: String) -> FileDialog:
	var d := FileDialog.new()
	d.access = FileDialog.ACCESS_FILESYSTEM
	d.file_mode = dialog_mode
	d.add_filter(filter)
	d.current_dir = ProjectSettings.globalize_path("res://scenes/FirstAwakening")
	add_child(d)
	return d

func _request_open(path: String) -> void:
	if source.text != saved_text:
		pending_open = path
		discard_dialog.popup_centered()
	else:
		_open_file(path)

func _open_file(path: String) -> void:
	if not path.ends_with(".dialogue"):
		status.text = "请选择 .dialogue 文件。"
		return
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		status.text = "读取失败：%s" % error_string(FileAccess.get_open_error())
		return
	file_path = path
	disk_text = file.get_as_text()
	saved_text = disk_text
	selected_line = 0
	json_editor.text = ""
	selected_source = ""
	current_beat = {}
	updating = true
	source.text = disk_text
	updating = false
	_refresh()

func _source_changed() -> void:
	if not updating:
		_refresh()

func _refresh() -> void:
	var result: Dictionary = StoryScore.parse(source.text)
	beats = result.get("beats", [])
	nodes.clear()
	for beat: Dictionary in beats:
		var line_num: int = int(beat.get("_line", 0))
		var title: String = str(beat.get("_title", ""))
		var triggers: Array = beat.get("_trigger_lines", [])
		var trig_desc := "调用%s" % str(triggers) if not triggers.is_empty() else "未被调用"
		nodes.add_item("%s · %s · L%d (%s)" % [str(beat.get("id", "?")), title, line_num, trig_desc])
	var errors: Array = result.get("errors", [])
	status.text = "\n".join(errors) if not errors.is_empty() else "%d 个编排节点；语法正确。" % beats.size()
	file_label.text = (file_path if not file_path.is_empty() else "未保存源文") + (" *" if source.text != saved_text else "")

func _clean(beat: Dictionary) -> Dictionary:
	var clean := beat.duplicate(true)
	for k: Variant in clean.keys():
		if str(k).begins_with("_"):
			clean.erase(k)
	return clean

func _select_beat(index: int, _discard_applied: bool = false) -> void:
	var beat: Dictionary = beats[index]
	selected_line = int(beat.get("_line", 0))
	current_beat = _clean(beat)
	json_baseline = JSON.stringify(current_beat, "\t")
	json_editor.text = json_baseline
	var lines := source.text.split("\n")
	selected_source = lines[selected_line - 1] if selected_line > 0 and selected_line <= lines.size() else ""
	source.set_caret_line(maxi(0, selected_line - 1))
	source.center_viewport_to_caret()
	_load_beat_to_form(current_beat)
	canvas.actor = ui_track_actor.get_item_text(ui_track_actor.selected)
	canvas.set_beat(current_beat)

func _new_beat() -> void:
	selected_line = 0
	selected_source = ""
	current_beat = {
		"id": "beat_%d" % (beats.size() + 1),
		"wait": true,
		"tracks": {
			"侦探": {"path": [], "speed": 90, "face": "front", "pose": "idle"}
		}
	}
	json_editor.text = JSON.stringify(current_beat, "\t")
	_load_beat_to_form(current_beat)
	canvas.actor = "侦探"
	canvas.set_beat(current_beat)
	status.text = "已新建节点。配置属性或路径后点击‘更新节点 → 源文’。"

func _duplicate_beat() -> void:
	if current_beat.is_empty(): return
	var copy: Dictionary = current_beat.duplicate(true)
	copy["id"] = str(copy.get("id", "beat")) + "_copy"
	selected_line = 0
	selected_source = ""
	current_beat = copy
	json_editor.text = JSON.stringify(current_beat, "\t")
	_load_beat_to_form(current_beat)
	canvas.set_beat(current_beat)
	status.text = "已复制节点。修改后点击‘更新节点 → 源文’插入到文件尾部。"

func _load_beat_to_form(beat: Dictionary) -> void:
	syncing_form = true
	ui_id.text = str(beat.get("id", ""))
	ui_wait.button_pressed = bool(beat.get("wait", true))

	if beat.has("mechanism") and beat.mechanism is Dictionary:
		ui_mech_enabled.button_pressed = true
		var mech: Dictionary = beat.mechanism
		var k := str(mech.get("kind", ""))
		var k_idx := MECHANISM_KINDS.find(k)
		ui_mech_kind.select(k_idx if k_idx >= 0 else 0)
		var op := str(mech.get("operation", ""))
		var op_idx := SAFE_OPERATIONS.find(op)
		ui_mech_op.select(op_idx if op_idx >= 0 else 0)
		var actor := str(mech.get("actor", ""))
		var a_idx := ACTOR_NAMES.find(actor)
		ui_mech_actor.select(a_idx if a_idx >= 0 else 0)
		ui_mech_id.text = str(mech.get("id", mech.get("action", "")))
	else:
		ui_mech_enabled.button_pressed = false
		ui_mech_kind.select(0)
		ui_mech_id.text = ""

	_refresh_form_track_ui()
	syncing_form = false

func _refresh_form_track_ui() -> void:
	var act_name := ui_track_actor.get_item_text(ui_track_actor.selected)
	var tracks: Dictionary = current_beat.get("tracks", {})
	var track: Dictionary = tracks.get(act_name, {})
	var f_idx := FACINGS.find(str(track.get("face", "front")))
	ui_track_face.select(f_idx if f_idx >= 0 else 0)
	var p_idx := POSES.find(str(track.get("pose", "idle")))
	ui_track_pose.select(p_idx if p_idx >= 0 else 0)
	ui_track_speed.value = float(track.get("speed", 90))
	ui_track_stand.button_pressed = bool(track.get("stand", false))
	ui_track_seat.value = float(track.get("seat", -1))
	var path: Array = track.get("path", [])
	ui_points_label.text = "途经点: %d" % path.size()

func _sync_form_to_beat() -> void:
	if syncing_form: return
	current_beat["id"] = ui_id.text.strip_edges()
	current_beat["wait"] = ui_wait.button_pressed

	if ui_mech_enabled.button_pressed and ui_mech_kind.selected > 0:
		var kind: String = MECHANISM_KINDS[ui_mech_kind.selected]
		var mech := {"kind": kind}
		match kind:
			"safe":
				mech["operation"] = SAFE_OPERATIONS[ui_mech_op.selected]
				mech["actor"] = ACTOR_NAMES[ui_mech_actor.selected]
			"inspect":
				mech["id"] = ui_mech_id.text.strip_edges()
			"perform":
				mech["actor"] = ACTOR_NAMES[ui_mech_actor.selected]
				mech["action"] = ui_mech_id.text.strip_edges()
			"cancel":
				pass
		current_beat["mechanism"] = mech
	else:
		current_beat.erase("mechanism")

	var act_name := ui_track_actor.get_item_text(ui_track_actor.selected)
	var tracks: Dictionary = current_beat.get("tracks", {})
	if not tracks.has(act_name):
		tracks[act_name] = {"path": []}
	var track: Dictionary = tracks[act_name]
	track["face"] = FACINGS[ui_track_face.selected]
	track["pose"] = POSES[ui_track_pose.selected]
	track["speed"] = int(ui_track_speed.value)
	if ui_track_stand.button_pressed:
		track["stand"] = true
	else:
		track.erase("stand")
	if ui_track_seat.value >= 0:
		track["seat"] = ui_track_seat.value
	else:
		track.erase("seat")
	current_beat["tracks"] = tracks

	json_editor.text = JSON.stringify(current_beat, "\t")
	canvas.set_beat(current_beat)

func _on_json_editor_changed() -> void:
	var parser := JSON.new()
	if parser.parse(json_editor.text) == OK and parser.data is Dictionary:
		current_beat = parser.data
		_load_beat_to_form(current_beat)
		canvas.set_beat(current_beat)

func _apply_current_beat() -> void:
	var beat := _clean(current_beat)
	if str(beat.get("id", "")).is_empty():
		status.text = "节点 ID 不能为空。"
		return
	if selected_line > 0:
		var lines := source.text.split("\n")
		if selected_line > lines.size() or lines[selected_line - 1] != selected_source:
			status.text = "源行已修改，请重新从列表中选择节点。"
			return
	updating = true
	source.text = StoryScore.with_beat(source.text, beat, selected_line)
	updating = false
	_refresh()
	for idx: int in range(beats.size()):
		if str(beats[idx].get("id", "")) == str(beat.id):
			nodes.select(idx)
			_select_beat(idx)
			break

func _add_point(actor_name: String, pt: Vector2) -> void:
	var tracks: Dictionary = current_beat.get("tracks", {})
	if not tracks.has(actor_name):
		tracks[actor_name] = {"path": [], "face": "front", "pose": "idle", "speed": 90}
	var path: Array = tracks[actor_name].get("path", [])
	path.append([pt.x, pt.y])
	tracks[actor_name]["path"] = path
	current_beat["tracks"] = tracks
	_load_beat_to_form(current_beat)
	json_editor.text = JSON.stringify(current_beat, "\t")
	canvas.set_beat(current_beat)

func _remove_point(actor_name: String) -> void:
	var tracks: Dictionary = current_beat.get("tracks", {})
	if not tracks.has(actor_name): return
	var path: Array = tracks[actor_name].get("path", [])
	if path.is_empty(): return
	path.pop_back()
	tracks[actor_name]["path"] = path
	current_beat["tracks"] = tracks
	_load_beat_to_form(current_beat)
	json_editor.text = JSON.stringify(current_beat, "\t")
	canvas.set_beat(current_beat)

func _clear_path() -> void:
	var act_name := canvas.actor
	if act_name.is_empty(): return
	var tracks: Dictionary = current_beat.get("tracks", {})
	if tracks.has(act_name):
		tracks[act_name]["path"] = []
		current_beat["tracks"] = tracks
		_load_beat_to_form(current_beat)
		json_editor.text = JSON.stringify(current_beat, "\t")
		canvas.set_beat(current_beat)

func _insert_beat_macro_at_caret() -> void:
	var beat_id := str(current_beat.get("id", ""))
	if beat_id.is_empty():
		status.text = "请先选择或新建一个编排节点。"
		return
	var macro := 'do story_beat("%s")\n' % beat_id
	source.insert_text_at_caret(macro)
	_refresh()

func _save() -> void:
	if file_path.is_empty():
		save_dialog.popup_centered_ratio(0.75)
	else:
		_save_to(file_path)

func _save_to(path: String) -> void:
	if not path.ends_with(".dialogue"):
		status.text = "保存目标必须为 .dialogue。"
		return
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		status.text = "保存失败：%s" % error_string(FileAccess.get_open_error())
		return
	file.store_string(source.text)
	file.flush()
	file_path = path
	disk_text = source.text
	saved_text = source.text
	_refresh()
	status.text = "已保存至: " + path
	EditorInterface.get_resource_filesystem().scan()

func _choose_export() -> void:
	var directory: String = ProjectSettings.globalize_path("res://../artifacts/story-modularization").simplify_path()
	DirAccess.make_dir_recursive_absolute(directory)
	export_dialog.current_dir = directory
	export_dialog.current_file = "story-score.md"
	export_dialog.popup_centered_ratio(0.75)

func _export(path: String) -> void:
	var target := path.simplify_path()
	var file := FileAccess.open(target, FileAccess.WRITE)
	if file == null:
		status.text = "导出失败：%s" % error_string(FileAccess.get_open_error())
		return
	file.store_string(StoryScore.markdown(source.text))
	file.flush()
	status.text = "已导出查验表至: " + target
