# 近似模拟接口

2026-10-24 的生产唯一策略为 `simulation_mode="approximate-v1"`。`30-session.dv` 的 `dvs_choose` 直接扫描当前行动，`dvs_prepare_cognition` 直接执行一次声明推理。旧 `dvp_plan`、taskgraph、多层假设选择、付费听者预测准备、自动论证动作生成和完整认知组合已从源码删除。`legacy-explicit` 已退役；付费因果重演、实际听闻、法则求值和正式判答继续保留各自的真实消费者。

## 权威与知情

实际权威值保存在原 physical 单元格中，既有正式法则继续读写同一份权威。`physical.simulation.RealityFacts` 只登记内容明确列出的事实，记录 `{id, ref, RealityFact, KnownByMask}`。`RealityFact` 为当前完整权威单元格；32位掩码来自已绑定主体的实际、无冲突且与当前权威投影一致的心智单元格。掩码计算不授予观察，不把权威复制给主体。未登记事实继续使用既有 DV 读取合同。

`identities` 使用唯一的 `actor/index`。索引0–15为学员，16–31为其他身份；`ScholarMask=65535`。掩码使用0至4294967295的整数，当前 double ABI 能精确表示全部32位整数，算法不使用有符号移位。事实内容和图扩散数值继续遵守原数值类型合同。

`mind.CognitionFilter={hidden_objects, anterograde}`。隐藏对象对本人 `dv_read` 返回未知，叶命题经过相同读取，可见人物/物体目录与新观察过滤该对象。设置该过滤器时叶命题绕开不含该机制的原生快速路径。`anterograde=true` 拒绝新观察，跳过当前认知更新，并在本人 Complete 后保留原 mind；权威效果、执行费用、其他现场见证者与行动审计仍保存。执行账本和成功次数属于正式机制记录。

## 频率与动作

| 频率 | 当前入口与语义 |
| --- | --- |
| 宿主帧 | Godot 保持物理、输入和呈现。DV 不接管逐帧移动。 |
| 近场1 Hz | `dvg_simulation_pulse(state, seconds, visible_rooms)`；seconds为累计单调现实秒。整数秒去重，房间集合扩到声明图的一跳邻室，更新近场唤醒标记。 |
| 宏观15游戏分钟 | 环境推进逐个访问季度小时边界；同步房间浓度，唤醒离场人物的当前日程/效用选择。 |
| 每日06/18 | 在对应边界执行 `daily_rules` 中已采纳的正式 enforce 法则，记录实际应用或拒绝。 |

`pulse` 返回新state，保持pending与正式动作队列；它不推进游戏分钟、不产生动作、不完成动作、不授予知识。游戏内时间只由正式调度和环境边界推进。离场人物每个宏观节拍重新选择，邻室人物受近场节拍唤醒，在场人物保持正式当前候选选择。已排队动作沿原到期回执完成。日程中的 `action_id` 必须是本人的既有动作，其效用附加在当前日内窗口；准入、成本、实际执行与结算仍走共同动作链。

近场当前实现房间范围与主体唤醒，声学和气压的连续模拟尚未实现。范围未变的空节拍不追加事件；范围变化记录 `simulation_nearby_scope`，实际选策/执行由其正常事件记录。保存/恢复后的宿主单调秒起点由会话桥接层对齐，DV继续拒绝倒退时刻。

近似选择枚举当前合法候选，使用未知相关观察的预期价值、本人已有动机、明确日程效用和已声明成本。未曾成功使用的移动可取得探索效用；它不预测目的地内容。并列保持原声明顺序。没有正效用候选时让出，明确现行义务和保护预约使用其正式资格。记录策略、候选扫描量和零搜索展开；不填写全局最优或完整搜索证明。

## 内容声明

`campaign_simulation_config(scene)` 由35提供默认声明，`config.simulation` 可在打开前显式覆盖。扩章更新声明，计时、锁和实际世界沿原状态继续。内容结构如下：

```dv
{
    "identities": [{"actor": "resident", "index": 0}],
    "filters": [{"actor": "resident", "hidden_objects": [], "anterograde": false}],
    "facts": [{"id": "fact-id", "ref": dv_ref("object-id", "state", "field")}],
    "rooms": [{"id": "room-id", "concentration_ref": dv_ref("room-id", "state", "concentration"), "height": 0, "volume": 1, "settling": 0}],
    "edges": [{"from": "room-id", "to": "other-room", "opening": 0, "conductance": 0, "wind": 0, "downward": 0}],
    "daily_rules": [{"id": "ledger-id", "at": 360, "law": "adopted-law", "category": "food"}],
    "offscreen_schedules": [{"actor": "resident", "action_id": "declared-action", "from": 360, "to": 1080, "utility": 1}],
    "dangers": [{"id": "danger-id", "room": "room-id", "fact_id": "fact-id", "when": dv_true(), "affected_mask": 1}],
    "demon": {"actor": "protector", "location_ref": dv_ref("protector", "state", "location"), "travel_actions": [{"room": "room-id", "action_id": "declared-move"}], "protect_action_id": "declared-protect", "release_when": dv_not(dv_true())}
}
```

这是结构示例，所有身份、法则与数值都要由实际内容声明。`opening_ref` 可替代边的固定 opening，必须读取已知0–1开口值。`conductance` 是每15分钟步的系数，wind在-1至1之间，downward非负，volume严格为正，settling在0–1之间。全部边先读取同一旧浓度；差值、风向与高差生成定向质量流，超出源房间总质量时同比限幅，统一提交后扣除沉降。该离散浮点模型标记近似，未提供连续输运误差界。

日账 `at` 仅接受360或1080。category是记录分类；粮食、作物、侵蚀及槽位的实际效果由引用的已采纳法则定义。被日账接管的同名 law 不再由普通 world_processes 重复执行。动态槽位的具体模板、初始对象与更新法则仍需内容声明。

## 单实体保护

每个危险的未知受害学员掩码为 `(AffectedMask & 65535) & ~KnownByMask`。实际实现采用32位集合差。空闲时按声明顺序预约一个持续险情，记录唯一实体、房间和受害者。预约不移动实体。目标房间之外只请求内容声明的移动动作；实际到场后才请求保护动作。保护 Complete 成功且权威位置吻合后，当前房间的实际见证者通过普通观察机制得到公开危险，认知过滤继续生效。

持续险情占用保留到其条件明确为假，或显式 release_when 成立。公开危险产生的知情变化本身不释放锁。其他房间的危险保持等待；未声明路线、主体缺席或行动不合法时不制造分身、不改位置。现有内容尚未给出完整保护行动和危险声明时，锁机制保持未配置。

## 当前交付边界

35已注册现有场景可用的天气、源质计和人物侵蚀/存活事实。默认生产入口实际使用这些事实和近似选择。房间输运参数、日账粮食/作物具体规律、动态槽位模板、模因对象清单和恶魔危险/保护行动需要内容提供。没有用推测数值填充这些空项。

语言编译器、法则求值器、正式判答、付费重演、证据与存档库保持现役依赖。旧行动树、任务缓存、搜索续行、根层机会预测评分和自动全脑组合已删除。保留的单步 `dvp_forecast` 由章节导航资格与正式成就条件消费；它不参与当前选策评分。全部剧情内容尚未迁为位图原生结构。构建诊断与现场人工审阅由主任务报告记录。本文件不宣称两章叙事进展、毫秒性能或画面效果已经实测。


## 单一评分与退役配置

`dvs_current_action_score(actor, action, bindings, relevant, actual)` 返回当前价值、时间扣分、总分和真实分项。实际选择与显式observer scores共同调用该函数；界面不再运行旧机会预测或听者评分。现有 `information_component/progress_component/time_penalty/total` 投影字段继续可读，`components` 记录声明、私人、本人、个人选择、答辩、回应、探索和日程效用。历史选择分与当前即时报价仍分别显示。

`simulation_mode` 只接受 `approximate-v1`。`selective_planning`、`opt_prune`、`opt_planreuse`、`opt_taskgraph`、`prune_top_k`、`prune_depth`、`plan_time_bucket` 作为输入明确拒绝；旧precise/selective入口不保留兼容执行路径。共享预测备忘 `opt_plancache` 和付费重演缓存 `opt_replaycache` 继续有效。`Replay_Depth` 仍限制本人实际付费重演；已删除行动树的深度不再用于选策。

等待记录标为 `approximate-yield`，只说明当前候选已检查。它保留本人输入、收闻、接触和声明时界的失效条件；不再依赖已删除的全脑固定点、续行树或任务缓存。选择产生的正式队列只持有当前行动及结算所需字段。

后台准备先同步模拟节拍与等待失效记录，再按统一 `dvs_simulation_actor_ready` 判断未排队NPC。该同步保留玩家的待选择状态；已排队且到期的动作继续由正式回执链执行。人类阅读期间不推进游戏分钟。

全员当前无正值候选时，已配置模拟时钟继续保持会话活性。唯一调度器逐次消费下一声明时界，宏观唤醒上限为 `min(last_macro + 15, horizon)`；终点落在宏观刻度之间时直接推进到终点。环境效果只在实际到达对应时刻时执行，主体知识仍经正式观察获得。抵达horizon后只允许已排队到期动作完成，停止新增决策；行动总上限继续优先生效。
