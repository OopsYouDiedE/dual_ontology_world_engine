"""生成界面主题：scenes/UI/Styles/ 下的三份主题与原生弹窗图标。

设计规范第六部分 §4：现场类界面用 19 世纪末报纸版式，翻阅类界面用近代博物书对开；
语义色板、五级字号（标题页书名与通栏问题另可用 64）、正文朝华打字机、标题思源宋体特粗、笔迹 Yozai。
颜色、字号与字体只在本文件顶部定义一次，主题条目一律引用这些名字；改外观先改这里再重新生成。

    python mcp/asset_pipeline/build_ui_theme.py          # 写出三份现役主题及共用图标
    python mcp/asset_pipeline/build_ui_theme.py --check  # 只比对，不一致时返回 1
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

STYLES = (Path(__file__).resolve().parents[2] / "godot_csharp") / "scenes" / "UI" / "Styles"

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
    # 日文字形回退（汉字字体缺假名时按字取用）
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
            uid_field = f' uid="{uid}"' if uid else ''
            out.append(f'[ext_resource type="{kind}"{uid_field} path="{path}" id="{rid}"]')
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
    """像素字类型（Label 型）：字号为屏幕尺寸，pixel_scale 为渲染倍率。"""
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


def native_icons() -> dict[str, str]:
    """原生弹窗的墨线符号，与主题一同生成和校验，避免引擎白色图标混入纸面。"""
    ink, rule, hand = (PALETTE[name] for name in ("ink", "rule", "hand"))
    paths = {
        "native-close.svg": f'<path d="M4 4 L12 12 M12 4 L4 12" stroke="#{ink}"/>',
        "native-submenu.svg": f'<path d="M6 3 L11 8 L6 13" stroke="#{ink}"/>',
        "native-submenu-mirrored.svg": f'<path d="M10 3 L5 8 L10 13" stroke="#{ink}"/>',
        "native-radio-off.svg": f'<circle cx="8" cy="8" r="5.5" stroke="#{rule}"/>',
        "native-radio-on.svg": (f'<circle cx="8" cy="8" r="5.5" stroke="#{rule}"/>'
                                f'<circle cx="8" cy="8" r="2.5" fill="#{hand}" stroke="none"/>'),
    }
    return {name: ('<!-- Generated by tools/build_ui_theme.py. -->\n'
                   '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 16 16">'
                   '<g fill="none" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">'
                   + path + '</g></svg>\n') for name, path in paths.items()}


def native_surfaces(t: Theme, f: dict[str, str]) -> None:
    """原生窗口、确认框和菜单共用纸张、墨色、衬线标题及正文角色。"""
    close = t.texture_file("native_close", "res://scenes/UI/Styles/native-close.svg", "")
    for state in ("close", "close_pressed"):
        t.set(f"Window/icons/{state}", close)
    t.set("Window/colors/title_color", c("ink"))
    t.set("Window/colors/title_outline_modulate", c("paper", 0.0))
    t.set("Window/fonts/title_font", f["deck"])
    t.set("Window/font_sizes/title_font_size", SIZE["sub"])
    for name, value in (("title_outline_size", 0), ("title_height", 36), ("resize_margin", 4),
                        ("close_h_offset", 18), ("close_v_offset", 24)):
        t.set(f"Window/constants/{name}", value)
    for state, alpha in (("embedded_border", 0.85), ("embedded_unfocused_border", 0.6)):
        t.set(f"Window/styles/{state}", t.box("native_window_" + state, bg=c("paper_dark"),
              border=c("rule", alpha), width=(1, 1, 1, 1), margin=(8, 28, 8, 8), expand=(8, 36, 8, 8),
              shadow=c("ink", 0.18), shadow_size=8, shadow_offset=(0, 3)))
    t.set("AcceptDialog/styles/panel", t.box("native_dialog_panel", bg=c("paper"), margin=(24, 18, 24, 16)))
    t.set("AcceptDialog/constants/buttons_separation", 16)
    panel = t.box("native_popup_panel", bg=c("paper"), border=c("rule", 0.75), width=(1, 1, 1, 1),
                  margin=(10, 8, 10, 8), shadow=c("ink", 0.18), shadow_size=6, shadow_offset=(0, 3))
    t.set("PopupPanel/styles/panel", panel)
    t.set("PopupMenu/styles/panel", panel)
    t.set("PopupMenu/styles/hover", t.box("native_menu_hover", bg=c("paper_dark", 0.72),
          border=c("hand", 0.75), width=(0, 0, 0, 1)))
    separator = t.box("native_menu_separator", border=c("rule", 0.55), width=(0, 1, 0, 0), margin=(0, 4, 0, 4))
    for name in ("separator", "labeled_separator_left", "labeled_separator_right"):
        t.set(f"PopupMenu/styles/{name}", separator)
    for name, color in (("font_color", c("ink")), ("font_accelerator_color", c("muted")),
                        ("font_disabled_color", c("muted", 0.55)), ("font_hover_color", c("hand")),
                        ("font_separator_color", c("muted")), ("font_outline_color", c("paper", 0.0)),
                        ("font_separator_outline_color", c("paper", 0.0))):
        t.set(f"PopupMenu/colors/{name}", color)
    t.set("PopupMenu/fonts/font", f["body"])
    t.set("PopupMenu/fonts/font_separator", f["register"])
    t.set("PopupMenu/font_sizes/font_size", SIZE["body"])
    t.set("PopupMenu/font_sizes/font_separator_size", SIZE["note"])
    for name, value in (("indent", 16), ("h_separation", 10), ("v_separation", 10),
                        ("outline_size", 0), ("separator_outline_size", 0),
                        ("item_start_padding", 6), ("item_end_padding", 6), ("icon_max_width", 20),
                        ("gutter_compact", 1)):
        t.set(f"PopupMenu/constants/{name}", value)
    check_on = t.texture_file("book_check_on", "res://ShareAsset/Internal/Props/UIDesk/Painterly3D/check-on.svg", "")
    check_off = t.texture_file("book_check_off", "res://ShareAsset/Internal/Props/UIDesk/Painterly3D/check-off.svg", "")
    radio_on = t.texture_file("native_radio_on", "res://scenes/UI/Styles/native-radio-on.svg", "")
    radio_off = t.texture_file("native_radio_off", "res://scenes/UI/Styles/native-radio-off.svg", "")
    for name, texture in (("checked", check_on), ("unchecked", check_off),
                          ("radio_checked", radio_on), ("radio_unchecked", radio_off)):
        t.set(f"PopupMenu/icons/{name}", texture)
        t.set(f"PopupMenu/icons/{name}_disabled", texture)
    for name in ("submenu", "submenu_mirrored"):
        t.set(f"PopupMenu/icons/{name}", t.texture_file("native_" + name,
              "res://scenes/UI/Styles/native-" + name.replace("_", "-") + ".svg", ""))


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
    t.set("PaperFrame/colors/ornament", c("rule", 0.72))
    t.set("PaperFrame/styles/outer", t.box("paper_frame_outer", border=c("rule", 0.85), width=(2, 2, 2, 2)))
    t.set("PaperFrame/styles/inner", t.box("paper_frame_inner", border=c("rule", 0.65), width=(1, 1, 1, 1)))
    t.type("SpatialChoicePanel", "PanelContainer")
    t.set("SpatialChoicePanel/styles/panel", t.paper("spatial_choice_panel", margin=(16, 16, 16, 16)))
    t.set("VScrollBar/styles/grabber", t.box("scroll_grabber", bg=c("ink", 0.35)))
    t.set("VScrollBar/styles/grabber_highlight", t.box("scroll_grabber_hi", bg=c("ink", 0.55)))
    t.set("VScrollBar/styles/grabber_pressed", t.box("scroll_grabber_hi", bg=c("ink", 0.55)))
    t.set("VScrollBar/styles/scroll", t.box("scroll_track", bg=c("paper_dark", 0.8), margin=(3, 0, 3, 0)))
    native_surfaces(t, f)
    return empty


def Color_dark(alpha: float = 1.0) -> str:
    """标题背景用的暗色：取墨色压暗，不用纯黑。"""
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
    text_type(t, "ChapterDialogueBody", "RichTextLabel", size=SIZE["body"], color=c("ink"))
    t.set("ChapterDialogueBody/constants/line_separation", 6)
    t.type("DialogueMargins", "MarginContainer")
    for side, value in zip(("left", "top", "right", "bottom"), (28, 14, 28, 12)):
        t.set(f"DialogueMargins/constants/margin_{side}", value)
    t.type("DialogueColumn", "VBoxContainer")
    t.set("DialogueColumn/constants/separation", 6)
    t.type("DialogueChoice", "Button")
    t.set("DialogueChoice/colors/font_color", c("hand"))
    t.set("DialogueChoice/fonts/font", f["hand"])
    text_type(t, "DialogueAside", "Label", size=SIZE["sub"], color=c("paper"), rich=False)
    text_type(t, "DialogueFlash", "Label", size=SIZE["title"], color=c("paper"), rich=False)
    t.type("ChapterDialogueChoices", "GridContainer")
    t.set("ChapterDialogueChoices/constants/h_separation", 16)
    t.set("ChapterDialogueChoices/constants/v_separation", 8)
    t.type("ChapterDialogueDialoguePanel", "Panel")
    t.set("ChapterDialogueDialoguePanel/styles/panel", t.box("dialogue_panel", bg=c("paper"), border=c("rule", 0.65), width=(1, 1, 1, 1), shadow=c("ink", 0.6), shadow_size=14, shadow_offset=(0, 7)))
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
    t.set("ChapterDialoguePortraitFrame/styles/panel", t.box("portrait_mount", bg=c("paper", 0.0), margin=(0, 0, 0, 0)))
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
    # The 3D stage owns geometry and projection; themes own page typography.
    cloth = t.texture_file("cloth_texture", "res://ShareAsset/Internal/Props/UIDesk/Painterly3D/reference-cloth.webp", "")
    t.type("TitleCover", "PanelContainer")
    t.set("TitleCover/styles/panel", t.sub("StyleBoxTexture", "title_cloth", [
        ("texture", cloth), ("axis_stretch_horizontal", "0"), ("axis_stretch_vertical", "0")]))
    t.type("TitleSlip", "PanelContainer")
    slip_texture = t.texture_file("blank_leaf_texture", "res://ShareAsset/Internal/Props/UIDesk/Painterly3D/reference-paper.webp", "")
    t.set("TitleSlip/styles/panel", t.sub("StyleBoxTexture", "title_slip", [
        ("texture", slip_texture), ("content_margin_left", "20.0"), ("content_margin_top", "20.0"),
        ("content_margin_right", "20.0"), ("content_margin_bottom", "24.0")]))
    t.type("TitleColumn", "VBoxContainer")
    t.set("TitleColumn/constants/separation", 14)
    t.type("TitleMount", "PanelContainer")
    t.set("TitleMount/styles/panel", t.box("title_plate", bg=c("ink"), border=c("ink"), width=(2, 2, 2, 2)))
    t.type("TitleMenu", "VBoxContainer")
    t.set("TitleMenu/constants/separation", 4)
    t.type("BookLeaf", "PanelContainer")
    leaf_texture = t.texture_file("blank_leaf_texture", "res://ShareAsset/Internal/Props/UIDesk/Painterly3D/reference-paper.webp", "")
    t.set("BookLeaf/styles/panel", t.sub("StyleBoxTexture", "book_leaf", [
        ("texture", leaf_texture), ("content_margin_left", "42.0"), ("content_margin_top", "36.0"),
        ("content_margin_right", "36.0"), ("content_margin_bottom", "24.0")]))
    t.type("BookColumn", "VBoxContainer")
    t.set("BookColumn/constants/separation", 12)
    text_type(t, "BookHeading", "Label", size=SIZE["title"], color=c("ink"), font=f["deck"], rich=False)
    text_type(t, "BookSectionHeading", "Label", size=SIZE["sub"], color=c("ink"), font=f["deck"], rich=False)
    text_type(t, "BookCaptionLabel", "Label", size=SIZE["note"], color=c("muted"), font=f["body"], rich=False)
    text_type(t, "BookBody", "RichTextLabel", size=SIZE["body"], color=c("ink"), font=f["body"], bold=f["deck"])
    text_type(t, "BookCaption", "RichTextLabel", size=SIZE["note"], color=c("muted"), font=f["body"])
    text_type(t, "BookHandText", "RichTextLabel", size=SIZE["body"], color=c("hand"), font=f["hand"])
    t.type("BookRow", "VBoxContainer")
    t.set("BookRow/constants/separation", 4)
    t.type("BookImageMount", "PanelContainer")
    t.set("BookImageMount/styles/panel", t.box("book_image_mount", bg=c("paper_dark", 0.45), border=c("rule", 0.6), width=(1,1,1,1), margin=(8,8,8,8)))
    for name, base in (("BookNoteEdit", "TextEdit"), ("BookLineEdit", "LineEdit")):
        t.type(name, base)
        t.set(f"{name}/colors/font_color", c("hand"))
        t.set(f"{name}/fonts/font", f["hand"])
        t.set(f"{name}/font_sizes/font_size", SIZE["body"])
        t.set(f"{name}/styles/normal", t.box(name + "_normal", bg=c("paper_dark", 0.22), border=c("rule", 0.6), width=(0,0,0,1), margin=(8,8,8,8)))
        t.set(f"{name}/styles/focus", t.box(name + "_focus", border=c("hand", 0.6), width=(0,0,0,1)))
    t.type("BookToggle", "CheckButton")
    t.set("BookToggle/colors/font_color", c("ink"))
    t.set("BookToggle/colors/font_hover_color", c("hand"))
    t.set("BookToggle/colors/font_pressed_color", c("hand"))
    t.set("BookToggle/fonts/font", f["register"])
    check_on = t.texture_file("book_check_on", "res://ShareAsset/Internal/Props/UIDesk/Painterly3D/check-on.svg", "")
    check_off = t.texture_file("book_check_off", "res://ShareAsset/Internal/Props/UIDesk/Painterly3D/check-off.svg", "")
    for state in ("checked", "checked_disabled", "checked_mirrored", "checked_disabled_mirrored"):
        t.set(f"BookToggle/icons/{state}", check_on)
    for state in ("unchecked", "unchecked_disabled", "unchecked_mirrored", "unchecked_disabled_mirrored"):
        t.set(f"BookToggle/icons/{state}", check_off)
    slider_mark = t.texture_file("book_slider_mark", "res://ShareAsset/Internal/Props/UIDesk/Painterly3D/slider-mark.svg", "")
    for state in ("grabber", "grabber_highlight", "grabber_disabled"):
        t.set(f"HSlider/icons/{state}", slider_mark)
    t.set("HSlider/styles/slider", t.box("book_slider_track", border=c("rule", 0.8), width=(0, 0, 0, 1), margin=(0, 3, 0, 3)))
    t.set("HSlider/styles/grabber_area", t.box("book_slider_ink", border=c("hand", 0.65), width=(0, 0, 0, 1), margin=(0, 3, 0, 3)))
    t.set("HSlider/styles/grabber_area_highlight", t.box("book_slider_ink_hi", border=c("hand"), width=(0, 0, 0, 2), margin=(0, 3, 0, 3)))
    t.set("HSlider/styles/focus", t.box("book_slider_focus", border=c("hand", 0.4), width=(0, 0, 0, 1), margin=(0, 4, 0, 4)))
    option_plain = t.box("book_option", border=c("rule", 0.55), width=(0, 0, 0, 1), margin=(8, 6, 8, 6))
    option_hover = t.box("book_option_hover", bg=c("paper_dark", 0.4), border=c("hand"), width=(0, 0, 0, 1), margin=(8, 6, 8, 6))
    button_type(t, "Button", "", normal=t.box("book_action", margin=(10, 7, 10, 7)),
                hover=t.box("book_action_hover", bg=c("paper_dark", 0.4), border=c("hand"),
                            width=(0, 0, 0, 1), margin=(10, 7, 10, 7)),
                pressed=t.box("book_action_pressed", bg=c("paper_dark", 0.72), border=c("hand"),
                              width=(0, 0, 0, 1), margin=(10, 7, 10, 7)),
                disabled=t.box("book_action", margin=(10, 7, 10, 7)),
                focus=t.box("book_action_focus", border=c("hand"), width=(0, 0, 0, 2)),
                color=c("ink"), disabled_color=c("muted", 0.55), size=SIZE["body"], font=f["body"])
    button_type(t, "BookOption", "Button", normal=option_plain, hover=option_hover, pressed=option_hover,
                disabled=option_plain, focus=t.box("book_option_focus", border=c("hand"), width=(0, 0, 0, 1)),
                color=c("ink"), disabled_color=c("muted"), size=SIZE["body"], font=f["register"])
    button_type(t, "BookIndexButton", "Button", normal=t.box("book_index", border=c("rule", 0.6), width=(0,0,0,1), margin=(10,10,10,10)),
                hover=option_hover, pressed=option_hover, disabled=option_plain,
                focus=t.box("book_index_focus", border=c("hand"), width=(0,0,0,1)),
                color=c("ink"), disabled_color=c("muted"), size=SIZE["sub"], font=f["deck"])
    flat = t.box("title_flat", margin=(24, 6, 24, 6))
    under = t.box("title_under", border=c("ink"), width=(0, 0, 0, 1), margin=(24, 6, 24, 5))
    button_type(t, "TitleMenuButton", "Button", normal=flat, hover=under, pressed=under, disabled=flat, focus=under,
                color=c("ink"), disabled_color=c("muted", 0.5), size=SIZE["sub"], font=f["register"])
    return t


# ============================================================= OverlayTheme ===

def overlay_theme() -> Theme:
    """现场报头与独立推演纸页，复用书本的语义色及字体角色。"""
    t = Theme("uid://cdg6m0q2t8r4h")
    f = fonts(t)
    t.header += [("default_font", f["body"]), ("default_font_size", SIZE["body"])]
    paper_basics(t, f, c("ink"))
    t.set("RichTextLabel/fonts/normal_font", f["body"])
    t.set("RichTextLabel/constants/line_separation", 6)

    t.type("HUDPanel", "PanelContainer")
    t.set("HUDPanel/styles/panel", t.box("hud_panel", bg=c("paper", 0.97),
          border=c("rule", 0.7), width=(0, 0, 0, 1),
          shadow=c("ink", 0.16), shadow_size=4, shadow_offset=(0, 2)))
    t.type("PaperPanel", "PanelContainer")
    paper = t.texture_file("overlay_paper_texture", "res://ShareAsset/Internal/Props/UIDesk/Painterly3D/reference-paper.webp", "")
    t.set("PaperPanel/styles/panel", t.sub("StyleBoxTexture", "overlay_paper_panel", [("texture", paper)]))

    for name, margin in (("HUDMargin", (16, 7, 16, 7)), ("ModalMargin", (32, 26, 32, 24))):
        t.type(name, "MarginContainer")
        for side, value in zip(("left", "top", "right", "bottom"), margin):
            t.set(f"{name}/constants/margin_{side}", value)
    for name, base, separation in (("HUDRow", "HBoxContainer", 20),
                                   ("ModalStack", "VBoxContainer", 12),
                                   ("HeaderRow", "HBoxContainer", 20),
                                   ("BodyStack", "VBoxContainer", 12)):
        t.type(name, base)
        t.set(f"{name}/constants/separation", separation)
    t.type("FooterRow", "HFlowContainer")
    t.set("FooterRow/constants/h_separation", 16)
    t.set("FooterRow/constants/v_separation", 8)

    text_type(t, "HUDLocation", "Label", size=SIZE["body"], color=c("ink"), font=f["deck"], rich=False)
    text_type(t, "HUDCaption", "Label", size=SIZE["note"], color=c("muted"), font=f["register"], rich=False)
    text_type(t, "EditorialCaption", "Label", size=SIZE["note"], color=c("muted"), font=f["register"], rich=False)
    text_type(t, "EditorialTitle", "Label", size=SIZE["title"], color=c("ink"), font=f["deck"], rich=False)
    text_type(t, "EditorialSubtitle", "Label", size=SIZE["sub"], color=c("ink"), font=f["deck"], rich=False)
    text_type(t, "ManuscriptBody", "RichTextLabel", size=SIZE["body"], color=c("ink"), font=f["body"], bold=f["deck"])
    text_type(t, "ManuscriptNote", "RichTextLabel", size=SIZE["body"], color=c("hand"), font=f["hand"], bold=f["hand"])
    for name in ("ManuscriptBody", "ManuscriptNote"):
        t.set(f"{name}/constants/line_separation", 6)
    text_type(t, "FieldHint", "Label", size=SIZE["body"], color=c("ink"), font=f["body"], rich=False,
              style=t.box("field_hint", bg=c("paper", 0.96), border=c("rule", 0.55),
                          width=(0, 0, 0, 1), margin=(16, 8, 16, 8)))
    tooltip = t.box("field_tooltip", bg=c("paper"), border=c("rule", 0.65),
                    width=(1, 1, 1, 1), margin=(12, 6, 12, 6),
                    shadow=c("ink", 0.18), shadow_size=4, shadow_offset=(0, 2))
    text_type(t, "FieldTooltip", "Label", size=SIZE["note"], color=c("ink"), font=f["body"], rich=False, style=tooltip)
    t.set("TooltipPanel/styles/panel", tooltip)
    text_type(t, "TooltipLabel", "Label", size=SIZE["note"], color=c("ink"), font=f["body"], rich=False)

    focus = t.box("overlay_focus", border=c("hand", 0.85), width=(0, 0, 0, 2))
    for name, size, font, margin, color in (
        ("Button", SIZE["body"], f["body"], (10, 7, 10, 7), c("ink")),
        ("QuietAction", SIZE["note"], f["register"], (8, 4, 8, 4), c("muted")),
        ("TextAction", SIZE["body"], f["hand"], (12, 8, 12, 8), c("hand")),
        ("ChoiceRow", SIZE["body"], f["body"], (12, 12, 12, 12), c("ink")),
    ):
        rule_width = (0, 0, 0, 1) if name == "ChoiceRow" else (0, 0, 0, 0)
        plain = t.box(name + "_plain", border=c("rule", 0.45), width=rule_width, margin=margin)
        hover = t.box(name + "_hover", bg=c("paper_dark", 0.42), border=c("hand", 0.7),
                      width=(0, 0, 0, 1), margin=margin)
        pressed = t.box(name + "_pressed", bg=c("paper_dark", 0.72), border=c("hand"),
                        width=(0, 0, 0, 1), margin=margin)
        button_type(t, name, "Button", normal=plain, hover=hover, pressed=pressed,
                    disabled=plain, focus=focus, color=color, disabled_color=c("muted", 0.55),
                    size=size, font=font)
    t.set("HSeparator/styles/separator", t.box("overlay_rule", border=c("rule", 0.65),
          width=(0, 1, 0, 0), margin=(0, 3, 0, 3)))
    t.set("HSeparator/constants/separation", 8)
    return t


def main() -> int:
    check = "--check" in sys.argv
    outputs = {"DialogueTheme.tres": dialogue_theme().render(), "TitleTheme.tres": title_theme().render(),
               "OverlayTheme.tres": overlay_theme().render(), **native_icons()}
    stale = []
    for name, text in outputs.items():
        path = STYLES / name
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
