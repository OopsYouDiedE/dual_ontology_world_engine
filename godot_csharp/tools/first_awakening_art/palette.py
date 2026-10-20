"""One hand-tuned palette for the whole life wing.

Value plan: pale limed floors carry the dark-clad cast; walnut furniture sits
in the middle values; dormitory walls share the same saturated accent; caps
and void are the darkest frame. Warm interior light answers cool fog glass.
"""
from .pixel import hexc


def r(*values: str):
    return [hexc(v) for v in values]


INK = hexc("#1b1420")
VOID = hexc("#0e0b12")

CAP = r("#171119", "#2a2029", "#342832", "#44353f", "#584753")

OAK = r("#5f4c42", "#7c6757", "#96816b", "#a9947b", "#baa68b", "#cbb99c", "#dbcdb2")
HONEY = r("#4a3527", "#634836", "#7a5c45", "#8e6d53", "#a07f63", "#b29276", "#c3a78c")
SAGE_TILE = r("#3c4a45", "#4d5d57", "#607169", "#72847a", "#879889")
PLANK = r("#3a2926", "#51392f", "#654739", "#795843", "#8e6a50", "#a37f62")
PARQUET = r("#2f201d", "#452c25", "#5a3a2e", "#6f4a38", "#855c45", "#9c7054")
WALNUT = r("#20141a", "#341f20", "#4b2c28", "#643c31", "#7f4f3d", "#9c6649", "#b98260")
BRASS = r("#44291a", "#6f4b23", "#9d7031", "#c89a45", "#e8c56d", "#f8e6a6")
IRON = r("#181b22", "#272c35", "#3a414c", "#545d69", "#77818c", "#a2aab2")
STONE = r("#34312f", "#494441", "#5f5954", "#77706a", "#8f8880", "#a9a298", "#c3bcb0")
LINEN = r("#6f6a62", "#8f887b", "#afa692", "#cac1aa", "#e0d8c1", "#f2ecd9")
CREAM = r("#7e7262", "#9c8f7a", "#b8ab93", "#cfc3a9", "#e1d7bf", "#efe7d3")
FOG = r("#39435c", "#53607c", "#7888a4", "#a1b1c7", "#c8d4e1", "#e5ecf2")
FLAME = r("#8a3c16", "#d57a28", "#fbb84e", "#ffe29a", "#fffbe8")
LEAF = r("#122017", "#1c3322", "#29492d", "#3b633a", "#557f48", "#7ca25f")
TILE = r("#7f8278", "#9fa196", "#bdbdb0", "#d3d2c4", "#e6e4d7", "#f3f1e6")
# Pale unglazed limestone quarry tile for the service rooms.
QUARRY = r("#6a6257", "#82796b", "#9b917f", "#b0a691", "#c1b7a1", "#cfc6b1", "#dcd4c1")
TILE_GREEN = r("#1d2f2a", "#284039", "#35544a", "#476b5e", "#5f8676")
SLATE = r("#262b31", "#343b42", "#465057", "#5b6769", "#73817f")
CLARET = r("#2a0f17", "#431822", "#60232e", "#80323b", "#a1464b", "#be6660")
OCHRE = r("#4a3113", "#6f4d1c", "#977029", "#bf933d", "#dcb65e", "#efd690")
CHALK = r("#1c2522", "#27332f", "#34433d", "#e3e0d2")
GRASS = r("#0f1a17", "#15241e", "#1d3027", "#273d30", "#344c3a")

# Room materials: public rooms may vary; dormitories share one finish.
TEAL = r("#13262b", "#1c353b", "#28494e", "#386163", "#517f7c", "#76a39a")
SAGE = r("#222c24", "#313f33", "#435645", "#5a6f59", "#778c72", "#9db092")
ROSE = r("#371d22", "#522d2f", "#6d403f", "#8a5750", "#a8746a", "#c69885")
PLUM = r("#1f1828", "#302539", "#43344f", "#594767", "#745f80", "#978099")
BOTTLE = r("#0f1d16", "#172a20", "#21392c", "#2e4b3a", "#406350", "#5d826b")

# Dormitories have the same institutional finish; only door numbers differ.
DORMS = [
    {"name": f"R-{5 + i:02}", "paper": TEAL, "accent": OCHRE, "rug": TEAL, "quilt": TEAL, "motif": "pinstripe"}
    for i in range(4)
]
