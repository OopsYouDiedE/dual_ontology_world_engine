# R17 宿主状态引用转交最终静态审查

日期：2026-10-15。依据 `protected-change-r17.diff`，核对现役 helper 定义及全部非生成 DV 调用位置，并沿用本轮提案审查中已读取的实际消费者、initial、事件和预测路径。未读取设计规范或私有轨迹，未改源码、构建或执行会话；原提案审查保留。

## 实现核对

`dvs_owned_state_update` 创建新的 result、keys 和每个字段 pair；旧 state、旧 pair 与 updates 不被原地修改。已有键使用 updates 值或原值引用，新键按 updates 顺序追加，键登记防止追加重复条目。在正式调用均为唯一字符串键的现役 record 合同下，未发现字段遗漏或新增重复键。

helper 未调用 dv_clone/dv_with/dv_with_many；沿用字段值引用符合本轮限定的线性转交目的。未知字段、pending、initial、控制状态以及时间/预算等未更新字段会自动保留，不依赖手写字段清单。

非生成 DV 中仅找到两个调用：dvg_extend 最后状态组合，以及 dvg_prepare 用新 actors 替换当前主体列表。扩章判定、事件内容与追加位置、时间口径、费用、预算和后续 prepare 循环均未改；world、mind、法则采用、预测及证据快照的独立复制 API 未变。

## 引用消费者与别名边界

DvSession 继续以返回值替换唯一 state；GameEngine 的状态变更仍由原 busy/receipt 边界串行调度，结束保存等待当前 work。新的 actors 容器由 prepare 自己构造，主体刷新返回更新后的 actor，没有通过此 helper 原地改 initial 或历史事件。

expanded story 与顶层 physical/actors/bindings 现在可能共享同一字段值；这是此次明确引入的所有权别名，不能再把 story 的这些字段当作独立可写快照。但现役后续路径采用持久 world/mind 更新、新 actor/list 替换，未发现会在这两处转交后原地改写 story/initial 子树的消费者。真实 events 继续是当前会话追加账本，既有事件数据复制不变。

预测没有新增该 helper 调用，也没有取得完整宿主 state 的新入口。对外 Snapshot/Save/Trace 继续序列化，不返回内部 List 作为可写分支。若未来把旧 state 保存为可独立继续执行的分叉，或原地修改继承的 story/world/mind 字段，必须重新审查；本 helper 不是通用浅复制授权。

## 结论与验证范围

最终差异符合限定提案，未发现新增的别名污染、历史 initial 改写、预测现实效果或字段生命周期错误。相关流程文档已同步限定使用范围。

本报告仅为静态合同审查。尚未核对本轮新生成程序集、构建或运行结果；仍需单独构建、封存新来源与程序集指纹并开启新正式会话。实际判答交付、章节内容可用及真实几何跨门须分别依据本轮证据记录，不能从本结论推定完成。此前章节安装内部其他深复制成本也没有由本差异消除。
