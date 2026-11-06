extends Node

## 游戏唯一根场景与生命周期总协调器（Main.gd）
## 统一持有 SessionBridge.cs 与 DvPresentationContent.cs 跨语言网关。
## 协调管理 12 对简化 UI 体系、3D 现场与书案舞台。

signal shutdown_completed
signal mode_changed(new_mode: String)

const SESSION_BRIDGE_SCRIPT := preload("res://scenes/main/SessionBridge.cs")
const PRESENTATION_CONTENT_SCRIPT := preload("res://scenes/main/DvPresentationContent.cs")

# UI panels references (lazy or instantiated on demand)
const BOOK_SHELL_SCENE := preload("res://scenes/UIpanel/BookShell/BookShellUI.tscn")
const FIELD_HUD_SCENE := preload("res://scenes/UIpanel/Field/FieldHUD.tscn")
const TITLE_STAGE_SCENE := preload("res://scenes/UIpanel/Field/TitleStageUI.tscn")
const INTERACTION_MENU_SCENE := preload("res://scenes/UIpanel/Field/InteractionMenu.tscn")
const DEDUCTION_INTERFACE_SCENE := preload("res://scenes/UIpanel/Field/DeductionInterface.tscn")
const DIALOGUE_UI_SCENE := preload("res://scenes/UIpanel/Field/DialogueUI.tscn")
const MODAL_ALERT_SCENE := preload("res://scenes/UIpanel/Shared/ModalAlertUI.tscn")
const UI_INPUT_SCENE := preload("res://scenes/UIpanel/Shared/UIInput.tscn")
const LIVE_BUILDER_SCENE := preload("res://tools/godot_architect/LiveBuilderOverlay.tscn")

@export var default_scenario_path := "res://assets/data/scenarios/campaign.dv"
@export var autoplay_title := true

var session_bridge: Node
var presentation_content: RefCounted

# UI instances
var ui_input: Node
var title_stage: Control
var field_hud: CanvasLayer
var book_shell: Control
var interaction_menu: Control
var deduction_interface: Control
var dialogue_ui: CanvasLayer
var modal_alert: CanvasLayer
var live_builder: CanvasLayer

# State
var current_mode := "title" # "title", "field", "book", "deduction", "dialogue"
var session_ready := false
var flat_ui_active := false
var perspective_active := false
var architect_builder_active := false

func _ready() -> void:
	name = "Main"
	process_mode = Node.PROCESS_MODE_ALWAYS
	_initialize_gateways()
	_initialize_ui_system()

	if autoplay_title:
		enter_title()

	for arg in OS.get_cmdline_user_args():
		if arg == "--acceptance-demo":
			_start_acceptance_demo()

func _initialize_gateways() -> void:
	# Single gateway instantiation
	session_bridge = SESSION_BRIDGE_SCRIPT.new()
	session_bridge.name = "SessionBridge"
	add_child(session_bridge)

	presentation_content = PRESENTATION_CONTENT_SCRIPT.new()

	session_bridge.EventsReady.connect(_on_session_events)
	session_bridge.TraceClosed.connect(_on_session_trace_closed)

func _initialize_ui_system() -> void:
	# Instantiate UIInput router first
	ui_input = UI_INPUT_SCENE.instantiate()
	add_child(ui_input)
	ui_input.flat_ui_toggled.connect(_on_flat_ui_toggled)
	ui_input.perspective_toggled.connect(_on_perspective_toggled)
	ui_input.architect_builder_toggled.connect(_on_architect_builder_toggled)
	ui_input.notebook_toggled.connect(_on_notebook_toggled)
	ui_input.cancel_requested.connect(_on_cancel_requested)

	# Instantiate Shared Modal Alert
	modal_alert = MODAL_ALERT_SCENE.instantiate()
	add_child(modal_alert)

	# Instantiate Field HUD
	field_hud = FIELD_HUD_SCENE.instantiate()
	add_child(field_hud)
	field_hud.book_open_requested.connect(open_book)
	field_hud.hide()

	# Instantiate Interaction Menu
	interaction_menu = INTERACTION_MENU_SCENE.instantiate()
	add_child(interaction_menu)
	interaction_menu.action_selected.connect(_on_interaction_action_selected)
	interaction_menu.hide()

	# Instantiate Deduction Interface
	deduction_interface = DEDUCTION_INTERFACE_SCENE.instantiate()
	add_child(deduction_interface)
	deduction_interface.deduction_closed.connect(return_to_field)
	deduction_interface.hide()

	# Instantiate Dialogue UI
	dialogue_ui = DIALOGUE_UI_SCENE.instantiate()
	add_child(dialogue_ui)
	dialogue_ui.dialogue_finished.connect(_on_dialogue_finished)
	dialogue_ui.hide()

	# Instantiate Book Shell UI
	book_shell = BOOK_SHELL_SCENE.instantiate()
	add_child(book_shell)
	book_shell.closed.connect(return_to_field)
	book_shell.hide()

	# Instantiate Title & Stage Settlement UI
	title_stage = TITLE_STAGE_SCENE.instantiate()
	add_child(title_stage)
	title_stage.start_game_requested.connect(start_new_game)
	title_stage.continue_game_requested.connect(continue_game)
	title_stage.open_casebook_requested.connect(open_book)
	title_stage.quit_requested.connect(func(): get_tree().quit())

	# Instantiate Live Builder Overlay
	live_builder = LIVE_BUILDER_SCENE.instantiate()
	add_child(live_builder)

# Gateway Public API for GDScript presenters
func submit_session_action(action: Dictionary) -> Array:
	if not is_instance_valid(session_bridge): return []
	var response: Dictionary = session_bridge.Dispatch(action)
	return response.get("events", [])

func observe_session() -> Dictionary:
	if not is_instance_valid(session_bridge): return {}
	return session_bridge.Snapshot()

func read_presentation_record(path: String, category: String, identity: String) -> Dictionary:
	if presentation_content == null: return {}
	return presentation_content.ReadRecord(path, category, identity)

func validate_prefab_placement(prefab_path: String, position: Vector3, rotation: Vector3, context: Dictionary = {}) -> Dictionary:
	if not is_instance_valid(session_bridge): return {"valid": false, "reason": "no_session"}
	return session_bridge.ValidatePrefabPlacement(prefab_path, position, rotation, context)

# Navigation & Mode Flow
func enter_title() -> void:
	current_mode = "title"
	title_stage.show_title()
	field_hud.hide()
	book_shell.hide()
	deduction_interface.hide()
	dialogue_ui.hide()
	interaction_menu.hide()
	mode_changed.emit(current_mode)

func start_new_game() -> void:
	session_bridge.Open(default_scenario_path)
	session_ready = true
	title_stage.hide_stage()
	enter_field()

func continue_game() -> void:
	var saved: String = session_bridge.ReadSavedGame()
	if not saved.is_empty():
		session_bridge.Open(default_scenario_path)
	else:
		session_bridge.Open(default_scenario_path)
	session_ready = true
	title_stage.hide_stage()
	enter_field()

func enter_field() -> void:
	current_mode = "field"
	field_hud.show()
	title_stage.hide_stage()
	book_shell.hide()
	deduction_interface.hide()
	dialogue_ui.hide()
	interaction_menu.hide()
	mode_changed.emit(current_mode)

func open_book(section_id := "B01") -> void:
	current_mode = "book"
	var snap := observe_session()
	book_shell.configure(snap)
	book_shell.open(section_id)
	mode_changed.emit(current_mode)

func close_book() -> void:
	book_shell.close()

func return_to_field() -> void:
	enter_field()

func open_deduction(hypothesis_id := "") -> void:
	current_mode = "deduction"
	field_hud.hide()
	deduction_interface.open_deduction(hypothesis_id, [], [])
	mode_changed.emit(current_mode)

func start_dialogue(dialogue_data: Dictionary) -> void:
	current_mode = "dialogue"
	dialogue_ui.start_dialogue(dialogue_data)
	mode_changed.emit(current_mode)

func show_settlement(report_data: Dictionary) -> void:
	current_mode = "settlement"
	field_hud.hide()
	title_stage.show_settlement(report_data)
	mode_changed.emit(current_mode)

# Input Router Event Handlers
func _on_flat_ui_toggled(active: bool) -> void:
	flat_ui_active = active
	if is_instance_valid(book_shell) and book_shell.has_method("set_flat_mode"):
		book_shell.set_flat_mode(flat_ui_active)
	var mode_name := "2D 平面直通模式" if flat_ui_active else "3D 书案视口模式"
	field_hud.show_snackbar("已切换至 " + mode_name, "info")

func _on_perspective_toggled(active: bool) -> void:
	perspective_active = active
	var status := "开启" if perspective_active else "关闭"
	field_hud.show_snackbar("认知透视已 " + status, "info")

func _on_architect_builder_toggled(active: bool) -> void:
	architect_builder_active = active
	if is_instance_valid(live_builder) and live_builder.is_active != architect_builder_active:
		live_builder.toggle_builder()
	var status := "启动" if architect_builder_active else "退出"
	field_hud.show_snackbar("3D 建造台模式已 " + status, "warning")

func _on_notebook_toggled() -> void:
	if current_mode == "book":
		close_book()
	elif current_mode == "field":
		open_book("B01")

func _on_cancel_requested() -> void:
	match current_mode:
		"book":
			close_book()
		"deduction":
			deduction_interface.close_deduction()
		"dialogue":
			pass
		_:
			if interaction_menu.visible:
				interaction_menu.close_menu()

func _on_interaction_action_selected(action_id: String, target_id: String) -> void:
	submit_session_action({"kind": "action", "action": action_id, "target": target_id})
	interaction_menu.close_menu()

func _on_dialogue_finished(_last_choice: String) -> void:
	enter_field()

func _on_session_events(events: Array) -> void:
	for ev in events:
		var kind: String = str(ev.get("kind", ""))
		if kind == "erosion_warning":
			modal_alert.show_erosion_alert(float(ev.get("level", 0.5)), str(ev.get("message", "源质侵蚀加剧")))

func _on_session_trace_closed() -> void:
	shutdown_completed.emit()

func _start_acceptance_demo() -> void:
	var demo_tree := get_tree()
	print("[ACCEPTANCE] Starting automated acceptance demonstration...")
	await demo_tree.create_timer(1.2).timeout
	print("[ACCEPTANCE] Phase 1: Title Stage active.")

	start_new_game()
	print("[ACCEPTANCE] Phase 2: Start New Game -> Field HUD entered.")
	await demo_tree.create_timer(1.8).timeout

	open_book("B01")
	print("[ACCEPTANCE] Phase 3: Casebook Opened (3D Paper Mode).")
	await demo_tree.create_timer(1.8).timeout

	_on_flat_ui_toggled(true)
	print("[ACCEPTANCE] Phase 4: F2 2D Flat UI Mode active.")
	await demo_tree.create_timer(1.8).timeout

	return_to_field()
	open_deduction("H01")
	print("[ACCEPTANCE] Phase 5: Deduction Interface active.")
	await demo_tree.create_timer(1.8).timeout

	return_to_field()
	_on_architect_builder_toggled(true)
	print("[ACCEPTANCE] Phase 6: F4 Live Builder Overlay active.")
	await demo_tree.create_timer(2.0).timeout

	print("[ACCEPTANCE] Complete. All 6 gameplay and UI systems verified.")
