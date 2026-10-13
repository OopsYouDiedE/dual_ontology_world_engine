# DV v1 完整语法方案

状态：2026-10-13 的设计交付，尚未实现。本文统一领域层与算法层，取代 DESIGN.md 第 12 节中不完整的表层语法目标；已有实现仍使用 S 表达式。完整方案指本版语言的声明、表达式、操作、类型、边界和诊断均有合同，不意味着语言已经编译运行，或包含任意自然语言理解和无限推断。

本版领域核心是：有主体/版本/来源的切片，实际传递与感知，本人理解和可撤销采纳，动作评分与选择分布，意图与真实执行。全部核心与编译器最终用 DV 实现；C# 只保留平台 ABI。人物/世界/话语/公理实例和 oracle 保持内容包隔离。

除明确完整文件之外，代码块是局部语法片段，premise、basis、budget等名称表示调用者已绑定的参数，不是可自动补齐的全局值。标准库的协议类型/构造器是用普通DV record/type/defn定义的受检合同，不另增一套数据语法。实现顺序不使尚未实现形式变成允许静默执行的输入。

## 1 源文件和中文书写

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

文件结构：头部 define、一个 module、import、export、声明，程序模块可有一个 main；库模式无 main。导入只装载声明，不运行顶层 I/O。依赖图必须无环，函数可相互递归；导入符号冲突必须使用 `import name as alias;` 解决，成员通过 `alias.member` 引用。export 显式列名，不传播 define；oracle 模块不能被主体模块的传递依赖引用。

define 方向为 `define 别名 原名`，一行一条，末尾分号可选。仅当前文件的完整 token 映射；目标为一个规范名称、关键字或运算符，不是代码片段。禁止重复、占用保留字/rt_前缀、映射 define、自映射、链与环、正文中晚定义。字符串/注释不替换，原中文拼写/span保留。首版别名字符范围沿用 DESIGN 12.8；内部绑定名为 ASCII，不自动 Unicode 归一化。关键字也可被映射，先规范 token 再解析。

注释为 `//` 与非嵌套 `/* ... */`；块用花括号，语句以分号结束，缩进不参与语义。文件编码 UTF-8；SourceSpan 记录原文 UTF-16 偏移及行列，不使用规范拼写长度重算位置。

## 2 基本类型和数据

| 类型 | 合同 |
| --- | --- |
| Bool | true/false，仅程序控制条件 |
| Int | 有符号 64 位，溢出诊断，无静默环绕 |
| Float | 有限 IEEE-754 double，非有限结果诊断；不隐式与 Int 混算 |
| Byte | 0..255；Char 为一个 Unicode 标量，代理项不是合法 Char |
| String | Unicode 文本；标准库区分 UTF-16 索引与标量遍历 |
| Symbol | 通过 symbol("name") 显式构造，区别于 String 与名称引用 |
| Unit | 无结果，字面量 unit |
| List<T> / Map<K,V> | 正式领域集合不可变；改变返回新值，Map 保留声明插入顺序 |
| Option<T> / Result<T,E> | Some/None、Ok/Err，必须显式分支；没有隐式解包 |
| Value | 仅旧 ABI 迁移的动态值域，不用于证明主体/能力安全 |

```dv
const limit: Int = 10;
let message = "文本\n下一行";
let initial: Char = 'a';
let items: List<Int> = [1, 2, 3];
let mapping: Map<String, Int> = map { "a" => 1, "b" => 2 };
```

十进制整数为 Int，含小数点或指数为 Float；前缀负号属于表达式。字符串/字符沿用基本转义，新增 `\u{HEX}` 表达完整标量；字符转义必须得到一个标量。列表、Map 允许末尾逗号；空集合必须由类型上下文确定元素类型。Map 重复键诊断，不静默覆盖；运行 lookup 返回 Option。显式数值转换检查范围，禁止自动将 Score、Probability 等领域量转换成 Float。

旧容器经 `Value` 保留原可变 ABI，原数字仍为有限 double，旧 rt_eq 对列表按身份比较。typed 集合用显式 equal 做结构比较；迁移必须写明边界，不把 `==` 偷换成深比较。正式 `==` 对可比较基本值比较值，不开放集合默认相等。

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

`type X = T` 是透明别名，不建立新的权限或量纲。record 构造 `Span { file: "sample.dv", start: 0, end: 5 }` 必须且仅有声明字段，顺序不影响字段绑定，求值仍按源码顺序；读取 `span.start`，更新 `span with { end: 8 }` 返回新记录。分支构造器可唯一解析时使用 Selected(1)，否则 Choice.Selected(1)。没有继承、隐式字段、运行时反射或任意对象注入。

领域量和能力句柄为内建名义类型，不能用 type 别名伪造。tag 声明领域字段及值类型，区别于普通 record 字段；其访问仍需要主体引用、视界和来源。

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

函数类型为 `fn(T1, T2) -> R requires [Capability...]`；闭包捕获普通绑定时捕获值，捕获 var 时使用显式局部 cell语义，禁止逃逸捕获阶段租约/Prepared等线性值。要求的能力随调用和闭包传播；能力列表可省略为待推导，但导出签名和 extern 必须显式填写。普通递归有明确运行预算，无自动终止证明承诺。

let 不可重绑定，var 允许赋值；不允许局部重复绑定或遮蔽外层名称。两者均不冻结 Value 指向的旧容器。赋值只允许局部 var，禁止直接写 record 字段/索引/主体世界。while、for 的循环体为 Unit 或显式 return；for 按 List 顺序或 Map 插入顺序有限迭代，写作 `for item in items { ... }`。v1 不支持 break/continue、异常语法、默认参数或重载；错误用 Result 返回，程序边界可报告失败。

if 同时支持语句和表达式；表达式形式必须有 else 和同类型分支。块末尾不带分号的表达式为块值；函数返回必须显式 return，Unit函数可自然结束。所有非 Unit 返回路径必须完整；不可达语句诊断。main 是无参数、Unit、HostIO 的程序入口，可调用有显式上下文的会话启动函数。

```dv
return match value {
    Some(x) if x > 0 => x,
    Some(_) => 0,
    None => 0
};
```

模式可为 `_`、绑定名、基本字面量、构造器模式、record模式；record模式必须列齐字段或使用结尾 `..`。分支按源码顺序匹配，guard仅Bool、无提交/I/O；带guard不算覆盖整个构造器。要求穷尽，无效/不可达分支诊断。模式中不可执行普通函数。

## 5 表达式

优先级高到低：调用/字段/索引、前缀 `- not`、`* / %`、`+ -`、`< <= > >=`、`== !=`、`and`、`or`。算术左结合，比较/相等不得链写。% 仅 Int，向零截断后的余数，除零诊断；Int / 返回 Int 的向零截断商，Float / 返回 Float。列表索引越界产生运行诊断，Map不用索引隐式解包；文本连接为 concat，不重载数值加法。

普通调用和二元运算从左到右求值；Bool and/or短路，not仅Bool。普通函数 dv_and(a,b) 保留急切实参求值，迁移不可改为短路。if、match只执行选中分支；记录/列表/Map构造按源码顺序。泛型调用依类型推导，无法推导时报错，v1无 `f<T>(x)` 语法；嵌套类型的 `>>` 是两闭合符，无移位运算。

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

每个切片/认知结果携带 owner、snapshot_version、本地对象/切面、事件来源与有效时间。字段未知/冲突以及支持份额、信度、置信分列。对他人的模型额外携带 model_owner、modeled_subject、basis、assumptions；不由模型类型取得目标真实认识。

ReadView、ReadAuthority、Transmit、StageWrite、HostIO、Random、CognitiveWork为能力种类。实际 capability 必须由参数或受信任入口提供；requires只约束调用，不生成能力。Prepared、事务租约等线性值只消费一次，不能复制/序列化后再次提交。普通Ref不是访问授权。

静态检查类型/效果/显式上下文和阶段混用；动态核对主体身份、snapshot_version、可闻/可达、引用投影、剩余预算和一次性token。v1不宣称依赖类型或跨主体身份完全静态证明。Value不得隐式转换成任何主体上下文、依据、概率或能力。

## 7 视界与切片原生操作

```dv
within snapshot as mind {
    let stored = slice target facet facet_name from mind fields field_names;
    let working = recall stored within mind by retrieval_policy budget budget;
}
```

within仅绑定显式MindSnapshot到词法MindContext，无全局“当前人”。target为本人的LocalObject/Ref，facet与fields由绑定/字段检查确认。slice读取本人存储材料，缺字段保留Unknown；recall返回实际取用材料和预算收据，不自动增强真值。首轮使用“完整取用所选字段”的显式检索策略保留现机制，不加入随机遗忘、激活或注意力公式。

```dv
let offered = offer working from speaker_mind to recipients via channel;
let receipt = transmit offered using transmit_capability budget transport_budget;
let percepts = perceive receipt for listener_snapshot using observation_capability;
```

offer只构造提供材料，不发送、不更新听者；材料来自提供者本人持有内容或可信权威提供端获准暴露的切片。transmit执行实际传递，保留路径/损失与接收事件；perceive只能基于真实收据和对应授权投影，不能传入裸权威世界恢复全文。两种提供源用不同构造器标注 SubjectOffer/AuthorityExposure，无法互换来源。

每个操作返回 Result，包含明确拒绝/未到达/未知；上述示意 let 只展示组合形态，正式调用者须用 match 解包。事务提交/获取收据不允许通过忽略Result直接往下一阶段执行。

## 8 命题、依据与判断

```dv
let claim = proposition {
    exists(target) and tagged(target, amount, >= 1)
};
let judgement = evaluate claim within mind;
```

命题区域的固定原子为：`exists(ref)`、`tagged(ref, tag, Compare value)`、`related(ref, relation, other)`、`at(ref, place)`、`kind(ref, kind)`、`atom(predicate, terms...)`、`believes(model, proposition)`，以及True/False/Unknown、and/or/not、括号与有限量化。`forall (x in domain) { proposition }`、`exists_var (x in domain) { proposition }` 是量化，exists(ref)是对象存在。逻辑变量写 `?name`，只在显式rule/model变量绑定区合法，不等于词法变量；slot(name)是行动角色槽位，不能读取主体外部实体。

命题and/or不做Bool短路丢弃依据，保留三值和冲突。Unknown不转false；冲突是Judgement独立状态。tagged的比较参数必须显式，既有默认AtLeast迁移写成>=，不改为相等。believes只引用本人持有的ModeledMind；无模型返回缺依据，不查询目标真实脑内。

Evidence构造必须有真实事件或明确Assumption；来源类型区分观察、证词、记忆、推导、估计和假设。不能用作者填一个SourceSpan代替实际事件。`assume claim within mind because basis`只生成带标记的Assumption，不写事实。`infer claim within mind using rules budget budget`返回有依据的Judgement/Derivation，只有本人已采纳且适用规则参与。

## 9 理解与采纳

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

此声明生成InterpretationRule，不生成某人物的理解结果。字段类型：id为当前主体规则集内唯一的String实例标识；cue为Symbol/String协议标识；when/conclude/available/withdraw/bridge为Proposition；两个scope为Scope；support为EvidenceWeight；basis为Provenance。全部必填，Unknown或无反证用显式命题表示，不采用静默默认。所有字段表达式在规则工厂实例化时构造数据，不能在该阶段读取真实听者或执行动作。声明工厂名称与规则实例id不同；其他法则也通过显式实例构造器指定实例身份，不按工厂名静默覆盖。

```dv
let understood = interpret percept within mind
    using adopted_interpretation_rules
    memory working_slices
    budget cognition_budget;

let revision = adopt understood into snapshot by acceptance_policy;
```

interpret仅接受本主体实际Percept、本人可取用切片/规则与明确预算，输出解释、作用域桥、来源、未知/冲突、使用材料和认知工作收据。相同输入可因本人材料/规则不同而产生不同解释，不强制差异。它不提交采纳。adopt返回Revision，记录accepted/retained/rejected/withdrawn和原因；用 `commit_revision revision using mind_capability` 提交时核对主体/原版本并消费一次。后续新证据触发 `reconsider adoption within mind using policy budget budget`，撤回解释不等于证明其否定。

原有解释桥中的prior=0.6等未标定支持参数在v1保持EvidenceWeight意义，迁移到DTO的原字段仅兼容现算法，不能宣称成为校准Probability。有概率模型的解释器另通过显式模型合同产生Distribution<Interpretation>，不强制所有理解采用RSA或随机抽样。

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

全部形式为 `Kind name<T...>(params...) version Expr { field: Expr; ... }`。version为正Int/显式旧Value桥接，name唯一，参数具词法作用域，泛型可省；字段顺序任意但求值按源码顺序，重复/缺字段/额外字段诊断。仅norm需按mode附加：Forbid/Permit有priority:Priority而无utility，Require/Prefer有utility:Score而无priority；字段不合法不能静默忽略。解释规则与推知规则不携带物理效果，choice不产生命题真理。

```dv
rule derive(target: Ref<Subjective>, basis: Provenance) version 1 {
    when: premise;
    conclude: conclusion;
    scope: scope;
    basis: basis;
}
```

世界enforce、主体predict/infer/norm的采纳分开。`adopt_law law into context as mode confidence confidence because basis`返回Revision；物理采纳由权威能力提供，主体采纳由本人依据提供。规则声明不自动采纳。旧axiom实例不得仅据字段猜测改成rule/worldlaw。

## 11 概率模型与评分类型

模型变量只允许声明有限枚举域；Factor函数返回Likelihood或明确HeuristicWeight，不混算。PriorSpec必须明确NormalizedPrior或HeuristicPrior；先验/因子/依赖组缺失诊断。同相关源重复证据不能按独立证据累加。首版精确枚举有预算和明确停止结果，不承诺无限或连续模型求解；非有限模型由前端明确拒绝。

`condition model on evidence within mind budget budget`返回Distribution和EvidenceTrace，使用本人证据；它是模型条件化，不是写入客观真值。源信度、认识置信、支持份额、似然、合理度、代价、价值分和动作概率是不同名义量。构造器如confidence(x)、weight(x)、probability(x)、score(x)、cost(x)明确校验范围/量纲，不提供隐式转换；score/cost必须有限，概率/置信/信度为[0,1]，Cost非负。

## 12 评分分布与动作选择

前述标准库合同补全如下。它们使用普通type/record/函数表达，字段类型在导出签名中固定：

- ValueTerm含label和evaluate函数，签名为`fn(Action, MindContext) -> TermResult requires [ReadView]`；TermResult为Known(Score, Provenance)、Unknown(MissingRefs)、Conflicted(ConflictRefs)。ScoreCombiner显式接受有序分项并返回ScoreBreakdown，预算/来源由同一上下文核验，不在语言里隐式固定总分公式。
- UnknownPolicy为ReportOnly、RejectIncomplete、PenalizeMissing(Cost)。ReportOnly保留无完整分数状态，不能自动进入概率归一化；PenalizeMissing只按已声明策略处理未知项，仍标注不完整。现有按未知项数量计代价的规则通过此策略保留，不改未知事实。
- EligibilityPolicy为RequireKnownTrue或AttemptUnknown(AttemptMethod)，后者产生独立“尝试”候选，不把原前提改为True。两者都保留被拒/未知原因并受权威执行门禁；主体不可见的原因只存执行侧。
- FiniteVariable含name、ordered_domain；Assignment为这些变量到域值的完整映射。NormalizedPrior含有序Assignment与Probability，须归一化；HeuristicPrior含有序Assignment与非负有限权重，其标准化只在显式模型操作中进行。Factor含name、依赖变量、Likelihood或HeuristicWeight函数；DependencySpec列明共享源/条件依赖，重复身份核验。
- Speech含literal:Proposition、surface:String、referents:List<ReferentBinding>、scope:Scope、presuppositions:List<Proposition>、cues:List<Cue>。自然语言正文不决定独立真值；字面/预设/暗示分列，不从文案自动补作者私义。传递后内容损失仍进入Percept。
- Scope由内容声明的领域/时间锚点/对象范围构成，通过命名构造器明确绑定；Scope与普通String不隐式互换。Provenance含来源种类、产生主体、实际源事件和父边，Assumption独立标记；可信收据核对这些事件确实存在，不以自由填写元数据授予知识。
- Budget分别为认知工作、模型展开、预测、真实行动/资源等合同；限额是冻结配置，操作产生收据并扣实际工作，同一付费结果复用必须引用原收据。ABI执行指令限额与游戏代价分开，不自动互换计时单位。
- Empty/invalid分布不是合法概率分布：DecisionResult为Ready(分数、分布、认知收据)、NoCandidate(原因)、Incomplete(未知/冲突)、BudgetStopped(已耗/剩余)；参数非法和身份越界为Result错误。类型显式区分正常停滞与程序错误，不用空列表同时代表所有结果。

每个协议构造器仅验证并构造数据，具体evaluate/combine/infer算法仍由DV函数提供；没有隐藏宿主评分器。列表中的额外协议名称是标准库类型，不是新增语法关键字。

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

Selector为闭合类型：Argmax{ties:DeclarationOrder/Uniform}、Boltzmann{temperature:T>0}、Weighted{transform:fn(Score)->NonnegativeWeight}、EpsilonGreedy{epsilon:[0,1],ties:DeclarationOrder/Uniform}。Weighted要求转换函数纯且显式，有负值/全零即Result错误，不临时平移。Boltzmann按exp(score/T)归一化，计算减去最大值稳定化；参数与分数尺度一起封存。epsilon-greedy以(1-epsilon)使用argmax分布、epsilon使用合法集合均匀分布。Argmax Uniform平分时均匀分布，DeclarationOrder集中于声明序列最先者。

```dv
decision choose_response(mind: MindSnapshot, policy: ValuePolicy,
                         chooser: ChoicePolicy, budget: CognitiveBudget) version 1 {
    candidates: personally_available_actions;
    eligible: known_preconditions_and_capabilities;
    score: policy;
    distribution: chooser;
    budget: budget;
}
```

decision为有明确参数的求值入口，返回Result<DecisionResult>，包含ScoredCandidates、ActionDistribution、被拒/未知候选和认知收据，不抽样、不执行。candidates为本人可产生的List<Action>，eligible为对候选及本人上下文求值的EligibilityPolicy，score为已采纳ValuePolicy，distribution为已采纳ChoicePolicy，budget为本次工作限额；字段全部必填。拒绝跨主体、跨快照拼分及重复动作身份，未知处理必须由EligibilityPolicy/ValuePolicy显式声明，无候选返回NoCandidate。

`distribute scores using chooser`为单独分布操作，返回Result<ActionDistribution>；分布支持集只含许可候选，概率有限且合计1。合法性/客观门禁不能被大负分替代，权威原因不可泄漏为本人未获知识。`choose distribution using random_source`返回Intention及抽样收据；DeclarationOrder集中分布不消耗随机源，其他选择按冻结规则消耗。动作选择概率不等于命题置信或成功概率。

v1不规定所有人物共同采用某公式。第一章迁移先用显式Argmax DeclarationOrder保留现决策/平分含义（其他评分排序按实际旧入口分别核对），新增分布只是记录。随机实验另立选择器/参数/种子/候选顺序的基线，不挑最佳种子，不放宽剧情条件。

## 13 效果预测和真实执行

Effect是闭合代数类型，通过具名构造器Set、Change、Spend、Relate、RequireCondition、When、Sequence、Act、Say、Think、Wait、Lease构造，不用裸字典猜操作。每个构造器按DESIGN第5节含义类型化，When以Proposition为条件，未知预测保留Unknown或明确假设分支。后端尚未支持某构造器时编译诊断，禁止静默跳过。

```dv
let forecast = predict action within mind using adopted_laws
    context action_context budget prediction_budget;
let preparation = prepare intention against authority using execution_capability;
let receipt = commit prepared using execution_capability;
```

predict返回私有Prediction，不写现实/本人事实。prepare返回Result<Prepared>，核对当前真实执行条件、资源/报价/碰撞及主体合同；commit消费Prepared一次，核对世界和本人版本并产生Receipt。正式调度必须经真实代价器，不由偏好策略自己报价。选择或完成推演不算执行/获知/分享；Receipt只有经实际观察才能更新有关主体。

`model_view target within mind using modeling_policy budget budget`返回本人ModeledMind（含假设/依据），不是目标真实快照；`forecast meaning for model from offered using interpreter budget budget`只在私有模型中计算，记录预算且不提交目标。任意嵌套模型受显式深度和工作预算限制，不展开无限心智递归。

## 14 宿主接口与包

```dv
extern defn read_text(path: String, io: HostIO) -> Result<String, IOError>
    requires [HostIO] abi "dv.read_text.v1";
```

extern只允许签名绑定到已审计的ABI目录，禁止嵌入C#/Python文本、反射、动态eval或任意符号链接。语言内核不提供领域推理/决策/自然语言解析的隐藏宿主primitive。标准库、算法库、内容包、oracle包和宿主包使用同一module/import机制；构建manifest固定包权限和传递依赖。oracle可引用公开合同，主体不可引用oracle，规则检查不执行oracle纠正人物。

const只允许纯确定表达式，在显式编译预算内初始化；禁止读取世界、随机、I/O或提交。v1没有无检查宏、任意syntax引用、类继承、隐式异常、并发或自动微分。它们属于明确排除项，不是留下未定义却允许输入的语法。完整通用计算、词法闭包与有限领域模型足够表达现有编译器与推演算法。

## 15 规范文法

EBNF中 `{}` 为重复、`[]` 为可选，带引号的符号为源码。lexical Name/QualifiedName/Number/String/Char/AliasName遵守前述词法；Types、Constructors、Capabilities、字段及特殊操作按绑定合同核验。

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

PropPredicate为第8节固定列表，参数按各签名解释；tagged第三参为Compare+Expr，believes第二参为Proposition表达式；atom接受词法值、绑定?变量、slot及构造项。任意别的函数名在命题区域不能自动变成隐藏求解器。

所有Special的完整形式如下（p为Expr；为免多字关键字歧义，复杂实参须用括号，不将后续保留分隔词吞入Expr）：

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

Special解析为具名语义节点，绑定和检查后再降低到DV库；不得通过全局字符串替换实现。within语句与表达式中within分隔词按位置区分。record构造 `{}` 与控制块有歧义时，条件等分隔位置的构造实参必须加括号，例如`if (Flag { enabled: true }).enabled { ... }`；formatter保持必要括号。解析器按位置/文法决定，不运行世界规则猜意思。

## 16 展开诊断与兼容

链路：UTF-8原文→原token与define映射→CST→领域/算法AST→名称、类型、能力与控制流→明确DV IR→C#后端。内核和编译器均用DV实现；C0启动只负责能表达新前端的旧子集，之后迁移编译器并重建C1/C2/C3固定点。

诊断类别至少覆盖词法、缺符号、重复/缺字段、未知名称/类型/能力、模块环、包越界、非穷尽match、非法赋值/返回、Bool/Truth混用、Score/概率混用、主体/版本/来源不符、线性token复用、缺预算、未支持操作。每个诊断保留原文件/行列/span、原中文与规范名、展开节点及依据位置。阶段失败不产生可执行目标文件，运行错误完整保存栈与原始日志。

`--syntax sexpr-v0` 与 `--syntax surface-v1` 为待实现的显式选项；禁止失败后自动回退。旧Value兼容模式使用原ABI数值/容器/求值规则，与正式typed模式标明分开；无法静态保证的旧代码不伪装安全。所有生成物由源码生成，转换工具使用语法结构和位置映射，不能仅格式化世界record巨树冒充领域迁移。

formatter幂等、保留注释/中文别名，保持声明/实参/效果/候选顺序。不从优化删除来源或预算收据。所谓领域简写必须展开为同一可核验核心操作；数据构造器只构造数据，算法调用按其合同执行，声明不是未审计宿主solver入口。

## 17 第一章实际测试接线

内容样例单独置于 `verification/scenarios/surface-v1-proposal/`，不在本规范复制剧情实例。它展示现有第一章解释规则在新领域语法中的对应形式，仍是待实现前端输入，当前不可执行、不进入现有生产manifest。

迁移范围明确对应现有chapter1-laws、pragmatics、content、history、verification-config和独立oracle模块；人物/世界/话语/答案、公理实例和参数均从各原模块逐项迁移。冻结原源码和生成程序指纹，旧/新导出语义结构、作用域、来源、候选顺序、预算和实际行为分列核对；没有更换内容条件。

第一轮采用原有确定决策，观察第一章实际输入、本人切片、同一原句的逐人接收/解释/采纳、模型预测与真实理解差异、证据纠正、回答提交/接受和分享。原记录不足或窗口未发生分别注明，不强制错误、拒答或结局。主程序使用引擎主体自己的认识和决策，不注入状态、不预写操作/答案、不用oracle替主体选择。

这是隔离编译DV逻辑会话测试，不能宣称Godot输入/碰撞/画面或三级游戏验收。用户已授权随后用第一章实际测试，但前置方案本轮先交付，尚未运行。若后续另做正式游戏最终效果，使用规定的新会话、保护入口与实际画面证据，禁止flow/flow2及统一流程回归。

随机动作实验必须另列choice/参数/种子合同。第一章既有参数范围授权不自动扩大到温度/epsilon；确定机制迁移不需要这些新参数。空候选、未知、失败、预算耗尽、超时和未到达均如实保留；没有终局trace时不宣布章节完成。最多三个已有补充程序的分域与对照限制继续遵守。

## 18 实施顺序和完成判据

1. 交付本完整方案与第一章内容样例，固定核心合同；本轮到此，不执行尚无新前端的假测试。
2. 用DV实现扫描、define、算法层解析与lowering，补记录/分支类型/模块/闭包、类型/能力及来源检查；逐项实际输入输出与诊断取证。
3. 实现上述领域声明/操作，构建同一 typed AST/IR；未实现构造明确拒绝。迁移compiler.dv，封存新C1/C2/C3，核对固定点、独立程序和诊断，不用旧固定点替代。
4. 迁移第一章和会话/检查器，oracle独立包；用新编译程序导出并运行新正式逻辑会话，保留全部实际输入、本人信息与轨迹。失效或内容差异先排错，不绕过一致性检查。
5. 分别汇报新语法编译、自举、原机制语义保持、人物具体理解/行动和未验证的宿主范围；依据实际行为评价，不采用通过计数或全绿。

设计完成条件是本版允许/禁止形式、类型、展开和行为边界有明确合同；实现完成条件另需每项实际编译、执行、来源核验、自举与第一章会话证据。设计交付不能作为实现或游玩验收完成声明。
