"""Art specs as data, each value tied to the file it was read from.

The prose master is docs/design-standard.md Part 5 (and Part 4 §2 for actions). Where the
runtime README and the design doc disagree the conflict is listed, not silently resolved.
"""
from __future__ import annotations

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
GODOT = HERE.parents[1]
ROOT = GODOT.parent
sys.path.insert(0, str(GODOT / "tools"))

from first_awakening_art import palette  # noqa: E402

DOC = "docs/design-standard.md"
CHAR_README = "godot_csharp/ShareAsset/Internal/Characters/README-zh.md"
ANIM_README = "godot_csharp/ShareAsset/Internal/Characters/animation_v2/README.md"
PROP_README = "godot_csharp/ShareAsset/Internal/Props/FirstAwakeningInterior/README.md"
EVID_README = "godot_csharp/ShareAsset/Internal/Props/FirstAwakeningEvidence/README.md"
PALETTE_SRC = "godot_csharp/tools/first_awakening_art/palette.py"

ANIMS = ["idle", "walk", "talk", "inspect", "interact", "sit", "break", "cast"]
FRAMES = [1, 8, 3, 4, 4, 4, 6, 7]
DIRECTIONS = ["front", "back", "left"]  # rows 0-7, 8-15, 16-23; right mirrors left

UI_MANUSCRIPT = {"paper": "#E6DCBF", "ink": "#2B2016", "seal": "#8A3324"}


def ramps() -> dict[str, list[str]]:
    out = {}
    for key, value in vars(palette).items():
        if key.isupper() and isinstance(value, list) and value and isinstance(value[0], tuple):
            out[key] = ["#%02x%02x%02x" % c[:3] for c in value]
        elif key.isupper() and isinstance(value, tuple) and len(value) == 4:
            out[key] = ["#%02x%02x%02x" % value[:3]]
    return out


def palette_rgb() -> list[tuple[int, int, int]]:
    seen, out = set(), []
    for vals in ramps().values():
        for h in vals:
            c = tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))
            if c not in seen:
                seen.add(c)
                out.append(c)
    return out


KINDS = {
    "character_front": {
        "summary": "身份基准正面站立帧，32×48 单帧或 224×144 的 21 人图集（7 列×3 行）",
        "allowed_sizes": [[32, 48], [224, 144]], "empty_frames_allowed": False,
        "frame": [32, 48], "foot_row": 46, "transparent_rows": [47], "binary_alpha": True,
        "max_colors_per_frame": 16, "max_colors_total": 96,
        "runtime_path": "godot_csharp/ShareAsset/Internal/Characters/",
        "source_path": "godot_csharp/AssetsSource/Internal/Characters/FrontStanding/",
        "import": "Sprite2D scale 1, nearest filter; 脚底像素在居中原点下方 22px",
        "sources": [CHAR_README, f"{DOC} 第五部分 §1"],
    },
    "character_atlas": {
        "summary": "animation_v2 动作图集 8 列×24 行，64×64 承载格",
        "size": [512, 1536], "cell": [64, 64], "anims": ANIMS, "frames": FRAMES, "directions": DIRECTIONS,
        "anchor": [32, 55], "foot_y": 54, "sprite_offset": [0, -23], "figure_height": 43,
        "collision": {"size": [16, 12], "offset": [0, -6]}, "binary_alpha": True,
        "fps": {"walk": 10, "other": 7}, "loop": ["idle", "walk"],
        "runtime_path": "godot_csharp/ShareAsset/Internal/Characters/animation_v2/<actor>.png + .tres",
        "source_path": "godot_csharp/AssetsSource/Internal/Characters/DemoAnimation/<actor>-{front,back,left}.png",
        "build": "godot_csharp/tools/build_demo_animation_atlas.ps1 -Actor <actor>",
        "sources": [ANIM_README, f"{DOC} 第五部分 §1"],
    },
    "prop": {
        "summary": "原生 1x 家具/道具精灵，底边中点为落地锚点",
        "anchor": "bottom-centre; 最后一行必须有不透明像素", "binary_alpha": True,
        "palette": "first_awakening_art.palette 各 ramp 及其 shift() 派生色；RGB 距离≤16 视为在盘内",
        "light": "左上主光；阴影色相偏紫(0.72)，高光偏暖(0.13)",
        "recipe": "godot_csharp/tools/first_awakening_art/props.py 新增函数，经 _finish() 描边与硬化，"
                  "再在 build_first_awakening_interior.py 注册",
        "runtime_path": "godot_csharp/ShareAsset/Internal/Props/FirstAwakeningInterior/<name>.png (+ manifest.json)",
        "build": "godot_csharp/addons/rpg_asset_adapter/mcp/.venv/Scripts/python.exe godot_csharp/tools/build_first_awakening_interior.py",
        "collision": "由场景脚底占地决定，不用整张透明画布", "sources": [PROP_README, PALETTE_SRC],
    },
    "tile": {
        "summary": "地图材质 32×32 网格，47-autotile 布局",
        "cell": [32, 32], "layout": "47-autotile", "binary_alpha": True,
        "collision": "门洞、墙体、地面分层，碰撞只限实体接触区",
        "tool": "rpg-tilemap MCP (tilemap_workflow / native_art_catalog)", "sources": [f"{DOC} 第五部分 §3"],
    },
    "evidence_closeup": {
        "summary": "推理页物证特写（非原生像素图，UI 检视用）",
        "size": [1254, 1254], "mode": "RGB", "binary_alpha": False, "not_empty": True,
        "runtime_path": "godot_csharp/ShareAsset/Internal/Props/FirstAwakeningEvidence/",
        "sources": [EVID_README],
    },
    "ui_manuscript": {
        "summary": "手稿界面 9-patch 面板",
        "colors": UI_MANUSCRIPT, "nine_patch": True, "fonts": "仅 OFL 等可商用开源字体",
        "tool": "rpg-tilemap MCP build_diegetic_ui + inspect_godot_ui",
        "sources": [f"{DOC} 第四部分 §3"],
    },
}

CONFLICTS = [
    {"topic": "角色动作清单与帧数",
     "a": {"source": f"{DOC} 第四部分 §2",
           "value": "9 项：idle/walk/run/sit/operate/present/alert/focus/retreat；idle 4、walk 6、run 6、operate/focus 4 帧循环"},
     "b": {"source": ANIM_README,
           "value": "8 项：idle/walk/talk/inspect/interact/sit/break/cast；帧数 1/8/3/4/4/4/6/7"},
     "status": "未裁定。validate 按运行时 b 检查（现有 10 张图集均符合 b）；新动作美术开工前由用户裁定并同步设计文档。"},
]

# Content the first case must not show (user constraint 2026-09-29, design-standard 第二部分 §6.9).
FIRST_CASE_FORBIDDEN = [
    "任何长期居住痕迹：补丁、磨损的私人物品、杯碟碎片配对、值班表、名册、长期作息记录",
    "身体上的长期痕迹：愈合疤痕、头发长度变化",
    "四间宿舍之间的差异化个人陈设（宿舍共用同一套家具与材质，只有门牌不同）",
    "现代物件：耳机、悬挂试管；职业图标式常驻道具（长枪、棋盘、测量工具、厨刀随身）",
]

GENERAL_RULES = [
    "整数倍缩放、最近邻采样，禁止非整数缩放与抗锯齿半透明边",
    "alpha 只能为 0 或 255",
    "遮挡排序按脚底接触线 Y；精灵锚点必须落在接触线上",
    "生产源放 godot_csharp/AssetsSource/（.gdignore），运行时资产放 godot_csharp/ShareAsset/",
    "image_gen 源稿只作来源，运行时须经确定性导入/缩制并记录；不能把概念图标成像素风直接使用",
    "检查通过只证明结构，不等于艺术验收",
]

# Evidence closeups preserve their generated resolution and are displayed as UI
# images; the native pixel-art scale, alpha and ground-anchor rules do not apply.
EVIDENCE_RULES = [
    "物证特写为 1254×1254 RGB PNG，不能导出为空透明图层",
    "仅用于 UI 检视与答辩物证，场景物件仍使用原生像素资产",
    "生产源放 godot_csharp/AssetsSource/，运行时由既有入口原样复制到 godot_csharp/ShareAsset/",
    "检查通过只证明结构，不等于艺术验收",
]
