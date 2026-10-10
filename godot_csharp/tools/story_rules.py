#!/usr/bin/env python3
"""`.story` 设计块之二：条件短语、规矩、规矩格、结局、案件。

条件只用白名单短语，未知短语直接报错并指出行号；编译成 JSON 树，由 C# 的小型求值器执行
（求值器是 Core 之外的通用算法，条件的内容——日期、人物、物件、标记——全部写在剧本里）。
"""
from __future__ import annotations

import re
from typing import Any

from story_base import Line, StoryError, split_list
from story_design import Node, rows, unescape

CMP = {"≥": ">=", "≤": "<=", "＜": "<", "＞": ">", "=": "==", "＝": "==", "≠": "!=", "<": "<", ">": ">"}


def fail(message: str, line: int):
    raise StoryError(message, line)


class CondParser:
    """案件与证词用的条件短语。且／或／并非 连接；“且”先于“或”。原子短语：

      恒真
      夜里 第 N 日 <人> <行动>、<行动> [至少 M 次]     昨夜的人物行动记录
      夜巡 第 N 日 <地点> [<体型>] [见证 <人>] [是 <人>]  夜里望见的身影（不写体型即任何身量）
      状态 <物件> <标记> ≥|≤|＜|＞|=|≠ <数>             客观世界里的数值
      见过 <物件> <标记>                               自案发日起亲见过（只要看到过这个标记）
      见到 <物件> <标记>                               自案发日起亲见到它的数值大于 0
      细看过 <物件> / 问过 <人> / 读过 <规矩>           侦探的调查记录与某人读过的规矩
      已查清 <痕迹>                                    侦探对某项痕迹已经查清
    """

    def __init__(self, ctx: Any, rules: dict[str, str], traces: dict[str, int], line: int) -> None:
        self.ctx, self.rules, self.traces, self.line = ctx, rules, traces, line

    def parse(self, text: str) -> dict[str, Any]:
        ors = [self.conj(part.strip()) for part in re.split(r"\s或\s", text.strip())]
        return ors[0] if len(ors) == 1 else {"op": "any", "items": ors}

    def conj(self, text: str) -> dict[str, Any]:
        ands = [self.atom(part.strip()) for part in re.split(r"\s且\s", text)]
        return ands[0] if len(ands) == 1 else {"op": "all", "items": ands}

    def person(self, word: str) -> str:
        if word not in self.ctx.person_enum:
            fail(f"条件引用了未声明的人物“{word}”。", self.line)
        return self.ctx.person_enum[word]

    def place(self, word: str) -> str:
        if word not in self.ctx.dorms:
            fail(f"条件引用了未声明的地点“{word}”。", self.line)
        return self.ctx.dorms[word]

    def thing(self, word: str) -> str:
        if word not in self.ctx.object_ids:
            fail(f"条件引用了未声明的物件“{word}”。", self.line)
        return self.ctx.object_ids[word]

    def job(self, word: str) -> str:
        jobs = self.ctx.vocab.mapping("jobs")
        if word not in jobs:
            fail(f"条件引用了未知行动“{word}”。", self.line)
        return jobs[word]

    def atom(self, text: str) -> dict[str, Any]:
        if text.startswith("并非 "):
            return {"op": "not", "item": self.atom(text[3:].strip())}
        if text == "恒真":
            return {"op": "true"}
        m = re.fullmatch(r"夜里 第\s*(\d+)\s*日 (\S+) (\S+?)(?: 至少\s*(\d+)\s*次)?", text)
        if m:
            return {"op": "night_did", "day": int(m[1]), "who": self.person(m[2]),
                    "jobs": [self.job(w) for w in split_list(m[3])], "min": int(m[4]) if m[4] else 1}
        m = re.fullmatch(r"夜巡 第\s*(\d+)\s*日 (\S+)(?: (?!见证|是)(\S+))?(?: 见证 (\S+))?(?: 是 (\S+))?", text)
        if m:
            builds = self.ctx.vocab.mapping("builds")
            if m[3] is not None and m[3] not in builds:
                fail(f"条件引用了未知体型“{m[3]}”。", self.line)
            node = {"op": "sighting", "day": int(m[1]), "at": self.place(m[2])}
            if m[3] is not None:
                node["build"] = builds[m[3]]
            if m[4]:
                node["witness"] = self.person(m[4])
            if m[5]:
                node["truth"] = self.person(m[5])
            return node
        m = re.fullmatch(r"状态 (\S+) (\S+) (≥|≤|＜|＞|=|＝|≠|<|>) (-?\d+)", text)
        if m:
            return {"op": "state", "thing": self.thing(m[1]), "tag": m[2], "cmp": CMP[m[3]], "n": int(m[4])}
        m = re.fullmatch(r"(见过|见到) (\S+) (\S+)", text)
        if m:
            return {"op": "saw_key" if m[1] == "见过" else "saw_pos", "thing": self.thing(m[2]), "tag": m[3]}
        m = re.fullmatch(r"细看过 (\S+)", text)
        if m:
            return {"op": "looked", "thing": self.thing(m[1])}
        m = re.fullmatch(r"问过 (\S+)", text)
        if m:
            return {"op": "asked", "who": self.person(m[1])}
        m = re.fullmatch(r"读过 (\S+)", text)
        if m:
            if m[1] not in self.rules:
                fail(f"条件引用了未声明的规矩“{m[1]}”。", self.line)
            return {"op": "rule_known", "rule": self.rules[m[1]]}
        m = re.fullmatch(r"已查清 (\S+)", text)
        if m:
            if m[1] not in self.traces:
                fail(f"条件引用了未声明的痕迹“{m[1]}”。", self.line)
            return {"op": "trace_known", "trace": self.traces[m[1]]}
        fail(f"无法识别的条件短语：{text}", self.line)


# ------------------------------------------------------------------ 规矩 --

def split_flags(ctx: Any, text: str, line: int) -> list[str]:
    flags = ctx.vocab.mapping("flags")
    result = []
    for word in split_list(text):
        if word not in flags:
            fail(f"未知价值标记“{word}”。", line)
        result.append(flags[word])
    return result


def compile_rules(blocks: list[tuple[Line, list[Node]]], ctx: Any) -> list[dict[str, Any]]:
    """`规矩 <名称>`：编号、立规者（人物或“恶魔”）、内容、标记、张贴、格。"""
    rules: list[dict[str, Any]] = []
    slots = ctx.vocab.mapping("slots")
    for head, body in blocks:
        name = head.text[2:].strip()
        fields = {n.pair()[0]: (n.pair()[1], n) for n in body if n.pair()[0] != "张贴"}
        unknown = set(fields) - {"编号", "立规者", "内容", "标记", "格", "针对", "有效期", "执行"}
        if unknown:
            fail(f"规矩不支持字段“{sorted(unknown)[0]}”。", head.number)
        for need in ("编号", "立规者", "内容", "标记"):
            if need not in fields:
                fail(f"规矩“{name}”缺少“{need}”。", head.number)
        author = fields["立规者"][0]
        entry: dict[str, Any] = {"name": name, "id": fields["编号"][0], "text": unescape(fields["内容"][0]),
                                 "flags": split_flags(ctx, fields["标记"][0], fields["标记"][1].number)}
        lifetime = fields["有效期"][0] if "有效期" in fields else "world"
        if lifetime not in {"world", "chapter", "interaction"}:
            fail("规则有效期须为 world、chapter 或 interaction。", head.number)
        entry["lifetime"] = lifetime
        from story_ast import rules as ast_rules
        # Metadata-only declarations carry no executable rules. Explicit AST rows stay strict.
        entry["rules"] = ast_rules(_records(fields["执行"][1]) if "执行" in fields else {}, head.number)
        if author == "恶魔":
            entry["demon"] = True
        else:
            if author not in ctx.person_enum:
                fail(f"立规者“{author}”未声明。", fields["立规者"][1].number)
            entry["author"] = ctx.person_enum[author]
        if "格" in fields:
            entry["card"] = fields["格"][0]
            entry["card_line"] = fields["格"][1].number
        if "针对" in fields:
            target = fields["针对"][0]
            if target not in ctx.person_enum:
                fail(f"针对的人物“{target}”未声明。", fields["针对"][1].number)
            entry["target"] = ctx.person_enum[target]
        posts = []
        for node in body:
            key, value = node.pair()
            if key != "张贴":
                continue
            m = re.fullmatch(r"第\s*(\d+)\s*日 (\S+) (\S+?)(?: (集中|单条|静默))?", value)
            if not m:
                fail("张贴须写成“第 2 日 傍晚 中央厅 [集中|单条|静默]”。", node.number)
            if m[2] not in slots:
                fail(f"未知时段“{m[2]}”。", node.number)
            if m[3] not in ctx.dorms:
                fail(f"张贴引用了未声明的地点“{m[3]}”。", node.number)
            posts.append({"day": int(m[1]), "slot": slots[m[2]], "place": ctx.dorms[m[3]], "mode": m[4] or "单条"})
        if posts:
            entry["posts"] = posts
        rules.append(entry)
    ids = [r["id"] for r in rules]
    if len(set(ids)) != len(ids):
        fail("规矩编号重复。", blocks[0][0].number if blocks else 0)
    return rules


AXES = (("主语", "subjects"), ("条件", "conds"), ("行为", "behaviors"), ("例外", "exceptions"), ("记账", "accounts"))
AXIS_NAME = {axis: word for word, axis in AXES}


def _records(node):
    from story_chapter1 import scalar
    result = {}
    for kid in node.kids:
        key, value = kid.pair()
        if key in result: fail('重复 AST 字段：'+key,kid.number)
        result[key] = _records(kid) if kid.kids else scalar(value)
    return result


def compile_card_kit(tree: list[Node], ctx: Any, head: Line) -> dict[str, Any]:
    """规矩格：五格拼规则的各格选项，以及每个选项带的价值标记。

    行：`- 名称 | 标记`；行为多一列编号：`- 名称 | 编号 | 标记`；
    标记可写成 `若有 A、B：X、Y 否则：Z`（有 A、B 之一时取 X、Y，否则取 Z）。
    """
    kit: dict[str, Any] = {}
    wordings: dict[str, list[dict[str, Any]]] = {}
    axes = dict(AXES)
    listings = {"案件说法": "case", "名录库说法": "archive"}
    for node in tree:
        key, _ = node.pair()
        if key in listings:
            continue
        axis = axes.get(key)
        if axis is None:
            fail(f"规矩格不支持“{key}”。", node.number)
        items = []
        for kid in node.kids:
            if not kid.is_item:
                fail("格的选项每行须以“- ”开头。", kid.number)
            cells = [cell.strip() for cell in kid.item.split("|")]
            entry: dict[str, Any] = {"text": cells[0], "flags": []}
            if axis == "behaviors":
                if len(cells) < 2:
                    fail("行为须写成“- 名称 | 编号 | 标记”。", kid.number)
                entry["id"] = cells[1]
                cells = [cells[0]] + cells[2:]
            if len(cells) > 1 and cells[1]:
                m = re.fullmatch(r"若有 (\S+)：(\S+) 否则：(\S+)", cells[1])
                if m:
                    entry["if_any"] = split_flags(ctx, m[1], kid.number)
                    entry["flags"] = split_flags(ctx, m[2], kid.number)
                    entry["else_flags"] = split_flags(ctx, m[3], kid.number)
                else:
                    entry["flags"] = split_flags(ctx, cells[1], kid.number)
            from story_ast import expression, rules
            extra = _records(kid)
            if set(extra)-{'id','condition','rules','witnessed','manipulative','targeted'}:
                fail('未知规则格 AST 字段。',kid.number)
            entry.update(extra)
            if axis in ('subjects','conds'):
                if 'condition' not in entry: fail('主体和条件格须声明 condition。',kid.number)
                entry['condition'] = expression(entry['condition'],'c',kid.number)
            if axis == 'behaviors':
                if 'rules' not in entry: fail('行为格须声明 rules。',kid.number)
                entry['rules'] = rules(entry['rules'],kid.number)
            if 'witnessed' in entry: entry['witnessed'] = expression(entry['witnessed'],'c',kid.number)
            if 'id' not in entry: fail('规则格选项须声明 id。',kid.number)
            items.append(entry)
        if len({e['id'] for e in items})!=len(items): fail('规则格选项 ID 重复。',node.number)
        kit[axis] = items
    for _, axis in AXES:
        if axis not in kit:
            fail(f"规矩格缺少“{AXIS_NAME[axis]}”。", head.number)
    for node in tree:
        key, _ = node.pair()
        if key not in listings:
            continue
        listing = []
        for kid in node.kids:
            if not kid.is_item:
                fail("说法每行须以“- ”开头。", kid.number)
            cells = [cell.strip() for cell in kid.item.split("|")]
            if len(cells) != 6:
                fail("说法须写成“- 主语 | 条件 | 行为 | 例外 | 记账 | 说法”。", kid.number)
            listing.append({"card": resolve_card(kit, cells[:5], kid.number), "text": cells[5]})
        wordings[listings[key]] = listing
    kit["wordings"] = wordings
    return kit


def resolve_card(kit: dict[str, Any], cells: list[str], line: int) -> list[int]:
    card = []
    for cell, (_, axis) in zip(cells, AXES):
        names = [e["text"] for e in kit[axis]]
        if cell not in names:
            fail(f"格里的“{cell}”不是“{AXIS_NAME[axis]}”里已声明的选项。", line)
        card.append(names.index(cell))
    return card


def compile_endings(tree: list[Node]) -> list[dict[str, str]]:
    return [{"id": cells[0], "text": cells[1]} for cells in rows(tree, 2, "结局")]


# ------------------------------------------------------------------ 案件 --

def _subst(node: Node, mapping: dict[str, str]) -> Node:
    text = node.line.text
    for key, value in mapping.items():
        text = text.replace(key, value)
    return Node(Line(node.line.number, node.line.indent, text), [_subst(kid, mapping) for kid in node.kids])


def expand_templates(items: list[Node], ctx: Any) -> list[tuple[Node, dict[str, str]]]:
    """主张与证据的模板：条目里写 {人} 即对全体住民（侦探除外）各展开一条，{人寝区} 是此人的寝区；
    写 {寝区} 即对各寝区各展开一条。不带占位符的条目原样保留。返回 (展开后的条目, 绑定)。"""
    people = [(name, enum) for name, enum in ctx.person_enum.items() if enum != "Det" and name in ctx.person_dorm]
    dorms = sorted({dorm for dorm in ctx.person_dorm.values()}, key=lambda d: list(ctx.dorms).index(d) if d in ctx.dorms else 0)
    result: list[tuple[Node, dict[str, str]]] = []
    for node in items:
        whole = node.line.text + "".join(_flatten(node))
        if "{人}" in whole or "{人寝区}" in whole:
            for name, enum in people:
                bind = {"{人}": name, "{人寝区}": ctx.person_dorm[name]}
                result.append((_subst(node, bind), {"person": enum, "template": node.item}))
        elif "{寝区}" in whole:
            for dorm in dorms:
                result.append((_subst(node, {"{寝区}": dorm}), {"dorm": ctx.dorms[dorm], "template": node.item}))
        else:
            result.append((node, {}))
    return result


def _flatten(node: Node) -> list[str]:
    out = []
    for kid in node.kids:
        out.append(kid.line.text)
        out.extend(_flatten(kid))
    return out


CLAIM_TOPICS = {"取用": "take", "门闩": "latch", "台账": "ledger", "任意": "any"}


def compile_case(tree: list[Node], ctx: Any, rules: dict[str, str], head: Line) -> dict[str, Any]:
    """`案件`：痕迹、事实、重演变量、主张、证据。结构见 chapter_01.story。
    主张与证据可写模板（{人}、{人寝区}、{寝区}），由编译器按住民与寝区展开；展开项带 person／dorm 与原模板文字。"""
    case: dict[str, Any] = {}
    sections = {n.pair()[0]: n for n in tree}
    unknown = set(sections) - {"案发夜", "痕迹", "事实", "变量", "主张", "证据"}
    if unknown:
        fail(f"案件不支持“{sorted(unknown)[0]}”。", head.number)
    for need in ("案发夜", "痕迹", "事实", "变量", "主张", "证据"):
        if need not in sections:
            fail(f"案件缺少“{need}”。", head.number)
    case["night"] = int(sections["案发夜"].pair()[1])

    trace_nodes = sections["痕迹"].kids
    trace_rows = rows([Node(n.line, []) for n in trace_nodes], 3, "痕迹")
    traces = {cells[0]: i for i, cells in enumerate(trace_rows)}

    def cond(node: Node, text: str) -> dict[str, Any]:
        return CondParser(ctx, rules, traces, node.number).parse(text)

    case["traces"] = []
    for cells, node in zip(trace_rows, trace_nodes):
        entry = {"id": cells[0], "label": cells[1], "where": cells[2]}
        for sub in node.kids:
            skey, svalue = sub.pair()
            if skey != "查清":
                fail(f"痕迹不支持“{skey}”。", sub.number)
            entry["known"] = cond(sub, svalue)
        if "known" not in entry:
            fail(f"痕迹“{cells[0]}”缺少“查清”条件。", node.number)
        case["traces"].append(entry)
    case["facts"] = {}
    for node in sections["事实"].kids:
        key, value = node.pair()
        case["facts"][key] = cond(node, value)

    variables = []
    for node in sections["变量"].kids:
        if not node.is_item:
            fail("变量每项须以“- ”开头。", node.number)
        options = [kid.text for kid in node.kids]
        if len(options) < 2:
            fail("变量至少两个选项。", node.number)
        variables.append({"question": node.item, "options": options})
    case["variables"] = variables

    evidence = []
    names = []
    for node, bind in expand_templates(sections["证据"].kids, ctx):
        if not node.is_item:
            fail("证据每项须以“- ”开头。", node.number)
        cells = [cell.strip() for cell in node.item.split("|")]
        if len(cells) != 2:
            fail("证据须写成“- 编号 | 名称”，其下写“已有：…”和可选的“亲见：…”。", node.number)
        entry: dict[str, Any] = {"key": cells[0] + (":" + bind["person"] if "person" in bind else ":" + bind["dorm"] if "dorm" in bind else ""), "name": cells[1]}
        if "person" in bind:
            entry["person"] = bind["person"]
        if "dorm" in bind:
            entry["dorm"] = bind["dorm"]
        for sub in node.kids:
            skey, svalue = sub.pair()
            if skey == "已有":
                entry["have"] = cond(sub, svalue)
            elif skey == "亲见":
                entry["saw"] = cond(sub, svalue)
            elif skey == "说话人":
                if svalue not in ctx.person_enum:
                    fail(f"说话人“{svalue}”未声明。", sub.number)
                entry["person"] = ctx.person_enum[svalue]
            else:
                fail(f"证据不支持“{skey}”。", sub.number)
        if "have" not in entry:
            fail(f"证据“{cells[1]}”缺少“已有”。", node.number)
        if cells[1] in names:
            fail(f"证据名称“{cells[1]}”重复。", node.number)
        evidence.append(entry)
        names.append(cells[1])
    case["evidence"] = evidence

    claims = []
    for node, bind in expand_templates(sections["主张"].kids, ctx):
        if not node.is_item:
            fail("主张每项须以“- ”开头。", node.number)
        entry = {"text": node.item}
        if "person" in bind:
            entry["person"] = bind["person"]
            entry["template"] = bind["template"]
        for sub in node.kids:
            skey, svalue = sub.pair()
            if skey == "问":
                if svalue not in CLAIM_TOPICS:
                    fail(f"主张的“问”须是{'、'.join(CLAIM_TOPICS)}之一。", sub.number)
                entry["topic"] = CLAIM_TOPICS[svalue]
            elif skey == "为真":
                entry["true"] = cond(sub, svalue)
            elif skey == "反应":
                entry.setdefault("reaction", unescape(svalue))
            elif skey.startswith("反应·"):
                # 某人专属的反应（模板展开到此人时取代通用的那句）。
                who = skey[3:]
                if who not in ctx.person_enum:
                    fail(f"“{skey}”引用了未声明的人物。", sub.number)
                if bind.get("person") == ctx.person_enum[who]:
                    entry["reaction"] = unescape(svalue)
            elif skey == "相关证据":
                refs = split_list(svalue)
                for ref in refs:
                    if ref not in names:
                        fail(f"相关证据“{ref}”未在证据里声明。", sub.number)
                entry["relevant"] = [names.index(ref) for ref in refs]
            elif skey == "冤枉":
                entry["frames"] = svalue == "是"
            else:
                fail(f"主张不支持“{skey}”。", sub.number)
        for need in ("true", "reaction", "relevant", "topic"):
            if need not in entry:
                fail(f"主张“{entry['text']}”缺少“{ {'true': '为真', 'reaction': '反应', 'relevant': '相关证据', 'topic': '问'}[need] }”。", node.number)
        claims.append(entry)
    case["claims"] = claims
    return case
