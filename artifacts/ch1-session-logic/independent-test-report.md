# 第一章会话修复：独立自动化验证

日期：2026-10-08。分支：`codex/ch1-session`。本轮由全新测试 Agent 执行，未读取设计文档，未修改任何受版本控制的实现文件，未提交。验收范围来自主 Agent 指定的五项会话行为。

结论：六个独立 `GameSession` 自动化场景全部通过。剧本生成物检查、Core 24 项回归、本地还原后的宿主构建及 DetectiveSimulation 构建通过。常规统一回归的宿主步骤仍失败；正式 UI 探针未完成。不得据此宣布整体游戏或 UI 验收通过。

## 运行方法与证据

按照要求复制提供的 `scratch/audit/pt/Program.cs` 到当前工作树 `scratch/session-verification/`，仅修改忽略目录中的夹具并创建引用当前 `GameSession.csproj` 的临时项目。每个场景从新的进程、新的游戏会话开始，输入通过正式 `GameSession.Dispatch`，自动回复呈现完成、选择及空间到场。反射只读取当前章节以核验进度，不向正式会话注入答案或剧情状态。

夹具不再跳过同一次输出中被后卡片取代的卡片；一条 Dispatch 中出现多张卡片直接失败，因此重复 reopen 或过期 presentation ID 不会被夹具隐藏。唯一状态注入是 V 场景末尾独立 `AwakeningContentSession` 的 `refusal_seen` 单元检查，和前面的正式连续会话分开。

这是自动化验证，代报了空间到场并使用预选操作。没有执行一级三份记录验收、二级画面验收或三级盲玩；没有实测几何、碰撞、遮挡、布局或游玩时长。人物行为合理性与游玩体验未验证。

| 场景 | 结果与覆盖 | 本轮证据 |
| --- | --- | --- |
| R | 在 roam 检视 E12、E11a 不播 `v6_doubt`；相遇后仍不播，E1 试验解锁重演后完整播一次；重复物证和 tick 不重播 | `independent-semantic-R.stdout.log`、`independent-R-raw.jsonl`、`independent-R-final.json` |
| A | 第一问用已提供的 `me/E3` 通过；第二问取消后保留 step=2；重新进入仍为第二问；`answer_open` 只在首次进入出现；未输出 `answer_ok1.002` | `independent-semantic-A.stdout.log`、`independent-A-raw.jsonl`、`independent-A-final.json` |
| F | 四组非法输入：伪造 say、伪造 show、已定义但未提供的 `V3_0`、未提供的 E9，均不能推进第一问；随后已提供的 `me/E3` 可以推进至 step=2 | `independent-semantic-F.stdout.log`、`independent-F-raw.jsonl`、`independent-F-final.json` |
| X | E1 试验实际触发 `recall_ask_detective` 与 `recall_detective`；只产生初始卡和一张最终重开卡；回复两张卡都无过期 ID 错误 | `independent-semantic-X-fixed.stdout.log`、`independent-X-raw.jsonl`、`independent-X-final.json` |
| H | E5 help 实际触发 `recall_touch_su` 与后续回忆；只产生初始卡和一张最终重开卡；所有呈现回复无过期 ID 错误 | `independent-semantic-H-final.stdout.log`、`independent-H-raw.jsonl`、`independent-H-final.json` |
| V | 五个输入完成实际非 skip 重演；当前与上次变量数组都存六项且 V6=-1；V6 关闭时无 V6/trust 陈述；E12+E11a 打开第六问后仍未选，已有物证可以合法提供 V6 陈述；独立 refusal_open 且无第六问物证时，V6=-1 不生成 V6/trust | `independent-semantic-V.stdout.log`、`independent-V-raw.jsonl`、`independent-V-closed.json`、`independent-V-opened.json` |

X/H 初次夹具错误及日志保留：X 用不存在的 `flash_` 行 ID 前缀判断，H 首先误选无回忆的 E4，再错误地期待 `recall_ask_su` 而实际为 `recall_touch_su`。修正只涉及 scratch 夹具。失败日志分别为 `independent-semantic-X.*.log`、`independent-semantic-H.*.log`、`independent-semantic-H-fixed.*.log`，失败输入输出封存在 `independent-X-initial-raw.jsonl`、`independent-H-initial-raw.jsonl`、`independent-H-prefix-failed-raw.jsonl`。最终 X/H 均通过。

## 构建与回归

| 命令 | 结果 |
| --- | --- |
| `python godot_csharp/tools/compile_story.py --check` | 首次与最终检查均通过：7 个生成物一致。`independent-story-check-final.*` |
| `dotnet build godot_csharp/DualOntologyWorldEngine.sln` | 首次失败，exit=1，0 warning/0 error；未给出可归因的错误。`independent-host-build.*` |
| 同一 solution `--no-restore` | 显示缺失 `.godot/mono/temp/obj/project.assets.json`。`independent-host-build-no-restore.*` |
| `dotnet restore godot_csharp/DualOntologyWorldEngine.sln --source C:/Users/Administrator/.nuget/packages --ignore-failed-sources --disable-parallel -m:1 -v normal` | 使用只读现有本地包缓存成功还原，输出在当前工作树。`independent-host-restore-local.*` |
| `dotnet build godot_csharp/DualOntologyWorldEngine.sln --no-restore -m:1` | 通过，0 error；1 个既有 Chapter2 `_confrontIndex` 未使用警告。`independent-host-build-restored.*` |
| `python tools/verify.py --only core scope host` | 首次、还原后重跑均为 core/scope PASS、host FAIL exit=1 0 diagnostics。`independent-verify.*`、`independent-verify-restored.*` |
| 上述 verify，进程级 `DOTNET_PROCESSOR_COUNT=1` | core/scope PASS（24 checks），host 仍 FAIL exit=1 0 diagnostics。无持久化配置变更。`independent-verify-one-processor.*` |
| DetectiveSimulation 本地源、`-m:1` 构建 | 通过，0 warning/0 error；移除 BeginRest 引用未造成编译错误。`independent-detective-build.*` |

统一回归的原始分通道日志另外位于 `artifacts/project-audit/verify-logs/20261008-202108-051710-17132/`、`20261008-202418-271334-26856/`、`20261008-202738-725849-34000/`。未改动验证工具或 NuGet 配置。只能确认替代构建已通过，不能把 exact verify 的失败改写为通过。

可选 StoryPacketCheck：本地源、`-m:1` 构建通过。常规 run 无进展后终止；直接运行已构建 DLL 的有界尝试先通过 `opening accepts investigation without a rest choice`，之后在 `Program.cs:103` 的 `recall_offer_su` 旧菜单检查失败（`interaction reaches a real story menu`，检查函数位置 `Program.cs:21`）。45 秒结束前已有异常堆栈，包装器记录 exit=124。证据为 `independent-story-packet-build.*`、`independent-story-packet-direct.*`。未修复这个范围外的旧断言。

## UI 探针限制

正式 `ReplayManuscript.tscn` 的有界 headless 探针先因工作树外 `user://logs` 写入失败发生 Godot 崩溃。改为工作树内 `--log-file` 后成功进入脚本，但当前工作树缺失导入后的 `ZhaohuaTypeWriter.ttf` fontdata，使 `UITheme.tres` 与 Paper 依赖加载失败。随后 `Paper.story_text` 调用失败，无法构建完整选项按钮；探针最终超时 exit=124。

损坏依赖下输出了四个初步断言：五个可见问题截断第六项、五题完成可运行、展开后 V6 仍 -1、未选 V6 禁用运行。但由于存在 script/engine 错误，这些输出不计为干净的 UI 通过。第六问按钮状态、实际选项交互、非法索引归一化的完整探针以及画面均未验证。停止后续环境重试。

证据：`independent-replay-ui-probe-bounded.*`、`independent-replay-ui-probe-local-log.*`、`independent-replay-ui-godot.log`。探针没有生成完整结果 JSON。源码归一化检查位置：`ReplayManuscript.gd:102` 与 `:108`。

## 完整性与实现定位

`git diff --check` 通过。当前 18 个修改的受版本控制文本文件均为 LF，无混合 LF/CRLF；完整计数及 Git 属性保存在 `independent-source-integrity.json`。没有可独立复原的修改前工作树字节快照，因此这里只证明当前格式及属性，不宣称通过基线工作树字节比对。

生成对白中保留 478 个行 ID、未新增行 ID。对白行删除仅为 `answer_ok1.002`、`ending_rest.001` 至 `.006`、`rest_confirm.001` 和 `.002`；本地化 CSV 同时删除对应两个选择 ID `rest_confirm.003/.004`，共 11 个 authored rows。所有保留的本地化行内容未改变。

相关位置：`Chapter1Script.cs:288`（RevealV6 门槛）、`:360`（Statements）；`Chapter1Session.cs:568`（五项归一化到 V6=-1）；`Chapter1SessionFlows.cs:105` 与 `:388`（flash 不内层重开卡）、`:402`（延迟 V6 演出）、`:567` 与 `:600`（按当前 offered IDs 校验答辩）、`:645`（仅模拟时取第六项兜底值）。

未验证：真实窗口 UI、资源导入、全部 flow/flow2 回归、一级/二级/三级验收、设计符合性、人物行为合理性和真实游玩体验。
