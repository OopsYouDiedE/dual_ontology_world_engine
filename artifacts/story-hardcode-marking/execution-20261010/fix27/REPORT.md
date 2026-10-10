# fix27 执行报告

## 改动与原因

- 修复第一章流程 provider 的派生值被旧参数冻结；继续流程时重算步数、裁决、下一步，仅显式保留交谈对象。
- 人物协助改走物件附近可达点，完成后复用增量证物卡。地图落点与导航网格共用角色脚印判定，避免终点撞桌沿。
- 修复第二章请求 AST 的对象归属：由发言者内部世界提交，再由正式发言传播映射给接收者；已有认知目标仍以引用传递。
- 章二夹具按当前剧本日程检视物证，傍晚先核对真实对账菜单与时钟，再执行原有证据提交检查。
- 新增六项携带旧参数的答辩回归；更新已过时的 StoryPacketCheck 前置与选择走法，修正两处采样时机。
- 未读取设计规范；未改 UI、布局、样式、Core、`.story`、编译器或生成物；未提交。保留开始时已有三份翻译二进制修改。

下表文件缩写均位于 `godot_csharp/`：

| 缩写 | 文件 |
| --- | --- |
| A | `scenes/Awakening/Script/Chapter1Flows.cs` |
| B | `scenes/Awakening/Script/Chapter1SessionFlows.cs` |
| M | `scenes/Awakening/AwakeningMap.gd` |
| C | `scenes/Chapter2/check/check_flow.gd` |
| R | `scenes/Chapter2/Prototype/Chapter2SocialActions.cs` |

## 原有 27 项失败逐项归类

| 失败名 | 根因 | 分类 | 改动文件 |
| --- | --- | --- | --- |
| E5 assistance starts from the person menu | 邀约迁移遗漏完成后的增量卡 | 真缺陷 | B |
| E7 person invitation completes assistance（未告知代价） | 热点中心在家具内；附近点判定未覆盖角色脚印 | 真缺陷 | B、M |
| E7 协助与闪回后的本人取舍都有记录（未告知） | 上项走位失败，未进入协助流程 | 真缺陷 | B、M |
| huo 自动闪回取舍，已知代价=false | 同上，未触发闪回 | 真缺陷 | B、M |
| E7 person invitation completes assistance（已告知代价） | 同一不可达终点 | 真缺陷 | B、M |
| E7 协助与闪回后的本人取舍都有记录（已告知） | 走位失败，未进入协助流程 | 真缺陷 | B、M |
| huo 自动闪回取舍，已知代价=true | 同上，未触发闪回 | 真缺陷 | B、M |
| E5 person invitation completes assistance（未告知） | 协助发生，但未返回增量卡 | 真缺陷 | B |
| E5 person invitation completes assistance（已告知） | 同上 | 真缺陷 | B |
| E10 person invitation completes assistance | 物件热点中心不可走，且遗漏增量卡 | 真缺陷 | B、M |
| E10 协助与闪回后的本人取舍都有记录 | 走位失败，未进入协助流程 | 真缺陷 | B、M |
| 沈鼎在闪回后自己向铜口问代价 | E10 协助未完成，后续未触发 | 真缺陷 | B、M |
| 请人看过之后只给新得到的那一句 | 邀约路径未复用 OpenCard(id,true) | 真缺陷 | B |
| 陈述不对：回答的是别的问题 | TryAdd 保留旧 wrong_show，不重算当前裁决 | 真缺陷 | A |
| 门开：S11 结尾 | 旧裁决与步数阻止答辩结束 | 真缺陷 | A |
| 门开：隔离门画面换为开启 | 终局未进入 | 真缺陷 | A |
| 门开后移除门外遮罩 | 终局未进入 | 真缺陷 | A |
| 通常开门路线显示门外痕迹 | 终局未进入 | 真缺陷 | A |
| 结局时地名显示“门厅” | 终局未进入，仍在答辩 | 真缺陷 | A |
| 通常开门路线保留手印追问 | 终局未进入 | 真缺陷 | A |
| 通常开门路线显示侦探心声 | 终局未进入 | 真缺陷 | A |
| 通常开门路线按回忆状态选择台词 | 终局未进入 | 真缺陷 | A |
| 结局列出取回者与没有往下想的人 | 终局未进入 | 真缺陷 | A |
| 本次写入 chapter1_end.json | 终局未进入，保存指令未执行 | 真缺陷 | A |
| 正式 .story 开场、首课与自由行动台词 | 新剧本将首课移到次日上午 | 断言过时 | C |
| 章二第一个可操作状态在讲堂 | 新剧本起点为起居厅 | 断言过时 | C |
| 到案发次日傍晚 | 换场后检视旧地点导致物证为空；同行 AST 归属错误；进入傍晚即启动对账 | 走法错误＋真缺陷 | C、R |

第一章这 24 项原断言全部保留。依据：`story/第一章/阶段与流程.story:109–116、158–203、218–239`；`d9afe5f1` 引入人物邀约与 TryAdd，派生值不能沿用上一轮答案。

## 所有既有断言及走法调整，供复核

以下 `.story` 相对路径均以 `godot_csharp/scenes/Awakening/Script/` 为根。

| 位置／原检查 | 调整与保留的性质 | 依据 |
| --- | --- | --- |
| C：handoff 同时出现 arrival/lesson/free | 开场只出现 arrival；次日上午另检查 gather/lesson/free 实际经过 UI | `story/续篇/08_流程.story:16–23、46–54、95–103` |
| C：初始地图 lecture | 改为 living_hall；出生点可走、人物数量 >3 不变，仅相应改检查名称 | 同文件 `21、40–44`；`70a35c8a` |
| C：多地图检查后恢复 lecture | 恢复 living_hall，全部 17 张地图的出生点检查保留 | 同文件 `16–23` |
| C：始业日 collect 后去物流检视 | 就地等待到夜里；次日首课与配给演完才检视告示；增加相识、晚饭、入夜、丙丁区及库廊检查 | 同文件 `29–69、97–103`：晚饭会换到 DormLife，并自动结算 |
| C：到案发次日傍晚、对账及提交全对 | 原条件不变；跨入傍晚后先停在菜单检查时钟，再设置原代选策略；不额外提交 stay | 同文件 `88–90、104–106`；`Chapter2InteractionSession.cs` 的 Run→Pump |
| C：同行行动 | 保留正式 action；增加物证非空与接受后时段 +1 检查 | `Chapter2InteractionSession.cs` 的 Action/Commit；R 的 HostPrepareSocialAction |
| flow：同伴按实际站位听 | 改比 tuning_fork 输出前最近正式输入上下文，仍检查没有同伴 walk/place；不比对白结束后的站位 | `Chapter1SessionFlows.cs` 的 SoundTest：到达回执后计算听闻，再演对白；现场允许让路 |
| flow：R-07 磨刀石文本 | 文本断言不变；从等 3 帧改为等待真实证物卡完成 | `chapter1_fixture.gd` 的 settle_card；避免读到前一弹窗 |
| StoryPacketCheck：回忆菜单两项 | 增加 price_heard 前置，原两项可选、沉默无效果、告知有效果断言不变 | `story/第一章/场面/04_低语与回想.story:23–31` |
| StoryPacketCheck：旧“先问问铜口”选项 | 在已确认恰好两项后，选沉默以外的真实选项，继续核验 show_rule | 同文件 `27–29` 的当前告知选项 |
| StoryPacketCheck：同伴比手立即开放 | 先核验仅自比并检查实际命令，登记自比完成后再核验同伴开放 | `Chapter1Interaction.cs:35–50` 的现有先自比门槛；命令断言保留 |
| StoryPacketCheck：离场后返回 aside/card | 检查移除后的候选抛 ArgumentException，且不登记比对效果 | 同文件 `43、64`：不在场者不列入候选；仍验证过期操作没有效果 |

新增六项参数回归覆盖：旧步数→当前问题、旧错误类型→当前错误、改正后成功音效、正确问题回馈、仅推进一步。测试使用正式编译文档与运行时，不改生成物。

## 验证结果与日志

- 开始分支：`codex/fix27`。原始失败复现日志：`tmp/fix27/flow-baseline.*.log`、`flow2-baseline.*.log`。
- 初始统一命令：`python tools/verify.py --skip gallery --keep-going --godot <指定 Godot>`；core/scope 通过，host 默认并行构建无诊断失败，Godot 初始被 appdata 写权限阻止。原始日志保留于 `artifacts/project-audit/verify-logs/20261010-002822-929597-24048/`。
- 途中独立复测的失败日志保留于 `tmp/fix27/final-*`；没有用旧通过日志代替修订后的验证。
- 最终全新独立 Agent 的结果（原始字节日志 `tmp/fix27/accepted-*`）：

| 检查／命令 | 最终结果 |
| --- | --- |
| `dotnet build csharp/Core.sln` | 退出 0，0 警告／0 错误 |
| ScopeRegression Release build＋`--no-build` run | 退出 0，24/24 通过 |
| 宿主 `.sln -m:1 --disable-build-servers` | 退出 0，0 编译错误；2 项 NU1900 为沙箱无法访问 NuGet 漏洞源 |
| StoryPacketCheck Release build＋`--no-build` run | 退出 0，181 条 PASS，无 FAIL |
| 正式 flow（独立 Agent） | 136/136，退出 0，stderr 空，引擎错误／泄漏模式均 0 命中 |
| 正式 flow2（独立 Agent） | 109/109，退出 0，stderr 空，引擎错误／泄漏模式均 0 命中 |

- `.story` 与编译器未修改，无需重新生成；构建期间 `compile_story.py --check` 显示 7 份输出均与源一致。
- `git diff --check` 通过；禁改 UI 目录未产生本轮修改。

## 环境限制、未完成项与风险

- 默认宿主构建只输出失败、0 警告/0 错误；串行 `dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers` 通过，保留两种结果，不归为代码通过的默认构建。
- Godot 使用工作区内 APPDATA 与临时 PEM override 绕开沙箱的用户目录、根证书读取限制；正式回归使用 `--headless --max-fps 60`，物理运动按实际秒执行。临时 override 已移除。
- 初次统一回归 Godot 在 appdata 拒绝写入后退出 0xc0000005，汇总缺失；修正工作区 APPDATA 后重试，初始失败日志保留。最终独立 flow/flow2 均退出 0、汇总完整，无退出崩溃，不需重试。
- 仅执行自动回归；一级三份全程记录、二级 UI 专项、三级盲玩未执行。人物行为审美与真实游玩体验未验证。
- 请求的目标筛选分支未逐个专测；本轮验证正式同行及既有回归覆盖的社交流程，未更改角色倾向或剧情规则。
- 本轮没有新增生产或正式测试 DEBUG 打印；临时 `debug_help.gd` 已删除，调试原始日志保留。
- 提交、合并、最终 Godot 渲染验收由主代理负责。本轮没有更新剧本产物或触碰已有翻译二进制。
