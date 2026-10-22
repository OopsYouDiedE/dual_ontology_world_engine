# Foolish 本人策略候选审查（2026-10-22）

## 范围和结论

分支 `opt/fix-foolish`，基于 `77e31772`。本候选只修改 30 通用收益评分、40 侦探本人策略配置及工程说明；未改 NPC、答案资格、判答、门闩法则、规划循环。子代理未读取设计文件，采用主代理提供的工程与人物边界。

结论：Standard 在本轮正确提交四问并自行开门。Foolish 本轮没有提交任何答案，雀儿实际挑开外闩；但侦探没有拉开内闩、没有过门，其他乙区成员没有发表目标所需的实际怀疑和不满。目标只部分实现，不能合并为完成修复，也不能将后续全局章节可用事件算成侦探实际交接。

## 实现审查

`dvp_personal_motivation` 用本人 value_order 的首个非零影响评分 personal_choice，避免一切 personal_choice 均无条件算正进展。通用实现不读取 profile，也不改变执行资格。

`dvp_inquiry_initiative` 只读本人 empirical_initiative/counterfactual_initiative，缩放 inspect/experiment/sound_test/replay 的预期信息和验证收益。默认 1，Standard 1/1，Foolish 0.01/0.03。数值是待评估的策略参数；本轮不能证明这是唯一或最佳参数。未继续调权来凑剧情结果。

## 本轮实际证据

同一源码清单（Standard/source-hashes.json 与 Foolish/source-hashes.json 完全相同），seed=1，zone_yi，formal-model，prune_depth=2，TopK=3，全部既有优化默认开，运行上限各 300 秒。测试错开，Foolish 在继承 Standard 运行及只读检查进程已结束后才启动。此次保留继承证据，不重跑 Standard。

- Standard：接受事件 e1462（第1日08:21）、e2112（09:45）、e2266（10:05）、e2504（10:29）；正文分别为闩门者、声源、原话、信任缘由四问。第1日10:33 拔内闩，10:34 开门，10:36 实际进入起居厅。raw-events.dvdag 37,385 个提交事件，有 initial/final checkpoint 和 seal，complete_sealing=true。继承的 Standard 只读因果检查进程曾到 domain-check-start 后消失，result.dvlog 缺失；不得声称完整工具成功。
- Foolish：没有 answer_submitted/answer_accepted/answer_rejected 事件。e1057（第1日08:14）等 planning_stop 报 `selective-no-supported-task-within-search`，最佳重问 net_score=-0.01。侦探在 e1371（08:19）自介后至第二日之前没有新的实际行动。
- Foolish 的雀儿：e2091（第1日13:17）实际 `quer.departure.pick-outer`，status=applied，实际用时17游戏分钟。未发现任何侦探 draw-inner/open-door/cross-out，也未发现 su.picked-rebuke/shen.picked-doubt/huo.picked-cold 的 action_execution。
- Foolish 实际会话172.907现实秒，因旧版 `actual-chapter-two-closing-record` 停止于第5日09:30；这个停止条件当时不要求侦探实际看到章末。3万余事件与二进制正文均保留。33,072个提交事件，raw_sealing_complete=true，source_unchanged=true，evidence_complete=true。只读因果检查完成，main_causal_supported=false；这符合本轮没有完成第一章实际答辩的记录，不能包装成剧情成功。

全文见各配置 `02-引擎输出的玩家可见动作.txt`；定位和完整原始值见 `actual-event-index.dvlog`、`stdout.dvlog`、`raw-events.dvdag`。未提交巨大 raw、编译工程与逐事件投影，只保留本小报告进 Git。没有 Godot 几何、画面或输入验收。

## 失败根因与后续边界

当前低实证收益使本人无正价值任务而停下，而非只保留错误推演。侦探停在活动厅；雀儿从起居厅一侧挑闩的现场声音没有被在场侦探收到。门闩、NPC 后果仍要求实际位置、本人已知条件及正式动作，因此不能宣称他已经“等人开门后出去”。

现有三个 `picked-*` NPC 对话的正文分别描述横闩金属响、门槛灰迹和铁丝划痕，且都要求成员实际在起居厅；这些声明不能被当作“怀疑侦探能力/对侦探不满”的证据。不得靠预置态度、NPC 读取 profile 或强制移动补齐。本次未扩大到这些内容区域。

主线随后已加入更准确的章末实际观察边界与默认深度变更；本轮源码仍为封存的77e31772加本候选，不冒充新主线测试。后续若调整本人策略或已授权内容，应重新冻结全部来源，并按固定目录整体替换本次测试输出。
