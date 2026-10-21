# 游戏修复与存档复测记录 — 2026-10-21

## 结论与范围

已修复呈现队列集中排空、连续 AI 调度时难以取得安全书签边界、读档后播放倍率与物理步频配置不一致三项问题，并补齐本机缺失的原谱音乐和音效。新增可复用的真实存档采集入口，完成新开局、指定书签恢复、继续调度与玩家输入诊断。

最终冻结版本为 **e6e915ee5f763e73c48cbfffb3ad8f6605e99ade**。180 秒新开局到达逻辑第二章，第三个安全书签恢复后继续运行 60 秒。游戏末态仍 active=true；本报告不把章节字段切换当作正确作答、完整案件或通关证明。第二章窗口内侦探仍在 R08 等待已知作息机会，其他主体继续行动；后续侦探案件流程尚未验收。

实际环境：用户 Windows 项目、Godot 4.6.1 .NET、真实渲染和物理执行；窗口置于屏幕外，截图仍来自实际 viewport。引擎 AI 自主决策，采集器只在已显示对白上发送 Space。本轮正式新开局与续跑没有外部键鼠动作包，不使用预写路线、答案或状态注入。两轮串行，普通日志，无 managed profiler。

工作区原有剧情、素材和文档修改保留。本次提交仅包含自己的代码与文档；不能用提交号代表整个工作区干净。每轮 source-before/after.json 同时封存受测源码、运行 DLL、素材和生成音频的指纹；两轮各自及跨轮比对均无变化。

## 修改

- **Main.gd：呈现队列分帧。** 按约 2 毫秒批次依原顺序交付全部事件，避免对白结束后大量 NPC 投影在一帧集中刷新。该预算只在事件之间生效，单个事件仍可能超时。排空时暂缓新的现场操作，已接受的物理执行和当前对白仍可结束。
- **Main.gd / CasebookPersistence.cs：安全书签请求。** 在正常现场请求保存时，暂存后续 dv_next；当前现场动作、Complete 和阅读继续完成。宿主呈现边界与 C# 命令边界同时就绪后调用既有保存接口，完成或失败后恢复调度。关闭、错误及换会话时清理旧请求。
- **Main.gd：读档配置。** 新开局和读档共用播放配置，恢复会话的控制者保持原值。本轮 4 倍实际记录为 240 Hz、每帧最多 32 个物理步。
- **缺失音频。** 以仓库已有九份原谱经已安装 MuseScore 4 重建九首音乐，使用原有工具生成十二个音效；已有五个人物短音保留。完成 Godot 导入，后续日志没有 main_theme 缺失警告。生成音频按项目现有规则忽略，不强行加入源码提交。
- **复用入口。** 新增 [run_saved_game_capture.py](../../godot_csharp/tools/run_saved_game_capture.py)、[capture_saved_game.gd](../../godot_csharp/tools/capture_saved_game.gd) 与 [使用说明](../../godot_csharp/tools/SAVED_GAME_CAPTURE.md)。拒绝复用输出目录，检查时长、源文件变化、运行错误、封存结果和检查点结果；复制失败不计成功。启动器返回码表示采集条件通过，实际恢复和游戏进展另外核验。

C# 工程构建为 0 错误、0 警告，日志见 build-save-boundary.log；后续实际 Godot 运行也验证了 GDScript 装载。原 dv_level3_capture.gd 未改动。

## 最终两轮实机证据

| 项目 | final-fresh-01 | final-restore-01 |
| --- | --- | --- |
| 启动时间 UTC | 03:52:40.385 | 03:57:38.195 |
| Godot PID | 12516 | 27444 |
| 实际采集时间 | 180.004 秒 | 60.003 秒 |
| 起点 | 新开局，第 1 章，08:00，R05 | 第 3 号书签，第 2 章，次日 07:51，R08 |
| 末态 | 第 2 章，次日 11:02，R08 | 第 2 章，次日 11:29，R08 |
| 书签 | 3 份成功 | 2 份成功 |
| 源码变化 / 超时 / ERROR | 无 / 无 / 无 | 无 / 无 / 无 |
| 封存 / 归档错误 | 完整 / 无 | 完整 / 无 |

第一条观察到 chapter=2 的采样发生于新开局 **122.295 秒**，游戏时间 1830（次日 06:30）。这是采样观察时间，不替代精确剧情事件时刻。

新开局检查点：

| 检查点 | 请求现实秒 | 保存及副本完成现实秒 | 保存游戏时间 | 状态 | 已存案卷条数 |
| --- | ---: | ---: | --- | --- | ---: |
| 001 | 30.000 | 32.685 | 08:10，第 1 章 | idle | 17 |
| 002 | 80.000 | 82.884 | 09:41，第 1 章 | idle | 47 |
| 003 | 130.006 | 133.802 | 次日 07:51，第 2 章 | idle | 53 |

第 1 个请求时已有 prepared 动作；实际保存发生在其完成与正文阅读结束之后。三个主存档和案卷侧车分别与原件 SHA-256 相同，侧车绑定的主档哈希也一致。

第 3 号主档 SHA-256：

`7ceff2e02e50e2d3ebebfc43c836d3aa2032c55ed5bf94f92838e63a14e38c5c`

续跑实际启动参数明确加载 final-fresh-01/run/checkpoints/checkpoint-003.dvsave。首个 view 在 **2.759 秒**到达，其完整世界快照与保存时快照相等，快照摘要哈希也一致；不是只比较位置、章节几个字段。已存侧车的 **53 条案卷**与恢复对象的结构、字段值及列表顺序一致，没有 casebook warning。递归类型核对发现 314 处整数经 Godot JSON 变为数值相等的浮点表示，未发现布尔与数字混同或其他差异；因此此处不是恢复对象 JSON 字节一致的声明。具体记录见 casebook-value-audit.dvlog。

恢复后游戏时间从 1911 推进至 2129，并于 **24.508、44.356 秒**再次生成安全书签；两对副本与原件、侧车绑定都通过校验。实际加载验证覆盖第 3 号书签，其他四份只验证了保存、复制和绑定，并未逐一载入。

所有真实续跑书签均在 run/checkpoints/ 下。新开局关闭时 close-result 的 pending_action=action478，unprocessed_commands=0；续跑关闭时 pending_action 为空，unprocessed_commands=0。关闭封存允许保留真实在途动作，其 host-session/save.dvsave 不作为本轮已验证可续读书签。

主要原始证据：

- final-fresh-01/ 与 final-restore-01/：launch.json、source-before/after.json、result.dvlog、run/host-events.dvlog、run/measurement.dvlog、run/checkpoint-events.dvlog、截图及 host-session/。
- final-verified-2-runs.dvlog：从上述原始记录提取的汇总，保留关键时间、指纹、完整对象比较结果。
- final-fresh-01/run/frame-000122295.png、frame-000179919.png：已实际查看，R08 现场与角色可见、时钟分别为 06:30 / 11:02。

## 其他诊断与失败记录

- startup-diagnostic-01：45 秒原始基线，观察到对白后集中投影产生约 125.9 ms 绘制间隔，并发现主音乐缺失。
- checkpoint-diagnostic-01、02：60 / 35 秒早期方案未能取得请求的检查点，均保留为失败诊断，不计通过。
- checkpoint-diagnostic-03：65 秒，三次真实安全保存成功。后续 restore-diagnostic-01 实际恢复其第 3 号书签，100 秒 4 倍运行到逻辑第二章，初始快照与已存案卷值匹配。
- player-ui-diagnostic-01：180 秒玩家模式，实际 Tab 打开案卷、鼠标进入书签页并点击保存，保存事件成功，Escape 返回现场。
- player-restore-controls-01：65 秒玩家书签恢复，实际 D/A 移动及墙体边界、Tab 案卷、M 地图与返回现场可用，控制者保持玩家。起始 R 推演入口不可用，故未验收推演页或答案提交；后续 Escape / F1 的设置页切换按实际上下文记录，不据此猜测输入卡死。
- verified-summary-01.dvlog 与 evidence-summary-output.log 保留上述诊断摘要；音频重建日志为 audio-generation.log。

## 残留问题与未覆盖范围

1. **书本和绘制间隔仍会卡顿。** 玩家诊断中开书约 0.23–0.28 秒，设置页曾出现约 0.99 秒绘制间隔。最终新开局 / 续跑最大间隔分别为 861.573 / 1002.099 ms，均在检查点副本处理附近；普通后期也有约 50–116 ms 间隔。采集、同步复制与游戏开销尚未分离，这些不是纯游戏保存耗时或同条件 FPS 改善结论。
2. **退出资源警告仍存在。** 两轮均报告 7 个 Texture RID 与 ObjectDB 实例未释放；两个日志重复收集同一警告，数量不重复相加。
3. **不是完整游戏通过。** 未证实正确推演、正确作答被接受、第二章完整案件流程或结局；逻辑第二章已出现，末态仍 active=true。未进行物理手柄验收、所有书签逐一恢复、损坏存档负面测试或长时间稳定性测试。
4. 案卷全等的起点是已写盘侧车，不据此宣称保存前全部 UI 临时字段都无损保留。启动器返回码也不单独证明读档成功或剧情达成。

全新独立上下文审阅者只读审查了六份当前实现、最终派生证据和提取比较脚本，未读取设计标准、未亲自运行游戏，也未独立解码全部原始二进制。在上述范围与限制下，审查结论认为“新开局推进—安全书签—指定书签恢复—继续调度”的证据链充分。

## 继续复测

从仓库根目录执行；输出目录必须新建，保留 .dvsave 与同名 .casebook.dvlog 配对：

```powershell
$env:GODOT = 'C:\Users\iii\Desktop\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe'
python godot_csharp/tools/run_saved_game_capture.py --out artifacts/gameplay-validation-20261021/resume-next-01 --seconds 90 --speed 4 --load artifacts/gameplay-validation-20261021/final-fresh-01/run/checkpoints/checkpoint-003.dvsave --checkpoint 30,60
```

这条路径已实际验证过加载与继续调度。再次测试前核对当前源码、运行程序集、配置与该轮 source-before.json；修改后的运行单列，不能冒充同一冻结版本。完整参数与输入约定见 godot_csharp/tools/SAVED_GAME_CAPTURE.md。
