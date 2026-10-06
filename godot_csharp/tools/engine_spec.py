"""Typed authoring model for the Awakening chapter compiler."""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Iterable


@dataclass(frozen=True)
class Expr:
    op: str
    value: Any = None
    name: str = ""
    args: tuple["Expr", ...] = ()

    def json(self) -> dict[str, Any]:
        if self.op == "literal":
            return {"op": "literal", "value": self.value}
        result: dict[str, Any] = {"op": self.op}
        if self.name:
            result["name"] = self.name
        if self.args:
            result["args"] = [arg.json() for arg in self.args]
        return result


@dataclass(frozen=True)
class Instruction:
    op: str
    data: dict[str, Any] = field(default_factory=dict)

    def json(self) -> dict[str, Any]:
        def convert(value: Any) -> Any:
            if isinstance(value, Expr):
                return value.json()
            if isinstance(value, Instruction):
                return value.json()
            if isinstance(value, (tuple, list)):
                return [convert(part) for part in value]
            if isinstance(value, dict):
                return {key: convert(part) for key, part in value.items()}
            return value
        return {"op": self.op, **{key: convert(value) for key, value in self.data.items()}}


@dataclass(frozen=True)
class ChapterSpec:
    """Reusable chapter boundary joining typed dialogue entries and entity records."""
    id: str
    entries: tuple[tuple[str, list[Instruction]], ...]
    entities: tuple[Any, ...] = ()


def literal(value: Any) -> Expr:
    return Expr("literal", value=value)


def call(name: str, *args: Expr | str | int | bool) -> Expr:
    return Expr("call", name=name, args=tuple(arg if isinstance(arg, Expr) else literal(arg) for arg in args))


def unary(op: str, value: Expr) -> Expr:
    return Expr(op, args=(value,))


def binary(op: str, left: Expr, right: Expr) -> Expr:
    return Expr(op, args=(left, right))


def line(speaker: str, text: str, mood: str = "", voice_clip: str = "") -> Instruction:
    if not isinstance(text, str):
        raise TypeError("dialogue line text must be a string")
    return Instruction("line", {"speaker": speaker, "mood": mood, "text": text, "voice_clip": voice_clip})


def effect(name: str, *args: Expr | str | int | bool) -> Instruction:
    return Instruction("call", {"name": name, "args": [arg if isinstance(arg, Expr) else literal(arg) for arg in args]})


def jump(target: str) -> Instruction:
    return Instruction("jump", {"target": target})


def condition(test: Expr, then: Iterable[Instruction], otherwise: Iterable[Instruction] = ()) -> Instruction:
    return Instruction("if", {"condition": test, "then": _build(then), "else": _build(otherwise)})


def choice_option(id: str, label: str, body: Iterable[Instruction] = (), test: Expr | None = None,
                  lock_hint: str = "") -> dict[str, Any]:
    return {"id": id, "label": label, "condition": test, "lock_hint": lock_hint, "body": list(body)}


def choices(options: Iterable[dict[str, Any]]) -> Instruction:
    return Instruction("choices", {"options": list(options)})


def _build(instructions: Iterable[Any] | Instruction) -> list[Instruction]:
    if isinstance(instructions, Instruction) or isinstance(instructions, dict):
        instructions = [instructions]
    result: list[Instruction] = []
    pending: list[dict[str, Any]] = []
    def flush() -> None:
        if pending:
            result.append(choices(tuple(pending)))
            pending.clear()
    for item in instructions:
        if isinstance(item, dict) and {"id", "label", "body"} <= item.keys():
            pending.append(item)
        else:
            flush()
            if not isinstance(item, Instruction):
                raise TypeError(f"expected instruction or choice declaration, got {type(item).__name__}")
            if item.op == "if":
                item = Instruction("if", {**item.data, "then": _build(item.data["then"]), "else": _build(item.data["else"])})
            result.append(item)
    flush()
    return result


def entry(id: str, *instructions: Instruction) -> tuple[str, list[Instruction]]:
    return id, _build(instructions)


def dialogue_document(entries: Iterable[tuple[str, list[Instruction]]]) -> dict[str, Any]:
    items = list(entries)
    if len({name for name, _ in items}) != len(items):
        raise ValueError("duplicate dialogue entry id")
    known = {name for name, _ in items}

    def validate(instructions: list[Instruction], where: str) -> None:
        for instruction in instructions:
            if instruction.op == "jump" and instruction.data["target"] not in known | {"END"}:
                raise ValueError(f"{where}: unknown jump target {instruction.data['target']!r}")
            if instruction.op == "if":
                validate(instruction.data["then"], where)
                validate(instruction.data["else"], where)
            if instruction.op == "choices":
                for option in instruction.data["options"]:
                    validate(option["body"], where)
    for name, instructions in items:
        validate(instructions, name)
    return {"entries": {name: [instruction.json() for instruction in instructions] for name, instructions in items}}
