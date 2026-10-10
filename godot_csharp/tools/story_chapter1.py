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
from story_base import note_line, split_tags  # noqa: E402
from story_script import (ECOLOGY_BLOCKS, Line, StoryError, children, read_lines,  # noqa: E402
                          source_lines, split_list, split_pair)


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
    supports: list | None = None

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
    "detective_name": "侦探名", "seen": "见过", "known": "获知", "tested": "试过", "asked": "问过", "flag": "旗",
    "recalled": "回想过", "compared": "比对过", "v6_open": "六问已开", "told_rule": "告知规则",
    "hooks": "钩数", "total_hooks": "总钩数", "matched": "已吻合", "replayed": "已重演",
    "fragment": "片段", "alone_choice": "独处选择", "life_thought": "生活所思", "talk_first": "先开口",
    "recall_thought": "回想所思", "wrong_name": "错名", "addr": "称呼", "heard": "所闻",
    "heard_where": "所闻之处", "sound_sentence": "声路句子",
    "has": "有记录", "count": "记录数", "value": "记录值", "replay_count": "重演次数", "full_match": "重演吻合",
    "replay_refused": "重演被拒", "in_room": "在房间", "local": "同处", "busy": "忙碌",
    "said": "陈述", "shown": "出示", "is_person": "是同伴", "geo_reach": "几何可达",
    "acquaintance_seen": "相识已查", "replay_var": "重演变量", "bolted": "重演侦探闩门",
    "credence": "把握", "knows": "知道", "speech": "话题回应", "dorms_open": "已出门寝区",
    "dorm_out": "已出门", "accused": "有人被指控",
}
QUERY_BY_ZH = {zh: en for en, zh in QUERY_NAMES.items()}
EFFECT_WORDS = {"设旗": "set_flag", "清旗": "clear_flag", "放大": "amplify", "低语": "whisper", "共享回忆": "share_recall"}
COMPARE = {"==": "eq", "!=": "ne", ">": "gt", ">=": "gte", "<": "lt", "<=": "lte"}

# 设定表：块名 -> (CONTENT_TABLES 键或独立属性, 形式)
TABLES = {
    "人物兼容编号": ("PERSON_IDS", "constants"),
    "房间兼容编号": ("ROOM_IDS", "constants"),
    "重演通路": ("REPLAY_PASSAGES", "records"),
    "策略牵引": ("STRATEGY_TRACK", "records"),
    "表达": ("EXPRESSION", "records"),
    "规则有效期": ("RULE_LIFETIMES", "map"),
    "现场表现": ("PRESENTATION", "records"),
    "侦探搜索参数": ("DETECTIVE_SEARCH", "constants"),
    "侦探调查行动": ("DETECTIVE_ACTIONS", "records"),
    "侦探痕迹字段": ("DETECTIVE_TRACE_FIELDS", "records"),
    "侦探动机假设": ("DETECTIVE_MOTIVES", "records"),
    "侦探论证来源": ("DETECTIVE_ARGUMENTS", "records"),
    "侦探对白选择意图": ("DETECTIVE_DIALOGUE_CHOICES", "records"),
    "地点名单": ("PLACES", "list"), "人名": ("PERSON_NAMES", "map"), "演员节点": ("ACTOR_NODES", "map"),
    "房间名": ("ROOM_NAMES", "map"), "陈述": ("STATEMENTS", "map"), "低语": ("WHISPERS", "map"),
    "协助说明": ("HELP_NOTES", "map"), "家具旁白": ("FURNITURE_ASIDES", "map"),
    "岗位坐标": ("POSTS", "points"), "演出点": ("STAGING", "points"), "物品立绘": ("OBJECT_SPRITES", "map"), "自带物件": ("OWN_OBJECTS", "map"),
    "回忆物件": ("RECALL_OBJECTS", "list"), "相识证物": ("ACQUAINTANCE_EVIDENCE", "list"),
    "对白演员": ("DIALOGUE_CAST", "map"), "声路站位": ("SOUND_SPOTS", "map"), "同伴站位": ("COMPANION_SPOTS", "map"),
    "站位候补": ("SPOT_ORDER", "list"), "鞋底对象": ("SOLE_TARGETS", "map"), "点位框": ("SITE_BOXES", "boxes"), "布局常量": ("LAYOUT_CONSTANTS", "constants"),
    "物件外观名": ("OBJECT_NAMES", "map"), "纹理外观名": ("TEXTURE_NAMES", "map"), "纹理前缀外观名": ("TEXTURE_PREFIX_NAMES", "map"),
    "现场显示词": ("FIELD_WORDS", "map"), "精灵热点": ("SPRITE_SITES", "map"),
}
CONTENT_KEYS = ("PLACES", "PERSON_NAMES", "ACTOR_NODES", "ROOM_NAMES", "VARIABLES", "STATEMENTS", "WHISPERS", "HELP_NOTES", "STRATEGY_TRACK",
                "PERSON_IDS", "ROOM_IDS", "REPLAY_PASSAGES",
                "EXPRESSION",
                "RULE_LIFETIMES",
                "PRESENTATION",
                "SOUND_SPOTS", "COMPANION_SPOTS", "SPOT_ORDER", "SOLE_TARGETS",
                "DETECTIVE_SEARCH", "DETECTIVE_ACTIONS", "DETECTIVE_TRACE_FIELDS", "DETECTIVE_MOTIVES", "DETECTIVE_ARGUMENTS", "DETECTIVE_DIALOGUE_CHOICES",
                "OBJECT_NAMES", "TEXTURE_NAMES", "TEXTURE_PREFIX_NAMES", "FIELD_WORDS", "SPRITE_SITES")


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

# 话题在读取时登记：须先声明后使用，与人物同一顺序语义。
TOPICS: dict[str, Any] = {}
# 地点名、人物名 → 运行时键（来自共享生态的“编号”）。
PLACES: dict[str, str] = {}
PERSON_KEYS: dict[str, str] = {}
VALUE_IDS: dict[str, str] = {}


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
        elif text.startswith("回应话题 "):
            topic, _, who = text[len("回应话题 "):].partition("：")
            result.append(expand_topic_response(topic.strip(), who.strip(), line.number))
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
                result.append(dialogue(line, entry_id))
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
        label, _, label_id = split_tags(line.text[2:].strip(), line.number)
        note_line(entry_id, label_id, line.number)
        body, _ = parse_body(sub, 0, depth + 1, entry_id) if sub else ([], 0)
        options.append(choice_option("", restore_interpolation(label), body, test, hint, label_id))
    return options


def dialogue(line: Line, entry_id: str):
    text, mood, line_id = split_tags(line.text, line.number)
    if text.startswith("（"):
        speaker, content = "", text
    else:
        speaker, content = split_pair(Line(line.number, line.indent, text))
    note_line(entry_id, line_id, line.number)
    return dialogue_line(restore_interpolation(speaker), restore_interpolation(content), mood, line_id=line_id)


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
    tail_meanings = split_list(fields["句尾含义"][0]) if "句尾含义" in fields else []
    for word in tail_meanings:
        if word not in {"开门", "求救"}:
            raise StoryError(f"句尾含义不认识“{word}”。可用：开门、求救", head.number)
    return {"full": fields["整句"][0], "tail": fields["句尾"][0],
            **{name: zh in meanings for zh, name in VOICE_MEANINGS.items()},
            "TailWantsOpen": "开门" in tail_meanings or "求救" in tail_meanings, "TailHelp": "求救" in tail_meanings}


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


def read_acoustic_laws(body: list[Line]) -> dict[str, Any]:
    """`声学`：铜口所在、隔离门两侧、接头（名 | 房间 | 松/紧）、隔墙低语的房间，以及听感与喊声的两张名单。"""
    fields = read_fields(body)
    need = {"铜口所在", "隔离门", "接头", "隔墙低语", "听来像在门外", "喊声可闻"}
    if set(fields) != need:
        raise StoryError("声学须写：" + "、".join(sorted(need)))
    inner, _, outer = fields["隔离门"][0].partition("|")
    joints = []
    for item in fields["接头"][2]:
        parts = [part.strip() for part in item.text.removeprefix("- ").split("|")]
        if len(parts) != 3 or parts[2] not in {"松", "紧"}:
            raise StoryError("接头写成“- 名称 | 房间 | 松或紧”。", item.number)
        joints.append({"name": parts[0], "room": parts[1], "loose": parts[2] == "松"})
    return {"tube_room": fields["铜口所在"][0], "door": [inner.strip(), outer.strip()], "joints": joints,
            "murmur": split_list(fields["隔墙低语"][0]),
            "seems_outside": split_list(fields["听来像在门外"][0]), "hears_shout": split_list(fields["喊声可闻"][0]),
            "number": fields["铜口所在"][1].number}


def derive_hearing(laws: dict[str, Any], room_key, loose: dict[str, bool] | None = None) -> dict[str, dict[str, str]]:
    """按声学法则推出两种声源在各房间的听感（与运行时 C# Acoustics 同一套法则）。"""
    loose = loose if loose is not None else {j["name"]: j["loose"] for j in laws["joints"]}
    leaking = [room_key(j["room"]) for j in laws["joints"] if loose[j["name"]]]
    inner, outer = (room_key(r) for r in laws["door"])
    tube: dict[str, str] = {room_key(laws["tube_room"]): "Full"}
    for room in leaking:
        tube.setdefault(room, "Tail")
    for room in laws["murmur"]:
        tube.setdefault(room_key(room), "Murmur")
    tube["*"] = "AlarmOnly"
    outside: dict[str, str] = {inner: "FullMuffled"}
    if inner in leaking:
        for room in leaking:
            outside.setdefault(room, "FullMuffled")
    outside[outer] = "Full"
    outside["*"] = "Nothing"
    return {"Tube": tube, "OutsideDoor": outside}


def finalize_acoustics(source) -> None:
    """声学法则 → 声路表（来源 → 听者房间键 → 听感），房间按 `房间名` 反查；表与法则一并交给运行时。"""
    data = source.RULES_DATA
    if "_ACOUSTIC_LAWS" in data:
        laws = data.pop("_ACOUSTIC_LAWS")
        names = {name: key for key, name in source.CONTENT_TABLES["ROOM_NAMES"].items()}

        def room(name: str) -> str:
            if name not in names:
                raise StoryError(f"声学里的房间“{name}”不在房间名表里。", laws["number"])
            return names[name]

        for hearing in laws["seems_outside"]:
            if hearing not in HEARINGS:
                raise StoryError(f"听感“{hearing}”不认识。可用：" + "、".join(HEARINGS), laws["number"])
        data["ACOUSTICS"] = {
            "hearing": derive_hearing(laws, room),
            "seems_outside": [HEARINGS[h] for h in laws["seems_outside"]],
            "hears_shout": [room(r) for r in laws["hears_shout"]],
            "laws": {"tube_room": room(laws["tube_room"]), "door": [room(r) for r in laws["door"]],
                     "joints": [{**j, "room": room(j["room"])} for j in laws["joints"]],
                     "murmur": [room(r) for r in laws["murmur"]]},
        }
        return
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


PERSON_FIELDS = {"欲望": "desire", "初醒信任": "initial_trust", "谨慎": "caution", "说话习惯": "speech", "心智范围": "mind_ranges"}
SPEECH_HABITS = {"坦率": "candor", "寡言": "reticence", "好问": "curiosity"}


def read_person(fields) -> dict[str, Any]:
    person: dict[str, Any] = {}
    for key, (value, line, sub) in fields.items():
        if key == "编号":
            continue
        if key not in PERSON_FIELDS:
            raise StoryError(f"人物里不认识“{key}”。", line.number)
        if key == "初醒信任":
            # 醒来时、记忆封存之下此人心里的信任：“侦探 1，铜口 0”。
            trust = {}
            for entry in split_list(value):
                name, _, number = entry.rpartition(" ")
                if not name.strip():
                    raise StoryError(f"初醒信任写成“对象 数值”：{entry}", line.number)
                trust[name.strip()] = num(number, "初醒信任", line.number)
            person["initial_trust"] = trust
        elif key == "谨慎":
            person["caution"] = num(value, "谨慎", line.number)
        elif key == "心智范围":
            # 隐藏心智抽样的范围：“初醒信任.侦探 -1~1，先入之见.缘由 2~4”；第一局用作者默认值，之后的局在范围内抽取。
            ranges = {}
            for entry in split_list(value):
                name, _, span = entry.rpartition(" ")
                low, sep, high = span.partition("~")
                if not name.strip() or not sep:
                    raise StoryError(f"心智范围写成“键 下限~上限”：{entry}", line.number)
                ranges[name.strip()] = [num(low, "心智范围", line.number), num(high, "心智范围", line.number)]
            person["mind_ranges"] = ranges
        elif key == "说话习惯":
            # “坦率 1，寡言 1，好问 1，会撒谎”：说话行为的修正；不写“会撒谎”即从不撒谎。
            speech: dict[str, Any] = {"candor": 0, "reticence": 0, "curiosity": 0, "may_lie": False}
            for entry in split_list(value):
                if entry == "会撒谎":
                    speech["may_lie"] = True
                    continue
                name, _, number = entry.partition(" ")
                if name not in SPEECH_HABITS:
                    raise StoryError(f"说话习惯不认识“{name}”。可用：" + "、".join(SPEECH_HABITS) + "、会撒谎。", line.number)
                speech[SPEECH_HABITS[name]] = num(number, "说话习惯", line.number)
            person["speech"] = speech
        else:
            person[PERSON_FIELDS[key]] = text_of(value, sub)
    return person


# ---- 话题：命题、牵涉与各人按言语行为的回应 ----
HOST_PEOPLE = {"侦探": "detective", "霍铎": "huo", "沈鼎": "shen", "苏泠": "su"}


def read_topic(name: str, head: Line, body: list[Line]) -> dict[str, Any]:
    """`话题 <名>`：命题写成“推演变量 = 选项”；回应按人（或“任何人”）与言语行为分列，每条台词带编号。"""
    fields = read_fields(body)
    if "命题" not in fields:
        raise StoryError(f"话题“{name}”缺少命题。", head.number)
    question, eq, option = fields["命题"][0].partition("=")
    if not eq:
        raise StoryError("命题写成“推演变量 = 选项”，如：V6 = 早已相熟。", fields["命题"][1].number)
    topic: dict[str, Any] = {"question": question.strip(), "option": option.strip(), "implicates": [], "responses": {}}
    if "牵涉" in fields:
        topic["implicates"] = [HOST_PEOPLE.get(who, who) for who in split_list(fields["牵涉"][0])]
    for key, (value, line, sub) in fields.items():
        if key in {"命题", "牵涉"}:
            continue
        if not key.startswith("回应 "):
            raise StoryError(f"话题里不认识“{key}”。可用：命题、牵涉、回应 <人物或任何人>。", line.number)
        who = key[len("回应 "):].strip()
        speaker = who if who != "任何人" else ""
        responses: dict[str, list[dict[str, Any]]] = {}
        j = 0
        while j < len(sub):
            item = sub[j]
            more, j = children(sub, j + 1, item.indent)
            act, _, text = item.text.partition("：")
            if not act.strip():
                raise StoryError("回应须有稳定的表达键。", item.number)
            if act in responses:
                raise StoryError(f"回应“{act}”重复。", item.number)
            said = []
            # 同一行为可接几句：首句写在冒号后，后续句缩进写在下面（同一说话人）。
            for number, raw in [(item.number, text.strip()), *((m.number, m.text) for m in more)]:
                words, mood, line_id = split_tags(raw, number)
                if not words:
                    raise StoryError("回应须写出台词。", number)
                note_line(f"topic.{name}", line_id, number)
                said.append(dialogue_line(speaker, restore_interpolation(words), mood, line_id=line_id).json())
            responses[act] = said
        topic["responses"]["any" if who == "任何人" else HOST_PEOPLE.get(who, who)] = responses
    return topic


def expand_topic_response(topic: str, who: str, number: int) -> Any:
    """`回应话题 <话题>：<人物>` 译成按此人所选言语行为分支的对白；选择在运行时由推理器与言语规则作出。"""
    if topic not in TOPICS:
        raise StoryError(f"话题“{topic}”尚未声明；话题须写在使用它的场面之前。", number)
    responses = TOPICS[topic]["responses"]
    own, common = responses.get(who, {}), responses.get("any", {})
    speaker = {host: zh for zh, host in HOST_PEOPLE.items()}.get(who, who)
    branches: list[Any] = []
    for act in dict.fromkeys([*own, *common]):
        lines = own.get(act) or common.get(act)
        if not lines:
            continue
        body = [dialogue_line(speaker, item["text"], item["mood"], line_id=item["line_id"]) for item in lines]
        branches.append((binary("eq", call("speech", who, topic), literal(act)), body))
    if not branches:
        raise StoryError(f"话题“{topic}”没有写给“{who}”或“任何人”的回应。", number)
    result: list[Any] = []
    for test, body in reversed(branches):
        result = [condition(test, body, result)]
    return result[0]


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
        if text == "选择：":
            options = []
            j = 0
            while j < len(sub):
                option = sub[j]
                if option.indent != depth + 1 or not option.text.startswith("- "):
                    raise StoryError("流程选择须写成缩进的“- 标签”。", option.number)
                branch, j = children(sub, j + 1, depth + 1)
                label, _, ident = split_tags(option.text[2:], option.number)
                if not ident:
                    raise StoryError("流程选择须有稳定编号。", option.number)
                note_line("flow", ident, option.number)
                options.append({"id": ident, "label": label, "label_id": ident,
                                "steps": parse_flow(branch, depth + 2)})
            steps.append({"op": "choices", "options": options})
            i = nxt
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
    stage: dict[str, Any] = {"enter": {}, "times": []}
    flags = {"走动": "walk", "检视": "examine", "笔记本": "notebook", "宿舍开放": "dorms", "房间事件": "room_events", "日常": "daily"}
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
            if value.startswith("流程 "):
                stage["enter"][room] = {"flow": value[3:].strip()}
                continue
            goto, _, signal = value.partition("，")
            stage["enter"][room] = {"stage": goto.strip().removeprefix("阶段 ").strip(), "signal": signal.strip().removeprefix("信号 ").strip()}
        elif key.startswith("时刻 "):
            minute_text = key[3:].strip()
            if not minute_text.isdecimal() or not 0 <= int(minute_text) < 1440:
                raise StoryError("阶段时刻须是 0 到 1439 的当日分钟。", ln.number)
            minute = int(minute_text)
            if not value.startswith("流程 ") or not value[3:].strip():
                raise StoryError("阶段时刻须写成“时刻 <分钟>：流程 <名>”。", ln.number)
            if any(item["minutes"] == minute for item in stage["times"]):
                raise StoryError(f"阶段“{name}”重复声明时刻 {minute}。", ln.number)
            stage["times"].append({"minutes": minute, "flow": value[3:].strip()})
        else:
            raise StoryError(f"阶段里不认识“{key}”。", ln.number)
    for key in flags.values():
        stage.setdefault(key, False)
    stage.setdefault("sites", [])
    stage["times"].sort(key=lambda item: item["minutes"])
    return stage


def read_supports(text: str, number: int) -> list[dict[str, Any]]:
    """证物的“支撑”：`命题[ 若 条件][ 须先试验]`，多条以“；”分隔。"""
    supports = []
    for part in [p.strip() for p in text.split("；") if p.strip()]:
        needs_test = part.endswith("须先试验")
        part = part.removesuffix("须先试验").strip()
        proposition, _, condition_text = part.partition(" 若 ")
        supports.append({"proposition": proposition.strip(), "needs_test": needs_test,
                         "condition": parse_expr(condition_text.strip(), number).json() if condition_text.strip() else None})
    return supports


def read_dorm_case(name: str, head: Line, body: list[Line], places: dict[str, str], people: dict[str, str]) -> dict[str, Any]:
    """`寝区案件 <名>`：寝区、题目、问题（各选项）、作答的问题、真相、痕迹（名 | 所见 | 条件 | 需查看）、先入之见、门外证据。"""
    import itertools
    fields = read_fields(body)
    need = {"寝区", "题目", "问题", "作答", "真相", "痕迹"}
    if not need <= set(fields):
        raise StoryError(f"寝区案件“{name}”缺少：" + "、".join(sorted(need - set(fields))), head.number)
    dorm = fields["寝区"][0]
    if dorm not in places:
        raise StoryError(f"寝区“{dorm}”没有声明为地点。", fields["寝区"][1].number)
    questions = []
    for item in fields["问题"][2]:
        key, options = split_pair(item)
        questions.append({"key": key, "options": split_list(options)})
    keys = [q["key"] for q in questions]

    def option_of(text: str, number: int) -> tuple[int, int]:
        key, _, option = text.partition("=")
        key, option = key.strip(), option.strip()
        if key not in keys or option not in questions[keys.index(key)]["options"]:
            raise StoryError(f"寝区案件里不认识“{text}”。", number)
        q = keys.index(key)
        return q, questions[q]["options"].index(option)

    answer = fields["作答"][0]
    if answer not in keys:
        raise StoryError(f"作答的问题“{answer}”不在问题里。", fields["作答"][1].number)
    truth = [None] * len(keys)
    for part in split_list(fields["真相"][0].replace("，", "、")):
        q, o = option_of(part, fields["真相"][1].number)
        truth[q] = o
    if None in truth:
        raise StoryError("真相须为每个问题各定一项。", fields["真相"][1].number)
    traces = []
    for item in fields["痕迹"][2]:
        parts = [part.strip() for part in item.text.removeprefix("- ").split("|")]
        if len(parts) != 4:
            raise StoryError("痕迹写成“- 名称 | 所见 | 问题 = 选项（或 ≠）| 需查看次数”。", item.number)
        negate = "≠" in parts[2]
        q, o = option_of(parts[2].replace("≠", "="), item.number)
        traces.append({"key": parts[0], "text": parts[1], "question": q, "option": o, "negate": negate,
                       "needs": int(num(parts[3], "需查看", item.number))})
    hypotheses = []
    for combo in itertools.product(*[range(len(q["options"])) for q in questions]):
        facts = {t["key"]: int((combo[t["question"]] == t["option"]) != t["negate"]) for t in traces}
        hypotheses.append({"options": list(combo), "facts": facts})
    true_facts = next(h["facts"] for h in hypotheses if h["options"] == truth)
    priors = []
    for item in (fields["先入之见"][2] if "先入之见" in fields else []):
        parts = [part.strip() for part in item.text.removeprefix("- ").split("|")]
        if len(parts) != 3 or parts[0] not in people:
            raise StoryError("先入之见写成“- 人物 | 问题 = 选项 | 强度”。", item.number)
        q, o = option_of(parts[1], item.number)
        priors.append({"person": people[parts[0]], "question": q, "option": o, "strength": int(num(parts[2], "强度", item.number))})
    earliest = None
    if "最早出门" in fields:
        match = re.fullmatch(r"第\s*(\d+)\s*日\s+(\d+)", fields["最早出门"][0])
        if not match or not 0 <= int(match[2]) < 1440:
            raise StoryError("最早出门须写成“第 N 日 当日分钟”。", fields["最早出门"][1].number)
        earliest = {"day": int(match[1]), "minutes": int(match[2])}
    return {"dorm": places[dorm], "title": fields["题目"][0], "questions": questions, "answer": keys.index(answer),
            "earliest_out": earliest,
            "truth": truth, "traces": traces, "true_facts": true_facts, "hypotheses": hypotheses, "priors": priors,
            "outside": fields["门外证据"][0] if "门外证据" in fields else ""}


SKILLS = {"力气": "strength", "开锁": "lock", "信号": "signal", "结构": "structure", "厨艺": "cook", "温室": "greenhouse_skill"}


def read_method(name: str, body: list[Line]) -> dict[str, Any]:
    """`方法 <目标>`：做法、须备（本事 数值）、分解（先要达成的目标）。"""
    fields = read_fields(body)
    if "做法" not in fields:
        raise StoryError(f"方法“{name}”缺少做法。")
    needs = {}
    for entry in split_list(fields["须备"][0]) if "须备" in fields else []:
        skill, _, level = entry.rpartition(" ")
        if skill not in SKILLS:
            raise StoryError(f"须备的本事“{skill}”不认识。可用：" + "、".join(SKILLS), fields["须备"][1].number)
        needs[SKILLS[skill]] = int(num(level, "须备", fields["须备"][1].number))
    return {"how": fields["做法"][0], "needs": needs, "parts": split_list(fields["分解"][0]) if "分解" in fields else []}


def finalize_verdicts(source) -> None:
    """判定按命题：陈述须是认定的陈述之一；出示须是支撑该命题的证物（满足其条件，须先试验的要已试过）。"""
    def eq(left: Expr, value: str) -> dict[str, Any]:
        return binary("eq", left, literal(value)).json()

    def any_of(terms: list[dict[str, Any]]) -> dict[str, Any]:
        if not terms:
            return literal(False).json()
        result = terms[0]
        for term in terms[1:]:
            result = {"op": "or", "args": [result, term]}
        return result

    def both(left: dict[str, Any], right: dict[str, Any]) -> dict[str, Any]:
        return {"op": "and", "args": [left, right]}

    for step, verdict in source.RULES_DATA.get("VERDICTS", {}).items():
        proposition = verdict.pop("_proposition", None)
        statements = verdict.pop("_statements", None)
        if proposition is None:
            continue
        say_ok = any_of([eq(call("said"), s) for s in statements])
        show_terms, need_terms, support = [], [], []
        for evidence in source.EVIDENCE:
            for item in evidence.supports or []:
                if item["proposition"] != proposition:
                    continue
                support.append(evidence.id)
                term = eq(call("shown"), evidence.id)
                if item["condition"] is not None:
                    term = both(term, item["condition"])
                if item["needs_test"]:
                    tested = call("tested", evidence.id).json()
                    need_terms.append(both(both(say_ok, eq(call("shown"), evidence.id)), {"op": "not", "args": [tested]}))
                    term = both(term, tested)
                show_terms.append(term)
        if not support:
            raise StoryError(f"判定 {step} 的命题“{proposition}”没有任何证物支撑。")
        verdict["proposition"] = proposition
        verdict["say_ok"] = say_ok
        verdict["show_ok"] = any_of(show_terms)
        if need_terms:
            verdict["need_test"] = any_of(need_terms)
        verdict["support"] = support


def read_verdict(step: str, head: Line, body: list[Line]) -> dict[str, Any]:
    fields = read_fields(body)
    verdict: dict[str, Any] = {"step": num(step, "判定步", head.number)}
    for key, (value, ln, _) in fields.items():
        if key == "命题":
            verdict["_proposition"] = value
        elif key == "认定陈述":
            verdict["_statements"] = split_list(value)
        elif key == "问题":
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


def read_scalar_records(body: list[Line]) -> dict[str, Any]:
    """Nested typed data tables; no code execution or chapter-specific field vocabulary."""
    result = {}
    i = 0
    while i < len(body):
        item = body[i]
        key, value = split_pair(item)
        sub, i = children(body, i + 1, item.indent)
        if key in result:
            raise StoryError("数据表重复键：" + key, item.number)
        if sub:
            if value:
                raise StoryError("嵌套数据表键不能同时有标量：" + key, item.number)
            result[key] = read_scalar_records(sub)
        else:
            result[key] = "" if value == "-" else scalar(value)
    return result


def read_strategy_track(data: dict[str, Any], number: int) -> dict[str, Any]:
    """Closed adapter wiring, with explicit lists rather than untyped scalar records."""
    from story_base import split_list, StoryError
    fields = {"cooldown_minutes", "wrong_scenes", "world_resolution_scenes", "decline_choices", "object_labels", "object_choices", "people"}
    if not fields <= set(data) or set(data) - fields - {"source_scenes"} or type(data["cooldown_minutes"]) is not int or data["cooldown_minutes"] <= 0:
        raise StoryError("策略牵引字段不完整、未知或冷却非法。", number)
    def strings(value):
        if not isinstance(value, str) or not value.strip():
            raise StoryError("策略牵引列表须为非空字串列表。", number)
        result = split_list(value)
        if len(result) != len(set(result)):
            raise StoryError("策略牵引列表不能重复。", number)
        return result
    data["wrong_scenes"] = strings(data["wrong_scenes"])
    data["world_resolution_scenes"] = strings(data["world_resolution_scenes"])
    if "source_scenes" in data:
        if not isinstance(data["source_scenes"], dict) or not data["source_scenes"]:
            raise StoryError("策略来源场面须为非空映射。", number)
        data["source_scenes"] = {key: strings(scenes) for key, scenes in data["source_scenes"].items()}
    for key in ("decline_choices", "object_labels", "object_choices", "people"):
        if not isinstance(data[key], dict):
            raise StoryError("策略牵引映射字段类型错误：" + key, number)
    for actions in data["object_choices"].values():
        if any(a not in {"hide", "destroy"} for a in strings(actions)):
            raise StoryError("策略牵引包含未知物件动作。", number)
    data["object_choices"] = {key: strings(actions) for key, actions in data["object_choices"].items()}
    for actor, person in data["people"].items():
        if not isinstance(person, dict) or set(person) != {"clues", "methods"} or not isinstance(person["methods"], dict):
            raise StoryError("策略牵引人物字段错误：" + actor, number)
        person["clues"] = strings(person["clues"])
        if not person["methods"] or any(k not in {"report", "test", "invite", "remember"}
                                      or not isinstance(v, str) or not v for k, v in person["methods"].items()):
            raise StoryError("策略牵引方法字段错误：" + actor, number)
    return data


def read_duration_table(body: list[Line], what: str) -> dict[str, int]:
    result = {}
    for item in body:
        key, value = split_pair(item)
        if key in result:
            raise StoryError(f"{what}重复键：{key}", item.number)
        duration = num(value, what, item.number)
        if type(duration) is not int or duration < 0:
            raise StoryError(f"{what}须为非负整数分钟：{key}", item.number)
        result[key] = duration
    return result


RUN_UNITS = {"分钟": 1, "小时": 60, "天": 1440}


def read_run(head: Line, body: list[Line]) -> dict[str, Any]:
    """`运行`：声明一次运行的时长（游戏分钟，自起始分钟起算）；“到结局”或不写表示直到剧情自然结束。"""
    if head.text != "运行":
        raise StoryError("运行写成单独一行“运行”，参数缩进写在下面。", head.number)
    fields = read_fields(body)
    unknown = set(fields) - {"时长"}
    if unknown:
        raise StoryError("运行不认识：" + "、".join(sorted(unknown)) + "。可用：时长。", head.number)
    duration = None
    if "时长" in fields:
        value, line, _ = fields["时长"]
        duration = parse_duration(value, line.number)
    return {"duration_minutes": duration}


def parse_duration(value: str, number: int) -> int | None:
    """`到结局`，或若干段“数字+单位”（分钟、小时、天），如 `1天22小时`。"""
    if value == "到结局":
        return None
    parts = re.findall(r"(\d+)\s*(分钟|小时|天)", value)
    if not parts or re.sub(r"\d+\s*(分钟|小时|天)", "", value).strip():
        raise StoryError(f"时长写成“到结局”或“数字+分钟/小时/天”：{value}", number)
    minutes = sum(int(n) * RUN_UNITS[unit] for n, unit in parts)
    if minutes <= 0:
        raise StoryError("时长必须大于零。", number)
    return minutes


def read_clock(body: list[Line]) -> dict[str, Any]:
    fields = read_fields(body)
    scalars = {"起始分钟": "start_minutes", "预计出门分钟": "expected_exit_minutes",
               "正常出门最早分钟": "normal_exit_earliest_minutes", "正常出门最晚分钟": "normal_exit_latest_minutes",
               "默认场面分钟": "default_scene_minutes", "同伴醒来分钟": "companion_start_minutes"}
    tables = {"场面时长": "scenes", "行动时长": "actions"}
    unknown = set(fields) - set(scalars) - set(tables) - {"每游戏分钟日常逻辑秒", "每游戏分钟现场秒"}
    if unknown:
        raise StoryError("未知剧情时长字段：" + "、".join(sorted(unknown)))
    result = {}
    for zh, en in scalars.items():
        if zh not in fields:
            raise StoryError("剧情时长缺少：" + zh)
        value, line, sub = fields[zh]
        result[en] = read_duration_table([Line(line.number, line.indent, zh + "：" + value)], zh)[zh]
        if sub:
            raise StoryError(zh + "不能带子表", line.number)
    if "每游戏分钟日常逻辑秒" not in fields:
        raise StoryError("剧情时长缺少：每游戏分钟日常逻辑秒")
    value, line, sub = fields["每游戏分钟日常逻辑秒"]
    ratio = num(value, "每游戏分钟日常逻辑秒", line.number)
    if sub or not isinstance(ratio, (int, float)) or not 0 < ratio < float("inf"):
        raise StoryError("每游戏分钟日常逻辑秒须为正有限数", line.number)
    result["daily_seconds_per_game_minute"] = ratio
    if "每游戏分钟现场秒" in fields:
        value, line, sub = fields["每游戏分钟现场秒"]
        ratio = num(value, "每游戏分钟现场秒", line.number)
        if sub or not isinstance(ratio, (int, float)) or not 0 < ratio < float("inf"):
            raise StoryError("每游戏分钟现场秒须为正有限数", line.number)
        result["field_seconds_per_game_minute"] = ratio
    for zh, en in tables.items():
        if zh not in fields:
            raise StoryError("剧情时长缺少：" + zh)
        value, line, sub = fields[zh]
        if value:
            raise StoryError(zh + "须写为缩进表", line.number)
        result[en] = read_duration_table(sub, zh)
    return result


def unique(table: dict, key: str, value: Any, what: str, number: int) -> None:
    if key in table:
        raise StoryError(f"{what}“{key}”重复。", number)
    table[key] = value


def merge_table(previous: Any, value: Any, what: str, number: int) -> Any:
    """Repeated table blocks continue one table: lists append, keyed tables must not repeat keys."""
    if previous is None:
        return value
    if isinstance(previous, (list, tuple)):
        return type(previous)([*previous, *value])
    merged = dict(previous)
    for key, item in value.items():
        unique(merged, key, item, what, number)
    return merged


def load(path: Path) -> Chapter1Source:
    TOPICS.clear()
    PLACES.clear()
    PERSON_KEYS.clear()
    VALUE_IDS.clear()
    lines = source_lines(path)
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
        if text == "运行" or text.startswith(("运行：", "运行:")):
            source.RULES_DATA["RUN"] = read_run(head, body)
            continue
        if text == "剧情时长":
            if "DAY_CLOCK" in source.RULES_DATA:
                raise StoryError("剧情时长重复声明", head.number)
            source.RULES_DATA["DAY_CLOCK"] = read_clock(body)
        elif text == "文案":
            for key, value in read_text_map(body).items():
                texts = source.RULES_DATA.setdefault("TEXT", {})
                if key in texts:
                    raise StoryError(f"文案键“{key}”重复。", head.number)
                texts[key] = value
        elif text == "守则":
            source.RULES_DATA.setdefault("POSTED_RULES", []).extend(item.text[2:].strip() for item in body)
        elif word == "阶段":
            unique(source.RULES_DATA.setdefault("STAGES", {}), rest, read_stage(rest, body), "阶段", head.number)
        elif text == "阶段事件":
            events = source.RULES_DATA.setdefault("STAGE_EVENTS", {})
            for item in body:
                unique(events, split_pair(item)[0], split_pair(item)[1], "阶段事件", item.number)
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
        elif text == "声学":
            if "_ACOUSTIC_LAWS" in source.RULES_DATA:
                raise StoryError("声学只能声明一处。", head.number)
            source.RULES_DATA["_ACOUSTIC_LAWS"] = read_acoustic_laws(body)
        elif word == "声路":
            raw = source.RULES_DATA.setdefault("_ACOUSTIC_RAW", {})
            unique(raw, rest, [(split_pair(item), item.number) for item in body], "声路", head.number)
        elif text.startswith(("听来像在门外：", "喊声可闻：")):
            key, value = split_pair(head)
            unique(source.RULES_DATA.setdefault("_ACOUSTIC_LISTS", {}), key, (split_list(value), head.number), key, head.number)
        elif word == "人物":
            fields = read_fields(body)
            if "编号" not in fields:
                raise StoryError(f"人物“{rest}”缺少编号。", head.number)
            ident = fields["编号"][0]
            if ident.startswith("person."):
                PERSON_KEYS[rest] = ident.split(".", 1)[1]
                if "心智范围" in fields:
                    source.RULES_DATA.setdefault("MIND_RANGES", {})[rest] = read_person({"心智范围": fields["心智范围"]})["mind_ranges"]
                # The shared full profile supplies the original four private desire records.
                host_id = {"person.Det": "detective", "person.Huo": "huo",
                           "person.Shen": "shen", "person.Su": "su"}.get(ident)
                if host_id:
                    source.RULES_DATA.setdefault("PEOPLE", {})[host_id] = read_person(
                        {key: field for key, field in fields.items() if key in PERSON_FIELDS})
            else:
                source.RULES_DATA.setdefault("PEOPLE", {})[ident] = read_person(fields)
        elif word == "方法":
            unique(source.RULES_DATA.setdefault("METHODS", {}), rest, read_method(rest, body), "方法", head.number)
        elif text == "心愿":
            wishes = source.RULES_DATA.setdefault("WISHES", {})
            for item in body:
                flag, _, goals = item.text.removeprefix("- ").partition("|")
                if flag.strip() not in VALUE_IDS:
                    raise StoryError(f"心愿的欲望标志“{flag.strip()}”不在价值词汇里。", item.number)
                unique(wishes, VALUE_IDS[flag.strip()], split_list(goals), "心愿", item.number)
        elif text == "开局人选":
            openings = source.RULES_DATA.setdefault("OPENINGS", [])
            for item in body:
                parts = [part.strip() for part in item.text.removeprefix("- ").split("|")]
                if len(parts) != 3 or parts[2] not in {"可选", "待验收"}:
                    raise StoryError("开局人选写成“- 人物 | 开场场面 | 可选或待验收”。", item.number)
                openings.append({"person": parts[0], "scene": parts[1], "selectable": parts[2] == "可选"})
        elif word == "寝区案件":
            unique(source.RULES_DATA.setdefault("DORM_CASES", {}), rest, read_dorm_case(rest, head, body, PLACES, PERSON_KEYS),
                   "寝区案件", head.number)
        elif word in ECOLOGY_BLOCKS or word in {"晨间日常", "出区日常"}:
            if word == "价值":
                for item in body:
                    parts = [part.strip() for part in item.text.removeprefix("- ").split("|")]
                    if len(parts) >= 2:
                        VALUE_IDS[parts[0]] = parts[1]
            if word in {"地点", "人物"}:
                ident = read_fields(body).get("编号", ("", head, []))[0]
                if "." in ident:
                    (PLACES if word == "地点" else PERSON_KEYS)[rest] = ident.split(".", 1)[1]
            # Full ecology validation and compilation use the shared story_script compiler.
            continue
        elif word == "话语":
            unique(source.RULES_DATA.setdefault("UTTERANCES", {}), rest, read_utterance(head, body), "话语", head.number)
        elif word == "试验句":
            source.RULES_DATA.setdefault("TEST_SENTENCES", []).append(read_utterance(head, body))
        elif text == "房间区域":
            source.RULES_DATA.setdefault("ROOM_REGIONS", []).extend(read_regions(body))
        elif text == "真相":
            truth = source.RULES_DATA.setdefault("TRUTH", {})
            for key, value in read_truth(body).items():
                unique(truth, key, value, "真相", head.number)
        elif word == "判定":
            unique(source.RULES_DATA.setdefault("VERDICTS", {}), rest, read_verdict(rest, head, body), "判定", head.number)
        elif text in {"认知命题", "认知接线", "推知关系"}:
            from story_knowledge import read_knowledge_block
            read_knowledge_block(source.RULES_DATA.setdefault("KNOWLEDGE", {}), head, body)
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
                recall_confirms=[int(n) for n in split_list(f["回忆证实"][0])] if "回忆证实" in f else None,
                supports=read_supports(f["支撑"][0], f["支撑"][1].number) if "支撑" in f else None))
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
            ids = split_list(f["取值编号"][0]) if "取值编号" in f else []
            bindings = split_list(f["绑定"][0]) if "绑定" in f else []
            if not ids or len(ids) != len(variable["options"]) or len(bindings) != len(ids):
                raise StoryError("推演取值编号、绑定与选项数量必须一致。", head.number)
            if len(set(ids)) != len(ids) or any(not value.strip() for value in ids + bindings):
                raise StoryError("推演取值编号重复或绑定为空。", head.number)
            variable["value_ids"] = ids
            variable["bindings"] = bindings
            variables = source.CONTENT_TABLES.setdefault("VARIABLES", [])
            if any(item["key"] == rest for item in variables):
                raise StoryError(f"推演变量“{rest}”重复。", head.number)
            variables.append(variable)
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
            elif kind == "records":
                value = read_scalar_records(body)
                if key == "策略牵引":
                    value = read_strategy_track(value, head.number)
                if key == "表达":
                    from story_expression import validate_expression
                    value = validate_expression(value, head.number)
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
            # A table block written again (e.g. in another included fragment) continues it.
            if attr in CONTENT_KEYS:
                previous = source.CONTENT_TABLES.get(attr)
                source.CONTENT_TABLES[attr] = merge_table(previous, list(value) if kind == "list" else value, key, head.number)
                content_order.append(attr)
            else:
                had = attr in source.__dict__ and getattr(source, attr)
                setattr(source, attr, merge_table(getattr(source, attr) if had else None, value, key, head.number))
        elif word == "话题":
            unique(TOPICS, rest, read_topic(rest, head, body), "话题", head.number)
            source.RULES_DATA.setdefault("TOPICS", {})[rest] = TOPICS[rest]
        elif word == "场面":
            if any(existing[0] == rest for existing in source.entries):
                raise StoryError(f"场面“{rest}”重复。", head.number)
            source.entries.append(entry(rest, *parse_body(body, 0, 1, rest)[0]))
        else:
            raise StoryError(f"未知块“{word}”。", head.number)
    finalize_acoustics(source)
    finalize_verdicts(source)
    ordered = {key: source.CONTENT_TABLES[key] for key in CONTENT_KEYS if key in source.CONTENT_TABLES}
    source.CONTENT_TABLES = ordered
    source.CHAPTER = ChapterSpec(source.chapter_id, tuple(source.entries),
                                 tuple(source.EVIDENCE) + tuple(source.LIFE_TRACES) + tuple(source.SITES) + tuple(source.PROPS))
    return source
