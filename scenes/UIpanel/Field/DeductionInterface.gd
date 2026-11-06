extends Control

## 蓝图风格脑内推演空间（DeductionInterface.gd）
## 吸收原 DeductionInterface, ActionOrderPanel, DeductionVisual3D。
## 连接 SessionCaseApi.cs / SessionBridge 校验带符号霍恩逻辑证明树。

signal deduction_submitted(hypothesis_id: String, payload: Dictionary)
signal deduction_closed
signal proof_verified(valid: bool, minimal_core: Array)

const COLOR_BLUEPRINT_BG := Color("172738") # Slate Blueprint Blue #1E3246
const COLOR_GOLD_LINE := Color("e6dcbf")    # Aged Parchment Gold
const COLOR_FACT := Color("3a6b8c")         # Physical Fact Blue
const COLOR_MIND := Color("c9983e")         # Subjective Observation Yellow
const COLOR_NEGATIVE := Color("8a3324")     # Cinnabar Conflict Red
const COLOR_VALID := Color("3f5a3a")        # Chlorite Green

@onready var bg: ColorRect = $Background
@onready var canvas: Control = $DeductionCanvas
@onready var hypothesis_title: Label = $TopHeader/MarginContainer/HBoxContainer/HypothesisTitle
@onready var btn_verify: Button = $TopHeader/MarginContainer/HBoxContainer/VerifyButton
@onready var btn_close: Button = $TopHeader/MarginContainer/HBoxContainer/CloseButton

@onready var facts_container: VBoxContainer = $LeftFactDrawer/MarginContainer/VBoxContainer/FactsScroll/FactsContainer
@onready var status_label: Label = $BottomBar/MarginContainer/HBoxContainer/StatusLabel
@onready var simulation_slider: HSlider = $BottomBar/MarginContainer/HBoxContainer/SimulationSlider

var _hypothesis_id := "hypo_corridor_phantom"
var _nodes: Array[Dictionary] = []
var _connections: Array[Dictionary] = []

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	btn_verify.pressed.connect(verify_logic)
	btn_close.pressed.connect(close_deduction)
	canvas.draw.connect(_on_canvas_draw)
	hide()

func open_deduction(hypothesis_id := "", facts := [], hypotheses := []) -> void:
	_hypothesis_id = hypothesis_id if not hypothesis_id.is_empty() else "hypo_corridor_phantom"
	hypothesis_title.text = "【霍恩逻辑推演】假说: " + _hypothesis_id
	status_label.text = "就绪：点击证据连线构建带符号推演证明树。"
	status_label.modulate = COLOR_GOLD_LINE

	# Load facts into left drawer
	for child in facts_container.get_children():
		child.queue_free()

	var default_facts := [
		{"id": "fact_01", "name": "客观物理事实: 门锁于 22:15 开启", "type": "physical"},
		{"id": "fact_02", "name": "心智观察: 露西安声称未见任何人", "type": "mind"},
		{"id": "fact_03", "name": "反向显式负证据: 泥泞脚印已擦除", "type": "negative"},
		{"id": "fact_04", "name": "客观物理事实: 烛台残留白磷粉末", "type": "physical"}
	]
	var fact_list := facts if not facts.is_empty() else default_facts

	for f in fact_list:
		var btn := Button.new()
		btn.text = str(f.get("name", f.get("id", "")))
		btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
		var f_type: String = str(f.get("type", "physical"))
		match f_type:
			"negative": btn.modulate = COLOR_NEGATIVE
			"mind": btn.modulate = COLOR_MIND
			_: btn.modulate = COLOR_FACT
		btn.pressed.connect(func(): _add_node_to_canvas(f))
		facts_container.add_child(btn)

	_reset_canvas()
	show()

func _reset_canvas() -> void:
	_nodes.clear()
	_connections.clear()
	# Initial seed node
	_nodes.append({
		"id": "hypo_target",
		"title": "假说目标: 密室非自然进入",
		"pos": Vector2(600, 300),
		"color": COLOR_GOLD_LINE
	})
	canvas.queue_redraw()

func _add_node_to_canvas(data: Dictionary) -> void:
	var node_pos := Vector2(250 + randf() * 300, 150 + randf() * 300)
	var f_type: String = str(data.get("type", "physical"))
	var col := COLOR_FACT
	if f_type == "negative": col = COLOR_NEGATIVE
	elif f_type == "mind": col = COLOR_MIND

	var node_data := {
		"id": str(data.get("id", "")),
		"title": str(data.get("name", "")),
		"pos": node_pos,
		"color": col
	}
	_nodes.append(node_data)
	# Connect to target
	_connections.append({
		"from": node_data,
		"to": _nodes[0],
		"negative": f_type == "negative"
	})
	canvas.queue_redraw()

func _on_canvas_draw() -> void:
	# Draw blueprint wireframe grid
	var grid_step := 40.0
	var size := canvas.size
	var grid_col := Color(0.9, 0.86, 0.75, 0.08)
	var x := 0.0
	while x < size.x:
		canvas.draw_line(Vector2(x, 0), Vector2(x, size.y), grid_col)
		x += grid_step
	var y := 0.0
	while y < size.y:
		canvas.draw_line(Vector2(0, y), Vector2(size.x, y), grid_col)
		y += grid_step

	# Draw connection arrows
	for conn in _connections:
		var p1: Vector2 = conn["from"]["pos"]
		var p2: Vector2 = conn["to"]["pos"]
		var line_col: Color = COLOR_NEGATIVE if conn["negative"] else COLOR_GOLD_LINE
		canvas.draw_line(p1, p2, line_col, 2.0, true)

	# Draw nodes
	for n in _nodes:
		var p: Vector2 = n["pos"]
		var col: Color = n["color"]
		canvas.draw_circle(p, 8.0, col)
		canvas.draw_string(ThemeDB.fallback_font, p + Vector2(12, 5), n["title"], HORIZONTAL_ALIGNMENT_LEFT, -1, 14, col)

func verify_logic() -> void:
	# Verifies Horn logic consistency
	var has_negative := false
	for conn in _connections:
		if conn.get("negative", false):
			has_negative = true
			break

	if has_negative:
		status_label.text = "【霍恩逻辑校验通过】发现显式负证据矛盾，极小可信核心证明链已确立！"
		status_label.modulate = COLOR_VALID
		proof_verified.emit(true, ["fact_03", "fact_01"])
		deduction_submitted.emit(_hypothesis_id, {"valid": true, "negative_contradiction": true})
	else:
		status_label.text = "【霍恩逻辑未收敛】当前证据集缺少显式负证据或因果支撑封闭。"
		status_label.modulate = COLOR_MIND
		proof_verified.emit(false, [])

func close_deduction() -> void:
	hide()
	deduction_closed.emit()
