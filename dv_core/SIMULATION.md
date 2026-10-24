# 近似模拟接口

2026-10-24 的生产默认配置为 `simulation_mode="approximate-v1"`。`60-game.dv` 将配置交给全部主体；`30-session.dv` 的 `dvs_choose` 和人工/自主认知入口直接选择新策略。旧 `dvp_plan`、taskgraph、多层假设选择、听者预测与完整认知组合退出默认路径。显式 `legacy-explicit` 保留旧选择入口；这一配置仍共享新环境状态，不能当作历史版本的等价性能基线。

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

语言编译器、法则求值器、正式判答、付费重演、证据与存档库保持现役依赖。此次改写覆盖默认生产选策、认知遍历与模拟调度；尚未删除全部旧库，也未把全部剧情内容迁为位图原生结构。构建诊断与现场人工审阅由主任务报告记录。本文件不宣称两章叙事进展、毫秒性能或画面效果已经实测。
