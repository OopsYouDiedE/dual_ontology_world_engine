#!/usr/bin/env python3
"""Compile human-readable `.story` chapter scripts to the world-script JSON IR.

A `.story` file is read as indentation-structured Chinese declarations and translated
to the statements `world_script.py` already compiles; the typed IR and its validation
stay in one place. Nothing here evaluates author code.

Blocks (top-level, 4-space indented bodies; `#` starts a comment):
  章节：<id>            前置章节：<id>、<id>
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
from story_base import Line, StoryError, children, read_lines, split_list, split_pair  # noqa: E402,F401
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

    def emit(self, text: str, line: int) -> None:
        for piece in text.split("\n"):
            self.out.append(piece)
            self.map.append(line)

    def variable(self, entity_id: str) -> str:
        self.counter += 1
        base = re.sub(r"\W", "_", entity_id)
        return f"e{self.counter}_{base}"

    def translate(self, raw: str) -> str:
        lines = read_lines(raw)
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
            elif keyword in DESIGN_BLOCKS:
                self.design_blocks.append((line, body))
            else:
                raise StoryError(f"未知块“{keyword}”。可用：章节、文案、请求、地点、人物、物件、交互点、家具、选项、回应、场面、原生，以及设计块：" + "、".join(sorted(DESIGN_BLOCKS)) + "。", line.number)
            i = nxt
        self.design = compile_design(self.design_blocks, self)
        return "\n".join(self.out) + "\n"

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
        first, last = body[0].number, body[-1].number
        for number in range(first, last + 1):
            text = raw_lines[number - 1].expandtabs(4)
            self.emit(text[4:] if text.startswith("    ") else text.lstrip(), number)

    # ---- 交互点、家具、选项、回应、场面 ----
    def fields_of(self, head, body, allowed):
        fields = {}
        j = 0
        while j < len(body):
            line = body[j]
            if line.indent != 1:
                raise StoryError("字段缩进错误。", line.number)
            key, value = split_pair(line)
            sub, j = children(body, j + 1, 1)
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
            allowed |= set(PROFILE_FIELDS_ZH) | PERSON_FIELDS
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
        profile: dict[str, Any] = {
            "group": group, "dorm": dorm, "speed": speed, "cooperation": 0, "build": build,
            "desire": need("欲望")[0], "demon_line": need("魔音台词")[0],
            "desire_flags": [], "strength": 0, "lock": 0, "signal": 0, "structure": 0,
            "greenhouse_skill": 0, "cook": 0, "craftsman": False, "values": {}, "prejudice": {},
            "demon_prejudice": 0, "habits": [], "voice": "",
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


PROFILE_FIELDS_ZH = {"阵营", "宿舍", "速度", "配合", "体型", "欲望", "魔音台词", "欲望标志", "力气", "开锁",
                     "信号", "结构", "温室", "厨艺", "工匠", "看重", "偏见", "魔偏见", "日常", "口吻"}


def compile_story(paths: list[Path], external: set[str]) -> dict[str, Any]:
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
            texts[path.as_posix()] = translator.translate(raw)
        except StoryError as exc:
            source = ws.Source(path.as_posix(), exc.line or 1, 1, exc.line or 1, 1)
            raise ws.ScriptError(str(exc), source) from exc
        maps[path.as_posix()] = translator.map
        designs.append(translator.design)
        world_paths.append(path)
    try:
        document = ws.Compiler(world_paths, external, texts=texts).parse()
        for chapter, design in zip(document["chapters"], designs):
            if design:
                chapter["design"] = design
        return document
    except ws.ScriptError as exc:
        if exc.source and exc.source.path in maps:
            mapped = maps[exc.source.path]
            line = mapped[exc.source.line - 1] if 0 < exc.source.line <= len(mapped) else exc.source.line
            exc.source = ws.Source(exc.source.path, line, 1, line, 1)
        raise


def main(argv: list[str] | None = None) -> int:
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("scripts", nargs="+", type=Path)
    parser.add_argument("--external-chapter", action="append", default=[], metavar="ID")
    parser.add_argument("-o", "--output", type=Path)
    parser.add_argument("--check", action="store_true", help="fail if --output is missing or differs from the compiled result")
    args = parser.parse_args(argv)
    try:
        document = compile_story(args.scripts, set(args.external_chapter))
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
