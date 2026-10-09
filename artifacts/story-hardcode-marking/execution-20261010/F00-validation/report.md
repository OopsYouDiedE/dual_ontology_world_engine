# F00 基线自动回归报告

本轮是全新独立 Agent 的自动回归，不属于一级、二级或三级验收。本 Agent 未阅读项目设计文件、剧本正文、谜底或黄金预期源码、历史试玩记录；正式工具正常读取各自输入。本 Agent 只检查工作流、文件写入接口、环境和本轮命令结果，没有修改生产代码、测试、AGENTS.md，未进行 UI 或盲玩。

## 基线与环境

- HEAD：`7489db56a714724eb7cb735958dd879dc662d238`。
- 分支：`codex/solution-review-audit-20261010`。
- Windows 10.0.26200，win-x64。
- .NET SDK 8.0.400，MSBuild 17.11.3+0c8610977，运行时 8.0.8。
- Python 3.10.6。
- Godot：`4.6.1.stable.mono.official.14d19694e`。
- 正式控制台：`C:/Users/iii/Desktop/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64_console.exe`。

开始时存在四个 tracked 修改（见 `tracked-diff-before.stdout.log`），因此结果针对该 HEAD 上被保留的实际工作区，不声称纯净检出的结果：

1. `artifacts/story-hardcode-marking/solution-review.md`
2. `godot_csharp/scenes/Awakening/Script/generated_lines_zh.scene.translation`
3. `godot_csharp/scenes/Awakening/Script/generated_lines_zh.speaker.translation`
4. `godot_csharp/scenes/Awakening/Script/generated_lines_zh.zh.translation`

## 检查结果

| 命令/检查 | 结果 | 墙钟耗时 | 证据 |
| --- | --- | --- | --- |
| `dotnet build csharp/Core.sln` | PASS，0 警告、0 错误 | 3.734 秒 | `core-build.stdout.log` / `core-build.stderr.log` |
| `dotnet build godot_csharp/DualOntologyWorldEngine.sln` | PASS，0 警告、0 错误 | 8.875 秒 | `host-build.stdout.log` / `host-build.stderr.log` |
| `python godot_csharp/tools/compile_story.py --check` | PASS，7 个生成物与源一致 | 0.656 秒 | `story-check.stdout.log` / `story-check.stderr.log` |
| `python tools/verify.py --skip gallery --keep-going` | FAIL（exit 1），环境入口未发现可运行的 .NET 控制台 | 5.812 秒 | `verify.stdout.log` / `verify.stderr.log` |
| 显式 `--godot` 的同一正式入口 | FAIL（exit 1），core/scope/host PASS，flow/flow2 FAIL，gallery SKIP | 437.562 秒 | `verify-console.stdout.log` / `verify-console.stderr.log` |

首轮统一回归的 core、scope（24 checks）、host 全部通过。flow/flow2 因 Godot 发现失败未启动，gallery 按主代理明确授权跳过。保留失败轮次，不修改工具实现；仅指定同一安装目录中的正式控制台重跑。

显式控制台轮结果如下。所有步骤均在正式超时以内结束，没有人工中止或超时：

| 回归步骤 | 结果 | 正式入口报告耗时 | 检查数 |
| --- | --- | --- | --- |
| core | PASS，0 警告、0 错误 | 2 秒 | 构建 |
| scope | PASS | 1 秒 | 24 项全部通过 |
| host | PASS，0 警告、0 错误 | 3 秒 | 构建 |
| flow | FAIL，exit 1 | 323 秒 | 134 项，110 通过、24 失败 |
| flow2 | FAIL，exit 1 | 109 秒；流程自身记录 108576 ms | 103 项，100 通过、3 失败 |
| gallery | SKIP | 未执行 | 按授权范围跳过 |

必需检查及环境修正后的重跑合计墙钟耗时为 456.639 秒；上述流程步骤耗时来自正式入口，按整数秒取整。逐命令开始 UTC 时间、精确墙钟耗时与 exit code 见 `results.json`。

### 在本轮基线观察到的存量失败

- 第一章 flow 的 24 个失败集中在人菜单协助及完成回执、相关后续显示与章末状态。第一处 FAIL 在本轮原始 `flow.stdout.log` 第 63 行，总结在第 160 行。没有 SCRIPT ERROR、Parse Error、ERROR: 或资源泄漏标记；stderr 为 0 字节。
- 第二章 flow2 的 3 个失败涉及正式剧本开场/首课/自由行动台词、首个可操作地点和推进至次日傍晚。另有两条 `ERROR:`：`交互地点已不在当前空间。` 与 `无效或不可用行动：escort`。回溯均由 `UISystem.gd:139` 呈现，经 `Main.gd` 和正式检查夹具触发；具体堆栈保存在本轮 `flow2.stderr.log`。
- 两个流程非零退出已经足以判统一回归失败；第二章引擎错误也符合正式入口的引擎错误扫描规则。不据这些检查标签自行推断实现原因或设计结论，不删失败测试，不修改黄金预期。
- Godot 自动发现首轮失败属于本机入口配置问题；显式控制台修正后已实际执行两个流程。首轮失败日志仍完整保留。

## 运行边界与等待

先阅读正式 `tools/verify.py`，确认默认失败停止、`--keep-going` 行为及内部超时：core/scope/host/flow 各 600 秒，flow2 900 秒；gallery 本轮跳过。外层捕获脚本只启动正式命令、保存 stdout/stderr 原始字节和耗时；没有替换、过滤或重写测试实现。整体外层兜底超时为 3600 秒。

主代理批准新增唯一时戳的 `artifacts/project-audit/verify-logs/<本轮目录>/`，不得覆盖历史日志，并要求复制到本 F00 目录、记录 SHA256；批准正式 flow/flow2 的 headless 运行及正常检查产物。未执行 gallery。

正式入口的两轮新增日志目录分别是 `20261010-002705-352710-24260` 和 `20261010-002902-483997-21652`。共 16 个 stdout/stderr 通道日志复制到 `verify-raw/`，全部逐字节相同。`verify-log-provenance.json` 记录原路径、复制路径、字节数、双方 SHA256 和相等结论；`raw-log-sha256.txt` 覆盖 F00 内所有原始 `.log`。本目录 `.gitattributes` 为 `*.log -text -diff`，实际检查属性已确认 text/diff 为 unset。

## 最终结论

- 构建：已通过。
- 机制：Core Scope 已通过 24 项；Godot 两个流程均失败，本轮自动回归不通过。
- 人物行为合理性：未验证；未读取设计，留给主代理核对。
- 游玩体验：未验证。
- 一级：未执行；二级：未执行；三级：未执行。
- gallery：按本轮明确范围跳过，不记通过。
- 最终 HEAD 与开始时相同。`tracked-diff-before.stdout.log` 和 `tracked-diff-after-console.stdout.log` 的路径/状态内容完全相同，没有新增 tracked 修改路径；既有四处 tracked 修改继续保留。该比较只证明变更路径集合相同，没有初始逐文件内容哈希，不能声称既有修改的字节完全不变。
- 本 Agent 的有意写入仅为 F00 证据文件、正式入口新建的唯一日志目录，以及构建/Godot 正常产物；未改生产代码、测试或 AGENTS.md。由主代理汇总提交，不自行提交共享工作区。
