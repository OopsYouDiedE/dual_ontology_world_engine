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
