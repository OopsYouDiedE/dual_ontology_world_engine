"""Compile passage identities, boundaries and message-driven gatherings from author data."""
import re

from story_base import StoryError, split_list


def compile_access(body, ctx, head):
    from story_chapter1 import read_scalar_records
    data = read_scalar_records(body)
    if set(data) != {"boundaries", "routes", "arrivals", "summons"}:
        raise StoryError("通行须声明 boundaries、routes、arrivals、summons。", head.number)
    rooms = {"world:room." + key for key in ctx.dorms.values()}
    people = {"world:person." + key for key in ctx.person_enum.values()}

    def refs(value, allowed):
        values = list(allowed) if value == "all" else split_list(value)
        if not values or len(values) != len(set(values)) or not set(values) <= allowed:
            raise StoryError("通行引用了未知或重复身份：" + str(value), head.number)
        return sorted(values) if value == "all" else values

    for kind in ("boundaries", "routes", "summons"):
        if not isinstance(data[kind], dict):
            raise StoryError("通行声明必须按稳定 ID 分组。", head.number)
        for key in data[kind]:
            if not re.fullmatch(r"[A-Za-z_][A-Za-z_0-9.-]*", key):
                raise StoryError("通行 ID 无效：" + key, head.number)
    for boundary in data["boundaries"].values():
        if set(boundary) != {"kind", "initial", "rooms"} or boundary["kind"] not in {"corridor", "dorm"} or type(boundary["initial"]) is not bool:
            raise StoryError("边界须声明 kind（corridor/dorm）、initial（布尔）、rooms。", head.number)
        boundary["rooms"] = refs(boundary["rooms"], rooms)
    destinations = set()
    for route in data["routes"].values():
        if set(route) != {"name", "to", "from", "visible_from"} or not isinstance(route["name"], str) or not route["name"] or route["to"] not in rooms or route["to"] in destinations:
            raise StoryError("每个目的地须有唯一的通路，声明 to、from、visible_from。", head.number)
        destinations.add(route["to"])
        route["from"] = refs(route["from"], rooms)
        route["visible_from"] = refs(route["visible_from"], rooms)
    if destinations != rooms:
        raise StoryError("通路必须覆盖已声明地点。", head.number)
    if {"world:" + p for p in data["arrivals"]} != people or any(room not in rooms for room in data["arrivals"].values()):
        raise StoryError("arrivals 须声明每个人物的初始地点。", head.number)
    for summon in data["summons"].values():
        if set(summon) != {"sender", "people", "to", "text", "compatibility"} or summon["sender"] not in people or summon["to"] not in rooms or not isinstance(summon["text"], str) or not summon["text"] or type(summon["compatibility"]) is not int:
            raise StoryError("召集须声明 sender、people、to、text、compatibility。", head.number)
        summon["people"] = refs(summon["people"], people)
    return data
