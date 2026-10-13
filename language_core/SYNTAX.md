# DV v1 语法方案

当前实现采用 S 表达式；本文定义新的 surface-v1 书写格式。

语言核心是有主体与来源的认知切片、个人理解、可撤销采纳、规划和动作分布。算法与领域声明使用同一编译链。

以下例子展示局部语法形式，参数和名称由所在上下文绑定。

## 1 源文件和中文书写

源码后缀为 `.dv`。文件依次包含 define、module、import、export、声明和程序入口；库模块只有声明。

```dv
define 函数 defn
define 返回 return

module sample.math;
import dv.numeric;
export minimum;

函数 minimum(a: Int, b: Int) -> Int requires [] {
    返回 if a < b { a } else { b };
}
```

`define 别名 原名` 是文件内的单 token 别名，一行一条，末尾分号可选。它用于关键字、函数、参数、类型和运算符；正文按映射后的 token 解析，字符串和注释按原文保存。别名采用 ASCII 名称或常用汉字，内部名称采用 ASCII。

模块以声明依赖组成有向无环图；`import name as alias;` 提供别名，成员用 `alias.member` 引用，export 显式列名。函数可相互递归。注释采用 `//` 和 `/* ... */`，花括号划分代码块，分号结束语句。源码为 UTF-8，位置记录使用原文 UTF-16 偏移与行列。

## 2 基本类型和数据

| 类型 | 合同 |
| --- | --- |
| Bool | true/false，仅程序控制条件 |
| Int | 有符号64位整数 |
| Float | 有限IEEE-754双精度浮点数 |
| Byte | Byte为0..255，Char为一个Unicode标量 |
| String | Unicode 文本；标准库区分 UTF-16 索引与标量遍历 |
| Symbol | 通过symbol("name")构造的符号值 |
| Unit | 无结果，字面量 unit |
| List<T> / Map<K,V> | 不可变集合，Map按声明插入顺序遍历 |
| Option<T> / Result<T,E> | Some/None与Ok/Err分支类型 |
| Value | 旧ABI使用的动态值域 |

```dv
const limit: Int = 10;
let message = "文本\n下一行";
let initial: Char = 'a';
let items: List<Int> = [1, 2, 3];
let mapping: Map<String, Int> = map { "a" => 1, "b" => 2 };
```

十进制整数为 Int，带小数点或指数的数字为 Float；负号为前缀运算。字符是一个 Unicode 标量，字符串使用双引号及转义。Symbol 由 symbol("name") 构造。

列表、Map 允许末尾逗号；Map 按键组织值，lookup 返回 Option。正式集合为不可变值，更新返回副本。旧 Value 沿用现有 double 数字、可变容器和列表身份相等；结构比较由 equal/dv_same 表达。

领域量使用 Score、Probability、Confidence 等具名类型，数值转换显式书写。

## 3 类型声明

```dv
type Count = Int;

record Span {
    file: String,
    start: Int,
    end: Int
}

type Choice<T> {
    Selected(value: T),
    Missing
}

tag amount: Int;
```

`type X = T` 定义透明类型别名，record 定义具名字段，type 的分支形式定义代数数据类型。记录构造写作 `Span { file: "sample.dv", start: 0, end: 5 }`，读取用 `span.start`，更新用 `span with { end: 8 }`。

构造器可写 Selected(1) 或 Choice.Selected(1)。tag 声明领域字段及值类型，领域字段通过引用和视界访问。

## 4 函数和控制流

```dv
defn identity<T>(value: T) -> T requires [] {
    return value;
}

defn count_to(limit: Int) -> Int requires [] {
    var i = 0;
    while i < limit {
        i = i + 1;
    }
    return i;
}

let transform: fn(Int) -> Int requires [] =
    fn(x: Int) -> Int requires [] { return x + 1; };
```

```dv
return match value {
    Some(x) if x > 0 => x,
    Some(_) => 0,
    None => 0
};
```

函数类型为 `fn(T1, T2) -> R requires [Capability...]`。闭包捕获词法绑定，var 通过局部 cell 表达共享状态。导出签名明确参数、返回类型和能力。

let 表示不可重绑定的局部值，var 表示可赋值局部值。while 为循环语句，for 按集合顺序迭代。if、match 可作为表达式，表达式块的末尾值为块值；函数返回使用 return，Unit 函数可以自然结束。

模式包括字面量、绑定名、`_`、构造器和记录模式，guard 为 Bool。main 是程序入口。错误结果使用 Result。

## 5 表达式

优先级从高到低：调用/字段/索引，前缀 `- not`，`* / %`，`+ -`，`< <= > >=`，`== !=`，`and`，`or`。算术左结合；多项比较写成显式合取。

调用及普通运算按源码从左到右求值，Bool and/or 短路，if/match 求值选中的分支。普通 dv_and(a,b) 沿用急切实参求值。

Int 除法向零截断，余数与该商对应；Float 除法产生 Float。文本连接用 concat。列表索引读取元素，记录字段通过点号读取。泛型函数根据参数推导类型。

## 6 领域类型与能力

```text
AuthorityWorld, MindSnapshot, MindContext, ModeledMind
Ref<Authority>, Ref<Subjective>, LocalObject, Facet, Scope
StoredSlice, WorkingSlice, OfferedSlice, Percept
Proposition, Truth, Judgement, Provenance, Evidence, Assumption
InterpretationRule, Interpretation, Adoption, Revision
WorldLaw, DerivationRule, NormRule, ValuePolicy, ChoicePolicy
Effect, Action, Prediction, Prepared, Intention, Receipt
Score, ScoredCandidates, ActionDistribution, Probability
Confidence, EvidenceWeight, Likelihood, SourceReliability, Cost, Priority
```

认知材料携带主体、本地对象/切面、快照版本、来源事件和有效时间。ModeledMind 还包含建模者、被建模者、来源及假设。字段的未知、冲突、支持份额、来源信度与认识置信分别表达。

ReadView、ReadAuthority、Transmit、StageWrite、HostIO、Random、CognitiveWork 描述操作能力。requires 是函数效果签名，能力值由参数和宿主入口提供；Prepared 和阶段租约具有一次性消费语义。

## 7 视界和切片

```dv
within snapshot as mind {
    let stored = slice target facet facet_name from mind fields field_names;
    let working = recall stored within mind by retrieval_policy budget budget;
}
```

```dv
let offered = offer working from speaker_mind to recipients via channel;
let receipt = transmit offered using transmit_capability budget transport_budget;
let percepts = perceive receipt for listener_snapshot using observation_capability;
```

within 将显式快照绑定为词法 MindContext。slice 取得本人存储的指定切面，recall 根据检索策略形成本次 WorkingSlice。

offer 构造提供材料，transmit 产生实际传递收据，perceive 将收据投影为接收者的 Percept。提供源区分本人表达 SubjectOffer 和授权世界暴露 AuthorityExposure，通道保留传递损失。操作结果使用 Result。

## 8 命题和判断

```dv
let claim = proposition {
    exists(target) and tagged(target, amount, >= 1)
};
let judgement = evaluate claim within mind;
```

命题原子为 exists、tagged、related、at、kind、atom、believes。Truth 包含 True、False、Unknown，Judgement 还携带依据与冲突。命题区域的 and/or/not 表达三值逻辑，程序分支使用 Bool。

对象存在用 exists(ref)，有限量化用 `forall (x in domain) { ... }` 和 `exists_var (x in domain) { ... }`。tagged 显式给出比较方式；逻辑变量写 `?name`，行动角色槽位写 slot(name)。

Evidence 区分观察、证词、记忆、推导、估计和假设。assume 形成假设；infer 根据本人采纳的规则、依据和工作预算产生推导。

## 9 理解和采纳

```dv
interpretation reading(target: Ref<Subjective>, basis: Provenance) version 1 {
    id: interpretation_id;
    cue: cue_id;
    when: premise;
    conclude: conclusion;
    available: availability;
    withdraw: counterevidence;
    source_scope: source_scope;
    target_scope: target_scope;
    bridge: scope_bridge;
    support: support_weight;
    basis: basis;
}
```

```dv
let understood = interpret percept within mind
    using adopted_interpretation_rules
    memory working_slices
    budget cognition_budget;

let revision = adopt understood into snapshot by acceptance_policy;
```

interpretation 声明解释规则工厂。id 是实例身份，cue 是输入线索，when/conclude/available/withdraw/bridge 是命题，两个 scope 是语义范围，support 是 EvidenceWeight，basis 是来源。

interpret 结合实际输入、本人取用的记忆切片和规则产生解释。adopt 形成采纳、保留、拒绝或撤回的 Revision，commit_revision 提交修订；reconsider 根据新材料重审已有采纳。

同一内容的理解由本人材料、规则、指代、时间范围和预算共同决定。现有解释桥的 prior 参数在兼容层对应 support，概率解释使用单独的模型。

## 10 法则与规则声明

| 声明 | 必填字段及含义 |
| --- | --- |
| axiom | when:Proposition、effects:List<Effect>；兼容现具名公理工厂，不自动采纳或执行 |
| worldlaw | when:Proposition、effects:List<Effect>、scope:Scope、basis:Provenance；世界转移模板 |
| rule | when:Proposition、conclude:Proposition、scope:Scope、basis:Provenance；本人推知规则 |
| norm | mode:Require/Forbid/Permit/Prefer、when:Proposition、action:ActionPattern、scope:Scope、basis:Provenance；规范规则 |
| interpretation | 第9节固定字段；本人理解规则模板 |
| policy | terms:List<ValueTerm>、combine:ScoreCombiner、unknown:UnknownPolicy、basis:Provenance；分项价值评价 |
| choice | selector:Selector、basis:Provenance；评分到动作分布策略 |
| model | variables:List<FiniteVariable>、priors:PriorSpec、factors:List<Factor>、dependencies:DependencySpec、basis:Provenance；有限概率模型 |
| decision | candidates:List<Action>、eligible:EligibilityPolicy、score:ValuePolicy、planning:Option<PlanningPolicy>、admission:ActionAdmissionPolicy、distribution:ChoicePolicy、budget:CognitiveBudget；第12节求值入口 |

```dv
rule derive(target: Ref<Subjective>, basis: Provenance) version 1 {
    when: premise;
    conclude: conclusion;
    scope: scope;
    basis: basis;
}
```

声明统一采用 `Kind name<T...>(params...) version Expr { field: Expr; ... }`。工厂名称、实例身份和版本分别表达。

norm 的 Forbid/Permit 使用 priority，Require/Prefer 使用 utility。世界规则以 enforce 采纳，主体规则分别以 predict、infer、norm 采纳。adopt_law 根据模式、confidence 和 basis 形成修订。

## 11 概率模型和评分量

model 声明有限变量域、先验、因子和依赖关系。FiniteVariable 包含有序域，Assignment 是变量赋值；NormalizedPrior 使用概率，HeuristicPrior 使用权重。Factor 表达条件似然或启发式权重，DependencySpec 表达共同来源。

`condition model on evidence within mind budget budget` 产生条件分布和证据轨迹。Score、Probability、Confidence、Likelihood、EvidenceWeight、SourceReliability、Cost、Priority 分别表示不同量。

ValueTerm 根据动作和本人上下文产生 Known、Unknown 或 Conflicted 分项；ScoreCombiner 组合有序分项。UnknownPolicy 包含 ReportOnly、RejectIncomplete 和 PenalizeMissing。EligibilityPolicy 包含 RequireKnownTrue 和产生独立尝试动作的 AttemptUnknown。

## 12 规划分布和选择

```dv
choice preserve_order() version 1 {
    selector: Argmax { ties: DeclarationOrder };
    basis: declared_basis;
}

choice stochastic(temperature: Temperature, basis: Provenance) version 1 {
    selector: Boltzmann { temperature: temperature };
    basis: basis;
}
```

```dv
decision choose_response(mind: MindSnapshot, policy: ValuePolicy,
                         chooser: ChoicePolicy, budget: CognitiveBudget) version 1 {
    candidates: personally_available_actions;
    eligible: known_preconditions_and_capabilities;
    score: policy;
    planning: planning_policy;
    admission: action_admission_policy;
    distribution: chooser;
    budget: budget;
}
```

decision 返回评分、规划和动作分布。planning 为 Option<PlanningPolicy>，admission 为选择门槛，distribution 为选择策略。PlanResult 包含可执行首步、路径、即期分、回传分、来源、已比较层、耗时和停止原因。

ActionAdmissionPolicy 包含 AnyEligible 和显式 ScoreGate。有候选但未达到门槛时，结果为 NoSelection；其他结果为 Ready、NoCandidate、Incomplete、BudgetStopped。

Selector 包含 Argmax、Boltzmann、Weighted、EpsilonGreedy。Argmax 的平分策略是 DeclarationOrder 或 Uniform；Boltzmann 按 exp(score/T) 归一化；Weighted 使用显式非负权重转换；EpsilonGreedy 混合最高分选择与均匀探索。

distribute 根据首步动作及其聚合分数形成分布；choose 根据分布和随机源产生 Intention。各现有入口分别沿用自身门槛与平分规则。

## 13 预测和执行

```dv
let forecast = predict action within mind using adopted_laws
    context action_context budget prediction_budget;
let preparation = prepare intention against authority using execution_capability;
let receipt = commit prepared using execution_capability;
```

Effect 的构造器为 Set、Change、Spend、Relate、RequireCondition、When、Sequence、Act、Say、Think、Wait、Lease。

predict 在本人私有状态中形成 Prediction。prepare 根据当前世界、资源、行动上下文形成 Prepared；commit 消费准备结果，产生 Receipt。报价、预留、执行和结算使用 CostVector、Quote、Reservation、Settlement。

model_view 形成本人持有的他人模型，forecast 根据该模型预测理解；真实听者通过自己的 Percept 和认识进行理解。

## 14 宿主接口与包

```dv
extern defn read_text(path: String, io: HostIO) -> Result<String, IOError>
    requires [HostIO] abi "dv.read_text.v1";
```

extern 将签名绑定到平台 ABI。编译器、推演器与系统库用 DV 实现，ABI 承接文件、控制台、容器和原子执行接口。

包分为标准库、算法库、内容、oracle 和宿主，均使用 module/import。Speech 由字面命题、正文、指代、作用域、预设和线索组成。内容包声明人物、世界、公理实例、话语和阶段。

const 在编译阶段计算纯确定表达式。v1 的扩展范围包括通用计算、有限领域模型和词法闭包。

## 15 规范文法

以下 EBNF 中 `{}` 表示重复、`[]` 表示可选，引号内为源码符号。

```text
File = {DefineLine}, "module", QName, ";", {Import}, {Export}, {Decl}, [Main] ;
DefineLine = "define", AliasName, TargetToken, [";"], LineEndOrEOF ;
Import = "import", QName, ["as", Name], ";" ;
Export = "export", Name, {",", Name}, ";" ;
Decl = Function | Extern | Const | TypeAlias | Record | Union | Tag | Domain ;
Function = "defn", Name, [Generics], Params, "->", Type, [Caps], Block ;
Extern = "extern", "defn", Name, Params, "->", Type, Caps,
         "abi", String, ";" ;
Const = "const", Name, ":", Type, "=", Expr, ";" ;
TypeAlias = "type", Name, [Generics], "=", Type, ";" ;
Record = "record", Name, [Generics], "{", [Fields], "}" ;
Union = "type", Name, [Generics], "{", Variant, {",", Variant}, [","], "}" ;
Variant = Name, ["(", Fields, ")"] ;
Tag = "tag", Name, ":", Type, ";" ;
Generics = "<", Name, {",", Name}, ">" ;
Params = "(", [Fields], ")" ;
Fields = Field, {",", Field}, [","] ;
Field = Name, ":", Type ;
Type = QName, ["<", Type, {",", Type}, ">"]
     | "fn", "(", [Type, {",", Type}], ")", "->", Type, Caps ;
Caps = "requires", "[", [QName, {",", QName}], "]" ;
Domain = DomainKind, Name, [Generics], Params, "version", Expr,
         "{", DomainField, {DomainField}, "}" ;
DomainKind = "axiom" | "worldlaw" | "rule" | "norm" | "interpretation"
           | "policy" | "choice" | "model" | "decision" ;
DomainField = FieldToken, ":", Expr, ";" ;
Main = "main", Block ;
Block = "{", {Stmt}, [Expr], "}" ;
Stmt = ("let" | "var"), Name, [":", Type], "=", Expr, ";"
     | Name, "=", Expr, ";" | "return", [Expr], ";"
     | "while", Expr, Block | "for", Name, "in", Expr, Block
     | "if", Expr, Block, ["else", Block]
     | "within", Expr, "as", Name, Block | Expr, ";" ;
Expr = operator-precedence expression over Atom, Prefix, Postfix, Special ;
Atom = Literal | QName | "(", Expr, ")" | List | Map | Construct
     | IfExpr | Match | Lambda | Proposition ;
Literal = Number | String | Char | "true" | "false" | "unit" ;
List = "[", [Expr, {",", Expr}, [","]], "]" ;
Map = "map", "{", [Expr, "=>", Expr,
      {",", Expr, "=>", Expr}, [","]], "}" ;
Construct = QName, "{", [Name, ":", Expr,
            {",", Name, ":", Expr}, [","]], "}" ;
Postfix = call "(" [Expr {"," Expr} [","]] ")"
        | field "." Name | index "[" Expr "]"
        | update "with" "{" Name ":" Expr {"," Name ":" Expr} [","] "}" ;
IfExpr = "if", Expr, Block, "else", Block ;
Match = "match", Expr, "{", Arm, {",", Arm}, [","], "}" ;
Arm = Pattern, ["if", Expr], "=>", (Expr | Block) ;
Pattern = "_" | Name | Literal | "-", Number
        | QName, "(", [Pattern, {",", Pattern}], ")"
        | QName, "{", [Name, ":", Pattern,
          {",", Name, ":", Pattern}], [",", ".."], "}" ;
Lambda = "fn", Params, "->", Type, [Caps], Block ;
Proposition = "proposition", "{", Prop, "}" ;
Prop = fixed-precedence expression over PropAtom, "not", "and", "or" ;
PropAtom = "True" | "False" | "Unknown" | "(", Prop, ")"
         | PropPredicate, "(", PropArguments, ")"
         | ("forall" | "exists_var"), "(", Name, "in", Expr, ")", "{", Prop, "}" ;
```

```text
slice p facet p from p fields p
recall p within p by p budget p
offer p from p to p via p
transmit p using p budget p
perceive p for p using p
evaluate p within p
assume p within p because p
infer p within p using p budget p
interpret p within p using p memory p budget p
adopt p into p by p
commit_revision p using p
reconsider p within p using p budget p
adopt_law p into p as p confidence p because p
condition p on p within p budget p
distribute p using p
choose p using p
predict p within p using p context p budget p
prepare p against p using p
commit p using p
model_view p within p using p budget p
forecast p for p from p using p budget p
```

PropPredicate 对应第8节的命题原子。Special 产生具名领域节点，实参中的复杂表达式用括号分组。记录构造与控制块通过文法位置及括号区分。

## 16 编译结构

编译链为：原文 → Token/define映射 → CST → 领域与算法AST → 名称/类型/效果 → DV IR → 目标后端。

源位置跟随节点进入后续阶段。formatter 保留注释、中文名称和表达顺序。旧模式为 sexpr-v0，新模式为 surface-v1；Value 桥接旧ABI，正式领域对象使用具名类型。

C0启动旧子集，由旧编译器产生新前端；随后迁移 compiler.dv，依次构建 C1、C2、C3。

## 17 第一章接线

第一章新语法样例位于 `verification/scenarios/surface-v1-proposal/`。实际内容模块为 chapter1-laws、pragmatics、content、history、verification-config 与独立 oracle。

会话调用切片、解释、重演、论证、规划、费用和回答协议。现役 Godot/C# 负责现场与呈现，隔离 DV 会话负责逻辑演算。

## 18 第一章反推的协议类型

[第一章源码需求](../artifacts/partial-bootstrap-core/chapter1-reverse-language-requirements-2026-10-13.md)补充以下标准库类型：

| 类型 | 作用 |
| --- | --- |
| ConceptState / QuestionRef | 问题的形成、收闻和本人默认认识 |
| TemporalClaim / TimeAnchor | 事件时刻、收到时刻和命题有效范围 |
| Hypothesis / SimulationTrace / PredictionCoverage | 历史与反事实重演、阶段状态和预测覆盖 |
| CandidateSpace / FeatureComparison / CoverageCertificate | 候选域、预测与观察比较、竞争解释覆盖 |
| EstimatedArgument / ClaimSlot / PublicProposal | 本人解释论证、公开命题槽位和提案 |
| DependencyFingerprint / Invalidation | 支持输入变化与论证撤回 |
| PlanResult / ImmediateScore / ReturnedScore | 多步路径和首步价值 |
| ActionExperience / Outcome | 尝试、成功、失败、无推进、中断经历 |
| Submission / AnswerJudgement / ProtocolReceipt | 回答、依据、完整性和正式接受 |
| ActionLifecycle / Scheduler / Lease | 动作阶段、等待、取消和临时占用 |
| PresentationReceipt / VisibleOffer / PersistenceCodec | 实际呈现、可见信息和保存恢复 |

这些类型与算法由普通 DV 系统库实现，领域语法提供上下文和操作关系。
