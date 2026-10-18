#!/usr/bin/env python3
"""Build a deterministic, source-preserving seven-file DV review package."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
import tempfile
from pathlib import Path

MANIFEST = "manifest.json"
README = "README.md"
GENERATED = [
    "01_dv_compiler.dv", "02_dv_core_runtime.dv", "03_dv_session_simulation.dv",
    "04_dv_story_chapter1.dv", "05_dv_story_chapter2.dv", "06_dv_game_integration.dv",
    "07_dv_verification_checks.dv", MANIFEST, README,
]
SPECS = [
    ("01_dv_compiler.dv", "compiler", "language_core/compiler", False),
    ("02_dv_core_runtime.dv", "core", "language_core/core", True),
    ("03_dv_session_simulation.dv", "session", "language_core/verification/session", True),
    ("04_dv_story_chapter1.dv", "chapter1", "language_core/verification/scenarios/chapter1", True),
    ("05_dv_story_chapter2.dv", "chapter2", "language_core/verification/scenarios/chapter2", True),
    ("06_dv_game_integration.dv", "game", "language_core/game", True),
    ("07_dv_verification_checks.dv", "verification_checks", "language_core/verification/checks", True),
]


class PackageError(Exception):
    pass


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def safe_relative(path: Path, root: Path) -> str:
    return path.relative_to(root).as_posix()


def collect(root: Path, rel: str, recursive: bool) -> list[tuple[Path, bytes]]:
    folder = root / Path(rel)
    if not folder.is_dir():
        raise PackageError(f"缺少输入目录：{rel}")
    paths = folder.rglob("*.dv") if recursive else folder.glob("*.dv")
    files = sorted((p for p in paths if p.is_file()), key=lambda p: p.relative_to(root).as_posix())
    if not files:
        raise PackageError(f"模块没有 .dv 文件：{rel}")
    result = []
    for path in files:
        try:
            data = path.read_bytes()
            data.decode("utf-8")
        except (OSError, UnicodeDecodeError) as exc:
            raise PackageError(f"无法读取 UTF-8 源文件 {safe_relative(path, root)}：{exc}") from exc
        result.append((path, data))
    return result


def build(root: Path) -> tuple[dict[str, bytes], dict]:
    contents: dict[str, bytes] = {}
    modules = []
    for output, key, rel, recursive in SPECS:
        files = collect(root, rel, recursive)
        sections = [
            "// DV SOURCE REVIEW PACKAGE — preserves original file boundaries.\n"
            "// This combined file is not a directly linkable program.\n"
        ]
        sources = []
        package_line = 3  # Line number of the next SOURCE header.
        for path, raw in files:
            text = raw.decode("utf-8").replace("\r\n", "\n").replace("\r", "\n")
            if text and not text.endswith("\n"):
                text += "\n"
            source_lines = text.count("\n")
            relpath = safe_relative(path, root)
            digest = sha256(raw)
            header = f"// ----- SOURCE: {relpath} | SHA256: {digest} -----\n"
            header_line = package_line
            start = header_line + 1
            end = start + source_lines - 1
            sections.extend((header, text, "\n"))
            sources.append({
                "path": relpath, "sha256": digest, "source_lines": source_lines,
                "package_line_start": start, "package_line_end": end,
            })
            # Header + body + one separator line; this also handles an empty body.
            package_line = header_line + source_lines + 2
        contents[output] = "".join(sections).encode("utf-8")
        modules.append({"key": key, "output": output, "input": rel, "recursive": recursive, "sources": sources})

    manifest = {
        "format": "dv-source-review-package-v1",
        "purpose": "七份按目录组合的源码审阅包，不是可直接联编程序；每个来源文件边界仍由标记保留。",
        "root_relative": ".",
        "modules": modules,
        "scope": {
            "included": [
                "language_core/compiler/*.dv（仅直接子文件；不含 examples 或其他子目录）",
                "language_core/core/**/*.dv",
                "language_core/verification/session/**/*.dv",
                "language_core/verification/scenarios/chapter1/**/*.dv",
                "language_core/verification/scenarios/chapter2/**/*.dv",
                "language_core/game/**/*.dv",
                "language_core/verification/checks/**/*.dv（包括 report、fixtures、authority 子目录）",
            ],
            "excluded": [
                "compiler 下的子目录文件（包括 examples）",
                "上述七个输入范围以外的仓库文件及非 .dv 文件",
                "verification/scenarios 下不属于 chapter1、chapter2 的其他内容",
                "验证目录中 checks 以外的内容（除明确纳入的 session/scenario 范围）",
            ],
        },
        "generated_files": GENERATED,
    }
    contents[MANIFEST] = (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    contents[README] = make_readme(manifest).encode("utf-8")
    return contents, manifest


def make_readme(manifest: dict) -> str:
    labels = [
        "DV 编译器", "DV Core", "会话执行与调度", "第一章场景", "第二章场景", "游戏接线", "验证检查",
    ]
    lines = [
        "# DV 七文件源码审阅包", "",
        "本目录由 `consolidate_dv.py` 从仓库指定的七个输入目录生成，供源码审阅和传递。",
        "七份文件按目录拼接，但每个原始文件都保留独立来源标记；它们是源码审阅包，不是可直接联编或运行的程序，也不代表全仓库源码。",
        "文件中的 `define`、`main`、`include` 等内容按原文保留。拼接不会改写这些声明之间的关系。", "",
        "## 七份文件", "",
    ]
    for module, label in zip(manifest["modules"], labels):
        lines.append(f"- `{module['output']}`：{label}；来源目录 `{module['input']}`，包含 {len(module['sources'])} 个 `.dv` 文件。")
    lines += ["", "## 范围", "", "纳入范围：", ""]
    lines.extend(f"- {item}" for item in manifest["scope"]["included"])
    lines += ["", "排除范围：", ""]
    lines.extend(f"- {item}" for item in manifest["scope"]["excluded"])
    lines += ["", "## 来源校验", "", "`manifest.json` 逐文件记录原始相对路径、原始字节 SHA-256、源文件行数，以及合并文件中的源正文行范围。空文件的结束行等于起始行减一，表示正文范围为空。文件按相对路径稳定排序。", ""]
    return "\n".join(lines)


def read_owned_names(out: Path) -> set[str]:
    path = out / MANIFEST
    if not path.exists():
        return set()
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError, UnicodeDecodeError):
        return set()
    if data.get("format") != "dv-source-review-package-v1":
        return set()
    names = data.get("generated_files")
    return set(names) if isinstance(names, list) and all(isinstance(n, str) for n in names) else set()


def main() -> int:
    parser = argparse.ArgumentParser(description="生成确定性的七文件 DV 源码审阅包")
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[4], help="仓库根目录，默认由脚本位置推导")
    parser.add_argument("--out", type=Path, help="输出目录，默认 <root>/artifacts/dv_consolidated_package_v2")
    parser.add_argument("--check", action="store_true", help="只比较预期内容与现有文件，不写入")
    args = parser.parse_args()
    root = args.root.resolve()
    out = (args.out if args.out is not None else root / "artifacts/dv_consolidated_package_v2").resolve()
    try:
        if not root.is_dir():
            raise PackageError(f"仓库根目录不存在：{root}")
        # Output must not overlap an input tree in either direction.
        for _, _, rel, _ in SPECS:
            source = (root / Path(rel)).resolve()
            if out == source or source in out.parents or out in source.parents:
                raise PackageError(f"输出目录与源码目录重叠：{out} / {source}")
        contents, _ = build(root)
        if args.check:
            missing = [name for name, data in contents.items() if not (out / name).is_file() or (out / name).read_bytes() != data]
            if missing:
                print("内容不匹配或缺失：" + ", ".join(missing), file=sys.stderr)
                return 1
            print(f"检查一致：{len(contents)} 个受管文件；未写入。")
            return 0

        out.parent.mkdir(parents=True, exist_ok=True)
        owned = read_owned_names(out)
        collisions = [name for name in contents if (out / name).exists() and name not in owned]
        if collisions:
            raise PackageError("拒绝覆盖未由本工具登记的现有文件：" + ", ".join(collisions))
        stage = Path(tempfile.mkdtemp(prefix=f".{out.name}.stage-", dir=out.parent))
        try:
            for name, data in contents.items():
                target = stage / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(data)
            out.mkdir(parents=True, exist_ok=True)
            # Replace only the nine named generated files; unrelated files are preserved.
            for name in contents:
                os.replace(stage / name, out / name)
        finally:
            stage_resolved = stage.resolve()
            if stage_resolved.parent != out.parent.resolve() or not stage_resolved.name.startswith(f".{out.name}.stage-"):
                raise PackageError(f"拒绝清理位置异常的临时目录：{stage_resolved}")
            shutil.rmtree(stage_resolved, ignore_errors=True)
        print(f"已生成 {len(contents) - 2} 份 DV 文件、manifest.json 与 README.md：{out}")
        return 0
    except (PackageError, OSError) as exc:
        print(f"错误：{exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
