# 第一章 Godot 回归入口修复

本轮只修改检查夹具、检查脚本和对应运行说明。没有修改游戏逻辑或 `.story`，没有读取 `docs/design-standard.md`，没有提交。所有运行验证由新的独立上下文 Agent 执行；每个并行 Godot 检查使用工作树内独立 APPDATA。

## 入口修复

- `godot_csharp/scenes/Awakening/check/chapter1_fixture.gd:68`：加载 `Host/Main.tscn`，通过标题页的开始按钮信号发送 `start`，让 Main 完成正常 `view_ready` 握手。默认略过醒来时实际推进对白，不再设置无读取者的 `skip_intro` 元数据。先验证资源、实例、引擎、标题页、UI、现场和玩家，加载失败请求非零退出并停止后续调用链。
- `godot_csharp/scenes/Shared/check/ChapterProbe.cs:13`：仅供检查的适配器，访问统一宿主持有的既有 `IChapterProbe`，复用宿主 Variant 转换。没有给正式宿主添加状态注入入口。此处对两个 `_session` 字段及转换方法的反射是测试边界，宿主内部结构变化时需同步维护。
- `flow_check.gd`、`state_gallery.gd` 和 `Shared/check/chapter1_smoke.gd` 统一复用夹具；重演改用 `host.dispatch`。旧源码扫描路径改为 `Host/Main.gd`。
- Gallery 的找不到对白/弹窗、遗漏回放/终局截图现在会计入失败，退出非零；报名表单实际填入姓名。冒烟检查的章末导航选项改为当前 `continue`。
- 同步更新 `godot_csharp/AGENTS.md:98` 和第一章 README 的检查入口说明。Godot import 从已有 CSV 重新生成三份 `.translation` 资源；未手改生成物。

## 替换的旧行为断言

1. 移除必须存在 `nudge_E1/E5/E7/E10` 的标题断言；改为验证 `nudge_*` 已移除、检视不触发低语，调查和换房不产生低语或自主回忆。`nudged` 的残留内部标记不等于玩家听到了低语；另核对 `whispered`、`life` 和 `recalls`。
2. 移除证物卡的主动 `recall` 操作以及低语后主动伸手的路线；改为 E5/E7/E10 的 `help` 后自动闪回，E1 的 `experiment` 后自动闪回。保留本人是否取回、钩数和重演变量不被代选的检查；代价说明根据本轮场面和 `price_heard` 进入，沈鼎自己向铜口询问。
3. E8 试验不再默认存在；核对 E7 + E6，以及 E7 + E5 协助，两条认知前置。表单实际选择侦探的听位，并核对实际房间和每人的所闻。删除第二轮小表与只说一次点评的旧断言；每轮都检查演出。
4. 不再假定同伴被试验搬到固定房间。核对正式输出中没有同伴 `walk/place` 调度，并把所闻房间与实际现场上下文对齐。现场既有 `_make_way()` 会让挡路者局部挪动 28 像素，因此绝对坐标不变不是正确的试验调度断言；仍打印前后坐标作证据，没有修改让路逻辑。
5. 新增无需重演的答辩陈述检查：按检视的相关证物开放各种说法、两件相熟旁证开放第六问和信任陈述，重演次数保持零。
6. E11 先只列侦探自己，完成自己的比对后才列架旁同伴。终局手印分支实际逐人完成比对，不把“已经检视”当成“已做试验”。
7. 移除歇息通关路线，改为核对铜口菜单没有 `rest` 且不会结束章节。
8. 反转心声、第六问心声、手印追问、结尾对白、醒来截图改按稳定剧本行号取本轮原文，避免旧字句造成失败；声路方向词改为当前说法。窗户测试从当前地图装饰区域取坐标；生活痕迹采用可行走的近处站位并等待可见性刷新。

## 验证结果

末轮 Flow 和 Smoke 已完成，不再出现原先 600 秒的空转超时。最终差异检查 `git diff --check` 通过。

| 检查 | 结果与关键输出 | 本轮证据 |
| --- | --- | --- |
| .NET 构建 | 成功；默认多节点还原失败，使用工作树内离线包缓存和 `-m:1` 后构建 0 错误、1 个已有 CS0414 警告 | [构建诊断](restore-build-diagnosis.md) |
| 剧本编译一致性 | 通过：`generated files are current (7 outputs)` | `independent-story-check-20261008T202100Z.log` |
| 缺失场景快速失败 | 控制流通过：1.293 秒内退出码 1；明确场景不存在，无 `UNEXPECTED_RETURN`、无 Nil 连锁异常。失败退出仍有 ObjectDB/1 个资源未释放警告 | `fixture-missing-load-round3.log` |
| `verify --only flow` 首轮 | 129 项、10 失败，154 秒；检查实际执行，无脚本错误或超时 | `flow-round1.log` |
| `verify --only flow` 第二轮 | 129 项、3 失败，190 秒；保留失败日志，不用首轮结果代替末轮 | `flow-round2.log` |
| `verify --only flow` 末轮 | **FAIL**，152.4 秒；`TOTAL 129 checks, 1 failed`，128 项通过，唯一失败为拒绝转述后第六问解锁。无脚本解析/运行错误、无加载错误、无超时。根证书库启动错误另行原样保留 | `flow-round3.log`、原始通道 `artifacts/project-audit/verify-logs/20261008-204147-607582-23772/flow.{stdout,stderr}.log` |
| `verify --only flow2` | **FAIL**，约 0.56 秒；`Chapter2/Game/check_flow.gd` 不存在，当前文件为 `Chapter2/check/check_flow.gd`。实际第二章检查未执行；未扩大本轮范围迁移第二章旧宿主 | `flow2.log` |
| `verify --only gallery` | **FAIL（环境错误）**，245 秒；`TOTAL 31 shots, 0 failed`，回放和终局标记全部 true。实际查看代表性 PNG，内容已显示。统一扫描保留两条 WASAPI 初始化错误和一条根证书库读取错误 | [Gallery 独立报告](gallery-verification.txt)、`gallery.log`、[截图元数据](gallery-verification.json) |
| 第一章冒烟末轮 | **断言通过**，34.2 秒；`共 135 句；失败 0`，原生进程退出 0。章末 `continue` 入口通过。仍保留根证书库 ERROR，因此不能把断言通过当成统一错误扫描通过 | `smoke-round3.log` |

每次 `verify.py` 原始 stdout/stderr 另存于 `artifacts/project-audit/verify-logs/`；本目录合并日志和详细独立报告保留全部失败。`.gitattributes` 禁止日志换行转换。

## 未修复的游戏代码问题

末轮保留失败断言：`重演中侦探因信任不足拒绝转述后，手稿出现第六个问题`。

- `godot_csharp/scenes/Awakening/Script/story/第一章/文案与守则.story:78` 当前 `夜间·怀疑·生人` 是“我凭什么信一个素不相识的人？”。
- `godot_csharp/scenes/Awakening/Script/Chapter1Flows.cs:69` 的 `ReplayRefused()` 读取这个剧本键，并在重演心声中匹配它。
- `godot_csharp/scenes/Awakening/Night/NightReplay.cs:661` 仍硬编码输出“我凭什么信一个昨天才认识的人？”。两者不一致，拒绝分支未识别，因此没有 `replay_refused` 演出，也没有开放第六问。
- 本轮运行证据：`V6_REPLAY doubt=我凭什么信一个素不相识的人？ matched_thoughts=[] open=false flags={ } seen=墨影散了。`。同一末轮的另一路“检视两件相熟旁证”正常开放第六问。

没有修改上述游戏文件，没有删除或放宽这一失败断言。应由负责游戏逻辑的任务消除旧硬编码，并重新执行本检查。

## 范围与未验证项

这是注入式自动回归，不是一级全程文本对齐、二级 UI 专项验收或三级独立盲玩，三者均未执行。人物行为合理性和游玩体验未评价；Core/scope 未单独回归。已执行第一章机制子集、实际 Godot UI 驱动和 Gallery 截图查看，不能据此宣称整体验收通过。
