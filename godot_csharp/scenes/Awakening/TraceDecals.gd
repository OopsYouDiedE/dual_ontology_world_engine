extends Node2D
## 现场痕迹：沿墙的传声铜管、松动的管接头、门内的扇形灰、马球靴印、湿布、门框凹痕、书架上的无尘方框与窄手印。
## 贴图由 tools/build_awakening_evidence.py 以原生像素绘制，放在 ShareAsset/Internal/Props/AwakeningEvidence/。

const ART := "res://ShareAsset/Internal/Props/AwakeningEvidence/"
const Bindings := preload("res://scenes/Shared/Field/PresentationBindings.gd")
## 铜管走向：铜口 → 沿活动厅北墙向西 → 门厅北墙 → 贴西墙下到隔离门北侧门框（松动接头）；
## 另一路向东，钻进活动厅与 R-08 之间的墙（松动接头在 R-08 墙后）。
# STORY-HARDCODE[AWK-042][schema][production]: 绘制本案铜管两条具体路径及松动接头位置。
const PIPES := [
	[Vector2(500, 40), Vector2(304, 40), Vector2(304, 266), Vector2(164, 266), Vector2(164, 346), Vector2(154, 346)],
	[Vector2(500, 40), Vector2(592, 40), Vector2(592, 204)],
]
## 局部贴花槽的纯几何；内容与纹理由剧本声明绑定。
const DECALS := [
	[Vector2(184, 322), 90.0], [Vector2(156, 418), -90.0], [Vector2(157, 354), 90.0],
	[Vector2(157, 320), 0.0], [Vector2(585, 186), 0.0], [Vector2(157, 344), 0.0],
]

var host: Node
var textures: Dictionary = {}
var states: Dictionary = {}

func _ready() -> void:
	for id in host.spatial.presentation.traces:
		var spec: Dictionary = host.spatial.presentation.traces[id]
		var path := ART + str(spec.texture)
		if not ResourceLoader.exists(path):
			Bindings.fail(self, "Missing trace asset: " + str(id) + " -> " + path)
			return
		textures[id] = load(path)
		if spec.has("node") and not host.layout.shelves.has(str(spec.node)):
			Bindings.fail(self, "Missing trace node: " + str(id) + " -> " + str(spec.node))
			return
		if spec.has("slot") and (int(spec.slot) < 0 or int(spec.slot) >= DECALS.size()):
			Bindings.fail(self, "Missing trace geometry slot: " + str(id))
			return

func apply_snapshot(value: Dictionary) -> void:
	for id in value:
		if not host.spatial.presentation.traces.has(id) or not value[id] is bool:
			Bindings.fail(self, "Unknown or invalid authoritative trace state: " + str(id))
			return
	for id in host.spatial.presentation.traces:
		if not value.has(id):
			Bindings.fail(self, "Missing authoritative trace state: " + str(id))
			return
	states = value.duplicate()
	queue_redraw()

func _process(_delta: float) -> void:
	if host == null or host.sight == null:
		return
	# 新进入房间后重新绘制墙边痕迹，未知房间的书架灰痕也不能穿过明亮墙格。
	queue_redraw()

func _known(point: Vector2) -> bool:
	return host != null and host.sight != null and host.sight.can_see(point)

func _draw() -> void:
	var copper := Color("9d7031")
	var copper_light := Color("c89a45")
	var shade := Color("44291a")
	for pipe in PIPES:
		_pipe(pipe, copper, copper_light, shade)
	for id in states:
		if not bool(states[id]): continue
		var spec: Dictionary = host.spatial.presentation.traces[id]
		var point: Vector2
		var angle := 0.0
		if spec.has("slot"):
			var geometry: Array = DECALS[int(spec.slot)]
			point = geometry[0]
			angle = float(geometry[1])
		else:
			var shelf: Array = host.layout.shelves[str(spec.node)]
			point = Vector2(float(shelf[0]), float(shelf[1]))
		if not _known(point): continue
		draw_set_transform(point, deg_to_rad(angle))
		draw_texture(textures[id], Vector2.ZERO)
	draw_set_transform(Vector2.ZERO)

func _pipe(points: Array, copper: Color, copper_light: Color, shade: Color) -> void:
	for i in range(1, points.size()):
		var a: Vector2 = points[i - 1]
		var b: Vector2 = points[i]
		var r := Rect2(a.min(b) - Vector2(1, 1), (b - a).abs() + Vector2(3, 3))
		draw_rect(r, shade)
		draw_rect(r.grow(-1), copper)
		if a.y == b.y:
			draw_rect(Rect2(r.position + Vector2(1, 1), Vector2(r.size.x - 2, 1)), copper_light)
		else:
			draw_rect(Rect2(r.position + Vector2(1, 1), Vector2(1, r.size.y - 2)), copper_light)
		# 管卡
		var length := a.distance_to(b)
		for step in range(16, int(length), 48):
			var p := a.lerp(b, step / length).round()
			draw_rect(Rect2(p - Vector2(2, 2), Vector2(5, 5)), shade)
