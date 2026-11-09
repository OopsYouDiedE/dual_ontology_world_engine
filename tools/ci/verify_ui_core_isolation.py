#!/usr/bin/env python3
"""UI / Core 隔离门禁（违规时退出码 1）。
R1 core/**/*.cs 不得引用 Godot 命名空间。
R2 scenes/UIpanel/**/*.gd 不得引用会话桥或 Core（SessionBridge、DvPresentationContent、.cs 脚本、bridge.、core/）。
R3 scenes/UIpanel/**/*.gd 不得调用现场宿主私有成员（field._x / host._x）或以 field.get("…") 读取内部状态。
R4 scenes/UIpanel 下每个 .tscn 必须有同名 .gd（1:1 配对）。
R5 表现组件 SimsBehaviorDirector.gd 只能使用宿主公开端口（不得出现 host._ 或 bridge）。
"""
import pathlib, re, sys
ROOT = pathlib.Path(__file__).resolve().parents[2]
violations = []
def scan(files, pattern, rule, why):
    regex = re.compile(pattern)
    for path in files:
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            code = line.split("#", 1)[0] if path.suffix == ".gd" else line.split("//", 1)[0]
            if regex.search(code): violations.append((rule, f"{path.relative_to(ROOT)}:{number}", why, line.strip()[:120]))
core = [p for p in (ROOT / "core").rglob("*.cs") if "/bin/" not in str(p) and "/obj/" not in str(p)]
ui = sorted((ROOT / "scenes/UIpanel").rglob("*.gd"))
scan(core, r"^\s*using\s+Godot\b|GodotSharp", "R1", "Core 引用了 Godot")
scan(ui, r"SessionBridge|DvPresentationContent|\.cs\"|\bbridge\.|res://core/", "R2", "界面直接接触会话桥或 Core")
scan(ui, r"\b(field|host|_host|field_host)\._[a-z]|\bfield\.get\(\"", "R3", "界面读取现场宿主内部成员")
for scene in (ROOT / "scenes/UIpanel").rglob("*.tscn"):
    if not scene.with_suffix(".gd").exists(): violations.append(("R4", str(scene.relative_to(ROOT)), "缺少同名 .gd", ""))
scan([ROOT / "scenes/components/characters/SimsBehaviorDirector.gd"], r"\bhost\._|\bbridge\b", "R5", "表现组件绕过公开端口")
rules = {"R1": "Core 不引用 Godot", "R2": "界面不接触会话桥/Core", "R3": "界面不读宿主内部成员", "R4": "界面场景与脚本 1:1", "R5": "表现组件只用公开端口"}
print("UI / Core 隔离门禁")
for rule, title in rules.items():
    hits = [v for v in violations if v[0] == rule]
    print(f"  {'✔ PASS' if not hits else '✘ FAIL'}  {rule} {title}" + (f"（{len(hits)} 处）" if hits else ""))
    for _, where, why, line in hits[:12]: print(f"        {where}  {why}：{line}")
print(f"共扫描 Core {len(core)} 个、界面 {len(ui)} 个脚本；违规 {len(violations)} 处。")
sys.exit(1 if violations else 0)
