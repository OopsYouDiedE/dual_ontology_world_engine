# DuelView（DV）语言结构与自举设计

状态：2026-10-12 的目标规范。名称和 `.dv` 后缀由用户确定；下文的完整类型化语法与多后端体系仍是目标设计。已实现的自编译启动子集及其缺口见 compiler/README.md，不能将子集自举当作全规格实现。实现继续位于独立的 `language_core/`，不引用旧 Core 或 Godot。

## 1. 目标与完成条件

DV 是具有通用计算能力、显式主体视界和证据推演库的语言。DV 源文件经过引擎编译器生成 C#，后续可增加其他语言后端。语言自己的词法分析器、解析器、名称解析、类型与能力检查、中间表示转换、代码生成，以及全部引擎推演算法，最终均以 DV 源码提供。

生成的 C# 是构建产物。不能直接手改生成物，也不能将关键算法留在 C# 函数中，再通过 DV 的调用包装宣称自举。初始 C# 启动实现只用于产生第一份可运行的 DV 编译器，完成自举后不作为正常构建的必经路径。

外部目标工具链仍可使用：第一后端生成 C#，由 .NET 的 C# 编译器生成可运行程序集。DV→C# 编译器必须由 DV 自身实现；此次要求不包含重写整个 .NET、C# 编译器、操作系统或内存管理器。其他后端亦明确记录目标工具链和最小运行时依赖。

当前状态：解释链路仍保留 C# 启动解释器、Reader 与合同编解码。新增 `compiler/compiler.dv` 已实现独立扫描/AST 解析、名称/调用检查与 C# 后端，经 C0→C1→C2→C3 验证通用计算启动子集的自编译固定点。**完整静态类型、能力检查、目标无关 IR、闭包/模块和推演库编译迁移仍未实现，因此尚未满足本规范的完整结束条件。**

## 2. 语言结构

| 部分 | 必须提供的能力 | 用途 |
| --- | --- | --- |
| 通用计算 | 词法作用域、函数与闭包、递归、不可变绑定、显式局部可变状态 | 写解释器、编译器与算法 |
| 数据与类型 | Bool、Int、Float、Byte、Char、String、Symbol、List、Map、Option、Result；记录、代数数据类型、类型参数和模式匹配 | 表达 Token、AST、IR、证据、搜索状态与诊断 |
| 模块 | module/import/export、确定的依赖顺序、签名、受限宿主接口声明 | 分开编译器、推演库与宿主适配 |
| 主体视界 | View、主体本地 Ref、ActionContext、已获取的 ModeledView、来源与版本 | 在明确的信息边界中求值 |
| 逻辑 | Proposition、三值 Truth、逻辑变量与合一、规则、有限域量化 | 表达条件、推知与可错信念 |
| 证据推演 | Evidence、Provenance、Distribution、Likelihood、EvidenceWeight、冲突记录 | 分开概率模型与启发式权重 |
| 行动 | Effect、Prediction、Prepared、Receipt、租约/时序合同 | 分开预测、准备与实际提交 |
| 能力与效果 | Pure、ReadView、ReadAuthority、StageWrite、HostIO 等显式能力要求 | 编译期禁止主体算法越界读取或提交 |

推断、规划、社交、学习、收益与行动合法性等算法写成普通 DV 函数。专门的语法用于表达数据与操作边界，不给某个隐藏宿主求解器开入口。编译器是确定的普通程序；解析与类型检查不依赖某个角色的信念度。

标准库须提供确定的字符串索引/切片、UTF-8 编解码、字节缓冲与数值/字符转换，否则解析器和目标源码输出无法迁入 DV。编译器错误以 Result 与包含源位置的 Diagnostic 表达。宿主接口声明限定在可审计的平台 ABI，不能通过任意 C# 片段或反射绕过能力检查。

Bool 与 Truth 是不同类型。程序分支 `if` 只接受 Bool；主体命题评价得到 Truth（True/False/Unknown）和依据。Unknown 不隐式转为 false。冲突状态另行记录，既不等于 Unknown，也不强行折算为一个概率数。

第一版静态类型区分 Subjective 与 Authority 域，运行时继续检查 Ref 的 SubjectId、快照版本与来源；不能仅凭 `Ref<Subjective>` 就宣称已静态证明两个引用属于同一主体。未来可增加主体区域参数。

## 3. 书写语法

基础书写采用 S 表达式，减少自举前端的复杂度。`.dv` 是唯一的语言源码后缀。注释使用 `;`，字符串使用双引号，`'x` 表示引用的数据。代码、引用语法数据与已经验证的 AST 分属不同类型；引用列表不会自动成为可执行程序。

正式类型化语法的顶层形式：

```text
Module       = (module QualifiedName Decl*)
Decl         = Import | Export | TypeDecl | RecordDecl | TagDecl
             | FunctionDecl | ConstDecl | WorldLawDecl | RuleDecl
             | PolicyDecl | ModelDecl | ExternDecl
Import       = (import QualifiedName)
Export       = (export Name*)
TypeDecl     = (type Name [TypeParameters] Variant+)
RecordDecl   = (record Name [TypeParameters] Field*)
TagDecl      = (tag Name Type)
FunctionDecl = (defn Name [TypeParameters] (Param*) ReturnType Expr)
Param        = (Name Type)
Type         = Name | (TypeConstructor Type*)
Expr         = Literal | Name | Call | Lambda | Let | If | Match | Do
             | Construct | FieldAccess | Evaluate | Infer | Predict
             | Prepare | Commit | LocalCell
```

`[TypeParameters]` 表示规范中的可选部分，不是源码中的方括号语法。类型、构造器与导入解析由语义阶段确认；解析器不能依靠已执行的规则猜测语法含义。编译器所需的 AST 类型可写为：

```dv
(module dv.compiler.ast
  (export Expr)
  (type Expr
    (Literal LiteralValue)
    (Name Symbol)
    (Call Expr (List Expr))
    (Lambda (List Param) Type Expr)
    (Let (List Binding) Expr)
    (If Expr Expr Expr)
    (Match Expr (List Arm))))
```

这是拟定的正式语法示意；当前 seed 仅支持 README 所列 `def!`、`fn*`、`let*` 等解释子集。建立正式前端后，seed 子集迁移或显式转换，不能假装两者已经兼容。

逻辑程序可声明五类现有规则模式，但节点必须分型：

- 推知规则：前提命题 → 结论命题；保留推导来源与假设。
- 行动规则：Require / Forbid / Permit / Prefer；载荷是 Effect，含接受状态与有效期。
- 行动偏好使用 Utility；禁令/许可强度使用 Priority；证据使用 EvidenceWeight；严格概率模型使用 Likelihood 与 Prior。几种量不可互换。

模型声明定义变量域、先验与因子；观察提供证据与依赖信息。`infer` 是带算法与预算参数的库调用形式，不承诺支持任意模型的精确求解。第一概率后端可先采用有限枚举，模型之外的情形明确报告。

## 4. 三层树与中间表示

```text
.dv 文本
  → Token（位置、原文、类别）
  → Syntax/CST（列表、原子、引用；保留位置）
  → AST（函数、类型、命题、规则、效果等语言节点）
  → Bound/Typed AST（符号绑定、类型、能力、所属信息域）
  → DV IR（目标无关，闭包/分支/匹配/状态操作已明确）
  → C# 后端 / 后续其他语言后端
  → 目标工具链与平台运行时
```

原始列表树不是完整的 AST。解析、名称绑定、类型判定和后端降低各有单独的数据合同和诊断。每个节点保存 NodeId 与 SourceSpan（文件、起止偏移、行列）；SourceSpan 不参与语义等价判断。导入与符号编号采用确定顺序，不把随机 GUID、构建时刻或绝对机器路径写入语义输出。

## 5. AST 节点合同

下列是代数数据类型的结构定义，`A | B` 表示不同节点分支，非 C# 源码。

```text
CompilationUnit = { modules: List<Module>, sourceFiles: List<SourceFile> }
Module = { name: QualifiedName, imports: List<Import>, declarations: List<Decl> }

Decl = TypeDecl | RecordDecl | TagDecl | FunctionDecl | ConstDecl
     | WorldLawDecl | RuleDecl | PolicyDecl | ModelDecl | ExternDecl
Type = NamedType | TypeApply | FunctionType | TypeParameter
FunctionDecl = { name, typeParameters, parameters, returnType, body: Expr }

Expr = Literal(value)
     | NameRef(name)
     | Call(callee, arguments)
     | Lambda(parameters, returnType, body)
     | Let(bindings, body)
     | If(boolCondition, thenExpr, elseExpr)
     | Match(scrutinee, arms)
     | Construct(typeName, variantName, fields)
     | FieldAccess(record, fieldName)
     | Do(expressions)
     | CellCreate(initial) | CellRead(cell) | CellWrite(cell, value)
     | Evaluate(viewExpr, proposition)
     | Infer(viewExpr, modelExpr, evidenceExpr, algorithm, budget)
     | Predict(viewExpr, effect, actionContext, budget)
     | Prepare(authorityCapability, effect, actionContext)
     | Commit(authorityCapability, preparedToken)

Pattern = Wildcard | Bind(name) | LiteralPattern(value)
        | VariantPattern(name, fields) | RecordPattern(fields)
Arm = { pattern, optionalGuard: Expr<Bool>, body: Expr }

Term = Constant | LexicalRef(name) | LogicVariable(name)
     | ConstructorTerm(name, arguments) | SubjectRef(expr) | Slot(role)
Proposition = TruthLiteral(Truth)
            | Atom(predicate, terms)
            | ExistsObject(noun)
            | Tagged(noun, tag, comparison, value)
            | Related(noun, relation, other)
            | At(noun, place) | Kind(noun, kindName)
            | Not(proposition) | All(propositions) | Any(propositions)
            | ForAll(variable, finiteDomain, proposition)
            | Exists(variable, finiteDomain, proposition)
            | Belief(modelRef, proposition)

RuleDecl = DerivationRule(id, premise, conclusion, scope, provenance)
         | NormRule(id, mode, premise, effect, priority, utility,
                    acceptance, scope, provenance)
NormMode = Require | Forbid | Permit | Prefer
Scope = World | Chapter(owner) | Interaction(owner)

WorldLawDecl = { id, premise, transition: EffectOrFunction,
                 applicability, version, provenance }
PolicyDecl = { id, subjectScope, evaluator: FunctionRef,
               parameters, priority, version, provenance }

ModelDecl = { variables, priors, factors, dependenceDeclarations }
Evidence = { id, propositionOrObservation, source, speaker,
             basis, observedVersion, dependenceGroup, weighting }
Weighting = Heuristic(EvidenceWeight) | Probabilistic(Likelihood)

Effect = Set(noun, tag, expr)
       | Change(noun, tag, delta) | Spend(noun, tag, amount)
       | Relate(noun, relation, other, present)
       | RequireCondition(proposition)
       | When(proposition, effect) | Sequence(effects)
       | Act(methodRef, arguments) | Say(speechAct) | Think(proposition)
       | Wait(duration, effect) | Lease(scope, effect)

TypedNode<T> = { syntax: T, symbolBindings, inferredType,
                 requiredCapabilities, informationDomain, sourceSpan }
```

引用、变量和槽位是不同节点。`ExistsObject` 表示对象存在，`Exists` 表示量化，不能共用一个含糊的节点。`Belief` 的 modelRef 只选择调用者已持有的模型，不索引另一个角色的真实心智。语法可以写出尚未取得的引用，绑定/类型或运行验证必须拒绝它。

`NormRule` 根据 mode 限制字段：Prefer/Require 可有 utility；Forbid/Permit 使用 priority。没有相应语义的字段须为空，不能静默解释。推知规则不携带可执行效果。AST 的未来 Effect 节点不代表当前实现已经支持这些效果；尚未实现的后端必须给出明确诊断。

这些类型既用于程序 AST，也使规则/效果成为可检查的数据。运行值的来源、主体与版本不能仅用 SourceSpan 代替，须有独立 Provenance 与运行合同。

函数类型携带参数类型、返回类型与所需能力；闭包不会消除所捕获能力。函数声明可显式标注能力，内部局部函数可推导；模块导出的签名必须固定，递归调用必须完成能力传播。没有 Authority 参数的主体推演函数不能隐式获得该能力。

## 6. 一段源码及其 AST 解析树

拟定正式语法：

```dv
(module example
  (import dv.logic)
  (tag stock Int)
  (defn available
    ((v (View Subjective)) (x (Ref Subjective)))
    Judgement
    (evaluate v
      (all (exists x) (tagged x stock 1)))))
```

去掉 SourceSpan 等辅助字段后的语言 AST：

```text
Module("example")
├── Import("dv.logic")
├── TagDecl("stock", NamedType("Int"))
└── FunctionDecl("available")
    ├── Parameters
    │   ├── Param("v", TypeApply("View", [Subjective]))
    │   └── Param("x", TypeApply("Ref", [Subjective]))
    ├── ReturnType: NamedType("Judgement")
    └── Body: Evaluate
        ├── View: NameRef("v")
        └── Proposition: All
            ├── ExistsObject(SubjectRef(NameRef("x")))
            └── Tagged
                ├── Noun: SubjectRef(NameRef("x"))
                ├── Tag: "stock"
                ├── Comparison: AtLeast
                └── Value: Literal(Int(1))
```

其中 `(exists x)` 在命题位置产生 ExistsObject；量化使用独立的 `(exists-var ...)` 形式。`tagged` 缺省比较为 AtLeast，与当前条件合同衔接，不能自动解释成等于。名称解析后 NameRef 绑定到参数符号，Tag 绑定到 tag 声明；类型检查后 Evaluate 标记 ReadView，返回含 Truth、来源、假设与冲突信息的 Judgement。没有观察到 stock 时，结果保留 Unknown。

## 7. 后端与宿主边界

编译器统一前端与 DV IR。C# 后端写成 DV 模块，负责名字转义、字符串转义、类型布局、闭包环境、模式匹配、调用/尾递归和源位置映射。其他后端替换代码生成与运行时适配，不能另写一套不同的主体、规则或概率语义。

IR 合同包含 Program/Function/BasicBlock、具类型的值、指令与终结分支。指令包括 Const、Construct、FieldRead、Closure、Call、ViewRead 与受能力控制的操作；终结节点包括 Return、Branch、Jump、TailCall。先降低模式匹配与词法闭包，再输出目标语言；SSA 等优化形式可后续引入，不作为初次自举必需条件。类型、三值运算、依据与预算停止仍在该合同中明确，不靠目标语言的隐式真假或溢出行为决定。

IR 先保留明确的 ViewRead、Truth 运算、受能力控制的事务操作，再降低为对 DV 库函数的调用。推断与规划的循环、候选处理和归一化等来自被编译的 DV 源码；不能在生成 C# 时改为调用手写宿主 Reasoner。数字溢出、Float 非有限值、比较、集合顺序、显式随机源与预算停止合同须跨后端固定。

平台 ABI 只提供基础值、容器/内存、文件/控制台等显式 I/O、真实世界句柄与原子提交的最低适配。可达性算法、规则合法性、资源演算、认知更新和事务政策最终也迁入 DV，不能因为当前原型将它们放在 C# 就永久豁免。主体推演接收不可变 View；权限更高的执行模块另行取得 Authority capability。

同一权威实体的多个观察别名保持各自的主观信息，但真实事务内只对应一个资源状态。预测遇到未知 guard 或资源不能擅自选假分支；必须返回 Unknown，或在显式声明的分支模型中展开并标记假设。未取得观察的对象不能通过准备失败原因成为远距事实查询。

## 8. 编译器自举路径

设 `Compiler.dv` 表示全部编译器模块，`Core.dv` 表示全部引擎核心模块；CS 表示外部 C# 工具链。

```text
C0：C# 写的启动编译器，支持足以表达 Compiler.dv 的 DV 子集

C0(Compiler.dv + 必要运行库, target=C#)
  → Compiler1.cs → CS → C1

C1(同一份 Compiler.dv + 必要运行库, target=C#)
  → Compiler2.cs → CS → C2

C2(同一份 Compiler.dv + 必要运行库, target=C#)
  → Compiler3.cs → CS → C3

C2/Core 构建：C2(Core.dv, target=C#) → Core.cs → CS → 引擎核心
```

编译器前端、类型检查、IR 转换、C# 后端和自身依赖都在 Compiler.dv 的传递源码闭包内。C1 与 C2 构建不能再调用 C0 的解析器或代码生成函数。Compiler2.cs 与 Compiler3.cs 在固定源码、选项、ABI 和确定输出规则下应有一致指纹；再核对错误诊断和实际运行语义。固定点一致并不等于形式化证明或已排除所有共同错误。

现有解释器能作为开发/运行 seed 子集的辅助工具，不能取代上述编译器自编译链。先建立可表达 AST、解析和后端的最小静态子集，再将编译器与旧核心逐步迁入；最终保持正式构建可从已发布的自托管编译器重建。

自举结束须具备：冻结源码与依赖、各阶段编译命令和生成物、阶段间比较、实际编译器诊断与语义证据、全部核心模块的 DV 实现与宿主原语清单。旧 Core 行为对齐和游戏接入另按仓库规约验收，不以编译固定点代替游戏结果。

## 9. 目录演进

```text
language_core/
  Kernel/                 当前 C# seed 与宿主适配；最终缩减为平台 ABI
  bootstrap/*.dv          当前解释自举与推演原型
  examples/*.dv           通用机制例子
  compiler/               计划：lexer/parser/ast/bind/types/lower/*.dv
  backends/csharp/         计划：用 DV 实现 C# 输出
  std/                    计划：通用数据与运行库 *.dv
  core/                   计划：全部视界、推断、规划与执行政策 *.dv
  generated/              计划：可重新生成的目标代码；不手改
  DESIGN.md               本目标规范
```

“计划”目录尚未实现，不创建空目录来冒充进度。源码内容只能是通用机制；剧情仍只存在于 `.story` 与译文，不进入 DV 引擎代码。

## 10. 参考与适用边界

- [MAL](https://github.com/kanaka/mal) 提供解释器与元循环路线参考；其解释自举不能直接证明 DV 编译器自举。
- [GCC 的多阶段自举说明](https://gcc.gnu.org/install/build.html) 提供阶段构建与产物比较参考；这里比较的首先是确定生成的目标源码，而非直接假定 .NET 程序集字节恒定。
- [Roslyn 官方仓库](https://github.com/dotnet/roslyn) 是 C# 目标工具链参考。外部工具链不承担 DV 类型、主体或推演语义。
- [Lean 的 elaboration 与 kernel 分层](https://lean-lang.org/doc/reference/latest/Elaboration-and-Compilation/) 是小核心、外层展开和独立检查参考。DV 不沿用其依赖类型理论的全部逻辑前提。

本设计不宣称已经实现完整模态逻辑、通用概率编程、主观逻辑或定理证明系统。选择相关数学模型后须独立明确其公理、概率依赖与算法合同。

## 11. 借鉴 Lean 的架构：基本声明、环境与小语义核

用户明确要求借鉴 Lean 的设计思想和架构。采用其外层书写/展开与内核检查分离的原则，并将“公理/基本声明”的架构位置用于世界规则、本人接受的推演规则和行动策略。各类声明分别解释，不能仅因进入环境就取得同一种真理效力。

| 架构位置 | DV 中的对应 | 检查与作用 |
| --- | --- | --- |
| 基础声明 | WorldLawDecl | 描述世界状态怎样转移；固定适用条件、来源与版本 |
| 主体的推演基础 | DerivationRule、Evidence、明确 Assumption | 仅使用当前视界持有的依据；保留置信度、冲突与假设标记 |
| 主体的行为基础 | NormRule、PolicyDecl | 描述接受的约束、欲望/偏好和评价策略；分开合法性与选择分数 |
| 环境 | 定义环境与运行求值环境 | 模块/符号/类型固定；运行环境再明确主体、快照、法则/规则/策略版本 |
| elaboration | 名称绑定、类型/能力检查、语法糖展开 | 把易写的外层表达转换成显式且小的核心项 |
| 小语义核 | 核心类型/能力检查、条件与效果语义、依据与执行边界核验 | 检查展开结果与求解器产物的合同；不信任搜索分数代替事实或合法性 |
| 外层自动化 | 推导、证据融合、规划与社交/学习模块 | 搜索或生成带依据的结果，不能修改内核含义来保证成功 |

建议目标环境合同：

```text
DefinitionEnvironment = { modules, symbols, types, coreSignatures }
EvaluationEnvironment = {
  definitions, subjectView, actionContext,
  worldTheoryVersion, acceptedRuleSetVersion, policySetVersion,
  evidence, assumptions, inferenceConfiguration, budget
}
```

SubjectView、WorldTheory 与 PolicySet 的可用内容受能力限制；把它们列进合同不等于授予主体权威状态。世界真实法则与主体对世界法则的认识可能不同，必须保留 WorldLaw 与 SubjectRule 的区分。主体错误接受的规则可以在其视界中产生错误推演；随后由实际观察更新，不能在编译时用权威答案纠正。

引擎内核最后亦用 DV 实现，经自托管编译器产生目标代码。“小”要求可列明、可复查的核心项与运行合同；不等于整个推断器或所有语言构造都塞进内核。DV 可以通过函数、库与前端语法扩展能力，扩展产生的核心项仍经同一内核检查。

推导输出保存 Derivation（规则 ID、前提、结论、来源、视界版本）；概率推演保存 EvidenceTrace（依赖与权重/似然、更新过程）；行动选择保存 DecisionTrace（候选、预测、优先策略、合法性核验与停止原因）。各类记录的检查强度单独说明：它们提供可重演、可核对的依据，不能一概宣称是 Lean 式证明项。

策略说“优先选某行动”不会使某命题为真。策略与规范冲突时记录冲突并按显式策略处理；相反证据同时存在时保留矛盾，不通过经典逻辑爆炸推出任意结论。由此借用 Lean 的架构位置，同时保留 DV 的多视界、可错信念和带权重的推演含义。

## 当前领域语义实现（2026-10-12）

`core/*.dv` 新增世界、公理采纳、观察、状态切片、价值、回应、论证成本和历史检查点。当前公理表层节点为 `(axiom name version (parameters...) premise effects)`；DV 前端展开为普通函数与 `dv_axiom` 数据构造。它是此规范完整 WorldLawDecl 的启动实现，尚未具备独立静态类型化 IR。语义函数和检查器均由 DV 编译成 C#，JSON ABI 不参与领域推演。接口及未完成接线见 core/README.md；内容设计以设计规范本轮补充为准。
