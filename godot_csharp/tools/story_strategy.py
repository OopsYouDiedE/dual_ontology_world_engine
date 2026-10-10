"""Character strategy declarations: only thresholds in the owner's cognition."""
import re
from story_base import StoryError, children, split_pair, split_list


def strategies(field):
    if field is None:
        return []
    value, line, body = field
    if value or not body:
        raise StoryError("策略需要缩进声明，不能是行动队列。", line.number)
    result, names, i = [], set(), 0
    while i < len(body):
        head = body[i]
        if head.indent != 2 or not head.text.startswith("- "):
            raise StoryError("策略须写成“- 名称”并带对象、姿态、优先度、依据、方法。", head.number)
        name = head.text[2:].strip()
        sub, i = children(body, i + 1, 2)
        fields = {}
        for entry in sub:
            key, text = split_pair(entry)
            if entry.indent != 3 or key in fields or key not in {"对象", "姿态", "优先度", "依据", "方法", "条件"}:
                raise StoryError("未知、重复或越界的策略字段。", entry.number)
            fields[key] = text
        if not name or name in names or not {"对象", "姿态", "优先度", "依据", "方法"} <= fields.keys():
            raise StoryError("策略字段不全或名称重复。", head.number)
        if any(not fields[key].strip() for key in {"对象", "姿态", "依据", "方法"}):
            raise StoryError("策略对象、姿态、依据、方法不能为空。", head.number)
        if len(split_list(fields["方法"])) != len(set(split_list(fields["方法"]))):
            raise StoryError("策略方法不能重复。", head.number)
        if not re.fullmatch(r"\d+", fields["优先度"]) or not 0 <= int(fields["优先度"]) <= 1000:
            raise StoryError("策略优先度须在0–1000之间。", head.number)
        conditions = []
        for item in split_list(fields.get("条件", "")):
            match = re.fullmatch(r"本人 ([A-Za-z_][A-Za-z_0-9]*) >= (\d+)", item)
            if not match:
                raise StoryError("策略条件只接受“本人 状态 >= 非负阈值”，不读玩家意图或全局真值。", head.number)
            conditions.append({"tag": match[1], "minimum": int(match[2])})
        names.add(name)
        result.append({"name": name, "target": fields["对象"], "stance": fields["姿态"],
                       "priority": int(fields["优先度"]), "source": fields["依据"],
                       "methods": split_list(fields["方法"]), "conditions": conditions})
    if not 1 <= len(result) <= 3:
        raise StoryError("每个人物只能声明1–3条策略。", line.number)
    return result
