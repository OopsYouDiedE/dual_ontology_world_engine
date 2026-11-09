extends Node3D

## Sims 式现场表现导演（纯表现层，不改写任何事实或心智）。
## 只使用 CampaignRoomHost 的公开端口：actor_bodies / actor_activity / target_node / floor_point /
## route / report_ambient_placement / is_core_navigating / visible_activity / viewer_id / selected_actor。
##  1. 空闲人物按个体节律做环顾、短距踱步、面向附近的人（社交待机），不再原地僵立；
##  2. 工作中人物在行动开始时面向行动焦点（物件或交谈对象）；
##  3. 静止人物之间维持最小间距（分离转向），避免开局与入场后堆叠；
##  4. 头顶气泡只显示公开可见的活动类别，不显示私有需要、意图或评分；
##  5. 说服博弈：提议进行中对象头顶显示“权衡中…”，结果出现后短暂显示“采纳 / 婉拒”。
## 踱步与分离都走真实导航网格，并把最终落点回报 Core（ReportActorPlacement）。

const CATEGORY_WORDS := {
	"talk": "交谈", "inspect": "查看", "read": "阅读", "write": "记录", "eat": "进食", "drink": "饮水",
	"sleep": "睡眠", "rest": "休息", "walk": "走动", "travel": "赶路", "carry": "搬运", "care": "照护",
	"deduce": "思索", "cook": "烹煮", "wash": "清洗", "interact": "忙碌", "work": "劳作", "operate": "操作",
}
const CATEGORY_COLORS := {
	"talk": Color("9fd3ff"), "inspect": Color("ffd98a"), "read": Color("e8d2a6"), "eat": Color("ffb38a"),
	"drink": Color("8fe3ff"), "sleep": Color("b8a8ff"), "rest": Color("c8e6b0"), "deduce": Color("ffe36e"),
}

@export var separation_radius := 0.62
@export var separation_speed := 0.45
@export var wander_speed := 0.85
@export var bubble_range := 7.5
@export var min_idle_interval := 2.8
@export var max_idle_interval := 7.5

var host: Node = null
var _state: Dictionary = {}
var _flash: Dictionary = {}
var _plumbob: Node3D = null
var _clock := 0.0

func configure(field_host: Node) -> void:
	host = field_host
	_plumbob = _build_plumbob()
	add_child(_plumbob)
	_plumbob.visible = false

## 宿主在人物既无 Core 导航、也不在工作阶段时逐帧调用。
func ambient(actor: String, body: CharacterBody3D, delta: float) -> void:
	if not is_instance_valid(body): return
	var s := _actor_state(actor)
	s.working_execution = -1
	s.timer = float(s.timer) - delta
	match str(s.mode):
		"wander":
			var target: Vector3 = s.target
			if body.position.distance_to(target) <= 0.12 or float(s.timer) <= -6.0:
				body.stop_and_hold_position()
				s.mode = "stand"
				s.timer = _next_interval(s)
				_report(actor)
			else:
				body.step_towards(target, delta, wander_speed)
			return
		"look":
			body.play_action("idle")
			body.set_target_facing(float(s.yaw))
		"social":
			var peer: Variant = _nearest_peer(actor, body, 2.6)
			if peer != null: body.face_world_direction((peer as Node3D).position - body.position)
			body.play_action("idle")
		_:
			body.play_action("idle")
	if float(s.timer) > 0.0: return
	_choose_idle_behaviour(actor, body, s)

## 宿主在人物处于 Core 工作阶段时逐帧调用：每次新执行只调整一次朝向，不与其它控制器抢朝向。
func working(actor: String, body: CharacterBody3D, action: Dictionary, activity: Dictionary) -> void:
	if not is_instance_valid(body) or not is_instance_valid(host): return
	var s := _actor_state(actor)
	var execution := int(activity.get("execution", 0))
	if int(s.working_execution) == execution: return
	s.working_execution = execution
	s.mode = "stand"
	s.timer = _next_interval(s)
	var focus := str(action.get("focus", actor))
	if focus == actor or not host.has_method("target_node"): return
	var node: Variant = host.target_node(focus)
	if not node is Node3D or node == body or not (node as Node3D).is_inside_tree(): return
	var direction: Vector3 = (node as Node3D).global_position - body.global_position
	direction.y = 0.0
	if direction.length() > 0.05: body.face_world_direction(direction)
	if action.has("suggestion") and node is CharacterBody3D:
		var target_actor := str((node as Node).get_meta("campaign_actor", ""))
		if not target_actor.is_empty(): flash(target_actor, "权衡中…", Color("ffe9a8"), 2.5)

## 短暂的头顶提示（例如说服结果）。
func flash(actor: String, text: String, color: Color, seconds: float = 3.0) -> void:
	_flash[actor] = {"text": text, "color": color, "until": _clock + seconds}

func tick(delta: float, frozen: bool) -> void:
	_clock += delta
	if not is_instance_valid(host) or not host.has_method("actor_bodies"): return
	var bodies: Dictionary = host.actor_bodies()
	if not frozen: _separate(bodies, delta)
	_update_bubbles(bodies)
	_update_plumbob(bodies, delta)

# ============================================================ internal ===

func _actor_state(actor: String) -> Dictionary:
	if not _state.has(actor):
		var rng := RandomNumberGenerator.new()
		rng.seed = hash(actor) & 0x7fffffff
		_state[actor] = {"rng": rng, "mode": "stand", "timer": rng.randf_range(0.4, 2.4), "target": Vector3.ZERO,
			"yaw": 0.0, "working_execution": -1, "moved": false}
	return _state[actor]

func _next_interval(s: Dictionary) -> float:
	var rng: RandomNumberGenerator = s.rng
	return rng.randf_range(min_idle_interval, max_idle_interval)

func _choose_idle_behaviour(actor: String, body: CharacterBody3D, s: Dictionary) -> void:
	var rng: RandomNumberGenerator = s.rng
	var roll := rng.randf()
	s.timer = _next_interval(s)
	if roll < 0.35 and _nearest_peer(actor, body, 2.6) != null:
		s.mode = "social"
		return
	if roll < 0.62:
		var target := _wander_target(actor, body, rng)
		if target.x != INF:
			s.mode = "wander"
			s.target = target
			return
	s.mode = "look"
	s.yaw = body.rotation.y + rng.randf_range(-1.1, 1.1)

func _wander_target(actor: String, body: CharacterBody3D, rng: RandomNumberGenerator) -> Vector3:
	if not host.has_method("floor_point") or not host.has_method("route"): return Vector3(INF, INF, INF)
	for attempt in range(6):
		var angle := rng.randf_range(0.0, TAU)
		var radius := rng.randf_range(0.6, 1.6)
		var probe := body.position + Vector3(cos(angle), 0.0, sin(angle)) * radius
		var floor: Vector3 = host.floor_point(probe)
		if floor.distance_to(probe) > 0.3 or absf(floor.y - body.position.y) > 0.2: continue
		if _crowded(actor, floor, separation_radius + 0.15): continue
		var path: PackedVector3Array = host.route(body.position, floor)
		if path.is_empty() or path[path.size() - 1].distance_to(floor) > 0.2: continue
		var length := 0.0
		var prior := body.position
		for point in path:
			length += prior.distance_to(point)
			prior = point
		if length <= radius * 1.6: return floor
	return Vector3(INF, INF, INF)

func _nearest_peer(actor: String, body: Node3D, radius: float) -> Variant:
	var best: Variant = null
	var best_distance := radius
	var bodies: Dictionary = host.actor_bodies()
	for other_actor in bodies:
		if other_actor == actor: continue
		var other: Node3D = bodies[other_actor]
		if not is_instance_valid(other) or not other.visible: continue
		var distance := other.position.distance_to(body.position)
		if distance < best_distance:
			best_distance = distance
			best = other
	return best

func _crowded(actor: String, point: Vector3, radius: float) -> bool:
	var bodies: Dictionary = host.actor_bodies()
	for other_actor in bodies:
		if other_actor == actor: continue
		var other: Node3D = bodies[other_actor]
		if is_instance_valid(other) and other.position.distance_to(point) < radius: return true
	return false

## 静止且不受 Core 导航控制的人物之间做分离转向；落点经导航网格校验后回报 Core。
func _separate(bodies: Dictionary, delta: float) -> void:
	var viewer := str(host.viewer_id()) if host.has_method("viewer_id") else ""
	var player_driven := host.has_method("player_controls_viewer") and bool(host.player_controls_viewer())
	for actor in bodies:
		var body: CharacterBody3D = bodies[actor]
		if not is_instance_valid(body) or not body.visible: continue
		if actor == viewer and player_driven: continue
		if host.has_method("is_core_navigating") and bool(host.is_core_navigating(actor)): continue
		var s := _actor_state(actor)
		if str(s.mode) == "wander": continue
		var activity: Dictionary = host.actor_activity(actor) if host.has_method("actor_activity") else {}
		if str(activity.get("phase", "")) == "working": continue
		var push := Vector3.ZERO
		for other_actor in bodies:
			if other_actor == actor: continue
			var other: Node3D = bodies[other_actor]
			if not is_instance_valid(other) or not other.visible: continue
			var offset := body.position - other.position
			offset.y = 0.0
			var distance := offset.length()
			if distance >= separation_radius: continue
			if distance < 0.01:
				var rng: RandomNumberGenerator = s.rng
				offset = Vector3(rng.randf_range(-1.0, 1.0), 0.0, rng.randf_range(-1.0, 1.0))
				distance = 0.01
			push += offset.normalized() * (separation_radius - distance) / separation_radius
		if push.length() < 0.001:
			if bool(s.moved):
				s.moved = false
				_report(actor)
			continue
		var target := body.position + push.normalized() * separation_speed * delta
		var floor: Vector3 = host.floor_point(target)
		if floor.distance_to(target) <= 0.12 and absf(floor.y - body.position.y) <= 0.15:
			body.position = floor
			s.moved = true

func _report(actor: String) -> void:
	if host.has_method("report_ambient_placement"): host.report_ambient_placement(actor)

func _update_bubbles(bodies: Dictionary) -> void:
	var focus_body: Variant = host.player_body() if host.has_method("player_body") else null
	for actor in bodies:
		var body: Node3D = bodies[actor]
		if not is_instance_valid(body): continue
		var bubble := body.get_node_or_null("ThoughtBubble") as Label3D
		if bubble == null: bubble = _build_bubble(body)
		var text := ""
		var color := Color("efe7d5")
		var flash_entry: Dictionary = _flash.get(actor, {})
		if not flash_entry.is_empty() and float(flash_entry.until) > _clock:
			text = str(flash_entry.text)
			color = flash_entry.color
		else:
			_flash.erase(actor)
			var category := str(host.visible_activity(actor)) if host.has_method("visible_activity") else ""
			if not category.is_empty():
				text = str(CATEGORY_WORDS.get(category, CATEGORY_WORDS["interact"]))
				color = CATEGORY_COLORS.get(category, Color("efe7d5"))
			elif str(_actor_state(actor).mode) == "social":
				text = "…"
		var near := true
		if focus_body is Node3D and is_instance_valid(focus_body):
			near = (focus_body as Node3D).position.distance_to(body.position) <= bubble_range
		bubble.visible = body.visible and near and not text.is_empty()
		bubble.text = text
		bubble.modulate = color

func _build_bubble(body: Node3D) -> Label3D:
	var bubble := Label3D.new()
	bubble.name = "ThoughtBubble"
	var label := body.get_node_or_null("CampaignActorName") as Label3D
	bubble.position.y = (label.position.y if label != null else 1.6) + 0.26
	bubble.font_size = 34
	bubble.pixel_size = 0.005
	bubble.outline_size = 9
	bubble.outline_modulate = Color("1a1715")
	bubble.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	bubble.no_depth_test = true
	if label != null and label.font != null: bubble.font = label.font
	body.add_child(bubble)
	return bubble

func _build_plumbob() -> Node3D:
	var root := Node3D.new()
	root.name = "SelectionPlumbob"
	var material := StandardMaterial3D.new()
	material.albedo_color = Color(0.42, 0.95, 0.42, 0.9)
	material.emission_enabled = true
	material.emission = Color(0.25, 0.85, 0.3)
	material.emission_energy_multiplier = 0.8
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	for upper in [true, false]:
		var mesh := CylinderMesh.new()
		mesh.radial_segments = 4
		mesh.rings = 1
		mesh.height = 0.16
		mesh.top_radius = 0.0 if upper else 0.09
		mesh.bottom_radius = 0.09 if upper else 0.0
		var instance := MeshInstance3D.new()
		instance.mesh = mesh
		instance.material_override = material
		instance.position.y = 0.08 if upper else -0.08
		instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		root.add_child(instance)
	return root

func _update_plumbob(bodies: Dictionary, delta: float) -> void:
	if not is_instance_valid(_plumbob): return
	var target_actor := str(host.selected_actor()) if host.has_method("selected_actor") else ""
	if target_actor.is_empty() or not bodies.has(target_actor):
		target_actor = str(host.viewer_id()) if host.has_method("viewer_id") else ""
	var body: Variant = bodies.get(target_actor)
	if not body is Node3D or not is_instance_valid(body) or not (body as Node3D).visible:
		_plumbob.visible = false
		return
	var node := body as Node3D
	var label := node.get_node_or_null("CampaignActorName") as Label3D
	var height := (label.position.y if label != null else 1.6) + 0.62
	_plumbob.visible = true
	_plumbob.global_position = node.global_position + Vector3.UP * (height + sin(_clock * 2.2) * 0.04)
	_plumbob.rotation.y += delta * 1.6
