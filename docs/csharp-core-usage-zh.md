# 当前 C# Core 使用规范

本文说明当前代码的调用契约。未来能力见[长期模拟设计与测试方案](long-term-simulation-plan-zh.md)，不应据此假定相关接口已经实现。语言与维护规则见 [Core AGENTS.md](../godot_csharp/core/AGENTS.md)。

## 1. 项目与入口

游戏项目引用 `godot_csharp/core/DualOntologyWorldEngine.Core.csproj`。Core 使用 .NET 8、C# 12，独立于 Godot。游戏层承担实时展示、输入、房间内坐标与碰撞；Core 承担跨场景拓扑和离散行动值结算。

通用模拟从 `SimulationSession` 开始。它不自动创建人物、供应、感知范围或日程。怪谈群像内容可从 `LiterarySimulation` 开始，它装配 PatternBook、灵异物品、人物候选与默认评分。

一个世界使用一个 Session 和其唯一 Timeline。不要为同一 Runtime 创建多个 Session，否则执行通知可能被重复记账。手动注入 Runtime 时，Timeline、RuleEnvironment、RuleSystem、KotodamaSystem 应属于同一组装配对象。

## 2. 世界状态与对象身份

`Entity` 表示独立世界对象，`Character` 表示行动身体，`Soul` 表示认知主体。创建身体和心智后调用 `actor.Bind(soul)`。两者可以多对多绑定；`Mind`、`Body` 只返回首选绑定。

通过 `Reparent` 改变世界树归属，`Children` 供读取；位置参数是子节点槽位，不是房间坐标。调用方不得构造包含循环。跨房间移动先用 TopologySystem 取得下一段 Passage，再把通行条件与代价放入行动。底层 `F("move", ...)` 只改变归属，不自动寻路或检查门禁。

Tags 保存非负整数强度，Properties 保存内容属性，Facet 保存轻量关系与声明。只有需要独立生命周期的目标、条款、物品才建立 Entity。关系目标直接持有对象引用；名称可重复、可修改，不用作定位键。

动态字段在定义与读取处约定同一类型。声明式资源采用 `double`，标签采用 `int`，行动值采用 `double`。`Get<T>` 类型不匹配会返回默认值，不负责数值转换。`set` 保留输入类型，`change/spend` 的普通属性结果为 double。

## 3. 最小完整调用

以下代码可作为引用 Core 的控制台项目 Program.cs。只演示一次外部命令，不引入额外场景框架。

```csharp
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Simulation;
using static DualOntologyWorldEngine.Core.Rules.AST;

var world = new Entity("示例世界");
var room = new Entity("房间", world);
room.SetTag(CommonTag.Scene);
var actor = new Character("行动者", room);
var mind = new Soul("心智");
actor.Bind(mind);
var food = new Entity("口粮", actor);
food.Set("doses", 1d);

using var session = new SimulationSession(world);
session.Runtime.DeclarativeOnly = true;
var eat = new Intent(actor, mind, F("eat", actor, food),
    [F("spend", food, "doses", 1d), F("change", actor, "tag:Fed", 1d)],
    motionCost: 10, label: "进食",
    requirements: [E("eq", E("value", food, "$parent"), actor)]);
session.Submit(eat, result =>
    Console.WriteLine($"成功={result.Result?.Succeeded == true}"));
session.Timeline.RunUntil(10);
Console.WriteLine($"库存={food.Get<double>("doses")}，恢复时刻={session.ReadyAt(actor)}");
```

预期输出成功为 True、库存为 0、恢复时刻为 10。提交后效果在 0 AV 发生，行动代价约束身体的下一次行动。直接 `ResolveDetailed` 不等待身体恢复；一般外部命令必须经 `Submit`。

## 4. 行动声明与效果语法

Intent 的 `Effect` 是行动语义，供规则匹配、审计和目标提取；`Changes` 才是实际执行的变化列表。只写 `F("eat", actor, food)` 不会自动扣库存。新行动使用声明式构造器，即使无变化也传入 `[]`，避免落入历史闭包构造器。

语义参数采用 `(actor, target, tool)` 的顺序。`Target`、`Tool` 由参数形状推导，任意改变顺序会影响规则槽位和见证逻辑。声明式构造器默认代价 10，历史构造器默认代价 1；实际内容应明确写出代价。

以下是 EffectExecutor 的直接语法，操作符统一小写：

| 写法 | 实际含义 |
| --- | --- |
| `F("set", entity, field, value)` | 设置字段；普通属性值为 null 时删除属性 |
| `F("change", entity, field, amount)` | 数值增减；结果小于零则失败，不自动钳制为零 |
| `F("spend", entity, field, amount)` | 支付正数资源，余额不足则失败 |
| `F("move", entity, parent)` | 改变归属；目标为 Entity 或 null |
| `F("relate", entity, kind, target)` | 增加关系目标，重复目标不重复加入 |
| `F("unrelate", entity, kind, target)` | 移除关系目标 |
| `F("observe", soul, subject, field)` | 将执行视图中的字段写入该心智 KnownState |
| `F("sequence", effect1, effect2)` | 按顺序准备，后项可以读取前项的预测结果 |
| `F("when", condition, effect)` | 条件成立时准备效果 |
| `F("require", condition, reason)` | 在效果序列当前位置检查条件 |

`Requirements` 在全部变化之前检查。条件原语包括 `value`、`known`、`all`、`any`、`not`、`eq`、`ne`、`gte`、`gt`、`lte`、`add`、`subtract`、`contains`、`quote`、`true`、`false`。把 Expr 本身作为数据保存时，用 `quote` 避免被当作条件求值。

字段前缀 `tag:` 访问标签，`rel:` 访问关系目标数组，`$parent` 访问归属；Character 的 `strain` 映射到首选心智的 Fatigue。多心智身体的负荷并非自动分摊。`change/spend` 对 strain 的上限处理不能视作所有 Fatigue 写入路径的全局保证。

`set` 不提供通用非负或有限数检查，普通动态字段也没有自动范围限制。自定义内容要通过实际前置条件或效果处理器表达约束，避免把负值钳制当作支付成功。

## 5. 认知与自主行动

使用 `target.Offer(factory)` 发布对象能力，再用 `soul.Discover(perceived)` 安装候选发现。perceived 只返回实际可感知对象；工厂不执行行动、不消耗资源。感知公开状态时可调用 `StateView.Learn`，并提供来源。完整装配见[资源村示例](../godot_csharp/examples/resource_village/Program.cs)。

调用 `session.Activate(actor, soul, utility: score)` 开始自主行动。选择器必须返回本次 Decision 集合中的真实 Intent；空候选按 idleCost 重试，不永久注销身体。重新 Activate 替换旧槽，Deactivate 只取消自主槽，不取消已提交命令。

Session 对声明式候选使用 `new StateView(soul)`。未掌握的外部字段为 Unknown；不确定计划可能同时具有 `Succeeded=true` 和 `Uncertain=true`，这表示可以尝试，不表示客观成功。心智视图的计划不得 Commit。

当前 StateView 默认可读取心智自身、绑定身体及身体直接持有物的客观字段。需要“自己持有但尚不知道”的隐藏信息时，不能假定该默认视图已经支持，应在内容模型或后续感知设计中明确处理。

`Soul.Observe(Expr)` 和 `StateView.Learn` 分属命题观察与字段知识接口，不能相互替代。尤其只记录“见过事件”不会自动学习事件中的全部字段。通用 Session 的 WitnessesCollector 默认未装配；游戏决定见证范围及信息传播。

`utility`、Preference 和 Guard 回调有能力访问真实实体，框架不会自动阻止泄密。主观评分应从心智视图、预测计划与可观察事实读取信息；不要在评分时读取隐藏属性或 Runtime.Review 的客观拒绝理由。

重复惩罚依赖传入的 `context["events"]` 或 `context["runtime"]`，并非仅设置 PenaltyPerRepetition 就会生效。当前默认 LiterarySimulation.Start 未提供这一历史上下文。CommitTo 只提供目标关联的惯性评分，不负责目标完成、改期或自动替换。

## 6. 规则、补丁与条款

RuleEnvironment 的事实语言与 StateView 的字段条件语言有不同解释器。RuleDsl 的 `HasAtLeast/IsIn/Does/Then` 用于规则模型；Intent.Requirements 使用上一节的字段条件。不要把任意一侧的操作符原样送入另一侧，并假定有通用转换。

主体规则先创建 `RuleEntityFactory.CreateRulePublication`，再由 Soul.Receive、Understand、Accept 或 Reject 表达认知立场。采信后的 Forbid 会施加主观压力；不因此自动形成客观禁令。`Decision.Complies=false` 的行动仍可能被选择和执行。

客观补丁由 `CreateRulePatch` 创建并加入 Runtime.Patches。固定实体直接放入 AST，可变位置使用 RuleSlot.Actor/Target/Tool/Scene。Runtime 绑定完整 Intent；默认辖域沿身体的世界树推导。显式传入 domains 会覆盖默认推导，应由可信游戏装配控制。

文学条款通过 `Patterns` 创建，再交给 `PatternBook.Add` 登记。直接创建实体不会自动安装期限任务。使用 LiterarySimulation 时调用其 `Rules.Add`，不要再重复安装 PatternBook 或 HauntedItems。已有十二类范式见[条款源码](../godot_csharp/core/patterns/LiteraryPatterns.cs)，使用组合见[文学示例](../godot_csharp/examples/literary_story/Program.cs)。

KotodamaSystem 的 Apply/Invoke 是底层配方结算，不是自动调度的角色行动。需要角色成本和完整审计时，通过 Intent 承载调用。旧闭包路径仍可用于兼容学院，不具备声明式推演和多步回滚保证。

## 7. 时间、结算与生命周期

Timeline 按绝对 AV 和登记顺序调度。Schedule 参数为相对延迟，RunUntil 参数为绝对目标时刻。达到 maxSteps 后必须检查 Now，不能把预算耗尽当作时间已经到达。Core 没有固定的一天，也没有墙钟驱动。

完整行动依次进行主观评价、客观审查、前置反应、重新审查、提交、事件、恢复时间记账、契约、见证、心智后效、事后反应和监听。前置反应与事后反应属于当前嵌套行动链。反应者支付自己的代价。

| 结果 | 判定 | 事件及成本 |
| --- | --- | --- |
| 审查通过 | Review 返回 Allowed=true | 尚未执行，无事件、无成本 |
| 拒绝执行 | ResolveDetailed 返回 Allowed=false | 主行动无事件、无成本；已经发生的前置反应仍成立 |
| 执行失败 | Allowed=true 且 Result.Succeeded=false | 有执行事件并支付代价 |
| 执行成功 | Result?.Succeeded==true | 有事件；Outcome 可以为空 |

Review 会更新契约审查统计，因此不应把它当作完全无副作用的高频预测接口。EffectPlan 是一次使用的准备产物；不要复用或重复 Commit。原子性覆盖正常声明式准备失败与过期检查，不能回滚自定义处理器、监听器抛异常前的任意外部副作用。

Register 的自定义处理器只通过 `plan.View` 读取和 `plan.Write` 写入预测，不直接改世界。同名注册会失败。Guard 不得改变状态；EffectTransforms 必须在预测与执行中使用相同规则。仅变化计划的 StateChange 不包含全部契约及 MindMechanics 后效，审计需要结合事件链。

玩家接管前 Deactivate，再 Submit；保存 Submit 返回的取消函数。释放场景时 Dispose 对应 Session/PatternBook/LiterarySimulation，并移除自己登记的监听和世界任务。Session.Dispose 只清理它管理的槽和命令，不承诺清除游戏自建任务或释放整个世界。

## 8. 当前长期使用边界

LiterarySimulation.Score 目前只做当前行动及一步后继评估，后继候选最多取 64 个；不是任意深度规划器。Balance 中的默认常量不是已验证的长期经济模型。

Runtime.Events、心智观察和决策记录没有自动历史归档。长期运行必须先完成记录保留策略；单纯调用更大的 RunUntil 不能证明长期稳定。情绪周期衰减须由内容在 Timeline 上调用 MindMechanics.SettleDaily，不会随墙钟自动发生。

## 9. 调用验证

首次运行先恢复项目依赖；已有依赖缓存时可加 `--no-restore`。从仓库根目录运行：

```powershell
dotnet build godot_csharp/core/DualOntologyWorldEngine.Core.csproj
dotnet run --project godot_csharp/examples/resource_village
dotnet run --project godot_csharp/tests/literary_checks
dotnet run --project godot_csharp/tests/runner -- --verify
```

测试用于确认行为，不能替代对候选可见性、变化来源、成本和生命周期的逻辑检查。
