"""Strict subjective controller declarations; no routes, IDs or source locations."""
import re
from story_base import StoryError

KINDS = {"菜单": "menu", "表单选项": "form_field", "表单提交": "form_submit",
         "继续": "continue", "检视": "interact", "走位": "move", "等待": "wait",
         "新选择": "untried", "未看过": "new"}
MODES = {"偏好": "prefer", "要求": "require", "禁止": "forbid", "放行": "permit"}


def controller(fields, actor, name):
    declaration = fields.get("操控者")
    rules = fields.get("操控规则")
    reasoning = fields.get("推理预算")
    if declaration is None:
        if rules is not None or reasoning is not None:
            raise StoryError("操控规则/推理预算需要显式声明操控者。", (rules or reasoning)[1].number)
        return None
    mode, line, body = declaration
    if body or mode not in {"玩家", "代理"}:
        raise StoryError("操控者只能是玩家或代理，不带缩进正文。", line.number)
    compiled = []
    if rules is not None:
        value, rule_line, entries = rules
        if value or not entries:
            raise StoryError("操控规则须为缩进的主观规则列表。", rule_line.number)
        if len(entries) > 64:
            raise StoryError("操控规则最多 64 条。", rule_line.number)
        for entry in entries:
            match = re.fullmatch(r'- (偏好|要求|禁止|放行) (\S+) (\d+)(?: 文案 "([^"\r\n]+)")?', entry.text)
            if entry.indent != 2 or not match or match[2] not in KINDS:
                raise StoryError("操控规则须写成“- 偏好/要求/禁止/放行 输入类别 权重”；类别：" +
                                 "、".join(KINDS), entry.number)
            weight = int(match[3])
            if not 0 <= weight <= 1000:
                raise StoryError("操控规则权重须在 0–1000 之间。", entry.number)
            compiled.append({"mode": MODES[match[1]], "kind": KINDS[match[2]], "weight": weight})
            if match[4] is not None:
                if len(match[4]) > 200:
                    raise StoryError("公开候选文案筛选最多 200 字。", entry.number)
                compiled[-1]["label_contains"] = match[4]
    if mode == "代理" and not compiled:
        raise StoryError("代理必须声明本人的操控规则，不能默认编造策略。", line.number)
    budget = None
    if reasoning is not None:
        value, budget_line, body = reasoning
        match = re.fullmatch(r"深度 (\d+) 展开 (\d+) 假设 (\d+) 论证 (\d+) 求知 (\d+)", value)
        if body or not match:
            raise StoryError("推理预算须为“深度 N 展开 N 假设 N 论证 N 求知 N”。", budget_line.number)
        depth, expansions, hypotheses, argument, information = map(int, match.groups())
        if not (0 <= depth <= 64 and 1 <= expansions <= 100000 and 1 <= hypotheses <= 4096 and 0 <= argument <= 1000 and 0 <= information <= 1000):
            raise StoryError("推理预算超限：深度0–64、展开1–100000、假设1–4096、权重0–1000。", budget_line.number)
        budget = {"depth": depth, "expansions": expansions, "hypotheses": hypotheses, "argument_weight": argument, "information_weight": information}
    # Player declarations are validated but compile to the existing default.
    # Switching to an agent is an explicit authored change, never a CLI route override.
    if mode == "玩家":
        return None
    result = {"actor": actor, "name": name, "mode": "agent", "rules": compiled}
    if budget is not None:
        result["reasoning"] = budget
    return result
