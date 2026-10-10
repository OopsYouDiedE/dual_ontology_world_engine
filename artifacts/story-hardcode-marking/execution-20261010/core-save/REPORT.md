# core-save 执行报告（2026-10-10）

## 改动与原因

- 新增 `csharp/CognitionCodec.cs`：版本化 UTF-8 JSON，包含内容/能力版本、稳定主体身份、全部已分配句柄、Frozen AST 和完整审计；闭合分支恢复，不按存档中的类型名实例化 CLR 对象。
- 编码与解码均完整校验。未知版本/能力/节点/字段、重复 JSON 键/身份、缺引用、越界数值、非法 UTF-8 与五类限额明确失败。失败只返回错误，不发布候选。
- 新增 `csharp/RuntimeSave.cs`：保存现实属性、实体及删除身份、切面、主体图、审计和现实/认知绑定；先分配对象再重绑定，恢复不执行初始化、观察、效果或通知。恢复主体继续使用正常的按版本读取机制。
- 已分配但不存活的主体句柄在载入后退休，不能再次分配；新增回归覆盖遗忘句柄与未提交保留句柄。
- `WorldSession.Save.cs` / `SessionPersistence.cs` 保存注册表、声明、章节顺序/结束状态、进度、响应标记、调度时钟/序号和版本化随机状态。候选完全校验后由 Store 发布；相同字节重复载入返回同一对象及恢复 epoch。
- 第一章适配保存实际内容进度、章节时钟、日常生态游标、心智引用及宿主状态。独立恢复构造绕过初态。内容版本与当前编译内容匹配；呈现 ID 改为已保存的会话域＋序号，使新进程后续输出保持连续。
- `GameSession` / `GameEngine` 提供导出与载入 API，无 UI 按钮或布局改动。宿主保存处拒绝活动行动、等待、effects/租约、自主 wakeup、无类型 CTB 回调，以及待选项、呈现回执、走位和 fiber，返回 `UnsupportedSavePoint`，不清零或重跑初态。
- 新增 Godot headless `save` 检查：第一章实际推进至开场结束的空闲点，保存后在第二个 Godot 进程继续调查；完整比较输出、快照、进度、身份、认知版本、时钟、随机状态。该检查使用注入的空间上下文和正常呈现回执，是机制检查。
- 同步三个 `AGENTS.md` 的实际接口/验证入口。验证器保留完整原始日志；结果缺失只重试一次，完整失败直接失败，完整通过后的 `0xc0000005` 按输出判断。临时 DEBUG 打印已核查清理，正式测试结果输出保留。
- 未改 `.story`、生成剧本产物或 `scenes/UI/`；未提交、推送或运行 Godot 编辑器导入。

## 验证结果

所有测试由独立、全新上下文的验证 Agent 执行，未读取设计文件。

| 检查 | 本轮结果 | 墙钟耗时 |
| --- | --- | --- |
| `dotnet build csharp/ScopeRegression.csproj -c Release` | 通过，0 警告/错误 | 21.19 s |
| `dotnet run --project csharp/ScopeRegression.csproj -c Release --no-build` | 27/27 通过 | 4.35 s |
| `CognitionRegression` Release | 270/270 通过；原有 212 项预期未改 | 28.03 s |
| 宿主 solution，`-m:1 --disable-build-servers` | 通过，0 错误；2 个 NU1900 网络审计警告 | 33.97 s |
| SaveCheck 独立 dotnet 写入/新进程读取 | 20＋1 项通过 | 7.45＋4.54 s |
| 修正后的真实 Godot 保存/新进程恢复 | 20＋1 项通过，进程返回 0 | 11.31 s |
| 完整回归中的 flow | 136/136，0 失败 | 219.94 s |
| 完整回归中的 flow2 | 109/109，0 失败；另一轮亦 109/109 | 118.75 / 115.49 s |
| 验证器单测 `python tools/test_verify.py` | 7/7 通过 | 1.21 s |
| `git diff --check` | 通过；仅既有 CRLF→LF 提示 | — |

- Cognition 新增 58 项覆盖两个中性结构、完整 AST/审计 wire 往返、拒绝输入、限额及子进程重绑定；子进程再保存验证身份/审计无重放。原始拒绝输入与子进程输出在 `tmp/core-save-wire/`。
- 保存检查覆盖运行中行动、Wait、active 已空但 effects/租约仍在、自主 wakeup、CTB 回调、未知 Property、外部宿主缺 guard、fiber、选择、呈现及 GameSession 视图回执；也覆盖失败载入保持当前对象、重复载入不发放效果。
- 首次独立跨进程比较发现呈现回执使用新随机 GUID 导致输出不同，已修正生产回执分配并重新通过，未改比较预期。真实 Godot 首次检查发现 GDScript `:=` 推断失败，改为 `=` 后两个进程完整通过。
- Godot 运行时 `APPDATA` / `LOCALAPPDATA` 均指向工作区 `tmp/appdata`。本轮最终 Godot 检查没有退出崩溃或超时；崩溃处理由验证器单测覆盖。

## 失败、耗时与未完成项

- **`python tools/verify.py --skip gallery --keep-going` 未达到全绿，退出 1。** 保存、flow、flow2 的完整结果之外均出现 Windows `Failed to read the root certificate store`（`os_windows.cpp:2570`）。改动前基线也有同一错误；未过滤或改为通过。修正后的保存检查严格判定仍因父/子进程证书错误失败。
- flow 耗时：改动前 **191.35 s**，完整回归 **219.94 s（+14.94%）**。复测首轮 **407.52 s、134/136**，失败为 R-06 与 R-07 卡片文字断言（`flow_check.gd:399/407`）；旧验证器把非零失败数误判为缺汇总后重试，重试 **227.94 s、136/136**。已修正验证器并以单测确认完整失败不重试；首轮失败证据保留。
- 复测期间观测到另外两组 Godot 同时运行，CPU 争用污染性能样本；R-06/R-07 偶发失败原因尚未确认，未改断言、剧情或 UI。**“无显著减速”与该偶发失败的隔离复核尚未完成。**
- flow2 改动前完整耗时因上轮额度中断未取得；本轮首轮耗时由外层总时间减去重试时间得到，包含少量日志开销。旧验证器未识别 `N passed`/CRLF 导致不必要重试，现已修正并用两份真实日志验证，无第三次运行。
- 当前支持通用 WorldSession 空闲点与第一章适配的空闲点；第二章 GameSession、连续交接以及上述活动状态明确拒绝，尚无这些切点的序列化支持。
- 仅执行构建与自动机制回归；一级三份记录、二级 UI、三级盲玩及人物/游玩体验均未验证。

## 证据与交付

- 改动前基线：`tmp/core-save-baseline/step-timings.json` 与同目录 `raw-logs/`。
- 完整构建/回归：`tmp/core-save-final-validation/validation-summary.json`、`step-timings.json`、`raw-logs/20261010-043822-360343-6456/`；独立 dotnet 保存检查在 `after-detached/`。
- 修正后真实 Godot：`tmp/core-save-headless-recheck/raw-logs/20261010-044944-286270-28788/`（首轮）及 `retry/`（重试）；`process-timings.json`、`step-timings.json`、`godot-process-observation.json` 保留实际返回码、耗时与并发观察。
- 验证器：`tmp/core-save-headless-recheck/test_verify-final.stderr.log` 与 `real-flow2-recognition.json`；此前失败及初版验证日志未覆盖。
- 交付为工作区代码、测试与本报告。尚需主代理在无证书访问限制且无并发 Godot 的环境完成统一回归/性能裁决，再决定提交合并。
