# 列表样板与只读检查漂移修复（2026-10-18）

本轮只读审查附件与适用 AGENTS；未读取设计文件，未运行会话、构建、语法检查或回归，未提交。文本差异检查未报告空白错误；未作任何游戏验收结论。

## 实际修改

- 删除 chapter1/content.dv 中 c1_list_4 至 c1_list_44；现役 chapter1 content/history、独立 oracle 与 recall fixture 调用改为同顺序列表字面量。词法转换跳过字符串及行注释，匹配嵌套括号，不改参数表达式与求值顺序。全 verification 源码已无这些名称引用。
- checks/08-formal.dv 的独立重演补齐 timeline dispatch、全候选输出遮蔽、mask_plan、Replay_Depth 分阶段限额、分派工作扣费、每条规则预测事件编号及重新比较时的深度停止原因。独立源码仍只操作私有预测值，不调用 session，不发布真实事件。
- 切片检查从冻结的完整假设和本人演算前状态重建授权切片，核对实际 trial 与该结果一致；完整假设仍按原等值核验。切片吻合只是该次 probe 记录，不能代替完整假设的后续重演；实际模型消费及接受仍保留原独立完整性检查。
- checks/05-cost.dv 修复付费话语解释遗漏的 Implicature_Resistance、接纳程度 acceptance、保留解释时的 acceptance 回落；预测前清空 history/seen，与生产输入一致；补齐带来源 class_bias 的 trust 计算。移除无调用的旧 hearing/choice_signature 副本，检查了现役源码名称引用；未发现动态名称调用。
- 经主代理明确授权，session/25-simulation.dv 的 dvs_argument_revoke 与 dvs_refresh_derivations 恢复完整 actor_before/after、mind_before/after，同时保留 revision 与 actor_id。仍经 dvs_event_prepared 的不可变快照发布，没有改回退条件或预算算法。
- checks/03-evidence.dv 的私有撤回重演同步 last_cognitive_event、认知父链和事件字段投影。保持完整快照与独立回退演算，不用 revision 冒充证据，不跳过真实事件。

## 保留的独立实现及原因

未将仿真、因子剪枝、付费话语和撤回算法抽成生产/判定共用函数。它们定义待审因果演算、收费和证据有效性，简单共用会使同一错误同时产生结果与判定，破坏只读独立复算边界。本轮不为压缩行数牺牲此边界。已经共用的基础 Core 真值、独立更新、接纳门槛与数值/容器 ABI 仍是基础机制；不是新引入的 oracle 旁路。

因子剪枝逐函数源码差异封存在 factor-source-diff.txt：主要区别是 dv_with_many 与嵌套更新、生产索引查找与独立线性查找，以及生产发布事件而判定仅返回私有结果；未发现需要更改条件、证书覆盖或工作扣费的实质漂移。未运行动态证书验证。

paid-source-diff.txt 是修复过程中的中间源码差异，展示发现的 model_before 与 trust 漂移；该文件不是最终验证输出，也不是游戏证据。

## 验证限制

仅做源码审读、名称与重复声明扫描及文本差异检查。新增独立重演代码尚未编译和执行。未验证正式一级正文、Godot UI 或三级实机行为。新快照增加的是既定证据合同，不是同条件速度优化。相关接口与维护说明交由主代理同步。
