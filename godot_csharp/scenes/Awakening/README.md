# 第一章《门的这一侧》（重制版）

标题入口 `scenes/Title/Title.tscn` 选第一章后，经 `scenes/Main2D.tscn` 加载本场景 `Awakening.tscn`。设计依据为 [docs/design-standard.md](../../../docs/design-standard.md) 第二、三、五部分。旧章节已退出运行目录，语言设置与翻译资源统一位于 `scenes/Shared/Locale/`。

## 运行

```powershell
dotnet build godot_csharp/DualOntologyWorldEngine.sln
& $env:GODOT --path godot_csharp            # 有窗口游玩（默认场景）
```

新 worktree 先执行一次 `& $env:GODOT --headless --editor --path godot_csharp --import`。

操作：鼠标点击移动与交互（点可交互物件一律走过去并打开，站在够得着处再点直接打开；悬停时只描鼠标所指的那一件并显示名称；点到普通家具、窗、拖鞋、地毯等只给一句极短心声，同一件只说一次；人与人不互相阻挡，被挤到的同伴会让开）；WASD 移动；E / 空格 / Enter 检视或交谈（对白刚结束的 0.6 秒内多按的一下不触发新的交谈）；Tab 笔记本；R 重演手稿（第一次看到闩上的门后开放）。对白一次空格推进，H 回看；已问过的对话选项变灰。证物卡上可做现场试验、请同伴协助、请本人触物回忆；任意键返回现场。回放中空格暂停、F 倍速、Esc 跳过（跳过与看完的结算相同）。答辩在活动厅铜口处进行。

## 检查入口

| 检查 | 命令 | 说明 |
| :--- | :--- | :--- |
| 解空间穷举、感知隔离、重演不改现实、回忆只影响本人 | `dotnet run --project godot_csharp/scenes/Awakening/check/ReplayCheck` | 编译游戏内同一份 `Night/*.cs`，跑 216 种组合；输出写入 `artifacts/remake-belief-replay/solution-space.txt` |
| 流程跳转回归 | `& $env:GODOT --headless --path godot_csharp --script res://scenes/Awakening/check/flow_check.gd` | 直接跳到醒来、广播后公告框、门厅反转、“不妨一试”、同伴回忆、声路试验、重演吻合、答辩、门开（起居厅窄底鞋印、结局名单）、对白后多按空格、退出考题；对白由脚本代推进 |
| 关键画面截图（开发自检） | `& $env:GODOT --path godot_csharp --script res://scenes/Awakening/check/state_gallery.gd` | 有窗口；PNG 写入 `artifacts/remake-belief-replay/gallery/` |
| 乙区地图重建 | `python godot_csharp/tools/build_awakening_map.py` | 以第二章 `rooms.zone("乙")` 的几何画地图，写 `ShareAsset/Internal/Props/AwakeningEvidence/life_wing_yi.png`、`map_layout.tres`，并就地重写 `Awakening.tscn` 的地图节点（灯、墙、家具、门）；之后需 Godot 导入 |
| 证物美术重建 | `python godot_csharp/tools/build_awakening_evidence.py` | 原生像素配方 `tools/first_awakening_art/awakening_evidence.py`；同时写 AssetsSource 与 ShareAsset，之后需 Godot 导入 |

以上都是注入状态的自动检查，不替代独立盲玩。

## 盲玩驱动

```powershell
& $env:GODOT --path godot_csharp --rendering-method gl_compatibility --script res://scenes/Awakening/check/playtest_session.gd -- --session=<新id> --agent=<名字>
python godot_csharp/tools/blind_playtest.py --session <新id> snapshot
python godot_csharp/tools/blind_playtest.py --session <新id> action --json '{"type":"tap","key":"Space"}'
```

会话目录：`artifacts/remake-belief-replay/blind-<id>/`（截图、`timeline.jsonl` 输入轨迹、`status.json` 可见文字与控件）。驱动只注入通用鼠标键盘输入，不读写剧情状态。每次截图前等镜头连续 8 帧不动（最多 240 帧），截图里的坐标与下一次点击时的镜头一致。

## 文件

| 文件 | 职责 |
| :--- | :--- |
| `Night/NightVariables.cs` | 六个变量、人物与房间、声路表（§4.2 感知表）、声路试验可用的句子 |
| `Night/NightReplay.cs` | 昨夜重演：每次新建 `WorldRuntime`、四个 `SoulEssence` 与各自 `ProjectionWorld`；声路感知经 `Observation.Observe`；人物习惯是 `Rule`，由 `DecisionSystem` 选择；请求经 `Effect.Say` 携带，`SocialSystem` 依 Trust 判定；行动由 `SimulationSession`/`ActionRuntime` 在 CTB 上结算；宿主层只做寻路（走到行动目标所在房间、到了先看一眼再决定）与烟灰/鞋印/湿布的物理后果。“苏泠守在门边直到门关上”“沈鼎只在门已闭合时压湿布”是规则条件，不靠行动值先后；`NightTiming` 列出全部行动值参数，穷举检查在 39 组参数上复测。心声气泡用自然语言写出决策依据（信谁、为何不全信），不显示数值 |
| `Night/MorningTraces.cs` | 重演留下的晨间痕迹、今晨现实的证物事实（作者给定），以及对照（吻合／冲突／未查）与冲突文字 |
| `Night/MorningWorld.cs` | 清晨的现实世界：证物 Essence、四人的认知与核心欲望；公告框上的学园守则四条；恶魔的话以 Corruption 来源放大欲望；恶魔低语之后，同伴要不要自己走过去拿起旧物（`Intend`：拿起它／守着手头的事）与碰不碰旧物，都由其内部世界的 `Rule`（低语、欲望、对恶魔的信任、天性谨慎、是否读到守则第 1 条、是否已拿定主意）经 `DecisionSystem` 决定；伸手拿起与放回由 `SimulationSession`/`ActionRuntime` 结算（人不在物件所在房间就拿不到；放回后现实里留下“被动过”）；声路试验（感知）与触物回忆（Recall，记钩）只写入相应本人的认知 |
| `AwakeningEngine.cs` | GDScript 与 C# 之间的桥：重演、声路试验、回忆、钩数、章节结束状态 |
| `Awakening.tscn` / `Awakening.gd` | 生活翼地图（几何、寻路、相机复用旧场景的通用部分），S1–S10 流程、证物卡、笔记本、声路试验、触物回忆、始业之答、结局 |
| `map_layout.tres` | 地图生成脚本的输出：地面格、家具足迹、书架积灰的种类与位置、窗与地毯等壁饰的心声区域 |
| `AwakeningContent.gd` | 加载 C# 内容与生成布局，转换 Godot 坐标和命中框 |
| `ReplayManuscript.gd` | 重演手稿：左页六个变量，右页对照；改变量后旧结果标为过期 |
| `ReplayStage.gd` | 墨影回放：真实地图上的半透明人影与四个并排的心声气泡；暂停、倍速、跳过 |
| `TraceDecals.gd` | 传声铜管走向、管接头、灰扇、靴印、湿布、门框凹痕、书架无尘方框与窄手印 |
| `Script/chapter_01.story` | 两章对白、分支、证物事实与热点的唯一生产源（根文件）；可用 `引入：<相对路径或通配>` 拆成片段，按序引入与写在同一文件等价；每句台词行尾带 `[#id=…]` 稳定编号，新台词用 `python godot_csharp/tools/compile_story.py --assign-ids` 写回 |
| `Script/generated_lines_zh.csv` | 编译器生成的台词表（编号、场面、说话人、中文），供翻译使用；不手工修改 |
| `Script/Chapter1Dialogue.cs` / `Shared/Story/InteractionPacket.cs` | 纯 C# 对白查询、效果、选择与交互协议；不依赖 Godot |
| `Script/generated_awakening_*.json` / `Script/Chapter1CompiledDialogue.cs` | 由 `python godot_csharp/tools/compile_story.py` 生成；`--check` 检查一致性，不手工修改 |
| `Paper.gd`、`HintOverlay.gd`、`LampFlicker.gd`、`RoomDoor.gd`、`FieldNavigation.gd`、`AwakeningDialogue.tscn` | 手稿样式、交互高亮、灯光、宿舍门、网格寻路、对白框（后四者复制自旧场景的通用节点） |
| `check/` | 上表的检查与盲玩驱动 |

美术：`ShareAsset/Internal/Props/AwakeningEvidence/`（运行时）与 `AssetsSource/Internal/Props/AwakeningEvidence/`（生产源），共 18 张场景贴图（含公告框、门开后的窄底鞋印）与 12 张检视卡局部图（96×72，卡上 3 倍显示）。

## 与设计方案的对应

- **证物 12 件**：E1–E12 见 `AwakeningContent.gd`。可做现场试验的：E1 拔闩推门、E2 木楔比对、E3 检视自己的靴底（触发第一次反转）、E8 声路试验、E11 比手；同伴协助：E4/E9/E10 沈鼎、E5/E6 苏泠、E7 霍铎。
- **重演对照**：E1–E7、E9 参与对照；E8、E10、E11、E12 是旁证（E8 的声路试验与 V3/V4 的感知规则是同一张表）。
- **第六个问题**：重演手稿起初只有五个问题，“素不相识”是不写出来的默认；重演里侦探因信任不足拒绝转述，或细看过两件相熟旁证（杯子、酵种罐、无尘方框、放大镜）之后，才出现“昨夜之前，四人彼此……”。对照的冲突只写痕迹之差（“重演中……；现场……”）。
- **唯一解**：穷举确认 216 种组合中只有“她的房间 / 活动厅铜口旁 / 从主楼对着铜口说 / 闩门，别开 / 霍铎 / 早已相熟”全部吻合。
- **第二次反转**：V5=霍铎、V6=素不相识时，侦探对霍铎转述的请求由 `SocialSystem` 判为 Negotiate，回放中他的心声气泡是“我凭什么信一个昨天才认识的人？”；改为“早已相熟”后全部吻合，触发 S8。
- **开场**：S3 恶魔只称代管者与教师，不说自己是什么，不提导师与契约，不设“初涉斯地”的替换句（第五轮修订）；广播结束时镜头带过活动厅公告框，学园守则四条完整呈现一次（任意键翻过），之后可随时再读；S4 核验报偿时恶魔逐一点到四人的欲望（Corruption）。全章台词与公告框不出现第 9、10 条（流程检查逐字扫描）。
- **同伴自己会去碰（涌现）**：恶魔对某位同伴低语后（玩家第一次打开他的旧物时当面说出；或调查过四件后，趁侦探不在那个房间时说，玩家听不见），此人的认知里多了“铜口对我说的那句话”，由他自己决定要不要走过去拿起旧物。侦探在场时可以劝阻（经 `SocialSystem`，霍铎、苏泠与侦探素不相识，不听；沈鼎听）、说明第 8 条或任其自便；不在场时他独自取舍（沈鼎犹豫无人可问便放下），侦探只会看到物件被动过的痕迹、他事后的神色，回来时他说“我想起了一点”。
- **触物回忆（第一次“写”）**：音叉（苏泠）、石板（霍铎）、酵种罐（沈鼎）由同伴自己决定碰不碰；玩家第一次打开这件物件时，恶魔先说一句朝向此人欲望的“不妨一试”（每件一次、四句各不相同，以 Corruption 写入此人认知）；回忆选项标注“（守则第 1 条）”；同伴先走过来再决定；读过公告框后，玩家可在他决定前把守则第 1 条（“寻回者，自负其果”）指给他看，或什么都不说。三人倾向各不相同（设计 §5）：霍铎不论是否被告知都会碰（“图纸值这个价”）；苏泠未被告知会碰、被告知则拒绝；沈鼎未被告知时犹豫并反问“你说呢？”，侦探的回答（劝碰／劝别碰／由他自己）经 `SocialSystem` 判定后写进他的认知、再由他自己取舍，被告知则拒绝。插闩（侦探）由玩家决定，可拒绝。回忆把对应变量标为“回忆所证”，不替玩家改变量；钩数改变恶魔对此人的称呼与苏泠、沈鼎的结尾台词；结局卡列出“留在恶魔那里的钩”与“你没有交出的”，写入 `user://chapter1_end.json`。
- **始业之答**：铜口对话；第 1—4 步须陈述与出示成对（“陈述”“出示”两列各选一项后“作答”）；到第 4 步为止，不设反问（第五轮修订）。
- **乙区布局**（第五轮修订第 7 条）：西端门厅（北厕、南储物），隔离门在门厅西墙、开向起居厅；东接活动厅（北）与备餐间（南），再往东是中廊，两侧各三间房：北排 R-08、R-05，南排 R-07、R-06，外端两间空房床罩防尘布、灰落得均匀。昨夜重演的拓扑与此一致：住房开向中廊，中廊通活动厅，备餐间与门厅只经活动厅出入。
- **声路**：铜管从铜口出发，一路沿活动厅北墙进门厅、贴墙下到隔离门北侧门框（松动接头），一路钻进活动厅与 R-08 之间的墙（松动接头）；声路试验可在铜口旁、R-08、门边、备餐间、R-05 听；同伴只报听到的原字与声音来处的方向（如“……开门　从那边。（指向门）”），不说“像在门外”。重演对照的“未查”只说区域（如“门厅门框一带还有东西没看过”）。
- **门开**：恶魔须显形才能抬外闩，门开后起居厅的烟灰上露出一行窄底尖头的鞋印，从厅的北头走到门前又折回。细看过架上窄手印的人可以多问一句“架上的手印，是你的吗？”，恶魔答“诸位授权封存之物，是我取走的”，不提条款。
- **酵种罐**：罐壁上一层摞一层的旧面痕，数不清；第一章证物只证明“早已相识、不是昨天才到”，不能断定住了多久。
- **鞋印比对**：靴印证物卡上由玩家选择比对谁的鞋底（四人）；只有侦探自己的对得上。
- **R-06、R-07**：各有一处本人的生活痕迹（霍铎床头挂钩上的油渍、沈鼎磨得凹陷的磨刀石），不编号，作相熟与久居的旁证。
- **退出考题**：S4 的末位选项，以及铜口菜单末位“不作答，在生活翼歇息”。

偏离与待裁定事项见 `artifacts/remake-belief-replay/implementation-report.md`。
