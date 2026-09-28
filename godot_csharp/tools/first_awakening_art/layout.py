"""Authoritative 32px life-wing layout.

Marks: '#' wall cap, 'W'/'w' upper/lower wall face, 'D' walkable doorway,
'.' floor, 'G'/'g' the hall's south gate wall, ' ' outside. Two stone
flights descend from the public rooms into the hall. The isolation gate stands
in the hall's south wall toward the main building; like the classic RPG front
wall cutaway, that wall is drawn as an elevation the player faces.
"""

CELL = 32
ROWS = [
    "########################################",
    "#WWWWWWWW##WWWWWWWW##WWWWWWWW##WWWWWWWW#",
    "#wwwwwwww##wwwwwwww##wwwwwwww##wwwwwwww#",
    "#........##........##........##........#",
    "#........##........##........##........#",
    "#........##........##........##........#",
    "#........##........##........##........#",
    "#........##........##........##........#",
    "#........##........##........##........#",
    "#####DD########DD########DD########DD###",
    "#WWWWDDWWWWWWWWDDWWWWWWWWDDWWWWWWWWDDWW#",
    "#wwwwDDwwwwwwwwDDwwwwwwwwDDwwwwwwwwDDww#",
    "#......................................#",
    "#......................................#",
    "#######DD######################DD#######",
    "#WWWWWWDDWWWWWWWWWW##WWWWWWWWWWDDWWWWWW#",
    "#wwwwwwDDwwwwwwwwww##wwwwwwwwwwDDwwwwww#",
    "#..................##..................#",
    "#..................##..................#",
    "#..................##..................#",
    "#..................##..................#",
    "#..................##..................#",
    "#..................##..................#",
    "#..................##..................#",
    "#..................##..................#",
    "################DD####DD################",
    "#WWWWWWWWWWWWWW#DDWWWWDD#WWWWWWWWWWWWWW#",
    "#wwwwwwwwwwwwww#DDwwwwDD#wwwwwwwwwwwwww#",
    "               #........#               ",
    "               #........#               ",
    "               #GGGGGGGG#               ",
    "               #gggggggg#               ",
]
W = len(ROWS[0])
H = len(ROWS)
assert all(len(row) == W for row in ROWS)

DORM_X = [1, 11, 21, 31]  # first floor column of each dormitory


def mark(x: int, y: int) -> str:
    if 0 <= x < W and 0 <= y < H:
        return ROWS[y][x]
    return " "


def walkable(x: int, y: int) -> bool:
    return mark(x, y) in ".D"


def region(x: int, y: int) -> str:
    """Which room's material a cell (floor or face) belongs to."""
    if y <= 9:
        for i, fx in enumerate(DORM_X):
            if fx <= x < fx + 8:
                return f"dorm{i}"
        return "cap"
    if y <= 13:
        return "corridor"
    if y <= 25:
        return "study" if x < 19 else "prep"
    if 15 < x < 24:
        return "hall"
    return "exterior"
