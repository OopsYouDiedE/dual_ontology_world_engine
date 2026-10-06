#!/usr/bin/env python3
"""第一章 `.story` 读取器：把 chapter_01.story 读成 compile_story 需要的章节数据。

块（顶格，正文缩进 4 格，`#` 为注释）：
  章节：<id>
  证物 / 生活痕迹 / 热点 / 额外热点 / 摆设          调查与布局数据
  名单、对照、坐标、点位框、常量、推演变量         各类设定表
  场面 <编号>                                       对白、选择、条件、效果、跳转
  文案 / 守则 / 阶段 <名> / 阶段事件 / 流程 <名> / 判定 <步> 以及后续规则块，见 artifacts/unified-story-script/spec-draft.md §8。
"""
from __future__ import annotations

import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parent))
from engine_spec import (ChapterSpec, Expr, Instruction, binary, call, choice_option, choices,  # noqa: E402
                         condition, effect, entry, jump, line as dialogue_line, literal, unary)
from story_script import Line, StoryError, children, read_lines, split_list, split_pair  # noqa: E402


@dataclass(frozen=True)
class Evidence:
    id: str
    name: str
    place: str
    source: str
    facts: str
    experiment: str | None = None
    experiment_result: str | None = None
    help: str | None = None
    recall: str | None = None
    picture: str | None = None
    room: str | None = None
    mind_name: str | None = None
    world_facts: list | None = None
    compare_place: str | None = None
    compare_region: str | None = None
    on_examine: str | None = None
    recall_fragment: str | None = None
    recall_confirms: list | None = None
    experiment_mode: str | None = None
    repeatable: bool | None = None
    addenda: list | None = None
    compare_when: dict | None = None

    def json(self) -> dict[str, Any]:
        return {key: value for key, value in self.__dict__.items() if key != "id" and value is not None}


@dataclass(frozen=True)
class LifeTrace:
    id: str
    title: str
    room: str
    picture: str
    facts: str
    x: int
    y: int
    reach: float


@dataclass(frozen=True)
class Site:
    id: str
    evidence: str
    x: int
    y: int
    reach: float
    title: str
    click: str = "evidence"


@dataclass(frozen=True)
class Prop:
    node: str
    texture: str
    x: int
    y: int
    z_y: float


QUERY_NAMES = {
    "detective_name": "侦探名", "seen": "见过", "tested": "试过", "asked": "问过", "flag": "旗",
    "recalled": "回想过", "compared": "比对过", "v6_open": "六问已开", "told_rule": "告知规则",
    "hooks": "钩数", "total_hooks": "总钩数", "matched": "已吻合", "replayed": "已重演",
    "fragment": "片段", "alone_choice": "独处选择", "life_thought": "生活所思", "talk_first": "先开口",
    "recall_thought": "回想所思", "wrong_name": "错名", "addr": "称呼", "heard": "所闻",
    "heard_where": "所闻之处", "sound_sentence": "声路句子",
    "has": "有记录", "count": "记录数", "value": "记录值", "replay_count": "重演次数", "full_match": "重演吻合",
    "replay_refused": "重演被拒", "in_room": "在房间", "local": "同处", "busy": "忙碌",
    "said": "陈述", "shown": "出示", "is_person": "是同伴", "geo_reach": "几何可达",
    "acquaintance_seen": "相识已查", "replay_var": "重演变量", "bolted": "重演侦探闩门",
}
QUERY_BY_ZH = {zh: en for en, zh in QUERY_NAMES.items()}
EFFECT_WORDS = {"设旗": "set_flag", "清旗": "clear_flag", "放大": "amplify", "低语": "whisper", "共享回忆": "share_recall"}
COMPARE = {"==": "eq", "!=": "ne", ">": "gt", ">=": "gte", "<": "lt", "<=": "lte"}

# 设定表：块名 -> (CONTENT_TABLES 键或独立属性, 形式)
TABLES = {
    "地点名单": ("PLACES", "list"), "人名": ("PERSON_NAMES", "map"), "演员节点": ("ACTOR_NODES", "map"),
    "房间名": ("ROOM_NAMES", "map"), "陈述": ("STATEMENTS", "map"), "低语": ("WHISPERS", "map"),
    "协助说明": ("HELP_NOTES", "map"), "家具旁白": ("FURNITURE_ASIDES", "map"),
    "岗位坐标": ("POSTS", "points"), "演出点": ("STAGING", "points"), "物品立绘": ("OBJECT_SPRITES", "map"), "自带物件": ("OWN_OBJECTS", "map"),
    "回忆物件": ("RECALL_OBJECTS", "list"), "相识证物": ("ACQUAINTANCE_EVIDENCE", "list"),
    "对白演员": ("DIALOGUE_CAST", "map"), "声路站位": ("SOUND_SPOTS", "map"), "同伴站位": ("COMPANION_SPOTS", "map"),
    "站位候补": ("SPOT_ORDER", "list"), "鞋底对象": ("SOLE_TARGETS", "map"), "点位框": ("SITE_BOXES", "boxes"), "布局常量": ("LAYOUT_CONSTANTS", "constants"),
}
CONTENT_KEYS = ("PLACES", "PERSON_NAMES", "ACTOR_NODES", "ROOM_NAMES", "VARIABLES", "STATEMENTS", "WHISPERS", "HELP_NOTES",
                "SOUND_SPOTS", "COMPANION_SPOTS", "SPOT_ORDER", "SOLE_TARGETS")


class Chapter1Source:
    def __init__(self) -> None:
        self.EVIDENCE: list[Evidence] = []
        self.LIFE_TRACES: list[LifeTrace] = []
        self.SITES: list[Site] = []
        self.PROPS: list[Prop] = []
        self.EXTRA_SITES: list[dict[str, Any]] = []
        self.CONTENT_TABLES: dict[str, Any] = {}
        self.entries: list[tuple[str, list[Instruction]]] = []
        self.chapter_id = ""
        self.RULES_DATA: dict[str, Any] = {}
        for _, (name, _) in TABLES.items():
            if name not in CONTENT_KEYS:
                setattr(self, name, {} if _ != "list" else ())
        self.CHAPTER: ChapterSpec | None = None


def num(word: str, what: str, number: int):
    try:
        value = float(word)
    except ValueError:
        raise StoryError(f"{what}必须是数字：{word}", number) from None
    return int(value) if "." not in word and "e" not in word.lower() else value


def numbers(text: str, what: str, number: int) -> list:
    return [num(part.strip(), what, number) for part in re.split(r"[,，]", text) if part.strip()]


def text_of(value: str, sub: list[Line]) -> str:
    return value if value else "\n".join(item.text for item in sub)


# ---- 表达式 ----

TOKEN = re.compile(r'\s*(?:(?P<str>"[^"]*")|(?P<num>-?\d+(?:\.\d+)?)|(?P<op>==|!=|>=|<=|>|<)|(?P<p>[(),])|(?P<word>[^\s(),"=!<>]+))')


def tokenize(text: str, number: int) -> list[tuple[str, str]]:
    tokens, pos = [], 0
    text = text.strip()
    while pos < len(text):
        m = TOKEN.match(text, pos)
        if not m or m.end() == pos:
            raise StoryError(f"无法解析表达式：{text}", number)
        pos = m.end()
        kind = m.lastgroup
        tokens.append((kind, m.group(kind)))
    return tokens


class ExprParser:
    def __init__(self, text: str, number: int):
        self.tokens = tokenize(text, number)
        self.pos = 0
        self.number = number
        self.text = text

    def peek(self):
        return self.tokens[self.pos] if self.pos < len(self.tokens) else (None, None)

    def take(self):
        token = self.peek()
        self.pos += 1
        return token

    def fail(self, why: str):
        raise StoryError(f"{why}：{self.text}", self.number)

    def parse(self) -> Expr:
        result = self.or_expr()
        if self.pos != len(self.tokens):
            self.fail("表达式末尾有多余内容")
        return result

    def or_expr(self) -> Expr:
        left = self.and_expr()
        while self.peek() == ("word", "或"):
            self.take()
            left = binary("or", left, self.and_expr())
        return left

    def and_expr(self) -> Expr:
        left = self.not_expr()
        while self.peek() == ("word", "且"):
            self.take()
            left = binary("and", left, self.not_expr())
        return left

    def not_expr(self) -> Expr:
        if self.peek() == ("word", "非"):
            self.take()
            return unary("not", self.not_expr())
        return self.compare()

    def compare(self) -> Expr:
        left = self.primary()
        kind, value = self.peek()
        if kind == "op":
            self.take()
            return binary(COMPARE[value], left, self.primary())
        return left

    def primary(self) -> Expr:
        kind, value = self.take()
        if kind == "p" and value == "(":
            inner = self.or_expr()
            if self.take() != ("p", ")"):
                self.fail("缺少右括号")
            return inner
        if kind == "str":
            return literal(value[1:-1])
        if kind == "num":
            return literal(int(value) if "." not in value else float(value))
        if kind == "word":
            name = QUERY_BY_ZH.get(value, value)
            args: list[Expr] = []
            if self.peek() == ("p", "("):
                self.take()
                while self.peek() != ("p", ")"):
                    args.append(self.primary())
                    if self.peek() == ("p", ","):
                        self.take()
                self.take()
            return call(name, *args)
        self.fail("表达式不完整")


def parse_expr(text: str, number: int) -> Expr:
    return ExprParser(text, number).parse()


INTERP = re.compile(r"\{\{\s*([^{}(\s]+)(\([^{}]*\))?\s*\}\}")


def restore_interpolation(text: str) -> str:
    return INTERP.sub(lambda m: "{{" + QUERY_BY_ZH.get(m[1], m[1]) + (m[2] or "") + "}}", text)


# ---- 场面 ----

def parse_body(lines: list[Line], start: int, depth: int, entry_id: str) -> tuple[list[Any], int]:
    """解析缩进为 depth 的指令序列，返回指令列表与结束位置。"""
    result: list[Any] = []
    i = start
    while i < len(lines) and lines[i].indent >= depth:
        line = lines[i]
        if line.indent != depth:
            raise StoryError("缩进错误。", line.number)
        text = line.text
        if text.startswith("若 "):
            test_text = text[2:].rstrip("：:").strip()
            body_end = children(lines, i + 1, depth)
            then, _ = parse_body(body_end[0], 0, depth + 1, entry_id)
            i = body_end[1]
            else_part: list[Any] = []
            if i < len(lines) and lines[i].indent == depth and lines[i].text.startswith("否则"):
                else_part, i = parse_else(lines, i, depth, entry_id)
            result.append(condition(parse_expr(test_text, line.number), then, else_part))
        elif text.startswith("否则"):
            raise StoryError("“否则”前面必须有“若”。", line.number)
        elif text.startswith("跳转 "):
            result.append(jump(text[3:].strip()))
            i += 1
        elif text.startswith("选择"):
            sub, i = children(lines, i + 1, depth)
            result.append(choices(parse_options(sub, depth + 1, entry_id)))
        else:
            word = text.split(None, 1)[0]
            if word in EFFECT_WORDS:
                name = EFFECT_WORDS[word]
                rest = text[len(word):].strip()
                if name in {"amplify", "whisper"}:
                    who, content = split_pair(Line(line.number, 0, rest))
                    result.append(effect(name, who, content))
                else:
                    result.append(effect(name, rest))
            else:
                result.append(dialogue(line))
            i += 1
    return result, i


def parse_else(lines: list[Line], i: int, depth: int, entry_id: str) -> tuple[list[Any], int]:
    line = lines[i]
    rest = line.text[2:].lstrip().rstrip("：:").strip() if False else line.text
    sub, nxt = children(lines, i + 1, depth)
    if line.text.startswith("否则 若 "):
        test_text = line.text[len("否则 若 "):].rstrip("：:").strip()
        then, _ = parse_body(sub, 0, depth + 1, entry_id)
        else_part: list[Any] = []
        if nxt < len(lines) and lines[nxt].indent == depth and lines[nxt].text.startswith("否则"):
            else_part, nxt = parse_else(lines, nxt, depth, entry_id)
        return [condition(parse_expr(test_text, line.number), then, else_part)], nxt
    body, _ = parse_body(sub, 0, depth + 1, entry_id)
    return body, nxt


def parse_options(lines: list[Line], depth: int, entry_id: str) -> list[dict[str, Any]]:
    options = []
    i = 0
    while i < len(lines):
        line = lines[i]
        if line.indent != depth or not line.text.startswith("- "):
            raise StoryError("选项须写成“- 选项文字”。", line.number)
        sub, i = children(lines, i + 1, depth)
        test = None
        hint = ""
        while sub and sub[0].indent == depth + 1 and sub[0].text.startswith(("条件：", "锁定提示：")):
            key, value = split_pair(sub[0])
            if key == "条件":
                test = parse_expr(value, sub[0].number)
            else:
                hint = restore_interpolation(value)
            sub = sub[1:]
        body, _ = parse_body(sub, 0, depth + 1, entry_id) if sub else ([], 0)
        options.append(choice_option("", restore_interpolation(line.text[2:].strip()), body, test, hint))
    return options


def dialogue(line: Line):
    text = line.text
    if text.startswith("（"):
        speaker, content = "", text
    else:
        speaker, content = split_pair(line)
    mood = ""
    m = re.search(r"\s*\[#emotion=([A-Za-z_]+)\]\s*$", content)
    if m:
        mood, content = m[1], content[:m.start()]
    return dialogue_line(restore_interpolation(speaker), restore_interpolation(content), mood)


# ---- 规则块：文案、阶段、流程、判定 ----

def yes_no(value: str, what: str, number: int) -> bool:
    if value in {"是", "真", "true"}:
        return True
    if value in {"否", "假", "false"}:
        return False
    raise StoryError(f"{what}必须是“是”或“否”：{value}", number)


def scalar(text: str) -> Any:
    """流程里的值：true/false、数字，其余是字符串；`文案:键` 取文案表。"""
    if text in {"true", "是"}:
        return True
    if text in {"false", "否"}:
        return False
    if text.startswith("文案:"):
        return {"text": text[3:].strip()}
    if re.fullmatch(r"-?\d+", text):
        return int(text)
    if re.fullmatch(r"-?\d+\.\d+", text):
        return float(text)
    return text


def read_text_map(body: list[Line]) -> dict[str, str]:
    """`键：文字`，文字可接在下一行缩进处（多行以换行连接）。"""
    result: dict[str, str] = {}
    j = 0
    while j < len(body):
        line = body[j]
        key, value = split_pair(line)
        sub, j = children(body, j + 1, line.indent)
        if key in result:
            raise StoryError(f"文案键“{key}”重复。", line.number)
        result[key] = text_of(value, sub)
    return result


EXPERIMENT_MODES = {"普通": "plain", "声路": "sound", "逐人鞋底": "sole", "逐人手掌": "hand"}
EXAMINE_EFFECTS = {"解锁重演": "unlock_replay"}
CLICK_KINDS = {"证物卡": "evidence", "铜口菜单": "tube"}


def parse_addendum(item: Line) -> dict[str, Any]:
    """`若 <条件>：<文字>`：条件成立时接在证物现象之后。"""
    if not item.text.startswith("若 "):
        raise StoryError("补注须写成“若 <条件>：<文字>”。", item.number)
    condition, sep, text = item.text[2:].partition("：")
    if not sep:
        raise StoryError("补注缺少“：”。", item.number)
    return {"condition": parse_expr(condition.strip(), item.number).json(), "text": text.strip()}


SOLES = {"软底": "Soft", "马球靴": "PoloBoot", "工装靴": "WorkBoot"}
TOWARDS = {"插闩": "Bolt", "门缝": "DoorGap"}
TRUTH_FIELDS = {
    "内闩落下": "InnerBoltDown", "灰扇": "AshFan", "楔面沾灰": "WedgeTopSooted", "楔在角落": "WedgeInCorner",
    "鞋印": "Prints", "湿布": "Cloth", "湿布压鞋跟": "ClothOverHeel", "音叉在门框旁": "ForkByFrame",
    "门框凹痕": "FrameDent", "乐谱第一行": "ScoreLine1", "乐谱第二行": "ScoreLine2",
    "石板第一行": "SlateLine1", "石板第二行": "SlateLine2", "杯糖": "CupSugar",
}


def parse_facts(text: str, number: int) -> list[dict[str, Any]]:
    """`标签 = 1` 或 `标签 = 真相.键` 或 `标签 = 真相.键 是 n`（等于 n 时为 1）。"""
    facts = []
    for part in split_list(text):
        tag, eq, value = part.partition("=")
        tag, value = tag.strip(), value.strip()
        if not eq:
            raise StoryError(f"世界事实需写成“标签 = 值”：{part}", number)
        if value.startswith("真相."):
            key, _, equals = value[3:].partition(" 是 ")
            key = key.strip()
            if key not in TRUTH_FIELDS:
                raise StoryError(f"真相里没有“{key}”。", number)
            fact: dict[str, Any] = {"tag": tag, "truth": TRUTH_FIELDS[key]}
            if equals:
                fact["equals"] = int(equals)
            facts.append(fact)
        else:
            facts.append({"tag": tag, "value": int(value)})
    return facts


def read_truth(body: list[Line]) -> dict[str, Any]:
    truth: dict[str, Any] = {}
    for item in body:
        key, value = split_pair(item)
        if key not in TRUTH_FIELDS:
            raise StoryError(f"真相里不认识“{key}”。可用：" + "、".join(TRUTH_FIELDS), item.number)
        field = TRUTH_FIELDS[key]
        if field == "Prints":
            prints = []
            for part in split_list(value):
                sole, _, toward = part.partition("朝")
                if sole.strip() not in SOLES or toward.strip() not in TOWARDS:
                    raise StoryError(f"鞋印写成“马球靴 朝插闩”：{part}", item.number)
                prints.append({"sole": SOLES[sole.strip()], "toward": TOWARDS[toward.strip()]})
            truth[field] = prints
        elif value in {"是", "否"}:
            truth[field] = value == "是"
        else:
            truth[field] = num(value, key, item.number)
    return truth


VOICE_MEANINGS = {"闩门": "Bolt", "禁开": "ForbidOpen", "守铜口": "HoldTube", "开门": "Open", "求救": "Help"}


def read_utterance(head: Line, body: list[Line]) -> dict[str, Any]:
    fields = read_fields(body)
    meanings = split_list(fields["含义"][0]) if "含义" in fields else []
    for word in meanings:
        if word not in VOICE_MEANINGS:
            raise StoryError(f"话语含义不认识“{word}”。可用：" + "、".join(VOICE_MEANINGS), head.number)
    return {"full": fields["整句"][0], "tail": fields["句尾"][0], **{name: zh in meanings for zh, name in VOICE_MEANINGS.items()}}


def read_regions(body: list[Line]) -> list[dict[str, Any]]:
    """`名称 [区域后缀]：x0, y0, x1, y1`，`*` 为不设限。"""
    regions = []
    for item in body:
        key, value = split_pair(item)
        name, _, suffix = key.partition(" ")
        bounds = []
        for part in [p.strip() for p in re.split(r"[,，]", value)]:
            bounds.append(None if part == "*" else num(part, "区域坐标", item.number))
        if len(bounds) != 4:
            raise StoryError("房间区域需要四个坐标：x0, y0, x1, y1。", item.number)
        regions.append({"name": name, "region": name + ("_" + suffix.strip() if suffix.strip() else ""), "rect": bounds})
    return regions


HEARINGS = {"完整": "Full", "隔门完整": "FullMuffled", "句尾": "Tail", "低语": "Murmur", "仅警铃": "AlarmOnly", "无": "Nothing"}
ORIGINS = {"铜口": "Tube", "门外": "OutsideDoor"}


def finalize_acoustics(source) -> None:
    """声路表：来源 → 听者房间键 → 听感；房间按 `房间名` 反查，“其他”写作 `*`。"""
    data = source.RULES_DATA
    if "_ACOUSTIC_RAW" not in data:
        return
    names = {name: key for key, name in source.CONTENT_TABLES["ROOM_NAMES"].items()}

    def room(name: str, number: int) -> str:
        if name not in names:
            raise StoryError(f"声路里的房间“{name}”不在房间名表里。", number)
        return names[name]

    table: dict[str, dict[str, str]] = {}
    for origin, rows in data.pop("_ACOUSTIC_RAW").items():
        if origin not in ORIGINS:
            raise StoryError(f"声路来源“{origin}”不认识。可用：" + "、".join(ORIGINS), rows[0][1] if rows else 0)
        entry: dict[str, str] = {}
        for (rooms, hearing), number in rows:
            if hearing not in HEARINGS:
                raise StoryError(f"听感“{hearing}”不认识。可用：" + "、".join(HEARINGS), number)
            for name in split_list(rooms):
                entry["*" if name == "其他" else room(name, number)] = HEARINGS[hearing]
        table[ORIGINS[origin]] = entry
    lists = data.pop("_ACOUSTIC_LISTS", {})
    outside, number = lists.get("听来像在门外", ([], 0))
    shout, shout_number = lists.get("喊声可闻", ([], 0))
    data["ACOUSTICS"] = {
        "hearing": table,
        "seems_outside": [HEARINGS[h] for h in outside],
        "hears_shout": [room(r, shout_number) for r in shout],
    }


PERSON_FIELDS = {"欲望": "desire"}


def read_person(fields) -> dict[str, Any]:
    person: dict[str, Any] = {}
    for key, (value, line, sub) in fields.items():
        if key == "编号":
            continue
        if key not in PERSON_FIELDS:
            raise StoryError(f"人物里不认识“{key}”。", line.number)
        person[PERSON_FIELDS[key]] = text_of(value, sub)
    return person


def parse_flow(lines: list[Line], depth: int) -> list[dict[str, Any]]:
    steps: list[dict[str, Any]] = []
    i = 0
    while i < len(lines):
        line = lines[i]
        if line.indent != depth:
            raise StoryError("缩进错误。", line.number)
        text = line.text
        word, _, rest = text.partition(" ")
        rest = rest.strip()
        sub, nxt = children(lines, i + 1, depth)
        if word == "若":
            node: dict[str, Any] = {"op": "if", "condition": parse_expr(rest.rstrip("：:").strip(), line.number).json(),
                                   "then": parse_flow(sub, depth + 1), "else": []}
            i = nxt
            tail = node
            while i < len(lines) and lines[i].indent == depth and lines[i].text.startswith("否则"):
                head = lines[i].text
                inner, i2 = children(lines, i + 1, depth)
                if head.startswith("否则 若 "):
                    branch = {"op": "if", "condition": parse_expr(head[len("否则 若 "):].rstrip("：:").strip(), lines[i].number).json(),
                              "then": parse_flow(inner, depth + 1), "else": []}
                    tail["else"] = [branch]
                    tail = branch
                    i = i2
                else:
                    tail["else"] = parse_flow(inner, depth + 1)
                    i = i2
                    break
            steps.append(node)
            continue
        if sub:
            raise StoryError(f"“{word}”后面不能再缩进内容。", line.number)
        if word == "否则":
            raise StoryError("“否则”前面必须有“若”。", line.number)
        if word == "演出":
            steps.append({"op": "show", "scene": rest})
        elif word == "指令":
            steps.append({"op": "show", "scene": "@" + rest})
        elif word == "阶段":
            steps.append({"op": "stage", "value": rest})
        elif word == "设旗":
            steps.append({"op": "set_flag", "key": rest})
        elif word == "清旗":
            steps.append({"op": "clear_flag", "key": rest})
        elif word in {"记录", "移除", "详情"}:
            target, eq, value = rest.partition(" = ")
            parts = target.split()
            need = {"记录": 2, "移除": 2, "详情": 3}[word]
            if len(parts) != need or (word == "移除" and eq) or (word == "详情" and not eq):
                raise StoryError(f"“{word}”的写法不对：{text}", line.number)
            if word == "记录":
                steps.append({"op": "record", "group": parts[0], "key": parts[1], "value": scalar(value.strip()) if eq else True})
            elif word == "移除":
                steps.append({"op": "remove", "group": parts[0], "key": parts[1]})
            else:
                steps.append({"op": "detail", "group": parts[0], "key": parts[1], "field": parts[2], "value": scalar(value.strip())})
        elif word == "清空":
            steps.append({"op": "clear", "group": rest})
        elif word == "执行":
            steps.append({"op": "hook", "name": rest})
        elif word == "继续":
            steps.append({"op": "continue", "flow": rest})
        elif text == "结束":
            steps.append({"op": "stop"})
        else:
            raise StoryError(f"流程里不认识“{word}”。可用：演出、指令、阶段、设旗、清旗、记录、移除、详情、清空、执行、继续、结束、若。", line.number)
        i = nxt
    return steps


def read_stage(name: str, body: list[Line]) -> dict[str, Any]:
    stage: dict[str, Any] = {"enter": {}}
    flags = {"走动": "walk", "检视": "examine", "笔记本": "notebook", "宿舍开放": "dorms", "房间事件": "room_events"}
    for ln in body:
        key, value = split_pair(ln)
        if key == "说明":
            continue
        if key in flags:
            stage[flags[key]] = yes_no(value, key, ln.number)
        elif key == "可见热点":
            stage["sites"] = "all" if value == "全部" else split_list(value)
        elif key.startswith("进入 "):
            room = key[3:].strip()
            goto, _, signal = value.partition("，")
            stage["enter"][room] = {"stage": goto.strip().removeprefix("阶段 ").strip(), "signal": signal.strip().removeprefix("信号 ").strip()}
        else:
            raise StoryError(f"阶段里不认识“{key}”。", ln.number)
    for key in flags.values():
        stage.setdefault(key, False)
    stage.setdefault("sites", [])
    return stage


def read_verdict(step: str, head: Line, body: list[Line]) -> dict[str, Any]:
    fields = read_fields(body)
    verdict: dict[str, Any] = {"step": num(step, "判定步", head.number)}
    for key, (value, ln, _) in fields.items():
        if key == "问题":
            verdict["question"] = value
        elif key == "陈述正确":
            verdict["say_ok"] = parse_expr(value, ln.number).json()
        elif key == "出示正确":
            verdict["show_ok"] = parse_expr(value, ln.number).json()
        elif key == "需先试验":
            verdict["need_test"] = parse_expr(value, ln.number).json()
        else:
            raise StoryError(f"判定里不认识“{key}”。", ln.number)
    return verdict


# ---- 数据块 ----

def read_fields(body: list[Line]) -> dict[str, tuple[str, Line, list[Line]]]:
    fields: dict[str, tuple[str, Line, list[Line]]] = {}
    j = 0
    while j < len(body):
        line = body[j]
        if line.indent != 1:
            raise StoryError("字段缩进错误。", line.number)
        key, value = split_pair(line)
        sub, j = children(body, j + 1, 1)
        if key in fields:
            raise StoryError(f"字段“{key}”重复。", line.number)
        fields[key] = (value, line, sub)
    return fields


def optional(fields, key: str) -> str | None:
    return text_of(fields[key][0], fields[key][2]) if key in fields else None


def load(path: Path) -> Chapter1Source:
    lines = read_lines(path.read_text(encoding="utf-8-sig"))
    source = Chapter1Source()
    content_order: list[str] = []
    i = 0
    while i < len(lines):
        head = lines[i]
        if head.indent != 0:
            raise StoryError("顶层内容必须顶格。", head.number)
        body, i = children(lines, i + 1, 0)
        text = head.text
        if text.startswith("章节"):
            source.chapter_id = split_pair(head)[1]
            continue
        word, _, rest = text.partition(" ")
        rest = rest.strip()
        if text == "文案":
            for key, value in read_text_map(body).items():
                texts = source.RULES_DATA.setdefault("TEXT", {})
                if key in texts:
                    raise StoryError(f"文案键“{key}”重复。", head.number)
                texts[key] = value
        elif text == "守则":
            source.RULES_DATA["POSTED_RULES"] = [item.text[2:].strip() for item in body]
        elif word == "阶段":
            source.RULES_DATA.setdefault("STAGES", {})[rest] = read_stage(rest, body)
        elif text == "阶段事件":
            source.RULES_DATA["STAGE_EVENTS"] = {split_pair(item)[0]: split_pair(item)[1] for item in body}
        elif word == "流程":
            header: dict[str, Any] = {}
            steps_lines = []
            for item in body:
                if item.indent == 1 and item.text.startswith(("参数：", "参数来源：")):
                    key, value = split_pair(item)
                    header["params" if key == "参数" else "provider"] = split_list(value) if key == "参数" else value
                else:
                    steps_lines.append(item)
            flows = source.RULES_DATA.setdefault("FLOWS", {})
            if rest in flows:
                raise StoryError(f"流程“{rest}”重复。", head.number)
            flows[rest] = {**header, "steps": parse_flow(steps_lines, 1)}
        elif word == "声路":
            raw = source.RULES_DATA.setdefault("_ACOUSTIC_RAW", {})
            raw[rest] = [(split_pair(item), item.number) for item in body]
        elif text.startswith(("听来像在门外：", "喊声可闻：")):
            key, value = split_pair(head)
            source.RULES_DATA.setdefault("_ACOUSTIC_LISTS", {})[key] = (split_list(value), head.number)
        elif word == "人物":
            fields = read_fields(body)
            if "编号" not in fields:
                raise StoryError(f"人物“{rest}”缺少编号。", head.number)
            person = read_person(fields)
            source.RULES_DATA.setdefault("PEOPLE", {})[fields["编号"][0]] = person
        elif word == "话语":
            source.RULES_DATA.setdefault("UTTERANCES", {})[rest] = read_utterance(head, body)
        elif word == "试验句":
            source.RULES_DATA.setdefault("TEST_SENTENCES", []).append(read_utterance(head, body))
        elif text == "房间区域":
            source.RULES_DATA["ROOM_REGIONS"] = read_regions(body)
        elif text == "真相":
            source.RULES_DATA["TRUTH"] = read_truth(body)
        elif word == "判定":
            source.RULES_DATA.setdefault("VERDICTS", {})[rest] = read_verdict(rest, head, body)
        elif word == "证物":
            ident, _, name = rest.partition(" ")
            f = read_fields(body)
            source.EVIDENCE.append(Evidence(
                ident, name.strip(), optional(f, "地点"), optional(f, "来源"), optional(f, "现象"),
                optional(f, "实验"), optional(f, "实验结果"), optional(f, "协助"), optional(f, "回忆"), optional(f, "画面"),
                room=optional(f, "所在"), mind_name=optional(f, "认知名"),
                world_facts=parse_facts(f["世界事实"][0], f["世界事实"][1].number) if "世界事实" in f else None,
                compare_place=optional(f, "对照地点"), compare_region=optional(f, "对照区域"), on_examine=EXAMINE_EFFECTS[f["检视后"][0]] if "检视后" in f else None,
                recall_fragment=optional(f, "回忆片段"),
                experiment_mode=EXPERIMENT_MODES[f["实验方式"][0]] if "实验方式" in f else None,
                repeatable=yes_no(f["可重复"][0], "可重复", f["可重复"][1].number) if "可重复" in f else None,
                addenda=[parse_addendum(item) for item in f["补注"][2]] if "补注" in f else None,
                compare_when=parse_expr(f["逐人可用"][0], f["逐人可用"][1].number).json() if "逐人可用" in f else None,
                recall_confirms=[int(n) for n in split_list(f["回忆证实"][0])] if "回忆证实" in f else None))
        elif word == "生活痕迹":
            ident, _, title = rest.partition(" ")
            f = read_fields(body)
            x, y = numbers(f["位置"][0], "位置", head.number)
            source.LIFE_TRACES.append(LifeTrace(ident, title.strip(), optional(f, "房间"), optional(f, "画面"),
                                                optional(f, "现象"), x, y, num(f["触及"][0], "触及", head.number)))
        elif word == "热点":
            ident, _, title = rest.partition(" ")
            f = read_fields(body)
            x, y = numbers(f["位置"][0], "位置", head.number)
            source.SITES.append(Site(ident, f["证物"][0], x, y, num(f["触及"][0], "触及", head.number), title.strip(),
                                     CLICK_KINDS[f["点击"][0]] if "点击" in f else "evidence"))
        elif word == "额外热点":
            f = read_fields(body)
            record: dict[str, Any] = {"id": rest}
            for key, (value, ln, _) in f.items():
                if key == "位置":
                    record["x"], record["y"] = numbers(value, "位置", ln.number)
                elif key == "触及":
                    record["reach"] = num(value, "触及", ln.number)
                else:
                    record[{"种类": "kind", "目标": "target", "标题": "title"}[key]] = value
            source.EXTRA_SITES.append(record)
        elif word == "摆设":
            f = read_fields(body)
            x, y = numbers(f["位置"][0], "位置", head.number)
            source.PROPS.append(Prop(rest, f["贴图"][0], x, y, num(f["层"][0], "层", head.number)))
        elif word == "推演变量":
            f = read_fields(body)
            variable = {"key": rest, "question": f["问题"][0],
                        "options": [item.text[2:].strip() for item in f["选项"][2]],
                        "evidence": split_list(f["证据"][0])}
            source.CONTENT_TABLES.setdefault("VARIABLES", []).append(variable)
            if "VARIABLES" not in content_order:
                content_order.append("VARIABLES")
        elif word in TABLES or text in TABLES:
            key = text if text in TABLES else word
            attr, kind = TABLES[key]
            value: Any
            if kind == "list":
                value = tuple(item.text[2:].strip() for item in body)
            elif kind == "map":
                value = {split_pair(item)[0]: split_pair(item)[1] for item in body}
            elif kind == "points":
                value = {split_pair(item)[0]: tuple(numbers(split_pair(item)[1], "坐标", item.number)) for item in body}
            elif kind == "boxes":
                value = {}
                for item in body:
                    k, v = split_pair(item)
                    value[k] = "site-relative" if v == "随热点" else tuple(numbers(v, "点位框", item.number))
            else:
                value = {}
                for item in body:
                    k, v = split_pair(item)
                    if v.startswith("("):
                        value[k] = tuple(tuple(int(n) for n in re.findall(r"-?\d+", group))
                                         for group in re.findall(r"\([^)]*\)", v))
                    else:
                        value[k] = num(v, k, item.number)
            if attr in CONTENT_KEYS:
                source.CONTENT_TABLES[attr] = list(value) if kind == "list" else value
                content_order.append(attr)
            else:
                setattr(source, attr, value)
        elif word == "场面":
            source.entries.append(entry(rest, *parse_body(body, 0, 1, rest)[0]))
        else:
            raise StoryError(f"未知块“{word}”。", head.number)
    finalize_acoustics(source)
    ordered = {key: source.CONTENT_TABLES[key] for key in CONTENT_KEYS if key in source.CONTENT_TABLES}
    source.CONTENT_TABLES = ordered
    source.CHAPTER = ChapterSpec(source.chapter_id, tuple(source.entries),
                                 tuple(source.EVIDENCE) + tuple(source.LIFE_TRACES) + tuple(source.SITES) + tuple(source.PROPS))
    return source
