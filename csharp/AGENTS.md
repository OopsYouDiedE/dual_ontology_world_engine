# Core 维护规范

本目录使用 .NET 8、C# 12、nullable reference types 和 implicit usings。

- Essence 表示客观对象身份；非本质状态和机制由 Property 持有。
- Projection 不持有可供主体读取的 Essence 引用。对象身份使用引用，不增加定位 ID。
- Soul 的认知、规则、历史均在自身 ProjectionWorld。所有主观修改经过 Observation.Observe；内部提交实现除外。
- DecisionSystem、SocialSystem 只接收内部世界。不得在候选过滤时查询 WorldRuntime 或客观 Property。
- AST 使用类型化节点、槽位、Tag 和 Projection 引用。字符串只出现在名称、描述及文本解析边界。
- CTBSystem 只负责离散行动值延迟激活；动作选择、代价结算和嵌套阶段由调用方协调。
- ActionRuntime 对每个不含等待的阶段先准备再提交；不能在准备阶段修改实际状态。
- 对外公开的认知集合不可直接修改，避免绕过观察入口改变主体知识。
- 不引入 Godot、实时循环、房间内几何、碰撞或 UI。
- 验收通过独立因果场景，不能由测试脚本代写决策结果或绕过 Observe。
- 动物园规则及叙事专用内容属于游戏层，不能为了兼容旧示例把旧 Entity/Belief 机制搬回 Core。

## 目录纯净性与文件放置红线

- **严禁引入任何游戏层产物**：禁止在本目录放置 Godot 场景（`.tscn`）、资源（`.tres`、`.import`）、GDScript、图片素材、UI 或音效文件。任何游戏呈现与交互一律放入 `godot_csharp/`。
- **禁止随意自建临时目录**：核心级基准与验证脚本（如 `measure-core.ps1`）就地存放在 `csharp/` 根层，严禁随意建立临时文件夹堆放实验代码。
- **多轮次代码修改原则**：对核心系统的修改直接在既有文件上重构与演进，严禁创建 `Model_v2.cs` 等平行版本。

