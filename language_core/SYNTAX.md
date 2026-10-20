# DV 领域语法与数值类型方案

本方案名为 surface-v2。当前执行入口与七文件索引见 [README.md](README.md)，现行语法入口见 [docs/syntax.md](../docs/syntax.md)。本文件先列源码已接入的执行子集；第1–14节保留完整语言方案，超出本节边界的语法仍是目标，不能直接作为当前可编译代码或验收证据。

### 当前可执行边界

- 已实现：缩进块、fn/axiom、let/var/赋值、return/if/elif/else/while、中文标识符和 define、位置参数、记录/列表字面量、字段/下标、普通算术和短路逻辑；mapping 的 `×` 与 `*` 调用；acceptance 的 degree/levels；有限域 imprint 声明。
- 已实现的检查：函数名称/元数、局部变量、let 不可重新赋值、显式参数/返回值/局部名义类型、单位区间字面量、Nat 字面量整数性。Acceptance/Probability/Confidence/Reliability 构造使用独立范围构造器。配平使用精确有理数，最优性由 DV 证书核验器独立确认。
- 仍待实现：具名实参、通用 record/enum/scalar/generic 声明、单位与一般区间推导、by 证明、完整模块导入/能力系统、闭包、其余领域声明和目标无关 Typed IR。未注解代码采用 Value，既有通用计算仍用有限 double；不能将整个 Core 宣称为精确数值或完全静态证明。
- 编译器仍支持启动语法，自身尚未改写为新语法。实际写法以 [20-core.dv](20-core.dv)、[30-session.dv](30-session.dv) 和 [70-checks.dv](70-checks.dv) 的表层部分为例；10-compiler 和70中的部分报告仍使用启动语法，不能声称七文件全部转换。

当前 imprint 的执行形态把 achieve、vary、conserve using、limit using、minimize using 分别绑定目标记录、有限变量域、线性守恒式、当前资源/工作预算与具名资源价格。搜索结果经核验后由 dv_imprint_commit 提交。连续求解器和一般命题约束还未接入。

完整语言方案以“谁取得什么材料、如何理解、凭什么采纳、如何选择与行动”为书写单位，目标是让切片、解释、依据、评分和动作拥有专门语法，并由类型携带主体、来源与阶段。当前执行子集以普通函数和已接入的少量领域展开实现算法，其余能力按下面的设计范围阅读。

## 文件引用与调试入口

通过 `tools/build_session.py`、`tools/run_debug.py` 或编辑器检查入口组合源文件时，引用写为独立行：

```dv
#include 20-core.dv
#include 30-session.dv
```

路径相对当前文件，包含 `.dv` 后缀；每个文件加载一次，循环引用与缺失文件报错。依赖先编译，各文件的 define 保持文件作用域。当前功能由共用文件适配器解析，裸编译器接口接收组合后的源和文件范围。

`tools/dv_toolchain.py` 是共用文件适配器，生产入口为 [60-game.dv](60-game.dv)，独立检查入口为 [70-checks.dv](70-checks.dv)；[dv-check.json](dv-check.json) 为编辑器选择对应闭包。新轨迹使用二进制 DAG/DVLOG，`rt_json_write(path, value)` 执行时拒绝新 JSON 文件记录；进程内 JSON 交换及历史只读兼容仍保留。调试入口是 `60-game.dv` 的 `dvg_debug_run`，通过 `tools/run_debug.py` 按当前任务授权运行。

## 1 参照与取舍

| 参照 | 借鉴到 DV | 具体选择 |
| --- | --- | --- |
| MATLAB | 领域对象直接参与运算，声明参数形状与约束 | 切片、证据和动作分布可以运算；单位、形状和数值语义参与推导 |
| Lean | 带性质的类型、展开层与小型可信检查层 | 范围、量纲、归一化与预算关系自动检查；常用性质不要求作者写证明 |
| Rust | 名义类型、枚举匹配、只读借用与资源消费 | 区分同底层数值的含义，共享预测快照，一次性使用执行票据 |

MATLAB 的 arguments 表达输入约束，不能据此声称它会在编译期证明所有数值关系。[官方说明](https://www.mathworks.com/help/matlab/matlab_prog/function-argument-validation-1.html)

Lean 的 Subtype 将值与性质证明组合，编译后可具有与基础值相同的表示。DV 借鉴这一分层，将数学性质证明与人物信念分开。[官方说明](https://lean-lang.org/doc/reference/latest/Basic-Types/Subtypes/)

Rust 的 newtype 可以区分数值的语义，普通类型别名不能做到这一点；所有权约束值的使用。[名义类型](https://doc.rust-lang.org/book/ch20-03-advanced-types.html)、[所有权](https://doc.rust-lang.org/book/ch04-01-what-is-ownership.html)

## 2 基本外观

UTF-8、中文标识符、中缀表达式、具名参数、冒号和缩进块。换行结束语句，括号内可换行；空格缩进，不使用 Tab。标识符采用 Unicode 标识符类别与下划线，按 NFC 归一化，区分大小写。

```dv
module example.numeric
import dv.numeric
export 剩余时间

fn 剩余时间(总量: Duration, 已用: Duration) -> Duration
    where 已用 <= 总量:
    return 总量 - 已用

fn 平方(x: Rat) -> Rat = x * x

let 剩余 = 剩余时间(总量: 60min, 已用: 24min)
```

函数参数类型显式，局部类型推导。单表达式函数用 `=`，多语句函数用 return。`let` 不可重新绑定，`var` 为局部可变绑定；赋值 `=`，比较 `==`。默认参数写 `=`，调用支持位置参数与具名参数。同一参数不能重复提供。

```dv
record Point:
    x: Length
    y: Length

enum Outcome<T>:
    Done(value: T)
    Waiting
    Interrupted(reason: Text)

fn describe<T>(outcome: Outcome<T>) -> Text:
    match outcome:
        case Done(value): return "完成"
        case Waiting: return "等待"
        case Interrupted(reason): return reason
```

`type Name = Type` 仅创建别名；record、enum 与 scalar 创建新类型。记录值写 `Point(x: 1m, y: 2m)`，更新写 `point with { x = 2m }` 并返回新值。泛型形式为 `<T, const N: Nat>`。集合包括 List、Map、Set、Vector<T,N>、Matrix<T,R,C>。向量为 `[a,b]`，矩阵为 `[[a,b],[c,d]]`，Map 为 `map { key: value }`。

数值矩阵的 `*` 是矩阵乘法，`.*` 是逐元素乘法；不做隐式广播。形状进入类型。认知映射的 `*` / `×` 是第7节定义的专用映射运算，只借用乘法的书写形式，不要求矩阵乘法、外积或叉积算法。List 动态索引返回 Option，Vector 的 Index<N> 索引直接返回元素。[MATLAB 的运算区分](https://www.mathworks.com/help/matlab/matlab_prog/array-vs-matrix-operations.html)

if/else、for/in、while、match/case 提供一般控制流；and/or 短路，实参从左到右求值。Option/Result 用 match 处理，`?` 在返回相容 Result 的函数中传播错误。闭包写 `fn(x: T) => expression`。注释为 `//` 与 `/* ... */`，文本用双引号，字符用单引号。

模块用 import 引入，export 显式公开。模块声明依赖为有向无环图，模块内部函数可相互递归。主程序用 `fn main() -> Unit:` 入口，不另造一套执行语法。

## 3 中文与 define

```dv
define 函数 fn
define 返回 return
define 解释 reading
define 决策 decision

module example.localized
函数 平方(x: Rat) -> Rat = x * x
```

保留 `define x x` 格式：一行一条，放在 module 前，左右各一个 token；恒等映射本身合法。标识符原生支持中文，不需要逐个翻译变量。

别名一次映射，不递归替换，不影响字符串与注释，不允许同一 token 映射为不同内容。别名不改变运算优先级。规范名和原文名都保留在诊断中；NFC 后同名的标识符视为同名。中英文书写生成相同节点。

## 4 固定运算符

运算符对应固定操作，用户不能随意重载领域含义。Unicode 形式只是 ASCII 的等价拼写。

| 写法 | 操作 | 结果 |
| --- | --- | --- |
| `p @ m` | 在主体视界 m 中评价命题 p | Judgement，含真值、依据和冲突 |
| `mapping * input`，或 `mapping × input` | 用认知映射处理实际可得切片 | InterpretationSet，含主体、来源与映射过程 |
| `input ~> Reading(memory: prior)`，或 `⇝` | 用指定解释方式与本人材料理解输入 | InterpretationSet，尚未采纳 |
| `evidence \|- p`，或 `⊢` | 按现用规则检查材料是否支持 p | SupportResult，成功项含论证与来源 |
| `offered >> channel` | 在本人获准通道上实际发送提供材料 | Result<Delivery, TransmissionError> |
| `value \|> f(args)` | 将左值作为 f 的第一个实参 | f 的返回类型 |
| `premise => conclusion` | rule 中的前提到结论 | 规则声明，不执行世界变化 |
| `x ~ distribution` | model 中的随机变量声明 | 模型变量，不立即抽样 |

`evidence |- claim` 检查领域论证的支持关系，不宣称证据客观为真。数学性质证明用 by，不能用人物采纳的领域规则证明数值类型正确。

`offer >> channel` 是有效果的传递运算，左项必须是 OfferedSlice，不能是任意记忆或字符串。通道传递后，各接收者分别进行 perceive。一般理解沿采纳规则改变本人认知；外力刻画使用第9节的 imprint 约束求解操作。

`score_table |> softmax(temperature: t)` 等价于 `softmax(score_table, temperature: t)`。`sample distribution using rng` 才产生选择意图，抽样不隐式执行动作。

优先级从高到低：调用/字段/索引/`?`，前缀 `- not`，`* × / .* %`，`+ -`，比较，and，or，`@`，`~>`，`|-`，`>>`，`|>`。`×` 与 `*` 等价。`@`、`~>`、`|-` 不允许无括号连续结合；管道左结合。映射乘法不具有默认交换律或结合律，连续认知映射须括号或具名组合。`=>` 和 `~` 只在其声明位置解析。普通程序分支要 Bool，Judgement 不隐式转换为 Bool。

```dv
let 判断 = 命题 @ 本人视界
match 判断.truth:
    case True: return 已支持
    case False: return 已否定
    case Unknown: return 尚不清楚
```

以上返回值由调用者定义。冲突保留在 Judgement 的独立字段中；未知不以概率 0 或 0.5 代替。

## 5 核心数值与单位

核心采用精确 Int/Nat/Rat，十进制 0.1 是精确 1/10。Float64 是显式近似类型，写作 `0.1f64`，不能隐式流入核心领域量。后端证明范围可容纳时使用机器整数，否则使用大整数/有理数。

```dv
scalar Probability: Rat where 0 <= value and value <= 1
scalar Confidence: Rat where 0 <= value and value <= 1
scalar Reliability: Rat where 0 <= value and value <= 1
scalar Acceptance: Rat where 0 <= value and value <= 1
scalar Weight: Rat where value >= 0
scalar Blend: Rat where 0 <= value and value <= 1
scalar Score<Scale>: Rat
scalar Temperature<Scale>: Score<Scale> where value > 0

unit s
unit min = 60s
unit h = 60min

let 信度: Reliability = 0.8
let 停留: Duration = 2min + 30s
let 截止: TimePoint<GameClock> = 15:00@GameClock
```

scalar 创建名义类型。字面量 0.8 可按所需类型构造；已有 Confidence 变量不能赋给 Probability。Score 带评分尺度，不同尺度须通过具名映射转换。Temperature 与 Score 同尺度，其商无量纲。

| 数值 | 性质 |
| --- | --- |
| Probability / Confidence / Reliability / Acceptance / Blend | 各在 [0,1]，含义不互换；Acceptance 专指主体对切片的接纳程度 |
| EvidenceWeight / Weight | 非负，不要求总和为 1，二者语义类型独立 |
| Score<S> | 可为负，只与同尺度相加和比较 |
| Duration / TimeDelta | 前者非负，后者可为负 |
| TimePoint<C> | 同时钟时间点相减得到 TimeDelta；加 Duration 得时间点 |
| Budget<R> / Cost<R> | 指定资源的非负量，时间、认知步数、体力不混用 |
| Dist<A> | 非空支持集、非负质量、质量总和为 1 |
| Vector / Matrix | 元素类型与形状满足运算签名 |

Duration 相减的一般结果是 TimeDelta，第2节 where 关系可使其结果精化为 Duration。Rat 除法要求分母非零的已知性质；无法推出时使用返回 Result 的 divide。整数截断除法写 div，`%` 为相应余数，`/` 为有理数除法。TimePoint 的时钟域与历元由类型声明；例中的 HH:MM 是 GameClock 的显示格式。

加减保留数值语义尺度并推导区间：两 Probability 相加的一般结果为未归一化概率质量，不能自动回填 Probability；`1-p` 可证明仍在 [0,1]。Confidence 没有默认的相乘合证含义，凸组合使用 blend。Probability 的数值乘积虽仍在 [0,1]，也不证明事件独立；事件联合概率由模型关系计算。

```dv
fn complement(p: Probability) -> Probability = 1 - p

fn average_confidence(a: Confidence, b: Confidence, w: Blend) -> Confidence:
    return blend(a, b, w)

let 错误概率: Probability = 1.2  // 编译错误：超出范围
let 错误赋值: Probability = 信度  // 编译错误：数值含义不同
let 错误时间 = 3min + 0.8         // 编译错误：量纲不同
```

自动保证落在三个位置：

1. 常量、名义类型、单位、形状和可推导范围在编译时处理；where 前提参与调用和分支分析。
2. 自定义函数可用 ensures 约束结果。常量求值、区间、线性关系和已登记引理自动消解。未能消解的关系提供 by 证明，或使用返回 Result 的构造操作，不把“未证明”当作“已成立”。
3. 外部输入通过 Probability.parse 等构造一次取得有效类型，之后使用该性质，不在每次读写时重复检测。

函数契约写在返回类型之后：`where 参数关系 ensures 结果关系`，ensures 中 result 表示返回值。首版 by 只支持具名引理应用、代入、反身性与算术证书；不承诺自动证明任意定理。核心类型性质不能由剧情 axiom 或任意 cast 绕过。证明在代码生成后可擦除，运行时数据关系在实际构造处处理。

这里保证范围、量纲、形状、归一化、算术与已声明的数学关系。人物权重是否合理、模型是否符合现实，是内容与模型的问题。

## 6 分数到概率

```dv
let weights = weights { 调查: 3, 询问: 1 }
let distribution = normalize(weights)
// 静态非空且总量为正，直接获得 Dist，概率精确为 3/4 与 1/4。

let exact = distribution { 调查: 0.75, 询问: 0.25 }
// 显式概率质量已合计为 1，不偷偷归一化错误的概率声明。
```

动作值由调用者定义。按动作实例身份形成支持集，重复身份报错。多条预测路径若共享一个首步，须先聚合首步价值，不能变成多张抽签票。

| 策略 | 合同 |
| --- | --- |
| argmax | Score 到最高分点质量，平分规则显式指定 declaration_order 或 uniform |
| softmax | 同尺度 Score 与正 Temperature；稳定计算 exp((s-max(s))/T) 后归一化 |
| weighted | 具名 Score→Weight 映射，再 normalize |
| epsilon_greedy | Probability 类型 epsilon，混合最高分分布与均匀探索 |

全语言不固定默认公式，每个 decision 自己选择策略。保留“先研究可选规则、不预定公式”的约定。

Dist 以非负整数质量和正总量为逻辑表示，概率为精确比值；有理权重可通分，不必为每个候选反复做浮点除法。动态非空与正总量已被类型证明时 normalize 直接返回 Dist，否则返回 Result<Dist, EmptyOrZeroMass>；全零权重没有隐式均匀回退。

softmax 的指数计算是近似的，标准库记录实现版本和量化方式，再构造合法质量。精确归一化不意味着 softmax 映射无近似误差。采样使用质量区间与指定随机源，不使用四舍五入后的展示百分数。

## 7 视界、存储切片和提供切片

```dv
slice 表达(u: Utterance) = u.{literal, referents, scope, cues}
slice 背景(m: MindView) = m.memory.{episodes, adopted_rules}

fn 接收(delivery: Delivery, view: MindView) -> Result<Revision, ReceiveError>:
    mind view:
        let input = perceive(delivery)?
        let prior = recall(背景(view), using: relevant(input))
        let meanings = 本人映射(memory: prior) × input
        return adopt(meanings, using: 本人采纳策略)
```

本人映射和本人采纳策略为模块中的具名声明。MindView 实际带主体与版本参数，省略参数时从调用处推导。mind 块建立 self、memory、now 与现用规则的词法上下文；获得 Agent 标识不自动取得其私有视界。

`value.{fields}` 是投影，不复制整棵状态树。slice 为投影命名。类型自动携带主体、本地引用、源版本及来源：

```text
StoredSlice<S,V> → recall → WorkingSlice<S,V>
WorkingSlice<S,V> → offer → OfferedSlice<S>
OfferedSlice<S> >> Channel → Delivery
Delivery → perceive → Percept<Receiver,V>
Percept<S,V> ~> Reading(WorkingSlice<S,V>) → InterpretationSet<S,V>
```

offer 的 to 指定接收者；真实传递后才有 Delivery。perceive 需要属于当前接收者的传递项，没有听见或看到时返回对应结果。提供一个切片不会带走提供者的全部记忆。

同一输入与不同记忆可以产生不同解释。ModeledMind<Owner,Target> 是 Owner 对 Target 的模型，其理解预测带 Forecast 类型，不能作为 Target 已实际理解的回执。

### 7.1 乘法外观的认知映射

```dv
mapping 本人映射(memory: WorkingSlice) in mind:
    input Percept
    output InterpretationSet
    using 常用理解(memory: memory)

let 主观解释 = 本人映射(memory: 本人记忆) × 感知切片
```

`mapping` 是具名领域运算声明。input、output 为输入输出类型，using 绑定具备该签名的 reading 或 DV 函数；主体、源版本与实际来源由输入和上下文推导。底层可以做线索匹配、回忆检索、范围转换、规则推知或数值计算，不要求把命题、回忆、人物关系压成数值矩阵。`Reading × input` 的操作方向统一为“映射在左，材料在右”；`input ~> Reading(...)` 是直接调用理解策略的写法，两者在绑定同一策略时展开到相同 Interpret 节点。

完整语义是“客观来源切片 → 实际暴露与感知 → 本人认知映射 × 感知切片 → 主观解释切片”。客观来源不等于其中所有证词客观为真，也不等于主体能读取权威世界的全部内容。ObjectiveSlice 在权威域，主体运算接收实际 Delivery 形成的 Percept。投影与遮挡由真实通道决定。

同一份传递给多人时，每个接收者各自绑定自己的记忆、规则与偏好，分别运算；没有默认共享大脑。结果保留字面内容、解释作用域、桥接依据及来源，因而能表达字面相同但理解相反。映射求值本身不写入接纳状态；正式接收将映射结果交给下面的数值接纳机制。

## 8 命题、推知、理解、采纳

```dv
prop 已关闭(d: Door) = d.closed == true

rule 关闭推知(d: Door) in mind:
    已看见关闭(d) => 已关闭(d)

reading 常用理解(memory: WorkingSlice) in mind:
    on input: Percept
    when 匹配线索(input)
    interpret 候选解释(input, memory)
    scope input.scope -> 解释范围(memory)
    bridge 范围依据(memory)
    support 解释支持(input, memory)
    revise using 本人修订策略
```

例中的谓词与策略由模块提供。prop 构造 Proposition，读取领域引用不等于读取其权威值；在 prop、rule、known 等命题位置，字段比较构造命题。普通函数中只有拿到实际值才能普通比较。

rule 在本人有据采用后参与推知；声明不等于所有主体都知道。应用自动记录匹配前提、规则身份/版本与采用来源。`evidence |- claim` 返回 Supported(argument)、Undetermined(missing) 或 Conflicted(details)。

reading 的 support 为 EvidenceWeight，输出解释候选。adopt 调用本人接纳机制，按数值与门槛形成修订；`apply revision` 提交本人修订并产生新版本，不能提交到另一主体。正式 receive 在同一接收操作内完成 perceive、映射、评分与修订，不要求内容作者逐条写状态转换。

retain、revise、defend、withdraw 是不同策略操作；收到反证后是否维护旧理解，由本人的目标、依据与策略决定。语言不固定为所有人自动撤回或自动维护。

证据类型区分 Observation、Testimony、Recall、Derivation、Hypothesis、Estimate。组合保留同源依赖组，重复拷贝不产生独立证据；假设不能通过数值转换变成观察。

### 8.1 达到门槛自动转换接纳程度

```dv
acceptance 本人采纳策略 in mind:
    degree item: Acceptance = 接纳函数(item, memory, goals)
    levels:
        Held from 0
        Tentative from 暂纳门槛
        Adopted from 接纳门槛
```

degree 的具名函数将证据、来源依赖、冲突和主体已有认识转换为 Acceptance；不把动作 Score 或命题 Confidence 自动当作接纳程度。两个门槛为内容声明的 Acceptance，满足 `0 < 暂纳门槛 < 接纳门槛 <= 1`，不在语言里预定心理参数或评分公式。

levels 声明有序区间：取 degree 大于等于其下界的最高一级。等于门槛即进入该级；低于门槛按相同区间自动降级。连续 degree 和离散 level 同时保留。默认更新不是每帧扫描全脑，而在正式取得材料、依赖或相关主体参数改变时，重新计算受影响切片。预测上下文只产生预测修订；实际上下文自动提交真实修订与来源。

转移保留 before、after、触发依据和版本。曾 Adopted 后降级，生成 Withdrawn 转移回执；新的 level 仍由区间决定。Rejected 需要明确的拒斥依据或策略，不从低接纳程度自动推出反命题。接纳程度也不改变世界真值、证据来源与客观正确性。

门槛分类是纯函数；接纳数值的关联更新以一次新输入事件和旧快照为基础。若内容声明切片之间同步反馈，使用具名 update 求解函数声明稳定条件与计算预算，统一产生一个修订批次；预算内未求得结果则保留 Pending，不把任意迭代中间值当作最终接纳。

新 level 可成为共同动作决策的前提，例如 Adopted 切片受到质疑时出现 defend 候选。候选是否执行仍取决于本人目的、成本和动作选择，门槛不会直接强制人物说出某一句话。

## 9 动作与执行

```dv
action 转述材料(item: WorkingSlice, receiver: LocalAgent) by self:
    known 同处(self, receiver)
    cost time: 0min, cognition: 1step
    do:
        let offered = offer(item, to: receiver)
        let delivery = (offered >> hearing_channel)?
        return delivery
```

action 是模板，调用产生候选，不立即执行。known 在本人视界判断；若要尝试未知条件，声明独立 attempt 动作。cost 有资源类型，可提供上下界与具名结算函数。

do 只在正式执行阶段运行，错误形成 Failed/Interrupted 回执并结算已发生消耗。这里的 `?` 传播为失败回执，不撤销已经发生的言说和身体动作。单个世界修改可以声明 atomic；整段会话没有隐式回滚。

worldlaw 以 when 声明权威前提，以 effect 包含 `set ref = value`、`change ref by delta`、`spend resource = amount`、relate 等效果。axiom 为具名领域法则实例，norm 声明 permit/forbid/prefer；均不能代替数值性质证明。

```text
Action → decision → Intention → prepare → Prepared → commit → Receipt
                                     ↘ Blocked / Waiting
```

Prepared/Reservation 为仿射类型：消费一次或 cancel 释放。MindView、Evidence、预测快照可只读共享，写入路径产生新版本，作者不写深拷贝。静态类型约束局部消费，跨进程重放由宿主事务身份控制。

### 9.1 强制刻画与最小代价配平

```dv
imprint 刻画认知(target: MindTarget, idea: Interpretation, floor: Acceptance):
    require capability 刻画能力(target)
    achieve acceptance(target, idea) >= floor
    vary target.cognition via 允许的认知变换
    conserve using 代价守恒律
    limit using 可用资源
    minimize using 刻画代价
```

这些参数与具名法则由内容包提供；本例不规定谁拥有刻画能力或具体代价。imprint 声明产生一种行动模板，与 action 共用规划、准备、实际执行和回执。achieve 是强制达成的目标约束；vary 限定可调整的认知量与变换；conserve、limit、minimize 分别指定守恒关系、资源界限与目标函数。所有分句为必需，不存在退化为 `set mind = ...` 的执行分支。

系统对“目标脑状态、实际变换和支付来源”联合求解：在满足目标、现有世界法则及资源约束的可行方案中，寻找最小代价方案。允许调整哪些旧认知、依赖、强度、资源和外部作用由 vary 与已声明法则确定。不得把范围外的事实或历史改掉以获得便宜解。守恒法则是已有世界法则的声明引用；缺少具体公式时仍是未完成的内容参数，不能自行捏造物理能量与认知成本的兑换率。

achieve 在候选状态经过正常接纳函数重算后判定；求解器须改变法则允许的认知结构或施加作用，使重算结果达到目标。Acceptance 的派生字段不作为可任意覆盖的自由变量，因此不会出现刚写成已接纳、下一次正常重算又因未改变依据而立即消失的假刻画。是否允许持续施加作用由同一世界法则决定。

每种守恒资源分别配平，例如 `before + supplied = after + spent + exported`；账目具有资源单位，去向互斥、不重复记账，具体项及哪些是耗散由对应法则定义。优化目标是具名 Cost<Scale>，可以采用显式换算系数形成同尺度总成本，或声明 lexicographic 优先级；不把时间、体力与认知步数直接相加，也不通过改变单位数值制造最优解。计算本身的费用与目标脑的改动费用分别入账。

求解结果区分 Optimal（附最优依据）、Feasible（当前最优可行候选及搜索终止原因）、Infeasible（约束不相容依据）与 Unresolved（预算内尚无可行方案或结论）。有限候选可精确比较；连续问题由匹配其数学结构的具名求解器处理。通用非线性问题不宣称必然得到全局最优。可执行的 Feasible 方案必须符合该刻画声明绑定的执行政策，报告不称它为已证明最小代价。

执行阶段核对实际源版本与可支付资源，按求得的同一方案结算资源、更新认知、触发接纳门槛转移。认知刻画和相应支付是一次状态提交；无可行方案不写入目标认知，已实际用于求解的认知费用照常发生。预测配平结果不产生真实支付或真实脑状态变化。

目标脑中的新来源记录为 Imprint，并保留施加者、作用与支付事件；它不会自动成为 Observation 或证明。后续材料仍通过普通接纳机制更新它；要持续维持刻画，须在世界法则中声明持续作用及费用。

## 10 决策与规划

```dv
decision 调查选择(options: ActionSet, chooser: ChoicePolicy<Investigation>) in mind:
    candidates options
    eligible known
    score a: Score<Investigation> =
        information(a) * (12score<Investigation> / 1bit)
        - duration(a) * (0.1score<Investigation> / 1min)
    plan depth: 6, budget: 320step
    first_action max_return
    admit score > 0score<Investigation>
    choose chooser
```

系数仅说明单位语法，非第一章新增参数。information 返回信息量，duration 返回时长，乘上系数后统一为 Score<Investigation>。`12score<Investigation> / 1bit` 是每单位信息量的分值。

结果是 Ready(distribution, plan)、NoCandidate、NoSelection、BudgetStopped 或 Waiting。Ready 才可抽样；未达门槛不会强制行动。无规划时省略 plan/first_action，使用即期分；有规划时两项都明确。

plan 给出本次搜索深度与展开额度，累计认知资源放在主体资源账户中，不能混为一个预算。first_action 先按可执行首步分组，再用 max_return、具名期望策略等聚合。期望回传需要结果分布，不能预知未知检视内容。

PlanResult 自动记录即期分、回传分、预测后继、本人来源与停止原因。不变快照共享，依赖改变后重新计算受影响部分，作者不逐处填写 revision、event_id 和复制操作。

## 11 历史模型与论证

```dv
model 解释空间(options: NonEmptySet<Hypothesis>) in mind:
    hypothesis ~ uniform(options)
    given evidence = working_evidence
    predict using adopted_dynamics
    compare predicted_features with evidence
    conclude using explanatory_policy
```

这些输入由模型上下文绑定。model 声明有限变量域和分布；概率 prior/likelihood 与启发式 Weight 分开。given 限定本人材料，预测输出独立形成，不先写入被预测的观察结论。

condition 更新分布，simulate 消耗实际工作资源并返回 SimulationTrace，explain 根据比较、竞争覆盖和本人策略形成 EstimatedArgument。假设、预测和实际观察类型不同。

Argument 自动追踪依赖，材料变化后为 StaleArgument，重新评价后再提交。各 ClaimSlot 分别声明命题与证据种类，不以布尔标记代替论证。置信数值合法不等于历史判断被客观证明。

## 12 问题、协议与宿主

```dv
protocol 有据回答(question: Question, required: ClaimSchema):
    submit answer: Argument by speaker
    require answers(answer, question)
    require supported(answer)
    require complete(answer, required)
    accept record personal(speaker, question)
```

协议在权威端处理问题、支持和完整性，主体只取得实际公开的问题和方法。personal 回执与 public 最高结论独立。提交、结论正确、正式接受和章节交接各有回执。

QuestionRef 携带实际收闻或本人形成的来源。TimeAnchor 区分事件时刻、观察时刻与命题适用范围。PresentationReceipt 表示实际呈现；ExecutionReceipt 表示正式输入与执行，逻辑输出不能转换成显示回执。

标准库承接调度、等待、取消、租约、保存恢复、词项消歧和学习。宿主负责平台I/O、空间、碰撞、输入和显示。函数效果由实现推导，公开接口列明所需效果，不反复书写空能力列表。

```dv
extern fn read_text(path: Text) -> Result<Text, IOError>
    effects IO
    abi "dv.read_text.v1"
```

包分 library、content、oracle、host，角色与依赖在包清单声明。内容可初始化世界与人物，主体运行函数只取得入口提供的能力；oracle 不可由主体模块导入。静态主体类型和运行时的动态主体标识共同约束访问。

## 13 声明合同与解析骨架

| 声明 | 核心内容 |
| --- | --- |
| slice | 参数化字段投影 |
| prop / rule | 命题、可采用的前提到结论 |
| reading | on、when、interpret、scope、bridge、support、revise |
| mapping | input、output、using；具名认知映射及乘法调用 |
| acceptance | degree、levels；门槛自动分类与依赖更新 |
| imprint | require capability、achieve、vary、conserve、limit、minimize |
| action | by、known、cost、do；attempt 变体 |
| decision | candidates、eligible、score、admit、choose；成对可选 plan/first_action |
| model | 随机变量、given、predict、compare、conclude |
| protocol | submit、require、accept |
| worldlaw / axiom / norm | 权威转移、具名实例、规范的固定字段合同 |

```text
File       = Define* Module Import* Export* Declaration*
Block      = ':' NEWLINE INDENT Statement+ DEDENT
Function   = 'fn' Name Generic? Parameters ReturnType? Contract? (Block | '=' Expr NEWLINE)
Scalar     = 'scalar' Name Generic? ':' Type ('where' BoolExpr)? NEWLINE
Slice      = 'slice' Name Parameters '=' Projection NEWLINE
Prop       = 'prop' Name Parameters '=' PropExpr NEWLINE
Context    = 'mind' Expr Block
Domain     = DomainKind Name Parameters? ContextHeader? Version? DomainBlock
ContextHeader = 'in' 'mind' | 'by' Name
DomainBlock= ':' NEWLINE INDENT Clause+ DEDENT
Clause     = a clause from the fixed contract of its declaration kind
Projection = Expr '.' '{' Name (',' Name)* '}'
UnitValue  = NumericLiteral UnitName Generic? | ClockLiteral '@' ClockName
Statement  = Binding | Assignment | Return | If | For | While | Match | Context | Expr NEWLINE
```

这是解析骨架，表达式遵循第4节优先级，领域分句按固定合同解析，不再用任意 key:value 记录冒充领域语义。prop/rule/known/literal 位置使用 PropExpr，where/if 使用 BoolExpr；两者分别是三值命题与二值计算。

声明以模块限定名称作为稳定身份，版本默认取包版本，也可显式写 version 2。源位置由展开层生成；声明文件出处与主体实际得知的事件来源分别保存。

## 14 编译与落地

```text
原文 / define → 缩进 token / CST → 固定操作与领域声明展开
→ 名称、名义类型、主体、单位、形状和效果推导
→ 数值约束证书与资源消费检查
→ Typed IR: Project / Interpret / Support / Transmit / Score / Distribute
→ AcceptanceUpdate / SolveBalance / Commit
→ 共享快照、增量依赖和数值表示专门化 → C# 等目标后端
```

后端接收已分型节点，格式转换器不猜测领域语义。常规代码不暴露键值对AST树，兼容层连接现有双精度与列表ABI。

实施顺序：普通语法与名义数值类型；视界/切片/认知映射运算；门槛接纳/规则/动作；守恒求解与刻画结算；分布/规划/模型/协议；编译器自身迁移。同一前端与IR逐步扩展，不新增独立剧情解析器。旧 double 权重改为精确量时明确转换方式，不把数值变化当作纯语法替换。求解、接纳与映射算法继续由 DV 库实现，平台 ABI 提供基础数值与容器操作。

现行20–60的专业观察、询问回复、主动说明和个人答辩回执已使用表层源码。是否在当前环境完成编译及正式会话验证，以本轮实际记录为准；旧会话与源码迁移不证明完整语言方案已经实现。剩余范围以文首边界为准。


## 当前执行子集：集合与分支简写

Surface 的同级 `elif condition:` 按源码顺序降为已有嵌套 if/else，首个真分支执行；else 可省略。条件仍使用既有二值 Bool 运行合同。

```dv
fn positives(xs) = [for x in xs if x > 0: x * 2]
fn projected(xs) = [for x in xs: x]
fn revised(record) = dv_with_many(record, {"enabled": true, "weight": 2})
```

列表推导式语法为 `[for name in source [if guard]: projection]`。单绑定，允许嵌套；source 在外部作用域只求值一次，元素按原顺序经 guard 筛选后求 projection。绑定仅在 guard 与 projection 有效，可遮蔽外部同名绑定；内层 source 仍读取外层。结果是新列表，元素沿既有 rt_at 的容器读取合同，不额外承诺深拷贝。迭代期间不应修改 source；与现有索引 while 一样每轮读取当前长度。

source 运行时须为 List，guard 须为 Bool。元素静态类型保留 Value；具名函数调用、名义范围、不可变 let 与未绑定名称的检查继续执行。此实现不提供闭包、惰性迭代、多个并列 for、通用泛型或目标无关 IR。

映射与过滤用推导式组合。当前没有 map/filter/any/all 高阶函数；`rt_count([for x in xs if predicate(x): x]) > 0` 与 `rt_count([for x in xs if not predicate(x): x]) == 0` 可表达有限集合的存在/全称判断，但会完整遍历并分配结果；需要短路时仍使用 while。记录更新继续调用 dv_with_many；未增加 `{...record}` 语法。新简写不自动改写业务源码，也不构成代码体积或运行速度结论。
