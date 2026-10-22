# Core 终端入口

DvCli 单向引用 host_bridge，共用正式 Prepare → formal-model → Complete；不依赖 Godot、检查程序集或 oracle。

```powershell
dotnet run --project dv_core/cli/DvCli.csproj -- --tui
dotnet run --project dv_core/cli/DvCli.csproj -- --jsonl

```

## 列表与视角

空间内容按房间逐组排列：房间为一级标题，人物与物体为二级标题；附属状态、正文和通道使用较低对比度。人物和物体保持各自身份，通道逐条显示真实端点。界面只含短标签、状态、正文、选项与评分，不显示教程、来源说明或说明段落。ANSI 和 SVG 共用一个单元画布，房间、成员标题在彩色终端及 SVG 中使用粗体；无色模式仍保留缩进与独立行。Windows 原生控制台在绘制前启用当前输出句柄的 VT 模式，退出时恢复；不可用时使用无色输出。

默认展示当前观察者的主观地图。map subjective、map objective、map both 选择主观、客观或并排检阅；窄终端纵向排列。客观地图是显式预览投影，不向人物授予事实，不进入候选资格或规划。view ID 切换检阅人物，views 列出可检阅人物；当前正式控制者保持不变。启动时 --viewer ID 指定本局人物。

房间和实体由对应投影提供，排序相邻不产生连接，未观察地点和物体不从客观地图补入主观地图。容器内物体只沿当前投影已知且无冲突的位置链归入房间；未知归属或循环保持缺失。容器保持物体条目，不产生额外房间。当前本人只标在所属房间。TUI 按窗口高度分页，标题与状态固定，页码保持可见；page N/next/prev 翻页。首页行动窗口只保留选中项附近最多五项并显示编号范围，聚焦和确认回到此窗口；选中项使用 > 标记，重复标签的区分后缀在缩略时保留。两栏地图均把当前房间置顶；完整行动列表放在地图后供分页检阅。续页重复地图范围和所属房间或通道标题，成员不会脱离层级。zoom fit/in/out 仅改变行距密度；无会话示例仍可导出完整列表。

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
| capture NEW.svg | 导出当前页，不推进世界 |
| save NEW.dvsave / trace NEW.dvdag | 保存稳定提交点或轨迹 |
| quit | 关闭并封存 |

默认 --controller player；引擎自主控制使用 --controller ai。人工与外部模型均可通过同一命令或 JSON-Lines 输入 focus/confirm/act；引擎自主与 NPC 的已选动作经 next 分成聚焦、确认两步。人工选择可能先进入调度队列；另一人物更早到期时，仅记录已排程，后续 next 再聚焦实际待执行行动。排队确认不当作该行动已执行。一次输入不循环排空后续 NPC 工作。候选按当前 Core 资格重核，聚焦后状态版本改变则拒绝旧选择。评分来自 Core 的本人认知评分，未记录分数显示短横，不由 UI 构造或补算成历史值。浮点评分在界面最多显示八位有效数字，协议和原始记录保留原值；`score_kind` 分别标注“根 / 选择 / 即时”。“未展开”只按同一认知修订的真实规划记录判断，不按分数大小猜测。候选只读取行动 `label`；评分固定在首行，长标签续行按编号后缩进。相同费用在行动标题统一显示，不同费用逐项列出，缺失费用保持明确，零值费用项省略。客观人物列表附稳定标识，主观人物名称仍取本人的认识。

```json
{"command":"focus","argument":"01"}
{"command":"confirm"}

```

--jsonl 的 stdout 为带 kind、scope、data 的协议行，诊断走 stderr；JSON-Lines 不作为新原始证据落盘。TUI 与 JSON-Lines 互斥。inspect/tp/time 保持显式 --dev 的开发入口。

## 回放

```powershell
dotnet run --project dv_core/cli/DvCli.csproj -- --replay FILE.dvdag --tui --speed 2
dotnet run --project dv_core/cli/DvCli.csproj -- --replay FILE.dvdag --jsonl --interactive

```

回放共用 SmartReplay；不创建正式会话。--interactive 从暂停状态接受命令；step/next 逐帧，play/pause/restart 控制播放。键盘空格暂停或继续，右方向键单步，1/2/4/8 调速，Tab 切地图，Q/Esc 退出。view ID 固定人物视角，view follow 跟随实际行动者。

新 CLI 记录保留每次交互的候选 `label`、评分及 `score_kind`、选择、角色显示名目录、主客观投影和正式结果，统一呈现 focus → confirm。聚焦展示行动意图，结果正文归确认后的事件记录；缺失标签显示“（未记录标签）”。旧记录依据已有输入与行动结果归一为同样的展示步骤，缺失的候选、评分或主观地图保持缺失，不拿当前内容或分数补写历史。显示帧不代替可恢复存档，最终状态不回填早期认识。每个可见节拍单独保留展示时间，原思考和设备静默不进入播放等待；游戏时钟消费当前已记录时刻，原始输入时刻仍保存在证据中。标题栏保留播放/暂停，并以“帧内：聚焦/确认”等短状态区分交互阶段。

## 导出与有限采集

无会话 --preview 默认显示双地图示例，--preview-rooms 1..24 与 --preview-current N 调整示例结构；不启动 Core 或 AI。--export-ui NEW.svg 可用于预览、实际会话只读 status/options 或记录首帧 step。交互中 capture NEW.svg 导出当前真实显示。PNG 转换工具保留标题字重：

```powershell
python mcp/verification/render_terminal_export.py --svg NEW.svg --out NEW.png
python mcp/verification/capture_cli.py --out NEW_DIRECTORY --timeout 90 --format tui

```

采集工具转发操作者输入，不代选行动；默认 90 秒硬超时，结束自己启动的进程树并保留 .partial。可显式指定 --controller ai、--format jsonl、--viewer ID 或 --replay FILE。正式行为、实际界面、录制回放和工程构建分别记录。

保存与轨迹使用带校验的二进制，独占新路径。退出只调用一次 Close，并独立尝试保存与轨迹；失败返回非零并保留部分证据。Ctrl+C 在同步 Core 调用返回后执行封存；采集工具的硬超时负责终止未返回的测试进程。
