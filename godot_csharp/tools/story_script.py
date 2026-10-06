#!/usr/bin/env python3
"""Compile human-readable `.story` chapter scripts to the world-script JSON IR.

A `.story` file is read as indentation-structured Chinese declarations and translated
to the statements `world_script.py` already compiles; the typed IR and its validation
stay in one place. Nothing here evaluates author code.

Blocks (top-level, 4-space indented bodies; `#` starts a comment):
  章节：<id>            前置章节：<id>、<id>
  引入：<相对路径或通配> 就地展开另一个片段文件的全部顶层语句；分段引入与写在同一文件等价
  继承生态：<相对路径>    只继承词汇、常数、地点和人物，不带入案件或晨间循环
  续篇 <id>             缩进正文声明后续章节；--section 选择，默认只编译主体；同名续篇再次出现即续写同一作用域
  运行                  只写在入口文件末尾：此前各句只登记内容，运行声明一次模拟的时长；外部测试可用参数代替
  晨间日常              封锁、旁听与人物有序步骤（房间、目标、动作、说明、权重、游戏分钟）
  文案                  <键>：<文字> / <键>： 后接 `- 条目` 列表
  请求                  `- <标签> → <岗位>`
  地点 <名称>           编号 别名 范围
  人物 <名称>           编号 别名 所在 范围 + 画像、属性、认知、可见、立绘
  物件 <名称>           编号 别名 位置 范围 + 属性、可见
  原生                  缩进内容原样交给 world_script（尚未迁入 .story 的选项、反应、场面、交互点）
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parent))
import world_script as ws  # noqa: E402
from story_base import FILE_STRIDE, Line, StoryError, children, note_line, origin, read_lines, register_file, split_list, split_pair, split_tags  # noqa: E402,F401
from story_design import DESIGN_BLOCKS, PERSON_FIELDS, VOCAB_BLOCKS, Vocab, compile_design, compile_person, nodes  # noqa: E402

UI_KEYS = {
    "回放标题": "replay_title", "回放提示": "replay_hint", "线索标题": "finding_titles",
    "质询问题": "confront_questions", "饥饿": "hungry", "太早睡": "bed_early",
    "太早守夜": "watch_early", "配给台关闭": "meal_closed",
}
SCOPES = {"世界": "world", "章节": "chapter"}
TAGS = {"信任": "Trust", "合作": "Cooperation", "患病": "Sick", "饥饿": "Hungry", "吃饱": "Fed", "虚弱": "Weak"}
INTENTS = {"作证": "testimony", "拒绝": "decline"}
OPS = {"≥": ">=", "≤": "<=", "＜": "<", "＞": ">", "<": "<", ">": ">", "=": "==", "＝": "==", "≠": "!="}
RESERVED_SCENE_FIELDS = {"事件", "日", "时段"}
ECOLOGY_BLOCKS = set(VOCAB_BLOCKS) | {"常数", "地点", "人物"}


def ecology_block(head: Line) -> bool:
    return head.text.split(None, 1)[0] in ECOLOGY_BLOCKS


def expand_includes(path: Path, ancestors: tuple[Path, ...] = (), base: int = 0) -> list[Line]:
    """Read a story file and splice every top-level `引入：<相对路径或通配>` in place.

    Feeding a story in pieces equals writing it in one file: an include is replaced by
    the included file's top-level statements, in order, so declare-before-use holds
    exactly as it would after concatenation (Python-like sequential semantics).
    """
    resolved = path.resolve()
    if resolved in ancestors:
        raise StoryError("引入出现循环：" + " → ".join(p.name for p in (*ancestors, resolved)))
    try:
        lines = read_lines(path.read_text(encoding="utf-8-sig"), base)
    except OSError as exc:
        raise StoryError(f"无法读取剧本“{path}”：{exc}") from exc
    result: list[Line] = []
    i = 0
    while i < len(lines):
        head = lines[i]
        body, i = children(lines, i + 1, 0)
        if head.indent != 0:
            raise StoryError("顶层内容必须顶格。", head.number)
        if head.text == "运行" or head.text.startswith(("运行：", "运行:")):
            if base:
                raise StoryError("运行只能写在入口文件的末尾，片段里不能写。", head.number)
            result.extend([head, *body])
        elif head.text.startswith(("引入：", "引入:")):
            _, pattern = split_pair(head)
            if not pattern or body:
                raise StoryError("引入须写成“引入：相对路径或通配”，不带正文。", head.number)
            if Path(pattern).is_absolute():
                raise StoryError("引入必须使用相对路径。", head.number)
            matches = sorted(path.parent.glob(pattern)) if any(c in pattern for c in "*?[") else [path.parent / pattern]
            if not matches:
                raise StoryError(f"引入“{pattern}”没有匹配的文件。", head.number)
            for included in matches:
                fragment = expand_includes(included, (*ancestors, resolved), register_file(included.as_posix()))
                for line in fragment:
                    if line.indent == 0 and line.text.startswith(("章节：", "章节:", "前置章节：", "前置章节:")):
                        raise StoryError("被引入的片段不能声明章节或前置章节。", line.number)
                result.extend(fragment)
        else:
            result.extend([head, *body])
    if not ancestors:
        heads = [line for line in result if line.indent == 0]
        runs = [line for line in heads if line.text == "运行" or line.text.startswith(("运行：", "运行:"))]
        if len(runs) > 1 or runs and heads[-1] is not runs[0]:
            raise StoryError("运行只能有一处，且必须是全剧最后一条语句。", runs[-1].number)
    return result


def source_lines(path: Path, *, ecology_only: bool = False,
                 ancestors: tuple[Path, ...] = (), section: str | None = None) -> list[Line]:
    """Resolve includes and ecology inheritance without importing a source chapter's case or scenes."""
    resolved = path.resolve()
    if resolved in ancestors:
        raise StoryError("继承生态出现循环：" + " → ".join(p.name for p in (*ancestors, resolved)))
    lines = expand_includes(path)
    result = []
    sections: dict[str, tuple[Line, list[Line]]] = {}
    i = 0
    while i < len(lines):
        head = lines[i]
        body, i = children(lines, i + 1, 0)
        if head.text.split(None, 1)[0] == "续篇":
            name = head.text[len("续篇"):].strip()
            if not re.fullmatch(r"[A-Za-z_][A-Za-z_0-9-]*", name):
                raise StoryError("续篇需要有效的章节编号：续篇 <id>。", head.number)
            if not body:
                raise StoryError("续篇必须有缩进正文。", head.number)
            lifted = [Line(line.number, line.indent - 1, line.text) for line in body]
            for line in lifted:
                if line.indent == 0 and (line.text.startswith(("章节：", "章节:"))
                                         or line.text.split(None, 1)[0] == "续篇"):
                    raise StoryError("续篇编号已声明章节，正文不能再次声明章节或嵌套续篇。", line.number)
            if name in sections:
                # A later block with the same id reopens the scope, as if its body had
                # been written at the end of the first one.
                if lifted[0].text.startswith(("前置章节：", "前置章节:")):
                    raise StoryError("前置章节只能写在该续篇第一次出现的正文开头。", lifted[0].number)
                sections[name][1].extend(lifted)
            else:
                sections[name] = (head, lifted)
        elif head.text.startswith(("继承生态：", "继承生态:")):
            _, relative = split_pair(head)
            if not relative or body:
                raise StoryError("继承生态须写成“继承生态：相对路径”，不带正文。", head.number)
            if Path(relative).is_absolute():
                raise StoryError("继承生态必须使用相对路径。", head.number)
            inherited = source_lines(path.parent / relative, ecology_only=True,
                                     ancestors=(*ancestors, resolved))
            # An import never introduces its source chapter header or morning loop.
            result.extend(line for line in inherited if not (
                line.indent == 0 and line.text.startswith(("章节：", "章节:"))))
        elif not ecology_only or ecology_block(head) or head.text.startswith(("章节：", "章节:")):
            result.extend([head, *body])
    if section is None:
        return result
    if section not in sections:
        raise StoryError(f"文件没有续篇“{section}”。")
    head, selected = sections[section]
    # Keep prerequisites immediately after the virtual header, then share only
    # authored ecology. The main case and its daily loop never enter this section.
    prefix: list[Line] = [Line(head.number, 0, f"章节：{section}")]
    if selected[0].indent == 0 and selected[0].text.startswith(("前置章节：", "前置章节:")):
        prefix.append(selected[0])
        selected = selected[1:]
    shared: list[Line] = []
    i = 0
    while i < len(result):
        shared_head = result[i]
        body, i = children(result, i + 1, 0)
        if ecology_block(shared_head):
            shared.extend([shared_head, *body])
    return [*prefix, *shared, *selected]


def table(mapping: dict[str, str], word: str, what: str, line: int) -> str:
    if word in mapping:
        return mapping[word]
    if word in mapping.values():
        return word
    raise StoryError(f"未知的{what}“{word}”。可用：" + "、".join(mapping), line)


def parse_int(word: str, what: str, line: int) -> int:
    try:
        return int(word)
    except ValueError:
        raise StoryError(f"{what}必须是整数：{word}", line) from None


def parse_number(word: str, what: str, line: int) -> float | int:
    try:
        number = float(word)
    except ValueError:
        raise StoryError(f"{what}必须是数字：{word}", line) from None
    return int(number) if number.is_integer() and "." not in word else number


def duration_minutes(word: str, what: str, line: int) -> float:
    """时长写游戏分钟：裸数或“N分钟”；运行时再按每游戏分钟逻辑秒换算。"""
    value = word.strip().removesuffix("分钟").strip()
    if not re.fullmatch(r"\d+(?:\.\d+)?", value):
        raise StoryError(f"{what}须写游戏分钟（如 12 或 12分钟）：{word}", line)
    minutes = float(value)
    if not minutes > 0:
        raise StoryError(f"{what}必须大于零：{word}", line)
    return minutes


def parse_bool(word: str, what: str, line: int) -> bool:
    if word in {"是", "真", "可见", "true", "True"}:
        return True
    if word in {"否", "假", "false", "False"}:
        return False
    raise StoryError(f"{what}必须是“是”或“否”：{word}", line)


def py(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False).replace("true", "True").replace("false", "False").replace("null", "None")


def split_pair_safe(line):
    for sep in ("：", ":"):
        if sep in line.text:
            key, value = line.text.split(sep, 1)
            return key.strip(), value.strip()
    return "", line.text


class Translator:
    """Translate .story lines to world-script source while remembering source lines."""

    def __init__(self) -> None:
        self.out: list[str] = []
        self.map: list[int] = []
        self.vars: dict[str, str] = {}        # display name or alias -> python variable
        self.person_enum: dict[str, str] = {}  # display name -> C# P enum name
        self.dorms: dict[str, str] = {}        # 地点名或别名 -> C# Place 枚举名
        self.vocab = Vocab()                   # 故事自己声明的词汇（阵营、体型、时段、行动、价值、群落）
        self.design_blocks: list[tuple[Line, list[Line]]] = []
        self.design: dict[str, Any] = {}
        self.object_ids: dict[str, str] = {}   # 物件名或别名 -> 编号
        self.person_design: dict[str, dict[str, Any]] = {}   # C# P 枚举名 -> 设计数据（群落、值守、倾向……）
        self.counter = 0
        self.chapter_id = ""

    def emit(self, text: str, line: int) -> None:
        for piece in text.split("\n"):
            self.out.append(piece)
            self.map.append(line)

    def variable(self, entity_id: str) -> str:
        self.counter += 1
        base = re.sub(r"\W", "_", entity_id)
        return f"e{self.counter}_{base}"

    def translate(self, raw: str, lines: list[Line] | None = None) -> str:
        lines = read_lines(raw) if lines is None else lines
        self.prescan_people(lines)
        self.prescan_vocab(lines)
        i = 0
        have_chapter = False
        raw_lines = raw.splitlines()
        while i < len(lines):
            line = lines[i]
            if line.indent != 0:
                raise StoryError("顶层内容必须顶格。", line.number)
            head = line.text
            if head.startswith("章节") and ("：" in head or ":" in head):
                key, chapter_id = split_pair(line)
                if key != "章节":
                    raise StoryError("未知块：" + head, line.number)
                requires: list[str] = []
                i += 1
                if i < len(lines) and lines[i].indent == 0 and lines[i].text.startswith("前置章节"):
                    _, value = split_pair(lines[i])
                    requires = [] if value in {"", "无"} else split_list(value)
                    i += 1
                self.emit(f"chapter(id={py(chapter_id)}, requires={py(requires)})", line.number)
                have_chapter = True
                self.chapter_id = chapter_id
                continue
            if not have_chapter:
                raise StoryError("文件必须先写“章节：<id>”。", line.number)
            keyword = head.split(None, 1)[0]
            body, nxt = children(lines, i + 1, 0)
            if keyword == "文案":
                self.ui_text(line, body)
            elif keyword == "请求":
                self.requests(line, body)
            elif keyword in {"地点", "人物", "物件"}:
                name = head[len(keyword):].strip()
                if not name:
                    raise StoryError(f"{keyword}需要名称：{keyword} <名称>", line.number)
                self.entity(keyword, name, line, body)
            elif keyword == "交互点":
                self.site(line, body)
            elif keyword == "家具":
                self.furniture(line, body)
            elif keyword == "选项":
                self.option(line, body)
            elif keyword == "回应":
                self.reaction(line, body)
            elif keyword == "场面":
                self.scene(line, body)
            elif keyword == "原生":
                self.native(line, body, raw_lines)
            elif keyword == "晨间日常":
                self.morning(line, body)
            elif keyword == "出区日常":
                self.departures(line, body)
            elif keyword in {"运行", "运行：", "运行:"} or head.startswith(("运行：", "运行:")):
                pass  # 运行参数由章节数据读取（story_chapter1.read_run）；世界脚本只登记内容。
            elif keyword in DESIGN_BLOCKS:
                self.design_blocks.append((line, body))
            else:
                raise StoryError(f"未知块“{keyword}”。可用：章节、文案、请求、地点、人物、物件、交互点、家具、选项、回应、场面、原生，以及设计块：" + "、".join(sorted(DESIGN_BLOCKS)) + "。", line.number)
            i = nxt
        morning = self.design.get("morning")
        departures = self.design.get("departures", {})
        if departures:
            if morning is None:
                raise StoryError("出区日常须配有晨间日常块。")
            for person_id, routine in departures.items():
                if person_id in {"Det", "Huo", "Shen", "Su"}:
                    raise StoryError("出区日常只用于其余寝区人物。")
                if any(step["room"] in morning["blocked_rooms"] for step in routine["steps"]):
                    raise StoryError("出区日常不能进入封锁生活翼。")
            morning["departures"] = departures
        self.design = compile_design(self.design_blocks, self)
        if morning is not None:
            self.design["morning"] = morning
        return "\n".join(self.out) + "\n"

    def morning(self, head: Line, body: list[Line]) -> None:
        if "morning" in self.design:
            raise StoryError("晨间日常块只能有一个。", head.number)
        fields = self.fields_of(head, body, {"封锁", "旁听", "人物阶段", *self.person_enum})
        morning: dict[str, Any] = {"blocked_rooms": [], "people": {}, "witnesses": []}
        allowed_stages: dict[str, list[str]] = {}
        if "人物阶段" in fields:
            value, line, sub = fields["人物阶段"]
            if value or not sub:
                raise StoryError("人物阶段须使用缩进的人物与阶段列表。", line.number)
            for item in sub:
                name, stages = split_pair(item)
                person_id = table(self.person_enum, name, "人物", item.number)
                if person_id in allowed_stages:
                    raise StoryError(f"人物阶段“{name}”重复。", item.number)
                allowed_stages[person_id] = split_list(stages)
                if not allowed_stages[person_id]:
                    raise StoryError("人物阶段至少须声明一个阶段。", item.number)
        if "封锁" in fields:
            value, line, _ = fields["封锁"]
            morning["blocked_rooms"] = [table(self.dorms, name, "地点", line.number)
                                        for name in split_list(value)]
        actor_ids = {"Det": "detective", "Huo": "huo", "Shen": "shen", "Su": "su"}
        for name, (value, line, sub) in fields.items():
            if name in {"封锁", "人物阶段"}:
                continue
            if name == "旁听":
                if value or not sub:
                    raise StoryError("旁听须使用缩进的房间与感知文字列表。", line.number)
                for item in sub:
                    parts = [part.strip() for part in item.text.removeprefix("- ").split("|", 1)]
                    if not item.text.startswith("- ") or len(parts) != 2 or not all(parts):
                        raise StoryError("旁听须写成“- 房间 | 玩家能听见的文字”。", item.number)
                    morning["witnesses"].append({"room": parts[0], "text": parts[1]})
                continue
            if value or not sub:
                raise StoryError("晨间日常人物字段须使用缩进的步骤列表。", line.number)
            person_id = self.person_enum[name]
            steps = self.daily_steps(sub)
            for step, item in zip(steps, sub):
                if person_id not in actor_ids and step["room"] in morning["blocked_rooms"]:
                    raise StoryError("晨间日常不能让其余寝区人物进入封锁生活翼。", item.number)
            morning["people"][person_id] = {"actor": actor_ids.get(person_id, person_id.lower()), "steps": steps,
                                          "allowed_stages": allowed_stages.get(person_id, [])}
        for person_id in allowed_stages:
            if person_id not in morning["people"]:
                raise StoryError(f"人物阶段“{person_id}”没有晨间日常步骤。", head.number)
        self.design["morning"] = morning

    def departures(self, head: Line, body: list[Line]) -> None:
        if "departures" in self.design:
            raise StoryError("出区日常块只能有一个。", head.number)
        departures: dict[str, Any] = {}
        for name, (value, line, sub) in self.fields_of(head, body, set(self.person_enum)).items():
            if value or not sub:
                raise StoryError("出区日常人物须缩进声明出门分钟与行动。", line.number)
            fields = self.fields_of(line, sub, {"出门分钟", "行动"})
            if set(fields) != {"出门分钟", "行动"}:
                raise StoryError("出区日常人物须同时声明出门分钟与行动。", line.number)
            minutes_text, minutes_line, minutes_sub = fields["出门分钟"]
            minutes = parse_int(minutes_text, "出门分钟", minutes_line.number)
            if minutes_sub or minutes < 0:
                raise StoryError("出门分钟须为非负整数。", minutes_line.number)
            actions, action_line, steps = fields["行动"]
            if actions or not steps:
                raise StoryError("出区行动须使用缩进的步骤列表。", action_line.number)
            person_id = self.person_enum[name]
            departures[person_id] = {"actor": person_id.lower(), "minutes": minutes,
                                     "steps": self.daily_steps(steps)}
        self.design["departures"] = departures

    def daily_steps(self, lines: list[Line]) -> list[dict[str, Any]]:
        steps = []
        for item in lines:
            parts = [part.strip() for part in item.text.removeprefix("- ").split("|")]
            if not item.text.startswith("- ") or len(parts) not in {4, 5, 6, 7} or not all(parts):
                raise StoryError("日常步骤须写成“- 房间 | 目标 | 动作 | 说明 [| 权重 [| 分钟 [| 阶段]]]”。", item.number)
            room, target, action, note = parts[:4]
            if action not in {"check", "listen", "work", "rest", "move"}:
                raise StoryError(f"未知日常动作“{action}”。", item.number)
            weight = parse_int(parts[4], "日常权重", item.number) if len(parts) >= 5 else 1
            minutes = duration_minutes(parts[5], "日常时长", item.number) if len(parts) >= 6 else 10.0
            if weight <= 0:
                raise StoryError("日常权重必须大于零。", item.number)
            steps.append({"room": room, "target": target, "action": action, "note": note,
                          "weight": weight, "minutes": minutes,
                          "allowed_stages": split_list(parts[6]) if len(parts) == 7 else []})
        return steps

    def prescan_people(self, lines: list[Line]) -> None:
        """人物之间可以互相引用（偏见），因此先收集全部人物的 C# 枚举名。"""
        current = None
        for line in lines:
            if line.indent == 0:
                current = line.text[2:].strip() if line.text.startswith("人物 ") else None
            elif current and line.indent == 1 and line.text.startswith(("编号：", "编号:")):
                entity_id = split_pair(line)[1]
                if entity_id.startswith("person."):
                    self.person_enum[current] = entity_id.split(".", 1)[1]

    def prescan_vocab(self, lines: list[Line]) -> None:
        """词汇块与地点先于一切使用者被读入：人物的阵营、宿舍、日常等只认故事自己声明的词。"""
        i = 0
        while i < len(lines):
            line = lines[i]
            body, nxt = children(lines, i + 1, 0)
            keyword = line.text.split(None, 1)[0] if line.indent == 0 else ""
            if keyword in VOCAB_BLOCKS:
                self.vocab.add(keyword, nodes(body), line)
            elif keyword == "地点":
                name = line.text[2:].strip()
                fields = {split_pair(b)[0]: split_pair(b)[1] for b in body if b.indent == 1 and ("：" in b.text or ":" in b.text)}
                entity_id = fields.get("编号", "")
                if entity_id.startswith("room."):
                    local = entity_id.split(".", 1)[1]
                    self.dorms[name] = local
                    for alias in split_list(fields.get("别名", "")):
                        self.dorms[alias] = local
            i = nxt

    def ui_text(self, head: Line, body: list[Line]) -> None:
        values: dict[str, Any] = {}
        j = 0
        while j < len(body):
            key, value = split_pair(body[j])
            if key not in UI_KEYS:
                raise StoryError(f"未知文案键“{key}”。可用：" + "、".join(UI_KEYS), body[j].number)
            ir = UI_KEYS[key]
            if ir in values:
                raise StoryError(f"文案键“{key}”重复。", body[j].number)
            if value:
                values[ir] = value
                j += 1
            else:
                items = []
                j += 1
                while j < len(body) and body[j].text.startswith("- "):
                    items.append(body[j].text[2:].strip())
                    j += 1
                values[ir] = items
        args = ",\n    ".join(f"{k}={py(v)}" for k, v in values.items())
        self.emit(f"ui_text(\n    {args},\n)", head.number)

    def requests(self, head: Line, body: list[Line]) -> None:
        entries = []
        for line in body:
            if not line.text.startswith("- ") or "→" not in line.text:
                raise StoryError("请求须写成“- 标签 → 岗位”。", line.number)
            label, job = [part.strip() for part in line.text[2:].split("→", 1)]
            entries.append({"label": label, "job": table(self.vocab.mapping("jobs"), job, "岗位", line.number)})
        self.emit(f"requests({py(entries)})", head.number)

    def native(self, head: Line, body: list[Line], raw_lines: list[str]) -> None:
        if not body:
            return
        if head.number >= FILE_STRIDE:
            raise StoryError("原生块按根文件的原文行转写，不能放在被引入的片段里。", head.number)
        first, last = body[0].number, body[-1].number
        physical_head = raw_lines[head.number - 1].expandtabs(4)
        trim = len(physical_head) - len(physical_head.lstrip(" ")) + 4
        for number in range(first, last + 1):
            text = raw_lines[number - 1].expandtabs(4)
            self.emit(text[trim:] if text.startswith(" " * trim) else text.lstrip(), number)

    # ---- 交互点、家具、选项、回应、场面 ----
    def fields_of(self, head, body, allowed):
        fields = {}
        j = 0
        while j < len(body):
            line = body[j]
            if line.indent != head.indent + 1:
                raise StoryError("字段缩进错误。", line.number)
            key, value = split_pair(line)
            sub, j = children(body, j + 1, head.indent + 1)
            if key not in allowed:
                raise StoryError(f"{head.text.split()[0]}不支持字段“{key}”。可用：" + "、".join(sorted(allowed)), line.number)
            if key in fields:
                raise StoryError(f"字段“{key}”重复。", line.number)
            fields[key] = (value, line, sub)
        return fields

    def text_of(self, field):
        value, _, sub = field
        return value if value else "\n".join(item.text for item in sub)

    def block_id(self, head, what):
        parts = head.text.split(None, 1)
        if len(parts) != 2:
            raise StoryError(f"{what}需要编号：{what} <编号>", head.number)
        return parts[1].strip()

    def site(self, head, body):
        site_id = self.block_id(head, "交互点")
        f = self.fields_of(head, body, {"地图", "地点", "标题", "物件", "种类", "传说"})
        for need in ("地图", "标题"):
            if need not in f:
                raise StoryError(f"交互点“{site_id}”缺少“{need}”。", head.number)
        args = [f"id={py(site_id)}", f"map={py(f['地图'][0])}", f"title={py(f['标题'][0])}"]
        if "地点" in f:
            args.append(f"room={self.lookup(f['地点'][0], f['地点'][1].number, '地点')}")
        if "物件" in f:
            args.append(f"thing={self.lookup(f['物件'][0], f['物件'][1].number, '物件')}")
        if "种类" in f:
            args.append(f"kind={py(f['种类'][0])}")
        if "传说" in f:
            args.append(f"lore={py(self.text_of(f['传说']))}")
        self.emit("site(" + ", ".join(args) + ")", head.number)

    def furniture(self, head, body):
        for line in body:
            key, value = split_pair(line)
            self.emit(f"furniture(key={py(key)}, text={py(value)})", line.number)

    def option(self, head, body):
        option_id = self.block_id(head, "选项")
        f = self.fields_of(head, body, {"标签", "仅选择"})
        if "标签" not in f:
            raise StoryError(f"选项“{option_id}”缺少“标签”。", head.number)
        selection = parse_bool(f["仅选择"][0], "仅选择", f["仅选择"][1].number) if "仅选择" in f else False
        self.counter += 1
        self.emit(f"@option(id={py(option_id)}, label={py(f['标签'][0])}, selection_only={selection})", head.number)
        self.emit(f"def f{self.counter}(): pass", head.number)

    def condition(self, text, line):
        m = re.fullmatch(r"(?P<who>\S+)\s+(?P<tag>\S+)\s*(?P<op>≥|≤|＜|＞|<|>|=|＝|≠)\s*(?P<n>-?\d+)", text.strip())
        if not m:
            raise StoryError("条件须写成“人物 标签 ≥ 数值”，例如：侦探 信任 ≥ 0。", line)
        who = self.lookup(m["who"], line, "人物")
        return f'state({who}, {py(table(TAGS, m["tag"], "标签", line))}) {OPS[m["op"]]} {int(m["n"])}'

    def reaction(self, head, body):
        reaction_id = self.block_id(head, "回应")
        head_fields = {"角色", "意图", "条件"}
        split = 0
        while split < len(body) and body[split].indent == 1 and split_pair_safe(body[split])[0] in head_fields:
            split += 1
        f = self.fields_of(head, body[:split], head_fields)
        for need in head_fields:
            if need not in f:
                raise StoryError(f"回应“{reaction_id}”缺少“{need}”。", head.number)
        actor = self.lookup(f["角色"][0], f["角色"][1].number, "人物")
        intent = table(INTENTS, f["意图"][0], "意图", f["意图"][1].number)
        goal = self.condition(f["条件"][0], f["条件"][1].number)
        self.counter += 1
        self.emit(f"@reaction(id={py(reaction_id)}, actor={actor}, intent={py(intent)}, goal={goal})", head.number)
        self.emit(f"def f{self.counter}():", head.number)
        self.steps(body[split:], head)

    def scene(self, head, body):
        scene_id = self.block_id(head, "场面")
        args = [f"id={py(scene_id)}"]
        split = 0
        found = {}
        while split < len(body) and body[split].indent == 1:
            key = split_pair_safe(body[split])[0]
            if key not in RESERVED_SCENE_FIELDS:
                break
            found[key] = split_pair(body[split])[1]
            split += 1
        if "事件" not in found:
            raise StoryError(f"场面“{scene_id}”缺少“事件”。", head.number)
        args.append(f"event={py(found['事件'])}")
        if "日" in found:
            args.append(f"day={parse_int(found['日'], '日', head.number)}")
        if "时段" in found:
            args.append(f"slot={py(table(self.vocab.mapping('slots'), found['时段'], '时段', head.number))}")
        self.counter += 1
        self.emit("@sequence(" + ", ".join(args) + ")", head.number)
        self.emit(f"def f{self.counter}():", head.number)
        self.steps(body[split:], head)

    def steps(self, body, head):
        """场面与回应的正文：对白行原样成组，`选择`、`动作` 为指令行。"""
        run = []

        def flush():
            if not run:
                return
            self.emit("    ```", run[0].number)
            for item in run:
                self.emit("    " + item.text, item.number)
            self.emit("    ```", run[-1].number)
            run.clear()

        wrote = False
        for line in body:
            if line.indent != 1:
                raise StoryError("正文缩进错误。", line.number)
            if line.text.startswith("选择 "):
                flush()
                key, value = split_pair(Line(line.number, 0, line.text[3:]))
                self.emit(f"    choice(id={py(key)}, options={py(split_list(value))})", line.number)
            elif line.text.startswith("动作 "):
                flush()
                key, value = split_pair(Line(line.number, 0, line.text[3:]))
                args = [self.scalar(word, line.number) for word in split_list(value)]
                self.emit(f"    action(name={py(key)}, args={py(args)})", line.number)
            else:
                _, _, line_id = split_tags(line.text, line.number)
                note_line(f"{self.chapter_id}.{self.block_id(head, '场面')}", line_id, line.number)
                run.append(line)
            wrote = True
        flush()
        if not wrote:
            self.emit("    pass", head.number)

    @staticmethod
    def scalar(word, line):
        if word in {"真", "是"}:
            return True
        if word in {"假", "否"}:
            return False
        try:
            return int(word)
        except ValueError:
            return word

    def lookup(self, name: str, line: int, what: str = "对象") -> str:
        if name not in self.vars:
            raise StoryError(f"引用了尚未声明的{what}“{name}”。请把它写在使用者之前。", line)
        return self.vars[name]

    def entity(self, keyword: str, name: str, head: Line, body: list[Line]) -> None:
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
        if "编号" not in fields:
            raise StoryError(f"{keyword}“{name}”缺少“编号”。", head.number)
        entity_id = fields["编号"][0]
        aliases = split_list(fields["别名"][0]) if "别名" in fields else []
        scope = table(SCOPES, fields["范围"][0], "范围", fields["范围"][1].number) if "范围" in fields else "world"
        var = fields["代号"][0] if "代号" in fields else self.variable(entity_id)
        args = [f"id={py(entity_id)}", f"name={py(name)}"]
        if aliases:
            args.append(f"aliases={py(aliases)}")
        py_kind = {"地点": "room", "人物": "character", "物件": "object"}[keyword]
        allowed = {"编号", "别名", "范围", "代号"}
        if keyword != "地点":
            place_key = "所在" if keyword == "人物" else "位置"
            allowed.add(place_key)
            if place_key not in fields:
                raise StoryError(f"{keyword}“{name}”缺少“{place_key}”。", head.number)
            args.append(f"room={self.lookup(fields[place_key][0], fields[place_key][1].number, '地点')}")
        args.append(f"scope={py(scope)}")
        if keyword == "人物":
            if entity_id.startswith("person."):
                self.person_enum[name] = entity_id.split(".", 1)[1]
            for key in ("属性", "认知", "可见", "立绘"):
                allowed.add(key)
            args.extend(self.extras(fields, "state", "belief", "observable", "sprite", name))
            allowed |= set(PROFILE_FIELDS_ZH) | PERSON_FIELDS | CHAPTER_PERSONA_FIELDS
            args.append("profile=" + self.profile(name, fields, head))
            if entity_id.startswith("person."):
                self.person_design[entity_id.split(".", 1)[1]] = compile_person(name, fields, self, head)
        elif keyword == "物件":
            allowed |= {"属性", "可见"}
            args.extend(self.extras(fields, "state", None, "observable", None, name))
        for key, (_, line, _) in fields.items():
            if key not in allowed:
                raise StoryError(f"{keyword}不支持字段“{key}”。", line.number)
        if keyword == "物件":
            self.object_ids[name] = entity_id
            for alias in aliases:
                self.object_ids[alias] = entity_id
        self.vars[name] = var
        for alias in aliases:
            self.vars[alias] = var
        call = f"{var} = {py_kind}(" + ", ".join(args) + ")"
        self.emit(call, head.number)

    def extras(self, fields, state_key, belief_key, observable_key, sprite_key, name) -> list[str]:
        result = []
        if "属性" in fields and state_key:
            result.append(f"state={py(self.kv_ints(fields['属性']))}")
        if "认知" in fields and belief_key:
            result.append(f"belief={py(self.kv_ints(fields['认知']))}")
        if "可见" in fields and observable_key:
            value, line, sub = fields["可见"]
            tags = split_list(value) if value else [item.text.lstrip("- ").strip() for item in sub]
            result.append(f"observable={py({tag: True for tag in tags})}")
        if "立绘" in fields and sprite_key:
            result.append(f"sprite={py(fields['立绘'][0])}")
        return result

    def kv_ints(self, field) -> dict[str, int]:
        value, line, sub = field
        pairs: dict[str, int] = {}
        entries = split_list(value) if value else [item.text.lstrip("- ").strip() for item in sub]
        for entry in entries:
            parts = entry.rsplit(None, 1)
            if len(parts) != 2:
                raise StoryError(f"需要“名称 数值”：{entry}", line.number)
            pairs[parts[0]] = parse_int(parts[1], "数值", line.number)
        return pairs

    def profile(self, name: str, fields, head: Line) -> str:
        def get(key: str, default=None):
            return fields[key] if key in fields else default

        def need(key: str) -> tuple[str, Line, list[Line]]:
            if key not in fields:
                raise StoryError(f"人物“{name}”缺少“{key}”。", head.number)
            return fields[key]

        group = table(self.vocab.mapping("groups"), need("阵营")[0], "阵营", need("阵营")[1].number)
        dorm = table(self.dorms, need("宿舍")[0], "宿舍", need("宿舍")[1].number)
        build = table(self.vocab.mapping("builds"), need("体型")[0], "体型", need("体型")[1].number)
        speed = parse_number(need("速度")[0], "速度", need("速度")[1].number)
        gender = table(GENDERS, need("性别")[0], "性别", need("性别")[1].number)
        profile: dict[str, Any] = {
            "group": group, "dorm": dorm, "speed": speed, "cooperation": 0, "build": build,
            "desire": need("欲望")[0], "demon_line": need("魔音台词")[0],
            "desire_flags": [], "strength": 0, "lock": 0, "signal": 0, "structure": 0,
            "greenhouse_skill": 0, "cook": 0, "craftsman": False, "values": {}, "prejudice": {},
            "demon_prejudice": 0, "habits": [], "voice": "", "gender": gender,
        }
        for key, ir in (("配合", "cooperation"), ("力气", "strength"), ("开锁", "lock"), ("信号", "signal"),
                        ("结构", "structure"), ("温室", "greenhouse_skill"), ("厨艺", "cook"), ("魔偏见", "demon_prejudice")):
            if key in fields:
                profile[ir] = parse_int(fields[key][0], key, fields[key][1].number)
        if "工匠" in fields:
            profile["craftsman"] = parse_bool(fields["工匠"][0], "工匠", fields["工匠"][1].number)
        if "口吻" in fields:
            profile["voice"] = fields["口吻"][0]
        if "欲望标志" in fields:
            value, line, _ = fields["欲望标志"]
            profile["desire_flags"] = [table(self.vocab.mapping("flags"), word, "价值名", line.number) for word in split_list(value)]
        if "看重" in fields:
            value, line, sub = fields["看重"]
            entries = split_list(value) if value else [item.text.lstrip("- ").strip() for item in sub]
            for entry in entries:
                parts = entry.rsplit(None, 1)
                if len(parts) != 2:
                    raise StoryError(f"“看重”需要“名称 数值”：{entry}", line.number)
                profile["values"][table(self.vocab.mapping("flags"), parts[0], "价值名", line.number)] = parse_int(parts[1], "数值", line.number)
        if "偏见" in fields:
            value, line, sub = fields["偏见"]
            entries = split_list(value) if value else [item.text.lstrip("- ").strip() for item in sub]
            for entry in entries:
                parts = entry.rsplit(None, 1)
                if len(parts) != 2:
                    raise StoryError(f"“偏见”需要“人物 数值”：{entry}", line.number)
                if parts[0] not in self.person_enum:
                    raise StoryError(f"偏见引用了尚未声明的人物“{parts[0]}”。", line.number)
                profile["prejudice"][self.person_enum[parts[0]]] = parse_int(parts[1], "数值", line.number)
        if "日常" in fields:
            _, line, sub = fields["日常"]
            profile["habits"] = [self.habit(item) for item in sub]
        return py(profile)

    def habit(self, item: Line) -> dict[str, Any]:
        m = re.fullmatch(r"(?P<slots>\S+)\s+(?P<job>\S+)\s+重要\s+(?P<w>-?\d+)(?P<rest>.*)", item.text)
        if not m:
            raise StoryError("日常须写成“时段、时段 行动 重要 n [合欲望] [备注：…]”。", item.number)
        rest = m["rest"].strip()
        habit: dict[str, Any] = {
            "job": table(self.vocab.mapping("jobs"), m["job"], "行动", item.number),
            "slots": [table(self.vocab.mapping("slots"), word, "时段", item.number) for word in split_list(m["slots"])],
            "weight": int(m["w"]),
        }
        if rest.startswith("合欲望"):
            habit["desire"] = True
            rest = rest[3:].strip()
        if rest.startswith("备注"):
            habit["note"] = rest.split("：", 1)[-1].strip() if "：" in rest else rest[2:].strip()
        elif rest:
            raise StoryError(f"日常行末有无法识别的内容：{rest}", item.number)
        return habit


GENDERS = {"男": "Male", "女": "Female"}
# 由章节读取器（story_chapter1.read_person）解释的人物认知初值；世界脚本只放行。
CHAPTER_PERSONA_FIELDS = {"初醒信任", "谨慎", "说话习惯"}
PROFILE_FIELDS_ZH = {"性别", "阵营", "宿舍", "速度", "配合", "体型", "欲望", "魔音台词", "欲望标志", "力气", "开锁",
                     "信号", "结构", "温室", "厨艺", "工匠", "看重", "偏见", "魔偏见", "日常", "口吻"}


def compile_story(paths: list[Path], external: set[str], *, ecology_only: bool = False,
                  section: str | None = None) -> dict[str, Any]:
    if ecology_only and section is not None:
        raise ws.ScriptError("生态编译不能同时选择续篇。")
    texts: dict[str, str] = {}
    maps: dict[str, list[int]] = {}
    designs: list[dict[str, Any]] = []
    world_paths = []
    for path in paths:
        try:
            raw = path.read_text(encoding="utf-8-sig")
        except OSError as exc:
            raise ws.ScriptError(f"无法读取脚本：{exc}") from exc
        translator = Translator()
        try:
            lines = source_lines(path, ecology_only=ecology_only, section=section)
            if ecology_only:
                # Morning loops belong to this chapter, but are not inherited by later chapters.
                original = expand_includes(path)
                i = 0
                while i < len(original):
                    head = original[i]
                    body, i = children(original, i + 1, 0)
                    if head.text in {"晨间日常", "出区日常"}:
                        lines.extend([head, *body])
            texts[path.as_posix()] = translator.translate(raw, lines)
        except StoryError as exc:
            file, line = origin(exc.line or 1, path.as_posix())
            source = ws.Source(file, line, 1, line, 1)
            raise ws.ScriptError(str(exc), source) from exc
        maps[path.as_posix()] = translator.map
        designs.append(translator.design)
        world_paths.append(path)
    try:
        document = ws.Compiler(world_paths, external, texts=texts).parse()
        # Source IDs remain stable IR keys; locations point back to the author's
        # physical file, including a continuation's original (unlifted) line.
        for location in document.get("source_map", {}).values():
            mapped = maps.get(location["path"])
            if mapped is not None:
                root = location["path"]
                for key in ("line", "end_line"):
                    generated = location[key]
                    if 0 < generated <= len(mapped):
                        file, location[key] = origin(mapped[generated - 1], root)
                        if key == "line":
                            location["path"] = file
        for chapter, design in zip(document["chapters"], designs):
            if design:
                chapter["design"] = design
        return document
    except ws.ScriptError as exc:
        if exc.source and exc.source.path in maps:
            mapped = maps[exc.source.path]
            line = mapped[exc.source.line - 1] if 0 < exc.source.line <= len(mapped) else exc.source.line
            file, line = origin(line, exc.source.path)
            exc.source = ws.Source(file, line, 1, line, 1)
        raise


def main(argv: list[str] | None = None) -> int:
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("scripts", nargs="+", type=Path)
    parser.add_argument("--external-chapter", action="append", default=[], metavar="ID")
    parser.add_argument("--section", metavar="ID", help="编译同一 story 文件中的指定续篇，并共享主体生态")
    parser.add_argument("-o", "--output", type=Path)
    parser.add_argument("--check", action="store_true", help="fail if --output is missing or differs from the compiled result")
    args = parser.parse_args(argv)
    try:
        document = compile_story(args.scripts, set(args.external_chapter), section=args.section)
    except ws.ScriptError as exc:
        if exc.source:
            print(f"{exc.source.path}:{exc.source.line}: {exc}", file=sys.stderr)
        else:
            print(str(exc), file=sys.stderr)
        return 2
    serialized = json.dumps(document, ensure_ascii=False, indent=2) + "\n"
    if args.check:
        if args.output is None:
            print("--check 需要同时给出 -o。", file=sys.stderr)
            return 2
        current = args.output.read_text(encoding="utf-8") if args.output.exists() else None
        if current is None or current.replace("\r\n", "\n") != serialized:
            print(f"{args.output} 已过期，请重新生成。", file=sys.stderr)
            return 1
        return 0
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(serialized, encoding="utf-8", newline="\n")
    else:
        sys.stdout.write(serialized)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
