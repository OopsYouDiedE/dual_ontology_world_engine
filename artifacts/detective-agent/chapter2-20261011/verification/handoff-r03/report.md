# handoff-r03 独立机制验证

验证日期：2026-10-11。验证分支：`codex/virtual-detective-chapter2-20261011`。

本轮使用独立验证 Agent。仅读取适用 AGENTS、机制源码和中性检查；没有读取项目设计文件、正式 `.story`、历史游玩轨迹或三份全程记录。正式剧本生成物的一致性仅通过编译工具检查。本代理没有运行 FullTest，没有修改生产实现或 Core，没有提交或推送；开始时已有工作区改动保留。

结论：宿主构建、编译一致性、中性知识合同、两章流程回归与跨进程保存检查全部通过。所有 Godot 检查串行执行，均有完整结束记录、退出 0，无引擎错误，无重试。

## 实现核对与新增中性合同

- `Chapter1SessionFlows.cs` 的 `Ending` 在写入结束状态前调用 `_knowledge.ClearQuestions(_content.GameMinutes)`。
- `Chapter2InteractionSession.cs` 的 `Run` 在 `RunSlot` 完成后读取侦探实际空间；当前地图声明空间不同时，先调用 `Scene(resolvedPlace, output)`，再调用 `Lines(feed, output)`。输出场景及回执的顺序符合这一机制目标。
- 仅在 `Shared/check/KnowledgeCheck/Program.cs` 增加六项中性生命周期检查，使用 `p/q` 与通用 actor/material/claim/question 词项。检查清除当前问题标记、保留材料及可出示性、保留命题和完整证明结果、重复清理幂等，以及同一个问题后来再次实际呈现时能重新成为当前问题。

## 本轮结果

| 检查 | 结果 | 本轮证据 |
| --- | --- | --- |
| Godot 宿主构建 | 通过，0 Warning(s)，0 Error(s) | [host-build-corrected.stdout.log](host-build-corrected.stdout.log) |
| `compile_story.py --check` | 通过，7 outputs current | [compile-story-check.stdout.log](compile-story-check.stdout.log) |
| 知识中性编译合同与夹具准备 | 11 tests，OK；新目录 `tmp/verify-handoff-20261011` | [knowledge-fixture-prepare.stderr.log](knowledge-fixture-prepare.stderr.log) |
| KnowledgeCheck Release 构建 | 通过，0 Warning(s)，0 Error(s) | [knowledge-build-corrected.stdout.log](knowledge-build-corrected.stdout.log) |
| KnowledgeCheck 隔离夹具运行 | `TOTAL 79 checks`，退出 0 | [knowledge-contract-corrected.stdout.log](knowledge-contract-corrected.stdout.log) |
| 第二章 flow2 | `TOTAL 109 checks, 109 passed, 0 failed` | [原生 flow2.stdout.log](verify-native/initial/flow2.stdout.log) |
| 第一章 flow | `TOTAL 136 checks, 0 failed` | [原生 flow.stdout.log](verify-native/initial/flow.stdout.log) |
| 跨进程 save | WRITE 22 checks，READ 1 check，均 0 failed；`SESSION SAVE HEADLESS COMPLETE: 0` | [原生 save.stdout.log](verify-native/initial/save.stdout.log) |

宿主构建使用 `dotnet build godot_csharp/DualOntologyWorldEngine.sln '-m:1' --disable-build-servers`。知识检查按 AGENTS 先准备隔离夹具、构建 Release，随后用 `--no-build -- --fixture tmp/verify-handoff-20261011` 运行。Godot 检查均通过 `python tools/verify.py --only <flow2/flow/save> --godot <控制台路径>` 分别执行；控制台为 Godot 4.6.1 .NET。每次命令、退出码、开始结束时刻及原始通道 SHA-256 在对应 `*.result.json` 中记录。

## 失败记录与修正

两次验证操作错误均保留原始日志，未当作生产机制失败，也未覆盖为通过日志。

1. 首次宿主构建时，PowerShell 将未引用的 `-m:1` 传成 `-m:` 和 `1`，MSBuild 在构建源码前拒绝参数。命令参数修正后构建通过。证据：[host-build.result.json](host-build.result.json)、[host-build.stdout.log](host-build.stdout.log)。
2. 首次运行新增中性合同时，记录材料标签的测试代码尝试将 `Tag` 键字典直接序列化，导致 `NotSupportedException`；此时尚未调用该新增检查的 `ClearQuestions`，此前原有合同与 full/missing 夹具已通过。测试改为逐键值直接比较，保留材料标签完全相同的断言；重新构建和运行后 79 项通过。证据：[knowledge-contract.stderr.log](knowledge-contract.stderr.log)、[knowledge-contract-corrected.stdout.log](knowledge-contract-corrected.stdout.log)。

没有观测到真实断言失败或生产引擎错误。Godot 三项检查均没有缺失汇总，因而没有触发重试或退出崩溃兼容判定。

## 证据与退出确认

- 当前目录封存 32 份原始 `.log`，局部 `.gitattributes` 使用 `-text -diff`；各通道直接以字节写入。
- `verify-native/initial/` 的六份 flow2/flow/save stdout/stderr 与统一回归入口产生的原文件逐字节相同。原文件路径、副本路径和一致性结果见 [native-log-retention.json](native-log-retention.json)。
- 中性夹具的 full/missing 源及编译副本保存在 `neutral-fixture/`；没有写正式生成物。
- [raw-log-sha256.json](raw-log-sha256.json) 记录 32 份原始日志指纹；[verified-source-sha256.json](verified-source-sha256.json) 记录两处生产改动、认知机制、知识测试及两个检查工具的六份源码指纹。
- 本轮 Godot 检查结束后，进程快照只剩既有用户编辑器 PID 23084，未对其操作。证据：[godot-processes-final.stdout.log](godot-processes-final.stdout.log)。已经向主代理确认本轮检查全部退出，后续报告封存没有再启动 Godot。

## 验收范围与未验证项

| 分类 | 结论 |
| --- | --- |
| 构建 | 本轮宿主及 KnowledgeCheck 构建通过。 |
| 机制 | 上表所列自动回归和中性合同通过；第二章新增场景同步顺序已作源码核对。 |
| 人物行为合理性 | 未验证；本代理没有读取设计或正式剧情，不作内容判断。 |
| 游玩体验 | 未验证；本代理没有运行真实盲玩或 FullTest，不评价通关、有效游玩时长或体验质量。 |
| 一级剧情与引擎验收 | 未验证；没有生成或核对本轮三份全程记录。 |
| 二级 UI 专项验收 | 未验证；没有执行截图专项或视觉检查。 |
| 三级游戏本体验收 | 未验证。 |

流程回归含调试推进和夹具操作，保存检查只验证空闲切点的保存/恢复；它们均属于机制开发证据，不属于通关或盲玩证据。该报告不包含主代理后续 r03 FullTest 的结论。
