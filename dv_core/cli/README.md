# Core 终端入口

DvCli 单向引用 host_bridge，共用正式 Prepare → formal-model → Complete；不依赖 Godot、检查程序集或 oracle。

```powershell
dotnet run --project dv_core/cli/DvCli.csproj -- --tui
dotnet run --project dv_core/cli/DvCli.csproj -- --jsonl

```

## 列表与视角

空间内容按区域和房间排列，当前所在区域和房间优先：房间为一级标题，人物与物体为二级标题；人物在前，物体在后，各自使用稳定排序。区域内客观空房间合并为“空：……”，主观没有成员记录的房间合并为“未记录成员：……”，保留实际为空与尚未取得记录的区别。TUI 默认收拢其他已知房间的成员摘要，`zoom in` 展开完整关系；摘要不把过去认识解释成现场空无一物。单地图在至少 100 列时与最近记录并排，窄屏把最新一条记录放在地图前；双地图在至少 100 列时并排，最近一条记录保持在地图前，其余记录随后排列。没有正文时宽屏可将完整房间组排成两列。正文按新到旧排列，每条保留游戏时刻和说话人，续页重复记录范围及所属条目，未知说话人不从客观名称回填。附属状态和通道使用较低对比度；通道用 `<->` 连接真实端点，并显示投影已提供的开闭或锁定状态。页脚显示当前命令。ANSI 和 SVG 共用一个单元画布，房间、成员标题在彩色终端及 SVG 中使用粗体；无色模式仍保留缩进与独立行。SVG 合并同色同字重的连续字符，以 `textLength` 和 `lengthAdjust="spacingAndGlyphs"` 约束格宽，中文破折号按双格处理。Windows 原生控制台在绘制前启用当前输出句柄的 VT 模式，退出时恢复；不可用时使用无色输出。

默认展示当前观察者的主观地图。map subjective、map objective、map both 选择主观、客观或并排检阅；窄终端纵向排列。客观地图是显式预览投影，不向人物授予事实，不进入候选资格或规划。view ID 切换检阅人物，views 列出可检阅人物；当前正式控制者保持不变。启动时 --viewer ID 指定本局人物。

房间和实体由对应投影提供，排序相邻不产生连接，未观察地点和物体不从客观地图补入主观地图。所有者、实际携带者与容器分别处理：携带物归在人物下，容器内容归在容器下，所有权本身不决定位置。只沿当前投影已知且无冲突的位置链归入房间；未知归属或循环保持缺失。容器保持物体条目，不产生额外房间；事件和谜题状态不作为实物混入地图，具有实际物理身份的开场道具保留。当前本人只标在所属房间。TUI 按窗口高度分页，标题、状态、统计与页脚固定，页码保持可见；`page N/next/prev` 翻阅正文和地图。行动只有一个显示区域，默认每页最多五个候选；`actions next/prev` 翻行动页，`actions all/compact` 在同一位置展开或收拢，完整列表不再在地图后重复。新聚焦跟随选中项所在行动页，编号与正式候选顺序相同；选中项使用 `>` 标记，长标签和区分后缀完整换行保留。两种地图在当前房间优先之外按区域、房名与内部标识固定排序，当前观察者用颜色突出；内部标识不显示在成员名称旁。分页让房间和条目标题跟随首条内容，续页重复所属范围与标题。`zoom fit/in/out` 只改变显示密度和非当前房间的展开程度，不推进会话；无会话示例仍可导出完整列表。

## 统一输入

开局可直接 focus/act：idle 阶段先 Prepare，再聚焦真实候选。固定 view 时，next 仍按两步确认 Core 已选定的 NPC 行动，结果只呈现检阅者可见内容；检阅人物不改变人工控制者。

交互记录的 actor 表示行动主体，viewer 表示检阅者，result 的 presentation_viewer 表示正文实际观察者。回放正文按该来源筛选；JSON-Lines 的实时交互也按当前检阅者投影候选，不随 NPC 调度输出另一主体的私有评分。

| 命令 | 行为 |
| --- | --- |
| status / options | 读取当前状态或候选，不调度下一动作 |
| focus 01 / focus ID / focus JSON | 聚焦当时真实候选，记录选项和评分 |
| confirm | 核对版本并提交已聚焦选项；到期行动经 Complete，排队选择显示已排程 |
| cancel | 取消当前聚焦 |
| act 01 / act ID / act JSON | 便捷调用同一 focus → confirm 链 |
| next | 一次 Prepare 后显示聚焦；再次 next 确认该行动 |
| view ID / views | 只读切换或列出人物视角 |
| map subjective/objective/both | 选择地图投影 |
| page N / page next / page prev | 翻页检阅完整列表 |
| actions next / actions prev / actions all / actions compact | 翻行动页，或在同一行动区展开、收拢 |
| capture NEW.svg | 导出当前页，不推进世界 |
| capture all NEW_PREFIX | 在当前帧展开行动列表并导出全部续页；每帧最多 16 页，超限明确失败 |
| save NEW.dvsave / trace NEW.dvdag | 保存稳定提交点或轨迹 |
| scores on/off/auto | 调整评分显示，人类与大模型默认隐藏，引擎自主默认显示；回放沿记录的类型 |
| perf / perf NEW_PREFIX | 读取统计拆分，或导出新的报告前缀，不推进世界 |
| quit | 关闭并封存，自动导出本会话耗时报告 |

`TerminalView.RenderFrame` 为当前显示一次构造单元画布，同时返回文本、可选 SVG、正文页码与行动页码；常规绘制与抓取不再分别重建同一画布。费用只在全部候选都有同一已记录费用时上提为“各项”，其余逐项显示。嵌套携带或容器内物件继续保留当前投影已知的所有者，携带关系不替代所有权。

同一结果事件内的原文行与说话者次序保持原顺序，同一说话者的连续行合为一段；仅事件之间按新到旧排列。正文窗口淘汰完整旧事件，不从事件中间截走开头。单地图宽屏只让能与地图高度配合的最近事件并排，其余较早事件使用全宽，续页不沿用已经空出的半屏列。长通道的状态另起缩进行，并与该通道端点一起分页。只有位置快照、缺少本帧完整主观投影时，标题显示“已记录位置／本帧未记录完整认知图”；缺失物件或通道不显示成它们已经消失，也不从旧帧补写。

默认 `--controller player`，`human` 为同义入口；外部大模型使用 `--controller llm`，引擎自主控制使用 `--controller ai`。`--jsonl` 只设置协议格式。控制来源在首次计算前确定，存档保留诊断标签；默认恢复沿用记录的类型，显式覆盖仍须符合存档实际控制权。人工与外部模型均可通过同一命令或 JSON-Lines 输入 focus/confirm/act；引擎自主与 NPC 的已选动作经 next 分成聚焦、确认两步。人工选择可能先进入调度队列；另一人物更早到期时，仅记录已排程，后续 next 再聚焦实际待执行行动。排队确认不当作该行动已执行。一次输入不循环排空后续 NPC 工作。候选按当前 Core 资格重核，聚焦后状态版本改变则拒绝旧选择。评分来自 Core 的本人认知评分，未记录分数显示短横，不由 UI 构造或补算成历史值。浮点评分在界面最多显示八位有效数字，协议和原始记录保留原值；`score_kind` 分别标注“根 / 选择 / 即时”。“未展开”只按同一认知修订的真实规划记录判断，不按分数大小猜测。候选只读取行动 `label`；重复标签先按已公开的地点、对象或行动类别区分，仍相同则使用顺序编号，不显示内部 ID。评分紧邻标签放在首行，长标签续行按编号后缩进。相同费用在行动标题统一显示，不同费用放在对应候选的首行，缺失费用保持明确，零值费用项省略。客观人物与物体使用内容中的中立名称；主观人物名称仍取本人的认识。

```json
{"command":"focus","argument":"01"}
{"command":"confirm"}

```

--jsonl 的 stdout 为带 kind、scope、data 的协议行，诊断走 stderr；JSON-Lines 不作为新原始证据落盘。TUI 与 JSON-Lines 互斥。inspect/tp/time 保持显式 --dev 的开发入口。

## 耗时统计

时钟下的固定统计区只显示三项累计比率：

| 指标 | 计算与单位 |
| --- | --- |
| 推进速度 | 正式推进游戏小时 ÷ 累计现实小时，`game_hours_per_real_hour` |
| 决策频率 | 全部来源正式提交决策数 ÷ 累计现实小时，次／现实小时，`decisions_per_real_hour` |
| 接口耗时率 | 当前控制来源累计决策接口耗时小时 ÷ 累计现实小时，小时／现实小时，`interface_hours_per_real_hour` |

接口耗时率标明引擎、LLM 或人工，并同时显示实际采样时长与推进的游戏分钟。三项比率共用本会话现实窗口，不再并列倒数“秒／游戏小时”或“每计算小时决策数”。零现实覆盖或缺失字段显示“—”，真实零推进可显示零；短窗口比率是累计样本的归一值。正式决策只计进入会话状态的 `action_selected`，聚焦、候选节点及未采用预备分支不计入。现实用时从打开／恢复完成计至关闭；人工与 LLM 接口耗时取显式决策区间，缺失区间保持缺失，不能拿整段会话时间替代。详细接口阶段、正式引擎、投影、初始化和归档拆分留在 `perf` 及导出报告。退出时自动导出，可通过 `--performance NEW_PREFIX` 指定位置；导出格式及完整口径见 [引擎耗时记录](../../docs/engine-performance.md)。

采样窗口以现实秒显示，并保留毫秒小数；短于一秒的窗口不会显示成整秒零值。

`--scores` / `--no-scores` 与 `scores on/off/auto` 调整评分投影与呈现，不改变行动资格、规划预算或正式选择。默认只提供已经记录的根分与选择路径分；只有显式 `--scores` 或 `scores on` 才请求当前即时估值与分项。未请求且没有历史分的候选显示“未估值”；只有真实规划记录标为 unexpanded 才显示“当前未展开”，缺失不补零。人类和大模型模式默认隐藏评分，引擎默认显示已有分数。协议与交互记录保留本次真实取得的评分来源，不补算历史值。交互记录保存 `controller_kind` 和实际摘要；回放的 `perf` 只读取当前帧，禁止导出新报告，缺失字段不补造。

## 回放

```powershell
dotnet run --project dv_core/cli/DvCli.csproj -- --replay FILE.dvdag --tui --speed 2
dotnet run --project dv_core/cli/DvCli.csproj -- --replay FILE.dvdag --jsonl --interactive

```

回放共用 SmartReplay；不创建正式会话。--interactive 从暂停状态接受命令；step/next 逐帧，play/pause/restart 控制播放。键盘空格暂停或继续，右方向键单步，1/2/4/8 调速，Tab 切地图，Q/Esc 退出。view ID 固定人物视角，view follow 跟随实际行动者。

`seek N` 与 `replay frame N` 使用标题栏相同的一基帧号；`seek first/last` 定位记录首尾。定位会清空显示缓存，从记录起点静默消费到目标帧，按当前检阅视角恢复先前正文，最后只绘制目标帧；不打开正式会话、不执行行动，也不补录没有取得的主观或客观投影。原始帧仍完整保留，最近正文遵守现有 40 条显示窗口。`replay marks` 只输出本人已完成行动及起止 checkpoint 的帧号、类型、游戏时刻、观察者和公开行动标签，便于选择截图；正文与其他人物私有候选不进入该索引。

新 final checkpoint 可携带关闭边界真实取得的 `closing_preview` 与 `closing_preview_state_version`。回放直接使用其中实际记录的观察者快照和地图，终态仍清除候选及聚焦；不在回放时打开会话或重新求值主观知识。没有该字段的旧记录保留位置快照并明确完整认知图缺失，不沿用前一时刻的投影冒充末帧。

`replay capture NEW_DIRECTORY` 要求 TUI 和固定 `--viewer ID`，最多选取四个真实记录锚点：前两个本人可见完成结果、最后本人已完成行动、记录末帧。不存在的结果如实缺失，不构造开场或穿衣正文。每个锚点重建先前正文后展开当时行动列表，导出全部续页；单帧超过 16 页时失败并报告已导出的部分，不将不完整采集报告成成功。文件分别以 `01-first-result`、`02-next-result`、`03-last-action`、`04-record-end` 命名，续页使用 `-page-02` 等后缀。`capture_summary` 给出实际帧号、公开标签和每页路径，角色由真实新记录决定，不预置剧情路线。

新 CLI 记录保留每次交互的候选 `label`、评分及 `score_kind`、当前即时估值的 `score_breakdown`、选择、角色显示名目录、实际检阅视角投影、已采集的 `performance` 摘要和正式结果，统一呈现 focus → confirm。投影只按实际请求记录；回放中未记录的视角或客观图保持缺失，不借另一时刻的图补齐。候选区明确“当时的候选”，地图与正文使用该次真实结果；`selection_game_time/selection_state_version` 和 `result_game_time/result_state_version` 分别保留两个边界。即时分项、历史决策根分与最终选择路径分各自保留来源；选中项可并列显示选择时刻、路径分与当前即时分，`zoom in` 展示已记录即时分项。聚焦展示行动意图，结果正文归确认后的事件记录；缺失标签显示“（未记录标签）”。`confirm` 帧的 `phase/action_state` 进一步区分已确认待结果、已排程、已执行和被拒绝，`option_time_basis` 标明候选时点。旧记录缺失的候选、评分或主观地图保持缺失，不拿当前内容或分数补写历史。最终 checkpoint 显示结束，清除旧聚焦，并保留记录中的待执行项、存档截点与运行来源；轨迹结束不代表所有已选行动完成，显示帧不代替可恢复存档。每个可见节拍单独保留展示时间，原思考和设备静默不进入播放等待；游戏时钟消费当前已记录时刻，原始输入时刻仍保存在证据中。标题栏保留播放/暂停和帧内实际阶段。

## 导出与有限采集

无会话 --preview 默认显示双地图示例，--preview-rooms 1..24 与 --preview-current N 调整示例结构；不启动 Core 或 AI。--export-ui NEW.svg 可用于预览、实际会话只读 status/options，或回放的 step、seek N、replay frame N。交互中 capture NEW.svg 导出当前真实显示；capture all NEW_PREFIX 导出当前帧的完整分页。PNG 是这些 SVG 的栅格导出，不称为操作系统终端截图。转换工具仅支持生产导出的 rect/text 子集，保留基线、颜色、字重，并按字体真实 advance 将整段字形横向缩放至 textLength；不支持的 lengthAdjust 明确报错：

```powershell
python mcp/verification/render_terminal_export.py --svg NEW.svg --out NEW.png
python mcp/verification/capture_cli.py --out NEW_DIRECTORY --timeout 90 --format tui

```

采集工具转发操作者输入，不代选行动；默认 90 秒硬超时，结束自己启动的进程树并保留 .partial。可显式指定 --controller ai、--format jsonl、--viewer ID 或 --replay FILE。工具不扫描源码或程序集、不计算源码哈希，也不检查运行前后代码变化；日志明记 source_validation=not-performed，--no-source-validation 为兼容性显式标记。回放始终显式传入检阅人物，只使用已记录主观图。正式行为、实际界面、录制回放和工程构建分别记录。

正式 AI 已封存后，以下命令只读使用新 session.dvdag，不为截图重新开局。--capture-replay 只自动输入 replay capture 与 quit，实际命令和 capture_summary 写入 console.dvlog；截图保存在新采集目录的 screenshots 下。

```powershell
python mcp/verification/capture_cli.py --cli dv_core/cli/bin/Release/net8.0/dv-cli.dll --replay RUN_DIRECTORY/session.dvdag --out NEW_CAPTURE_DIRECTORY --viewer detective --width 110 --height 50 --timeout 45 --format tui --capture-replay --no-source-validation
Get-ChildItem NEW_CAPTURE_DIRECTORY/screenshots/*.svg | ForEach-Object { python mcp/verification/render_terminal_export.py --svg $_.FullName --out ([System.IO.Path]::ChangeExtension($_.FullName, ".png")) }
```

非 Windows 或缺少默认微软雅黑字体时，转换命令显式添加 --font PATH_TO_CJK_FONT。单张指定帧可使用 `dotnet dv_core/cli/bin/Release/net8.0/dv-cli.dll --replay FILE.dvdag --viewer detective --tui --command "seek N" --export-ui NEW.svg`；它只导出目标帧第一页，完整续页使用交互 capture all 或上述批量采集。所有输出独占新路径，重复名称失败，失败与部分导出保留原样。

保存与轨迹使用带校验的二进制，独占新路径。退出只调用一次 Close，并独立尝试保存与轨迹；失败返回非零并保留部分证据。Ctrl+C 在同步 Core 调用返回后执行封存；采集工具的硬超时负责终止未返回的测试进程。
