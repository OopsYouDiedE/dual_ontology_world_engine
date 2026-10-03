extends Node2D
## 现场痕迹：沿墙的传声铜管、松动的管接头、门内的扇形灰、马球靴印、湿布、门框凹痕、书架上的无尘方框与窄手印。
## 贴图由 tools/build_awakening_evidence.py 以原生像素绘制，放在 ShareAsset/Internal/Props/AwakeningEvidence/。

const ART := "res://ShareAsset/Internal/Props/AwakeningEvidence/"
## 铜管走向：铜口 → 活动厅北墙 → 活动厅与备餐间之间的墙 → 门厅北墙脚 → 隔离门左侧门框。
const PIPE := [Vector2(512, 506), Vector2(616, 506), Vector2(616, 892), Vector2(580, 892), Vector2(580, 958)]
const DECALS := [
	["ash_fan.png", Vector2(596, 926)],
	["boot_print.png", Vector2(652, 946)],
	["wet_cloth.png", Vector2(598, 953)],
	["pipe_joint.png", Vector2(574, 950)],
	["frame_dent.png", Vector2(696, 970)],
]
const SHELVES := [Vector2(38, 50), Vector2(358, 50), Vector2(678, 50), Vector2(998, 50)]

var host: Node
var textures: Dictionary = {}

func _ready() -> void:
	for name in ["ash_fan.png", "boot_print.png", "wet_cloth.png", "pipe_joint.png", "frame_dent.png", "shelf_dust.png", "shelf_dust_hand.png"]:
		if ResourceLoader.exists(ART + name):
			textures[name] = load(ART + name)

func _draw() -> void:
	var copper := Color("9d7031")
	var copper_light := Color("c89a45")
	var shade := Color("44291a")
	for i in range(1, PIPE.size()):
		var a: Vector2 = PIPE[i - 1]
		var b: Vector2 = PIPE[i]
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
	for spec in DECALS:
		var texture: Texture2D = textures.get(spec[0])
		if texture:
			draw_texture(texture, spec[1])
	for i in SHELVES.size():
		var texture: Texture2D = textures.get("shelf_dust_hand.png" if i == 0 else "shelf_dust.png")
		if texture:
			draw_texture(texture, SHELVES[i])
