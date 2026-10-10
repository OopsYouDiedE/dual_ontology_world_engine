# core-d 执行报告（2026-10-10）

## 改动与原因

- 新增 Core 五类类型化事件：行动、观察、证词、传播、呈现。共用稳定事件 ID、时刻/单位、主体/对象/位置引用、阶段/结果、结构化命题、来源、感知者与因果引用；保留旧 Event 投影和 ObservationEvent 入口。
- 权威记录与主体事件分域：引擎登记校验身份、世界引用及前置因果；权限投影只保留本人获准感知、已知对象/命题及可见因果。认知快照使用主体句柄，重绑定与遗忘处理事件依赖。
- 行动提交、原子阶段、实际操作和终态分别登记；失败阶段不登记生效，取消停止后续效果。发言产生证词，接受请求不执行效果，履行以实际执行及请求 Topic/因果为据；主体退役后仍保留终态身份。
- 第二章 Interpret 查询证词命题；InkFrames 查询实际行动地点；Seizure 查询回应 Topic；口供、登记、候选、可读线索与“有变化”过滤改为结构化字段。转述关联具体来源事件，因果链呈现过滤未获知步骤。
- 痕迹保存玩家可辨的事实类别与完整/一瞥权限，比较返回枚举状态；显示文字不再决定地点、嫌疑或比较结果。现有口供文案原样移入 `.story` 文案表。
- CaseTraces.Equals/GetHashCode 比较全部字段及列表值；逐项比较也包含 LatchToolMarks，修复不同工具痕被 Describe 文本掩盖的问题。
- AWK-038 动机 cue 改为实际可见剧本行/重演事件的稳定来源 ID；未署来源的显示文本不提供证据。来源声明保存在 `story/第一章/模拟侦探.story`。
- 删除 CH2-007、028、031、038、039、045、050、051、052、082 与 AWK-038 对应审计注释，同步两个子目录 AGENTS.md；未改 UI、布局、样式和既有测试断言，未提交。
- 本轮新增代码无临时 DEBUG 打印；回归保留 PASS/TOTAL 输出。

## 验证

由独立上下文验证代理执行；只做机制自动验证，未执行一级全程、二级 UI 或三级盲玩验收。

| 项目 | 本轮结果 |
| --- | --- |
| ScopeRegression Release build / run --no-build | 0 警告、0 错误；24/24 通过 |
| CognitionRegression Release build / run --no-build | 0 警告、0 错误；264/264 通过 |
| 新增 EventCheck Release build / run --no-build | 临时源编译产物；0 警告、0 错误；22/22 通过 |
| DetectiveSimulation Release build | 临时源编译产物；0 警告、0 错误 |
| 完整 Godot C# 宿主源码 build（临时项目，-m:1 --disable-build-servers） | 通过、0 源码错误/告警；仅 2 条 NU1900 网络告警 |
| 正式宿主 solution build（-m:1 --disable-build-servers） | 未通过：两份剧本生成 JSON 过期；另有 2 条 NU1900 网络告警 |
| verify.py --skip gallery flow flow2 --keep-going | core/scope/cognition 通过；host 因上述产物检查失败；退出 1 |
| InvestigationCheck（既有断言） | 15 项通过、2 项失败；随后空菜单索引异常，超时终止 |
| git diff --check | 通过 |

- 中性事件回归在平坦/嵌套两种世界覆盖五类字段与权限、跨域拒绝、稳定 ID/因果校验、快照重绑定、发言≠提交、接受≠执行、失败/取消、实际到场及退役主体身份。
- 新增宿主检查替换日志、对白/模板、标点、痕迹文字和人物显示名；地点、候选、实际回应、比较状态与规划器行动选择保持一致。另验证未知痕迹不提供候选、无关人物消息不开放权威行动、只差 LatchToolMarks 必须不等。
- 本 worktree 的 ScopeRegression 入口实际输出 24 项（HEAD c5e5c8ab），未删除/改变其测试；与用户所述整合基线 27 项的计数差异需主代理合并时核对。原 Cognition 212 项加事件回归 52 项，共 264 项。
- 临时资源由正式 compile_story.generated_files 从当前 `.story` 生成到 `tmp/core-d-validation/host/`，链接当前源码编译；未写正式生成物、未手改产物。上述临时检查不代替正式管线通过。
- 原始构建/运行日志保留在 `tmp/core-d-validation/`；25–31 为最终四次构建和三次运行，均退出 0；33 为完整临时宿主编译，34 为最终 Cognition 264 项复验。11 为统一 dotnet 回归，18 为 Investigation 原始失败与超时记录。32 为临时项目 SDK 导入格式错误，修正后由 33 通过。

## 两项既有失败的定位

1. `Original day-three Logistics/Clinic inspections yield real known traces and legal candidates`：检查取得台账、门闩、碗三条本人调查记录，但台账无缺额/签名，门闩松动不含人物或身量，碗无新增物。原始输出 `CAND []`。
2. `Nonempty original candidates produce semantic questions`：由同一空候选导致 `QUESTIONS []`；其后 Program.source:47 直接取 `menu[0]`，抛 ArgumentOutOfRangeException。记录异常后仍未退出，按准确进程身份超时终止；既有产物恢复原状。

源码依据：Morning.cs:171 从实际 NightLedgerRecords 取得签名；Morning.cs:513 的 ReplayCandidates 只采本人读到的身量/签名及获准见闻；Morning.cs:531 的 BuildsSeenAt 继续校验守夜/问话来源。本次路线没有这些人物证据，不能从松动文案或隐藏行动补出候选。

未判定两条断言过时，未改预期或注入身份。路线是否应通过 `.story` 调整获得更多见闻，或断言应接受合法空候选，交主代理裁决；本报告不将这两项记为通过。

## 未完成项与风险

- 按用户分工，正式剧本重生成及 Godot/flow/flow2 回归留给主代理。待更新 `generated_awakening_content.json` 与 `generated_chapter_02.json` 后重跑正式宿主构建及 `python tools/verify.py --skip gallery --keep-going`；本轮未验证 136/136、109/109 基线，不能宣称全量通过。
- 本轮未运行 Godot，因此无 0xc0000005 退出结果可裁定；主代理运行时须将 APPDATA/LOCALAPPDATA 指到工作区 tmp/appdata，并按本轮用户要求处理缺失输出的重试。
- AWK 的 shen_care 两项旧文案 cue，以及 su_hearing 的负向 cue，在现有 `.story` 实际可见行中无对应来源；新来源声明显式为空，保持其先验，不虚构证据。主代理若补写真实来源，应同时更新声明并验证规划结果。
- 权威 EventAudit/ActionExecution.Events 供引擎与宿主使用；角色查询仍须从本人的认知/来源入口读取。Core 无 codec/跨进程保存扩展，本任务未改变该边界。
