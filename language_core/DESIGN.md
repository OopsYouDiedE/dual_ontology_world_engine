# DuelView（DV）语言结构与自举设计

状态：2026-10-13 的目标规范。名称和 `.dv` 后缀由用户确定；本轮确立易读表层语法（第 12 节），尚未实现其解析器。下文的完整类型化语法与多后端体系仍是目标设计。已实现的自编译启动子集及其缺口见 compiler/README.md，不能将子集自举当作全规格实现。实现继续位于独立的 `language_core/`，不引用旧 Core 或 Godot。

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

2026-10-13 起，正式源码采用面向阅读的 DV 表层语法：具名声明、普通函数调用、中缀运算和花括号代码块。完整合同见第 12 节。`.dv` 仍是唯一源码后缀；AST 是编译器内部结构，不要求作者按树节点书写程序。本节以下 S 表达式仅保留为旧设计与启动格式的迁移记录，不再作为正式书写目标。当前编译器仍只接受旧格式，新表层前端尚未实现。

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
  → Syntax/CST（声明、表达式、代码块、注释；保留位置与原文）
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

正式表层写法（设计示例，尚不能由当前编译器编译）：

```dv
module example;
import dv.logic;

tag stock: Int;

defn available(v: View<Subjective>, x: Ref<Subjective>)
    -> Judgement requires [ReadView] {
    return evaluate(v, proposition { exists(x) and tagged(x, stock, >= 1) });
}
```

其中 `proposition { ... }` 明确建立命题，`and` 在这个区域内是三值合取。`tagged` 显式标出比较方式；没有取得观察时仍为 Unknown。以下旧设计保留用于核对同一个 AST 的映射：

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

## 12. 易读表层语法（2026-10-13）

本节替代第 3 节的 S 表达式书写目标；第 5 节的语义节点和第 7 节的后端边界继续有效。用户要求设计更容易阅读的源码格式。本轮只修改语言设计、维护合同和状态说明，没有迁移可执行源码或实现新解析器。以下代码块均为目标语法示例，不能作为当前编译成功的证据。

### 12.1 选择与参考

采用 **具名声明 + 中缀表达式 + 显式代码块 + 分型领域声明**。算法和内容使用同一语言，不另设自然语言解释器。括号只用于参数、分组与构造器，不要求手工嵌套 AST 节点。花括号标明块边界，分号标明语句结束；缩进帮助阅读，不参与语义，避免多行公理、长表达式和自动格式化改变块结构。

参考范围：

- [Lean 的声明与定义](https://lean-lang.org/doc/reference/latest/Definitions/Definitions/)及 [notation 展开](https://lean-lang.org/doc/reference/latest/Notations-and-Macros/Notations/)：借鉴可读外层语法进入显式内部结构的分层；不借此赋予 DV 可错信念以定理真理的效力。
- [Rust 的模式匹配](https://doc.rust-lang.org/stable/book/ch06-02-match.html)：借鉴具名分支和构造器模式，便于分别处理 True、False、Unknown，以及 Option/Result。
- [Racket 的 quote 边界](https://docs.racket-lang.org/guide/quote.html)：借鉴代码与语法数据分开的原则；旧解释器的引用语义须独立迁移，不将数据列表自动执行。

不引入隐式主体、自动取权威状态、自然语言猜测、任意目标语言嵌入或无检查宏。首轮表层前端仅提供固定语法糖；扩展机制另行设计。

### 12.2 普通算法的写法

以下对照取自 `core/00-data.dv` 的 `dv_clone`，保留递归、复制、访问顺序与原 ABI；不是新增领域规则。

旧写法：

```dv
(defn dv_clone (value)
  (if (rt_is_list value)
    (let (result (rt_list) i 0)
      (do (while (rt_lt i (rt_count value))
        (do (rt_push result (dv_clone (rt_at value i)))
            (set i (rt_add i 1)))) result)) value))
```

新写法：

```dv
defn dv_clone(value: Value) -> Value {
    if rt_is_list(value) {
        let result = rt_list();
        var i = 0;
        while i < rt_count(value) {
            rt_push(result, dv_clone(rt_at(value, i)));
            i = i + 1;
        }
        return result;
    } else {
        return value;
    }
}
```

`Value` 是启动 ABI 的动态值域标记，不是完成静态领域类型化的证明。`let` 禁止重新绑定；`var` 才允许赋值。这约束绑定，不会自动冻结 `rt_list` 所引用的可变容器；上例仍沿用现有“创建新列表再写入”的合同。`return` 返回当前函数，`while` 是语句，不产生可用于计算的值。旧代码若依赖 `while` 返回 false，转换时必须显式保留 false，不能把语句返回值改成 Unit 偷换行为。

常用写法：

```dv
defn dv_min(a: Value, b: Value) -> Value {
    return if a < b { a } else { b };
}

main {
    let result = dv_min(20, 22);
    rt_print(result);
}
```

块作表达式时，末尾不带分号的表达式为块值；函数不支持隐式返回，必须使用 `return`（Unit 函数可自然结束）。`if` 作表达式必须有 `else` 且两分支类型一致。条件一律为 Bool；函数调用参数和二元操作数从左到右求值。普通调用 `dv_and(a, b)` 保留参数急切求值；只有 Bool 运算符 `and` / `or` 短路，转换工具不得将急切调用机械替换成短路运算。

### 12.3 词法与表达式合同

| 项目 | 正式写法与规则 |
| --- | --- |
| 函数 | `defn name(x: Type, y: Type) -> Type { ... }`；保留 defn，减少迁移改名 |
| 类型参数 | `defn identity<T>(x: T) -> T { return x; }`；类型应用 `List<T>` |
| 名称 | 初版保持 ASCII 字母/下划线开头，后续可含数字；中文用于字符串/注释，Unicode 名称另行设计 |
| 注释 | `//` 行注释、`/* ... */` 非嵌套块注释；旧 `;` 注释只在旧格式模式解析 |
| 字符串 | 双引号，沿用现有转义及 Unicode 文本；首轮不引入插值或模板执行 |
| 数字 | 十进制数字，负号是前缀运算；启动模式沿用有限 double，不暗中迁成 Int |
| 集合 | `[a, b, c]` 为列表；`Type { field: value }` 为具名记录构造；不设 JSON 装载语法糖 |
| 字段 | `record.field` 仅用于已绑定类型的记录，不替代动态 `dv_get(record, "field")` |
| 赋值 | `name = expr;` 仅修改已声明的局部 var；`==` 才是比较 |
| 模块 | `module dv.compiler.ast; import dv.logic; export available;`；启动迁移阶段不得假装模块已支持 |
| 能力 | `requires [ReadView]` 是函数签名的一部分；既不创建 capability 也不授予权威句柄 |

优先级从高到低：调用/字段/索引 → 前缀 `-`、`not` → `* / %` → `+ -` → `< <= > >=` → `== !=` → `and` → `or`。算术左结合；比较和相等不允许连写，`a < b < c` 必须改为显式合取。`%` 只在后端定义了同一数值合同后开放，启动迁移先拒绝。启动 `==` 映射 `rt_eq`，列表仍按身份比较；结构比较必须显式调用 `dv_same`，不能因外观变化更换含义。

普通表达式中的 `and` / `or` / `not` 只接受 Bool。命题区域内同样的字面运算符创建 All/Any/Not 节点，不执行 Bool 分支，也不把 Unknown 当成 false。运算符依据语法区域和类型绑定，不依据当前世界状态解析。

泛型参数只在声明和类型位置使用 `<...>`；泛型函数调用先采用类型推导，不引入含糊的 `f<T>(x)`。嵌套类型里的 `>>` 解析为两个闭合符号，初版无移位运算符。列表索引仅作为既有容器访问的语法糖，不开放通过索引赋值；越界规则沿用运行时合同。

### 12.4 类型、匹配与内部树

```dv
module dv.compiler.ast;
export Expr;

type Expr {
    Literal(value: LiteralValue),
    Name(name: Symbol),
    Call(callee: Expr, arguments: List<Expr>),
    If(condition: Expr, then_branch: Expr, else_branch: Expr)
}

record SourceSpan {
    file: String,
    start: Int,
    end: Int
}

defn truth_label(value: Truth) -> String {
    return match value {
        True => "true",
        False => "false",
        Unknown => "unknown"
    };
}
```

`type` 定义带标签的分支，`record` 定义字段。构造器通过名称解析唯一绑定；模式不得调用任意函数。`match` 要求覆盖所有分支或显式 `_`，重复或不可达分支诊断；分支可为表达式或代码块。程序 Bool 字面值为 `true/false`，Truth 构造器为 `True/False/Unknown`。冲突是 Judgement 的独立字段，不新增一个与 Unknown 混用的布尔值。

作者声明 AST 类型仍使用普通语言语法；编译器持有 AST 的事实不再决定所有算法的外观。代码引用以后使用明确的 `syntax { ... }` 产生 Syntax 数据，须单独检查；初版表层前端不支持它，也不支持 `'`。旧解释器引用程序留在旧模式，禁止无损迁移声明覆盖这个缺口。

### 12.5 公理与领域计算

当前 `(axiom Name Version (Parameters...) Premise Effects)` 首先有一个完全显式的可读对应形式：

```dv
axiom name(x: Value) version 1 {
    when: make_premise(x);
    effects: make_effects(x);
}
```

`make_premise` / `make_effects` 是占位库签名，并非新增实现或具体世界规则。此形式按原参数顺序展开为具名函数，返回 `dv_axiom("name", 1, premise, effects)`，仍执行已有名称和元数核验。声明只构造公理数据，不采纳、不执行、不证明结论；`when` 在声明内是字段标签，不是让编译器调用权威世界做判断的指令。

完整类型化阶段再提供分开的声明（各表达式名仅为通用签名占位）：

```dv
worldlaw transition(x: Ref<Authority>) version 1 {
    when: condition_of(x);
    effects: transition_of(x);
}

rule derive(x: Ref<Subjective>) version 1 {
    when: premise_of(x);
    conclude: conclusion_of(x);
}

policy choose(view: View<Subjective>) version 1 {
    score: score_candidates(view);
}
```

`worldlaw` 绑定 WorldLawDecl，`rule` 绑定 DerivationRule，`policy` 绑定 PolicyDecl。推知规则不能写物理 effects，策略评分不能写 conclude；禁令/许可与行动偏好的完整表层声明另行补齐，不先用一个宽松节点接受所有载荷。既有 axiom 实例的运行采纳域保持不变，不能由迁移器猜测 worldlaw/rule 分类。命题用 `proposition { ... }`，效果的第一阶段仍写具名构造函数，减少过早引入新效果解释器。

例如第 6 节的 `tagged(x, stock, >= 1)` 只在命题语法区域内合法，生成带比较字段的 Tagged；其中 `>= 1` 是比较参数专门形式，不是普通函数实参。对象存在写 `exists(x)`；有限量化写 `forall (x in domain) { ... }` / `exists_var (x in domain) { ... }`，避免把存在性与量化混作同一个 AST。量化域必须有限，命题合取不通过 Bool 短路跳过依据采集。

`evaluate(view, proposition)`、`infer(view, model, evidence, algorithm, budget)`、`predict(view, effect, context, budget)`、`prepare(authority, effect, context)`、`commit(authority, token)` 均有显式实参和独立合同。新写法不引入“当前角色”的隐式全局读取，也不把预测提交到世界。能力、来源、主体和版本核验继续遵循第 5、7 节。

### 12.6 最小文法与诊断

下列 EBNF 确定首个表层解析器的结构；类型/模块/命题等完整构造属于后续阶段。`[]` 表示可选，`{}` 表示重复，带引号的符号才是源码字面量。

```text
Program  = { Function | Axiom }, Main ;
Function = "defn", Name, "(", [Params], ")", ["->", Type], Block ;
Params   = Param, {",", Param} ;
Param    = Name, [":", Type] ;
Axiom    = "axiom", Name, "(", [Params], ")", "version", Number,
           "{", "when", ":", Expr, ";", "effects", ":", Expr, ";", "}" ;
Main     = "main", Block ;
Block    = "{", {Stmt}, [Expr], "}" ;
Stmt     = ("let" | "var"), Name, [":", Type], "=", Expr, ";"
         | Name, "=", Expr, ";"
         | "return", Expr, ";"
         | "while", Expr, Block
         | "if", Expr, Block, ["else", Block]
         | Expr, ";" ;
Expr     = Primary | Prefix | Binary | Call | IfExpr ;
Call     = Name, "(", [Expr, {",", Expr}], ")" ;
IfExpr   = "if", Expr, Block, "else", Block ;
Primary  = Number | String | "true" | "false" | Name | "(", Expr, ")" ;
```

表达式按 12.3 的固定优先级解析（递归下降加 precedence climbing），不是递归尝试 Binary 的无优先级文法。类型初期只开放 Value；省略签名等价于 Value，不悄悄声称推导了静态类型。函数允许前向引用和递归；初版沿用禁止局部重名/遮蔽的约束。`main` 必须且仅有一个，位置不限；生成文件由工具显式选择库模式时才允许无 main，不能根据文件名猜测。

花括号 `if` 后若遇分号，作为表达式语句；否则作为控制语句。表层 AST 增加 Block、LocalBinding、Assign、Return、While，再明确降低到现有后端；不通过简单字符串替换生成 S 表达式。循环体和语句分支必须正常结束为 Unit 或显式 return，拒绝悄悄丢弃裸块值；Unit 属于正式类型域增量。return 的所有路径与不可达语句由控制流检查处理。

诊断至少保留文件、原文 span、行列、类别、期望项和实际项；展开节点关联作者的原位置。示例：缺少分号、对 let 赋值、Bool/Truth 混用、未知构造器、未实现的语法、缺少 required capability。formatter 保留注释，不重排声明、实参、premise 或 effect；幂等格式化只改变空白。启动 Number 的有限值和运算顺序不因换语法改变。

### 12.7 落地顺序与验收边界

1. **表层计算子集**：在 DV 内实现词法、语句/表达式解析和独立 lowering；复用现有名称/元数检查与 ABI。显式 `--syntax surface-v1` / `--syntax sexpr-v0` 选择格式，旧脚本默认旧模式，禁止解析失败后自动换模式。上述选项是待实现合同，当前命令不可使用。
2. **可审阅迁移**：保留旧源码指纹；提供结构化转换器，输出表层源码与位置映射，不重命名 API，不重写算法。先迁移小型通用函数，再迁移 compiler.dv；新解析器由旧 DV 编译器启动，但不是由 Python/C# 代为解析。以实际输入/输出、求值次序、副作用和错误诊断逐例核对。
3. **新语法自举**：迁移后的同一份表层 compiler.dv 依次生成新 C1/C2/C3，封存每阶段源码/ABI/程序集，比较确定生成源码固定点并运行独立程序。旧格式自举证据不当作新前端证据。
4. **完整声明**：随后实现模块、记录/代数类型、match、能力和命题区域，进入统一类型化 AST/IR；再迁移 core、session、内容和独立 oracle。未实现项必须明确拒绝，禁止因后端使用 object 而跳过检查。旧解释器链路与 quote 迁移另列证据。

语法变更只改变书写与前端。全部公理、参数、接受条件、预算、执行与判断语义须保持；必要语义变化另立说明和基线，不混入同条件迁移。内容仍仅置于 DV 内容目录，oracle 不进入主体包；现役 Godot/旧 Core 不在本次语法设计范围内。

本轮只完成设计一致性审阅，不运行流程回归，不触发一级/二级/三级游戏验收，也不将语法示例当作人物或游玩改善证据。实现后需分别记录编译器工程事实与正式会话事实。
