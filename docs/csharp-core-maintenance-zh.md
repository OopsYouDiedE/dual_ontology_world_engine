# C# Core 维护与验收

本轮维护以开始时的工作区为基线，保留已有实体文件合并。先维护 Core，再更新资源村简单示例。长篇人物成长、持续戏剧张力与长期数值平衡保持原有实现。

Core C# 源文件总行数由 7,566 降至 6,889，减少 677 行（约 8.9%）；排除空行、注释和预处理指令后，由 5,116 降至 4,827，减少 289 行（约 5.6%）。统计排除 bin/obj，不将维护前已有的文件合并计入本轮成果。

## 结构与接口

`Timeline.cs` 仅负责行动值队列；`ActionRuntime.cs` 负责行动审查与结算。`SimulationSession` 组合运行时并管理自主槽、外部命令与恢复时间。Core 继续作为独立 .NET 8 项目，无 Godot 依赖。

规则的 Effect/Intent 两个评价入口共用压力计算。模板参数共用递归绑定。拓扑搜索改为标准优先队列，按最短距离扩展，等距时保留原有邻接场景顺序。角色所在场景复用拓扑系统查询。

删除 `ReviewResult`，审查与结算统一返回 `ActionResolution`。删除无调用的 `Decision` 历史构造器、`WithAction`、`ScheduleImmediate`、调度中不起作用的 `priority` 参数，以及旧的元组式 `ActionRuntime.Resolve`。调用方改用 `ResolveDetailed`，以 `Result?.Succeeded == true` 判断效果成功。

保留仍被学院使用的闭包式 Intent、基础动作、知识投影与规则 DSL。未找到可以整套移除而不影响现有机制的其他过时系统；公开扩展没有仓内调用并不自动等于过时。

## 核心闭环

1. 内容通过 Affordance 或 Soul 的 Step 插件提供 Intent。
2. Session 用心智 StateView 准备声明式候选，Soul 根据已知信息和已接受规则排序。未知状态允许尝试，不提前泄露客观答案。
3. Runtime 客观审查身体绑定、行动值、前置条件、效果计划、契约与补丁。带槽位的补丁绑定实际行动；没有显式 domains 时按身体所在世界树确定辖域。
4. 前置反应作为嵌套行动执行，随后重新审查并重新准备主行动。
5. 声明式效果通过同一 EffectExecutor 提交。拒绝审查不产生执行事件；进入执行后的失败仍记录事件并支付行动值。
6. Runtime 通知 Session 记账，再结算契约、见证、心智后效和事后反应。事后反应不插入普通时间队列。监听器再入结算也受深度上限约束。
7. Session 根据身体累计恢复时间安排下一槽；世界过程与外部命令继续使用同一 Timeline。

执行审计中的 Decision 使用 Soul 的认知环境，避免把客观事实混入主体评分。规则去重比较 AST 和对象引用，不再比较显示文本。门锁读取不创建状态；收纳失败不清除持握状态。

底层 `KotodamaSystem.Apply/Invoke` 保留配方与契约原子结算能力。它们本身不是角色行动槽，直接调用不会自动支付角色行动值或生成完整行动审计；需要这两项语义时由 Intent 网关承载。旧闭包适配也不提供声明式回滚保证。

## 语法约定

正式维护约定见 [Core AGENTS.md](../godot_csharp/core/AGENTS.md)。C# 12 已在 Core 项目中显式声明，nullable 与 implicit usings 继续启用。语法规范以减少样板和明确状态边界为目的，不增加格式门禁或无意义验证。

`AST.E/F` 在工厂边界兼容旧代码传入的 null 参数数组，将其归一为空数组。新代码使用零参数调用表示空列表，使用 RuleSlot 表示模板槽位；不要用 null 冒充通配符。

## 验证入口

```powershell
dotnet build godot_csharp/core/DualOntologyWorldEngine.Core.csproj --no-restore
dotnet run --project godot_csharp/tests/literary_checks --no-restore
dotnet run --project godot_csharp/tests/runner --no-restore -- --verify
dotnet run --project godot_csharp/examples/resource_village --no-restore
```

现有验证覆盖原子支付、过期计划、认知隔离、契约、反制、事件和行动值队列。本轮补充规则引用等价、客观补丁槽位与辖域、主观审计、监听器再入深度，以及拓扑和失败操作回归。学院五日仅作为已有行为回归，不生成或覆盖长篇轨迹。

验收前结果：Core 与文学示例构建为 0 警告、0 错误；文学机制检查 22/22 通过；Runner 的核心、基础物品动作和架构回归全部通过，学院五日仍产生 544 个事件；资源村的全部断言通过。`--verify` 已纳入原有基础物品动作检查，并关闭该检查的报告写入。
