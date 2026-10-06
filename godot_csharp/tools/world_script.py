#!/usr/bin/env python3
"""Compile safe Python-like chapter scripts (including fenced dialogue) to typed JSON IR.

Files are parsed with ast.parse after dialogue fence preprocessing and never imported/executed.
"""
from __future__ import annotations

import argparse
import ast
import json
import math
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

FORMAT = "dual-ontology-world-script/v1"
INT32_MIN = -(2**31)
INT32_MAX = 2**31 - 1
ENTITY_KINDS = {"room", "object", "character"}
ENTITY_SCOPES = {"world", "chapter"}
DECLARATION_ARGS = {"id", "name", "aliases", "scope", "room", "space", "properties", "state", "belief", "observable", "sprite"}
BEHAVIOR_NAMES = {"option": "option", "选项": "option", "reaction": "reaction", "反应": "reaction"}
SEQUENCE_NAMES = {"sequence": "sequence", "场面": "sequence"}
ACTION_NAMES = {
    "say": "say", "台词": "say", "dialogue": "say",
    "set": "set", "设定": "set", "change": "change", "改变": "change",
    "spend": "spend", "支付": "spend", "inspect": "inspect", "检查": "inspect",
    "take": "take", "拿取": "take", "destroy": "destroy", "销毁": "destroy",
    "place": "place", "放置": "place", "travel": "travel", "前往": "travel",
    "unlock": "unlock", "解锁": "unlock",
}
AUTHORITY_ACTIONS = {"lesson_answer": ["boolean"], "mediate": []}
REACTION_INTENTS = {"testimony", "decline"}
# A line ID tag may precede or follow the emotion tag at the end of a dialogue line.
LINE_ID_TAG = re.compile(r"\s*\[#id=([A-Za-z0-9_][A-Za-z0-9_.\-]*)\](?=\s*(?:\[#emotion=[a-z_]+\])?\s*$)")
COGNITIVE_QUERY_TAGS = {"Trust", "Cooperation", "Sick", "Hungry", "Fed", "Weak"}
PROFILE_FIELDS = {
    "group", "dorm", "speed", "cooperation", "build", "desire", "demon_line", "desire_flags",
    "strength", "lock", "signal", "structure", "greenhouse_skill", "cook", "craftsman", "values",
    "prejudice", "demon_prejudice", "habits", "voice", "gender",
}


@dataclass(frozen=True)
class Source:
    path: str
    line: int
    column: int
    end_line: int
    end_column: int

    def json(self) -> dict[str, Any]:
        return {"path": self.path, "line": self.line, "column": self.column,
                "end_line": self.end_line, "end_column": self.end_column}


@dataclass
class Entity:
    id: str
    name: str
    kind: str
    scope: str
    properties: dict[str, dict[str, Any]]
    declared_properties: dict[str, dict[str, Any]]
    aliases: list[str]
    source: Source
    chapter_id: str
    profile: dict[str, Any] | None = None

    def json(self) -> dict[str, Any]:
        result = {"id": self.id, "name": self.name, "kind": self.kind, "scope": self.scope,
                "properties": self.declared_properties, "aliases": self.aliases,
                "source_id": source_id(self.source)}
        if self.profile is not None:
            result["profile"] = self.profile
        return result

    def runtime_id(self) -> str:
        return f"world:{self.id}" if self.scope == "world" else f"chapter:{self.chapter_id}:{self.id}"


@dataclass
class Chapter:
    id: str
    requires: list[str]
    source: Source
    entities: list[Entity] = field(default_factory=list)
    patches: list[dict[str, Any]] = field(default_factory=list)
    candidates: list[dict[str, Any]] = field(default_factory=list)
    sequences: list[dict[str, Any]] = field(default_factory=list)
    sites: list[dict[str, Any]] = field(default_factory=list)
    furniture: list[dict[str, Any]] = field(default_factory=list)
    ui_text: dict[str, Any] | None = None
    requests: list[dict[str, str]] | None = None

    def json(self) -> dict[str, Any]:
        return {"id": self.id, "requires": self.requires,
                "entities": [entity.json() for entity in self.entities],
                "patches": self.patches, "candidates": self.candidates, "sequences": self.sequences,
                "sites": self.sites,
                "furniture": self.furniture,
                **({"ui_text": self.ui_text} if self.ui_text is not None else {}),
                **({"requests": self.requests} if self.requests is not None else {}),
                "source_id": source_id(self.source)}


class ScriptError(Exception):
    def __init__(self, message: str, source: Source | None = None):
        super().__init__(message)
        self.source = source


def source_id(source: Source) -> str:
    return f"{source.path}:{source.line}:{source.column}"


def infer(value: Any) -> dict[str, Any]:
    if value is None:
        kind = "null"
    elif isinstance(value, bool):
        kind = "boolean"
    elif isinstance(value, int):
        kind = "integer"
    elif isinstance(value, float):
        if not math.isfinite(value):
            raise ValueError("非有限数值不能进入世界属性。")
        kind = "number"
    elif isinstance(value, str):
        kind = "string"
    elif isinstance(value, list):
        kind = "array"
    elif isinstance(value, dict):
        kind = "object"
    else:
        raise ValueError(f"unsupported property value type {type(value).__name__}")
    return {"type": kind, "value": value}


class Compiler:
    def __init__(self, paths: list[Path], external_chapters: set[str] | None = None, texts: dict[str, str] | None = None):
        self.paths = paths
        self.texts = texts or {}
        self.external_chapters = external_chapters or set()
        self.chapters: list[Chapter] = []
        self.chapter_ids: dict[str, int] = {}
        self.entities: dict[str, Entity] = {}
        self.entity_aliases: dict[str, str] = {}
        self.aliases: dict[str, str] = {}
        self.source_map: dict[str, dict[str, Any]] = {}
        self.behavior_ids: set[str] = set()
        self.candidate_kinds: dict[str, str] = {}
        self.candidate_labels: dict[str, str] = {}
        self.sequence_ids: set[str] = set()
        self.current: Chapter | None = None
        self.current_path = ""
        self.source_lines: list[str] = []
        self.dialogue_blocks: dict[str, str] = {}
        self.dialogue_block_start_lines: dict[str, int] = {}

    def location(self, node: ast.AST) -> Source:
        line = getattr(node, "lineno", 1)
        end_line = getattr(node, "end_lineno", line)
        column = self.character_column(line, getattr(node, "col_offset", 0))
        end_column = self.character_column(end_line, getattr(node, "end_col_offset", getattr(node, "col_offset", 0)))
        return Source(self.current_path, line, column, end_line, end_column)

    def character_column(self, line: int, byte_column: int) -> int:
        if not 1 <= line <= len(self.source_lines):
            return byte_column + 1
        prefix = self.source_lines[line - 1].encode("utf-8")[:byte_column]
        return len(prefix.decode("utf-8", errors="ignore")) + 1

    def fail(self, node: ast.AST, message: str) -> None:
        raise ScriptError(message, self.location(node))

    def record_source(self, source: Source) -> str:
        key = source_id(source)
        self.source_map[key] = source.json()
        return key

    def parse(self) -> dict[str, Any]:
        for path_index, path in enumerate(self.paths):
            self.current_path = path.as_posix()
            try:
                raw = self.texts[self.current_path] if self.current_path in self.texts else path.read_text(encoding="utf-8-sig")
            except OSError as exc:
                raise ScriptError(f"无法读取脚本：{exc}") from exc
            except UnicodeError as exc:
                raise ScriptError(f"脚本不是有效的 UTF-8 文本：{exc}") from exc
            prepared = self.preprocess_dialogue_blocks(raw)
            try:
                tree = ast.parse(prepared, filename=self.current_path, mode="exec", type_comments=False)
            except SyntaxError as exc:
                source = Source(self.current_path, exc.lineno or 1, exc.offset or 1,
                                exc.end_lineno or exc.lineno or 1, exc.end_offset or exc.offset or 1)
                raise ScriptError(f"Python 语法错误：{exc.msg}", source) from exc
            self.source_lines = raw.splitlines()
            self.aliases = {}
            chapter_call: ast.Call | None = None
            for index, statement in enumerate(tree.body):
                if index == 0 and isinstance(statement, ast.Expr) and isinstance(statement.value, ast.Constant) and isinstance(statement.value.value, str):
                    continue
                if self.current is None:
                    if chapter_call is None and self.is_call_statement(statement, "chapter"):
                        chapter_call = statement.value
                        self.begin_chapter(chapter_call, path_index)
                        continue
                    self.fail(statement, "每个脚本文件必须先声明 chapter(id=..., requires=...)。")
                self.compile_top(statement)
            if self.current is None:
                raise ScriptError("脚本没有 chapter 声明。", Source(self.current_path, 1, 1, 1, 1))
            self.current = None
        self.validate_dependencies()
        return self.document()

    def preprocess_dialogue_blocks(self, raw: str) -> str:
        """Replace code-state fences with sentinels, preserving the raw block and line count."""
        self.dialogue_blocks = {}
        self.dialogue_block_start_lines = {}
        spans: list[tuple[int, int]] = []
        i = 0
        quote: str | None = None
        triple = False
        escaped = False
        comment = False
        while i < len(raw):
            char = raw[i]
            if comment:
                if char in "\r\n": comment = False
                i += 1
                continue
            if quote is not None:
                marker = quote * (3 if triple else 1)
                if escaped:
                    escaped = False
                    i += 1
                    continue
                if char == "\\":
                    escaped = True
                    i += 1
                    continue
                if raw.startswith(marker, i):
                    quote = None
                    triple = False
                    i += len(marker)
                    continue
                i += 1
                continue
            if char == "#":
                comment = True
                i += 1
                continue
            if char in "'\"":
                quote = char
                triple = raw.startswith(char * 3, i)
                i += 3 if triple else 1
                continue
            if raw.startswith("```", i):
                start = i
                end = raw.find("```", i + 3)
                if end < 0:
                    line = raw.count("\n", 0, start) + 1
                    col = start - raw.rfind("\n", 0, start)
                    raise ScriptError("三反引号台词块缺少结束符。", Source(self.current_path, line, col, line, col + 3))
                spans.append((start, end + 3))
                i = end + 3
                continue
            i += 1

        pieces: list[str] = []
        cursor = 0
        for index, (start, end) in enumerate(spans):
            pieces.append(raw[cursor:start])
            block = raw[start + 3:end - 3]
            sentinel = f"__WORLD_DIALOGUE_BLOCK_{index}__"
            while sentinel in raw:
                sentinel += "_"
            self.dialogue_blocks[sentinel] = block
            self.dialogue_block_start_lines[sentinel] = raw.count("\n", 0, start + 3) + 1
            pieces.append(json.dumps(sentinel, ensure_ascii=False))
            pieces.append("\n" * raw[start:end].count("\n"))
            cursor = end
        pieces.append(raw[cursor:])
        return "".join(pieces)

    def is_call_statement(self, node: ast.stmt, name: str) -> bool:
        return isinstance(node, ast.Expr) and isinstance(node.value, ast.Call) and self.call_name(node.value.func) == name

    @staticmethod
    def call_name(node: ast.expr) -> str | None:
        return node.id if isinstance(node, ast.Name) else None

    def begin_chapter(self, call: ast.Call, path_index: int) -> None:
        values = self.arguments(call, {"id", "requires"}, required={"id"})
        chapter_id = self.literal_string(values["id"], call)
        self.validate_id(chapter_id, call, "章节")
        if chapter_id in self.chapter_ids:
            self.fail(call, f"章节 ID {chapter_id!r} 重复；已在第 {self.chapter_ids[chapter_id] + 1} 个输入文件声明。")
        requires_node = values.get("requires")
        raw_requires = [] if requires_node is None else self.static_value(requires_node)
        if not isinstance(raw_requires, list) or any(not isinstance(item, str) for item in raw_requires):
            self.fail(requires_node or call, "requires 必须是字符串章节 ID 列表。")
        if len(raw_requires) != len(set(raw_requires)):
            self.fail(requires_node or call, "requires 中章节 ID 不能重复。")
        for required in raw_requires:
            self.validate_id(required, requires_node or call, "依赖章节")
        chapter = Chapter(chapter_id, raw_requires, self.location(call))
        self.chapter_ids[chapter_id] = path_index
        self.chapters.append(chapter)
        self.current = chapter
        self.record_source(chapter.source)

    def compile_top(self, node: ast.stmt) -> None:
        assert self.current is not None
        if isinstance(node, ast.Assign) and len(node.targets) == 1 and isinstance(node.targets[0], ast.Name):
            if not isinstance(node.value, ast.Call):
                self.fail(node, "顶层赋值只允许 room/object/character/ref 声明。")
            function = self.call_name(node.value.func)
            if function == "ref":
                values = self.arguments(node.value, {"id"}, required={"id"})
                entity_id = self.literal_string(values["id"], node.value)
                self.resolve_entity(entity_id, node.value)
                if node.targets[0].id in self.aliases:
                    self.fail(node, f"本文件变量别名 {node.targets[0].id!r} 重复。")
                self.aliases[node.targets[0].id] = entity_id
                return
            if function in ENTITY_KINDS:
                self.declare_entity(function, node.targets[0].id, node.value)
                return
            self.fail(node.value, f"不支持顶层声明 {function!r}；允许 room/object/character/ref。")
        if isinstance(node, ast.Expr) and isinstance(node.value, ast.Call):
            name = self.call_name(node.value.func)
            if name in {"site", "热点"}:
                self.compile_site(node.value)
                return
            if name in {"furniture", "家具"}:
                self.compile_furniture(node.value)
                return
            if name == "ui_text":
                self.compile_ui_text(node.value)
                return
            if name == "requests":
                self.compile_requests(node.value)
                return
            if name == "extend":
                self.compile_extend(node.value)
                return
            if name == "patch":
                self.fail(node.value, "运行时不支持声明式 patch；请使用候选里的 set/change 动作。")
            self.fail(node.value, f"不支持顶层调用 {name!r}。")
        if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            if node.decorator_list and isinstance(node.decorator_list[0], ast.Call) and self.call_name(node.decorator_list[0].func) in SEQUENCE_NAMES:
                self.compile_sequence(node)
            else:
                self.compile_behavior(node)
            return
        self.fail(node, f"不支持顶层语法 {type(node).__name__}。")

    def compile_site(self, call: ast.Call) -> None:
        values = self.arguments(call, {"id", "map", "room", "title", "kind", "thing", "lore"},
                                required={"id", "map", "title"})
        site_id = self.literal_string(values["id"], call)
        map_id = self.literal_string(values["map"], call)
        title = self.literal_string(values["title"], call)
        self.validate_id(site_id, call, "热点")
        self.validate_id(map_id, call, "地图")
        if any(site["id"] == site_id and site["map"] == map_id for site in self.current.sites):
            self.fail(call, f"地图 {map_id!r} 的热点 ID {site_id!r} 重复。")
        room = self.entity_ref(values["room"], "room").runtime_id() if "room" in values else None
        thing = self.entity_ref(values["thing"], "object").runtime_id() if "thing" in values else None
        kind = self.static_value(values["kind"]) if "kind" in values else None
        lore = self.static_value(values["lore"]) if "lore" in values else None
        if kind is not None and (not isinstance(kind, str) or not kind):
            self.fail(values["kind"], "热点 kind 必须是非空字符串。")
        if lore is not None and not isinstance(lore, str):
            self.fail(values["lore"], "热点 lore 必须是字符串。")
        site = {"id": site_id, "map": map_id, "title": title, "source_id": self.record_source(self.location(call))}
        if room is not None: site["room"] = room
        if thing is not None: site["thing"] = thing
        if kind is not None: site["kind"] = kind
        if lore is not None: site["lore"] = lore
        self.current.sites.append(site)

    def compile_furniture(self, call: ast.Call) -> None:
        values = self.arguments(call, {"key", "text"}, required={"key", "text"})
        furniture_id = self.literal_string(values["key"], call)
        text = self.literal_string(values["text"], call)
        self.validate_id(furniture_id, call, "家具原型")
        if any(item["key"] == furniture_id for item in self.current.furniture):
            self.fail(call, f"家具原型 {furniture_id!r} 重复。")
        self.current.furniture.append({"key": furniture_id, "text": text,
                                       "source_id": self.record_source(self.location(call))})

    def compile_ui_text(self, call: ast.Call) -> None:
        if call.args or any(keyword.arg is None for keyword in call.keywords):
            self.fail(call, "ui_text 只接受逐项命名的静态文案参数。")
        if self.current.ui_text is not None:
            self.fail(call, "每章只能声明一次 ui_text。")
        values: dict[str, Any] = {}
        for keyword in call.keywords:
            if keyword.arg in values:
                self.fail(keyword.value, f"ui_text 键 {keyword.arg!r} 重复。")
            value = self.static_value(keyword.value)
            if keyword.arg in {"finding_titles", "confront_questions"}:
                if (not isinstance(value, list) or len(value) != (5 if keyword.arg == "finding_titles" else 4)
                        or any(not isinstance(item, str) or not item.strip() for item in value)):
                    self.fail(keyword.value, f"ui_text.{keyword.arg} 必须是含指定数量非空字符串的列表。")
            elif not isinstance(value, str) or not value.strip():
                self.fail(keyword.value, f"ui_text.{keyword.arg} 必须是非空字符串。")
            values[keyword.arg] = value
        required = {"replay_title", "replay_hint", "finding_titles", "confront_questions",
                    "hungry", "bed_early", "watch_early", "meal_closed"}
        if values.keys() != required:
            missing = sorted(required - values.keys())
            extra = sorted(values.keys() - required)
            self.fail(call, "ui_text 键不符合章节宿主契约。缺少：" + ", ".join(missing) + "；未知：" + ", ".join(extra))
        self.current.ui_text = values

    def compile_requests(self, call: ast.Call) -> None:
        if len(call.args) != 1 or call.keywords:
            self.fail(call, "requests 只接受一个静态请求列表。")
        if self.current.requests is not None:
            self.fail(call, "每章只能声明一次 requests。")
        entries = self.static_value(call.args[0])
        jobs = {"Sleep", "Rest", "Class", "Collect", "Distribute", "Cook", "FetchWater", "Care",
                "NightTakeSick", "NightTakeSickRec", "NightTakeSelf", "NightTakeSelfRec", "RepairLatch",
                "InspectStructure", "Patrol", "GuardArchive", "VisitArchive", "Greenhouse", "Socialize",
                "Work", "CountLedger", "Stele", "Scavenge", "Hoard", "Accuse", "PostRules", "Defend",
                "AskLatchHelp", "Seize", "AnswerName", "Escort", "Inform", "FeedWeak", "GiveKey", "Tidy",
                "Wander", "Share", "Tear", "Watch", "AskOpen", "Relay", "Teach", "Exchange", "Follow",
                "Avoid", "AskLeave", "Treat", "Retreat", "Report", "Sample", "Needle", "NightSketch", "Sketch"}
        if not isinstance(entries, list) or not entries:
            self.fail(call.args[0], "requests 必须是非空静态列表。")
        compiled: list[dict[str, str]] = []
        seen: set[str] = set()
        for entry in entries:
            if (not isinstance(entry, dict) or entry.keys() != {"label", "job"}
                    or not isinstance(entry["label"], str) or not entry["label"].strip()
                    or entry["job"] not in jobs or entry["job"] in seen):
                self.fail(call.args[0], "每条 request 需有唯一的 C# Job 名与非空 label。")
            seen.add(entry["job"])
            compiled.append({"label": entry["label"], "job": entry["job"]})
        self.current.requests = compiled

    def arguments(self, call: ast.Call, allowed: set[str], required: set[str] = frozenset()) -> dict[str, ast.expr]:
        if call.args:
            self.fail(call, "请使用命名参数，例如 id=..., room=...。")
        values: dict[str, ast.expr] = {}
        for keyword in call.keywords:
            if keyword.arg is None:
                self.fail(keyword.value, "不支持 **kwargs；声明参数必须逐项命名。")
            if keyword.arg not in allowed:
                self.fail(keyword.value, f"未知参数 {keyword.arg!r}。")
            if keyword.arg in values:
                self.fail(keyword.value, f"参数 {keyword.arg!r} 重复。")
            values[keyword.arg] = keyword.value
        missing = required - values.keys()
        if missing:
            self.fail(call, "缺少必填命名参数：" + "、".join(sorted(missing)))
        return values

    def literal_string(self, node: ast.AST, context: ast.AST) -> str:
        value = self.static_value(node)
        if isinstance(value, str):
            value = self.dialogue_blocks.get(value, value)
        if not isinstance(value, str) or not value:
            self.fail(context, "此处需要非空字符串。")
        return value

    def require_int32(self, value: Any, node: ast.AST, subject: str) -> int:
        if not isinstance(value, int) or isinstance(value, bool):
            self.fail(node, f"{subject} 必须是整数。")
        if value < INT32_MIN or value > INT32_MAX:
            self.fail(node, f"{subject} 超出 Core Int32 范围 [{INT32_MIN}, {INT32_MAX}]。")
        return value

    def validate_id(self, value: str, node: ast.AST, subject: str) -> None:
        if any(char.isspace() for char in value) or ":" in value:
            self.fail(node, f"{subject} ID 不能包含空白或冒号。")

    def compile_profile(self, node: ast.AST) -> dict[str, Any]:
        profile = self.static_value(node)
        if not isinstance(profile, dict):
            self.fail(node, "profile 必须是静态字典。")
        unknown = profile.keys() - PROFILE_FIELDS
        missing = PROFILE_FIELDS - profile.keys()
        if unknown or missing:
            self.fail(node, "profile 字段必须完整且仅包含既定角色画像字段；缺少：" +
                      ("、".join(sorted(missing)) or "无") + "；未知：" + ("、".join(sorted(unknown)) or "无"))
        string_fields = {"group", "dorm", "build", "desire", "demon_line", "voice", "gender"}
        for key in string_fields:
            if not isinstance(profile[key], str): self.fail(node, f"profile.{key} 必须是字符串。")
        if profile["gender"] not in {"Male", "Female"}:
            self.fail(node, "profile.gender 必须是 Male 或 Female。")
        if profile["group"] not in {"Life", "Wenqi", "Xingyi", "Houqin"}:
            self.fail(node, "profile.group 必须是 Group 稳定枚举名称。")
        if profile["build"] not in {"Small", "Medium", "Large"}:
            self.fail(node, "profile.build 必须是 Build 稳定枚举名称。")
        if profile["dorm"] not in {"DormLife", "DormW", "DormS", "DormL"}:
            self.fail(node, "profile.dorm 必须是 Place 稳定枚举名。")
        if not isinstance(profile["speed"], (int, float)) or isinstance(profile["speed"], bool) or not math.isfinite(profile["speed"]) or profile["speed"] <= 0:
            self.fail(node, "profile.speed 必须是有限正数。")
        for key in {"cooperation", "strength", "lock", "signal", "structure", "greenhouse_skill", "cook", "demon_prejudice"}:
            self.require_int32(profile[key], node, f"profile.{key}")
        if type(profile["craftsman"]) is not bool:
            self.fail(node, "profile.craftsman 必须是布尔值。")
        rf_names = {"Records", "SickPriority", "PerHead", "Restricts", "TargetsQuer", "CentralCook",
                    "EvidenceFirst", "OpenArchive", "CommitteeArchive", "SealArchive", "PactArchive",
                    "NightException", "TwoWitness", "WardAuthority", "Tradition", "Relay", "Safety",
                    "NoException", "ForbidNightOpen", "ForbidsDesire", "Rations", "Targets"}
        if not isinstance(profile["desire_flags"], list) or any(v not in rf_names for v in profile["desire_flags"]):
            self.fail(node, "profile.desire_flags 必须是 RF 稳定名称列表。")
        if not isinstance(profile["values"], dict) or any(k not in rf_names for k in profile["values"]):
            self.fail(node, "profile.values 必须按 RF 稳定名称映射整数倾向。")
        for value in profile["values"].values(): self.require_int32(value, node, "profile.values 值")
        person_names = {"Det", "Huo", "Shen", "Su", "Lucian", "Gu", "Quer", "Ward", "Selma", "Ruan", "Elena", "Moran", "Veronica", "Bertrand", "Margaret", "Mora"}
        if not isinstance(profile["prejudice"], dict) or profile["prejudice"].keys() - person_names:
            self.fail(node, "profile.prejudice 必须按 C# P 枚举名映射整数倾向。")
        for value in profile["prejudice"].values(): self.require_int32(value, node, "profile.prejudice 值")
        if not isinstance(profile["habits"], list): self.fail(node, "profile.habits 必须是习惯字典列表。")
        slots = {"Morning", "Forenoon", "Noon", "Afternoon", "Evening", "Night"}
        jobs = {"Sleep", "Rest", "Class", "Collect", "Distribute", "Cook", "FetchWater", "Care", "NightTakeSick", "NightTakeSickRec", "NightTakeSelf", "NightTakeSelfRec", "RepairLatch", "InspectStructure", "Patrol", "GuardArchive", "VisitArchive", "Greenhouse", "Socialize", "Work", "CountLedger", "Stele", "Scavenge", "Hoard", "Accuse", "PostRules", "Defend", "AskLatchHelp", "Seize", "AnswerName", "Escort", "Inform", "FeedWeak", "GiveKey", "Tidy", "Wander", "Share", "Tear", "Watch", "AskOpen", "Relay", "Teach", "Exchange", "Follow", "Avoid", "AskLeave", "Treat", "Retreat", "Report", "Sample", "Needle", "NightSketch", "Sketch"}
        for habit in profile["habits"]:
            if not isinstance(habit, dict) or habit.keys() - {"job", "slots", "weight", "desire", "note"} or not {"job", "slots", "weight"} <= habit.keys():
                self.fail(node, "每条习惯需包含 job/slots/weight，可选 desire/note。")
            if habit["job"] not in jobs or not isinstance(habit["slots"], list) or any(slot not in slots for slot in habit["slots"]):
                self.fail(node, "习惯 job/slots 必须使用 C# Job/Slot 稳定枚举名称。")
            self.require_int32(habit["weight"], node, "profile.habits.weight")
            if "desire" in habit and type(habit["desire"]) is not bool: self.fail(node, "profile.habits.desire 必须是布尔值。")
            if "note" in habit and not isinstance(habit["note"], str): self.fail(node, "profile.habits.note 必须是字符串。")
        return profile

    def static_value(self, node: ast.AST) -> Any:
        if isinstance(node, ast.Constant) and (node.value is None or isinstance(node.value, (str, int, float, bool))):
            return node.value
        if isinstance(node, (ast.List, ast.Tuple)):
            return [self.static_value(value) for value in node.elts]
        if isinstance(node, ast.Dict):
            values: dict[str, Any] = {}
            for key, value in zip(node.keys, node.values):
                if key is None:
                    self.fail(value, "不支持字典解包。")
                name = self.static_value(key)
                if not isinstance(name, str):
                    self.fail(key, "声明字典的键必须是字符串。")
                if name in values:
                    self.fail(key, f"字典键 {name!r} 重复。")
                values[name] = self.static_value(value)
            return values
        if isinstance(node, ast.UnaryOp) and isinstance(node.op, (ast.USub, ast.UAdd)) and isinstance(node.operand, ast.Constant):
            value = node.operand.value
            if isinstance(value, (int, float)) and not isinstance(value, bool):
                return -value if isinstance(node.op, ast.USub) else value
        self.fail(node, "此处只允许字面量、列表、元组或字典；不执行 Python 表达式。")

    def entity_ref(self, node: ast.AST, expected_kind: str | None = None) -> Entity:
        if isinstance(node, ast.Name) and (node.id in self.aliases or node.id in self.entity_aliases):
            entity_id = self.aliases.get(node.id, self.entity_aliases.get(node.id, ""))
        elif isinstance(node, ast.Call) and self.call_name(node.func) == "ref":
            values = self.arguments(node, {"id"}, required={"id"})
            entity_id = self.literal_string(values["id"], node)
        elif isinstance(node, ast.Constant) and isinstance(node.value, str):
            entity_id = self.entity_aliases.get(node.value, node.value)
        else:
            self.fail(node, "实体引用请使用本文件别名、稳定 ID 字符串或 ref(id=...)。")
        entity = self.resolve_entity(entity_id, node)
        if expected_kind is not None and entity.kind != expected_kind:
            self.fail(node, f"实体 {entity_id!r} 类型是 {entity.kind}，此处要求 {expected_kind}。")
        return entity

    def resolve_entity(self, entity_id: str, node: ast.AST) -> Entity:
        entity_id = self.entity_aliases.get(entity_id, entity_id)
        entity = self.entities.get(entity_id)
        if entity is None:
            self.fail(node, f"实体 {entity_id!r} 尚未在本章或前置章节声明。")
        return entity

    def typed_property(self, key: str, value: Any, node: ast.AST) -> dict[str, Any]:
        try:
            item = infer(value)
        except ValueError as exc:
            self.fail(node, str(exc))
        if key.startswith(("state:", "belief:")) and item["type"] != "integer":
            self.fail(node, f"属性 {key!r} 必须是整数。")
        if key.startswith(("state:", "belief:")):
            self.require_int32(item["value"], node, f"属性 {key}")
        if key.startswith("observable:") and item["type"] != "boolean":
            self.fail(node, f"属性 {key!r} 必须是布尔值。")
        if key == "sprite" and item["type"] != "string":
            self.fail(node, "属性 'sprite' 必须是字符串。")
        if key == "space" and item["type"] != "entity":
            self.fail(node, "属性 'space' 必须引用房间实体。")
        if key.startswith(("state:", "observable:")):
            tag = key.split(":", 1)[1]
            if not tag or any(char.isspace() for char in tag) or tag.startswith(":") or tag.endswith(":") or "::" in tag:
                self.fail(node, "状态标签必须是非空且不含空白的名称。")
        if key.startswith("belief:"):
            tag = key.split(":", 1)[1]
            if not tag or any(char.isspace() for char in tag) or tag.startswith(":") or tag.endswith(":") or "::" in tag:
                self.fail(node, "belief 标签必须是非空且不含空白的名称。")
        if key in {"state:", "observable:"} or (key.startswith(("state:", "observable:")) and not key.split(":", 1)[1]):
            self.fail(node, "状态标签必须是非空字符串。")
        if not (key == "space" or key == "sprite" or key.startswith(("state:", "belief:", "observable:"))):
            self.fail(node, f"不支持实体属性 {key!r}；允许 space、sprite、state:<tag>、belief:<tag>、observable:<tag>。")
        return item

    def declare_entity(self, kind: str, alias: str, call: ast.Call) -> None:
        values = self.arguments(call, DECLARATION_ARGS | {"profile"}, required={"id", "name"})
        entity_id = self.literal_string(values["id"], call)
        self.validate_id(entity_id, call, "实体")
        name = self.literal_string(values["name"], call)
        if entity_id in self.entities:
            previous = self.entities[entity_id]
            self.fail(call, f"实体 ID {entity_id!r} 已声明；请用 ref(id=...) 引用它（原声明 {previous.source.path}:{previous.source.line}）。")
        alias_owner = self.entity_aliases.get(entity_id)
        if alias_owner is not None:
            self.fail(call, f"实体 ID {entity_id!r} 与实体 {alias_owner!r} 的中文名称或别名冲突。")
        name_owner = self.entity_aliases.get(name)
        if name_owner is not None and name_owner != entity_id:
            self.fail(call, f"显示名称 {name!r} 已用于实体 {name_owner!r}。")
        if alias in self.aliases:
            self.fail(call, f"本文件变量别名 {alias!r} 重复。")
        if alias in {"chapter", "room", "object", "character", "ref", "extend", "patch",
                     "option", "选项", "reaction", "反应", "say", "台词", "line", "dialogue",
                     "set", "change", "spend", "inspect", "take", "destroy", "place", "travel", "unlock",
                     "known", "state", "at"}:
            self.fail(call, f"实体变量别名 {alias!r} 与内置脚本名称冲突。")
        raw_aliases = self.static_value(values["aliases"]) if "aliases" in values else []
        if not isinstance(raw_aliases, list) or any(not isinstance(item, str) or not item for item in raw_aliases):
            self.fail(values.get("aliases", call), "aliases 必须是非空字符串名称列表。")
        raw_aliases = [name, *raw_aliases]
        if len(raw_aliases) != len(set(raw_aliases)):
            self.fail(call, "实体中文名称或别名不能重复。")
        for display_alias in raw_aliases:
            owner = self.entity_aliases.get(display_alias)
            if owner is not None and owner != entity_id:
                self.fail(call, f"实体别名 {display_alias!r} 已用于 {owner!r}。")
            if display_alias in self.entities and display_alias != entity_id:
                self.fail(call, f"实体别名 {display_alias!r} 与已声明实体 ID 冲突。")
        scope = self.static_value(values.get("scope", ast.Constant(value="chapter")))
        if not isinstance(scope, str) or scope not in ENTITY_SCOPES:
            self.fail(values.get("scope", call), "scope 只能是 'world' 或 'chapter'。")
        properties: dict[str, dict[str, Any]] = {}
        if "room" in values and "space" in values:
            self.fail(call, "room 与 space 不能同时提供。")
        room_node = values.get("room", values.get("space"))
        if kind in {"object", "character"}:
            if room_node is None:
                self.fail(call, f"{kind} 声明必须指定 room=房间引用。")
            room = self.entity_ref(room_node, "room")
            properties["space"] = {"type": "entity", "value": room.runtime_id()}
        elif room_node is not None:
            self.fail(room_node, "room 实体不能再指定所属 room。")
        raw_props = self.static_value(values["properties"]) if "properties" in values else {}
        if not isinstance(raw_props, dict):
            self.fail(values["properties"], "properties 必须是字典。")
        raw_state = self.static_value(values["state"]) if "state" in values else {}
        raw_belief = self.static_value(values["belief"]) if "belief" in values else {}
        raw_observable = self.static_value(values["observable"]) if "observable" in values else {}
        if not isinstance(raw_state, dict) or not isinstance(raw_belief, dict) or not isinstance(raw_observable, dict):
            self.fail(call, "state、belief 与 observable 必须是字典。")
        if "sprite" in values:
            if "sprite" in raw_props:
                self.fail(call, "sprite 不能同时放在 properties 和顶层参数。")
            raw_props["sprite"] = self.static_value(values["sprite"])
        combined = dict(raw_props)
        for tag, value in raw_state.items():
            if not isinstance(tag, str) or not tag:
                self.fail(call, "state 标签必须是非空字符串。")
            combined[f"state:{tag}"] = value
        for tag, value in raw_belief.items():
            if not isinstance(tag, str) or not tag:
                self.fail(call, "belief 标签必须是非空字符串。")
            combined[f"belief:{tag}"] = value
        for tag, value in raw_observable.items():
            if not isinstance(tag, str) or not tag:
                self.fail(call, "observable 标签必须是非空字符串。")
            combined[f"observable:{tag}"] = value
        for key, value in combined.items():
            if key in properties:
                self.fail(call, f"属性 {key!r} 重复声明。")
            if key.startswith("belief:") and kind != "character":
                self.fail(call, "belief:<tag> 仅支持 character；请用 observable/state 定义物件表面与客观状态。")
            properties[key] = self.typed_property(key, value, call)
        if "sprite" in properties and kind != "character":
            self.fail(call, "sprite 属性当前只支持 character。")
        declared_properties = {key: dict(value) for key, value in properties.items()}
        profile = None
        if "profile" in values:
            if kind != "character":
                self.fail(values["profile"], "profile 仅支持 character 实体。")
            profile = self.compile_profile(values["profile"])
        entity = Entity(entity_id, name, kind, scope, properties, declared_properties,
                        raw_aliases, self.location(call), self.current.id, profile)
        self.entities[entity_id] = entity
        self.current.entities.append(entity)
        self.aliases[alias] = entity_id
        self.entity_aliases[name] = entity_id
        for display_alias in raw_aliases:
            self.entity_aliases[display_alias] = entity_id
        self.record_source(entity.source)

    def compile_extend(self, call: ast.Call) -> None:
        values = self.arguments(call, {"target", "properties"}, required={"target", "properties"})
        entity = self.entity_ref(values["target"])
        changes = self.static_value(values["properties"])
        if not isinstance(changes, dict) or not changes:
            self.fail(values["properties"], "extend 的 properties 必须是非空字典。")
        typed_changes: dict[str, dict[str, Any]] = {}
        for key, value in changes.items():
            if not isinstance(key, str):
                self.fail(values["properties"], "属性键必须是字符串。")
            typed = self.typed_property(key, value, call)
            if key.startswith("belief:") and entity.kind != "character":
                self.fail(call, "belief:<tag> 仅支持 character。")
            previous = entity.properties.get(key)
            if previous is not None:
                self.fail(call, f"extend 只能增加新属性；{entity.id}.{key} 已存在。")
            if not key.startswith(("state:", "observable:")):
                self.fail(call, "extend 当前只允许新增 state:<tag> 与 observable:<tag> 属性。")
            if key.startswith("observable:") and typed["value"] is not True:
                self.fail(call, "新增 observable:<tag> 属性当前必须设为 true。")
            typed_changes[key] = typed
        self.current.patches.append({"target": entity.runtime_id(), "mode": "extend", "properties": typed_changes,
                                     "source_id": self.record_source(self.location(call))})
        entity.properties.update(typed_changes)

    def compile_behavior(self, node: ast.FunctionDef | ast.AsyncFunctionDef) -> None:
        if isinstance(node, ast.AsyncFunctionDef):
            self.fail(node, "不支持 async 候选声明；脚本函数不会被执行。")
        if node.args.posonlyargs or node.args.kwonlyargs or node.args.vararg or node.args.kwarg or len(node.args.args) > 1:
            self.fail(node, "候选响应函数不接受参数；请直接引用已声明实体。")
        if not node.decorator_list:
            self.fail(node, "候选函数必须用 @option/@reaction（或 @选项/@反应）声明。")
        if len(node.decorator_list) != 1:
            self.fail(node, "每个候选函数只能有一个声明装饰器。")
        decorator = node.decorator_list[0]
        if not isinstance(decorator, ast.Call) or self.call_name(decorator.func) not in BEHAVIOR_NAMES:
            self.fail(decorator, "仅支持 flat @option/@reaction 或 @选项/@反应 候选。")
        kind = BEHAVIOR_NAMES[self.call_name(decorator.func)]
        if kind == "option":
            allowed = {"id", "actor", "target", "label", "applicability", "goal", "delay", "scope", "selection_only"}
            values = self.arguments(decorator, allowed, required={"id"})
        else:
            allowed = {"id", "actor", "target", "response_to", "applicability", "goal", "weight", "delay", "scope", "intent"}
            values = self.arguments(decorator, allowed, required={"id", "actor", "goal", "intent"})
        candidate_id = self.literal_string(values["id"], decorator)
        self.validate_id(candidate_id, decorator, "候选行动")
        full_id = f"{self.current.id}:{candidate_id}"
        if full_id in self.behavior_ids:
            self.fail(decorator, f"候选 ID {candidate_id!r} 在章节内重复。")
        self.behavior_ids.add(full_id)
        self.candidate_kinds[full_id] = kind
        if kind == "option":
            actor = self.static_value(values.get("actor", ast.Constant(value="player")))
            if actor != "player":
                self.fail(values.get("actor", decorator), "option 的 actor 必须是 'player'。")
            policy = {"mode": "player", "weight": 0}
            selection_only = self.static_value(values.get("selection_only", ast.Constant(value=False)))
            if type(selection_only) is not bool:
                self.fail(values.get("selection_only", decorator), "selection_only 必须是布尔值。")
        else:
            actor_entity = self.entity_ref(values["actor"], "character")
            actor = actor_entity.runtime_id()
            intent = self.literal_string(values["intent"], decorator)
            if intent not in REACTION_INTENTS:
                self.fail(values["intent"], "reaction intent 只能是 'testimony' 或 'decline'。")
            weight = self.static_value(values.get("weight", ast.Constant(value=1)))
            weight = self.require_int32(weight, values.get("weight", decorator), "reaction weight")
            if weight < 0:
                self.fail(values.get("weight", decorator), "reaction weight 必须是非负整数。")
            policy = {"mode": "core", "weight": weight}
            selection_only = False
        target = self.entity_ref(values["target"]).runtime_id() if "target" in values else None
        applicability = self.compile_candidate_expr(values["applicability"])[0] if "applicability" in values else None
        goal = self.compile_candidate_expr(values["goal"])[0] if "goal" in values else None
        for key, expression in (("applicability", applicability), ("goal", goal)):
            if expression is not None and self.expression_type(values[key]) != "boolean":
                self.fail(values[key], f"{key} 必须是布尔条件。")
        if kind == "reaction":
            raw_links = self.static_value(values["response_to"]) if "response_to" in values else []
            if isinstance(raw_links, str): raw_links = [raw_links]
            if not isinstance(raw_links, list) or any(not isinstance(link, str) for link in raw_links):
                self.fail(values.get("response_to", decorator), "reaction response_to 必须是本章 option ID 字符串列表。")
            for link in raw_links:
                self.validate_id(link, values["response_to"], "回应候选 ID")
            response_to = [link if ":" in link else f"{self.current.id}:{link}" for link in raw_links]
        else:
            response_to = []
        delay = self.static_value(values.get("delay", ast.Constant(value=1)))
        if not isinstance(delay, (int, float)) or isinstance(delay, bool) or delay < 0 or not math.isfinite(delay):
            self.fail(values.get("delay", decorator), "delay 必须是有限非负数。")
        scope = self.static_value(values.get("scope", ast.Constant(value="world")))
        if scope not in ENTITY_SCOPES:
            self.fail(values.get("scope", decorator), "候选 scope 只能是 'world' 或 'chapter'。")
        if selection_only:
            if len(node.body) != 1 or not isinstance(node.body[0], ast.Pass):
                self.fail(node, "selection_only option 函数体只能写 pass；分支正文请放到对应 sequence。")
            response = []
        elif kind == "reaction" and len(node.body) == 1 and isinstance(node.body[0], ast.Pass):
            response = []
        else:
            response = self.compile_candidate_response(node.body)
        label = self.literal_string(values["label"], decorator) if "label" in values else None
        if kind == "option":
            if label is None:
                first_line = next((line for line in response if line["op"] == "line"), None)
                if first_line is None:
                    self.fail(node, "option 必须声明 label，或包含一条台词供玩家选择。")
                label = first_line["text"]
        self.current.candidates.append({
            "id": candidate_id, "kind": kind, "actor": actor, "target": target, "label": label,
            "applicability": applicability, "goal": goal, "response": response,
            "response_to": response_to, "policy": policy, "delay": float(delay), "scope": scope,
            "source_id": self.record_source(self.location(decorator)),
        })
        if kind == "reaction":
            self.current.candidates[-1]["intent"] = intent
        if kind == "option":
            self.candidate_labels[full_id] = label

    def compile_sequence(self, node: ast.FunctionDef | ast.AsyncFunctionDef) -> None:
        if self.current is None:
            self.fail(node, "sequence 必须位于 chapter 声明之后。")
        if isinstance(node, ast.AsyncFunctionDef) or node.args.args or node.args.posonlyargs or node.args.kwonlyargs or node.args.vararg or node.args.kwarg:
            self.fail(node, "sequence 是纯声明，不接受参数，也不能是 async。")
        if len(node.decorator_list) != 1 or not isinstance(node.decorator_list[0], ast.Call):
            self.fail(node, "每个剧情序列必须使用单个 @sequence(...) 装饰器。")
        decorator = node.decorator_list[0]
        name = self.call_name(decorator.func)
        if name not in SEQUENCE_NAMES:
            self.fail(decorator, "剧情声明只支持 @sequence/@场面。")
        values = self.arguments(decorator, {"id", "event", "day", "slot", "once", "condition"}, required={"id", "event"})
        sequence_id = self.literal_string(values["id"], decorator)
        self.validate_id(sequence_id, decorator, "sequence")
        if sequence_id in self.sequence_ids:
            self.fail(decorator, f"sequence ID {sequence_id!r} 重复。")
        self.sequence_ids.add(sequence_id)
        event = self.literal_string(values["event"], decorator)
        trigger: dict[str, Any] = {"event": event, "once": True}
        if "day" in values:
            day = self.static_value(values["day"])
            self.require_int32(day, values["day"], "剧情 day")
            if day < 1:
                self.fail(values["day"], "剧情 day 必须为正整数。")
            trigger["day"] = day
        if "slot" in values:
            slot = self.static_value(values["slot"])
            if not isinstance(slot, str) or slot not in {"Morning", "Forenoon", "Noon", "Afternoon", "Evening", "Night"}:
                self.fail(values["slot"], "slot 必须是 Morning/Forenoon/Noon/Afternoon/Evening/Night。")
            trigger["slot"] = slot
        if "once" in values:
            once = self.static_value(values["once"])
            if type(once) is not bool: self.fail(values["once"], "once 必须是布尔值。")
            trigger["once"] = once
        if "condition" in values:
            condition = self.static_value(values["condition"])
            if type(condition) is not bool:
                self.fail(values["condition"], "当前运行时只支持静态布尔 trigger.condition；状态条件尚不可执行。")
            trigger["condition"] = condition
        steps = self.compile_sequence_steps(node.body)
        self.current.sequences.append({"id": sequence_id, "trigger": trigger, "steps": steps,
                                       "source_id": self.record_source(self.location(decorator))})

    def compile_sequence_steps(self, statements: list[ast.stmt]) -> list[dict[str, Any]]:
        steps: list[dict[str, Any]] = []
        for statement in statements:
            if isinstance(statement, ast.Pass):
                continue
            if isinstance(statement, ast.Expr) and isinstance(statement.value, ast.Constant) and isinstance(statement.value.value, str):
                block = self.dialogue_blocks.get(statement.value.value)
                if block is None:
                    self.fail(statement, "sequence 正文仅允许反引号对白块、choice() 与 action()。")
                steps.extend(self.compile_dialogue_lines(block, statement.lineno))
                continue
            if not (isinstance(statement, ast.Expr) and isinstance(statement.value, ast.Call)):
                self.fail(statement, "sequence 正文不支持该语法；仅允许顺序对白、choice() 与 action()。")
            call = statement.value
            name = self.call_name(call.func)
            if name in {"choice", "选择"}:
                values = self.arguments(call, {"id", "options"}, required={"id", "options"})
                choice_id = self.literal_string(values["id"], call)
                raw_options = self.static_value(values["options"])
                if not isinstance(raw_options, list) or not raw_options or any(not isinstance(v, str) for v in raw_options):
                    self.fail(values["options"], "choice options 必须是非空 option ID 字符串列表。")
                options = []
                for local_id in raw_options:
                    full_id = local_id if local_id.startswith(self.current.id + ":") else f"{self.current.id}:{local_id}"
                    if self.candidate_kinds.get(full_id) != "option":
                        self.fail(values["options"], f"choice 引用的 {local_id!r} 未声明为本章 flat option。")
                    options.append({"candidate_id": full_id, "label": self.candidate_labels[full_id]})
                if len({item["candidate_id"] for item in options}) != len(options):
                    self.fail(values["options"], "choice options 不能重复。")
                steps.append({"op": "choice", "id": choice_id, "options": options})
                continue
            if name in {"action", "剧情动作"}:
                values = self.arguments(call, {"name", "args"}, required={"name", "args"})
                action_name = self.literal_string(values["name"], call)
                expected_types = AUTHORITY_ACTIONS.get(action_name)
                if expected_types is None:
                    self.fail(values["name"], f"未注册的权威剧情动作 {action_name!r}。")
                raw_args = self.static_value(values["args"])
                if not isinstance(raw_args, list) or len(raw_args) != len(expected_types):
                    self.fail(values["args"], f"动作 {action_name} 参数数量必须为 {len(expected_types)}。")
                for index, (argument, expected) in enumerate(zip(raw_args, expected_types)):
                    actual = infer(argument)["type"]
                    if expected == "boolean" and actual != "boolean":
                        self.fail(values["args"], f"动作 {action_name} 第 {index + 1} 个参数必须是布尔值。")
                    if actual == "integer": self.require_int32(argument, values["args"], f"动作 {action_name} 参数")
                    if actual not in {"string", "integer", "number", "boolean"}:
                        self.fail(values["args"], "剧情动作参数必须是标量字面量。")
                steps.append({"op": "action", "name": action_name, "args": raw_args})
                continue
            self.fail(call, f"sequence 不支持调用 {name!r}；权威动作请用 action(name=..., args=[...])。")
        return steps

    def compile_dialogue_lines(self, block: str, start_line: int) -> list[dict[str, Any]]:
        result = []
        emotion = re.compile(r"\s+\[#emotion=([a-z_]+)\]$")
        variables = re.compile(r"\{\{\s*(detective_name|wrong_name\(\))\s*\}\}")
        for offset, raw_line in enumerate(block.splitlines()):
            line = raw_line.strip()
            if not line: continue
            line_number = start_line + offset
            original = self.source_lines[line_number - 1] if line_number <= len(self.source_lines) else raw_line
            source = Source(self.current_path, line_number, len(original) - len(original.lstrip()) + 1,
                            line_number, len(original) + 1)
            line_id = ""
            tagged = LINE_ID_TAG.search(line)
            if tagged:
                line_id, line = tagged.group(1), (line[:tagged.start()] + line[tagged.end():]).rstrip()
            mood = ""
            match = emotion.search(line)
            if match:
                mood = match.group(1)
                line = line[:match.start()].rstrip()
            elif "[#emotion=" in line:
                raise ScriptError("情绪标记需是末尾的 [#emotion=name] 格式。", source)
            separator = "：" if "：" in line else ":" if ":" in line else None
            if separator is None:
                speaker, text = "", line
            else:
                speaker, text = (part.strip() for part in line.split(separator, 1))
                if not speaker:
                    raise ScriptError("对白说话人不能为空。", source)
                entity_id = self.entity_aliases.get(speaker)
                if speaker != "旁白" and entity_id is None:
                    raise ScriptError(f"对白说话人 {speaker!r} 未在人物 aliases 中声明。", source)
                if entity_id is not None and self.entities[entity_id].kind not in {"character", "object"}:
                    raise ScriptError(f"对白说话人 {speaker!r} 不是人物或发声物件。", source)
            if not text:
                raise ScriptError("对白内容不能为空。", source)
            for placeholder in re.findall(r"\{\{.*?\}\}", text):
                if variables.fullmatch(placeholder) is None:
                    raise ScriptError(f"不支持的对白插值 {placeholder!r}。", source)
            source_ref = self.record_source(source)
            result.append({"op": "line", "speaker": speaker, "text": text, "mood": mood,
                           "voice_clip": "", "line_id": line_id, "source_id": source_ref})
        return result

    def compile_candidate_response(self, statements: list[ast.stmt]) -> list[dict[str, Any]]:
        response: list[dict[str, Any]] = []
        for statement in statements:
            if isinstance(statement, ast.Expr) and isinstance(statement.value, ast.Constant) and isinstance(statement.value.value, str):
                sentinel = statement.value.value
                block = self.dialogue_blocks.get(sentinel, sentinel)
                first_line = self.dialogue_block_start_lines.get(sentinel, statement.lineno)
                for offset, raw_line in enumerate(block.splitlines()):
                    line = raw_line.strip()
                    if not line: continue
                    line_number = first_line + offset
                    original = self.source_lines[line_number - 1] if line_number <= len(self.source_lines) else raw_line
                    source = Source(self.current_path, line_number, len(original) - len(original.lstrip()) + 1,
                                    line_number, len(original) + 1)
                    line_id = ""
                    tagged = LINE_ID_TAG.search(line)
                    if tagged:
                        line_id, line = tagged.group(1), (line[:tagged.start()] + line[tagged.end():]).rstrip()
                    separator = "：" if "：" in line else ":" if ":" in line else None
                    if separator is None:
                        raise ScriptError("fence 台词每行请写‘人物名：台词’或‘旁白：台词’。", source)
                    speaker_name, text = line.split(separator, 1)
                    speaker_name, text = speaker_name.strip(), text.strip()
                    if not speaker_name or not text:
                        raise ScriptError("fence 台词的人物名与台词内容都不能为空。", source)
                    if speaker_name == "旁白":
                        speaker = speaker_name
                    else:
                        speaker_id = self.entity_aliases.get(speaker_name)
                        if speaker_id is None:
                            raise ScriptError(f"fence 台词说话人 {speaker_name!r} 未声明为人物名称或别名。", source)
                        speaker_entity = self.entities[speaker_id]
                        if speaker_entity.kind != "character":
                            raise ScriptError(f"fence 台词说话人 {speaker_name!r} 不是人物实体。", source)
                        speaker = speaker_entity.runtime_id()
                    source_ref = self.record_source(source)
                    response.append({"op": "line", "speaker": speaker, "mood": "neutral", "text": text,
                                     "voice_clip": "", "line_id": line_id, "source_id": source_ref})
                continue
            if not (isinstance(statement, ast.Expr) and isinstance(statement.value, ast.Call)):
                self.fail(statement, "候选 response 只允许裸 fence 台词块、line()/台词() 与白名单动作；不支持 if、循环或嵌套答案。")
            call = statement.value
            source = self.record_source(self.location(call))
            name = self.call_name(call.func)
            if name in {"line", "台词", "dialogue"}:
                values = self.arguments(call, {"speaker", "text", "mood", "voice_clip"}, required={"speaker", "text"})
                speaker = self.speaker_value(values["speaker"])
                text = self.literal_string(values["text"], call)
                mood = self.static_value(values.get("mood", ast.Constant(value="neutral")))
                voice = self.static_value(values.get("voice_clip", ast.Constant(value="")))
                if not isinstance(mood, str) or not isinstance(voice, str):
                    self.fail(call, "台词 mood 与 voice_clip 必须是字符串。")
                response.append({"op": "line", "speaker": speaker, "mood": mood, "text": text,
                                 "voice_clip": voice, "source_id": source})
            else:
                action_name = ACTION_NAMES.get(name or "")
                if action_name is None:
                    self.fail(call, f"未知候选动作 {name!r}；当前支持 say/set/change/spend/inspect/take/destroy/place/travel/unlock。")
                response.append(self.compile_candidate_action(action_name, call, source))
        if not response:
            self.fail(statements[0] if statements else ast.Pass(), "候选必须包含至少一条台词或一个动作。")
        return response

    def speaker_value(self, node: ast.AST) -> str:
        if isinstance(node, ast.Constant) and isinstance(node.value, str) and node.value == "旁白": return node.value
        entity = self.entity_ref(node, "character")
        return entity.runtime_id()

    def compile_candidate_action(self, name: str, call: ast.Call, source: str) -> dict[str, Any]:
        signature = {
            "say": {"recipient", "text"}, "set": {"target", "tag", "value"},
            "change": {"target", "tag", "amount"}, "spend": {"target", "tag", "amount"},
            "inspect": {"target"}, "take": {"target"}, "destroy": {"target"},
            "place": {"target", "destination"}, "travel": {"target"}, "unlock": {"target", "tool"},
        }[name]
        args = self.arguments(call, signature, required=signature)
        ordered = ["recipient", "text"] if name == "say" else list({
            "set": ("target", "tag", "value"), "change": ("target", "tag", "amount"),
            "spend": ("target", "tag", "amount"), "inspect": ("target",), "take": ("target",),
            "destroy": ("target",), "place": ("target", "destination"), "travel": ("target",),
            "unlock": ("target", "tool"),
        }[name])
        compiled = []
        for key in ordered:
            node = args[key]
            if key in {"target", "recipient", "destination", "tool"}:
                compiled.append(self.literal_expr(self.entity_ref(node).runtime_id()))
            elif key in {"tag"}:
                compiled.append(self.literal_expr(self.literal_string(node, call)))
            elif key == "text":
                compiled.append(self.literal_expr(self.literal_string(node, call)))
            else:
                expression, value_type = self.compile_candidate_expr(node)
                wanted = "integer"
                if value_type != wanted:
                    self.fail(node, f"动作 {name} 的 {key} 必须是 {wanted}。")
                if wanted == "integer" and expression.get("op") != "literal":
                    self.fail(node, f"动作 {name} 的 {key} 必须是 Int32 字面量。")
                if name == "spend" and key == "amount" and expression.get("value", 0) < 0:
                    self.fail(node, "spend amount 不能为负数。")
                compiled.append(expression)
        return {"op": "call", "name": name, "args": compiled, "source_id": source}

    def compile_candidate_expr(self, node: ast.AST) -> tuple[dict[str, Any], str]:
        if isinstance(node, ast.Constant) and (node.value is None or isinstance(node.value, (str, int, float, bool))):
            if isinstance(node.value, int) and not isinstance(node.value, bool):
                self.require_int32(node.value, node, "Core 整数常量")
            return self.literal_expr(node.value), infer(node.value)["type"]
        if isinstance(node, ast.UnaryOp) and isinstance(node.op, (ast.USub, ast.UAdd)) and isinstance(node.operand, ast.Constant):
            number = node.operand.value
            if isinstance(number, int) and not isinstance(number, bool):
                number = -number if isinstance(node.op, ast.USub) else number
                self.require_int32(number, node, "Core 整数常量")
                return self.literal_expr(number), "integer"
        if isinstance(node, ast.UnaryOp) and isinstance(node.op, ast.Not):
            value, kind = self.compile_candidate_expr(node.operand)
            if kind != "boolean": self.fail(node, "not 只支持布尔表达式。")
            return {"op": "not", "args": [value]}, "boolean"
        if isinstance(node, ast.BoolOp) and isinstance(node.op, (ast.And, ast.Or)):
            children = [self.compile_candidate_expr(child) for child in node.values]
            if any(kind != "boolean" for _, kind in children): self.fail(node, "and/or 两侧必须是布尔表达式。")
            return {"op": "and" if isinstance(node.op, ast.And) else "or", "args": [item for item, _ in children]}, "boolean"
        if isinstance(node, ast.Compare) and len(node.ops) == 1 and len(node.comparators) == 1:
            left, left_type = self.compile_candidate_expr(node.left)
            right, right_type = self.compile_candidate_expr(node.comparators[0])
            op = {ast.Eq: "eq", ast.NotEq: "ne", ast.Gt: "gt", ast.GtE: "gte", ast.Lt: "lt", ast.LtE: "lte"}.get(type(node.ops[0]))
            if op is None: self.fail(node, "条件只支持 ==、!=、<、<=、>、>=。")
            if left_type != "integer" or left.get("op") != "call" or left.get("name") != "state" or right_type != "integer" or right.get("op") != "literal" or isinstance(right.get("value"), bool):
                self.fail(node, "Core 目前只支持 state(entity, tag) 与整数常量比较。")
            return {"op": op, "args": [left, right]}, "boolean"
        if isinstance(node, ast.Name) and (node.id in self.aliases or node.id in self.entity_aliases):
            entity = self.resolve_entity(self.aliases.get(node.id, self.entity_aliases.get(node.id, "")), node)
            return self.literal_expr(entity.runtime_id()), "entity"
        if isinstance(node, ast.Call):
            name = self.call_name(node.func)
            if name not in {"known", "state", "at"}:
                self.fail(node, f"未知条件查询 {name!r}；只支持 known(entity)、state(entity, tag)、at(entity, room)。")
            expected = {"known": {"entity"}, "state": {"entity", "tag"}, "at": {"entity", "room"}}[name]
            args = self.expression_arguments(node, expected)
            if name == "known":
                entity = self.entity_ref(args["entity"])
                values = [self.literal_expr(entity.runtime_id())]
                result_type = "boolean"
            elif name == "state":
                entity = self.entity_ref(args["entity"])
                tag = self.literal_string(args["tag"], node)
                if "state:" + tag not in entity.properties and "belief:" + tag not in entity.properties and tag not in COGNITIVE_QUERY_TAGS:
                    self.fail(node, f"实体 {entity.id} 未声明 state:{tag} 或 character belief:{tag}。")
                values = [self.literal_expr(entity.runtime_id()), self.literal_expr(tag)]
                result_type = "integer"
            else:
                entity = self.entity_ref(args["entity"])
                room = self.entity_ref(args["room"], "room")
                values = [self.literal_expr(entity.runtime_id()), self.literal_expr(room.runtime_id())]
                result_type = "boolean"
            return {"op": "call", "name": name, "args": values}, result_type
        self.fail(node, "条件仅支持已声明实体、白名单查询、布尔组合和比较；不执行 Python 表达式。")

    def expression_arguments(self, call: ast.Call, names: set[str]) -> dict[str, ast.expr]:
        ordered = list(names)
        # Keep public query order explicit; sets themselves have no author-facing order.
        ordered = {"known": ["entity"], "state": ["entity", "tag"], "at": ["entity", "room"]}[self.call_name(call.func)]
        if len(call.args) > len(ordered):
            self.fail(call, "条件查询参数过多。")
        values = dict(zip(ordered, call.args))
        for keyword in call.keywords:
            if keyword.arg is None or keyword.arg not in names:
                self.fail(keyword.value, "条件查询只接受已知的命名参数。")
            if keyword.arg in values:
                self.fail(keyword.value, f"查询参数 {keyword.arg!r} 重复。")
            values[keyword.arg] = keyword.value
        if values.keys() != names:
            missing = names - values.keys()
            self.fail(call, "缺少查询参数：" + "、".join(sorted(missing)))
        return values

    @staticmethod
    def literal_expr(value: Any) -> dict[str, Any]:
        return {"op": "literal", "value": value}

    def expression_type(self, node: ast.AST) -> str:
        return self.compile_candidate_expr(node)[1]

    def validate_dependencies(self) -> None:
        for index, chapter in enumerate(self.chapters):
            for required in chapter.requires:
                if required not in self.chapter_ids and required not in self.external_chapters:
                    raise ScriptError(f"章节 {chapter.id!r} 依赖缺失章节 {required!r}。", chapter.source)
                if required in self.chapter_ids and self.chapter_ids[required] >= index:
                    raise ScriptError(f"章节 {chapter.id!r} 要求 {required!r}，但输入顺序必须先编译依赖章节。", chapter.source)
            for candidate in chapter.candidates:
                if candidate["kind"] != "reaction": continue
                for linked_id in candidate["response_to"]:
                    linked_chapter = linked_id.split(":", 1)[0]
                    if self.candidate_kinds.get(linked_id) != "option" or linked_chapter not in self.chapter_ids or self.chapter_ids[linked_chapter] > index:
                        raise ScriptError(f"reaction {candidate['id']!r} 引用了缺失或非 option 候选 {linked_id!r}。", chapter.source)
            branches: dict[str, int] = {}
            for sequence in chapter.sequences:
                event = sequence["trigger"]["event"]
                if event.startswith("choice:"):
                    branches[event.removeprefix("choice:")] = branches.get(event.removeprefix("choice:"), 0) + 1
            for sequence in chapter.sequences:
                for step in sequence["steps"]:
                    if step["op"] != "choice": continue
                    for option in step["options"]:
                        candidate_id = option["candidate_id"]
                        if branches.get(candidate_id) != 1:
                            raise ScriptError(f"剧情选项 {candidate_id!r} 必须有且仅有一个 trigger.event='choice:{candidate_id}' 后续 sequence。", chapter.source)
    def document(self) -> dict[str, Any]:
        return {"format": FORMAT, "external_chapters": sorted(self.external_chapters),
                "chapters": [chapter.json() for chapter in self.chapters],
                "source_map": self.source_map}


def compile_paths(paths: list[Path], external_chapters: set[str] | None = None) -> dict[str, Any]:
    if not paths:
        raise ScriptError("至少提供一个按顺序排列的脚本文件。")
    for external in external_chapters or set():
        if not external or any(char.isspace() for char in external) or ":" in external:
            raise ScriptError(f"外部章节 ID 无效：{external!r}。")
    return Compiler(paths, external_chapters).parse()


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("scripts", nargs="+", type=Path, help="ordered chapter Python scripts; dependencies must appear first")
    parser.add_argument("--external-chapter", action="append", default=[], metavar="ID",
                        help="declare an externally supplied chapter handoff contract (the runtime must activate and validate it)")
    parser.add_argument("-o", "--output", type=Path, help="write deterministic typed JSON IR to this file (default: stdout)")
    args = parser.parse_args(argv)
    try:
        document = compile_paths(args.scripts, set(args.external_chapter))
    except ScriptError as exc:
        if exc.source:
            location = exc.source
            print(f"{location.path}:{location.line}:{location.column}: {exc}", file=sys.stderr)
        else:
            print(str(exc), file=sys.stderr)
        return 2
    serialized = json.dumps(document, ensure_ascii=False, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(serialized, encoding="utf-8", newline="\n")
    else:
        sys.stdout.write(serialized)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
