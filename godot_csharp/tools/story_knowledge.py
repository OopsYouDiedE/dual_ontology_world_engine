"""Typed acquisition declarations; never derive beliefs from verdicts or hidden state."""
import re
from story_base import StoryError, split_pair, split_list

BLOCKS = {"认知命题", "认知接线", "推知关系"}
SOURCES = {"亲见": "perception", "证言": "testimony", "回忆": "memory", "候选": "proposal"}
KINDS = {"卡片": "card", "行": "line", "陈述": "statement", "变量": "variable",
         "问题": "question", "选择": "choice", "现场": "field"}
ACTS = {"陈述": "assert", "回想": "recall", "提议": "propose", "询问": "ask"}


def read_knowledge_block(data, head, body):
    data.setdefault("propositions", {})
    data.setdefault("bindings", [])
    data.setdefault("implications", [])
    for line in body:
        if line.indent != head.indent + 1 or not line.text.startswith("- "):
            raise StoryError("认知声明使用单层条目。", line.number)
        if head.text == "认知命题":
            key, label = split_pair(line)
            key = key.removeprefix("- ").strip()
            if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_.-]*", key) or not label or key in data["propositions"]:
                raise StoryError("命题须使用唯一编号与非空文字，不声明真假。", line.number)
            data["propositions"][key] = label
        elif head.text == "推知关系":
            parts = [p.strip() for p in line.text[2:].split("=>")]
            if len(parts) != 2:
                raise StoryError("推知关系写成“- 前提、前提 => 结论”。", line.number)
            data["implications"].append({"premises": split_list(parts[0]), "conclusion": parts[1]})
        else:
            parts = [p.strip() for p in line.text[2:].split("|")]
            if len(parts) != 7 or parts[0] not in KINDS or parts[2] not in SOURCES or parts[5] not in ACTS:
                raise StoryError("认知接线写成“- 类型 | 可见来源编号 | 来源 | 可靠性 | 说话人 | 言语行为 | 命题”。", line.number)
            if not parts[3].isdigit() or not 0 <= int(parts[3]) <= 100 or not parts[1]:
                raise StoryError("接线需要来源编号及 0–100 的可靠性。", line.number)
            source, kind, act = SOURCES[parts[2]], KINDS[parts[0]], ACTS[parts[5]]
            if kind not in {"line", "card"} and source != "proposal":
                raise StoryError("候选只能声明可提出的命题。", line.number)
            if source == "perception" and act != "assert" or source == "memory" and act != "recall":
                raise StoryError("亲见须是陈述，回忆须是回想行为。", line.number)
            if source == "testimony" and (not parts[4] or act != "assert"):
                raise StoryError("证言必须绑定说话人与陈述行为。", line.number)
            data["bindings"].append({"kind": kind, "key": parts[1], "source": source,
                "reliability": int(parts[3]), "speaker": parts[4], "act": act, "propositions": split_list(parts[6])})


def validate_knowledge(data, evidence, statements, variables, line_speakers, choices, sites=()):
    known = set(data["propositions"])
    def refs(items):
        if not items or any(p.removeprefix("!") not in known for p in items):
            raise StoryError("认知声明含空命题或未声明命题。")
    for law in data["implications"]:
        refs(law["premises"]); refs([law["conclusion"]])
    seen = set()
    for b in data["bindings"]:
        refs(b["propositions"])
        pair = b["kind"], b["key"]
        if pair in seen:
            raise StoryError("认知接线重复：" + str(pair))
        seen.add(pair)
        kind, key = pair
        if kind == "card" and key not in evidence or kind == "statement" and key not in statements:
            raise StoryError("认知来源不存在：" + key)
        if kind == "variable":
            v, _, option = key.partition(":")
            if v not in variables or not option.isdigit() or int(option) >= variables[v]:
                raise StoryError("重演取值不存在：" + key)
        if kind in {"line", "question"} and (key not in line_speakers or b["speaker"] != line_speakers[key]):
            raise StoryError("认知行/说话人不匹配：" + key)
        if kind == "choice" and key not in choices:
            raise StoryError("认知选择不存在：" + key)
        if kind == "field" and key not in sites:
            raise StoryError("认知现场不存在：" + key)
