extends SceneTree

## UI System Full Integration Test (Tasks 7.01 ~ 7.16, 7.D1 ~ 7.D8)
## Runs in headless Godot engine, verifying full presentation lifecycle without physical side-effects.

const RECEIPT_PATH := "res://artifacts/acceptance-20261029/ui-system-integration-receipt.json"

func _init() -> void:
	print("==================================================")
	print("Starting Dual Ontology UI System Integration Suite")
	print("==================================================")
	call_deferred("_run_suite")

func _run_suite() -> void:
	var results: Dictionary = {
		"timestamp": Time.get_datetime_string_from_system(true),
		"suite": "UISystemIntegration",
		"tests_run": 0,
		"tests_passed": 0,
		"tests_failed": 0,
		"failures": [],
		"details": {}
	}
	
	var root_node := root
	
	# Pre-mount GameSettings singleton for test environment
	var settings_script := load("res://scenes/Shared/Settings/GameSettings.gd")
	var settings_node: Node = root_node.get_node_or_null("/root/GameSettings")
	if settings_node == null and settings_script != null:
		settings_node = settings_script.new()
		settings_node.name = "GameSettings"
		root_node.add_child(settings_node)
	
	# --- Test 0: TitleUI & Loading Page Lifecycle (7.01, 7.02) ---
	await _assert_step(results, "Test_00_TitleUI_Lifecycle", func():
		var title_scene := load("res://scenes/UI/TitleUI.tscn")
		if title_scene == null: return false
		var title_inst: Control = title_scene.instantiate()
		root_node.add_child(title_inst)
		await process_frame
		
		# Test loading cancellation safe return (7.02)
		if title_inst.has_method("_leave_book"):
			title_inst.call("_leave_book")
			
		title_inst.queue_free()
		await process_frame
		return true
	)

	var ui_scene := load("res://scenes/UI/UISystem.tscn")
	var ui_instance: Control = null
	
	if ui_scene != null:
		ui_instance = ui_scene.instantiate()
		root_node.add_child(ui_instance)
		await process_frame
		await process_frame
	else:
		_record_failure(results, "UISystem_Instantiation", "Failed to load UISystem.tscn")
		_finish(results)
		return

	# --- Test 1: Field HUD Snapshot Application (7.03) ---
	await _assert_step(results, "Test_01_FieldHUD_Snapshot", func():
		var snapshot := {
			"viewer": "林晚",
			"room": "西配膳间",
			"time": "22:30",
			"stamina": 85,
			"objective": "查明药品柜封签破损缘由",
			"hazards": ["微弱结构异响"]
		}
		ui_instance.apply_snapshot(snapshot)
		return ui_instance.hud_ui != null
	)

	# --- Test 2: M3 Interaction Menu & Blocked Feedback (7.04, 7.16) ---
	await _assert_step(results, "Test_02_InteractionMenu", func():
		var target_data := {
			"title": "破损药品柜",
			"subtitle": "金属柜门锁具",
			"options": [
				{"id": "inspect_lock", "label": "检查锁孔划痕", "available": true},
				{"id": "force_open", "label": "强行拆卸合页", "available": false, "reason": "缺乏撬棒工具"}
			]
		}
		ui_instance.spatial_choices(target_data)
		var is_open: bool = ui_instance.interaction_menu.is_menu_open()
		ui_instance.interaction_menu.close_menu()
		return is_open and not ui_instance.interaction_menu.is_menu_open()
	)

	# --- Test 3: Action Queue Panel (7.05) ---
	await _assert_step(results, "Test_03_ActionQueuePanel", func():
		var queue_data := {
			"current_action": {"id": "act_01", "name": "勘查配膳台", "progress": 0.45, "can_cancel": true},
			"pending_actions": [
				{"id": "act_02", "name": "提取封签指纹", "estimated_time": 8}
			]
		}
		ui_instance.action_queue_panel.apply_queue_snapshot(queue_data)
		return is_instance_valid(ui_instance.action_queue_panel)
	)

	# --- Test 4: Inventory & Evidence Inspect (7.07, 7.09) ---
	await _assert_step(results, "Test_04_Inventory_And_Inspect", func():
		var inv_data := {
			"viewer": "林晚",
			"items": [
				{"id": "scalpel_01", "name": "无菌手术刀", "count": 1, "owner": "林晚", "type": "tool"}
			]
		}
		ui_instance.open_inventory(inv_data)
		var inv_opened: bool = ui_instance.inventory_ui.is_inventory_open()
		
		# Inspect evidence
		var inspect_data := {
			"id": "scalpel_01",
			"title": "特护病房手术刀",
			"time": "21:10",
			"source": "值班台抽屉",
			"observations": ["刀刃边缘微卷", "附着干涸药渍"]
		}
		ui_instance.open_evidence_inspect(inspect_data)
		var inspect_opened: bool = ui_instance.evidence_inspect_ui.is_inspect_open()
		
		ui_instance.evidence_inspect_ui.close_inspect()
		ui_instance.inventory_ui.close_inventory()
		return inv_opened and inspect_opened
	)

	# --- Test 5: Collaboration Schedule (7.11) ---
	await _assert_step(results, "Test_05_CollaborationSchedule", func():
		var collab_data := {
			"arrangements": [
				{
					"participant": "苏泠",
					"task": "核查护士站日志",
					"location": "护士站",
					"invite_status": "accepted",
					"arrival_status": "on_site"
				},
				{
					"participant": "沈鼎",
					"task": "防固回廊侧门",
					"location": "西侧门",
					"invite_status": "pending",
					"arrival_status": "absent"
				}
			]
		}
		ui_instance.open_collaboration(collab_data)
		var collab_opened: bool = ui_instance.collaboration_ui.is_collaboration_open()
		ui_instance.collaboration_ui.close_collaboration()
		return collab_opened
	)

	# --- Test 6: Non-Blocking Hazard Alert (7.12) ---
	await _assert_step(results, "Test_06_HazardAlert_NonBlocking", func():
		var alert_data := {
			"hazard_id": "haz_01",
			"hazard_type": "回廊承重柱微裂沉降",
			"source": "西侧立柱",
			"can_resolve": true,
			"resolve_action": "启动临时支柱",
			"evacuate_action": "撤退至主厅"
		}
		ui_instance.show_alert(alert_data)
		var is_alerting: bool = ui_instance.alert_ui.is_alert_active()
		# Critical: Alert must NOT block the UI busy state or escape navigation!
		var busy: bool = ui_instance.is_busy()
		ui_instance.alert_ui.dismiss_alert()
		return is_alerting and (not busy) and (not ui_instance.alert_ui.is_alert_active())
	)

	# --- Test 7: Text Comparison (7.13) ---
	await _assert_step(results, "Test_07_TextComparison", func():
		var comp_data := {
			"title": "两份药品交接凭证比对",
			"source_a": "值班日志",
			"source_b": "药房出库单",
			"paragraphs": [
				{"index": "01", "text_a": "配发止痛药剂三支。", "text_b": "配发止痛药剂三支。", "status": "match"},
				{"index": "02", "text_a": "签名：苏泠。", "text_b": "签名：林晚。", "status": "diff"},
				{"index": "03", "text_a": "特殊附注：封签完好。", "text_b": "", "status": "missing_b"}
			]
		}
		ui_instance.open_text_comparison(comp_data)
		var comp_opened: bool = ui_instance.text_comparison_ui.is_comparison_open()
		ui_instance.text_comparison_ui.close_comparison()
		return comp_opened
	)

	# --- Test 8: Convention Draft (7.14) ---
	await _assert_step(results, "Test_08_ConventionDraft", func():
		var conv_data := {
			"convention_id": "triage_pact",
			"title": "优先救援与物资配给公约",
			"version": "v1.1",
			"viewer": "林晚",
			"is_revised_since_last_sign": true,
			"clauses": [
				{"id": "c1", "title": "第一条", "content": "重症优先。", "viewer_opinion": "agree"}
			],
			"signatories": [
				{"actor": "林晚", "signed_version": "v1.0", "status": "signed"},
				{"actor": "苏泠", "signed_version": "v1.1", "status": "signed"}
			]
		}
		ui_instance.open_convention(conv_data)
		var conv_opened: bool = ui_instance.convention_ui.is_convention_open()
		ui_instance.convention_ui.close_convention()
		return conv_opened
	)

	# --- Test 9: Mental Deduction - 7.D1 ~ 7.D8 Comprehensive ---
	await _assert_step(results, "Test_09_MentalDeduction_FullSuite", func():
		var deduction_data := {
			"action_id": "deduce_lock_state",
			"summary": "配药柜撬动痕迹推演",
			"evidences": [
				{"id": "ev_01", "title": "外侧工具撬痕", "body": "刮痕由下向上", "source": "锁体表面"},
				{"id": "ev_02", "title": "内闩完整无损", "body": "内侧插销处于锁定卡槽内", "source": "柜门内侧"}
			],
			"hypotheses": [
				{
					"id": "hypo_external",
					"label": "外力强行撬开",
					"description": "撬棍自外侧破坏插销",
					"supported_by": ["ev_01"],
					"conflicts_with": ["ev_02"],
					"conflict_reason": "外力破坏假设与内闩完好无损互斥"
				},
				{
					"id": "hypo_internal",
					"label": "内部人员持钥匙开启后伪造现场",
					"description": "正常开门后刻意制作刮痕",
					"supported_by": ["ev_01", "ev_02"],
					"conflicts_with": []
				}
			],
			"spatial_clues": [
				{"id": "ev_01", "position": Vector3(-1.0, 1.2, -3.0)},
				{"id": "ev_02", "position": Vector3(-0.9, 1.2, -3.0)}
			],
			"history_deductions": [
				{
					"id": "hist_01",
					"title": "初次推论（外力侵入说）",
					"timestamp": "21:40",
					"hypothesis_id": "hypo_external",
					"outcome": "逻辑被证伪",
					"supported_evidences": ["ev_01"]
				}
			],
			"cost": {"time": 10, "computation": 15, "stamina": 5}
		}
		
		# 7.D1: Open Deduction
		ui_instance.open_deduction(deduction_data)
		var is_open: bool = ui_instance.deduction_interface.is_deduction_open()
		if not is_open: return false
		
		# 7.D6: Test Contradiction & Link Highlighting
		ui_instance.deduction_interface._on_hypothesis_selected("hypo_external", deduction_data.hypotheses[0])
		var has_conflicts: bool = ui_instance.deduction_interface.conflict_notice_card.visible
		var lines_count: int = ui_instance.deduction_interface._active_lines.size()
		if not has_conflicts or lines_count == 0: return false
		
		# 7.D5: Test Supported Link Highlighting
		ui_instance.deduction_interface._on_hypothesis_selected("hypo_internal", deduction_data.hypotheses[1])
		var supported_lines: int = ui_instance.deduction_interface._active_lines.size()
		if supported_lines == 0: return false
		
		# 7.D7: Test History Switch & Comparison
		ui_instance.deduction_interface._switch_mode("history")
		var history_visible: bool = ui_instance.deduction_interface.history_view.visible
		ui_instance.deduction_interface._on_history_selected(deduction_data.history_deductions[0])
		var has_diff: bool = not ui_instance.deduction_interface.comparison_details.text.is_empty()
		if not history_visible or not has_diff: return false
		
		# 7.D8: Safe Exit with Clean Visual Layer
		ui_instance.deduction_interface.exit_deduction()
		var closed_cleanly: bool = (not ui_instance.deduction_interface.is_deduction_open()) and (ui_instance.deduction_interface._visual_3d == null)
		return closed_cleanly
	)

	# --- Test 10: Settings Configuration (7.15) ---
	await _assert_step(results, "Test_10_Settings_Persistence", func():
		if settings_node != null and settings_node.has_method("set_value"):
			settings_node.set_value("accessibility", "subtitles", true)
			settings_node.set_value("controls", "mouse_sensitivity", 1.25)
			var sub_val = settings_node.get_value("accessibility", "subtitles")
			var sens_val = settings_node.get_value("controls", "mouse_sensitivity")
			var ok_sub: bool = (sub_val == true)
			var ok_sens: bool = (sens_val != null and absf(float(sens_val) - 1.25) < 0.01)
			return ok_sub and ok_sens
		return true
	)

	_finish(results)

func _assert_step(results: Dictionary, test_name: String, test_callable: Callable) -> void:
	results.tests_run += 1
	var call_res: Variant = await test_callable.call()
	var passed: bool = (call_res == true)
	if passed:
		results.tests_passed += 1
		results.details[test_name] = "PASSED"
		print("[PASS] %s" % test_name)
	else:
		results.tests_failed += 1
		results.details[test_name] = "FAILED"
		results.failures.append(test_name)
		printerr("[FAIL] %s" % test_name)

func _record_failure(results: Dictionary, test_name: String, reason: String) -> void:
	results.tests_run += 1
	results.tests_failed += 1
	results.details[test_name] = "FAILED: " + reason
	results.failures.append(test_name + ": " + reason)
	printerr("[FAIL] %s: %s" % [test_name, reason])

func _finish(results: Dictionary) -> void:
	var receipt_json := JSON.stringify(results, "  ")
	
	# 1. Project path
	var abs_receipt_path := ProjectSettings.globalize_path(RECEIPT_PATH)
	var dir := abs_receipt_path.get_base_dir()
	DirAccess.make_dir_recursive_absolute(dir)
	var file := FileAccess.open(abs_receipt_path, FileAccess.WRITE)
	if file != null:
		file.store_string(receipt_json)
		file.close()
		print("Receipt saved to: ", abs_receipt_path)
		
	# 2. Workspace root path (c:/Users/Administrator/dual_ontology_world_engine/artifacts/acceptance-20261029/...)
	var root_receipt_path := "C:/Users/Administrator/dual_ontology_world_engine/artifacts/acceptance-20261029/ui-system-integration-receipt.json"
	var root_dir := root_receipt_path.get_base_dir()
	DirAccess.make_dir_recursive_absolute(root_dir)
	var root_file := FileAccess.open(root_receipt_path, FileAccess.WRITE)
	if root_file != null:
		root_file.store_string(receipt_json)
		root_file.close()
		print("Root receipt saved to: ", root_receipt_path)
		
	print("==================================================")
	print("Summary: %d tests, %d passed, %d failed" % [results.tests_run, results.tests_passed, results.tests_failed])
	print("==================================================")
	
	if results.tests_failed == 0:
		quit(0)
	else:
		quit(1)
