#!/usr/bin/env python3
"""`.story` 的“设计数据”块：词汇、时间、文本、案件、倾向、物件规则等。

这些块不翻译成 world_script 源码，而是直接编译成章节 JSON 的 `design` 子树；
运行时（C#）与宿主（GDScript，经 C# 转发）只读这棵树，不再各自手写一份。
块的写法见 artifacts/unified-story-script/spec-draft.md 第 9 节。
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field
from typing import Any

from story_base import Line, StoryError, children, split_list, split_pair


# ---------------------------------------------------------------- 通用读取 --

@dataclass
class Node:
    line: Line
    kids: list["Node"] = field(default_factory=list)

    @property
    def text(self) -> str:
        return self.line.text

    @property
    def number(self) -> int:
        return self.line.number

    def pair(self) -> tuple[str, str]:
        return split_pair(self.line)

    @property
    def is_item(self) -> bool:
        return self.line.text.startswith("- ")

    @property
    def item(self) -> str:
        return self.line.text[2:].strip()


def nodes(body: list[Line]) -> list[Node]:
    """把按缩进排列的行读成树。"""
    result: list[Node] = []
    i = 0
    while i < len(body):
        line = body[i]
        sub, i = children(body, i + 1, line.indent)
        for kid in sub:
            if kid.indent < line.indent + 1:
                raise StoryError("缩进错误。", kid.number)
        result.append(Node(line, nodes(sub)))
    return result


def unescape(text: str) -> str:
    return text.replace("\\n", "\n")


def kv_map(body: list[Node], what: str) -> dict[str, str]:
    """`键：值` 逐行，值为文字。"""
    result: dict[str, str] = {}
    for node in body:
        key, value = node.pair()
        if key in result:
            raise StoryError(f"{what}的键“{key}”重复。", node.number)
        if not value:
            raise StoryError(f"{what}的“{key}”缺少内容。", node.number)
        result[key] = unescape(value)
    return result


def rows(body: list[Node], width: int, what: str) -> list[list[str]]:
    """`- a | b | c` 表格行。"""
    result = []
    for node in body:
        if not node.is_item:
            raise StoryError(f"{what}每行须以“- ”开头。", node.number)
        cells = [cell.strip() for cell in node.item.split("|")]
        if len(cells) != width or any(not cell for cell in cells):
            raise StoryError(f"{what}每行须有 {width} 列，用“|”分隔：{node.item}", node.number)
        result.append(cells)
    return result


def parse_number(word: str, what: str, line: int) -> int | float:
    try:
        number = float(word)
    except ValueError:
        raise StoryError(f"{what}必须是数字：{word}", line) from None
    return int(number) if number.is_integer() and "." not in word else number


def need_int(word: str, what: str, line: int) -> int:
    try:
        return int(word)
    except ValueError:
        raise StoryError(f"{what}必须是整数：{word}", line) from None


# -------------------------------------------------------------------- 词汇 --

VOCAB_BLOCKS = {"阵营": "groups", "体型": "builds", "时段": "slots", "行动": "jobs", "价值": "flags", "群落": "clusters"}
VOCAB_COLUMNS = {
    "groups": ("word", "id"),
    "builds": ("word", "id", "figure"),
    "slots": ("word", "id", "name"),
    "jobs": ("word", "id", "text", "frame"),
    "flags": ("word", "id", "text"),
    "clusters": ("word", "id"),
}
FRAMES = {"坐", "递物", "操作", "拉拽", "蹲查", "看守", "行走", "交谈"}


class Vocab:
    """故事自己声明的词汇：关键词 → C# 稳定名。第二章只认这里声明的词。"""

    def __init__(self) -> None:
        self.tables: dict[str, list[dict[str, str]]] = {kind: [] for kind in VOCAB_COLUMNS}

    def add(self, keyword: str, body: list[Node], line: Line) -> None:
        kind = VOCAB_BLOCKS[keyword]
        columns = VOCAB_COLUMNS[kind]
        table = self.tables[kind]
        for row in rows(body, len(columns), keyword):
            entry = dict(zip(columns, row))
            if any(e["word"] == entry["word"] or e["id"] == entry["id"] for e in table):
                raise StoryError(f"{keyword}词汇“{entry['word']}/{entry['id']}”重复。", line.number)
            if kind == "jobs" and entry["frame"] not in FRAMES:
                raise StoryError(f"动作帧“{entry['frame']}”未知。可用：" + "、".join(sorted(FRAMES)), line.number)
            table.append(entry)

    def mapping(self, kind: str) -> dict[str, str]:
        return {e["word"]: e["id"] for e in self.tables[kind]}

    def ids(self, kind: str) -> set[str]:
        return {e["id"] for e in self.tables[kind]}

    def empty(self) -> bool:
        return not any(self.tables.values())


# -------------------------------------------------------------------- 时间 --

DAY_TOKEN = re.compile(r"\{日(?:-1)?\}")


def compile_time(body: list[Node], vocab: Vocab, line: Line) -> dict[str, Any]:
    time: dict[str, Any] = {"day_names": {}, "slot_alias": [], "days": {}}
    slot_words = vocab.mapping("slots")
    for node in body:
        key, value = node.pair()
        if key == "日名":
            for kid in node.kids:
                dkey, dvalue = kid.pair()
                if dkey == "其余":
                    if not DAY_TOKEN.search(dvalue):
                        raise StoryError("“其余”须含 {日} 或 {日-1}。", kid.number)
                    time["day_other"] = dvalue
                else:
                    time["day_names"][str(need_int(dkey, "日名的日序", kid.number))] = dvalue
        elif key == "时段别称":
            for kid in node.kids:
                if not kid.is_item:
                    raise StoryError("时段别称每行须以“- ”开头。", kid.number)
                m = re.fullmatch(r"第\s*(\d+)\s*日\s+(\S+)：(.+)", kid.item)
                if not m:
                    raise StoryError("时段别称须写成“- 第 2 日 早晨：显示名”。", kid.number)
                if m[2] not in slot_words:
                    raise StoryError(f"未知时段“{m[2]}”。", kid.number)
                time["slot_alias"].append({"day": int(m[1]), "slot": slot_words[m[2]], "name": m[3].strip()})
        elif key in {"时段分钟", "首日時段分钟"}:
            ranges = {}
            for kid in node.kids:
                parts = [part.strip() for part in kid.item.split("|")] if kid.is_item else []
                if len(parts) != 3 or parts[0] not in slot_words:
                    raise StoryError("时段分钟须写成“- 时段 | 起始分钟 | 结束分钟”。", kid.number)
                slot = slot_words[parts[0]]
                start, end = (need_int(v, "时段分钟", kid.number) for v in parts[1:])
                if slot in ranges or not 0 <= start < end <= 2880:
                    raise StoryError("时段重复或分钟范围无效。", kid.number)
                ranges[slot] = [start, end]
            if set(ranges) != set(slot_words.values()):
                raise StoryError("时段分钟须覆盖全部时段。", node.number)
            time["slot_minutes" if key == "时段分钟" else "first_day_slot_minutes"] = ranges
        elif key == "日程":
            for kid in node.kids:
                dkey, dvalue = kid.pair()
                time["days"][dkey] = need_int(dvalue, f"日程“{dkey}”", kid.number)
        elif key in {"开场白", "操作说明"}:
            time["intro" if key == "开场白" else "instructions"] = unescape(value)
        else:
            raise StoryError(f"时间块不支持“{key}”。可用：日名、时段别称、时段分钟、首日時段分钟、日程、开场白、操作说明。", node.number)
    for need in ("day_other", "intro", "instructions"):
        if need not in time:
            raise StoryError(f"时间块缺少“{ {'day_other': '日名·其余', 'intro': '开场白', 'instructions': '操作说明'}[need] }”。", line.number)
    for need in ("首日", "案发日", "末日"):
        if need not in time["days"]:
            raise StoryError(f"时间块的“日程”缺少“{need}”。", line.number)
    return time


# -------------------------------------------------------------------- 文本 --

PLACEHOLDER = re.compile(r"\{([^{}]+)\}")


def compile_text(body: list[Node]) -> dict[str, Any]:
    """`文本` 块：分组缩进，叶子是 `键：文字` 或 `键：` + `- 条目`。键用“组.键”平铺。"""
    flat: dict[str, Any] = {}

    def walk(items: list[Node], prefix: str) -> None:
        for node in items:
            key, value = node.pair()
            path = f"{prefix}{key}"
            if node.kids and node.kids[0].is_item:
                if value:
                    raise StoryError(f"“{key}”既写了内容又写了条目。", node.number)
                flat_add(path, [unescape(kid.item) for kid in node.kids], node.number)
            elif node.kids:
                if value:
                    raise StoryError(f"“{key}”既写了内容又写了子项。", node.number)
                walk(node.kids, path + ".")
            else:
                if not value:
                    raise StoryError(f"文本“{path}”缺少内容。", node.number)
                flat_add(path, unescape(value), node.number)

    def flat_add(path: str, value: Any, number: int) -> None:
        if path in flat:
            raise StoryError(f"文本“{path}”重复。", number)
        flat[path] = value

    walk(body, "")
    return flat


# -------------------------------------------------------------------- 人物 --

PERSON_FIELDS = {"群落", "值守", "工作地点", "信任", "立绘"}


def compile_person(name: str, fields: dict[str, tuple[str, Line, list[Line]]], ctx: Any, head: Line) -> dict[str, Any]:
    """人物块里属于“设计数据”而非 Core 画像的字段：群落、值守、工作地点、初始信任、立绘。"""
    data: dict[str, Any] = {}
    if "群落" in fields:
        word, line, _ = fields["群落"]
        clusters = ctx.vocab.mapping("clusters")
        if word not in clusters:
            raise StoryError(f"未知群落“{word}”。可用：" + "、".join(clusters), line.number)
        data["cluster"] = clusters[word]
    if "值守" in fields:
        word, line, _ = fields["值守"]
        if word not in {"是", "否"}:
            raise StoryError("值守必须是“是”或“否”。", line.number)
        data["on_duty"] = word == "是"
    if "工作地点" in fields:
        word, line, _ = fields["工作地点"]
        if word not in ctx.dorms:
            raise StoryError(f"工作地点引用了未声明的地点“{word}”。", line.number)
        data["work_place"] = ctx.dorms[word]
    if "立绘" in fields:
        data["sprite"] = fields["立绘"][0]
    if "信任" in fields:
        value, line, sub = fields["信任"]
        trust: dict[str, int] = {}
        entries = split_list(value) if value else [kid.text.lstrip("- ").strip() for kid in sub]
        for entry in entries:
            parts = entry.rsplit(None, 1)
            if len(parts) != 2 or parts[0] not in ctx.person_enum:
                raise StoryError(f"“信任”需要“人物 数值”，人物须已声明：{entry}", line.number)
            trust[ctx.person_enum[parts[0]]] = need_int(parts[1], "信任数值", line.number)
        data["trust"] = trust
    return data


# ---------------------------------------------------------------- 入口汇总 --

DESIGN_BLOCKS = set(VOCAB_BLOCKS) | {"时间", "文本", "常数", "规矩", "规矩格", "结局", "案件"}


def compile_design(blocks: list[tuple[Line, list[Line]]], ctx: Any) -> dict[str, Any]:
    from story_rules import compile_card_kit, compile_case, compile_endings, compile_rules  # 避免循环引用

    vocab: Vocab = ctx.vocab
    design: dict[str, Any] = {}
    texts: dict[str, Any] = {}
    rule_blocks = [(head, nodes(body)) for head, body in blocks if head.text.split(None, 1)[0] == "规矩"]
    if rule_blocks:
        design["rules"] = compile_rules(rule_blocks, ctx)
    rule_ids = {r["name"]: r["id"] for r in design.get("rules", [])}
    for head, body in blocks:
        keyword = head.text.split(None, 1)[0]
        tree = nodes(body)
        if keyword in VOCAB_BLOCKS or keyword == "规矩":
            continue
        if keyword == "时间":
            if "time" in design:
                raise StoryError("时间块只能有一个。", head.number)
            design["time"] = compile_time(tree, vocab, head)
        elif keyword == "常数":
            consts = design.setdefault("consts", {})
            for node in tree:
                key, value = node.pair()
                if key in consts:
                    raise StoryError(f"常数“{key}”重复。", node.number)
                consts[key] = parse_number(value, f"常数“{key}”", node.number)
        elif keyword == "规矩格":
            design["card_kit"] = compile_card_kit(tree, ctx, head)
        elif keyword == "结局":
            design["endings"] = compile_endings(tree)
        elif keyword == "案件":
            design["case"] = compile_case(tree, ctx, rule_ids, head)
        elif keyword == "文本":
            for key, value in compile_text(tree).items():
                if key in texts:
                    raise StoryError(f"文本“{key}”重复。", head.number)
                texts[key] = value
        else:
            raise StoryError(f"未知设计块“{keyword}”。", head.number)
    if "rules" in design:
        from story_rules import resolve_card
        for rule in design["rules"]:
            line = rule.pop("card_line", None)
            if "card" in rule:
                if "card_kit" not in design:
                    raise StoryError("规矩用了“格”，但没有“规矩格”块。", line or 0)
                rule["card"] = resolve_card(design["card_kit"], [c.strip() for c in rule["card"].split("|")], line or 0)
    if getattr(ctx, "person_design", None):
        design["people"] = ctx.person_design
    if texts:
        design["text"] = texts
    if not vocab.empty():
        design["vocab"] = {kind: table for kind, table in vocab.tables.items() if table}
    return design
