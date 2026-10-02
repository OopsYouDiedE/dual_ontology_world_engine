# 假说因果链与推理平衡性归档 (Deduction Balance)

> 本文档为 `artifacts/deduction-balance/` 交付物说明，归纳了案件推演、假说因果链与信息暴露边界的平衡性审计模型。

---

## 一、 假说平衡性设计模型

根据 `docs/design-standard.md` 与双重本体世界引擎规范，推理平衡性遵循以下核心法则：

1. **认知与物理严格隔离**：
   - 假说世界（Hypothesis World）不修改现场物理状态；
   - 证物（Evidence）是触发推演的必要前提，必须在实机调查阶段通过有效操作获取；
2. **因果链自洽性**：
   - 每个候选假说必须经过前置条件检验、动作合法性推演与后置状态核验；
   - 产生物理冲突（如尺寸不符、时间倒错、锁具未开等）的假说在整合轮予以明确证伪（Refute），并给出合理反例反馈；
3. **收敛与唯一确定性**：
   - 完整证据链条必须能在终轮唯一锁定真实时序与动机，杜绝模糊歧义。

---

## 二、 演进状态与归宿

- **历史方案归档**：早期的孤立门扇推演原型（Door Case）已全量退役，其相关硬编码与临时检测工具均已彻底清理；
- **当前主线**：第一章“未归的红筹”完整 9 字段时序与动机因果推演逻辑已全面实装于 Core 引擎与 `RedTokenDemo`，具体规范详见 [`docs/design-standard.md`](../../docs/design-standard.md)；
- **实机通关验证**：最新 30 分钟无作弊盲玩与答辩一致性验证数据，详见 [`artifacts/first-awakening/consolidated-audit-report.md`](../first-awakening/consolidated-audit-report.md)。
