# Core 维护规范

本目录使用 .NET 8、C# 12、nullable reference types 和 implicit usings。世界引擎原理详见 [统一设计规范](../docs/design-standard.md#第三部分core-独立世界引擎运作原理)。

- Essence 表示客观对象身份；物理状态和环境机制由 Property 持有。
- Projection 为主观认知投射，对象身份使用引用，结构保持精炼。
- Soul 的认知、规则与历史均保存在自身的 ProjectionWorld。主观认知的更新统一经过 Observation.Observe 接口。
- DecisionSystem 与 SocialSystem 严格基于主体的主观认知展开逻辑演算与决策。
- AST 使用类型化节点、槽位、Tag 和 Projection 引用。字符串仅用于名称、描述及文本解析边界。
- CTBSystem 负责离散行动值延迟激活；动作选择、代价结算和嵌套阶段由调用方协调。
- ActionRuntime 对每个阶段先准备再提交，状态变更在提交阶段权威生效。
- 认知集合的访问保持封闭，确保认知演变完全由观察与交互事件驱动。
- 核心引擎保持纯粹性，与具体的渲染引擎、实时循环、几何与 UI 分立。
- 验收依据独立因果场景推进，决策结果由规则权威结算。

## 独立打包边界

- Core 作为独立 .NET 类库构建与打包；包元数据和 API 说明与当前 C# 源码保持一致。
- Core 的包说明维护在本目录 `README.md`。
- Core 的 NuGet 打包输出统一保存在仓库根目录 `artifacts/releases/core/`。
- 使用 `dotnet build csharp/Core.sln -c Release` 构建；使用 `dotnet pack csharp/DualOntologyWorldEngine.Core.csproj -c Release -o artifacts/releases/core` 本地打包。

## 目录纯净性与代码演化

- 本目录专属于 Core 独立逻辑与核心基准测试，游戏呈现与交互资源属于 `godot_csharp/`。
- 核心级基准与验证脚本就地保存在 `csharp/` 根层。
- 系统演进直接在既有架构与文件上持续重构。
