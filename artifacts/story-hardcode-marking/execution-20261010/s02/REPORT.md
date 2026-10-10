# S02 执行报告

本轮只处理语义结果与表达分离。未读取 `docs/design-standard.md`，未修改 `scenes/UI/` 或布局/样式资源，未提交。

## 改动与原因

- Core 的 `EvidenceWeight`、`ReasonBasis`、`SpeechReason` 返回来源、采信因子、理由代码及参数；说话人使用投影引用。证据权重、信任公式、言语评分与立场计算不变。
- 新增共享 `ResponseCatalog`，从编译后的 `表达` 声明按条件与优先级筛选回应。读取说话人的认知，分别传递说话人与听者；无适用声明返回明确的 `none`。
- 第一章的来源/理由模板、言语行为到回应键的映射、等待及警铃心声条件移入 `表达.story`。仍用既有 `SpeechPolicy` 选择行为。
- 删除 `Chapter2Content.PlayerText` 的全文称呼替换。身份占位符及实际 `VoiceName` 按说话人所知身份解析；普通正文不会被改写。
- 第二章的答疑、个体立场、转述成见、人物印象、物证描述、守夜报告、帮助例句和章末文案移入 `09_表达.story`。答疑资格读取听者已学课程；描述条件读取调查者已知投影。
- 守夜身影合并使用时间段、身量、方向等结构化字段，不替换生成句子中的“一个”；声音保留既有来源消息。同一议案比较改用稳定 ID。
- 章末总结只读既有状态，不登记事件或推进时间。开门表达删除没有执行回执支持的排队、登记、签名与取物叙述，仅保留原有已结算开门句；原句型仍逐字迁移保留在剧本中。
- 两个模块的 AGENTS.md 同步了表达接口、编译数据与检查入口。

移除本任务的 18 个标记：CSH-009；AWK-012、016、062；CH2-015、023、026、044、046、048、057、058、059、060、066、072、080、081。源码扫描无剩余命中。CH2-066 原判据已由前批结构化事件覆盖，本轮不重复改机制。

## 验证

构建与运行由独立上下文代理 `s02_verification` 执行；该代理未访问设计文件。既有测试与断言预期未修改。

| 检查 | 结果 |
| --- | --- |
| ScopeRegression Release 构建、运行 | 最终统一回归构建 0 警告/0 错误，27/27 |
| Core cognition | 最终统一入口 264/264 |
| Godot 宿主构建 | 最终 0 错误；2 个 NU1900 漏洞信息网络警告 |
| ExpressionCheck | 最终单节点构建 0 警告/0 错误，20/20 |
| EventCheck | 单节点构建 0 警告/0 错误，22/22 |
| ReasonerRegression | 单节点构建 0 警告/0 错误，38/38 |
| Python 表达编译检查 | 6/6 |
| `compile_story.py` 与 `--check` | 由源重新生成，7 项生成物一致 |
| `git diff --check` | 通过 |
| 第一章既有内容表 | 原键值递归比较无变化；对白 CSV 的 762 行内容完全相同，仅回应分支顺序调整 |
| 第二章既有编译内容 | 原键值递归比较无变化，新增表达声明与模板 |
| 帮助全文迁移 | 45 行与原 C# raw string 逐字一致 |
| 最终 `verify.py --skip gallery --keep-going` | 退出码 0：6 PASS、1 SKIP；flow 136/136、flow2 109/109，无引擎错误；gallery 按要求跳过 |

新增检查覆盖：译文替换不改变答疑判定；同一说话人在身份未知/已知时称呼不同且不泄漏给他人；听者条件独立；无适用回应为 `none`；来源引用直传与权重保持；总结不生成事实。

最终原始日志位于 `artifacts/project-audit/verify-logs/20261010-052226-388043-33536/`，汇总为 `tmp/s02-validation/verify-final-02.log`；专项日志均保存在 `tmp/s02-validation/`。

## 已保留的失败与限制

- 首次完整统一回归：core/scope/cognition/host 通过；flow 136/136、flow2 109/109 的断言通过，但两步均因 Godot 无法读取 Windows 根证书库而被入口判失败，退出码 1。原日志位于 `artifacts/project-audit/verify-logs/20261010-044947-513025-18220/`，汇总为 `tmp/s02-validation/verify-01.log`。
- 复测临时使用本机 certifi 的公开 CA 包（121 条）与 `network/tls/certificate_bundle_override`，保留 TLS 验证和原错误扫描。[Godot 官方证书配置说明](https://docs.godotengine.org/en/4.6/tutorials/networking/ssl_certificates.html)。复测完成后已删除临时 `override.cfg`，未交付项目网络设置变更；沙箱默认系统证书读取仍受限。
- ExpressionCheck 与既有 EventCheck 首次默认多节点还原失败，未出现源码错误诊断。保留原日志后，用 `-m:1 --disable-build-servers` 构建及 `--no-build` 运行通过。
- Python 新旧测试首次均遇临时目录写入拒绝。新增表达检查改用工作区单个临时文件，六项断言不变，复测通过。既有 `test_story_script.py` 的 5 项仍因临时目录权限失败，未修改测试；见 `tmp/s02-validation/python-story-script-01.log`。
- 额外运行旧 `Awakening/check/CognitionCheck`：第三项“匹配重演应说服侦探”失败。隔离导出的 HEAD 同样失败，确认修改前已存在；两边构建均通过。记录为 `tmp/s02-validation/awakening-cognition-check-01.log` 与 `head-baseline-cognition-run-01.log`。基线异常退出迟滞，保存完整异常后核对完整可执行路径，仅终止该基线子进程；终止记录为 `head-baseline-cognition-termination-01.log`，失败结论不变。
- 对该旧断言的源码依据：`Chapter1Replay.cs` 仅在已检视物证全部为 Match 时承认 `checkedMatch`；测试的 Fresh 只初始化感知与定位，随后直接重演，没有登记检视。其“已匹配”前提与当前源码合同冲突，是否调整预期由主代理裁决。本轮未改断言；后续断言未执行。

## 未完成项与风险

- 本轮为构建、机制与表达合同回归；一级三份全程记录、二级实际截图检查、三级盲玩均未执行。人物行为合理性、文学质量与游玩体验未验证，不用自动回归代替。
- `CaseBelief.Basis`、`Reasoner.Weight`、`SpeechChoice.Reasons` 的公开返回类型改变；仓库内调用已更新并构建，仓库外调用需迁移。
- 身份称呼的认知条件由剧本声明；本轮不补写身份揭示剧情或创建新的身份事实。后续内容若要激活已知称呼，须通过正常认知更新提供相应事实。
- 未改其他任务的剧情机制或其审计标记。提交、合并及合并后的 Godot 呈现验收由主代理处理。
