# 当前 Core 使用示例

引用 `godot_csharp/core/DualOntologyWorldEngine.Core.csproj`，使用命名空间 `DualOntologyWorldEngine.Core`。

## 建立世界和自主行动

以下代码可放入引用 Core 的 .NET 8 控制台项目：

```csharp
using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

var world = new WorldRuntime();
var room = new Essence("房间", ProjectionKind.Space);
var roomView = world.Project(room, "房间");

var body = new Essence("身体", ProjectionKind.Body);
body.Add(new SpatialProperty { Space = room });
var energy = new Tag("Energy");
var state = body.Add(new StateProperty());
state.Values[energy] = 0;
state.Observable.Add(energy);
var bodyView = world.Project(body, "我的身体", roomView);

var soul = new SoulEssence("主体");
soul.Relations.Add((Relations.Controls, body));
world.Project(soul, "主体");

// 引擎提供经过筛选的感知范围，返回的对象属于主体内部世界。
var perceived = world.Observe(soul, [roomView, bodyView]);
var self = perceived[1];
var decoder = new AstDecoder(soul.World, [energy]);
var rules = decoder.DecodeRules(
    "RULE not(tag_at_least($Actor, Energy, 1)) => PREFER sequence(set($Actor, Energy, 1), inspect($Actor))");

// 采信是主体的认知更新，解析成功本身不意味着采信。
Observation.Observe(soul, new("已掌握规则", ProjectionKind.Procedure)
{
    Source = ObservationSource.Introspection,
    Rules = [..rules.Select(r => r with { Acceptance = Truth.True })]
});

using var session = new SimulationSession(world);
session.Activate(soul, self, delay: 2);
session.CTB.RunUntil(10);
Console.WriteLine(state.Values[energy]); // 1
```

这里的 set 是引擎效果原语。游戏层应通过自己的流程条件、代价和已掌握规则决定何时提供这项能力。
更新 Property 后不会自动更新认知；示例中的 inspect 使角色重新获知自己的能量状态。

## 文本和嵌入

`DecodeEffect("inspect(@0)", projection)` 的名词直接引用 projection。
`DecodeEffect("inspect(@我的身体)")` 只查询构造解码器时指定的 World，重名报歧义。
名称含空格时可写 `@"某个物体"`。名称只用于外部解析和显示，运行时比较使用对象引用。

动态位置使用 `$Actor`、`$Target`、`$Tool`、`$Scene`。Decision 枚举候选投影填充目标与工具。
直接 Submit 时 Actor 和 Scene 可由行动上下文填充，其他槽位需要事先绑定。
say 的请求体属于接收者的后续行动上下文，因此不会在发送者提交时填充其槽位。

自定义 Tag 和 RelationTag 通过构造解码器时的 tags、relations 参数登记。
未登记或拼错的标签会产生 AstParseException，其包含行号和列号。
类型化 C# AST 可直接构造；规则文本和言灵文本都使用同一 AstDecoder。

## 当前语法

```text
RULE condition => REQUIRE effect, FORBID effect, PERMIT effect, PREFER effect

条件：
true() | false() | unknown()
exists(noun)
tag_at_least(noun, Tag, integer)
related(noun, RelationTag, noun)
at(noun, noun)
kind(noun, ProjectionKind)
not(condition) | all(condition, ...) | any(condition, ...)

效果：
unlock(door, key) | travel(door) | take(item) | inspect(target) | destroy(target)
set(target, Tag, integer) | change(target, Tag, integer) | spend(target, Tag, integer)
relate(target, RelationTag, other) | unrelate(target, RelationTag, other)
sequence(effect, ...) | when(condition, effect) | require(condition)
wait(delay, effect) | say(recipient, "text" [, requestEffect])
infer(exists(noun)) | infer(not(exists(noun)))
infer(related(noun, RelationTag, noun)) | infer(not(related(noun, RelationTag, noun)))
infer(kind(noun, ProjectionKind))
```

规则语法可以有多项指令；DecodeRules 返回规则数组。模式权重和采信状态通过 Rule 的类型化成员配置。
Unknown 不等于 False，Not(Unknown) 仍是 Unknown。

## 言灵与等待

把解码后的 Effect 通过 Observe 存入一个内部 Projection 的 Procedure，再调用 `session.Invoke(soul, self, procedure)`。
无需创建另一套言灵对象或执行器。

`sequence(spend(...), set(...))` 在同一阶段原子提交。
`sequence(spend(...), wait(5, set(...)))` 先支付，5 行动值后结算后续效果。
等待期间取消动作不会退款；后续阶段仍重新检查现实。

Submit 返回 ActionExecution，取消调用 `session.Cancel(execution)`。
调整激活时间调用 `session.CTB.Reschedule(execution.Scheduled!, remaining)`，不会改变名词指向。
CTB.ActionValue 提供速度/动作成本换算，CTB 自身不读取身体速度。

## 认知修正与异常

Observe 的 Target 不为空时原地更新投影。未指定的字段表示没有新信息。
Tags 的值为 null 表示撤销；DenyRelations 明确否定；RemoveRelations 撤销关系认知并恢复未知。
Existence 是主观存在判断，Detected 是本次证据是否感知到对象，二者可以冲突。
Name、Kind 和身份关系都可以因认知改变，投影引用仍保持。

IProjectionTransform 接收 ObservationInput，返回改变后的输入或 null。WorldRuntime.Observe 在关系图映射后调用它。
CorruptionProperty 通过 Cognition.Corrupt 应用同一转换接口，最终仍调用 Observe。
Forget 清理对象及依赖其引用的规则和历史；它不表示客观对象消失。

WorldRuntime.Associate 只供引擎建立初始错误记忆的对应，不向 Soul 暴露真实对象，也不允许已绑定投影静默改绑。

完整因果场景见 `godot_csharp/tests/core_acceptance/Program.cs`。
