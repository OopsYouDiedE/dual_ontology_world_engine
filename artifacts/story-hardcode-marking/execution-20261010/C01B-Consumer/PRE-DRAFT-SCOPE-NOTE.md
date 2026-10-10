# C01/B 消费范围补充说明（草案读取前）

主代理于本轮消息明确：phase1 基础合同与各消费迁移分开。`Assess` 整轮一致性由 C06 消费卡明确实现和独立验证；基础单 AST 锁不能被记录为 Assess 已完成。基础合同应提供可让整次评分共享 immutable snapshot/read scope 的接口和凭证，但不要求 phase1 越界迁移全部消费方。

`GC-CB-01` 的 foundation gate 是“合同能表达整次消费固定版本”，实际 Decision/Social/Reasoner 迁移 gate 保持后续未验证；pre-draft-recommendations.json 中 blocking-for-foundation 不代表要求本批提前完成 C06。

Actor Forget 的 `GC-CB-06` 仅是静态兼容风险，需在后续 ActionRuntime 精确任务卡与中性独立复现中区分身体动作和认知效果；不要求 phase1 自行改执行连续性，也不以当前目标 Forget 诊断代替 actor 复现。

完整保存仍是获批 B 的本批任务。phase1 cognition codec 可以单独冻结/实施，前提是明确不是完整 save，并保留后续现实、调度、租约、host progress/presentation/RNG provider 的任务依赖和整体 B 未完成状态。

本补充不改变先形成的源码盘点、授予生产写权限或代签用户批准/租约。
