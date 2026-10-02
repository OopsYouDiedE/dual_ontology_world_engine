"""PNG structure checks with concrete fix instructions. Structure only, never artistic approval."""
from __future__ import annotations

import hashlib
from pathlib import Path

import numpy as np
from PIL import Image

import specs

MAX_PIXELS = 40_000_000


def resolve(path: str) -> Path:
    p = Path(path)
    if not p.is_absolute():
        p = specs.ROOT / p
    p = p.resolve()
    if specs.ROOT.resolve() not in p.parents:
        raise ValueError(f"path must be inside the repository: {path}")
    if p.suffix.lower() != ".png" or not p.is_file():
        raise ValueError(f"not an existing .png file: {path}")
    return p


def _rel(p: Path) -> str:
    return p.relative_to(specs.ROOT.resolve()).as_posix()


class Report:
    def __init__(self):
        self.items = []

    def check(self, name, ok, detail=None, fix=None, level="error"):
        item = {"check": name, "passed": bool(ok)}
        if detail is not None:
            item["detail"] = detail
        if not ok:
            item["level"] = level
            if fix:
                item["fix"] = fix
        self.items.append(item)
        return ok


def _opaque_colors(rgba: np.ndarray) -> np.ndarray:
    px = rgba[rgba[..., 3] > 0][:, :3]
    return np.unique(px, axis=0) if len(px) else np.zeros((0, 3), np.uint8)


def palette_distance(colors: np.ndarray) -> np.ndarray:
    pal = np.array(specs.palette_rgb(), dtype=np.int32)
    if not len(colors):
        return np.zeros(0)
    out = np.empty(len(colors))
    for i in range(0, len(colors), 4096):  # chunked: large maps have many colours
        c = colors[i:i + 4096].astype(np.int32)
        out[i:i + 4096] = np.sqrt(((c[:, None, :] - pal[None]) ** 2).sum(-1)).min(1)
    return out


def _alpha(r: Report, a: np.ndarray):
    partial = int(((a > 0) & (a < 255)).sum())
    r.check("binary_alpha", partial == 0, {"partial_alpha_pixels": partial},
            "把 0<alpha<255 的像素阈值化：alpha≥128→255，否则→0；不要用抗锯齿笔刷或非最近邻缩放。")


def _not_empty(r: Report, a: np.ndarray) -> bool:
    return r.check("not_empty", (a > 0).any(), None, "图像完全透明，确认导出了正确的图层。")


def _bbox(mask):
    ys, xs = np.where(mask)
    return [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())]


def check_character_front(r, rgba, spec):
    fw, fh = spec["frame"]
    h, w = rgba.shape[:2]
    if not r.check("frame_grid", [w, h] in spec["allowed_sizes"],
                   {"size": [w, h], "frame": [fw, fh], "allowed_sizes": spec["allowed_sizes"]},
                   f"画布只允许单帧 {fw}×{fh} 或 21 人图集 224×144（7 列×3 行），不能改变已有角色格的排列。"):
        return
    total = set()
    frames = []
    for row in range(h // fh):
        for col in range(w // fw):
            f = rgba[row * fh:(row + 1) * fh, col * fw:(col + 1) * fw]
            op = f[..., 3] > 0
            idx = row * (w // fw) + col
            if not r.check(f"frame{idx}_not_empty", op.any(), {"frame": idx, "row": row, "column": col},
                           f"第 {idx} 个角色格（行 {row}、列 {col}，从 0 计）为空；恢复对应角色，不能留空或移动其余角色格。"):
                frames.append({"frame": idx, "empty": True})
                continue
            bb = _bbox(op)
            cols = _opaque_colors(f)
            total |= {tuple(c) for c in cols}
            info = {"frame": idx, "bbox": bb, "foot_row": bb[3], "colors": len(cols)}
            frames.append(info)
            r.check(f"frame{idx}_foot_row", bb[3] == spec["foot_row"], info,
                    f"脚底最后一行须在第 {spec['foot_row']} 行（从 0 计），第 47 行全透明；整体上下平移 {spec['foot_row'] - bb[3]} px。")
            r.check(f"frame{idx}_colors", len(cols) <= spec["max_colors_per_frame"], info,
                    f"每人不透明颜色≤{spec['max_colors_per_frame']}；合并相近阴影色。")
            r.check(f"frame{idx}_inside", bb[0] > 0 and bb[2] < fw - 1 and bb[1] > 0, info,
                    "轮廓触到帧左/右/上边，会与相邻帧粘连；收窄或降低人物。", level="warning")
    r.check("total_colors", len(total) <= spec["max_colors_total"], {"colors": len(total)},
            f"整张图集≤{spec['max_colors_total']} 色；统一共用的皮肤/描边色。")
    return {"frames": frames}


def check_character_atlas(r, rgba, spec):
    h, w = rgba.shape[:2]
    if not r.check("size", [w, h] == spec["size"], {"size": [w, h]},
                   f"动作图集须为 {spec['size'][0]}×{spec['size'][1]}（8 列×24 行，每格 64×64，无格线）。"):
        return
    a = rgba[..., 3] > 0
    cw, ch = spec["cell"]
    rows = []
    for rr in range(24):
        anim = spec["anims"][rr % 8]
        want = spec["frames"][rr % 8]
        d = spec["directions"][rr // 8]
        feet, edge, wrong = set(), [], []
        heights = []
        for c in range(8):
            cell = a[rr * ch:(rr + 1) * ch, c * cw:(c + 1) * cw]
            if (c < want) != cell.any():
                wrong.append(c)
            if cell.any():
                bb = _bbox(cell)
                feet.add(bb[3])
                heights.append(bb[3] - bb[1] + 1)
                if bb[0] == 0 or bb[1] == 0 or bb[2] == cw - 1 or bb[3] == ch - 1:
                    edge.append(c)
        where = f"row{rr} {d}/{anim}"
        r.check(f"{where} frames", not wrong, {"expected_frames": want, "wrong_cells": wrong},
                f"第 {rr} 行（{d} {anim}）须在第 0..{want - 1} 列有帧，其余列全透明。")
        r.check(f"{where} foot", feet <= {spec['foot_y']}, {"foot_rows": sorted(feet)},
                f"每帧最后一个脚底像素须在 y={spec['foot_y']}（锚点 {tuple(spec['anchor'])}）；按差值整帧平移。",
                level="error" if anim == "idle" else "warning")  # 1px lift in walk/interact may be a deliberate bob
        r.check(f"{where} edge", not edge, {"cells": edge}, "轮廓触到 64×64 格边；缩小动作幅度或移回格中央。")
        if anim == "idle" and heights:
            hh = max(heights)
            r.check(f"{where} height", abs(hh - spec["figure_height"]) <= 3, {"height": hh},
                    f"idle 人形高度约 {spec['figure_height']}px（±3）；按方向 idle 统一缩放，最近邻。", level="warning")
        rows.append({"row": rr, "direction": d, "anim": anim, "foot_rows": sorted(feet)})
    return {"rows": rows}


def check_prop(r, rgba, spec):
    a = rgba[..., 3] > 0
    h, w = a.shape
    bb = _bbox(a)
    if spec.get("_wall_mounted"):
        r.check("wall_anchor", True, {"bbox": bb, "note": "壁挂件：不要求最后一行落地，场景须用显式位置而非脚底排序"})
    else:
        r.check("anchor_bottom", bb[3] == h - 1, {"bbox": bb, "height": h},
                f"最后一行须有不透明像素（底边中点是落地锚点）；裁掉底部 {h - 1 - bb[3]} 行透明。"
                "若是壁挂件，用 wall_mounted=true 重新校验。")
    r.check("even_width", w % 2 == 0, {"width": w},
            "宽度取偶数，底边中点才能落在整像素上（现有全部道具均为偶数宽）。", level="warning")
    cols = _opaque_colors(rgba)
    dist = palette_distance(cols)
    far = cols[dist > 16]
    px = rgba[a][:, :3]
    share = float(np.isin(px.view([("", px.dtype)] * 3), far.view([("", far.dtype)] * 3)).mean()) if len(far) else 0.0
    r.check("palette", share <= 0.05, {"colors": len(cols), "off_palette_colors": len(far),
                                       "off_palette_pixel_share": round(share, 3),
                                       "examples": ["#%02x%02x%02x" % tuple(c) for c in far[:8]]},
            "超过 5% 像素偏离 palette.py 色盘（RGB 距离>16）。从 art_brief 给出的 ramp 取色或用 shift() 派生；"
            "发光体（灯焰）可例外，但须在 brief 里写明。", level="warning")
    r.check("color_count", len(cols) <= 72, {"colors": len(cols)},
            "单件道具现有最多 67 色；超过 72 色通常是渐变或缩放残留，改用 ramp 分段着色。", level="warning")
    return {"bbox": bb, "colors": len(cols)}


def check_tile(r, rgba, spec):
    h, w = rgba.shape[:2]
    r.check("grid", w % 32 == 0 and h % 32 == 0, {"size": [w, h]}, "尺寸须为 32 的整数倍。")
    return {"cells": [w // 32, h // 32]}


def check_evidence(r, rgba, spec, size, mode):
    r.check("size", size == spec["size"], {"size": size},
            f"物证特写须保留现行 {spec['size'][0]}×{spec['size'][1]} 分辨率，从生产源重新原样导出。")
    r.check("mode", mode == spec["mode"], {"mode": mode, "expected": spec["mode"]},
            "物证特写须导出为 RGB PNG；先确认正确的可见内容与背景，再转换为 RGB，不要导出透明图层。")
    _not_empty(r, rgba[..., 3])
    return {}


def validate_png(path: str, kind: str, wall_mounted: bool = False) -> dict:
    if kind not in specs.KINDS:
        raise ValueError(f"kind must be one of {sorted(specs.KINDS)}")
    p = resolve(path)
    spec = dict(specs.KINDS[kind], _wall_mounted=wall_mounted)
    raw = p.read_bytes()
    with Image.open(p) as img:
        if img.size[0] * img.size[1] > MAX_PIXELS:
            raise ValueError(f"image too large: {img.size}")
        mode = img.mode
        rgba = np.array(img.convert("RGBA"))
        size = list(img.size)
    r = Report()
    extra = {}
    if kind == "evidence_closeup":
        extra = check_evidence(r, rgba, spec, size, mode)
    elif _not_empty(r, rgba[..., 3]):
        _alpha(r, rgba[..., 3])
        fn = {"character_front": check_character_front, "character_atlas": check_character_atlas,
              "prop": check_prop, "tile": check_tile, "ui_manuscript": lambda *_: {}}[kind]
        extra = fn(r, rgba, spec) or {}
    errors = [i for i in r.items if not i["passed"] and i["level"] == "error"]
    warns = [i for i in r.items if not i["passed"] and i["level"] == "warning"]
    return {"path": _rel(p), "kind": kind, "size": size, "mode": mode,
            "sha256": hashlib.sha256(raw).hexdigest(),
            "verdict": "fail" if errors else ("pass_with_warnings" if warns else "pass"),
            "failed": errors + warns, "passed_checks": sum(i["passed"] for i in r.items), "total_checks": len(r.items),
            "detail": extra,
            "limits": "结构检查，不评价造型、比例观感、动作连贯或风格一致；这些须在 4× 最近邻检视图与实机中确认。"}
