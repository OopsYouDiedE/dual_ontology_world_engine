"""Furniture placement: the single source for sprites, footprints and baked floor work.

position is the sprite's bottom-centre floor anchor; footprint is the solid
floor rectangle used for collision, routing and baked contact shadow.
"""
from __future__ import annotations


def dorm(i: int) -> list[dict]:
    bx = 32 + 320 * i
    items = [
        {"name": f"Bookcase{i}", "sprite": "bookcase_empty", "position": [bx + 26, 110], "footprint": [bx + 6, 96, 40, 14]},
        {"name": f"Bed{i}", "sprite": "bed_0", "position": [bx + 122, 166], "footprint": [bx + 104, 98, 36, 64]},
        {"name": f"Nightstand{i}", "sprite": "nightstand", "position": [bx + 154, 120], "footprint": [bx + 145, 106, 18, 12]},
        {"name": f"Wardrobe{i}", "sprite": "wardrobe", "position": [bx + 226, 112], "footprint": [bx + 206, 96, 40, 16]},
        {"name": f"Desk{i}", "sprite": "desk_0", "position": [bx + 36, 178], "footprint": [bx + 7, 150, 58, 26]},
        {"name": f"DeskChair{i}", "sprite": "chair_coat" if i == 0 else "chair_back", "position": [bx + 36, 200], "footprint": [bx + 29, 188, 14, 10]},
        {"name": f"LinenChest{i}", "sprite": "linen_chest_0", "position": [bx + 226, 206], "footprint": [bx + 207, 188, 38, 16]},
        {"name": f"Fern{i}", "sprite": "fern", "position": [bx + 62, 282], "footprint": [bx + 55, 272, 14, 10]},
        {"name": f"Dresser{i}", "sprite": "dresser", "position": [bx + 20, 256], "footprint": [bx + 3, 240, 34, 16]},
        {"name": f"CoatStand{i}", "sprite": "coat_stand", "position": [bx + 236, 282], "footprint": [bx + 230, 274, 12, 8]},
    ]
    # Standard furnishings cannot reveal an occupant's profession or shared past.
    # The wrapped wedge is the surviving case clue, not a personal furnishing.
    items.append({"name": f"PaperBasket{i}", "sprite": "paper_basket" if i == 0 else "paper_basket_empty", "position": [bx + 76, 196], "footprint": [bx + 70, 188, 12, 8]})
    items.append({"name": "WallClock" if i == 0 else f"WallClock{i}", "sprite": "wall_clock", "position": [bx + 184, 82], "footprint": None})
    return items


FURNITURE: list[dict] = [item for i in range(4) for item in dorm(i)] + [
    # Common study
    {"name": "StudyBookcaseA", "sprite": "bookcase_study", "position": [70, 558], "footprint": [50, 544, 40, 14]},
    {"name": "StudyBookcaseB", "sprite": "bookcase_study_b", "position": [114, 558], "footprint": [94, 544, 40, 14]},
    {"name": "StudyBookcaseC", "sprite": "bookcase_0", "position": [568, 558], "footprint": [548, 544, 40, 14]},
    {"name": "CopperTransmitter", "sprite": "transmitter", "position": [498, 530], "footprint": None},
    {"name": "TransmitterRules", "sprite": "instructions", "position": [522, 526], "footprint": None},
    {"name": "StudyChairNW", "sprite": "chair_front", "position": [288, 628], "footprint": [281, 616, 14, 10]},
    {"name": "StudyChairNE", "sprite": "chair_front", "position": [352, 628], "footprint": [345, 616, 14, 10]},
    {"name": "StudyTable", "sprite": "study_table", "position": [320, 680], "footprint": [254, 626, 132, 46]},
    {"name": "StudyChairSW", "sprite": "chair_back", "position": [288, 704], "footprint": [281, 692, 14, 10]},
    {"name": "StudyChairSE", "sprite": "chair_back", "position": [352, 704], "footprint": [345, 692, 14, 10]},
    {"name": "StudyLampW", "sprite": "floor_lamp", "position": [200, 602], "footprint": [195, 594, 10, 8]},
    {"name": "StudyLampE", "sprite": "floor_lamp", "position": [438, 708], "footprint": [433, 700, 10, 8]},
    {"name": "ReadingChair", "sprite": "armchair", "position": [70, 760], "footprint": [56, 742, 28, 16]},
    {"name": "ReadingChairB", "sprite": "armchair", "position": [138, 760], "footprint": [124, 742, 28, 16]},
    {"name": "ReadingTable", "sprite": "side_table_plain", "position": [104, 766], "footprint": [92, 752, 24, 12]},
    {"name": "Globe", "sprite": "globe", "position": [52, 646], "footprint": [44, 638, 16, 8]},
    {"name": "StudySofa", "sprite": "sofa", "position": [446, 792], "footprint": [418, 774, 56, 18]},
    {"name": "StudyFern", "sprite": "plant_stand", "position": [590, 744], "footprint": [582, 736, 16, 8]},
    {"name": "StudyConsole", "sprite": "console_tray", "position": [584, 660], "footprint": [566, 648, 36, 12]},
    # Washroom and pantry
    {"name": "Washstand", "sprite": "washstand", "position": [863, 570], "footprint": [839, 548, 48, 20]},
    {"name": "Sideboard", "sprite": "sideboard", "position": [1150, 570], "footprint": [1109, 546, 82, 22]},
    {"name": "Range", "sprite": "range_stove", "position": [1216, 568], "footprint": [1195, 546, 42, 20]},
    {"name": "Worktable", "sprite": "worktable", "position": [980, 692], "footprint": [932, 654, 96, 30]},
    {"name": "CupTable", "sprite": "side_table", "position": [746, 652], "footprint": [732, 638, 28, 14]},
    {"name": "PantryLamp", "sprite": "floor_lamp", "position": [1104, 704], "footprint": [1099, 696, 10, 8]},
    {"name": "PantryFern", "sprite": "fern", "position": [686, 724], "footprint": [679, 714, 14, 10]},
    {"name": "PantryBarrelA", "sprite": "barrel", "position": [1196, 772], "footprint": [1187, 760, 18, 12]},
    {"name": "PantryBarrelB", "sprite": "barrel", "position": [1218, 790], "footprint": [1209, 778, 18, 12]},
    {"name": "PantryCrate", "sprite": "crate", "position": [1190, 796], "footprint": [1179, 786, 22, 10]},
    {"name": "PantrySackA", "sprite": "sack", "position": [1236, 772], "footprint": [1229, 764, 14, 8]},
    {"name": "PantryCrateB", "sprite": "crate", "position": [1224, 640], "footprint": [1213, 630, 22, 10]},
    {"name": "PantrySackB", "sprite": "sack", "position": [1230, 662], "footprint": [1223, 654, 14, 8]},
    # Corridor
    {"name": "CorridorConsoleA", "sprite": "console_vase", "position": [347, 400], "footprint": [329, 388, 36, 10]},
    {"name": "CorridorConsoleB", "sprite": "console_tray", "position": [987, 400], "footprint": [969, 388, 36, 10]},
    {"name": "CorridorPlant", "sprite": "plant_stand", "position": [667, 400], "footprint": [659, 392, 16, 8]},
    # Hall
    {"name": "HallLamp", "sprite": "floor_lamp", "position": [752, 954], "footprint": [747, 946, 10, 8]},
]

# Floor-level textiles baked into the architecture (x, y, w, h, palette name).
RUGS = [(118 + 320 * i, 150, 72, 88, 0) for i in range(4)] + [(222, 598, 196, 148, "study"), (44, 728, 120, 60, "study"), (916, 700, 128, 64, "pantry")]
SHOES = [(32 + 320 * i + 96, 178) for i in range(4)]

# The gate sits in the south front wall and hangs down from the floor line
# at y=960, so everyone standing in the hall sorts in front of it.
GATE = {"position": [644, 960], "bolt": [640, 1007]}
STOP = {"now": [532, 952], "before": [628, 952]}

LIGHTS = (
    [{"at": [32 + 320 * i + 154, 90], "tint": "ffc98a", "energy": 0.85, "size": 220} for i in range(4)]
    + [{"at": [32 + 320 * i + 76, 104], "tint": "b9cde6", "energy": 0.55, "size": 190} for i in range(4)]
    + [{"at": [200, 566], "tint": "ffc98a", "energy": 0.9, "size": 280}, {"at": [438, 672], "tint": "ffc98a", "energy": 0.9, "size": 280},
       {"at": [1104, 668], "tint": "ffc98a", "energy": 0.9, "size": 280}, {"at": [1216, 540], "tint": "ff9a5a", "energy": 0.5, "size": 150},
       {"at": [347, 548], "tint": "b9cde6", "energy": 0.6, "size": 240}, {"at": [863, 548], "tint": "b9cde6", "energy": 0.6, "size": 240},
       {"at": [752, 918], "tint": "ffc98a", "energy": 0.85, "size": 240}, {"at": [644, 960], "tint": "c6b8d8", "energy": 0.4, "size": 200}]
)
