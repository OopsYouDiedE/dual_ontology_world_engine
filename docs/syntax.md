# DV 语法与工程文档导航

现行语法及实施边界统一维护在 [language_core/SYNTAX.md](../language_core/SYNTAX.md)。本页保留旧链接入口，避免两份“完整规范”分别更新。世界、人物、剧情与玩法的设计依据仍是 [design-standard.md](design-standard.md)。

| 查阅内容 | 维护位置 |
| --- | --- |
| 源码结构、词法、作者语法、自举语法及内置操作 | [DV 语法参考](../language_core/SYNTAX.md) |
| 七个 DV 文件、生产依赖与当前工具入口 | [语言工程 README](../language_core/README.md) |
| 编译器、运行时与会话的架构目标和实施边界 | [语言工程 DESIGN](../language_core/DESIGN.md) |
| 自举编译器与平台 ABI | [compiler/README.md](../language_core/compiler/README.md) |
| 宿主接入、序列化和会话接口 | [Session/README.md](../language_core/Session/README.md) |
| 修改、证据和验收约束 | [语言工程 AGENTS](../language_core/AGENTS.md) |

当前编译器源是 `language_core/10-compiler.dv`，平台 ABI 位于 `language_core/compiler/Runtime.cs`；通用机制、会话和生产入口分别是 `20-core.dv`、`30-session.dv`、`60-game.dv`。语法说明需区分当前可编译的作者语法、编译器自举所用形式、旧输入兼容接口和计划中的能力，不能仅凭旧示例或函数名称宣称功能已经实现。

修改语法、内置操作、声明字段或宿主接口时，同轮更新上述唯一对应文档、示例与源码索引；文档描述、实际源码和本轮执行证据分别核对。
