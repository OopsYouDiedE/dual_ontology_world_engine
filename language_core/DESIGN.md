# DuelView 语言结构

DuelView 简称 DV，源码后缀为 `.dv`。完整书写格式见 [SYNTAX.md](SYNTAX.md)，第一章需求见[源码反推报告](../artifacts/partial-bootstrap-core/chapter1-reverse-language-requirements-2026-10-13.md)。

## 语言定位

DV 表达不同主体如何取得材料、结合已有认识形成理解、采纳或修订判断，并据此规划和选择行动。语言同时提供通用计算能力，用于实现编译器和推演算法。

领域层的基本对象是认知切片、来源、命题、解释、采纳、假说、模型轨迹、论证、规划、评分、分布、意图和执行回执。算法层提供函数、闭包、集合、类型和控制流。

## 认知与行动

```text
存储材料 → 本次取用
提供材料 → 实际传递 → 本人感知
本人感知 + 记忆 + 规则 → 理解 → 采纳或保留
本人认识 + 目标 → 多步规划 → 首步评分 → 动作分布
动作分布 → 意图 → 准备 → 执行 → 新的可感知材料
```

主体视界绑定本人、本地引用、快照版本与来源。权威世界记录真实状态和变化；本人认识记录已取得的材料和可错判断。ModeledMind 表达本人持有的他人模型。

命题评价产生 Truth（True、False、Unknown）、依据及冲突；程序控制使用 Bool。直接观察、证词、假设、推知和估计论证各有来源种类。采纳和撤回保留支持关系。

Score 描述行动评价，ActionDistribution 描述选择概率，Confidence 描述认识把握。即期分、路径回传分、信息价值、代价和规范优先级分别表达。第一章规划还包含有候选却暂不行动的结果。

surface-v2 使用 `认知映射 × 实际感知切片` 表达形成主观解释的过程。乘法外观不指定矩阵算法；映射实现可以组合记忆检索、规则、范围桥接与数值模型。输入保留真实传递来源，映射绑定本人的记忆与视界。权威世界材料经实际暴露与感知成为主体的输入。

Acceptance 为独立数值类型。接纳规则给出数值更新函数和有序门槛，依赖变化触发相关切片重算，自动生成接纳等级与转移回执。等级与真值、置信度、动作概率分别表达。

imprint 将强制刻画展开为约束求解：目标认知是达成约束，允许的状态变换、守恒法则、资源界限与成本函数共同定义可行域和优化目标。求解、支付及脑状态提交相连。有限最优、有依据的连续最优、当前可行候选与无解/未求得结果分别表达。具体资源法则在内容中声明。

## 声明与环境

| 声明 | 对象 |
| --- | --- |
| axiom / worldlaw | 公理实例和世界变化 |
| rule | 主体推知规则 |
| norm | Require、Forbid、Permit、Prefer |
| reading / mapping | 线索、前提、解释、作用域及认知映射运算 |
| acceptance | 接纳程度更新、有序门槛与等级转换 |
| imprint | 认知目标、可调变量、守恒与最小代价求解 |
| policy | 价值分项和组合方式 |
| choice | 分数到动作分布的策略 |
| model | 有限变量、先验、因子和依赖 |
| decision | 候选、规划、选择门槛与分布 |

DefinitionEnvironment 保存模块、符号、类型及核心签名。EvaluationEnvironment 保存主体上下文、行动上下文、规则和策略版本、依据、假设及工作预算。

内容包声明人物、世界、话语、公理实例和章节协议；oracle 使用独立包。标准库实现解释、证据聚合、重演、竞争论证、规划、学习、调度、费用和保存。

## 编译结构

```text
.dv 原文
  → Token / define 别名映射
  → CST
  → 领域与算法 AST
  → 名称绑定、类型与效果
  → DV IR
  → C# 或其他目标后端
```

Token 与节点携带 SourceSpan。AST 包含函数、类型、命题、规则、切片操作、解释、规划和执行操作。IR 将闭包、匹配、分支、状态访问和阶段操作展开为目标无关表示。

| 核心结构 | 内容 |
| --- | --- |
| CompilationUnit / Module | 文件、导入和声明 |
| Decl | 类型、函数、常量和领域声明 |
| Expr | 值、调用、绑定、控制流、构造和领域操作 |
| Pattern | 字面量、绑定、构造器及记录模式 |
| Proposition | 原子、逻辑组合、量化和模型信念 |
| Effect | 状态变化、行动、话语、等待和租约 |
| TypedNode | 节点、符号、类型、能力、信息域及位置 |

类型区分领域量与信息域，effects 与能力类型表达操作所需权限。主体身份、版本、来源和阶段回执跟随运行值。数值采用名义类型、单位与范围精化，数学证明与人物领域信念分开。常量与可推导关系在编译期消解；外部值和动态求解在构造有效值时确认合同，后续运算复用已知性质。

运算符在类型确定后展开到固定节点：Interpret、Support、Transmit、Distribute、AcceptanceUpdate、SolveBalance 与 Commit。`*` 对数值矩阵是矩阵乘法，对认知映射是领域映射，两种签名由语言定义。对未改变的视界共享快照，对改变的认知依赖增量更新。

## 自举与后端

编译器前端、IR转换和目标后端用 DV 实现。第一后端生成 C#，由 .NET 工具链构建。平台 ABI 提供基础值、容器、文件、控制台和最低执行适配。

```text
C0(Compiler.dv) → Compiler1.cs → C1
C1(Compiler.dv) → Compiler2.cs → C2
C2(Compiler.dv) → Compiler3.cs → C3
C2(Core.dv)     → Core.cs      → 引擎核心
```

当前 compiler/compiler.dv 同时读取 S 表达式启动源与 surface-v2 可执行子集。新前端在 DV 中完成扫描、缩进解析、领域展开、局部变量重命名和基本名义类型检查；函数体直接生成 C# 语句。Core、会话、检查程序和正式第一章使用新语法，启动编译器仍以原语法自举。

core/55-cognition.dv 实现接纳门槛与有限域刻画：搜索枚举声明的候选空间，独立核验器重新计算可行性、资源成本和最优性；认知与资源在同一返回状态提交。搜索和核验各按候选计工作量。精确有理数由平台 ABI 提供，心理解释和搜索策略均在 DV。完整精化证明、量纲/泛型/能力系统与目标无关 Typed IR 尚待实现，不把现有名义检查称为 Lean 完整内核。

## 目录

| 目录 | 职责 |
| --- | --- |
| Kernel / bootstrap | 当前解释启动链 |
| compiler | DV 编译器、启动 seed 和平台 Runtime |
| core | 世界、观察、传播、价值、回应、费用和论证 |
| verification/session | 隔离 DV 会话 |
| verification/scenarios | 人物、世界、话语、规则和章节内容 |
| verification/checks / report | 只读判定与报告 |
| tools | 构建与进程编排 |

## 参考

[Lean 的展开与内核分层](https://lean-lang.org/doc/reference/latest/Elaboration-and-Compilation/)用于组织表层表达和核心项；[MAL](https://github.com/kanaka/mal)用于解释器结构；[GCC 多阶段自举](https://gcc.gnu.org/install/build.html)用于编译器阶段构建。认知切片和动作选择的研究见[领域语言研究](../artifacts/partial-bootstrap-core/dv-domain-language-research-2026-10-13.md)。

## 2026-10-14 动作实现收敛

旧 Kernel/bootstrap/WorldCore 解释执行链已退役。compiler/seed 独立保留 C0 需要的语法 Reader/Value；生成阶段只依赖 Runtime ABI。真实动作只有编译 DV session 的 prepare/complete，宿主和逻辑运行器复用它。


## 独立值的构建优化

dv_with_many 合并连续字段构造；dv_with_path 按更新路径构造并独立复制其余分支。法则与观察批次在首次独立复制后使用私有暂存构建器，按原顺序验证，成功后转交，失败保留原输入。session 的 prepared 事件入口仅转交调用现场刚独立构造的数据；既有事件仍保留完整快照。调用限制与等价测量方法见本目录 AGENTS.md 的“等价复制优化”。
