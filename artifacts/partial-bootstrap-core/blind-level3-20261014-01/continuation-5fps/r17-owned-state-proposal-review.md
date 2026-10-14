# R17 宿主状态引用转交提案独立审查

日期：2026-10-15。只读审查现役 DvSession、GameEngine、game/campaign.dv、session runner/control helper、初始快照及事件构造、第二章主体刷新和预测入口。未读取设计规范或私有规划，未改机制源码、构建或运行会话。本报告审查提案，不表示尚未实现的 helper 已验证。

## 结论

限定在 `dvg_extend` 末尾组合状态与 `dvg_prepare` 替换 actors 的线性宿主状态转交，可使用新建顶层记录、复用字段值引用的局部 helper。没有发现这两处必须通过深拷贝整局 events 来保护预测或 initial 的必要性。此判断以保留以下所有权限制为条件；不能推广为通用 record/world/mind 的浅复制替代。

## 所有权与历史隔离依据

- DvSession 只有一个私有 state。Prepare/Complete 同步取得 DV 返回值后替换该字段，没有建立旧宿主状态版本列表或预测状态分支。GameEngine 的 busy/awaitingReceipt 调度串行化状态变更，CloseTrace 等待 work 完成后再保存。
- runner 的 prepare/complete 返回字面量，以及现役 `dvs_control_state`，已经采用新建容器并沿用 events、initial 等字段引用的模式。因此不能把整个宿主 state 解释为每次转换都必须独立深复制的通用不可变值。
- initial 在 session_open 中保存初始 physical/actors 引用。后续历史隔离主要来自 world/mind 的持久复制更新、actor 复制更新以及 actors 容器替换，而不是 dvg_extend 最后的全状态深复制。事件构造 `dvs_event` 在追加前通过 dv_with 复制 data；新 helper 不应替代这条事件快照路径。
- `c2d_refresh_actor_goals`/`c2_refresh_actor_goals` 构造新目标和政策，并返回新 actor。actions 重写使用新外壳的 dvs_replace。dvg_prepare 构造新的 actors 列表，未原地修改 initial.actors。
- 预测入口使用主体 mind/actor 视图，dv_predict 与世界/认知更新继续依赖原复制操作；未发现把完整宿主 state 直接送入预测的现役路径。只替换宿主容器组合不会自动把真实 state 暴露为预测可写状态。
- Save/Snapshot/Trace 对外提供序列化结果，不返回可供调用者修改的宿主 List 引用。采集 Snapshot 可在后台工作期间读取旧 state；新 helper 不应原地修改该旧顶层容器。现有 events 追加共享不能因此扩大为任意主体/物理状态原地修改。

## helper 必须满足的合同

1. 每次新建顶层 record，建议新建每个字段 pair，沿用字段值引用。不得修改旧 record 的条目、原字段容器或 updates 本身。
2. 保留所有未覆盖字段和原有键语义；覆盖字段取 updates 的值引用，新字段正确追加，不产生重复键。尤其不能遗失 initial、pending、player_actor、awaiting_player、player_selection、cursor、executed、idle、预算及时间口径。
3. 本轮只替换 dvg_extend 最后一次状态组合和 dvg_prepare 的 actors 替换。保留构造 expanded story/world/mind 的现有复制；不得顺带替换 dv_with、dv_with_many、dv_clone、dv_adopt、dv_store、dv_predict 或事件数据复制。
4. 复用 events 意味着同一实际会话的事件追加账本继续存在，不代表允许更改既有事件或把预测追加到真实事件流。initial 只能继承，不能改写其子树。
5. 运行中的旧宿主 state 只可作为只读别名，不得被当作分叉会话或稳定历史快照保存后再独立执行。若未来增加此类消费者，应在分叉处建立明确快照，不能扩大本 helper 的适用范围。

## 预期收益与尚需证据

该提案可去掉章节交接最终组合以及第二章每次准备时重复复制整局事件历史的工作。它不会消除章节安装内部采用法则、新增对象或主体刷新本身的其他深复制成本；不能据此保证所有停顿均消失。

实现后需审查 exact diff/生成接线，并在独立新会话中保留真实判答、宿主交付、内容可用与实际跨门的不同时间证据。针对所有权的必要工程核对应关注旧 state/initial/既有事件未被覆盖、预测无真实效果、控制字段及 pending 生命周期保留；不得以跳过事件、重置状态、提高预算或提前发布成功代替性能修复。

本报告没有自行读取采样文件或重测。主代理所报告的 R16 栈定位与交付时刻作为本次提案背景，不由本静态审查重新测量；不以源码推定画面或跨门已经发生。
