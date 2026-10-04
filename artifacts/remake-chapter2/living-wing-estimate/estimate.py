# 生活翼方庭占地估算（设计规范第三部分 §3.2）。单位：米；游戏格 32px。
# 运行：python estimate.py  → 打印并写出 estimate.json
import json, math, os

WALL_OUT, WALL_IN, PART = 0.6, 0.3, 0.25   # 外墙、内墙、隔墙厚
ROOM_W, ROOM_D = 3.6, 4.8                   # 1900 年前后书院式单人起居兼卧室，约 17 m²
CORRIDOR, HALL_D, FOYER_D = 1.8, 6.0, 3.0   # 寝区内走廊、活动厅/备餐间进深、门厅进深
CLOISTER = 2.5                              # 回廊净宽
ROOMS_PER_ZONE = 6
STOREY_H = 4.2                              # 层高（含屋架以下）

zone_len = ROOMS_PER_ZONE * ROOM_W + (ROOMS_PER_ZONE - 1) * PART + 2 * WALL_OUT
zone_depth = WALL_OUT + ROOM_D + WALL_IN + CORRIDOR + WALL_IN + HALL_D + WALL_IN + FOYER_D + WALL_OUT

def quad(zone_len, zone_depth):
    side = zone_len + 2 * zone_depth          # 四角为门楼与浴室，各占 进深×进深
    court = zone_len - 2 * CLOISTER
    return dict(outer_side=round(side, 1), courtyard_side=round(court, 1),
                footprint_m2=round(side * side), courtyard_m2=round(court * court),
                roofed_m2=round(side * side - court * court),
                roofed_per_student_m2=round((side * side - court * court) / 16, 1))

single = quad(zone_len, zone_depth)
# 对照：寝区两层（每层三间），进深不变
two_storey = quad(3 * ROOM_W + 2 * PART + 2 * WALL_OUT, zone_depth)

# 坡度：旧岛屿模型（artifacts/island-3d-structure，未通过验收，仅作量级参考）南部学园楼基 22.5—27.0 m，跨约 250 m
grade_low, grade_high = 0.02, 0.03
drop = [round(single['outer_side'] * g, 1) for g in (grade_low, grade_high)]

# 对照尺度
refs = {
    '旧模型_中央厅鼓座直径': 40.0,
    '旧模型_中央厅至门楼净距': 232 - 20,
    '旧模型_学园范围_m2': round(math.pi * 240 * 215),
    '牛津_Tom_Quad_边长约': 80.0,
}

# 游戏地图：第一章乙区 40×32 格容四间房；人物 48px≈1.7 m → 1 格≈1.1 m；玩家步速 90 px/s≈2.8 格/s
TILE_M, SPEED_TILES = 1.1, 90 / 32
zone_tiles_w = 40 + 2 * 10                 # 六间房：每间房含隔墙约 10 格
zone_tiles_d = 32
literal_side = zone_tiles_w + 2 * zone_tiles_d
compact_side = 40                           # 方庭地图压缩为一屏（1280×992 ≈ 40×31 格）
game = dict(
    zone_interior_tiles=[zone_tiles_w, zone_tiles_d],
    literal_quad_side_tiles=literal_side,
    literal_cross_seconds=round(literal_side / SPEED_TILES),
    literal_loop_seconds=round(4 * (literal_side - zone_tiles_d) / SPEED_TILES),
    compact_quad_side_tiles=compact_side,
    compact_cross_seconds=round(compact_side / SPEED_TILES),
    ch1_cross_seconds=round(40 / SPEED_TILES),
)

out = dict(zone_len_m=round(zone_len, 1), zone_depth_m=round(zone_depth, 1), storey_h_m=STOREY_H,
           single_storey=single, two_storey_zones=two_storey,
           slope_drop_across_m=drop, references=refs, game_map=game)
print(json.dumps(out, ensure_ascii=False, indent=2))
with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'estimate.json'), 'w', encoding='utf-8') as f:
    json.dump(out, f, ensure_ascii=False, indent=2)
