"""Turn a request into an explicit drawing order: canvas, anchor, colours, paths, forbidden content, acceptance."""
from __future__ import annotations

import json
import re

import specs

NAME = re.compile(r"^[a-z0-9][a-z0-9_]{0,47}$")
MANIFEST = specs.GODOT / "ShareAsset/Internal/Props/FirstAwakeningInterior/manifest.json"


def prop_sizes() -> dict:
    try:
        return {k: v["size"] for k, v in json.loads(MANIFEST.read_text("utf-8"))["sprites"].items()}
    except (OSError, KeyError, ValueError):
        return {}


def _ramps(names: list[str] | None) -> dict:
    all_r = specs.ramps()
    if not names:
        return {}
    bad = [n for n in names if n not in all_r]
    if bad:
        raise ValueError(f"unknown ramps {bad}; available: {sorted(all_r)}")
    return {n: all_r[n] for n in names}


def art_brief(kind: str, name: str, purpose: str, ramps: list[str] | None = None,
              size: list[int] | None = None, first_case_scene: bool = True,
              similar_to: str | None = None) -> dict:
    if kind not in specs.KINDS:
        raise ValueError(f"kind must be one of {sorted(specs.KINDS)}")
    if not NAME.match(name):
        raise ValueError(f"name must match {NAME.pattern}")
    if not purpose or len(purpose) > 400:
        raise ValueError("purpose: one or two sentences, what the asset is for and what the player must read from it")
    if size is not None and (not isinstance(size, list) or len(size) != 2
                             or any(type(n) is not int or n <= 0 for n in size)):
        raise ValueError("size must be exactly two positive integers: [width, height]")
    spec = specs.KINDS[kind]
    open_q = []
    steps: list[str] = []
    out = {"kind": kind, "name": name, "purpose": purpose, "spec": spec}

    if kind == "prop":
        sizes = prop_sizes()
        ref = sizes.get(similar_to) if similar_to else None
        if similar_to and ref is None:
            raise ValueError(f"similar_to '{similar_to}' not in manifest; known: {sorted(sizes)}")
        canvas = size if size is not None else ref
        if canvas is None:
            open_q.append("画布尺寸未定：给 size=[w,h] 或 similar_to=<现有道具名>。参考 existing_sizes。")
        else:
            if not isinstance(canvas, list) or len(canvas) != 2 or any(type(n) is not int or n <= 0 for n in canvas):
                raise ValueError("prop canvas must be exactly two positive integers: [width, height]")
            if canvas[0] % 2:
                raise ValueError("prop width must be even so the bottom-centre anchor is a whole pixel")
        use = _ramps(ramps)
        if not use:
            open_q.append("未指定材质 ramp：从 palette 选 1–3 个（木 WALNUT/OAK/HONEY，金属 BRASS/IRON，布 LINEN/CREAM）。")
        out.update(canvas=canvas, anchor="bottom-centre = 落地接触点", ramps=use,
                   existing_sizes={k: v for k, v in sizes.items() if not re.search(r"_\d$", k)},
                   target=f"godot_csharp/ShareAsset/Internal/Props/FirstAwakeningInterior/{name}.png")
        steps = [
            f"在 godot_csharp/tools/first_awakening_art/props.py 新增 def {name}() -> Canvas，画布 {canvas or '[w,h]'}，透明底。",
            "左上主光：顶面取 ramp 高段，正面中段，右侧与底部暗段；阴影用 shift(c,-x) 偏紫，高光用 shift(c,+x) 偏暖。",
            "只从指定 ramp 与 INK/VOID 取色；需要新色时在 palette.py 增加 ramp，并说明原因。",
            "最后一行必须是落地像素（腿脚或底座）；桌面上固定不动的物证直接画进家具本体。",
            "return _finish(c)（描边+二值 alpha），在 build_first_awakening_interior.py 注册输出并更新 manifest。",
            "运行构建入口，然后调用 validate_png(kind='prop') 与 4× 最近邻检视。",
        ]
    elif kind == "character_front":
        out.update(canvas=spec["frame"], foot_row=spec["foot_row"],
                   target="godot_csharp/ShareAsset/Internal/Characters/characters-21-front-master.png（替换对应格）")
        steps = [
            "32×48 透明帧；脚底最后一行在 y=46，y=47 全透明；人物左右至少留 1px。",
            "完整身份图集只用 224×144（7 列×3 行），21 个角色格均必须非空；单件交付用 32×48。",
            f"≤{spec['max_colors_per_frame']} 色/人，整图≤{spec['max_colors_total']} 色；用发型、体形、衣着色块、站姿区分人物。",
            "约 1900 年前后服装基础；默认空手站姿，不画职业图标道具。",
            "源稿放 AssetsSource/Internal/Characters/FrontStanding/，由 build_character_atlas.py 确定性整理。",
            "validate_png(kind='character_front')，再看 4× 预览。",
        ]
    elif kind == "character_atlas":
        layout = [{"rows": [d * 8 + i for d in range(3)], "anim": a, "frames": f,
                   "fps": spec["fps"]["walk"] if a == "walk" else spec["fps"]["other"],
                   "loop": a in spec["loop"]} for i, (a, f) in enumerate(zip(spec["anims"], spec["frames"]))]
        out.update(canvas=spec["size"], cell=spec["cell"], layout=layout, anchor=spec["anchor"],
                   target=f"godot_csharp/ShareAsset/Internal/Characters/animation_v2/{name}.png + {name}.tres")
        open_q.append(f"动作清单存在未裁定冲突（见 art_spec_catalog.conflicts）；本 brief 按运行时 8 动作 111 帧。")
        steps = [
            "每方向源稿按 8 列×5 行：行0 idle/talk/inspect，行1 walk，行2 interact/sit，行3 break，行4 cast。",
            f"源稿放 {spec['source_path']}；右向不画，由左向镜像。",
            f"导入：{spec['build']}；按各方向 idle 高度统一缩放到约 43px，最近邻，alpha≥192 取轮廓。",
            "每帧最后脚底像素 y=54，轮廓不触 64×64 格边，未用格全透明。",
            "validate_png(kind='character_atlas')，再在 Godot 以 AnimatedSprite2D 实播检查连贯性。",
        ]
    elif kind == "tile":
        steps = ["用 rpg-tilemap MCP：先 tilemap_workflow 与 native_art_catalog，再按 47-autotile 生产；本 MCP 只做结构校验。"]
    elif kind == "ui_manuscript":
        steps = ["用 rpg-tilemap MCP build_diegetic_ui 生成切片，inspect_godot_ui 实机验收；颜色见 spec.colors。"]
    elif kind == "evidence_closeup":
        out.update(canvas=spec["size"], mode=spec["mode"],
                   target=f"{spec['runtime_path']}{name}.png")
        steps = [
            "特写为 UI 检视图，非原生像素；须与场景中对应道具的形状、颜色、痕迹位置一致。",
            f"保留 {spec['size'][0]}×{spec['size'][1]} 生成分辨率，导出 RGB PNG，不使用透明图层。",
            "源图放 AssetsSource/Internal/Props/FirstAwakeningEvidence/；由 build_first_awakening_evidence.gd 原样发布。",
        ]
    forbidden = list(specs.EVIDENCE_RULES if kind == "evidence_closeup" else specs.GENERAL_RULES)
    if first_case_scene:
        forbidden = specs.FIRST_CASE_FORBIDDEN + forbidden
    out.update(steps=steps, must_not=forbidden, open_questions=open_q,
               acceptance=[f"validate_png(path, kind='{kind}') verdict 为 pass（warning 须逐条说明）",
                           "检视图人工确认形状与痕迹可读性" if kind == "evidence_closeup" else "4× 最近邻检视图人工确认可读性",
                           "实机场景截图确认尺度、遮挡、光向",
                           "结构通过不等于艺术验收"],
               ready=not open_q)
    return out
