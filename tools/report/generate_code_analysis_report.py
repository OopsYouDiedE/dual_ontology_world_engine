from __future__ import annotations

import ast
import bisect
import fnmatch
import hashlib
import json
import os
import re
import subprocess
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "artifacts" / "code-analysis"
CODE = {".cs", ".gd", ".py", ".js", ".mjs", ".ts", ".html", ".gdshader", ".gdshaderinc"}
RESOURCE = {".tscn", ".tres", ".dv", ".csproj", ".godot", ".yaml", ".sln"}
SKIP = {".git", ".godot", ".runtime", ".venv", "node_modules", "bin", "obj", "__pycache__", "artifacts"}
MODULES = {
    "core/dv_lang": "读取 .dv 文档及 bundle，形成 JSON 声明、内容哈希；提供兼容历史脚本读取与 C# 内容导出。",
    "core/ontology": "值运算、快照、字段引用与 FactStore；也保留 DVKernel 的列表/键值表示与事件、成本、论证机制。",
    "core/cognition": "主体感知与证据投影、置信聚合、认知侵蚀、个人策略和会话心智；以角色所知约束决策。",
    "core/reasoning": "案件理论读取、带符号事实/规则、分步证明搜索、独立证明校验、历史与做局分支。",
    "core/spatial": "导航与房间图、门洞可视性、房间生命周期、交互槽预留、座位适配；不依赖 Godot 节点。",
    "core/autonomy": "需求效用计算、目标/任务规划、决策调度及 Sim4 规划；区分感知预期收益与完成后的实际效果。",
    "core/simulation": "RuntimeSession 组合 World、人物、时钟、规则、调度、空间与机制；同时保留 object 形态的 DVSimulationKernel。",
    "core/session_cli": "当前 NativeCli 标准输入宿主及会话 API、观察、审计、存档/回放与验证；包含旧 DvSession 兼容实现。",
    "scenes/main": "Main 编排标题/书本/现场，SessionBridge 连接 RuntimeSession，另外处理展示声明、Godot 值转换和书本存档。",
    "scenes/UIpanel": "标题、存档、设置、书页、现场 GUI；通过 Main/现场公开接口读取观察和提交操作。",
    "scenes/components/characters": "现场主控、动作队列、执行器、物理移动、对话动画、姿态与房间切换；回报宿主真实完成结果。",
    "scenes/components/architecture": "参数化墙/楼梯/地面、门窗与道具；C# 房间流送和剖切几何配合原生 Godot 场景。",
    "scenes/components/world": "书房舞台、书叶与桌面呈现；通过 SubViewport 与坐标映射显示 UI 纹理。",
    "scenes/components/shared": "语言、设置、声音、音乐上下文、材质控制及宿主生命周期。",
    "scenes/components/rendering": "观察环境、世界时间照明与渲染着色器。",
    "scenes/World": "地图/场域的外观与引用场景组件。",
    "mcp/game": "MCP 服务、环境准备、构建缓存、进程运行、输入、自动化与视口截图。",
    "mcp/build": "构建会话与 DV 工具链的 Python 入口。",
    "mcp/verification": "现存批次验证与 CLI 报告代码；存在不代表当前门禁有效。",
    "mcp/spatial": "空间专项审计工具。",
    "mcp/asset_pipeline": "章节绘制、角色准备和渲染等离线资产处理。",
    "tools/godot_architect": "原生编辑器插件、预制件面板、gizmo 与实时建筑叠层。",
    "tools/building": "房间组合、模型导出、资产打包和发布工具。候选与正式资源由工具命令区分。",
    "tools/web_building_editor": "浏览器房间编辑器、目录转换与页面补丁；包含随仓第三方 js-yaml。",
    "tools/studio_web": "证明 DAG 与时间线的浏览器工作台。",
    "tools/story": "故事编写、结构导出与聚焦审计；部分 C# 被 CLI 工程显式链接。",
    "tools/tuning": "角色数值提取、优化、验证与发布流程。",
    "tools/diagnostics": "开发控制台、运行诊断、回放舞台与图像/角色回执工具。",
    "tools/ci": "仓库卫生、UI/Core 隔离和既有冒烟记录脚本。此报告不运行这些游戏测试。",
    "tools/verification": "既有移动回归 Godot 场景与脚本。",
    "tools/sandbox": "现存作者沙盒与调试覆盖层，不作为本报告的运行测试入口。",
    "tools/audio": "音效合成工具。",
    "tools/report": "本报告的可复用静态分析与 HTML 生成器。",
    "assets/materials": "Godot 材质着色器：书页、场景表面、海面、天空、灯光与效果。",
    "assets/shaders": "Godot 着色器源文件；与 materials 中部分文件可能同内容，按哈希对照。",
}
KNOWN = {
    ("AutonomyDriveSolver.cs", "Score"): "校验时长、成本与需求参数，以 S(x)=x−x²/200 的凹满意度差乘需求权重，再减 seconds×timeCost。只评价 perceivedEffects，不承诺客观结果。",
    ("AutonomyDriveSolver.cs", "F_dvs_autonomy_complete"): "先推进需求衰减；仅成功完成时读取独立声明的实际效果，按 eventId 去重并限制需求范围至 0..100，提交 autonomy_needs_committed 事件。",
    ("SimulationClock.cs", "Step"): "暂停则保持时间；分别累积逻辑秒与已执行物理秒。pendingWork 阻止前进；步长受可用时间、每片上限和下一事件限制；物理导航时间不再乘加速率。",
    ("RuntimeSession.cs", "Step"): "在预算切片内推进规则、认知/推理与策略、决策调度、动作完成和空间导航；通过 SimulationClock 限制到事件边界，并向世界应用累积机制的变更。正文给出实际顺序。",
    ("RuntimeSession.cs", "SaveState"): "生成版本 4 检查点，保存世界、人物、时钟、规则、调度、槽位、在行动作、导航、策略、对话、门、冷却与动作结果。",
    ("RuntimeSession.cs", "RestoreState"): "先检查版本、集合完整性和角色/策略兼容性，再恢复子系统与执行队列；异常检查防止不兼容检查点被接纳。",
    ("SessionBridge.cs", "Open"): "读取声明快照并 CreateSession，收集打断目标和策略动作，确定观察者，创建实时审计目录并写 session_open，发送 StateChanged 后返回内容声明。",
    ("SessionBridge.cs", "Dispatch"): "按 kind 分流：simulation_pulse 调 Step，action 调 Submit，其余只产生 dispatched 包装事件。统一发送 EventsReady；dispatched=true 本身不证明玩法动作成功。",
    ("SessionBridge.cs", "Complete"): "凭执行句柄查询 Decision，并调用 RuntimeSession.HostCompleted；完成后的正式结算仍由运行会话处理。",
    ("SessionBridge.cs", "ValidatePrefabPlacement"): "先拒绝空路径；对名称含 chair/seat/bench 或带 chair_fit 的上下文使用座位高度/站位距离校验。这是有限放置校验，不是完整场景碰撞检测。",
    ("Main.gd", "submit_session_action"): "验证桥接对象有效后调用 SessionBridge.Dispatch，向调用方返回 events 数组；桥接缺失时返回空数组。",
    ("Main.gd", "_initialize_presentation"): "实例化书房、UI 层、现场 GUI、书本、标题与建筑叠层，并把标题/书本信号接到 Main 的导航函数。",
    ("Main.gd", "blocks_input"): "当前直接返回 false。该函数没有基于 HostFlowState 标志真正阻止输入，不能据字段名称推断已实现保护。",
    ("WorldRules.cs", "EvaluateSuggestion"): "只取说话者已知且无冲突的证据；分数合并信任、证据权重、成本、风险、价值和情境，再用目标本人记忆计算佐证/矛盾项。证据足够且分数达阈值才采纳，并记录原因、反馈与通信证据。",
    ("PortalPolicy.cs", "OpenWithoutLeaf"): "缺少动态门叶时读取 source_mechanism.closed_at_start；明确初始关闭则不准当作开放，其余情况返回开放。",
    ("PortalPolicy.cs", "ReachableRooms"): "以 seeds 入队做广度遍历，只沿 IntersectsView 且开放的连接扩展；prepareBehindClosedDoors 可允许预备闭门之后的房间。",
    ("PortalPolicy.cs", "ClipAperture"): "校验点和平面后，逐平面对凸门洞多边形裁剪；边跨平面时用插值补交点，输出最终可见多边形。",
    ("CaseProofChecker.cs", "CheckSteps"): "分步独立验证理论指纹、节点预算、scope、变量替换与父节点。只接受引用较早有效节点的 axiom/apply/exact，不调用证明搜索。",
    ("CaseProofChecker.cs", "CheckGoal"): "先验证证明树，再确认目标为合法 ground literal 且末节点结论正好等于目标，避免无关证明被当作目标证书。",
    ("DvCompiler.cs", "CompileToCSharp"): "将已解析文档的 Schema、Hash 与 RawJson 输出为静态 C# 类。这里导出的是内容常量，并非将任意 DV 业务代码编译为可执行算法。",
    ("DvCompiler.cs", "Compile"): "直接委托 DvDocument.Parse 解析一个 source。",
    ("DvCompiler.cs", "CompileBundle"): "直接委托 DvDocument.ParseBundle 合并并解析 sources。",
    ("ActorCognition.cs", "F_dvs_actor_can_act"): "检查 erosion_stage 不是 terminal/death，同时 incapacitated 不为真，返回角色当前能否行动。",
    ("ExecutionEngine.cs", "F_dvs_session_complete_commit"): "object 形态旧会话的完成提交：快照输入，校验动作身份、宿主时间/距离范围、证据版本、条件与空间失败，然后结算成本、记录因果事件及角色认知。不能据此认定 Godot 当前主链直接调用该函数。",
}


def module_for(relative):
    return next((prefix for prefix in sorted(MODULES, key=len, reverse=True) if relative.startswith(prefix + "/")), "/".join(relative.split("/")[:2]))


def mask_code(source):
    pattern = r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\''
    return re.sub(pattern, lambda match: re.sub(r"[^\n]", " ", match.group()), source)


def balanced_end(masked, start, opening="{", closing="}"):
    depth = 0
    for index in range(start, len(masked)):
        if masked[index] == opening:
            depth += 1
        elif masked[index] == closing:
            depth -= 1
            if depth == 0:
                return index + 1
    return len(masked)


def extract_functions(source, suffix):
    starts = [0] + [match.end() for match in re.finditer("\n", source)]
    functions = []
    if suffix == ".py":
        try:
            tree = ast.parse(source)
            def walk(node, owner=""):
                for child in ast.iter_child_nodes(node):
                    qualified = owner
                    if isinstance(child, (ast.ClassDef, ast.FunctionDef, ast.AsyncFunctionDef)):
                        qualified = f"{owner}.{child.name}".strip(".")
                    if isinstance(child, (ast.FunctionDef, ast.AsyncFunctionDef)):
                        begin = starts[child.lineno - 1] + child.col_offset
                        end = starts[child.end_lineno - 1] + child.end_col_offset
                        functions.append({"name": child.name, "owner": owner, "line": child.lineno, "end": child.end_lineno, "start": begin, "stop": end, "kind": "async def" if isinstance(child, ast.AsyncFunctionDef) else "def"})
                    walk(child, qualified)
            walk(tree)
            return functions, []
        except SyntaxError as error:
            return [], [f"Python AST 解析失败：{error.msg}，行 {error.lineno}"]
    masked = mask_code(source)
    classes = []
    if suffix == ".cs":
        for match in re.finditer(r"\b(?:class|struct|interface)\s+(\w+)[^{;]*\{", masked):
            brace = masked.index("{", match.start())
            classes.append((match.group(1), brace, balanced_end(masked, brace)))
        pattern = re.compile(r"^[ \t]*(?:(?:public|private|protected|internal|static|async|virtual|override|sealed|partial|new|unsafe|readonly|extern|abstract)\s+)+[^\n;={}]*?\b(\w+)\s*(?:<[^\n()]+>)?\s*\(", re.M)
    elif suffix == ".gd":
        pattern = re.compile(r"^[ \t]*(?:static\s+)?func\s+(\w+)\s*\(", re.M)
    elif suffix in {".gdshader", ".gdshaderinc"}:
        pattern = re.compile(r"^[ \t]*(?:void|float|int|bool|vec[234]|mat[234])\s+(\w+)\s*\(", re.M)
    elif suffix in {".js", ".mjs", ".ts", ".html"}:
        pattern = re.compile(r"\bfunction\s+(\w+)\s*\(|\b(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s*)?\(", re.M)
    else:
        return [], []
    for match in pattern.finditer(masked):
        name = next(group for group in match.groups() if group)
        parenthesis = masked.rfind("(", match.start(), match.end())
        parameter_end = balanced_end(masked, parenthesis, "(", ")")
        line = bisect.bisect_right(starts, match.start())
        if suffix == ".gd":
            declaration_line = source[starts[line - 1]:].splitlines()[0]
            indent = len(declaration_line) - len(declaration_line.lstrip())
            end = parameter_end
            for next_line in range(bisect.bisect_right(starts, parameter_end), len(starts)):
                content = source[starts[next_line]:starts[next_line + 1] if next_line + 1 < len(starts) else len(source)]
                if content.strip() and not content.lstrip().startswith("#") and len(content) - len(content.lstrip()) <= indent:
                    break
                end = starts[next_line + 1] if next_line + 1 < len(starts) else len(source)
        else:
            tail = re.search(r"\{|=>|;", masked[parameter_end:])
            if tail is None:
                continue
            body_start = parameter_end + tail.start()
            if masked[body_start] == "{":
                end = balanced_end(masked, body_start)
            else:
                end = masked.find(";", body_start)
                end = len(masked) if end < 0 else end + 1
        owner = next((item[0] for item in reversed(classes) if item[1] < match.start() < item[2]), "")
        functions.append({"name": name, "owner": owner, "line": line, "end": bisect.bisect_right(starts, max(match.start(), end - 1)), "start": match.start(), "stop": end, "kind": "函数/方法"})
    return functions, []


def analyze_function(function, source, relative, suffix):
    offset = function.pop("start")
    body = source[offset:function.pop("stop")].strip()
    masked = mask_code(body) if suffix != ".py" else body
    call_names = [match.group(1) for match in re.finditer(r"\b([A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*)\s*\(", masked)]
    exclude = {"if", "while", "for", "foreach", "switch", "catch", "using", "lock", "nameof", "typeof", "sizeof", "match", "return", "with", "print"}
    calls = list(dict.fromkeys(re.sub(r"\s", "", name) for name in call_names if name.split(".")[-1] not in exclude))
    calls = [name for name in calls if name != function["name"]]
    key = (Path(relative).name, function["name"])
    branches = len(re.findall(r"\b(?:if|elif|case|catch)\b|\?\s*[^.?]", masked))
    loops = len(re.findall(r"\b(?:for|foreach|while)\b", masked))
    guards = re.findall(r"(?:throw\s+new\s+(\w+)|raise\s+(\w+)|\b(F_dv_require)\s*\()", masked)
    reads = list(dict.fromkeys(re.findall(r'(?:Get|GetProperty|F_dv_get|F_dv_find|\.get)\s*\([^\n]{0,110}?["\']([^"\']{1,60})["\']', body)))[:30]
    returns = [line.strip()[:250] for line in body.splitlines() if re.search(r"\breturn\b|=>", line)][:10]
    operations = []
    if guards:
        operations.append(f"包含 {len(guards)} 处异常/require 校验")
    if branches:
        operations.append(f"按 {branches} 处分支选择路径")
    if loops:
        operations.append(f"通过 {loops} 处循环遍历/迭代")
    effects = []
    for pattern, label in [(r"\b(?:write_text|write_bytes|WriteAllText|WriteAllBytes|save|Save|dump|write|mkdir|CreateDirectory)\s*\(", "文件/持久化操作"), (r"\b(?:emit|EmitSignal|connect|Connect)\s*\(", "信号发送或连接"), (r"\b(?:Popen|run|Start|Process|subprocess)\b", "进程/运行操作候选"), (r"\b(?:Apply|F_dv_with|F_dv_with_many|Set|set|append|Add|Enqueue)\s*\(", "集合/状态更新候选")]:
        if re.search(pattern, masked):
            effects.append(label)
    if calls:
        operations.append("委托/调用 " + "、".join(calls[:5]))
    if returns:
        operations.append("返回/表达式出口见下方原文")
    function.update({"id": f"{relative}:{function['line']}:{function['name']}@{offset}", "file": relative, "module": module_for(relative), "signature": " ".join(body.splitlines()[:1])[:250], "source": body, "calls": calls, "branches": branches, "loops": loops, "reads": reads, "returns": returns, "effects": effects, "explanation": KNOWN.get(key) or ("；".join(operations) or "无调用、分支或显式返回；查看正文中的赋值和声明。"), "review": "人工核对机制" if key in KNOWN else "正文结构提取", "stub": bool(re.search(r"\{\s*\}\s*$", masked))})
    return function


def git_value(arguments):
    try:
        return subprocess.check_output(["git", *arguments], cwd=ROOT, text=True, encoding="utf-8", stderr=subprocess.DEVNULL).strip()
    except (OSError, subprocess.CalledProcessError):
        return "不可用"


def partition_modules(records, resources):
    catalog = json.loads(Path(__file__).with_name("module_boundaries.json").read_text(encoding="utf-8"))
    identifiers = [module["id"] for module in catalog["modules"]]
    if len(set(identifiers)) != len(identifiers):
        raise ValueError("Duplicate partition identifiers")
    module_map = {module["id"]: module for module in catalog["modules"]}
    for module in catalog["modules"]:
        if any(target not in module_map for target in module["dependencies"]):
            raise ValueError(f"Unknown module dependency: {module['id']}")
        module["files"] = []
        module["resources"] = []
        module["functionCount"] = 0
        module["lines"] = 0
    visited = set()
    def visit(identifier, active):
        if identifier in active:
            raise ValueError(f"Partition dependency cycle: {identifier}")
        if identifier in visited:
            return
        for target in module_map[identifier]["dependencies"]:
            visit(target, active | {identifier})
        visited.add(identifier)
    for identifier in identifiers:
        visit(identifier, set())
    unassigned = []
    for record in records + resources:
        matches = []
        for module in catalog["modules"]:
            for pattern in module["patterns"]:
                if fnmatch.fnmatchcase(record["path"], pattern):
                    matches.append((len(pattern.split("*", 1)[0]), module["id"]))
        if not matches:
            unassigned.append(record["path"])
            continue
        maximum = max(length for length, identifier in matches)
        owners = {identifier for length, identifier in matches if length == maximum}
        if len(owners) != 1:
            raise ValueError(f"Ambiguous module ownership: {record['path']}: {owners}")
        identifier = owners.pop()
        record["partition"] = identifier
        module = module_map[identifier]
        is_code = "source" in record
        module["files" if is_code else "resources"].append(record["path"])
        if is_code:
            module["functionCount"] += len(record["functions"])
            module["lines"] += record["loc"]
    if unassigned:
        raise ValueError("Missing module ownership: " + ", ".join(unassigned))
    catalog["validation"] = {"modules": len(identifiers), "codeFiles": len(records), "resourceFiles": len(resources), "unassigned": unassigned, "acyclic": True, "gameTestsRun": False}
    return catalog


def functional_systems():
    design = json.loads(Path(__file__).with_name("functional_systems.json").read_text(encoding="utf-8"))
    system_ids = [system["id"] for system in design["systems"]]
    shared_ids = [shared["id"] for shared in design["shared"]]
    if len(set(system_ids + shared_ids)) != len(system_ids + shared_ids):
        raise ValueError("Functional system identifiers must be unique")
    known_ids = set(system_ids + shared_ids)
    known_sources = {source["id"] for source in design["sources"]}
    secondary_ids = []
    for system in design["systems"]:
        if any(target not in known_ids for target in system["dependencies"]):
            raise ValueError(f"Unknown functional dependency: {system['id']}")
        if any(reference not in known_sources for reference in system["references"]):
            raise ValueError(f"Unknown official source: {system['id']}")
        for reference in system["code"]:
            if not (ROOT / reference).exists():
                raise ValueError(f"Missing functional code mapping: {reference}")
        for secondary in system["subsystems"]:
            if not secondary["id"].startswith(system["id"] + ".") or not secondary["accept"]:
                raise ValueError(f"Invalid secondary contract: {secondary['id']}")
            secondary_ids.append(secondary["id"])
    if len(set(secondary_ids)) != len(secondary_ids):
        raise ValueError("Duplicate secondary system identifiers")
    for capability in design["engine"]:
        if any(owner not in known_ids for owner in capability["owner"]) or any(reference not in known_sources for reference in capability["references"]):
            raise ValueError(f"Invalid Godot capability mapping: {capability['name']}")
    for source in design["sources"]:
        if not source["url"].startswith("https://docs.godotengine.org/en/stable/"):
            raise ValueError(f"Unexpected reference origin: {source['url']}")
    design["validation"] = {"ui": sum(system["group"] == "UI" for system in design["systems"]), "scene": sum(system["group"] == "场景" for system in design["systems"]), "secondary": len(secondary_ids), "shared": len(shared_ids), "sources": len(known_sources), "gameTestsRun": False}
    return design


def core_systems():
    design = json.loads(Path(__file__).with_name("core_systems.json").read_text(encoding="utf-8"))
    systems = {system["id"]: system for system in design["systems"]}
    if len(systems) != len(design["systems"]):
        raise ValueError("Duplicate Core system identifier")
    flags = [system["flag"] for system in systems.values() if system["group"] == "特色扩展"]
    if len(set(flags)) != len(flags):
        raise ValueError("Duplicate Core capability flag")
    secondary_ids = []
    visited, visiting = set(), set()
    def visit(identifier):
        if identifier in visiting:
            raise ValueError("Core dependency cycle: " + identifier)
        if identifier in visited:
            return
        visiting.add(identifier)
        for dependency in systems[identifier]["dependencies"]:
            if dependency not in systems:
                raise ValueError("Unknown Core dependency: " + dependency)
            if systems[identifier]["group"] == "生活基础" and systems[dependency]["group"] != "生活基础":
                raise ValueError("Base depends on extension: " + identifier)
            visit(dependency)
        visiting.remove(identifier)
        visited.add(identifier)
    for identifier, system in systems.items():
        visit(identifier)
        for code in system["code"]:
            if not (ROOT / code).is_file():
                raise ValueError("Missing Core mapping: " + code)
        for index, subsystem in enumerate(system["subsystems"], 1):
            subsystem["id"] = f"{identifier}.{index}"
            if not subsystem["accept"]:
                raise ValueError("Missing Core acceptance condition")
            secondary_ids.append(subsystem["id"])
    profile_ids = [profile["id"] for profile in design["profiles"]]
    if len(set(profile_ids)) != len(profile_ids):
        raise ValueError("Duplicate Core profile")
    for profile in design["profiles"]:
        enabled = set(profile["enabled"])
        if len(enabled) != len(profile["enabled"]):
            raise ValueError("Duplicate profile capability")
        for identifier in enabled:
            if identifier not in systems or systems[identifier]["group"] != "特色扩展":
                raise ValueError("Invalid profile capability: " + identifier)
            if any(dependency not in enabled for dependency in systems[identifier]["dependencies"] if systems[dependency]["group"] == "特色扩展"):
                raise ValueError("Unresolved profile dependencies: " + profile["id"])
    design["validation"] = {"base": sum(system["group"] == "生活基础" for system in systems.values()), "extensions": len(flags), "secondary": len(secondary_ids), "profiles": len(profile_ids), "acyclic": True, "baseIndependent": True, "gameTestsRun": False, "switchesImplemented": False}
    return design


def collect():
    file_paths = []
    for base, directories, names in os.walk(ROOT, followlinks=False):
        directories[:] = [name for name in directories if name not in SKIP and not (Path(base) / name).is_symlink()]
        file_paths.extend(Path(base) / name for name in names if Path(name).suffix.lower() in CODE | RESOURCE)
    records, functions, resources, projects, warnings = [], [], [], [], []
    resource_edges, signals = [], []
    for path in sorted(file_paths):
        relative = path.relative_to(ROOT).as_posix()
        source = path.read_text(encoding="utf-8-sig", errors="replace")
        suffix = path.suffix.lower()
        references = list(dict.fromkeys(re.findall(r'res://([^"\'\s)]+)', source)))
        resource_edges.extend({"from": relative, "to": target, "kind": "res 路径引用"} for target in references)
        if suffix in RESOURCE:
            resources.append({"path": relative, "ext": suffix[1:], "lines": len(source.splitlines()), "references": references, "hash": hashlib.sha256(source.encode()).hexdigest()})
            if suffix == ".csproj":
                tree = ET.fromstring(source)
                projects.append({"path": relative, "dependencies": [(path.parent / element.attrib["Include"]).resolve().relative_to(ROOT).as_posix() for element in tree.iter("ProjectReference")], "compile": [element.attrib for element in tree.iter("Compile")], "framework": tree.findtext(".//TargetFramework")})
            continue
        extracted, issues = extract_functions(source, suffix)
        function_rows = [analyze_function(item, source, relative, suffix) for item in extracted]
        warnings.extend({"file": relative, "message": issue} for issue in issues)
        imports = re.findall(r"^\s*(?:using(?:\s+static)?|from|import)\s+([\w.]+)", source, re.M)
        function_ids = [item["id"] for item in function_rows]
        functions.extend(function_rows)
        for line, content in enumerate(source.splitlines(), 1):
            if re.search(r"\.connect\s*\(|\.Connect\s*\(|EmitSignal\s*\(|\.emit\s*\(", content):
                signals.append({"file": relative, "line": line, "source": content.strip()[:500]})
        records.append({"path": relative, "module": module_for(relative), "ext": suffix[1:], "loc": len(source.splitlines()), "hash": hashlib.sha256(source.encode()).hexdigest(), "functions": function_ids, "imports": sorted(set(imports)), "references": references, "role": MODULES.get(module_for(relative), "项目支持代码，见函数详情与源码。"), "source": source, "vendor": "vendor/" in relative or relative.endswith("js-yaml.min.js"), "annotations": re.findall(r"@(?:mechanism|contracts):[^\n]+", source)})
    names = defaultdict(list)
    for function in functions:
        names[function["name"]].append(function)
    for function in functions:
        links = []
        for call in function["calls"]:
            called_name = call.split(".")[-1]
            candidates = names.get(called_name, [])
            qualified_owner = call.split(".")[-2] if "." in call else ""
            owner_candidates = [item for item in candidates if qualified_owner and item["owner"] == qualified_owner]
            local = [item for item in candidates if item["file"] == function["file"] and item["owner"] == function["owner"]]
            chosen = owner_candidates or local or candidates
            links.append({"call": call, "candidates": [item["id"] for item in chosen], "basis": "类型名匹配候选" if owner_candidates else "同文件/同类型候选" if local else "跨文件同名候选" if candidates else "外部 API / 动态或未解析"})
        function["links"] = links
    modules = []
    for name in sorted({record["module"] for record in records}):
        members = [record for record in records if record["module"] == name]
        modules.append({"name": name, "role": MODULES.get(name, "项目支持代码"), "files": len(members), "loc": sum(item["loc"] for item in members), "functions": sum(len(item["functions"]) for item in members)})
    hashes = defaultdict(list)
    for record in records:
        hashes[record["hash"]].append(record["path"])
    duplicates = [members for members in hashes.values() if len(members) > 1]
    tracked = set(git_value(["ls-files"]).splitlines())
    scanned = {record["path"] for record in records}
    expected = {name for name in tracked if Path(name).suffix.lower() in CODE and not any(part in SKIP for part in Path(name).parts)}
    partitions = partition_modules(records, resources)
    return {"root": str(ROOT), "generated": datetime.now(ZoneInfo("Asia/Taipei")).isoformat(timespec="seconds"), "commit": git_value(["rev-parse", "HEAD"]), "dirty": git_value(["status", "--short"]), "files": records, "functions": functions, "resources": resources, "resourceEdges": resource_edges, "signals": signals, "projects": projects, "modules": modules, "partitions": partitions, "duplicates": duplicates, "warnings": warnings, "coverage": {"trackedCode": len(expected), "missingTracked": sorted(expected - scanned), "workingCode": len(records), "extensions": dict(Counter(record["ext"] for record in records)), "reviewedFunctions": sum(item["review"] == "人工核对机制" for item in functions), "anonymousNote": "Python 使用 AST；其他语言使用词法屏蔽及括号/缩进边界。匿名 lambda、C# 局部函数/属性存取器、JS 对象方法、字符串动态调用未保证全部识别。完整文件源码保留以供核对。"}}


def main():
    data = collect()
    data["functional"] = functional_systems()
    data["coreDesign"] = core_systems()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    template = Path(__file__).with_name("report.html").read_text(encoding="utf-8")
    payload = json.dumps(data, ensure_ascii=False, separators=(",", ":"))
    encoded = payload.replace("<", "\\u003c").replace("\u2028", "\\u2028").replace("\u2029", "\\u2029")
    destination = OUTPUT / "index.html"
    destination.write_text(template.replace("__REPORT_DATA__", encoded), encoding="utf-8")
    (OUTPUT / "analysis.json").write_text(payload, encoding="utf-8")
    (OUTPUT / "module-analysis.json").write_text(json.dumps(data["partitions"], ensure_ascii=False, indent=2), encoding="utf-8")
    (OUTPUT / "system-design.json").write_text(json.dumps(data["functional"], ensure_ascii=False, indent=2), encoding="utf-8")
    (OUTPUT / "core-design.json").write_text(json.dumps(data["coreDesign"], ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"output": str(destination), "files": len(data["files"]), "functions": len(data["functions"]), "resources": len(data["resources"]), "coverage": data["coverage"], "warnings": data["warnings"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
