extends RefCounted
## Public Rooms & Narrative Interaction Objects Library (Tasks 3.19 - 3.43).
## Implements all 25 public room, lecture, industrial, medical and key narrative objects.
## Fully compliant with WorldObject3D structure, stable IDs, physical collision, and interaction slots.
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")

# -------------------------------------------------------------
# 1. 餐厅与生活设施 (Tasks 3.19 - 3.25)
# -------------------------------------------------------------

## 3.19 长餐桌 (Dining Table)
static func create_dining_table(id: String, size: Vector3 = Vector3(2.4, 0.76, 1.0), seats_count: int = 6) -> Node3D:
	var root := _base_world_object(id, "dining_table")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var wood_mat := _mat(Color("523928"), 0.75)
	# Tabletop
	_add_box(vis, col, "Tabletop", Vector3(0, size.y - 0.025, 0), Vector3(size.x, 0.05, size.z), wood_mat)
	# Legs (4 corners)
	var lx := size.x * 0.5 - 0.06
	var lz := size.z * 0.5 - 0.06
	var leg_h := size.y - 0.05
	for sx: float in [-1.0, 1.0]:
		for sz: float in [-1.0, 1.0]:
			_add_box(vis, col, "Leg_%d_%d" % [int(sx), int(sz)], Vector3(sx * lx, leg_h * 0.5, sz * lz), Vector3(0.08, leg_h, 0.08), wood_mat)
	
	# Serving surface slot
	_add_slot(slots, "ServingSurface", Vector3(0, size.y, 0))
	
	# Individual seats on both sides
	var seats_per_side := seats_count / 2
	var step_x := size.x / float(seats_per_side + 1)
	var seat_idx := 1
	for side: float in [-1.0, 1.0]:
		for i in range(seats_per_side):
			var x_pos: float = -size.x * 0.5 + float(i + 1) * step_x
			var z_pos: float = side * (size.z * 0.5 + 0.45)
			_add_slot(slots, "Sit_%d" % seat_idx, Vector3(x_pos, 0.45, z_pos))
			seat_idx += 1
			
	root.set_meta("seats_count", seats_count)
	root.set_meta("occupied_seats", {})
	return root

## 3.20 餐椅与长凳 (Dining Chair / Long Bench)
static func create_dining_bench(id: String, length: float = 1.8, capacity: int = 3) -> Node3D:
	var root := _base_world_object(id, "dining_bench")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var wood_mat := _mat(Color("5a3d28"), 0.8)
	var seat_h := 0.45
	var depth := 0.42
	# Bench seat board
	_add_box(vis, col, "BenchSeat", Vector3(0, seat_h - 0.02, 0), Vector3(length, 0.04, depth), wood_mat)
	# Supports
	for sx: float in [-1.0, 1.0]:
		_add_box(vis, col, "Support_%d" % int(sx), Vector3(sx * (length * 0.5 - 0.12), (seat_h - 0.04) * 0.5, 0), Vector3(0.06, seat_h - 0.04, depth - 0.06), wood_mat)
	
	# Individual seats along the bench
	var step_x := length / float(capacity + 1)
	for i in range(capacity):
		var x_pos := -length * 0.5 + float(i + 1) * step_x
		_add_slot(slots, "Sit_%d" % (i + 1), Vector3(x_pos, seat_h, 0))
		_add_slot(slots, "Stand_Exit_%d" % (i + 1), Vector3(x_pos, 0, depth * 0.5 + 0.35))
		
	root.set_meta("capacity", capacity)
	root.set_meta("occupied_seats", {})
	return root

## 3.21 餐盘、食物守恒 (Food Tray & Plate)
static func create_food_tray(id: String, initial_portions: int = 4) -> Node3D:
	var root := _base_world_object(id, "food_tray")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var metal := _mat(Color("8a8b8c"), 0.3, 0.7)
	var food_mat := _mat(Color("c28847"), 0.9)
	_add_box(vis, col, "TrayBase", Vector3(0, 0.015, 0), Vector3(0.55, 0.03, 0.38), metal)
	
	# Portions
	for i in range(initial_portions):
		var px := -0.18 + float(i % 2) * 0.36
		var pz := -0.10 + float(i / 2) * 0.20
		_add_box(vis, null, "Portion_%d" % (i + 1), Vector3(px, 0.045, pz), Vector3(0.12, 0.03, 0.12), food_mat)
		
	_add_slot(slots, "PickupContact", Vector3(0, 0.05, 0))
	_add_slot(slots, "Approach", Vector3(0, 0, 0.45))
	
	root.set_meta("initial_portions", initial_portions)
	root.set_meta("served_portions", 0)
	root.set_meta("eaten_portions", 0)
	return root

## 3.22 配膳台 (Serving Counter)
static func create_serving_counter(id: String, size: Vector3 = Vector3(2.0, 0.9, 0.7)) -> Node3D:
	var root := _base_world_object(id, "serving_counter")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var counter_mat := _mat(Color("7a6754"), 0.6)
	var top_mat := _mat(Color("a29584"), 0.4)
	# Solid body preventing walking through
	_add_box(vis, col, "CounterBody", Vector3(0, (size.y - 0.05) * 0.5, 0), Vector3(size.x, size.y - 0.05, size.z), counter_mat)
	_add_box(vis, col, "CounterTop", Vector3(0, size.y - 0.025, 0), Vector3(size.x + 0.06, 0.05, size.z + 0.06), top_mat)
	
	# Slots on both sides (staff vs customer)
	_add_slot(slots, "StaffStand", Vector3(0, 0, -size.z * 0.5 - 0.45))
	_add_slot(slots, "CustomerApproach", Vector3(0, 0, size.z * 0.5 + 0.45))
	_add_slot(slots, "PickupContact", Vector3(0, size.y, 0))
	
	root.set_meta("stock_count", 12)
	return root

## 3.23 炉灶与壁炉热源 (Stove & Fireplace)
static func create_fireplace_stove(id: String, size: Vector3 = Vector3(1.2, 1.4, 0.6)) -> Node3D:
	var root := _base_world_object(id, "fireplace_stove")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var stone_mat := _mat(Color("635e58"), 0.9)
	var iron_mat := _mat(Color("2d2e2c"), 0.5, 0.8)
	_add_box(vis, col, "HearthBody", Vector3(0, size.y * 0.5, 0), size, stone_mat)
	_add_box(vis, col, "FireGate", Vector3(0, 0.4, -size.z * 0.5 - 0.02), Vector3(0.5, 0.5, 0.04), iron_mat)
	
	_add_slot(slots, "OperateStand", Vector3(0, 0, -size.z * 0.5 - 0.75))
	_add_slot(slots, "Contact", Vector3(0, 0.5, -size.z * 0.5))
	
	root.set_meta("is_ignited", false)
	root.set_meta("temperature_c", 18.0)
	root.set_meta("heat_radius_m", 4.0)
	return root

## 3.24 粮柜双闩柜 (Double-Latch Food Cabinet)
static func create_double_latch_cabinet(id: String, size: Vector3 = Vector3(1.0, 1.6, 0.55)) -> Node3D:
	var root := _base_world_object(id, "double_latch_cabinet")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var wood := _mat(Color("543d2c"), 0.8)
	var iron := _mat(Color("3b3b38"), 0.3, 0.8)
	_add_box(vis, col, "CabinetBody", Vector3(0, size.y * 0.5, 0), size, wood)
	# Upper and Lower Latches
	_add_box(vis, null, "UpperLatch", Vector3(0, size.y * 0.72, -size.z * 0.5 - 0.02), Vector3(0.18, 0.04, 0.03), iron)
	_add_box(vis, null, "LowerLatch", Vector3(0, size.y * 0.28, -size.z * 0.5 - 0.02), Vector3(0.18, 0.04, 0.03), iron)
	
	_add_slot(slots, "Stand", Vector3(0, 0, -size.z * 0.5 - 0.55))
	_add_slot(slots, "ContactUpper", Vector3(0, size.y * 0.72, -size.z * 0.5))
	_add_slot(slots, "ContactLower", Vector3(0, size.y * 0.28, -size.z * 0.5))
	
	root.set_meta("latch_upper", true)
	root.set_meta("latch_lower", true)
	root.set_meta("support_prop", false)
	root.set_meta("is_open", false)
	return root

## 3.25 钥匙柜各钥匙独立 ID (Key Cabinet)
static func create_key_cabinet(id: String, key_ids: Array = ["key_01", "key_02", "key_03", "key_04"]) -> Node3D:
	var root := _base_world_object(id, "key_cabinet")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var wood := _mat(Color("4a3525"), 0.8)
	var brass := _mat(Color("d4af37"), 0.3, 0.8)
	_add_box(vis, col, "BoxBody", Vector3(0, 0.5, 0), Vector3(0.6, 0.7, 0.15), wood)
	
	var hooks := {}
	for i in range(key_ids.size()):
		var hook_name := "hook_%d" % (i + 1)
		var k_id: String = str(key_ids[i])
		hooks[hook_name] = k_id
		var hx := -0.2 + float(i % 2) * 0.4
		var hy := 0.65 - float(i / 2) * 0.25
		_add_box(vis, null, "Hook_%s" % hook_name, Vector3(hx, hy, -0.09), Vector3(0.02, 0.04, 0.03), brass)
		
	_add_slot(slots, "Stand", Vector3(0, 0, 0.55))
	_add_slot(slots, "Contact", Vector3(0, 0.5, 0))
	
	root.set_meta("hooks", hooks)
	return root

# -------------------------------------------------------------
# 2. 讲堂与工业设施 (Tasks 3.26 - 3.33)
# -------------------------------------------------------------

## 3.26 登记簿 (Register Logbook)
static func create_register_logbook(id: String, title: String = "Access Registry") -> Node3D:
	var root := _base_world_object(id, "register_logbook")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var leather := _mat(Color("4a2820"), 0.85)
	var paper := _mat(Color("f4eedb"), 0.95)
	_add_box(vis, col, "Cover", Vector3(0, 0.02, 0), Vector3(0.35, 0.04, 0.28), leather)
	_add_box(vis, null, "Pages", Vector3(0.005, 0.022, 0), Vector3(0.33, 0.03, 0.26), paper)
	
	_add_slot(slots, "Stand", Vector3(0, 0, 0.45))
	_add_slot(slots, "Contact", Vector3(0, 0.04, 0))
	
	root.set_meta("title", title)
	root.set_meta("current_page", 1)
	root.set_meta("records", [])
	return root

## 3.27 黑板批注分层 (Blackboard)
static func create_blackboard(id: String, size: Vector2 = Vector2(2.2, 1.3)) -> Node3D:
	var root := _base_world_object(id, "blackboard")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var frame_mat := _mat(Color("3b281c"), 0.85)
	var slate_mat := _mat(Color("222824"), 0.9)
	_add_box(vis, col, "Frame", Vector3(0, 1.5, 0), Vector3(size.x, size.y, 0.06), frame_mat)
	_add_box(vis, null, "Slate", Vector3(0, 1.5, -0.015), Vector3(size.x - 0.12, size.y - 0.12, 0.02), slate_mat)
	
	_add_slot(slots, "Stand", Vector3(0, 0, 0.85))
	_add_slot(slots, "Contact", Vector3(0, 1.4, 0))
	
	root.set_meta("original_facts", ["Event 09:15 Arrival", "Event 10:00 Meeting Started"])
	root.set_meta("annotations", [])
	return root

## 3.28 讲台 (Lectern / Podium)
static func create_lectern(id: String, height: float = 1.15) -> Node3D:
	var root := _base_world_object(id, "lectern")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var wood := _mat(Color("543924"), 0.75)
	_add_box(vis, col, "Pedestal", Vector3(0, height * 0.45, 0), Vector3(0.55, height * 0.9, 0.45), wood)
	_add_box(vis, col, "SlopedDesk", Vector3(0, height, 0), Vector3(0.65, 0.06, 0.55), wood)
	
	_add_slot(slots, "SpeakerStand", Vector3(0, 0, -0.55))
	_add_slot(slots, "DocumentContact", Vector3(0, height, 0))
	_add_slot(slots, "AudienceApproach", Vector3(0, 0, 1.2))
	return root

## 3.29 讲堂桌席独立座 (Lecture Seating)
static func create_lecture_seating(id: String, seats_count: int = 4) -> Node3D:
	var root := _base_world_object(id, "lecture_seating")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var wood := _mat(Color("523928"), 0.8)
	var desk_len := float(seats_count) * 0.75
	# Long continuous desk
	_add_box(vis, col, "DeskTop", Vector3(0, 0.73, 0), Vector3(desk_len, 0.04, 0.45), wood)
	# Individual seats
	var step_x := desk_len / float(seats_count)
	for i in range(seats_count):
		var sx := -desk_len * 0.5 + (float(i) + 0.5) * step_x
		_add_box(vis, col, "SeatChair_%d" % (i + 1), Vector3(sx, 0.42, 0.55), Vector3(0.42, 0.04, 0.42), wood)
		_add_slot(slots, "Sit_%d" % (i + 1), Vector3(sx, 0.45, 0.55))
		_add_slot(slots, "AisleApproach_%d" % (i + 1), Vector3(sx, 0, 0.95))
		
	root.set_meta("seats_count", seats_count)
	root.set_meta("occupied_seats", {})
	return root

## 3.30 工作台工具槽 (Workbench)
static func create_workbench(id: String, size: Vector3 = Vector3(1.6, 0.85, 0.8)) -> Node3D:
	var root := _base_world_object(id, "workbench")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var wood := _mat(Color("634c38"), 0.9)
	var metal := _mat(Color("47484a"), 0.3, 0.8)
	_add_box(vis, col, "HeavyTop", Vector3(0, size.y - 0.04, 0), Vector3(size.x, 0.08, size.z), wood)
	_add_box(vis, col, "ViceClamp", Vector3(-size.x * 0.45, size.y + 0.05, -size.z * 0.4), Vector3(0.16, 0.12, 0.16), metal)
	
	_add_slot(slots, "Stand", Vector3(0, 0, -size.z * 0.5 - 0.5))
	_add_slot(slots, "Contact", Vector3(0, size.y, 0))
	_add_slot(slots, "ToolSlot_1", Vector3(-0.35, size.y, 0.15))
	_add_slot(slots, "ToolSlot_2", Vector3(0.35, size.y, 0.15))
	
	root.set_meta("required_tools", ["wrench", "hammer"])
	root.set_meta("has_tools", false)
	return root

## 3.31 压力表温度表读数 (Pressure & Temperature Gauges)
static func create_industrial_gauge(id: String, kind: String = "pressure", max_val: float = 10.0, unit: String = "bar") -> Node3D:
	var root := _base_world_object(id, "industrial_gauge")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var brass := _mat(Color("c4a24d"), 0.35, 0.8)
	var face := _mat(Color("f4f4ec"), 0.9)
	_add_box(vis, col, "GaugeBezel", Vector3(0, 1.4, 0), Vector3(0.24, 0.24, 0.06), brass)
	_add_box(vis, null, "GaugeDial", Vector3(0, 1.4, -0.02), Vector3(0.20, 0.20, 0.01), face)
	
	_add_slot(slots, "Stand", Vector3(0, 0, 0.65))
	_add_slot(slots, "Contact", Vector3(0, 1.4, 0))
	
	root.set_meta("gauge_kind", kind)
	root.set_meta("unit", unit)
	root.set_meta("current_value", 4.2 if kind == "pressure" else 82.5)
	root.set_meta("max_value", max_val)
	root.set_meta("last_read_timestamp", 0.0)
	return root

## 3.32 阀门手轮旋转与站位 (Valve & Handwheel)
static func create_valve_handwheel(id: String, max_turns: float = 3.0) -> Node3D:
	var root := _base_world_object(id, "valve_handwheel")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var iron := _mat(Color("7a3028"), 0.4, 0.7)
	var stem := _mat(Color("47484a"), 0.3, 0.8)
	_add_box(vis, col, "PipeStem", Vector3(0, 1.1, 0), Vector3(0.08, 0.08, 0.25), stem)
	_add_box(vis, null, "WheelRing", Vector3(0, 1.1, 0.14), Vector3(0.36, 0.36, 0.04), iron)
	
	_add_slot(slots, "Stand", Vector3(0, 0, 0.65))
	_add_slot(slots, "Contact", Vector3(0, 1.1, 0.14))
	
	root.set_meta("max_turns", max_turns)
	root.set_meta("current_rotation_deg", 0.0)
	root.set_meta("is_fully_open", false)
	return root

## 3.33 气闸双道隔离门 (Airlock Isolation Doors)
static func create_airlock_system(id: String, length_m: float = 3.0) -> Node3D:
	var root := _base_world_object(id, "airlock_system")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var steel := _mat(Color("4d555c"), 0.4, 0.8)
	# Door A (Outer at -length/2)
	_add_box(vis, col, "DoorLeaf_Outer", Vector3(0, 1.05, -length_m * 0.5), Vector3(1.0, 2.1, 0.08), steel)
	# Door B (Inner at +length/2)
	_add_box(vis, col, "DoorLeaf_Inner", Vector3(0, 1.05, length_m * 0.5), Vector3(1.0, 2.1, 0.08), steel)
	
	_add_slot(slots, "Stand_Outer", Vector3(0, 0, -length_m * 0.5 - 0.7))
	_add_slot(slots, "Stand_Inside", Vector3(0, 0, 0))
	_add_slot(slots, "Stand_Inner", Vector3(0, 0, length_m * 0.5 + 0.7))
	
	root.set_meta("door_outer_open", false)
	root.set_meta("door_inner_open", false)
	root.set_meta("chamber_sealed", true)
	return root

# -------------------------------------------------------------
# 3. 医疗与剧情证物 (Tasks 3.34 - 3.43)
# -------------------------------------------------------------

## 3.34 医疗床检查侧/给药侧 (Medical Bed)
static func create_medical_bed(id: String, size: Vector3 = Vector3(2.0, 0.65, 0.9)) -> Node3D:
	var root := _base_world_object(id, "medical_bed")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var metal := _mat(Color("8f9599"), 0.4, 0.6)
	var mattress := _mat(Color("e8ecef"), 0.95)
	_add_box(vis, col, "Frame", Vector3(0, size.y * 0.5, 0), size, metal)
	_add_box(vis, col, "Mattress", Vector3(0, size.y + 0.06, 0), Vector3(size.x - 0.08, 0.12, size.z - 0.08), mattress)
	
	_add_slot(slots, "Lie", Vector3(0, size.y + 0.12, 0))
	_add_slot(slots, "Stand_Exam", Vector3(0, 0, -size.z * 0.5 - 0.5))
	_add_slot(slots, "Stand_Meds", Vector3(0, 0, size.z * 0.5 + 0.5))
	_add_slot(slots, "Stand_Transfer", Vector3(size.x * 0.5 + 0.5, 0, 0))
	
	root.set_meta("patient_in_bed", true)
	root.set_meta("patient_locked", true) # Does not auto-stand during talk
	return root

## 3.35 双锁药柜 (Dual-Lock Medicine Cabinet)
static func create_dual_lock_cabinet(id: String, key1: String = "key_med_a", key2: String = "key_med_b") -> Node3D:
	var root := _base_world_object(id, "dual_lock_cabinet")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var enameled := _mat(Color("e0e5e8"), 0.4)
	var brass := _mat(Color("c4a24d"), 0.35, 0.8)
	_add_box(vis, col, "CabinetBody", Vector3(0, 0.8, 0), Vector3(0.9, 1.2, 0.45), enameled)
	_add_box(vis, null, "Lock_1", Vector3(-0.12, 0.8, -0.23), Vector3(0.04, 0.06, 0.02), brass)
	_add_box(vis, null, "Lock_2", Vector3(0.12, 0.8, -0.23), Vector3(0.04, 0.06, 0.02), brass)
	
	_add_slot(slots, "Stand", Vector3(0, 0, -0.7))
	_add_slot(slots, "ContactLock1", Vector3(-0.12, 0.8, -0.23))
	_add_slot(slots, "ContactLock2", Vector3(0.12, 0.8, -0.23))
	
	root.set_meta("key1_req", key1)
	root.set_meta("key2_req", key2)
	root.set_meta("lock1_unlocked", false)
	root.set_meta("lock2_unlocked", false)
	root.set_meta("is_open", false)
	return root

## 3.36 药瓶与三剂药守恒 (Medicine Bottles & Three Dosages)
static func create_medicine_bottle(id: String, batch_no: String = "BATCH-1926-C", total_doses: int = 3) -> Node3D:
	var root := _base_world_object(id, "medicine_bottle")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var amber_glass := _mat(Color(0.4, 0.25, 0.1, 0.7), 0.2, 0.1)
	amber_glass.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_add_box(vis, col, "BottleBody", Vector3(0, 0.09, 0), Vector3(0.08, 0.18, 0.08), amber_glass)
	
	_add_slot(slots, "Stand", Vector3(0, 0, 0.45))
	_add_slot(slots, "Contact", Vector3(0, 0.09, 0))
	
	root.set_meta("batch_number", batch_no)
	root.set_meta("total_doses", total_doses)
	root.set_meta("cabinet_stock", total_doses)
	root.set_meta("in_transit", 0)
	root.set_meta("carried", 0)
	root.set_meta("administered", 0)
	return root

## 3.37 双层标签残签 (Double-Layer Label)
static func create_layered_label(id: String, top_text: String, remnant_text: String) -> Node3D:
	var root := _base_world_object(id, "layered_label")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var paper_top := _mat(Color("f4eedb"), 0.9)
	var paper_under := _mat(Color("d6cdb8"), 0.95)
	_add_box(vis, col, "BaseProp", Vector3(0, 0.05, 0), Vector3(0.15, 0.10, 0.15), paper_top)
	_add_box(vis, null, "UnderRemnant", Vector3(0, 0.05, -0.076), Vector3(0.08, 0.04, 0.002), paper_under)
	_add_box(vis, null, "TopLabel", Vector3(0.01, 0.05, -0.078), Vector3(0.09, 0.05, 0.002), paper_top)
	
	_add_slot(slots, "Stand", Vector3(0, 0, 0.45))
	_add_slot(slots, "Contact", Vector3(0, 0.05, 0))
	
	root.set_meta("top_label_text", top_text)
	root.set_meta("remnant_text", remnant_text)
	root.set_meta("is_peeled", false)
	return root

## 3.38 清单碎片拼接 (List Fragments)
static func create_document_fragment(id: String, fragment_id: String, text: String) -> Node3D:
	var root := _base_world_object(id, "document_fragment")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var paper := _mat(Color("ede8d8"), 0.95)
	_add_box(vis, col, "FragmentSheet", Vector3(0, 0.002, 0), Vector3(0.12, 0.004, 0.16), paper)
	
	_add_slot(slots, "Stand", Vector3(0, 0, 0.35))
	_add_slot(slots, "Contact", Vector3(0, 0.002, 0))
	
	root.set_meta("fragment_id", fragment_id)
	root.set_meta("partial_text", text)
	return root

## 3.39 收餐凭条 (Meal Receipt Slip)
static func create_meal_receipt_slip(id: String, slip_number: String = "REC-4402") -> Node3D:
	var root := _base_world_object(id, "meal_receipt_slip")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var paper := _mat(Color("f2ede2"), 0.9)
	var metal_clip := _mat(Color("8f9599"), 0.3, 0.7)
	_add_box(vis, col, "SlipPaper", Vector3(0, 0.002, 0), Vector3(0.14, 0.004, 0.20), paper)
	_add_box(vis, null, "PaperClip", Vector3(-0.04, 0.005, -0.08), Vector3(0.03, 0.008, 0.04), metal_clip)
	
	_add_slot(slots, "Stand", Vector3(0, 0, 0.35))
	_add_slot(slots, "Contact", Vector3(0, 0.002, 0))
	
	root.set_meta("slip_number", slip_number)
	root.set_meta("status", "discovered") # discovered -> acquired -> verified -> filed
	return root

## 3.40 路线图对比 (Route Map Comparison)
static func create_route_map_comparison(id: String) -> Node3D:
	var root := _base_world_object(id, "route_map_comparison")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var cloth := _mat(Color("ded8c5"), 0.9)
	_add_box(vis, col, "MapTable", Vector3(0, 0.75, 0), Vector3(1.1, 0.04, 0.8), cloth)
	
	_add_slot(slots, "Stand", Vector3(0, 0, 0.65))
	_add_slot(slots, "Contact", Vector3(0, 0.75, 0))
	
	root.set_meta("full_blueprint_sections", ["1F", "2F", "3F_South", "3F_North"])
	root.set_meta("sketch_sections", ["1F", "2F", "3F_South"]) # Missing 3F_North
	return root

## 3.41 担架搬运 (Stretcher)
static func create_stretcher(id: String, length: float = 1.9, width: float = 0.65) -> Node3D:
	var root := _base_world_object(id, "stretcher")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var canvas := _mat(Color("736b56"), 0.9)
	var wood := _mat(Color("4a3525"), 0.8)
	_add_box(vis, col, "StretcherBed", Vector3(0, 0.15, 0), Vector3(length - 0.4, 0.04, width), canvas)
	# Carrying handles
	for sz: float in [-1.0, 1.0]:
		_add_box(vis, col, "Pole_%d" % int(sz), Vector3(0, 0.15, sz * (width * 0.5 - 0.03)), Vector3(length, 0.05, 0.05), wood)
		
	_add_slot(slots, "FrontGrip", Vector3(-length * 0.5 + 0.1, 0.15, 0))
	_add_slot(slots, "RearGrip", Vector3(length * 0.5 - 0.1, 0.15, 0))
	_add_slot(slots, "PatientBed", Vector3(0, 0.20, 0))
	
	root.set_meta("front_bearer", "")
	root.set_meta("rear_bearer", "")
	root.set_meta("patient", "")
	return root

## 3.42 警报装置局部传播 (Alarm Station)
static func create_alarm_station(id: String, sound_range: float = 25.0) -> Node3D:
	var root := _base_world_object(id, "alarm_station")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var red := _mat(Color("9c2a23"), 0.4, 0.3)
	var brass := _mat(Color("c4a24d"), 0.3, 0.8)
	_add_box(vis, col, "AlarmBox", Vector3(0, 1.4, 0), Vector3(0.25, 0.35, 0.12), red)
	_add_box(vis, null, "PullLever", Vector3(0, 1.35, 0.08), Vector3(0.04, 0.12, 0.04), brass)
	
	_add_slot(slots, "Stand", Vector3(0, 0, 0.55))
	_add_slot(slots, "Contact", Vector3(0, 1.35, 0.08))
	
	root.set_meta("is_active", false)
	root.set_meta("sound_range_m", sound_range)
	return root

## 3.43 公约签署工具 (Convention Signing Document)
static func create_convention_document(id: String, title: String = "Campus Neutrality Convention") -> Node3D:
	var root := _base_world_object(id, "convention_document")
	var vis := root.get_node("VisualRoot")
	var col := root.get_node("CollisionRoot/StaticBody3D")
	var slots := root.get_node("InteractionSlots")
	
	var parchment := _mat(Color("f4eedb"), 0.95)
	var inkstand := _mat(Color("3b3b3a"), 0.4, 0.7)
	_add_box(vis, col, "DocumentScroll", Vector3(0, 0.003, 0), Vector3(0.42, 0.006, 0.30), parchment)
	_add_box(vis, null, "Inkstand", Vector3(0.24, 0.03, 0), Vector3(0.08, 0.06, 0.08), inkstand)
	
	_add_slot(slots, "Stand", Vector3(0, 0, 0.45))
	_add_slot(slots, "Contact", Vector3(0, 0.01, 0))
	
	root.set_meta("title", title)
	root.set_meta("signatures", {}) # actor_id -> timestamp
	return root


# -------------------------------------------------------------
# Internal Helpers
# -------------------------------------------------------------

static func _base_world_object(id: String, kind: String) -> Node3D:
	var root := Node3D.new()
	root.name = "WorldObject3D"
	root.set_meta("world_object_id", id)
	root.set_meta("kind", kind)
	
	var vis := Node3D.new()
	vis.name = "VisualRoot"
	root.add_child(vis)
	
	var col_root := Node3D.new()
	col_root.name = "CollisionRoot"
	root.add_child(col_root)
	
	var body := StaticBody3D.new()
	body.name = "StaticBody3D"
	body.collision_layer = PhysicsLayers.SOLID
	body.collision_mask = 0
	col_root.add_child(body)
	
	var area := Area3D.new()
	area.name = "InteractionArea"
	area.collision_layer = PhysicsLayers.INTERACTION
	area.collision_mask = PhysicsLayers.ACTOR
	root.add_child(area)
	
	var area_shape := CollisionShape3D.new()
	area_shape.name = "AreaCollision"
	var area_box := BoxShape3D.new()
	area_box.size = Vector3(1.6, 1.8, 1.6)
	area_shape.shape = area_box
	area_shape.position = Vector3(0, 0.9, 0)
	area.add_child(area_shape)
	
	var slots := Node3D.new()
	slots.name = "InteractionSlots"
	root.add_child(slots)
	
	var label := Label3D.new()
	label.name = "LabelAnchor"
	label.text = ""
	label.position = Vector3(0, 1.2, 0)
	label.visible = false
	label.no_depth_test = false
	root.add_child(label)
	
	return root

static func _add_box(vis: Node3D, col: StaticBody3D, part_name: String, pos: Vector3, size: Vector3, mat: Material) -> void:
	if vis != null:
		var m := MeshInstance3D.new()
		m.name = part_name
		var box := BoxMesh.new()
		box.size = size
		m.mesh = box
		m.position = pos
		if mat != null:
			m.material_override = mat
		vis.add_child(m)
	if col != null:
		var c := CollisionShape3D.new()
		c.name = "Col_" + part_name
		var box := BoxShape3D.new()
		box.size = size
		c.shape = box
		c.position = pos
		col.add_child(c)

static func _add_slot(parent: Node3D, slot_name: String, pos: Vector3) -> void:
	var marker := Marker3D.new()
	marker.name = slot_name
	marker.position = pos
	parent.add_child(marker)

static func _mat(color: Color, roughness: float = 0.8, metallic: float = 0.0) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.roughness = roughness
	m.metallic = metallic
	return m
