extends Node2D
## 现场痕迹：沿墙的传声铜管、松动的管接头、门内的扇形灰、马球靴印、湿布、门框凹痕、书架上的无尘方框与窄手印。
## 贴图由 tools/build_awakening_evidence.py 以原生像素绘制，放在 ShareAsset/Internal/Props/AwakeningEvidence/。

const ART := "res://ShareAsset/Internal/Props/AwakeningEvidence/"
## 铜管走向：铜口 → 沿活动厅北墙向西 → 门厅北墙 → 贴西墙下到隔离门北侧门框（松动接头）；
## 另一路向东，钻进活动厅与 R-08 之间的墙（松动接头在 R-08 墙后）。
const PIPES := [
	[Vector2(500, 40), Vector2(304, 40), Vector2(304, 266), Vector2(164, 266), Vector2(164, 346), Vector2(154, 346)],
	[Vector2(500, 40), Vector2(592, 40), Vector2(592, 204)],
]
## [贴图, 位置, 旋转角度]：门在门厅西墙，灰扇向东散开，湿布贴着门竖着压成一条，靴印鞋尖朝北侧的插闩把手。
const DECALS := [
	["ash_fan.png", Vector2(184, 322), 90.0],
	["boot_print.png", Vector2(156, 418), -90.0],
	["wet_cloth.png", Vector2(157, 354), 90.0],
	["pipe_joint.png", Vector2(157, 320), 0.0],
	["pipe_joint.png", Vector2(585, 186), 0.0],
	["frame_dent.png", Vector2(157, 344), 0.0],
]

var host: Node
var textures: Dictionary = {}

func _ready() -> void:
	for name in ["ash_fan.png", "boot_print.png", "wet_cloth.png", "pipe_joint.png", "frame_dent.png", "shelf_dust.png", "shelf_dust_hand.png", "shelf_dust_even.png"]:
		if ResourceLoader.exists(ART + name):
			textures[name] = load(ART + name)

func _draw() -> void:
	var copper := Color("9d7031")
	var copper_light := Color("c89a45")
	var shade := Color("44291a")
	for pipe in PIPES:
		_pipe(pipe, copper, copper_light, shade)
	for spec in DECALS:
		var texture: Texture2D = textures.get(spec[0])
		if texture:
			draw_set_transform(spec[1], deg_to_rad(spec[2]))
			draw_texture(texture, Vector2.ZERO)
	draw_set_transform(Vector2.ZERO)
	# 书架上的灰：R-05 有半枚窄手印；四人的房里有干净的方框；空房的灰落得很匀。
	for shelf in host.layout.shelves:
		var texture: Texture2D = textures.get({"hand": "shelf_dust_hand.png", "plain": "shelf_dust.png", "even": "shelf_dust_even.png"}[shelf[2]])
		if texture:
			draw_texture(texture, Vector2(shelf[0], shelf[1]))

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
