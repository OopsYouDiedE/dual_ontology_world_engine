"""Export drawn UI text and scene objects; never starts or advances a game."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TARGET = ROOT / "artifacts/basic-ui-system/03-UI实际显示的内容.txt"


def export(trace: Path, target: Path) -> None:
    rows = [json.loads(line) for line in trace.read_text(encoding="utf-8-sig").splitlines() if line.strip()]
    completed = next((row.get("data", {}) for row in reversed(rows) if row.get("kind") == "render_finished"), {})
    if not all(completed.get(key) is True for key in ("finished", "ended_displayed", "engine_ended")):
        raise ValueError("UI 回放尚未完整绘制终局；不覆盖第三份全程记录。")
    visible = [row for row in rows if row.get("kind") == "ui_visible"]
    if not any(row.get("data", {}).get("panel") == "ending" for row in visible):
        raise ValueError("没有结束界面实际可见记录；不导出为全程。")
    start = next((row.get("data", {}) for row in rows if row.get("kind") == "render_started"), {})
    out = [
        "03 · UI 全程实际绘制的文字与场上物品",
        "=" * 60,
        "范围：醒来 → 生活翼调查与四问 → 同一局五日学园生活 → 名录库终局。",
        "来源：" + str(trace.resolve().relative_to(ROOT)).replace("\\", "/"),
        "对应引擎记录：" + str(start.get("source", "artifacts/basic-ui-system/player-traces/engine-full-output.jsonl")),
        "方式：将第二份同一局的实际输出交给正式 Godot 界面逐段呈现，从绘制后的界面采集。",
        "这是全程界面渲染回放；站位依公开房间与热点锚点重建，不作为真实碰撞、行走或盲玩记录。",
        "正文仅含当时实际显示的文字和可见场上物品；按钮上的文字保留，控件类型、路径、坐标及样式不列。",
        "长面板滚动后才记录新显示的内容，未显示的缓存文字不视作已见；重复文字表示不同画面时点。",
        "“引擎步骤”是对照索引；若附有时间，仅指回放时间，不是玩家阅读时长。",
        "",
    ]
    previous = None
    for row in visible:
        data = row.get("data", {})
        texts = [item["text"] for item in data.get("texts", []) if item.get("text")]
        objects = data.get("objects", [])
        if not isinstance(objects, list) or not all(isinstance(item, str) for item in objects):
            raise ValueError("场上物品应为名称列表，不能混入控件或内部对象数据。")
        step = data.get("source_sequence", row.get("source_sequence", ""))
        signature = (step, texts, objects)
        if signature == previous:
            continue
        previous = signature
        stamp = f" · 回放 {row['elapsed_ms'] / 1000:.3f}s" if "elapsed_ms" in row else ""
        out += [f"【引擎步骤 {step}{stamp}】", "文字："]
        out += texts or ["（无文字）"]
        out += ["场上物品：", "、".join(objects) if objects else "（当前没有可列出的可见物品）", ""]
    out += ["记录终点：第五日终局结束界面已实际绘制。"]
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text("\n".join(out) + "\n", encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("trace", type=Path)
    parser.add_argument("--output", type=Path, default=TARGET)
    args = parser.parse_args()
    export(args.trace.resolve(), args.output.resolve())
    print(args.output.resolve())
