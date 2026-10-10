"""生成界面主题：scenes/UI/Styles/UITheme.tres、DialogueTheme.tres、TitleTheme.tres。

设计规范第六部分 §4：现场类界面用 19 世纪末报纸版式，翻阅类界面用近代博物书对开；
语义色板、五级字号（标题页书名与通栏问题另可用 64）、正文朝华打字机、标题思源宋体特粗、笔迹 Yozai。
颜色、字号与字体只在本文件顶部定义一次，主题条目一律引用这些名字；改外观先改这里再重新生成。

    python godot_csharp/tools/build_ui_theme.py          # 写出三份主题
    python godot_csharp/tools/build_ui_theme.py --check  # 只比对，不一致时返回 1
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_ui_desk as desk  # noqa: E402  标题页的灯脚、火焰高度与投影缩短，与桌面图同一套数

STYLES = Path(__file__).resolve().parents[1] / "scenes" / "UI" / "Styles"

# ---------------------------------------------------------------- 语义色 ---
PALETTE = {
    "paper": "EAE0C4",
    "paper_dark": "E1D5B5",
    "ink": "221C15",
    "muted": "5A4C3B",
    "rule": "7E6F58",
    "hand": "2F3B57",
    "seal": "8A3324",
    "match": "3F5A3A",
}

# ---------------------------------------------------------------- 字号 ---
SIZE = {"note": 14, "body": 18, "sub": 22, "title": 32, "head": 48, "display": 64}

FONT_FILES = {
    "zhaohua": ("res://ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf", "uid://1mrmumh7kkwu"),
    "noto": ("res://ShareAsset/External/NotoSerifSC/NotoSerifSC[wght].ttf", "uid://wv7t0k0kjb08"),
    "sans": ("res://ShareAsset/External/NotoSansSC/NotoSansSC[wght].ttf", "uid://b2crko4arpitv"),
    "qingke": ("res://ShareAsset/External/ZCOOLQingKeHuangYou/ZCOOLQingKeHuangYou-Regular.ttf", "uid://wxkrrvnps5hn"),
    "zhimang": ("res://ShareAsset/External/zhimangxing/ZhiMangXing-Regular.ttf", "uid://cgux3m6rt3nkc"),
    "yozai": ("res://ShareAsset/External/Yozai/Yozai-Regular.ttf", "uid://b305mue3maiaf"),
    "symbols": ("res://ShareAsset/External/MaterialSymbols/MaterialSymbolsOutlined[FILL,GRAD,opsz,wght].ttf", "uid://cqoykvtr28he2"),
    # 日文字形回退（汉字字体缺假名时按字取用）；整套按语言换字由 UI/Components/UIAppearance.gd 负责
    "yuji": ("res://ShareAsset/External/yujisyuku/YujiSyuku-Regular.ttf", "uid://bp2hcr87qwy24"),
    "kurenaido": ("res://ShareAsset/External/ZenKurenaido/ZenKurenaido-Regular.ttf", "uid://efwuolvoxfi0"),
    "tegomin": ("res://ShareAsset/External/newtegomin/NewTegomin-Regular.ttf", "uid://5taqf2dxpeah"),
}


def tag(name: str) -> int:
    return (ord(name[0]) << 24) | (ord(name[1]) << 16) | (ord(name[2]) << 8) | ord(name[3])


def c(name: str, alpha: float = 1.0) -> str:
    hexv = PALETTE[name]
    r, g, b = (int(hexv[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
    return f"Color({r:.6g}, {g:.6g}, {b:.6g}, {alpha:.6g})"


def hexc(hexv: str, alpha: float = 1.0) -> str:
    """色板以外的物质色（册子封板、书页侧边）；只用于册子的实物部分，不作语义色。"""
    r, g, b = (int(hexv[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
    return f"Color({r:.6g}, {g:.6g}, {b:.6g}, {alpha:.6g})"


class Theme:
    def __init__(self, uid: str):
        self.uid = uid
        self.ext: list[tuple[str, str, str, str]] = []
        self.subs: list[tuple[str, str, list[tuple[str, str]]]] = []
        self.entries: list[tuple[str, str]] = []
        self.header: list[tuple[str, str]] = []

    def font_file(self, key: str) -> str:
        path, uid = FONT_FILES[key]
        rid = f"font_{key}"
        if all(e[3] != rid for e in self.ext):
            self.ext.append(("FontFile", uid, path, rid))
        return f'ExtResource("{rid}")'

    def texture_file(self, rid: str, path: str, uid: str) -> str:
        if all(e[3] != rid for e in self.ext):
            self.ext.append(("Texture2D", uid, path, rid))
        return f'ExtResource("{rid}")'

    def paper(self, rid: str, margin=(0, 0, 0, 0)) -> str:
        """像素纸纹平铺的纸面样式盒（tools/build_ui_paper.py 生成的纹理）。"""
        tex = self.texture_file("paper_texture", "res://ShareAsset/Internal/Props/UIPaper/paper_pixel.png", "uid://di8b1hqq18tsx")
        props = [(f"content_margin_{side}", f"{float(v)}") for side, v in zip(("left", "top", "right", "bottom"), margin) if v]
        props += [("texture", tex), ("axis_stretch_horizontal", "1"), ("axis_stretch_vertical", "1")]
        return self.sub("StyleBoxTexture", rid, props)

    def ruled(self, rid: str, margin=(0, 0, 0, 0)) -> str:
        """册子横格页：同一纸纹加每 32 px 一道淡横格（tools/build_ui_paper.py 生成）。"""
        tex = self.texture_file("ruled_texture", "res://ShareAsset/Internal/Props/UIPaper/paper_ruled.png", "uid://ylmi47d8oubq")
        props = [(f"content_margin_{side}", f"{float(v)}") for side, v in zip(("left", "top", "right", "bottom"), margin) if v]
        props += [("texture", tex), ("axis_stretch_horizontal", "1"), ("axis_stretch_vertical", "1")]
        return self.sub("StyleBoxTexture", rid, props)

    def sub(self, kind: str, rid: str, props: list[tuple[str, str]]) -> str:
        if all(s[1] != rid for s in self.subs):
            self.subs.append((kind, rid, props))
        return f'SubResource("{rid}")'

    def box(self, rid: str, *, bg: str | None = None, border: str | None = None, width=(0, 0, 0, 0),
            margin=(0, 0, 0, 0), expand=(0, 0, 0, 0), shadow: str | None = None, shadow_size: int = 0,
            shadow_offset: tuple[int, int] | None = None, radius=(0, 0, 0, 0)) -> str:
        props: list[tuple[str, str]] = []
        for side, value in zip(("left", "top", "right", "bottom"), margin):
            if value: props.append((f"content_margin_{side}", f"{float(value)}"))
        if bg is None:
            props.append(("draw_center", "false"))
        else:
            props.append(("bg_color", bg))
        for side, value in zip(("left", "top", "right", "bottom"), width):
            if value: props.append((f"border_width_{side}", str(value)))
        if border is not None and any(width): props.append(("border_color", border))
        for side, value in zip(("left", "top", "right", "bottom"), expand):
            if value: props.append((f"expand_margin_{side}", f"{float(value)}"))
        for corner, value in zip(("top_left", "top_right", "bottom_right", "bottom_left"), radius):
            if value: props.append((f"corner_radius_{corner}", str(value)))
        if shadow is not None:
            props.append(("shadow_color", shadow))
            props.append(("shadow_size", str(shadow_size)))
            if shadow_offset: props.append(("shadow_offset", f"Vector2({shadow_offset[0]}, {shadow_offset[1]})"))
        return self.sub("StyleBoxFlat", rid, props)

    def line(self, rid: str, color: str, vertical: bool, thickness: int = 1) -> str:
        return self.sub("StyleBoxLine", rid, [("color", color), ("thickness", str(thickness)), ("vertical", "true" if vertical else "false")])

    def empty(self, rid: str = "empty") -> str:
        return self.sub("StyleBoxEmpty", rid, [])

    def set(self, key: str, value) -> None:
        self.entries.append((key, str(value)))

    def type(self, name: str, base: str) -> None:
        self.set(f"{name}/base_type", f'&"{base}"')

    def render(self) -> str:
        load_steps = len(self.ext) + len(self.subs) + 1
        out = [f'[gd_resource type="Theme" load_steps={load_steps} format=3 uid="{self.uid}"]', ""]
        for kind, uid, path, rid in self.ext:
            out.append(f'[ext_resource type="{kind}" uid="{uid}" path="{path}" id="{rid}"]')
        if self.ext: out.append("")
        for kind, rid, props in self.subs:
            out.append(f'[sub_resource type="{kind}" id="{rid}"]')
            out.extend(f"{k} = {v}" for k, v in props)
            out.append("")
        out.append("[resource]")
        out.extend(f"{k} = {v}" for k, v in self.header)
        out.extend(f"{k} = {v}" for k, v in sorted(self.entries, key=lambda e: e[0]))
        out.append("")
        return "\n".join(out)


def fonts(t: Theme) -> dict[str, str]:
    """字体角色（设计规范第六部分 §4.5，中文组合 6）：
    body 正文朝华打字机；register 登记（朝华加字距）；head 黑体主标题（思源黑体特粗）；deck 粗宋副题（思源宋体半粗）；
    mast 行书报头（志莽行书，略加粗，便于半分辨率渲染不断笔）；column 美术字栏题（庆科黄油体）；hand 笔迹（悠哉）；icon 图标。
    正文类字体回退到图标字体与思源宋体，图标码位可直接混排，罕用字有宋体兜底。"""
    zhaohua, noto, sans, yozai = t.font_file("zhaohua"), t.font_file("noto"), t.font_file("sans"), t.font_file("yozai")
    qingke, zhimang, symbols = t.font_file("qingke"), t.font_file("zhimang"), t.font_file("symbols")
    yuji, kurenaido, tegomin = t.font_file("yuji"), t.font_file("kurenaido"), t.font_file("tegomin")
    system = t.sub("SystemFont", "system_fallback", [
        ("font_names", 'PackedStringArray("Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC")')])
    axes = lambda fill: "{ %d: %d, %d: %d, %d: 0, %d: 24 }" % (tag("wght"), 300, tag("FILL"), fill, tag("GRAD"), tag("opsz"))
    icon = t.sub("FontVariation", "font_icon", [("base_font", symbols), ("variation_opentype", axes(0))])
    icon_filled = t.sub("FontVariation", "font_icon_filled", [("base_font", symbols), ("variation_opentype", axes(1))])
    serif = t.sub("FontVariation", "font_serif", [("base_font", noto), ("variation_opentype", "{ %d: 400 }" % tag("wght"))])
    fallbacks = f"Array[Font]([{icon}, {tegomin}, {serif}, {system}])"
    body = t.sub("FontVariation", "font_body", [("fallbacks", fallbacks), ("base_font", zhaohua)])
    register = t.sub("FontVariation", "font_register", [("fallbacks", fallbacks), ("base_font", zhaohua), ("spacing_glyph", "2")])
    head = t.sub("FontVariation", "font_head", [("fallbacks", f"Array[Font]([{icon}, {system}])"), ("base_font", sans),
                                                ("variation_opentype", "{ %d: 900 }" % tag("wght")), ("spacing_glyph", "1")])
    deck = t.sub("FontVariation", "font_deck", [("fallbacks", f"Array[Font]([{icon}, {system}])"), ("base_font", noto),
                                                ("variation_opentype", "{ %d: 600 }" % tag("wght")), ("spacing_glyph", "1")])
    mast = t.sub("FontVariation", "font_mast", [("fallbacks", f"Array[Font]([{yuji}, {head}])"), ("base_font", zhimang),
                                                ("variation_embolden", "0.35"), ("spacing_glyph", "6")])
    column = t.sub("FontVariation", "font_column", [("fallbacks", f"Array[Font]([{head}])"), ("base_font", qingke), ("spacing_glyph", "4")])
    hand = t.sub("FontVariation", "font_hand", [("fallbacks", f"Array[Font]([{kurenaido}, {body}])"), ("base_font", yozai)])
    return {"body": body, "register": register, "head": head, "deck": deck, "mast": mast, "column": column, "hand": hand,
            "icon": icon, "icon_filled": icon_filled}


def pixel_types(t: Theme, f: dict[str, str]) -> None:
    """像素字类型（Label 型，由 PaperPixelText 读取）：字号为屏幕尺寸，pixel_scale 为渲染倍率。"""
    for name, font, size in (("PixelDisplay", f["mast"], SIZE["display"]), ("PixelMast", f["mast"], 40),
                             ("PixelHeadline", f["head"], SIZE["head"]), ("PixelTitle", f["head"], SIZE["title"]),
                             ("PixelSub", f["head"], SIZE["sub"]), ("PixelColumn", f["column"], SIZE["sub"])):
        text_type(t, name, "Label", size=size, color=c("ink"), font=font, rich=False)
        t.set(f"{name}/constants/pixel_scale", 2)


def text_type(t: Theme, name: str, base: str, *, size: int, color: str, font: str | None = None, rich: bool = True,
              outline: tuple[str, int] | None = None, bold: str | None = None, style: str | None = None) -> None:
    t.type(name, base)
    if rich:
        t.set(f"{name}/colors/default_color", color)
        t.set(f"{name}/font_sizes/normal_font_size", size)
        t.set(f"{name}/font_sizes/bold_font_size", size)
        if font: t.set(f"{name}/fonts/normal_font", font)
        if bold: t.set(f"{name}/fonts/bold_font", bold)
        if outline:
            t.set(f"{name}/colors/font_outline_color", outline[0])
            t.set(f"{name}/constants/outline_size", outline[1])
        if style: t.set(f"{name}/styles/normal", style)
    else:
        t.set(f"{name}/colors/font_color", color)
        t.set(f"{name}/font_sizes/font_size", size)
        if font: t.set(f"{name}/fonts/font", font)
        if outline:
            t.set(f"{name}/colors/font_outline_color", outline[0])
            t.set(f"{name}/constants/outline_size", outline[1])
        if style: t.set(f"{name}/styles/normal", style)


def button_type(t: Theme, name: str, base: str, *, normal: str, hover: str, pressed: str, disabled: str, focus: str,
                color: str, disabled_color: str, size: int | None = None, font: str | None = None,
                hover_pressed: str | None = None) -> None:
    if name != "Button": t.type(name, base)
    for state, box in (("normal", normal), ("hover", hover), ("pressed", pressed), ("disabled", disabled), ("focus", focus)):
        t.set(f"{name}/styles/{state}", box)
    t.set(f"{name}/styles/hover_pressed", hover_pressed or pressed)
    for state in ("font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color"):
        t.set(f"{name}/colors/{state}", color)
    t.set(f"{name}/colors/font_disabled_color", disabled_color)
    if size: t.set(f"{name}/font_sizes/font_size", size)
    if font: t.set(f"{name}/fonts/font", font)


def paper_basics(t: Theme, f: dict[str, str], ink: str) -> str:
    empty = t.empty("transparent_text")
    t.set("Label/colors/font_color", ink)
    t.set("Label/styles/normal", empty)
    t.set("RichTextLabel/colors/default_color", ink)
    t.set("RichTextLabel/fonts/bold_font", f["deck"])
    t.set("RichTextLabel/font_sizes/normal_font_size", SIZE["body"])
    t.set("RichTextLabel/font_sizes/bold_font_size", SIZE["body"])
    t.set("RichTextLabel/styles/normal", empty)
    t.set("RichTextLabel/styles/focus", empty)
    t.set("Paper/colors/rule", c("rule", 0.7))
    t.set("Paper/colors/rule_strong", c("ink"))
    t.set("VScrollBar/styles/grabber", t.box("scroll_grabber", bg=c("ink", 0.35)))
    t.set("VScrollBar/styles/grabber_highlight", t.box("scroll_grabber_hi", bg=c("ink", 0.55)))
    t.set("VScrollBar/styles/grabber_pressed", t.box("scroll_grabber_hi", bg=c("ink", 0.55)))
    t.set("VScrollBar/styles/scroll", t.box("scroll_track", bg=c("paper_dark", 0.8), margin=(3, 0, 3, 0)))
    return empty


def secondary_primary(t: Theme, f: dict[str, str]) -> None:
    """无框按钮（设计规范第六部分 §4.6）：次操作为淡下划线文字；主操作为粗下划线加副题字。"""
    m = (8, 6, 8, 6)
    focus = t.box("btn_focus", border=c("hand", 0.9), width=(0, 0, 0, 2), expand=(0, 0, 0, 2))
    button_type(t, "Button", "",
                normal=t.box("btn_normal", border=c("rule", 0.6), width=(0, 0, 0, 1), margin=m),
                hover=t.box("btn_hover", bg=c("paper_dark", 0.6), border=c("ink"), width=(0, 0, 0, 1), margin=m),
                pressed=t.box("btn_pressed", bg=c("paper_dark", 0.8), border=c("ink"), width=(0, 0, 0, 1), margin=m),
                disabled=t.box("btn_disabled", border=c("rule", 0.25), width=(0, 0, 0, 1), margin=m),
                focus=focus, color=c("ink"), disabled_color=c("muted", 0.5), size=SIZE["body"], font=f["body"])
    button_type(t, "PaperAction", "Button",
                normal=t.box("act_normal", border=c("ink"), width=(0, 0, 0, 3), margin=m),
                hover=t.box("act_hover", bg=c("paper_dark", 0.6), border=c("ink"), width=(0, 0, 0, 3), margin=m),
                pressed=t.box("act_pressed", bg=c("paper_dark", 0.8), border=c("ink"), width=(0, 0, 0, 3), margin=m),
                disabled=t.box("act_disabled", border=c("rule", 0.3), width=(0, 0, 0, 3), margin=m),
                focus=focus, color=c("ink"), disabled_color=c("muted", 0.5), size=SIZE["body"] + 2, font=f["deck"])


def book_types(t: Theme, f: dict[str, str], empty: str) -> None:
    """册子（设计规范第六部分 §4.7）：整套界面是所控角色的案卷册子。对开横格页、书口索引签、
    凡例行（印好的选项，所选者以笔迹圈出）、刻度尺（音量等）、键帽与书签贴片。"""
    t.set("PaperBook/colors/board", hexc("1B1916"))
    t.set("PaperBook/colors/stack_light", hexc("D8CCAE"))
    t.set("PaperBook/colors/stack_dark", hexc("C4B48F"))
    t.set("PaperBook/colors/gutter", c("ink", 0.2))
    t.set("PaperBook/colors/margin_line", c("muted", 0.35))
    t.set("PaperBook/colors/dim", c("ink", 0.62))
    for key, value in (("width", 1168), ("height", 912), ("viewport_margin", 16), ("edge", 12), ("stack", 8),
                       ("gutter_step", 8), ("gutter_steps", 4), ("margin_x", 72), ("tab_width", 56), ("tab_height", 72),
                       ("tab_top", 72), ("section_gap", 24), ("label_width", 144)):
        t.set(f"PaperBook/constants/{key}", value)
    t.type("PaperBookPageLeft", "PanelContainer")
    t.set("PaperBookPageLeft/styles/panel", t.ruled("book_page_left", margin=(96, 48, 40, 40)))
    t.type("PaperBookPageRight", "PanelContainer")
    t.set("PaperBookPageRight/styles/panel", t.ruled("book_page_right", margin=(40, 48, 96, 40)))
    t.type("PaperBookColumn", "VBoxContainer")
    t.set("PaperBookColumn/constants/separation", 16)
    t.type("PaperBookContent", "VBoxContainer")
    t.set("PaperBookContent/constants/separation", 0)
    t.type("PaperBookFooter", "HBoxContainer")
    t.set("PaperBookFooter/constants/separation", 32)
    t.type("PaperBookTabs", "VBoxContainer")
    t.set("PaperBookTabs/constants/separation", 6)
    tm = (8, 12, 8, 12)
    tab_shadow = c("ink", 0.35)
    button_type(t, "PaperBookTab", "Button",
                normal=t.box("book_tab", bg=c("paper_dark"), margin=tm, radius=(0, 6, 6, 0), shadow=tab_shadow, shadow_size=1, shadow_offset=(2, 2)),
                hover=t.box("book_tab_hover", bg=c("paper"), margin=tm, radius=(0, 6, 6, 0), shadow=tab_shadow, shadow_size=1, shadow_offset=(2, 2)),
                pressed=t.box("book_tab_current", bg=c("paper"), margin=(16, 12, 8, 12), radius=(0, 6, 6, 0), shadow=tab_shadow, shadow_size=1, shadow_offset=(2, 2)),
                disabled=t.box("book_tab_off", bg=c("paper_dark"), margin=tm, radius=(0, 6, 6, 0)),
                focus=t.box("book_tab_focus", border=c("hand", 0.9), width=(0, 0, 0, 2), radius=(0, 6, 6, 0)),
                color=c("ink"), disabled_color=c("muted", 0.5), size=SIZE["body"], font=f["body"])
    # 凡例：分节标题、行（下接淡规线）、行名、本人笔迹
    text_type(t, "BookSection", "Label", size=SIZE["sub"], color=c("ink"), font=f["head"], rich=False)
    t.type("BookRow", "PanelContainer")
    t.set("BookRow/styles/panel", t.box("book_row", border=c("rule", 0.45), width=(0, 0, 0, 1), margin=(0, 6, 0, 6)))
    t.type("BookRowLine", "HBoxContainer")
    t.set("BookRowLine/constants/separation", 16)
    text_type(t, "BookRowLabel", "Label", size=SIZE["body"], color=c("muted"), font=f["register"], rich=False)
    text_type(t, "BookNote", "Label", size=SIZE["body"], color=c("hand"), font=f["hand"], rich=False)
    # 圈选项：印好的选项文字，所选者画笔迹椭圆；不可用者淡墨删除线
    om = (8, 4, 8, 4)
    button_type(t, "PaperOption", "Button",
                normal=t.box("option_plain", margin=om),
                hover=t.box("option_hover", border=c("rule", 0.8), width=(0, 0, 0, 1), margin=om),
                pressed=t.box("option_plain", margin=om),
                hover_pressed=t.box("option_plain", margin=om),
                disabled=t.box("option_plain", margin=om),
                focus=t.box("option_focus", border=c("hand", 0.9), width=(0, 0, 0, 2)),
                color=c("ink"), disabled_color=c("muted", 0.5), size=SIZE["body"], font=f["body"])
    t.set("PaperOption/colors/mark", c("hand"))
    t.set("PaperOption/colors/strike", c("muted", 0.8))
    t.set("PaperOption/constants/mark_width", 2)
    t.type("PaperOptionRow", "HBoxContainer")
    t.set("PaperOptionRow/constants/separation", 8)
    # 刻度尺：一道墨线、长短相间的刻度，所选位置一个笔迹勾，右侧笔迹读数
    t.set("PaperScale/colors/line", c("ink"))
    t.set("PaperScale/colors/mark", c("hand"))
    t.set("PaperScale/colors/focus", c("hand", 0.5))
    t.set("PaperScale/fonts/mark", f["hand"])
    t.set("PaperScale/font_sizes/mark", 26)
    t.set("PaperScale/font_sizes/value", SIZE["body"])
    for key, value in (("width", 224), ("height", 32), ("intervals", 4), ("major_tick", 12), ("minor_tick", 6),
                       ("value_gap", 16), ("value_width", 56)):
        t.set(f"PaperScale/constants/{key}", value)
    # 键帽：印好的键名，淡下划线；改键时被笔迹圈出
    km = (8, 4, 8, 4)
    button_type(t, "PaperKeyCap", "Button",
                normal=t.box("keycap", border=c("rule", 0.6), width=(0, 0, 0, 1), margin=km),
                hover=t.box("keycap_hover", bg=c("paper_dark", 0.6), border=c("ink"), width=(0, 0, 0, 1), margin=km),
                pressed=t.box("keycap", border=c("rule", 0.6), width=(0, 0, 0, 1), margin=km),
                disabled=t.box("keycap_off", margin=km),
                focus=t.box("option_focus", border=c("hand", 0.9), width=(0, 0, 0, 2)),
                color=c("ink"), disabled_color=c("muted", 0.5), size=SIZE["body"], font=f["body"])
    t.set("PaperKeyCap/colors/mark", c("hand"))
    t.type("PaperKeyRow", "HBoxContainer")
    t.set("PaperKeyRow/constants/separation", 12)
    # 书签：每条存档是一张贴在页上的纸片；空位只有一圈淡线
    t.type("BookmarkSlot", "PanelContainer")
    t.set("BookmarkSlot/styles/panel", t.paper("bookmark_slot", margin=(12, 12, 12, 12)))
    t.type("BookmarkSlotEmpty", "PanelContainer")
    t.set("BookmarkSlotEmpty/styles/panel", t.box("bookmark_empty", border=c("rule", 0.5), width=(1, 1, 1, 1), margin=(12, 12, 12, 12)))
    t.type("BookmarkSlotCurrent", "PanelContainer")
    t.set("BookmarkSlotCurrent/styles/panel", t.box("bookmark_current", bg=c("paper"), border=c("hand"), width=(2, 2, 2, 2), margin=(12, 12, 12, 12)))
    t.type("BookmarkSlots", "VBoxContainer")
    t.set("BookmarkSlots/constants/separation", 16)
    t.type("BookmarkLines", "VBoxContainer")
    t.set("BookmarkLines/constants/separation", 4)
    text_type(t, "BookmarkTitle", "Label", size=SIZE["sub"], color=c("ink"), font=f["head"], rich=False)
    text_type(t, "BookmarkMeta", "Label", size=SIZE["note"], color=c("muted"), font=f["register"], rich=False)
    t.type("BookmarkButton", "Button")
    for state in ("normal", "hover", "pressed", "hover_pressed", "disabled"):
        t.set(f"BookmarkButton/styles/{state}", empty)
    t.set("BookmarkButton/styles/focus", t.box("option_focus", border=c("hand", 0.9), width=(0, 0, 0, 2)))
    text_type(t, "BookRowValue", "Label", size=SIZE["body"], color=c("ink"), rich=False)


# ================================================================ UITheme ===

def ui_theme() -> Theme:
    t = Theme("uid://0xr67dd1psa")
    f = fonts(t)
    t.header += [("default_font", f["body"]), ("default_font_size", SIZE["body"])]
    for name in PALETTE:
        t.set(f"Paper/colors/{name}", c(name))
    t.set("Paper/colors/shadow", c("ink", 0.55))
    t.set("Paper/constants/module", 32)
    t.set("Paper/constants/baseline", 8)
    for role in ("body", "register", "head", "deck", "mast", "column", "hand", "icon", "icon_filled"):
        t.set(f"Paper/fonts/{role}", f[role])
    pixel_types(t, f)
    empty = paper_basics(t, f, c("ink"))
    t.set("LineEdit/styles/normal", t.box("line_edit", border=c("ink", 0.8), width=(0, 0, 0, 1), margin=(4, 8, 4, 8)))
    t.set("LineEdit/styles/focus", t.box("line_edit_focus", border=c("hand"), width=(0, 0, 0, 2), margin=(4, 8, 4, 7)))
    t.set("LineEdit/colors/font_color", c("hand"))
    t.set("LineEdit/colors/caret_color", c("hand"))
    t.set("LineEdit/colors/font_placeholder_color", c("muted", 0.7))
    t.set("LineEdit/fonts/font", f["hand"])
    t.set("LineEdit/font_sizes/font_size", SIZE["sub"])
    t.set("VSeparator/styles/separator", t.line("col_rule", c("rule", 0.7), True))
    t.set("VSeparator/constants/separation", 1)
    t.set("HSeparator/styles/separator", t.line("row_rule", c("rule", 0.7), False))
    t.set("HSeparator/constants/separation", 1)

    secondary_primary(t, f)
    # 选项行：序号在左边距，勾在右边距；点线以 1 px 淡规线代替。
    om = (48, 8, 40, 8)
    button_type(t, "PaperCheck", "Button",
                normal=t.box("opt_normal", border=c("rule", 0.45), width=(0, 0, 0, 1), margin=om),
                hover=t.box("opt_hover", bg=c("paper_dark", 0.8), border=c("rule", 0.6), width=(0, 0, 0, 1), margin=om),
                pressed=t.box("opt_normal", border=c("rule", 0.45), width=(0, 0, 0, 1), margin=om),
                hover_pressed=t.box("opt_hover", bg=c("paper_dark", 0.8), border=c("rule", 0.6), width=(0, 0, 0, 1), margin=om),
                disabled=t.box("opt_normal", border=c("rule", 0.45), width=(0, 0, 0, 1), margin=om),
                focus=t.box("opt_focus", border=c("hand", 0.9), width=(0, 0, 0, 2)),
                color=c("ink"), disabled_color=c("muted", 0.55), size=SIZE["body"], font=f["body"])
    t.set("PaperCheck/colors/mark", c("hand"))
    t.set("PaperCheck/colors/tint", c("hand", 0.16))
    t.set("PaperCheck/colors/serial", c("muted"))
    t.set("PaperCheck/colors/strike", c("muted", 0.8))
    button_type(t, "PaperEntry", "Button",
                normal=t.box("opt_normal", border=c("rule", 0.45), width=(0, 0, 0, 1), margin=om),
                hover=t.box("opt_hover", bg=c("paper_dark", 0.8), border=c("rule", 0.6), width=(0, 0, 0, 1), margin=om),
                pressed=t.box("opt_hover", bg=c("paper_dark", 0.8), border=c("rule", 0.6), width=(0, 0, 0, 1), margin=om),
                disabled=t.box("opt_normal", border=c("rule", 0.45), width=(0, 0, 0, 1), margin=om),
                focus=t.box("opt_focus", border=c("hand", 0.9), width=(0, 0, 0, 2)),
                color=c("ink"), disabled_color=c("muted", 0.55), size=SIZE["body"], font=f["body"])
    t.set("PaperEntry/colors/serial", c("muted"))
    t.set("PaperEntry/colors/strike", c("muted", 0.8))
    tm = (16, 8, 8, 8)
    button_type(t, "PaperIndexTab", "Button",
                normal=t.box("tab_normal", margin=tm),
                hover=t.box("tab_hover", border=c("rule"), width=(0, 0, 0, 1), margin=tm),
                pressed=t.box("tab_current", border=c("ink"), width=(3, 0, 0, 0), margin=tm),
                disabled=t.box("tab_normal", margin=tm),
                focus=t.box("opt_focus", border=c("hand", 0.9), width=(0, 0, 0, 2)),
                color=c("ink"), disabled_color=c("muted", 0.55), size=SIZE["body"], font=f["body"])
    t.type("ChoiceMenuOption", "PaperEntry")
    t.type("NotebookEntryButton", "PaperEntry")
    t.type("ReplayManuscriptOption", "PaperCheck")
    t.type("ReplayManuscriptOpenCard", "Button")
    t.set("ReplayManuscriptOpenCard/font_sizes/font_size", SIZE["note"])

    # ---- 文字 ----
    text_type(t, "PaperLabelPaperLabel", "RichTextLabel", size=SIZE["body"], color=c("ink"))
    text_type(t, "PaperLabelTitle", "RichTextLabel", size=SIZE["title"], color=c("ink"), font=f["head"], bold=f["head"])
    text_type(t, "PaperLabelHeading", "RichTextLabel", size=SIZE["sub"], color=c("ink"), font=f["column"], bold=f["column"])
    text_type(t, "PaperLabelDeck", "RichTextLabel", size=SIZE["body"], color=c("ink"), font=f["deck"], bold=f["deck"])
    text_type(t, "PaperLabelSubheading", "RichTextLabel", size=SIZE["note"], color=c("muted"), font=f["register"])
    t.type("PaperLabelKicker", "PaperLabelSubheading")
    text_type(t, "PaperLabelNote", "RichTextLabel", size=SIZE["body"], color=c("ink"))
    t.type("PaperLabelNoteMuted", "PaperLabelNote")
    t.set("PaperLabelNoteMuted/colors/default_color", c("muted"))
    t.type("PaperLabelNoteSeal", "PaperLabelNote")
    t.set("PaperLabelNoteSeal/colors/default_color", c("seal"))
    text_type(t, "PaperLabelRegister", "RichTextLabel", size=SIZE["note"], color=c("muted"), font=f["register"])
    text_type(t, "PaperLabelHand", "RichTextLabel", size=SIZE["sub"], color=c("hand"), font=f["hand"], bold=f["hand"])
    text_type(t, "PaperLabelNumeral", "Label", size=SIZE["title"], color=c("ink"), font=f["head"], rich=False)
    text_type(t, "PaperIcon", "Label", size=24, color=c("ink"), font=f["icon"], rich=False)
    text_type(t, "PaperIconFilled", "Label", size=24, color=c("ink"), font=f["icon_filled"], rich=False)
    text_type(t, "PaperIconLarge", "Label", size=32, color=c("ink"), font=f["icon"], rich=False)

    # ---- 纸面弹窗：一张纸，无边框，投影；标题居中，下接双线 ----
    sheet = t.paper("paper_sheet", margin=(40, 32, 40, 32))
    t.set("PaperModal/colors/dim", c("ink", 0.62))
    t.set("PaperModal/constants/viewport_margin", 16)
    t.set("PaperModal/constants/width_small", 576)
    t.set("PaperModal/constants/width_medium", 832)
    t.set("PaperModal/constants/width_large", 1152)
    t.type("PaperModalPanelContainer", "PanelContainer")
    t.set("PaperModalPanelContainer/styles/panel", sheet)
    t.type("PaperSheet", "PanelContainer")
    t.set("PaperSheet/styles/panel", sheet)
    t.type("PaperModalColumn", "VBoxContainer")
    t.set("PaperModalColumn/constants/separation", 16)
    t.type("PaperModalBody", "VBoxContainer")
    t.set("PaperModalBody/constants/separation", 8)
    t.type("PaperModalFooter", "HBoxContainer")
    t.set("PaperModalFooter/constants/separation", 16)
    text_type(t, "PaperModalTitleLabel", "RichTextLabel", size=SIZE["title"], color=c("ink"), font=f["head"], bold=f["head"])
    t.type("PaperBox", "PanelContainer")
    t.set("PaperBox/styles/panel", t.box("paper_box", border=c("ink"), width=(3, 3, 3, 3), margin=(16, 12, 16, 12)))
    t.type("PaperMount", "PanelContainer")
    t.set("PaperMount/styles/panel", t.box("paper_mount", bg=c("paper_dark"), margin=(16, 16, 16, 16)))
    t.type("PaperPlate", "PanelContainer")
    t.set("PaperPlate/styles/panel", t.box("paper_plate", bg=c("ink", 0.85), border=c("ink"), width=(1, 1, 1, 1), margin=(1, 1, 1, 1)))
    t.type("PaperColumns", "HBoxContainer")
    t.set("PaperColumns/constants/separation", 24)
    t.type("PaperStampOuter", "PanelContainer")
    t.set("PaperStampOuter/styles/panel", t.box("stamp_seal", border=c("seal", 0.9), width=(1, 1, 1, 1), margin=(6, 1, 6, 1)))
    text_type(t, "PaperStampText", "Label", size=SIZE["note"], color=c("seal"), font=f["register"], rich=False)
    for tone in ("match", "muted"):
        cap = tone.capitalize()
        t.type(f"PaperStampOuter{cap}", "PanelContainer")
        t.set(f"PaperStampOuter{cap}/styles/panel", t.box(f"stamp_{tone}", border=c(tone, 0.9), width=(1, 1, 1, 1), margin=(6, 1, 6, 1)))
        text_type(t, f"PaperStampText{cap}", "Label", size=SIZE["note"], color=c(tone), font=f["register"], rich=False)
    t.set("PaperTally/colors/filled", c("ink"))
    t.set("PaperTally/colors/empty", c("ink", 0.45))
    t.set("PaperTally/constants/cell", 8)
    t.set("PaperTally/constants/gap", 4)
    t.set("PaperLedger/colors/line", c("rule", 0.35))
    t.set("PaperLedger/constants/step", 32)

    # ---- 学园图（纸面线路图） ----
    t.set("CampusMap/colors/background", c("paper"))
    t.set("CampusMap/colors/border", c("rule", 0.6))
    t.set("CampusMap/colors/link", c("ink"))
    t.set("CampusMap/colors/link_unknown", c("rule", 0.4))
    t.set("CampusMap/colors/station", c("ink"))
    t.set("CampusMap/colors/station_fill", c("paper"))
    t.set("CampusMap/colors/here", c("hand"))
    t.set("CampusMap/colors/hover", c("paper_dark"))
    t.set("CampusMap/colors/block", c("paper_dark", 0.8))
    t.set("CampusMap/colors/wing_outline", c("paper_dark", 0.8))
    t.set("CampusMap/colors/bubble", c("paper"))
    t.set("CampusMap/colors/bubble_border", c("ink", 0.7))
    t.set("CampusMap/colors/figure", c("ink", 0.8))
    t.set("CampusMap/colors/trail", c("muted", 0.7))
    t.set("CampusMap/colors/ink", c("ink"))
    t.set("CampusMap/colors/muted", c("muted"))
    t.set("CampusMap/constants/border_inset", 8)
    t.set("CampusMap/constants/border_width", 1)
    t.set("CampusMap/constants/link_width", 3)
    t.set("CampusMap/constants/station_radius", 7)
    t.set("CampusMap/constants/grid", 32)
    t.set("CampusMap/font_sizes/bubble", SIZE["note"])
    t.set("CampusMap/font_sizes/figure", SIZE["note"])
    t.set("CampusMap/font_sizes/hint", SIZE["note"])
    t.set("CampusMap/fonts/hand", f["hand"])
    t.set("CampusMap/fonts/register", f["register"])
    text_type(t, "CampusMapName", "Label", size=SIZE["body"], color=c("ink"), rich=False)
    text_type(t, "CampusMapHere", "Label", size=SIZE["sub"], color=c("hand"), font=f["hand"], rich=False)
    for name in ("CampusMapLocation", "CampusMapLocationCurrent", "CampusMapLocationHover"):
        t.type(name, "Panel")
        t.set(f"{name}/styles/panel", empty)

    # ---- 证物卡 ----
    text_type(t, "EvidenceCardTitle", "RichTextLabel", size=SIZE["title"], color=c("ink"), font=f["head"], bold=f["head"])
    text_type(t, "EvidenceCardFacts", "RichTextLabel", size=SIZE["body"], color=c("ink"))
    text_type(t, "EvidenceCardIntro", "RichTextLabel", size=SIZE["body"], color=c("ink"), font=f["deck"], bold=f["deck"])
    text_type(t, "EvidenceCardSource", "RichTextLabel", size=SIZE["note"], color=c("muted"), font=f["register"])
    t.type("EvidenceCardRow", "HBoxContainer")
    t.set("EvidenceCardRow/constants/separation", 24)
    t.type("EvidenceCardTexts", "VBoxContainer")
    t.set("EvidenceCardTexts/constants/separation", 8)
    t.type("EvidenceCardTable", "GridContainer")
    t.set("EvidenceCardTable/constants/h_separation", 24)
    t.set("EvidenceCardTable/constants/v_separation", 8)

    # ---- 章末卡 ----
    text_type(t, "EndingCardTitle", "RichTextLabel", size=SIZE["head"], color=c("ink"), font=f["head"], bold=f["head"])
    text_type(t, "EndingCardOutcome", "RichTextLabel", size=SIZE["sub"], color=c("ink"), font=f["deck"], bold=f["deck"])
    t.type("EndingCardRows", "VBoxContainer")
    t.set("EndingCardRows/constants/separation", 8)

    # ---- 现场抬头：纸色日期栏 ----
    t.type("FieldHUDBar", "PanelContainer")
    t.set("FieldHUDBar/styles/panel", t.paper("hud_bar", margin=(24, 0, 24, 0)))
    text_type(t, "FieldHUDTitle", "Label", size=SIZE["sub"], color=c("ink"), font=f["head"], rich=False)
    text_type(t, "FieldHUDTime", "Label", size=SIZE["body"], color=c("ink"), rich=False)
    text_type(t, "FieldHUDPlace", "Label", size=SIZE["sub"], color=c("ink"), font=f["head"], rich=False)
    slip = t.box("paper_slip", bg=c("paper"), border=c("ink", 0.8), width=(1, 1, 1, 1), margin=(16, 6, 16, 6),
                 shadow=c("ink", 0.5), shadow_size=6, shadow_offset=(0, 2))
    text_type(t, "FieldHUDHint", "Label", size=SIZE["body"], color=c("ink"), rich=False, style=slip)
    text_type(t, "FieldHUDHoverTip", "Label", size=SIZE["note"], color=c("ink"), rich=False,
              style=t.box("paper_slip_small", bg=c("paper"), border=c("ink", 0.8), width=(1, 1, 1, 1), margin=(8, 2, 8, 2),
                          shadow=c("ink", 0.5), shadow_size=4, shadow_offset=(0, 1)))
    text_type(t, "FieldHUDAside", "Label", size=SIZE["sub"], color=c("hand"), font=f["hand"], rich=False, style=slip)
    text_type(t, "FieldHUDFlash", "RichTextLabel", size=SIZE["title"], color=c("ink"), font=f["head"], bold=f["head"],
              style=t.box("flash_banner", bg=c("paper"), border=c("ink"), width=(0, 3, 0, 3), margin=(32, 12, 32, 12),
                          shadow=c("ink", 0.5), shadow_size=10, shadow_offset=(0, 3)))

    # ---- 表单与对质 ----
    t.type("FormGroup", "VBoxContainer")
    t.set("FormGroup/constants/separation", 4)
    t.type("FormRow", "HBoxContainer")
    t.set("FormRow/constants/separation", 16)
    t.type("FormColumns", "HBoxContainer")
    t.set("FormColumns/constants/separation", 24)
    t.type("FormLineEdit", "LineEdit")
    t.set("FormLineEdit/font_sizes/font_size", SIZE["sub"])
    t.type("FormOptionColumn", "VBoxContainer")
    t.set("FormOptionColumn/constants/separation", 0)
    t.type("FormOptionFlow", "HFlowContainer")
    t.set("FormOptionFlow/constants/h_separation", 8)
    t.set("FormOptionFlow/constants/v_separation", 0)
    text_type(t, "FormPanelPrompt", "RichTextLabel", size=SIZE["body"], color=c("ink"))
    text_type(t, "FormPanelQuestion", "RichTextLabel", size=SIZE["head"], color=c("ink"), font=f["head"], bold=f["head"])
    text_type(t, "FormPanelDeck", "RichTextLabel", size=SIZE["body"], color=c("muted"), font=f["deck"], bold=f["deck"])
    text_type(t, "FormPanelSummary", "RichTextLabel", size=SIZE["sub"], color=c("hand"), font=f["hand"], bold=f["hand"])

    # ---- 现场提示描边（像素层） ----
    t.set("HintOverlay/colors/hover", c("paper"))
    t.set("HintOverlay/colors/near", c("paper", 0.7))
    t.set("HintOverlay/colors/shade", c("ink", 0.6))
    t.set("HintOverlay/colors/spark_center", c("paper"))
    t.set("HintOverlay/colors/spark_edge", c("muted"))
    t.set("HintOverlay/colors/spark_light", c("paper", 0.9))

    # ---- 笔记本（对开：左页索引，右页条目） ----
    t.type("NotebookLayout", "HBoxContainer")
    t.set("NotebookLayout/constants/separation", 0)
    t.type("NotebookTabs", "VBoxContainer")
    t.set("NotebookTabs/constants/separation", 0)
    t.type("NotebookPage", "PanelContainer")
    t.set("NotebookPage/styles/panel", t.box("notebook_page", border=c("paper_dark"), width=(8, 0, 0, 0), margin=(32, 0, 0, 0)))
    t.type("NotebookColumn", "VBoxContainer")
    t.set("NotebookColumn/constants/separation", 0)
    t.type("NotebookGrid", "GridContainer")
    t.set("NotebookGrid/constants/h_separation", 24)
    t.set("NotebookGrid/constants/v_separation", 0)
    text_type(t, "NotebookEmpty", "RichTextLabel", size=SIZE["body"], color=c("muted"))

    # ---- 回放气泡与回放抬头 ----
    t.type("ReplayBubbleReplayBubble", "PanelContainer")
    t.set("ReplayBubbleReplayBubble/styles/panel", t.box("bubble", bg=c("paper", 0.96), border=c("ink", 0.8), width=(1, 1, 1, 1), margin=(8, 4, 8, 4)))
    text_type(t, "ReplayBubbleText", "RichTextLabel", size=SIZE["note"], color=c("ink"))
    t.type("ReplayOverlayBar", "PanelContainer")
    t.set("ReplayOverlayBar/styles/panel", t.paper("replay_bar", margin=(24, 8, 24, 8)))
    text_type(t, "ReplayOverlayCaption", "RichTextLabel", size=SIZE["body"], color=c("ink"))
    text_type(t, "ReplayOverlayControls", "Label", size=SIZE["note"], color=c("muted"), rich=False, style=slip)
    text_type(t, "ReplayOverlayDoor", "Label", size=SIZE["note"], color=c("ink"), rich=False, style=slip)

    # ---- 重演手稿（对开） ----
    t.type("ReplayManuscriptReplayManuscript", "HBoxContainer")
    t.set("ReplayManuscriptReplayManuscript/constants/separation", 0)
    t.type("ReplayManuscriptLeftPage", "PanelContainer")
    t.set("ReplayManuscriptLeftPage/styles/panel", t.box("manuscript_left", border=c("paper_dark"), width=(0, 0, 8, 0), margin=(8, 8, 32, 8)))
    t.type("ReplayManuscriptRightPage", "PanelContainer")
    t.set("ReplayManuscriptRightPage/styles/panel", t.box("manuscript_right", margin=(32, 8, 8, 8)))
    t.type("ReplayManuscriptColumn", "VBoxContainer")
    t.set("ReplayManuscriptColumn/constants/separation", 8)
    t.type("ReplayManuscriptActions", "HBoxContainer")
    t.set("ReplayManuscriptActions/constants/separation", 16)
    t.type("ReplayManuscriptOptions", "VBoxContainer")
    t.set("ReplayManuscriptOptions/constants/separation", 0)
    t.type("ReplayManuscriptFinding", "VBoxContainer")
    t.set("ReplayManuscriptFinding/constants/separation", 4)
    t.type("ReplayManuscriptQuestion", "HBoxContainer")
    t.set("ReplayManuscriptQuestion/constants/separation", 16)
    text_type(t, "ReplayManuscriptHeading", "RichTextLabel", size=SIZE["body"], color=c("ink"))
    text_type(t, "ReplayManuscriptDetail", "RichTextLabel", size=SIZE["note"], color=c("muted"))
    text_type(t, "ReplayManuscriptHint", "RichTextLabel", size=SIZE["body"], color=c("muted"), font=f["deck"], bold=f["deck"])
    text_type(t, "ReplayManuscriptPreview", "RichTextLabel", size=SIZE["body"], color=c("muted"))
    text_type(t, "ReplayManuscriptFullMatch", "RichTextLabel", size=SIZE["sub"], color=c("match"), font=f["deck"], bold=f["deck"])
    text_type(t, "ReplayManuscriptStale", "RichTextLabel", size=SIZE["note"], color=c("muted"))

    book_types(t, f, empty)

    # ---- 视野遮罩（像素层；纯黑留给恶魔） ----
    t.set("SightMask/colors/unseen", Color_dark())
    t.set("SightMask/colors/remembered", Color_dark(0.62))
    return t


def Color_dark(alpha: float = 1.0) -> str:
    """视野遮罩用的暗色：取墨色压暗，不用纯黑。"""
    return f"Color(0.105882, 0.094118, 0.082353, {alpha:.6g})"


# ============================================================ DialogueTheme ===

def dialogue_theme() -> Theme:
    t = Theme("uid://c6iaqwqliw75c")
    f = fonts(t)
    t.header += [("default_font", f["body"]), ("default_font_size", SIZE["body"])]
    paper_basics(t, f, c("ink"))
    m = (8, 8, 8, 8)
    button_type(t, "Button", "",
                normal=t.box("resp_plain", margin=m),
                hover=t.box("resp_hover", bg=c("paper_dark"), border=c("ink"), width=(0, 0, 0, 1), margin=m),
                pressed=t.box("resp_hover", bg=c("paper_dark"), border=c("ink"), width=(0, 0, 0, 1), margin=m),
                disabled=t.box("resp_plain", margin=m),
                focus=t.box("resp_focus", border=c("hand"), width=(0, 0, 0, 2)),
                color=c("ink"), disabled_color=c("muted", 0.5), size=SIZE["body"], font=f["body"])
    t.type("ChapterDialogueVisitedResponse", "Button")
    for state in ("font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color"):
        t.set(f"ChapterDialogueVisitedResponse/colors/{state}", c("muted"))
    text_type(t, "ChapterDialogueBody", "RichTextLabel", size=SIZE["sub"], color=c("ink"))
    t.set("ChapterDialogueBody/constants/line_separation", 8)
    t.type("ChapterDialogueChoices", "GridContainer")
    t.set("ChapterDialogueChoices/constants/h_separation", 16)
    t.set("ChapterDialogueChoices/constants/v_separation", 8)
    t.type("ChapterDialogueDialoguePanel", "Panel")
    t.set("ChapterDialogueDialoguePanel/styles/panel", t.paper("dialogue_panel"))
    text_type(t, "ChapterDialogueHeading", "Label", size=SIZE["note"], color=c("muted"), font=f["register"], rich=False)
    t.type("ChapterDialogueHistoryButton", "Button")
    t.set("ChapterDialogueHistoryButton/colors/font_color", c("muted"))
    t.set("ChapterDialogueHistoryButton/font_sizes/font_size", SIZE["note"])
    t.type("ChapterDialogueHistoryPanel", "Panel")
    t.set("ChapterDialogueHistoryPanel/styles/panel", t.paper("history_panel"))
    text_type(t, "ChapterDialogueMonogram", "Label", size=SIZE["head"], color=c("ink", 0.85), font=f["head"], rich=False)
    t.type("ChapterDialogueNext", "Button")
    t.set("ChapterDialogueNext/colors/font_color", c("ink"))
    t.set("ChapterDialogueNext/font_sizes/font_size", SIZE["note"])
    t.type("ChapterDialoguePortraitFrame", "PanelContainer")
    t.set("ChapterDialoguePortraitFrame/styles/panel", t.box("portrait_mount", bg=c("paper_dark"), border=c("ink", 0.8),
                                                             width=(1, 1, 1, 1), margin=(8, 8, 8, 8)))
    t.type("ChapterDialoguePortraitColumn", "VBoxContainer")
    t.set("ChapterDialoguePortraitColumn/constants/separation", 8)
    text_type(t, "ChapterDialoguePortraitName", "Label", size=SIZE["note"], color=c("muted"), font=f["register"], rich=False)
    text_type(t, "ChapterDialogueSpeaker", "RichTextLabel", size=SIZE["sub"], color=c("ink"), font=f["head"], bold=f["head"])
    pixel_types(t, f)
    return t


# =============================================================== TitleTheme ===

def title_theme() -> Theme:
    """标题页（设计规范第六部分 §4.7）：油灯下桌上合着的一本册子。布面封皮上贴题签：图版、像素书名、双线、菜单。"""
    t = Theme("uid://desx2ma08f07d")
    f = fonts(t)
    t.header += [("default_font", f["body"]), ("default_font_size", SIZE["body"])]
    paper_basics(t, f, c("ink"))
    pixel_types(t, f)
    t.set("Title/colors/background", Color_dark())
    t.set("Title/colors/slip_shadow", hexc("0A0604", 0.45))
    t.set("Title/colors/page_light", hexc("D6CAAC"))
    t.set("Title/colors/page_dark", hexc("BCAE8E"))
    t.set("Title/colors/cover_edge", hexc("22241E"))
    t.set("Title/colors/spine", hexc("000000", 0.28))
    # 册子的摆放与桌面渲染同一套数（build_ui_desk.py）：封面中心与尺寸（屏幕像素）、桌面上的转角（十分之一度）、
    # 近侧书页厚度（毫米厚度按近俯视竖直减半后的屏幕像素）、纵深缩短（百分之一）。册子投影已烘在桌面图里。
    for key, value in (("book_x", desk.BOOK_CENTER[0]), ("book_y", desk.BOOK_CENTER[1]),
                       ("book_w", desk.BOOK_SIZE[0]), ("book_h", desk.BOOK_SIZE[1]),
                       ("book_angle", round(desk.BOOK_ANGLE * 10)),
                       ("thickness", round(desk.mm(desk.BOOK_THICK_MM) * desk.ELEV_K * desk.S)),
                       ("elev_q", round(desk.ELEV_Q * 100)), ("slip_shadow", 4)):
        t.set(f"Title/constants/{key}", value)
    cloth = t.texture_file("cloth_texture", "res://ShareAsset/Internal/Props/UIDesk/cloth.png", "uid://318jfwph0wi7")
    t.type("TitleCover", "PanelContainer")
    t.set("TitleCover/styles/panel", t.sub("StyleBoxTexture", "title_cloth", [
        ("texture", cloth), ("axis_stretch_horizontal", "1"), ("axis_stretch_vertical", "1")]))
    t.type("TitleSlip", "PanelContainer")
    t.set("TitleSlip/styles/panel", t.paper("title_slip", margin=(20, 20, 20, 24)))
    t.type("TitleColumn", "VBoxContainer")
    t.set("TitleColumn/constants/separation", 14)
    t.type("TitleMount", "PanelContainer")
    t.set("TitleMount/styles/panel", t.box("title_plate", bg=c("ink"), border=c("ink"), width=(2, 2, 2, 2)))
    t.type("TitleMenu", "VBoxContainer")
    t.set("TitleMenu/constants/separation", 4)
    flat = t.box("title_flat", margin=(24, 6, 24, 6))
    under = t.box("title_under", border=c("ink"), width=(0, 0, 0, 3), margin=(24, 6, 24, 3))
    button_type(t, "TitleMenuButton", "Button", normal=flat, hover=under, pressed=under, disabled=flat, focus=under,
                color=c("ink"), disabled_color=c("muted", 0.5), size=SIZE["body"] + 2, font=f["register"])
    return t


def main() -> int:
    check = "--check" in sys.argv
    outputs = {"UITheme.tres": ui_theme(), "DialogueTheme.tres": dialogue_theme(), "TitleTheme.tres": title_theme()}
    stale = []
    for name, theme in outputs.items():
        path = STYLES / name
        text = theme.render()
        if check:
            if not path.exists() or path.read_text(encoding="utf-8") != text: stale.append(name)
        else:
            path.write_text(text, encoding="utf-8", newline="\n")
            print(f"wrote {path}")
    if stale:
        print("主题与生成器不一致：" + ", ".join(stale))
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
