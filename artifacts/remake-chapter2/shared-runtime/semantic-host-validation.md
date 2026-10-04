# 第二章独立局部宿主语义验收

2026-10-04。本轮为全新独立 Agent 的局部自动自检，不是盲玩、完整通章或全量回归。没有读取设计文件或历史报告，没有修改业务源码，没有构建 C#。使用原默认 Chapter2Game.tscn、实际 OpenGL 窗口与原桥队列；自动推进与对白揭示不用于宣称玩家有效时长。

共 26 项实际局部断言：25 项通过，1 项失败。默认两章实际窗口启动均无脚本解析错误；第一章退出有 ObjectDB 泄漏 warning，且该启动发生在 E11 嵌入文案增量构建之前，本报告不覆盖新 E11 文案。

## 实际通过

- C2 默认实际窗口启动；不存在 Console/CommandLine 节点，/ 与 J 无入口。
- HUD 忙碌时隐藏键位提示。Q 仅提供现场动作、当前公告、笔记和对白回看，无输入、传送、INFO 物件列表或全局歇。
- R 空候选显示“先走到现场细看”的调查引导，没有三个空变量槽。
- 原 ledger 实体绑定台账；实体回调经原查动作与原对白呈现后打开实际存下的 inspection 卡。截图可见台账图片、原现场事实和“第3日清晨，你在后勤院亲自细看”来源，没有 NPC 广播混入物证卡。
- Tab 展示分组且可点的已知记录。打开事实卡显示来源，打开卡本身不刷新世界状态。
- 第2日五次原歇、第2夜原守夜后勤院、第3晨原查台账、第3日上午原查碗，取得 26 项原 ReplayMenu 选项，呈现 13 组语义问题。没有捏造候选、Examples 或真相 CaseVars。
- 所有 13 组均生成可选按钮，超过六组无越界。零项不允许、一至三项允许、四项不允许。保留原菜单索引与原候选资格。
- 原 replay_ui_run 回调提交真实异步桥重演；第2夜“苏泠认甲”返回台账、门闩、碗三项吻合，以及四项未查。通过原 accept_replay 返回结果页面。
- zone_yi 实际映射 C# DormLife，白天实体椅子出现 rest 动作。
- 原贴生活翼进入 ReadCard，五次单格选择依次到达 CardCell 0—4，再进入原 yes/no 确认，选择否返回 main。

## 唯一失败与最小诊断

“重演前后完整显示快照相同”断言失败。随后仅重跑原桥命令做字段诊断，未重复已验动画、截图或称为游玩验证。

| 字段 | 结果 |
|---|---|
| day、slot、det_place、room、news、frames_revision、frames | 前后完全一致 |
| notebook_cards 中全部物证、人际、听闻、自知卡 | 前后完全一致 |
| rule:3 | name 从“规矩甲”变为“规矩乙” |
| rule:8 | name 从“规矩乙”变为“规矩甲” |
| 两张规矩卡的 facts、sources | 前后完全一致 |

这是规矩称呼一致性失败；已核对的现实字段、事实与来源没有变化，不能把它描述为世界物理改变，也不能将整轮写成全通过。源码原因：Morning.cs 的 PreviewKnownRules 在克隆称呼表中按 DetKnownSpecs 顺序取名；ReplayMenu(preview:true) 与正式 ReplayMenu 按逆序取名，正式重演首次将逆序称呼写入原 _labels 后，笔记卡标题交换。业务源码未修。

## 范围与证据

原完整动作窗口被用户插入任务中断，PID14988 已消失；原输入日志保留 tmp/semantic-host-interrupted.out/.err。确认 E11 构建结束后仅第二章同轮恢复 PID32276；已完成 HUD/menu/空R 检查与截图未重复。中断没有算游戏失败。字段诊断 PID10784 与本轮其余自身进程均已退出；用户编辑器30124未触碰。C2 两次测试 stderr 均无脚本错误。

第一章五/六问题门槛仅对当前源码抽样：replay_ui_data 按 V6Open 返回五或六项，V6Open 依原拒绝标记或两处熟人证据；本轮未重跑第一章该门槛。实体床的夜间 sleep 分支、人物卡使用侦探 projection 的最后见到位置、inspection 保存原 Describe 文本而非 Feed 广播，均已对当前源码核对；床的夜间点击未单独实机执行。

原窗口输出：tmp/semantic-host.out/.err；字段诊断原始证据：tmp/semantic-host-diff.out/.err。测试胶水：godot_csharp/scenes/Chapter2/Game/check/semantic_host_validation.gd。

七张实际视口截图：semantic-c2-hud.png、semantic-c2-menu.png、semantic-c2-notebook.png、semantic-c2-card.png、semantic-c2-replay-empty.png、semantic-c2-replay-questions.png、semantic-c2-result.png。已目视核对 HUD、Q 菜单、图片事实来源卡、结果页。结果截图显示“已过期”是测试直接传 selection 给回调却未通过 _choose 更新手稿 selection 的胶水限制；真实重演已返回，此图不证明正常选项按钮选择后的新鲜状态，也不将这一提示另报为业务失败。
