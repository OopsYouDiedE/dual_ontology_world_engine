# C# 文学模拟：实现与使用规范

2026-09-05。本文记录已经运行的实现；数值属于默认基线，尚未经过大量作品校准。设计目标是让人物的认知、欲求、约束与行动后果共同产生短篇互动，不保证自动生成完整小说。

## 游戏作者的入口

只引用 `godot_csharp/core/DualOntologyWorldEngine.Core.csproj`。使用 `LiterarySimulation` 创建场景、人物、条款和物品，然后 `Start()` / `Run()`。不需要复制学园决策代码，不需要安装 Godot，不需要 Python，也不需要给每个动作配置偏好分数。

```csharp
using DualOntologyWorldEngine.Core.Content;
using DualOntologyWorldEngine.Core.Patterns;
using DualOntologyWorldEngine.Core.Simulation;
using static DualOntologyWorldEngine.Core.Rules.AST;

using var story = new LiterarySimulation("夜班守则");
var room = story.Scene("值班室");
var door = story.Object("档案室门", room);
var source = story.Anchor("墙上的守则", room);
var visitor = story.Person("晚归者", room, door, "opened", true);
story.Rules.Add(Patterns.Prohibit(source, visitor, "open", door,
    breach: [F("change", visitor, "strain", 2d)]));
HauntedItems.Create(HauntedItem.Effigy, "纸人", visitor);
story.Start();
story.Run(60);
```

`Person` 的必填内容是人物、初始场景和目标状态。可选内容包括五种人格档案、牵挂对象 `attachment`、命题 `secret`、口吻 `tone` 和保密倾向 `guardSecret`。这里的命题是角色的初始认知，允许错误；设置它不会改变客观世界。公开观察会更新认知，听到证词只记录“某人这样说”，不把证词写成客观真相。

`Connect` 创建可推演的场景通道，可指定门。文学模拟不读取自定义 `Passage.Condition` 闭包；扩展通行条件应写入 `Passage.Requirements`。

## 核心结构

| 位置 | 职责 |
| --- | --- |
| `effects/EffectExecutor.cs` | 状态视图、效果准备、原子提交、认知隔离 |
| `patterns/LiteraryPatterns.cs` | 十二种句式、权限、期限、支付、违约、持续封存 |
| `content/HauntedItems.cs` | 十二类物品的默认能力和生命周期 |
| `content/Balance.cs` | 默认 AV、容量、疲劳界限和人格权重 |
| `mind/LiteraryDialogue.cs` | 有主题的陈述、问询、质疑、证据、承诺及回应 |
| `simulation/LiterarySimulation.cs` | 默认感知、候选、两步估算、反制和外部装配入口 |
| `timeline/Timeline.cs` | 审查、结算、事件、见证、嵌套反应 |

继续使用 Entity 和 Facet；运行时引用直接指向对象，没有另外建立对象 ID 仓库。游戏只定义内容起点，默认机制和数值位于 Core。

## 行动与扩展契约

新行动使用 `Intent(actor, mind, effect, changes, motionCost, label, requirements)`。`effect` 描述行为语义，`changes` 描述实际变化；不得只换动作名称绕过硬禁令。文学模拟拒绝没有 `Changes` 的闭包动作。

`ActionRuntime.Prepare` 是统一准备入口。普通行为和新文学言灵使用同一解释器；原 `KotodamaSystem` 的成本与产出也已改为通过该解释器批量结算。

基本效果为 `set`、`change`、`spend`、`move`、`observe`、`relate`、`unrelate`、`require`、`sequence`、`when`。字段使用属性名；标签和关系分别用 `tag:` 与 `rel:` 前缀，父对象使用 `$parent`。条件使用 `value`、比较、布尔组合和 `contains`。命题作为内容保存时使用 `quote`，不能把它误当作要执行的条件。

流程为：准备条件和全部效果 → 检查预算及状态读取是否过期 → 一次提交 → 事件和见证 → 条款后果。任何一笔支付不足或出现未知原语，整笔准备失败。条件与效果扩展必须只通过 `EffectPlan.View` 读取、通过 `EffectPlan.Write` 声明变化；不得在准备处理器内修改实体或随机数状态。

`story.Predict(intent)` 使用人物认知和独立覆盖视图。未知信息不回退到客观值，认知计划不能提交。已知禁令和触发式条款的直接后果进入估算；未知条款留待真实执行遭遇。默认搜索只展开下一步的有限候选，不是无限深度规划，也不完整展开未来他人的决策。

## 默认文学范式

十二种范式是禁止、许可、期限、触发、交换、例外、真名、真言/保密、自缚、替身、封存/释放、证词。

- 软禁令允许违反并结算后果，硬禁令阻断；解除封存也检查目标的开启动作限制。
- 限时义务允许在截止 AV 当刻履行，之后才判违约。例外条件在事件后解除原条款。
- 交换必须双方仍持有约定物品，不能只移动其中一件。
- 真名需要持有对应名牌，建立指称后可对远端对象施展指定封存/释放，仍受硬规则约束。
- 真言约束命题真假，保密约束听众和公开性；沉默、隐瞒和撤回不被当成肯定命题。
- 自缚在生效期间折扣一次施展的第一笔介质支付，最多省 1 Q，至少支付原价的一半。违反所放弃的动作后失去折扣。
- 封存每 20 AV 支付维持费用，介质离开施术者或余量不足后释放。

原学园配方和闭包动作仍在历史 Simulation 中使用；它们不是新文学入口的推演承诺。此次没有新增兼容适配层，也未完成整个历史学园内容的声明式重写。

## 物品与数值边界

符、盐、烛、铃、镜、红线、名牌、遗物、规则纸、录音器、封盒、替身由 Core 创建。它们提供有限资源、来源对象、可见性与位置条件。

符护持和盐的边界保护有 20 AV 时限；符抵消一次 1 点负荷，盐抵消一次印记。镜消耗完整度反射匹配来源的负荷；替身消耗承载量替受一次负荷。烛和封盒定期消耗，不会无代价永久维持。卸载会移除自身监听和定时器。

默认基础行动 10 AV、短回应 5 AV、调查与休息 20 AV，锚点 12 Q、替身 2 Q，负荷上限 6。后果预算按数值变化的绝对量计费，其他变化每项 1 Q；组合效果累计。预算是本引擎的原创机制，不代表民俗考据。

红线将一次负荷中的最多 1 点转交给另一人物，扣除剩余预算并断开联结，不复制伤害。使用 `Object(..., occultEvidence: true)` 的线索需要烛照或摇铃显迹后才能调查。跨全部物品的自动策略覆盖和长期平衡仍需更多场景验证。

## 人物表达的验收范围

已实现的文学性落在短篇互动的因果上：人物可以持有相互矛盾的认知；已知证据进入交涉；不同人格权重影响冒险和回应；保密倾向改变是否主动透露；质疑增加压力，被质疑者可以撤回、沉默或继续坚持原有判断。撤回不会改写世界真相，也不等于清除其全部信念。

表达使用有限的中文句式和口吻变化。陈述绑定具体命题，质疑引用矛盾两端，证据表达区分材料与亲眼所见。追问、出示、承诺和回应有状态去重，不能靠重复同一动作无限积累收益。等待是合法选择。

这里尚无长篇人物弧、自由自然语言理解、复杂潜台词生成或自动戏剧节奏导演。维护长期章节张力仍需要作品作者的内容设计。

## 复现

```powershell
dotnet run --project godot_csharp/tests/literary_checks/literary_checks.csproj
dotnet run --project godot_csharp/examples/literary_story/DualOntologyWorldEngine.LiteraryStory.csproj
dotnet run --project godot_csharp/tests/runner/runner.csproj -- --verify
dotnet build godot_csharp/DualOntologyWorldEngine.csproj --no-restore
```

新机制验证覆盖原子支付、过期准备、错误信念、规则期限、真言、交换、自缚、维持、反制、后果预测、真名作用域及自主人物互动。示例没有操作名偏好回调，也没有固定 Submit 剧情：夜班场景只切换守则是否存在；群像场景由原生决策产生证词冲突。

历史 `five_days_trajectory.md` 未覆盖。新示例运行记录单独保存在 `literary-simulation-run.txt`。
