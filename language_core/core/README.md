# DV Core：现行语法、机制与接口参考

宿主入口（2026-10-19）：`game/entry.dv` 的 `dvg_host_open(options)` 接收 controller、viewer 与 campaign；`dvg_host_viewer(state)` 返回持久化的观察者。controller 仅决定 `dvs_set_player` 是否设置人工输入主体。`dvs_control_state` 使用顶层所有权更新保留未知会话字段，不改 world/mind 复制合同。`dvg_snapshot` 输出 logical_location、execution_room 与原生可视绑定；`people` 逐人保留同类地点字段。`access.maps`按当前所在地图和权威门态供地图出口碰撞使用，人物本人出行资格仍由 DV law 判断。C# 导出观察者快照供显示，执行主体快照独立随 dv_action 供几何调度。现场身份和到达结果经同一 Complete 结算，未成功的移动不授予接触观察。


本轮接口补充（2026-10-18）：`compiler/compiler.dv` 的 `sf_comprehension`、`sf_rename_comprehension`、`ck_comprehension`、`sf_emit_comprehension` 负责列表推导式全链，`sf_stmt`处理elif。`dvp_rejected_condition_signature`、`dvp_rejected_signature`与`dvp_eligible_base`（session/15-action-planning.dv）在可重复资格之前核对失败动作的本人相关输入；`actor.rejected_attempt_signatures`由真实Complete失败后写入，成功后清除。等值单元保留known/value/conflicted；数值阈值保留真值，时钟或无关认识修订不解除屏蔽。对应predict采纳、前提与本人位置变化可重新比较。

`action.failure_grants`和`failure_observe_when`仅在formal-model回执applied而DV权威失败、现场条件成立时，沿原绑定投影产生观察。Godot失败回执不能证明逻辑拒绝后保留的起点现场，本轮不据此授予失败观察；正常检视和共同失败屏蔽仍有效。成功grants维持成功Complete后的原合同。

`c1n_clock_boundaries`与已声明目标的窗口元数据限制本人等待片段，不读取其他主体目标；既有1/5分钟等待费用承接不足15分钟的片段，取消越过机会14分钟的容忍。第二章声明daytime_wait_chunk_limit=120，仅日内480–1260分钟将片段限制为原上限与120的较小值；夜间沿用原上限，窗口边界仍可截短。导航只把本人已知可达的异地ready任务作为优先焦点。配额、判答与行动费用不增加。

`chapter2/daily.dv`分开`council_closed`（会议收束）与`council_ended`（全员实际签署齐备）：6330截止只收束，完整签署也可提前收束。环境独立产生持续低鸣/余震，实际会场检视后才形成本人观察；内容row.observation_grants只追加本人的成功后观察，不追加public_grants。`c2daily_chapter_summary`、`dvg_chapter_summary`与宿主snapshot.chapter_summary提供闭会后系统结算身份的真实状态投影，不写心智。未渲染的摘要字段不等于Godot章末卡已验收。

只读checks/08-formal.dv保留独立重演，补齐dispatch、mask_plan、Replay_Depth和切片冻结输入核验；05-cost.dv同步本人接纳、Resistance与信任来源。03-evidence.dv独立复算撤回，25-simulation.dv发布完整actor/mind前后证据。独立算法不直接调用待审会话算法。列表构造器c1_list_4..44已移除，现役内容使用保序列表字面量。

室内防护的未接通声明、空动作构造器与unknown房间输入隔离到checks/fixtures/room-safety-draft.dv，生产不包含该文件、不再创建无消费者房间状态或采纳无调度者法则。正式地点目录c2_rooms、实际气闸与户外暴露保持；runtime_contracts.unimplemented_room_safety_draft明确设计要求尚未实施，不能把隔离草案当作生产安全保障。

`Runtime.json_write`独占创建partial及正式文件，拒绝覆盖；`Session.Save/WriteSave/Restore`共用精确数标签与512 JSON深度，Trace精确数继续输出字符串。`run_debug.py`的evidence_complete要求正常退出、debug_end、正式trace、无partial、正文可解析及源码未改；logical_loop_finished不表示剧情完成，timeout_stage明确会话运行或封存阶段。相关工程与正式会话证据见artifacts/partial-bootstrap-core/parallel-repairs-20261018/，未覆盖分支保留未知。

同一真实执行的本人、现场与公共授权合并为每主体/引用的一份感知。`dvs_public_projection_inputs`读取本次执行事件尾段中已实际发布的观察，`dvs_public_filter_projected`只补尚未投影的观察，保留原ID、绑定与Core重复拒绝。`public_grants_projection`记录本次公共入口及已有收据来源，再次调用仍拒绝；正式接受回执的`-accepted`执行身份独立。公共observation事件的grants仅含实际新投影的physical引用，declared_grants保留原声明，聆听及listener_parse保持独立执行，不把同一执行伪装成第二份来源。

诊断后的纯值优化：`dv_read`在原subject→world.objects→ref.object检查后，以完整world和ref缓存完整cell，`dv_read_fields`只处理已验证前缀后的查找，缺对象仍不读facet/field。每次命中返回独立可变副本，嵌套world/ref变化使旧底本失效。`dv_truth`仅对List world中的非List常量值跳过通用memo；List常量保留原命中副本合同，其他路径及合取求值顺序保持。`dvp_topic_scan`保留原主题算法，入口的纯Bool缓存包含完整mind、item、question_ref存在性及值、topic_bindings、heard_topics；无合格mind时沿原扫描，有来源的personal早退仍优先。

`rt_has_value`对冻结全字符串列表或有效底本使用ordinal字符串集合；符合条件的结构值按保守指纹分桶后仍用原相等比较，新建查询可只读计算至多八层新建列表的临时指纹，不缓存查询；更深/循环及不支持值沿原扫描。`rt_record_with_many`在较大正规字符串记录上索引更新，小表与非正规表沿原路径；输出顺序、重复键更新、空更新与独立复制不变。缓存附着不可变值，修改沿既有底本失效链处理。

等待静态元数据由`c1n_clock_boundaries`/`c1n_without_clock`缓存完整condition的纯结果，其`_uncached`保留原递归顺序，目标待定、时钟及本人非时钟前提仍逐次核对。本人声明白天等待策略时，夜间片段也在日内480起点截短；既有费用与更小本人上限保留。该等待边界属于新行为条件，不能将后续吞吐当作纯同条件提速。

本文说明仓库当前可编译、可调用的 DV Core 与正式会话接口。依据是 `compiler/compiler.dv`、`compiler/Runtime.cs`、`core/*.dv` 和 `verification/session/*.dv` 的现行源码。示例只使用中性对象，不声明项目剧情。源码审阅不等于本轮运行验收。

[语言总览](../README.md) · [完整语法方案与实施边界](../SYNTAX.md) · [编译器](../compiler/README.md) · [宿主 Session](../Session/README.md) · [行动费用详述](COST.md) · [正式会话运行说明](../verification/README.md)

## 1. 实现层与边界

唯一编译执行链是 DV 源 → DV 编译器生成 C# → `Runtime.cs` 平台 ABI → 编译后的 Core/session。编译器自身保留 S 表达式启动源；seed 提供启动读取与值类型。世界规律、人物认知、推知、模型、规划、行动与结算算法在 DV 中。Godot 经 GameEngine → DvSession → 生成的 Session 类库调用同一正式接口。

当前语言支持普通计算和下面明确列出的领域展开。SYNTAX.md 后续章节中的完整类型、操作符和领域语法是语言方案，不能一概照抄到现行编译器。当前并无完整静态主体能力证明、仿射票据类型、一般精化证明、通用模块系统或目标无关 Typed IR。运行时的主体、版本、来源及资源核对仍是重要边界。

`core/` 是通用领域库；`verification/session/` 把这些机制组成正式会话；内容、人物、规则实例与 oracle 分包。报告和调试可以读取权威证据，但这些投影不会成为人物知识。预测状态、本人接受的说法与物理事实分别保留。

## 2. 当前表层语法

### 文件、标识符与注释

源码为 UTF-8 `.dv`。标识符支持 Unicode 字母、数字及下划线，开头不可为数字，扫描时按 NFC 归一化，区分大小写。换行结束语句，括号、列表和记录内部可以换行。块以冒号和更深的空格缩进开始；同块缩进一致，Tab 报错。现行扫描器支持 `//` 和 `;` 行注释；`/* ... */` 与字符字面量尚未实现。字符串用双引号，支持 `\n`、`\r`、`\t`、`\"`、`\\` 和四位十六进制 `\uXXXX`，其他转义报错。

```dv
define 函数 fn
define 返回 return

module neutral.example

函数 square(value: Number) -> Number = value * value

fn choose_total(first, second):
    let base = first + second
    var total = base
    while total < 10:
        total = total + 1
    if total == 10:
        返回 total
    else:
        返回 0
```

`define 原词 目标词` 位于声明之前，一次替换标识符 token，不递归替换，不改字符串和注释；同一别名重复声明报错。各源文件保持独立别名作用域。现行 `module ...` 行被识别并跳过，尚不建立命名空间、导出或依赖隔离；函数名称在组合程序内仍须唯一。`import`/`export` 尚未实现。

构建、调试与编辑器共同使用的源组合适配器支持独立行 `#include ../core/00-data.dv`。路径相对声明文件；依赖先于使用者、同一路径仅加载一次，缺失和循环引用报错。include 不由裸编译器独自解析，直接传原文件时应使用共同组合入口。

### 函数、绑定与控制流

| 写法 | 当前语义 |
| --- | --- |
| `fn name(a, b):` | 位置参数函数；参数未注解时为 Value；函数支持前向引用、递归及相互递归 |
| `fn name(a: Number) -> Number = expression` | 可选单个名义类型注解和单表达式返回 |
| `let name = expression` | 局部绑定，不可重新赋值 |
| `var name = expression`、`name = expression` | 可变局部与重新赋值；不能给未绑定变量赋值 |
| `return expression` | 从当前函数返回值 |
| `if expression:`、`elif expression:`、`else:` | Bool 条件分支；elif降为同层else中的if |
| `while expression:` | Bool 条件循环 |
| `function(a, b)` | 位置实参按顺序求值，检查函数名称与元数 |
| 单独表达式语句 | 调用或计算并舍弃返回值；机制返回的新状态仍须显式保存 |

现行赋值目标是局部变量。字段和列表更新使用 `dv_with`、`dv_store` 或容器 ABI；没有 `object.field = ...` 的持久更新语法。`let` 限制重新绑定，并不使其引用的普通列表自动只读。`for/in`、`break`、`continue`、`match/case`、闭包、函数值、默认参数、具名实参及一般泛型尚未实现。

### 值与表达式

```dv
fn example():
    let entries = [1, 2, 3]
    let record = {name: "neutral", "count": entries[0]}
    let copy = dv_with(record, "count", 2)
    return copy.count
```

记录字面量展开为有序 `[key, value]` 对的列表，字段名转换为文本。`record.field` 展开到 `dv_get`，缺字段报错；有默认值的查找用 `dv_find(record, key, fallback)`。下标 `items[index]` 展开到 `rt_at`，是零起始访问，越界报错，当前并非 Option。普通列表与记录共享平台列表表示，不具备通用 record/enum 声明的静态字段保证。

当前运算优先级从高到低如下；同级二元运算左结合：

| 层级 | 写法 | 说明 |
| --- | --- | --- |
| 后缀 | `f(...)`、`.field`、`[index]` | 调用、字段、下标 |
| 前缀 | `-`、`not` | 数值取负、Bool 否定 |
| 乘除 | `*`、`×`、`/` | 数值运算；具名 mapping 调用在左侧时为映射应用 |
| 加减 | `+`、`-` | 数值运算；文本连接用 rt_cat |
| 比较 | `== != < > <= >=` | 返回 Bool；结构比较用 dv_same |
| 逻辑与 | `and` | 短路求值 |
| 逻辑或 | `or` | 短路求值 |
| 管道 | `value |> f(args)` | 把 value 插到 f 的第一个位置参数 |

`%`、`.*`、`@`、`~>`/`⇝`、`|-`/`⊢`、`>>`、`=>`、`?` 和 `~` 的完整领域运算尚未实现。某些符号能被扫描为 token，不代表具备表达式语义。普通 `==` 使用平台基本值/列表身份语义；列表、记录与命题的结构值相等使用 `dv_same`（`rt_same_value`）。普通分支接受 Bool；领域三值真值用文本 `"true"`、`"false"`、`"unknown"`，须显式比较。

列表推导式为 `[for item in source if condition: expression]`，可省略过滤条件，可嵌套。source在外层作用域求值一次；item仅在过滤与投影中绑定为Value，不泄漏到外部。按源顺序求值过滤，只有保留项求值投影。source与过滤条件分别在运行时核对List/Bool，未提供静态集合元素推导或短路any/all函数。源列表是普通可变值，遍历期间通过别名修改它会影响后续迭代，推导式不建立不可变快照。这里的for不是独立语句。

```dv
fn positives(items):
    return [for item in items if item > 0: item * 2]
```

### 数值和名义检查

现行检查器识别 `Value`、`Number`、`Text`、`Bool`、`Acceptance`、`Probability`、`Confidence`、`Reliability`、`Weight`、`Nat` 和 `Mapping`。显式参数、返回和局部注解参与名义核对。未注解不等于已推导出完整领域类型。区间类型的字面量须在 `[0,1]`；Weight 非负；Nat 为非负整数。`Acceptance(x)`、`Probability(x)`、`Confidence(x)`、`Reliability(x)` 是保留的范围构造器，不能由同名用户函数替代。

普通数字字面量使用有限 double；`rt_exact("1/10")` 或 `rt_exact("0.1")` 创建精确有理数，带有理数的算术沿精确路径执行。把既有 double 转成精确数只保留该 double 的十进制表示，不能消除既有误差。当前没有通用 Int/Rat/scalar/unit 声明、单位后缀、矩阵形状、一般区间推导或 `by` 证明。有限域配平主动使用精确数，不能据此宣称整个 Core 已精确化。

## 3. 已实现的领域声明

### axiom：具名规律数据

```dv
axiom neutral_change(item):
    return dv_axiom("neutral_change", 1,
        dv_true(), [dv_assign(dv_ref(item, "state", "ready"), true)])
```

表层 axiom 使用函数体，返回规律记录。声明本身不执行、不采纳。`dv_instance(id, law)` 给同一规律的不同对象实例分配身份；重复采用同名实例不会静默覆盖。启动语法另支持 `(axiom Name Version (Parameters...) Premise Effects)` 的数据构造展开。

### mapping：具名认知映射

```dv
fn neutral_read(context, input):
    return {context: context, input: input}

mapping neutral_mapping(context):
    input Percept
    output InterpretationSet
    using neutral_read(context)

fn neutral_apply(context, input):
    return neutral_mapping(context) × input
```

展开为带额外末尾输入参数的 DV 函数，`using` 指向具名调用。输入/输出标识用于声明外观，当前不构成完整主体能力或 Percept 静态类型证明。乘法左项须直接是具名 mapping 调用；不支持任意映射闭包值或默认矩阵计算。正式话语接收在 `20-speech.dv` 中使用此机制；映射计算与实际采信分阶段处理。

### acceptance：数值与门槛分类

```dv
acceptance neutral_acceptance(value, threshold):
    degree item: Acceptance = value
    levels:
        held from 0
        accepted from threshold
```

展开为 `dv_acceptance_classify(degree, levels)`。现行分句顺序如上，建议保持示例的四空格与八空格缩进。levels 必须非空，从零开始，门槛严格递增且均在 `[0,1]`；取不超过 degree 的最高门槛，等于门槛进入该级。返回 `{degree, level}`。`dv_acceptance_transition` 比较前后，内置 `accepted` 降级回执为 `withdrawn`。分类本身为纯函数，不自动扫描所有认识，也不自动提交人物状态。

### imprint：有限域配平

```dv
imprint neutral_imprint(capability, goal, variables, laws, limits, prices):
    require capability capability
    achieve goal
    vary variables
    conserve using laws
    limit using limits
    minimize using prices
```

按固定顺序展开到 `dv_imprint_solve(capability, goal, variables, laws, limits, prices)`。这里的 achieve 是目标记录，vary 是已声明有限变量/选项，conserve 是线性约束，limit 是资源和工作预算，minimize 是资源价格。当前未实现 SYNTAX 中一般命题目标、连续优化及任意认知变换求解。

当前配平输入的关键字段如下，资源向量长度须一致：

| 输入 | 数据合同 |
| --- | --- |
| goal | coefficients 为坐标系数，minimum 为接纳下界；degree 为与候选 values 的点积并须在 `[0,1]` |
| variables | 各变量的 options 非空；每个 option 给出 value 和各资源 cost 向量；选项组合构成完整有限域 |
| laws | 各项 coefficients 与 total 声明精确线性等式 |
| limits | balances、search_budget、work_remaining、revision、subject；预算限定实际枚举和独立核验工作 |
| prices | 非负资源价格向量，与 charges 点积形成可比较成本 |

求解按声明顺序枚举，平价保留先到候选。结果含 status、best、visited、total、revision、subject；best 保存 index、choices、values、degree、charges、cost、feasible。有限域索引须位于当前 double 可精确表示的整数范围；这不授予未声明或连续状态可达性。

求解结果区分 Optimal、Feasible、Infeasible、Unresolved；Optimal 须由 `dv_imprint_check` 独立重建候选并检查覆盖、可行性及更便宜候选。`dv_imprint_commit` 当前只提交经核验 Optimal；其他结果仅结算已经发生的工作量。`dv_imprint_prepare/apply` 从本人字段和既有 ledger 建立坐标、核对证书并一并返回认知、物理、资源与工作额度；来源保持 Imprint，不转成 Observation。返回记录由权威宿主持有，当前没有不可伪造静态能力类型。

`slice`、`prop`、`rule`、`reading`、`action`、`decision`、`model`、`protocol`、`worldlaw`、`norm` 等其余表层声明尚未实现；对应机制由下面的函数和数据合同提供。不要把尚未解析的声明当作当前 API。

## 4. 世界、状态与知识边界

### 基础数据与复制

`dv_world(id, domain, subject)` 的 domain 为 physical 或 mind；physical 的 subject 为空，mind 必须指明主体。共同字段包括 id、domain、subject、revision、objects、adoptions、history、seen。物体包含 id、kind、facets；字段地址为 `dv_ref(object, facet, field)`，本人专属引用可用 `dv_scoped_ref`。`dv_same_local_ref` 只把省略的 subject 解析为当前主体，显式其他主体不相等。`dv_slice` 返回带世界、主体、版本和选定字段的切片。

未知 cell 的 known=false；value=false 是占位，不代表已知为假。已知 cell 保留 value、confidence、time、claims/basis、conflicted；聚合结果进一步保存 support_share、source_reliability、epistemic_confidence。`dv_uncontested` 区分已知且无争议。未知或冲突不默认为零、false 或中性人格参数。

`dv_clone` 返回独立可编辑副本；`dv_with`、`dv_with_many`、`dv_without`、`dv_store` 返回新值，调用者必须发布返回值。记录保持顺序；查找重复键取末项；dv_with 替换全部同键项，没有该键时追加。Runtime 通过不可变底本和延迟子树实体化维持分支隔离；`rt_snapshot` 发布递归只读快照，不能用 rt_push/rt_replace 修改。原输入、预测分支和事件证据各自隔离，结构共享不会授权原地修改输入。

`dv_stage_store_owned`、`dv_adopt_into`/`dv_finish_owned_adoptions` 是内部独占构建接口；批量采纳逐条保留原校验顺序，finish 核对旧前缀和长度，再发布世界。`dvs_owned_state_update` 只用于宿主线性会话外壳：新建顶层和字段对，沿用已属于本会话的值引用，调用者放弃前态。它不替代 world/mind、预测、规划分支或证据快照的复制合同。

### 命题、采用与执行

命题构造器包括 `dv_true`、`dv_fact`、`dv_at_least`、`dv_all`、`dv_not`；`dv_truth` 返回三值文本。效果构造器是 assign 与 increment。规则包含 name/version/premise/effects，采纳另含 mode、confidence、basis、active。物理采纳置信度为 1；主体采用需要依据，可修订或暂停。

| 领域和采纳模式 | 入口 | 产生的结果 |
| --- | --- | --- |
| physical / enforce | dv_execute | 核对权威前提、版本和事件身份，阶段性原子变更及真实回执 |
| mind / predict | dv_predict | 本人认识的 prediction 副本，不发布真实效果；冲突数值状态显式拒绝 |
| mind / infer | dv_infer | 本人有据推知，保留规则、前提、置信和依赖身份 |
| mind / norm | dv_norm | 本人规范判断，不自动变成物理限制 |

`dv_stage` 先在独立分支检查和应用效果，再发布结果；失败分支不污染原世界。版本冲突、重复事件、错误 domain、模式或缺少依据由库明确拒绝。推知的置信受前提与规则限制，同一规则/输出字段重算替换原依赖来源，事件身份仍记录实际发生的推知。

### 观察、来源与证据聚合

`dv_binding` 把客观实体绑定到某主体的本地标识；`dv_perceive` 由可信适配器提供 bindings/grants，`dv_perceive_changes` 只暴露已完成且获准的变更。`dv_project_value` 递归处理 entity-ref/ref；复合内容有未绑定客观引用时整项保持未知，避免泄漏客观 ID。

`dv_update_mind` 只接受该主体的观察，不读取 physical。它核对版本、主体与重复事件；相关证据依 dependency 替换，独立材料竞争。更新保留来源、origin、trust、time 和历史。较晚的直接物理感知可更新当前状态；较晚收到的证词、回想或推知不因接收更晚就抹除历史直接观察。支持份额与来源可靠度分开，重复同源证词不会把不可靠材料累加成确定性。

### 传播与实际话语

`dv_segment`、`dv_signal` 表示带强度/频段的片段；`dv_transport` 对显式路径逐段检查连通、介质、门控、启用和增益，只保留实际到达片段。未知与 blocked 分开，丢失内容不恢复为全文。`dv_transmit` 再核对规律采用、模式和前提；`dv_transport_theory` 显式声明传播语义。

会话 `dvs_speech`/`dvs_speech_owned` 在实际 Complete 后用传播结果更新实际收闻者；普通入口复制输入列表，owned 入口只在线性会话替换收闻者。映射、字面理解、范围桥接、本人采信与推知分别记录。候选接收者、可见人物及预计可达性不表示实际收闻；等待行动者的 pending_heard 只保存真正听到的话语及当时声道证据，到期后理解，不重查已经结束的声道。

## 5. 价值、回应、推理与模型

`dv_criterion` 声明数值/目标准则；`dv_value_subjective` 只评价本人或预测，`dv_value_objective` 只评价 physical，结果保留分项、未知项和完整性。客观价值是相对于显式准则的实况评价，无内置全知道德分数。评分不授予行动合法性。

`dv_person_policy` 和 `dv_accept_policy` 保存本人价值取向及采用依据。`dv_appraise_request` 从本人认识读取关系、情感、身体/注意状态及请求参数；未知或冲突保留。`dv_respond` 先核对候选可行性、预测来源版本，再按政策选择；无候选返回 no-response，平分保持声明顺序。行为意图与表达轴分开，`dv_affect_proposal` 只提议带回落的情感变化，选择不直接写回关系或情感。线性权重与成本为工程/游戏参数，未构成心理测量有效性证明。

`dv_proof_step`/`dv_judge_conclusion` 先校验前提、infer 采用、结论、依赖与预算，再计算论证成本。共享节点计一次，假设成本为正；成本、置信、hypothetical、reasonable 分列。合理度 `1/(1+cost/reference)` 是相对声明成本的尺度，不是真值或概率。操作规律不能证明其效果已经发生；当前不提供自动证明搜索或完整一阶逻辑。

`dvs_model`/`dvs_model_execute` 用本人材料和已采用法则展开付费重演，遮蔽待预测输出，再把独立预测与实际本人观察比较。模型保留源采用、输入、工作量、预测轨迹和比较。`25-simulation.dv` 沿读写依赖逆向选择证物相关演算；切片仍遮蔽原计划全部输出，吻合不能单独建立完整解释。因子域覆盖证书的来源、已支付计算和当前性分别核对，不把未执行分支当作已发生。

`dv_accept_explanation` 将付费模型、竞争解释、动态槽位、本人观察与政策组成可撤回论证；`25-simulation.dv` 检查模型/论证是否过期并在真实新材料到达时重评。预测、解释接纳与观察来源仍有区别。输入改变后不能沿用失效论证冒充当前依据。

### 主体初始化、共鸣、侵蚀与公开立场

`10-subjective.dv` 的 `dvs_initialize_cognition(actor, seed, ordinal)` 根据带来源的 cognitive_initialization 从显式 options 采样。非负整数 seed 与主体顺序决定可复现整数流，结果保存 cognitive_seed、cognitive_samples 和 first_day_mind；profile 的显式字段在采样后合并，未声明的认知不凭空生成。此初始化只设置开局参数，不预置模型结论、答案或行动结果。

`dvp_owned_quantity` 只从显式本人政策参数或本人已知且无争议的字段读取数值。主动共鸣资格使用 `drive × relevance + truth_bonus - caution - perceived_risk` 的本人效用 U，并要求 U>0、来源与前提成立；实际成本仍走 ledger，不把感知风险当作权威成本。被动闪回与主动共鸣分开。`dvs_complete_cognitive_effects` 仅在实际成功 Complete 且 retrieved_when 在本人认识中为真时，提交主动共鸣的 coupling、限定信任变化、夜间资格和事件回执；预测或失败不会取得这些后果。

`dvs_erosion_update` 从内容声明的 physical_ref 读取真实暴露，并按 coupling_factor 和本人已结算 coupling 修正暴露；阈值、memory_period 由内容给出。阶段为 early/middle/late/terminal/death，已到等级不自动下降。进入 late 后，按声明周期恢复 first_day_mind，提升认知版本并清除对应模型、解释、收闻、公开立场和依赖缓存；它是显式病理更新，不能描述为正常证据采用。terminal/death 由 `dvs_actor_can_act` 阻止行动，调度与到期分派重查并清除不能执行的排队行动。物理暴露可触发此病理接口，但不因此授予本人读取权威世界的知识。

`dvs_receive_public_claim` 仅由实际可听接收路径调用，记录发言者、公开立场键值及 source/parse 事件；同一发言者的历史已收闻立场与新声明 conflicts_with 匹配时形成 observed_claim_conflicts。没有实际接收不生成冲突证据。动作可声明 received_claim_conflict、received_speech_requirement、opportunity_requirement、desire_requirement、owner_interpretation_requirement 等本人资格；它们检查已获来源、现场和本人记录，不代替真实行动。

`dvp_owner_requirements` 同时用于正式候选、预测搜索和个人同组比较。主动 resonance 的正效用加入候选 progress；passive 不使用该项。数量声明接受数值、本人 policy_key 或本人 ref，optional false 为零，错误记录形状与非数政策值明确报错。resonance.requires、引用数值与 night_call_requirement.night_when 纳入规划依赖及签名；完整主体备忘包含实际资格账本，不能复用遗漏这些输入的结果。

`received_claim_conflict` 核对本人冲突的 speaker/key、previous_source、source_event、parse_event 与 basis。`received_speech_requirement` 接受单个 `{speaker, action_id}` 或 `{any_of: [...]}`，每个匹配项需要实际收闻的来源与理解回执。`completed_action_requirement: {action_id}` 只读取本人 completed_actions；该列表仅在真实 Complete 的 execution.status 为 applied 时追加。used 仍记录包含失败的已尝试动作，不能代替成功。预测不会新增真实完成记录。

规范的内容收据、本人采纳和实际执行分别保存。话语的 norm_proposal 只在实际可听传播、字面映射一致且本人已获得该内容时进入 norm_proposals；读板 `norm_receipt: {norm, content}` 也须成功现场执行、有实际观察且本人认识支持 content。该收据不自动同意提案。norm_decision 候选须匹配本人实际收到的完整规范，再由 personal_choice/value_order 比较；成功 Complete 后才写本人 norm_stances 与 policy.norm_rules，并保留内容、理解/观察、执行事件来源。拒绝移除本人对应规范，公开承诺仍不替代后续实际行动。工程实现与自然模拟达成情况分别报告。

私下行为的 private_execution 要求有来源、本人 requires 成立，并比较本人所知 benefit 与 resource_cost，只有 benefit>resource_cost 才具备资格。normative_method 再按本人有来源、当前适用的 norm_rules 与 value_order 判断；公开承诺不会直接写成物理禁令，私下规范也不等于必然执行。实际机会与共同动作选择仍须成立。

### 实际玩家影响的静默后果链

实际话语可以携带以下带来源的意图声明；这是可审查的数据合同，不是自动影响目标的命令：

```text
speech.manipulation = {
    targets: [目标主体标识],
    method: 方法标识,
    action_ids: [目标后续实际行动标识],
    consequence_when: 物理命题,
    basis: 来源列表
}
```

正式 Complete 以 state.player_actor 写入 actual_player_actor，并在实际发送路径计算 actual_literal_truth；内容提供者不能用自填身份替代当前玩家。实际玩家正式发出、字面在权威世界为真的话语，只有指定目标真正听到并完成 listener_parse 后，`dvs_receive_player_influence` 才把 speech_event、parse_event、player、target、method 和原声明保存为 pending_player_influences。预测理解或未实际收闻不写入该记录。

后续 `dvs_complete_player_influences` 仅在指定目标的指定行动成功 Complete 后，比较同一次执行前后 physical 的 consequence_when。前态非 true、后态 true 才形成 player_influence_history；已有结果、失败、未指定行动不记为已发生后果，未达成的待办继续保留。历史保存玩家、目标、方法、真实话语/解析/执行事件、行动、时刻及前后命题结果。该链是有明确声明和执行条件的来源账，不宣称心理因果已被普遍证明。

此记录不向玩家正文输出，不为目标增加候选、评分或采纳，不改目标的正常决策；成功后果依据目标实际执行，不能从操纵意图推定完成。普通 influence_consequence 的本人后果记录与这条实际玩家身份/物理前后变化链分别保留。

### 发言者信任与抗语用参数

`dvs_speaker_trust` 从本人 speaker_trust 出发，只应用带依据、匹配发言者的 class_bias，再限制到 `[0,1]`。这个结果进入证词与提案接纳；偏见是主体参数，不是发言者客观可靠度证明。语用解释门槛取 `max(implicature_threshold, Implicature_Resistance)`；解释仍检查本人已知前提、桥接依据、可用性、反证与撤回条件。scope_reference_check 开启时核对字面范围和解释范围的桥接真值，抗语用程度不自动产生新事实。

### 重演深度的实际执行限制

`Replay_Depth` 是 `25-simulation.dv` 对有序因果 stage 的展开限制，独立于 `plan_depth` 的行动搜索深度。同一连续 stage 的步骤共享一次阶段名额；跨到下一 stage 时消耗新的阶段名额。深度耗尽则停止真实展开，记录 replay_depth_limit、replay_stages_expanded、replay_depth_exhausted 和 replay-depth-exhausted，未展开后继不能作为已有覆盖的预测；特征投影保留相应未知与覆盖标记。

未完成模型保留 plan_complete=false；重新比较观察也不能把它认证为完整 explained 论证。计算预算、实际支付和深度限制分别保留。深度是允许的因果阶段上限，不代表本次实际展开量、正确性、已完成模型或人物知道未来。

## 6. 规划、费用与正式行动会话

### 本人决策与规划

`dvs_ready`/`dvs_reason`/`dvs_choose` 组合本人推知、假说、模型、论证与共同选择。`dvp_eligible` 保留动作前提、本人所有权、规范与相关证据条件。`dvp_plan` 从当前可执行首步建立预测后继，保存本人来源、即期分、回传、预算和停止原因；未知检视结果不写成确定观察。

规划对预测状态去循环，跨路径保留分数/耗时非支配路径；机会 access 键只用于去重，不把返回相同认知状态当作新进展。有来源的未完成沟通目标可得到接近机会分；该分是当前叶节点预期机会，不累积到无关后继，不预测他人实际接受。根层存在净正值正式非移动行动时，完成可覆盖的一层后继比较再选最佳首步；深度、预算或后继不足分别记录，有限理性搜索不保证全局最优。

若最佳首步是移动，成功 Complete 后在主体中暂存规划路径的下一非移动行动。再次决策重新运行规划，仅在它仍是直接可执行且净正值的候选时兑现；否则丢弃。`planned_followup_honored` 与 `superseded_path` 记录兑现的实际选择，不改变推演、费用和权威答辩条件。相关源码：`verification/session/10-subjective.dv`、`30-runner.dv`。

内容可为阶段交接声明 `evidence` 目标，并以本人已收的阶段字段作为 `refresh_refs`。错过公开问话后，既有旧开放字段观察不足以满足刷新下界；现场重新核对经正式行动取得 perception/direct 回执。话语是否听见仍由声学资格判定，目标刷新不生成听闻或答案。例见 `verification/scenarios/chapter1/content.dv` 的公开题目收据目标。

章节开场的 own_material 目标可依持续公开信息的本人收据激活，后续题目阶段不撤销该资格。专业请求清空直接观察授权，答复仍需另一主体的本人依据和实际发言。带 speech 的答辩候选在规划资格与 Complete 均以 `dvs_speech_truth` 检查本人话语，避免将未知陈述排队成注定拒绝的行动；见 `verification/scenarios/chapter1/opening.dv` 和 `verification/session/15-action-planning.dv`。

每次有资格决策从会话保存的 planning_limit 取得本次 plan_budget，实际行动搜索节点 plan_expansions 扣减并累入 planning_work_total；依赖闭包改用独立 dependency_budget，其实际工作单列 dependency_expansions/dependency_work_total；这与行动资源账本、付费模型额度、游戏时钟分开。等待者不重复搜索。检视与请求以本人语义输入签名去重，时间戳和无关来源编号不冒充新线索；refresh_until/refresh_refs 只使用显式列出的本人已知相关字段时间，缺省保持原 minimum_time。同条件检视去重在共同资格入口执行，未知结果也保留尝试记录。

### 报价与账本

`70-reasoning-cost.dv` 对 time/computation/stamina 分别结算。quote 绑定 ledger/world 身份与版本、行动、事件和上下界；admit 按最大额预留；settle 核对同一 reserved 回执、实际消耗范围和 applied/rejected，再退还未用预留并记录 spent。重复事件、过期报价、重复结算及超额拒绝。动作偏好分不替代资源余额。

即时 Core 调用仍在结算时推进耗时。正式排队行动带 scheduled_start，世界已等待到期，账本仍支付完整时长一次，完成时不重复推进世界时钟；Complete 的 actual.time 必须与排队声明一致，不默默改期或倒拨时间。

### prepare → 现场回执 → complete

1. `dvs_session_open(story, capability)` 从正式内容创建 physical、actors、账本、事件、控制状态和时域；`dvs_session_active` 判断行动上限、时间域及活动/排队状态。
2. `dvs_session_prepare(state)` 选择可决策主体，执行本人认知与选择，把行动排入到期队列。未到期时保留 scheduled_action。调度等待按环境时钟边界执行规律；尚可改变世界的进程逐分钟结算，相同时刻按循环主体顺序处理到期项；到期形成唯一 pending、报价和意图。
3. 宿主读取 `dvs_session_intent`。Godot 执行真实移动并返回现场结果；formal-model 适配只产生逻辑回执。移动区分逻辑地点、当前地图执行房间与显式站点，宿主还需核对主体、序号、动作、站点、房间、实际终点和到达误差。
4. `dvs_session_complete(state, receipt)` 核对 pending/action_event、实际时长及成功条件，再执行权威规律、观察、传播、答辩和费用结算。现场接触观察只在相应成功完成后形成。预测、卡片呈现或准备完成不能产生这些事实。
5. `dvs_session_close(state)` 关闭并返回会话结果。`dvs_run` 是沿同一 prepare/complete 的 formal-model 便利运行入口，不含 Godot 几何。章节扩展由 game/entry.dv 在此链外层衔接，不重建开局或清空既有事件/账本。

pending 存在时不重新分派行动。scheduled_action 优先于修订唤醒；空闲主体同一时刻只有本人修订或真实待理解收闻变化才再次决策，decision_revision 记录实际选择结束后的修订，避免内部推知造成自唤醒循环。

`dvs_set_player`、`dvs_player_options`、`dvs_select_player` 使用同一会话保存 player_actor/awaiting_player/selection；人工候选来自本人认知及共同资格，实际选择仍走 prepare/complete，不注入行动结果。game/entry.dv的`dvg_intent`为已准入候选附加`kind`及`interaction`空间绑定（`site`/`person`/`self`/`doorway`，跨房间携带destination），仅供宿主接近与局部选项。绑定不含观察结果、不改变资格；同房间WASD/鼠标接近不调用DV、无时间及行动次数结算。热点交互与跨房间才选择原动作，Complete仍以实际回执生效。呈现完成本身不触发Prepare；生产宿主以view_ready/dv_next请求下一步。

`dvs_answer` 分别核对题目匹配、字面真值、本人支持、论证完整性与权威接受规律。公共结论等级、本人正式接受、实际章节交接分别记录；他人的公共结论不替代本人回执。正文与呈现仍只展示该主体真正观察/收闻后获准显示的材料。

同寝区活动的内容声明在主体成功执行后，经 `public_observers` / `public_grants` 向实际同室且可见的旁观者传播对应字段；行动前没有已发生事实的检视。`inspection_attempt_signatures` 仅在 applied 检视后保存，失败执行不写观察或完成记录。开场的欢迎、自我介绍、奉茶按 `completed_action_requirement` 检查实际完成顺序；第四问完成后，开场返回活动厅目标停止提供折返收益。第一问与第四问答辩、沈鼎回想姓名的可见正文由原生内容 action 的 `speech.surface` 或 `surface` 提供，正式答辩回执另记。
正式第一问开放后，乙区门闩检视持续进入本人调查候选；执行仍核对真实门厅位置。甲区挑闩目标使用自身权威动作的时间和非时间前提，未来机会等待仅读本人已获得的前提，不预置挑闩结果。成功等待动作从现场时钟取得当前值，时间资格读取这次实际观察。
`game/entry.dv` 在本人已知姓名之外调用开场内容的 `c1_op_known_speaker_label`，根据观看者实际取得的角色信息显示代管者称谓；未获得身份信息时保持未具名。

### 生产认知配置与有据等待

生产 `game/entry.dv` 提供以下开局入口；配置值为文本，示例为当前默认配置：

```dv
fn configured_session():
    return dvg_open_config({seed: 1, profile: "Standard_Detective", detective_zone: "zone_yi"})
```

`dvg_open()` 使用相同默认值。跨区开局可选择 zone_jia、zone_bing、zone_ding；这改变声明的出生地点及本人的开局位置信息，实际后续移动与观察仍走正式执行。配置不注入答案，不绕过答辩、资源、碰撞或观察条件。

`verification/scenarios/chapter1/cognitive-profiles.dv` 的 c1c_install_scene 安装内容所有的参数与来源。当前 Standard_Detective 工程初值为 Replay_Depth=64、plan_depth=6、Implicature_Resistance=0.75、scope_reference_check=true；Foolish_Detective 为 Replay_Depth=2、plan_depth=2、Implicature_Resistance=0.25、scope_reference_check=false。两档分别声明价值排序与规范政策，低配档保留所声明 class_bias。驱力、谨慎、发言者信任、信息权重、语用门槛、共鸣相关度、真相收益和感知风险从窄幅 options 采样。profile 覆盖字段与采样顺序见初始化接口；这些值是当前工程初始范围，不是叙事事实、心理测量结果或验收目标。

第一章夜间内容提供 180/60/15/5/1 游戏分钟的正式等待动作。`c1n_known_wait_actor` 只接收本人有来源的未来意图或实际收到的机会时间，选择不越过目标的已声明时长；不足一分钟的余量仍按普通一分钟动作处理，不宣称准确到达预约时刻。等待经过共同选择、queue、世界时钟推进、权威 Complete 与资源结算，不直接改时钟跳到作者预定结果。等待不是保证其他夜间行动发生的脚本。

铜口呼名使用 night_call_requirement：仅先前主动取回成功、已保存 night_call_eligible 的主体，在午夜窗口实际位于声明地点、先经正式现场检视获得本人 night_observed，再经自己的定向感知行动成功 Complete，才取得本人实际听闻。白天取得资格本身不算已听闻，时间窗口或候选出现也不代替感知。正文使用私有代称，不生成真名、不开放名录库或其他主体访问；预测只保留预测状态，不产生该主体的真实收闻。

生产第一章保留至第2日06:00（从第1日00:00起第1800分钟）才使第二章内容可用。此扩展按实际 physical.time 检查，承接既有世界、认知、事件与账本，不等于角色实际跨门。当前总时域上界为从第1日00:00起第7200分钟，game_duration_budget 依开局时间计算至该边界；原有有限行动额度保持独立限制。会话可以先因行动额度、活动或其他条件停止，时域覆盖不证明已实际运行五日。本轮时间范围和行为模型改变，不能与旧窗口作同条件性能结论。

相关生产配置函数（内容声明的数值和来源由调用者提供）：

```text
Standard_Detective(replay_depth, resistance, empirical_values, mediation_rules, basis)
Foolish_Detective(replay_depth, resistance, values, norm_rules, class_bias, basis)
cognitive_sample(key, options, basis)
cognitive_initialization(profile, samples, basis)
c1c_install_scene(scene, seed, profile)
cognitive_erosion(exposure_ref, thresholds, coupling_factor, basis)
dvg_open()
dvg_open_config(config)
dvg_chapter_two_ready(state)
dvg_extend(state)
dvg_prepare(state)
dvg_complete(state, receipt)
```

`cognitive_erosion`返回的声明可扩充`incapacity_when`条件；每个threshold的`when`也可省略。Session在实际physical中核对可选条件，只有true才将对应剂量级推进或投影incapacitated；未知不当作已发生，无条件条目保持原即时阈值。rank仍单调，晚期认知损害仍执行；生命、症状和治疗的具体因果来自内容法则，剂量本身不替代内容的死亡或永久失能事实。声明例：`dv_with(cognitive_erosion(exposure_ref, [{"at": dose, "rank": 3, "when": terminal_condition}], coupling, basis), "incapacity_when", incapacitated_condition)`。未提供incapacity_when时不新增投影字段。

## 7. 历史、调试、保存与平台 ABI

`70-reasoning-cost.dv` 的 `dv_session` 是检查点容器，和正式 `dvs_session_*` 调度器不同。commit 核对更高版本、身份、domain/subject 与非倒退时间；advance 只推进容器时间。`dv_inspect_at` 选择不晚于请求时刻的本人真实检查点，支持 worldview/state/slice；未来、缺失历史明确报错，不用当前知识推造历史。worldview 包含有效规律采用和价值政策。

`dvs_event`/`dvs_event_prepared` 经 rt_snapshot 发布只读完整事件快照。`dv_debug_events(events, start, config)` 按原顺序投影事件、父编号、行动与结果；空 actor/type 过滤表示全部。返回 next 为原事件游标，过滤不改变调度或人物认识。完整快照保留在原始轨迹，摘要和 phase_progress 都不表示未完成行动已经完成。

Session 宿主提供流式 WriteTrace/WriteSave；Trace 的精确数沿原字符串格式，Save 使用 `$dv_rational` 标签。rt_json_write 写 `.partial` 后成功发布正式文件，新目标不覆盖原证据。关闭等待在途工作，SessionClose 不自动重试；complete 与各阶段错误以实际关闭结果为准，TraceClosed 只表示关闭结束。普通 JSON 列表/键值对不是可执行 DV 声明。

下面列出编译器 `arity` 接受的全部平台函数。除特别注明外，错误经异常/rt_fail 报告；ABI 负责基础值和 I/O，领域校验由 DV 库执行。

| 类别 | 入口（括号内为参数） | 语义 |
| --- | --- | --- |
| 数值 | rt_number(text), rt_is_number(text), rt_is_numeric_value(value), rt_exact(value), rt_floor(number) | 文本解析/可解析检查；运行值数值类型检查；精确有理数转换、double 向下取整 |
| 算术 | rt_add(a,b), rt_sub(a,b), rt_mul(a,b), rt_div(a,b), rt_lt(a,b) | 带 Rational 操作数走精确算术；除零/非有限值拒绝 |
| 相等与布尔 | rt_eq(a,b), rt_same_value(a,b), rt_not(value) | 基本/身份相等、递归结构相等、Bool 否定 |
| 文本 | rt_cat(a,b), rt_length(text), rt_slice(text,start,count), rt_chr(text,index), rt_code(text), rt_show(value) | 连接、UTF-16 长度/子串/单元、码值、基本值格式化；show 不格式化任意列表 |
| 标识符 | rt_id_start(text), rt_id_part(text), rt_normalize(text) | 标识符类别、NFC |
| 列表 | rt_list(), rt_push(list,value), rt_at(list,index), rt_replace(list,index,value), rt_list_with_index(list,index,value), rt_count(list), rt_is_list(value) | 构造、追加、读取、原地替换、新根共享项替换、计数；写只读快照时报错 |
| 记录 | rt_record_index(record,key), rt_record_find(record,key,fallback), rt_record_with(record,key,value), rt_record_with_many(record,updates) | 末项优先定位/查找；with只替首个同键项，with_many更新全部同键项；独立复制 |
| 复制 | rt_copy_value(value), rt_snapshot(value) | 独立可编辑分支、递归不可变快照 |
| 纯值备忘 | rt_memo_get(owner,key), rt_memo_put(owner,key,value) | get 返回 `[found,value]`，put 返回可独立使用的结果；完整输入底本和键决定结果 |
| 参数 | rt_arg_count(), rt_arg(index) | 启动命令行参数 |
| 文件 | rt_read(path), rt_write(path,text), rt_json_write(path,value) | 严格 UTF-8 读取、文本写入、流式新 JSON 文件 |
| JSON | rt_json_parse(text), rt_json_stringify(value) | 数据交换解析与序列化 |
| 控制台/失败 | rt_print(value), rt_fail(message) | 基本值打印并返回原值、抛出明确错误 |

`rt_number(text)` 对精确文本 `0`、`1`、`-1` 复用不可变有限数值，其余仍按 invariant Float 解析并检查有限性；负零及错误顺序保持原合同。

`rt_is_number(text)` 检查文本是否可解析为有限浮点数，传非文本会报 Expected Text；它不承担运行值类型判断。`rt_is_numeric_value(value)` 无隐式转换：有限 double 和有效 Rational 返回 true，数字文本、普通文本、Bool、列表/记录、非有限 double 返回 false。主体数量、依赖收集、侵蚀暴露以及表达策略权重使用后者。精确有理数权重直接按数值检查，不经 JSON 文本重解析。

调试投影对记录形状的 action 查询字段；普通列表用 rt_json_stringify 作为标识，原子值用 rt_show，false 保留空标识。投影只读，不能产生行动或改变人物资格。

rt_memo 的输入底本在修改时失效，结果返回独立副本，弱输入表不延长输入生命；以完整 key 首个文本项分区，各分区按插入顺序保留至多 512 键。它仅用于完整输入确定的纯计算，不能缓存事件发布、结算、采信或执行过程。rt_same_value 直接读取既有子项，保持列表顺序与精确数/浮点数相等规则，不以不同哈希否定值相等。记录索引只是查找优化，重复键、不规则记录和错误行为保持原合同。

## 8. 保留的 S 表达式启动语法

```text
Program = (defn Name (Name*) Expr)* (main Expr)
Expr = Number | String | true | false | Name
     | (Function Expr*) | (if Expr Expr Expr)
     | (let (Name Expr ...) Expr) | (set Name Expr)
     | (do Expr+) | (while Expr Expr)
```

该语法用于编译器自举及兼容构造，支持前向调用、递归和互递归；let 从左到右初始化，set 修改已绑定局部，if 仅求值选中分支，while 返回 false。启动标识符由现行 identifier/letter 检查，支持平台字母类别、下划线和数字后缀；不同于表层扫描器，这条路径不执行同样的 NFC token 归一化与组合标记处理。注释为分号到行末，文本可包含 Unicode。rt_ 前缀表示平台入口；AST 使用 kind/payload/offset，offset 为 UTF-16 位置。它与表层语法共用生成后端和 Runtime，不是另一套世界执行器。

## 9. 源码分类索引

下列签名从现役合并文件列出；原文件路径映射见任务的 path-map.json。声明接口与声明体保持原文，文件顺序不表达运行顺序。

### [core/00-data.dv](00-data.dv)

```dv
fn dv_pair(a, b):
fn dv_single(a):
fn dv_list3(a, b, c):
fn dv_and(a, b):
fn dv_or(a, b):
fn dv_require(condition, message):
fn dv_clone(value):
fn dv_same(a, b):
fn dv_append(items, value):
fn dv_has(items, value):
fn dv_find(record, key, fallback):
fn dv_has_key(record, key):
fn dv_record_like(value):
fn dv_get(record, key):
fn dv_with(record, key, value):
fn dv_m2(k1, v1, k2, v2):
fn dv_without(record, key):
fn dv_m3(k1, v1, k2, v2, k3, v3):
fn dv_m4(k1, v1, k2, v2, k3, v3, k4, v4):
fn dv_unit(x):
fn dv_unknown():
fn dv_min(a, b):
fn dv_max(a, b):
fn dv_cell(value, confidence, time, claims):
fn dv_entity_ref(id):
fn dv_ref(object_id, facet, field):
fn dv_scoped_ref(subject, object_id, facet, field):
fn dv_same_local_ref(subject, left, right):
fn dv_world(id, domain, subject):
fn dv_domain(world, domain):
fn dv_object(id, kind, facets):
fn dv_add_object(world, object):
fn dv_read(world, ref):
fn dv_store(world, ref, cell):
fn dv_slice(world, object_id, facet, fields):
fn dv_field(value):
fn dv_uncontested(cell):
fn dv_door(id, from, to, closed, held, inside, outside, automatic, gap):
fn dv_channel(id, from, to, medium, enabled, gain, gate):
fn dv_with_many(record, updates):
fn dv_with_path(record, keys, depth, value):
fn dv_stage_store_owned(owned, ref, cell):
fn dv_true():
fn dv_fact(ref, expected):
fn dv_at_least(ref, minimum):
fn dv_all(conditions):
fn dv_not(condition):
fn dv_truth(world, proposition):
fn dv_truth_uncached(world, proposition):
fn dv_assign(ref, value):
fn dv_increment(ref, amount):
fn dv_axiom(name, version, premise, effects):
fn dv_instance(id, law):
fn dv_checked_adoption(domain, adoptions, law, mode, confidence, basis):
fn dv_adopt(world, law, mode, confidence, basis):
fn dv_adopt_into(domain, owned_adoptions, law, mode, confidence, basis):
fn dv_finish_owned_adoptions(world, owned_adoptions):
fn dv_suspend(world, name):
fn dv_reconsider(world, law, mode, confidence, basis):
fn dv_premise_confidence(world, proposition):
fn dv_result(status, world, changes, basis):
fn dv_stage(world, effects, time, basis, confidence):
fn dv_infer_stage(world, effects, time, basis, confidence, law_name, event_id):
fn dv_apply_law(world, name, mode, event_id, time, expected_revision):
fn dv_execute(physical, name, event_id, time, expected_revision):
fn dv_predict(mind, name, event_id, time, expected_revision):
fn dv_infer(mind, name, event_id, time, expected_revision):
fn dv_norm(mind, name):
axiom door_open(door):
axiom door_self_close(door):
axiom door_latch(door):
axiom door_seal(door):
axiom channel_reconfigure(channel, gain):
axiom channel_flow(channel):
```

### [core/20-observation.dv](20-observation.dv)

```dv
fn dv_binding(physical, local, subject):
fn dv_local(bindings, physical, subject):
fn dv_validate_bindings(bindings, subject):
fn dv_project_value(value, bindings, subject):
fn dv_observation(id, subject, ref, value, source, origin, time, trust, dependency):
fn dv_perceive(physical, mind, bindings, grants, event_id, time):
fn dv_perceive_changes(receipt, mind, bindings, grants):
fn dv_observation_profile(source_weights, threshold):
fn dv_weight(observation, profile):
fn dv_replace_claim(claims, observation):
fn dv_aggregate(claims, profile, time):
fn dv_update_mind(mind, observations, expected_revision, profile):
fn dv_segment(content, strength, band):
fn dv_signal(location, medium, segments):
fn dv_filter_signal(segments, gain, threshold, bands):
fn dv_transport(world, path, signal, threshold):
fn dv_hear(delivery, subject, local_ref, perceived_origin, event_id, time):
fn dv_transport_theory(law):
fn dv_transmit(world, law_name, path, signal, threshold):
```

### [core/40-value.dv](40-value.dv)

```dv
fn dv_criterion(id, ref, mode, target, weight):
fn dv_measure(value, criterion):
fn dv_expected_measure(cell, criterion):
fn dv_value(world, criteria, subjective):
fn dv_value_subjective(mind, criteria):
fn dv_value_objective(physical, criteria):
fn dv_value_change(before, after, criteria):
fn dv_request(id, requester, self):
fn dv_feature_refs(request):
fn dv_appraise_request(mind, request):
fn dv_dot(features, weights):
fn dv_clamp_signed(value):
fn dv_expression(appraisal, expression_policy):
fn dv_no_change_prediction(mind):
fn dv_response_option(id, intent, feasible, prediction, effort, feature_weights):
fn dv_validate_expression_policy(expression_policy):
fn dv_person_policy(subject, values, effort_cost, uncertainty_cost, expression_policy):
fn dv_accept_policy(mind, id, policy, basis):
fn dv_respond_adopted(mind, request, options, policy_id):
fn dv_response_score(mind, option, appraisal, policy):
fn dv_respond(mind, request, options, policy):
fn dv_relax(current, baseline, retention):
fn dv_affect_proposal(mind, request, policy):
fn dv_acceptance_classify(degree, levels):
acceptance dv_threshold_acceptance(value, threshold):
fn dv_acceptance_transition(previous, current, source):
fn dv_balance_dot(a, b):
fn dv_balance_extent(variables):
fn dv_balance_candidate(index, goal, variables, laws, limits, prices):
fn dv_imprint_solve(capability, goal, variables, laws, limits, prices):
fn dv_balance_same_numbers(left, right):
fn dv_balance_same_witness(left, right):
fn dv_imprint_check(result, goal, variables, laws, limits, prices):
fn dv_imprint_commit(state, result, goal, variables, laws, limits, prices, source):
fn dv_imprint_prepare(mind, refs, ledger, work_remaining, levels, search_budget, capability):
fn dv_imprint_apply(mind, physical, ledger, state, result, goal, variables, laws, prices, event):
```

### [core/70-reasoning-cost.dv](70-reasoning-cost.dv)

```dv
fn dv_proof_step(id, kind, conclusion, parents, law):
fn dv_cost_policy(step, evidence, assumption, conflict, reference, maximum, allow_assumptions, budget):
fn dv_rule_concludes(law, conclusion):
fn dv_match_premises(premise, supplied):
fn dv_check_step(world, step, checked, policy):
fn dv_judge_conclusion(world, conclusion, proof, policy):
fn dv_checkpoint(world, time):
fn dv_session(worlds, time, time_unit):
fn dv_session_commit(session, expected_revision, world, time):
fn dv_session_advance(session, time):
fn dv_active_worldview(world):
fn dv_inspect_content(world, command, object_id, facet):
fn dv_inspect_at(session, subject, time, command, object_id, facet):
fn dv_inspector_main():
fn dv_action_cost_vector(time, computation, stamina):
fn dv_action_cost_validate(v):
fn dv_action_cost_le(a, b):
fn dv_action_cost_add(a, b):
fn dv_action_cost_sub(a, b):
fn dv_action_cost_bounds(minimum, maximum):
fn dv_action_cost_ledger(id, balances):
fn dv_action_cost_quote(ledger, world, action, event, bounds):
fn dv_action_cost_admit(ledger, world, quote):
fn dv_action_cost_actual(receipt, actual):
fn dv_action_cost_settle(ledger, receipt, actual, outcome):
fn dv_action_cost_completion_time(physical, receipt, actual):
fn dv_action_cost_clock_commit(physical, receipt, actual, status):
fn dv_action_cost_execution_metadata(execution, receipt, actual):
fn dv_action_cost_execute(ledger, physical, quote, actual):
fn dv_action_cost_finish(ledger, physical, quote, actual, outcome):
```

### [core/95-model-argument.dv](95-model-argument.dv)

```dv
fn dv_explanation_model_complete(model, status, revision):
fn dv_explanation_observation_sources(mind, comparison, sources):
fn dv_explanation_trace_changes(step):
fn dv_explanation_slot(model, slot):
fn dv_explanation_model_inputs(mind, model):
fn dv_explanation_model_current(mind, model):
fn dv_accept_explanation(mind, model, competitors, slots, sources, policy, budget, time, id):
fn dv_explanation_merge(a, b):
fn dv_debug_event(event):
fn dv_debug_events(events, start, config):
```

### [verification/session/10-subjective.dv](../verification/session/10-subjective.dv)

```dv
fn dvs_event(events, kind, actor, time, parents, data):
fn dvs_find_event_by_id(events, id):
fn dvs_cognitive_parents(actor, parents):
fn dvs_reason_cell(cell):
fn dvs_reason_value(cell):
fn dvs_reason_premises(mind, proposition):
fn dvs_reason_signature(mind, adoption):
fn dvs_reason_changes(changes):
fn dvs_reason_monotone_satisfied(mind, effects):
fn dvs_reason_run(actor, events, time, parent):
fn dvs_cached_model(actor, hypothesis):
fn dvs_working_explanation(actor, hypothesis):
fn dvs_hypotheses(actor, events, time, parent):
fn dvs_choose(actor, bindings, costs):
fn dvs_honor_planned_followup(actor, choice):
fn dvs_ready(actor, events, time, parent):
fn dvs_reason(actor, events, time, parent):
fn dvs_event_prepared(events, kind, actor, time, parents, data):
fn dvs_reason_input(actor):
fn dvs_prepare_cognition(actor, events, time, parent, chapter):
fn dvp_choice_groups(actions):
fn dvp_personal_ready(mind, choice):
fn dvp_personal_impact(choice, value):
fn dvp_personal_better(order, left, right):
fn dvp_received_speech_matches(receipts, requirement):
fn dvp_owner_requirements(actor, action, mind):
fn dvp_eligible(actor, action, mind, relevant):
fn dvp_eligible_after_base(actor, action, mind, relevant):
fn dvp_normative_ready(actor, action, mind):
fn dvp_owned_quantity(actor, mind, declaration):
fn dvp_resonance_utility(actor, action, mind):
fn dvp_owner_requirement_refs(action):
fn dvs_actor_can_act(actor):
fn dvs_cognitive_seed_next(seed):
fn dvs_initialize_cognition(actor, seed, ordinal):
fn dvs_erosion_update(actor, physical, time):
fn dvs_receive_public_claim(listener, speech, speaker, source, parse_event):
fn dvs_complete_cognitive_effects(actor, action, success, event, time):
fn dvs_receive_player_influence(listener, speech, speaker, source, parse_event):
fn dvs_complete_player_influences(actor, action, before, after, success, event, time):
fn dvs_pending_request(actor, action):
fn dvs_receive_request(actor, speech, parse_event, source, speaker):
fn dvs_finish_reply(actor, action):
fn dvs_record_request(actor, action):
fn dvs_request_inputs(mind, proposition):
fn dvs_request_context(actor, action, target):
fn dvs_visible_context(actor, physical, actors):
fn dvs_notice_peers(actor, physical, actors, bindings, events, time, parent):
fn dvs_visible_action_grants(actors, speaker_index, action, before, physical, bindings, events, time, parent, execution_id):
```

### [verification/session/15-action-planning.dv](../verification/session/15-action-planning.dv)

```dv
fn dvp_memo_input(actor, costs):
fn dvp_question(actor):
fn dvp_topic(actor, item):
fn dvp_add_ref(refs, ref):
fn dvp_merge_refs(refs, extra):
fn dvp_unmet_refs(mind, proposition, include_known):
fn dvp_dependency_add(graph, ref, inputs):
fn dvp_dependency_topology(mind, methods):
fn dvp_dependencies(mind, seeds, budget, methods):
fn dvp_goal_refs(actor):
fn dvp_probe_refs(actor):
fn dvp_scope(actor, action, bindings):
fn dvp_answer_reward(actor, action, mind):
fn dvp_input_signature(actor, action, refs):
fn dvp_answer_relevant(actor, action):
fn dvp_answer_attempt_allowed(prev_sig, cur_sig):
fn dvp_inspection_signature(actor, action, mind):
fn dvp_eligible_base(actor, action, mind, relevant):
fn dvp_source_supported(cell, source):
fn dvp_source_supported_since(cell, source, minimum_time):
fn dvp_needs_evidence(actor, ref):
fn dvp_action_value(actor, action, bindings, relevant, probes):
fn dvp_value_context(actor):
fn dvp_context_communication(action, goals):
fn dvp_action_value_context(actor, action, bindings, relevant, probes, context):
fn dvp_opportunities(actor, action):
fn dvp_initial_access(actor):
fn dvp_join_items(list):
fn dvp_state_key(room, tasks):
fn dvp_extract_required_room(prop, loc_ref):
fn dvp_node_key(node):
fn dvp_frontier_put(frontier, node):
fn dvp_dependency_methods(actor):
fn dvp_cached_dependencies(actor, seeds, budget):
fn dvp_plan(actor, bindings, costs):
fn dvp_find_action(actions, id):
fn dvp_actor_view(actor, mind):
fn dvp_dominated(nodes, candidate):
fn dvp_communication_progress(actor, action):
fn dvp_satisfied_goals(actor, mind, previous):
fn dvp_achievement_value(actor, action):
fn dvp_achievement_pending(actor, action, pending):
fn dvp_refresh_current_evidence(actor, goals):
fn dvp_forecast(mind, name):
fn dvp_room_actions(actions, room, location_ref):
fn dvp_inspection_freshness(actor, mind):
```

### [verification/session/20-speech.dv](../verification/session/20-speech.dv)

```dv
fn dvs_speech_cognitive_parents(parents, previous):
fn dvs_speech_ast_nodes(ast):
fn dvs_speech_map(mind, proposition, bindings):
fn dvs_speech_truth(mind, speech):
fn dvs_speech_presuppositions(mind, speech):
fn dvs_emission_location(speaker, speech, physical):
fn dvs_speech_hearing(speaker, listener, speech, physical):
fn dvs_speech_observations(mind, proposition, source, origin, time, trust):
fn dvs_speaker_trust(policy, speaker):
fn dvs_speech_proposal_decision(mind, policy, speech, trust):
fn dvs_speech_received_observations(mind, parsed, source, speaker, time, decision):
fn dvs_speech_candidates(policy, speech):
mapping dvs_listener_mapping(mind, policy, bindings):
fn dvs_speech_interpret(mind, policy, bindings, candidate):
fn dvs_speech_model_interpretations(mind, policy, speech, bindings):
fn dvs_speech_model_prediction(model, speech, speaker, source, time):
fn dvs_speech_rule_units(rule):
fn dvs_speech_prediction_units(model, speech):
fn dvs_speech_goals(actor):
fn dvs_speech_choice_signature(actor, action, goals, time):
fn dvs_speech_goal_value(goals, listener, prediction):
fn dvs_prepare_speech_choices(actor, events, time, parent):
fn dvs_proposal_reassess(actor, decision, events, time, parent):
fn dvs_receive_norm(actor, norm, source, parse_event, events, time):
fn dvs_complete_norm(actor, action, applied, events, time, execution_event, observation_event):
fn dvs_receive_scheduled_speech(listener, message, events, time):
fn dvs_drain_received_speech(actor, events, time):
fn dvs_speech(actors, speaker_index, action, events, time, parent, physical):
fn dvs_speech_owned(actors, speaker_index, action, events, time, parent, physical):
fn dvs_pragmatics_reconsider(actor, events, time, parent):
```

### [verification/session/25-simulation.dv](../verification/session/25-simulation.dv)

```dv
fn dvs_sim_refs(proposition):
fn dvs_sim_plan(hypothesis):
fn dvs_sim_plan_build(hypothesis):
fn dvs_sim_withhold(world, effects):
fn dvs_sim_feature(feature):
fn dvs_sim_mask_plan(plan):
fn dvs_sim_mask(mind, source_plan, start):
fn dvs_sim_inputs(world, proposition):
fn dvs_sim_snapshot(snapshots, final, covered, feature):
fn dvs_sim_prediction(feature, snapshot, start):
fn dvs_sim_compare(mind, prediction):
fn dvs_sim_comparisons(mind, predictions, budget):
fn dvs_sim_work_budget(actor):
fn dvs_model_execute(actor, hypothesis, events, time, parent):
fn dvs_model_recheck(actor, model):
fn dvs_sim_response_unknown(world, remaining, reason):
fn dvs_sim_response(actor, world, stage, time, remaining, confidence):
fn dvs_sim_mask_response_option(world, model, option_id):
fn dvs_sim_mask_response_stage(actor, world, stage):
fn dvs_sim_mask_response(actor, world, plan):
fn dvs_sim_response_bridge(actor, world, detail, time, remaining, confidence):
fn dvs_sim_gated_response(actor, world, step, time, remaining, confidence):
fn dvs_factor_proof_result(valid, remaining, confidence, steps, roots):
fn dvs_factor_merge(left, right):
fn dvs_factor_law_allowed(allowed, law):
fn dvs_factor_roots(actor, mind, ref, events, cutoff, remaining, allowed):
fn dvs_factor_refs_proof(actor, mind, refs, events, cutoff, remaining, allowed):
fn dvs_factor_inference(actor, mind, ref, cell, events, index, data, event, remaining, allowed):
fn dvs_factor_domain(domains, id):
fn dvs_factor_schema(domains, constraint):
fn dvs_factor_scope_proof(domains, fixed, proof):
fn dvs_factor_covers(certificate, assignments):
fn dvs_factor_paid(actor, events):
fn dvs_factor_prune(actor, events, time, parent):
fn dvs_factor_certificate_parents(certificate, parents):
fn dvs_factor_certificate_bound(actor, certificate, events):
fn dvs_factor_certificate_current(actor, certificate, events):
fn dvs_factor_assignment_complete(domains, assignments):
fn dvs_factor_assignment_key(domains, assignments):
fn dvs_factor_catalog_complete(domains, hypotheses):
fn dvs_sim_rule(predicted, covered, step, mind, confidence, start, i, event_count, selected_index):
fn dvs_sim_equal_guard(proposition, ref):
fn dvs_sim_dispatch(mind, predicted, step):
fn dvs_slice_intersects(left, right):
fn dvs_slice_law_io(mind, name):
fn dvs_slice_step_io(actor, step):
fn dvs_slice_feature(hypothesis, feature, plan, plan_io):
fn dvs_model(actor, hypothesis, events, time, parent):
fn dvs_argument_sources(events, actor):
fn dvs_capture_public_topics(actor, events, time, parent):
fn dvs_topic_was_heard(actor, topic):
fn dvs_argument_valid(actor):
fn dvs_argument_inputs_current(actor):
fn dvs_argument_revoke(actor, events, time, parent):
fn dvs_factor_coverage(actor, hypothesis, events):
fn dvs_factor_covered(candidate, certificates):
fn dvs_factor_argument(result, coverage):
fn dvs_arguments(actor, events, time, parent):
fn dvs_argument_action(actor, action):
fn dvs_materialize_arguments(actor):
fn dvs_update_support_events(actor, events):
fn dvs_subject_support_events(events, subject):
fn dvs_support_ref_key(subject, ref):
fn dvs_support_event_index(events, subject):
fn dvs_support_ref_events(events, index, subject, ref):
fn dvs_support_parents(mind, refs, events, subject_or_actor):
fn dvs_selection_parents(actor, action, choice, events, parent):
fn dvs_model_parent_ids(actor, parent, prior):
fn dvs_public_grant_parents(events, parent, subject):
fn dvs_physical_proposition(proposition, bindings, subject):
fn dvs_refresh_derivations(actor, events, time, parent):
fn dvs_model_observed_inputs(model):
fn dvs_public_model_paid(events, id, subject):
fn dvs_public_argument_valid(action, rule, events, subject):
fn dvs_factor_candidate_rejected(actor, candidate):
fn dvs_capture_heard_utterances(actor, events):
fn dvs_topic_origin_speakers(events, sources):
fn dvs_argument_reassessment_sources(actor, action, argument):
fn dvs_public_literal_source(events, parent, subject):
```

### [verification/session/30-runner.dv](../verification/session/30-runner.dv)

```dv
fn dvs_authority_speech_allowed(action, physical, subject):
fn dvs_acceptance_law_ready(physical, name):
fn dvs_answer(rules, action, physical, mind, bindings, events):
fn dvs_run(story, capability):
fn dvs_session_open(story, capability):
fn dvs_session_active(state):
fn dvs_scheduled_count(actors):
fn dvs_dispatch_due(state, index):
fn dvs_schedule(state):
fn dvs_debug_phase(story, actor, time, phase):
fn dvs_session_prepare(state):
fn dvs_prepare_actor(state):
fn dvs_session_complete(state, receipt):
fn dvs_session_close(state):
fn dvs_replace(items, index, value):
fn dvs_main():
fn dvs_public_grants(actors, speaker_index, action, physical, bindings, events, time, parent, execution_id):
fn dvs_observation_source(observations, action):
fn dvs_observation_profile(actor):
fn dvs_owned_state_update(state, updates):
fn dvs_environment(physical, story, events, parent):
fn dvs_environment_boundary(condition, clock, now, due):
fn dvs_environment_next_time(world, story, due):
fn dvs_present_replace(text, token, value):
fn dvs_present_lines(state, viewer, presentation):
fn dvs_session_intent(state):
fn dvs_session_presentation(state, viewer):
fn dvs_set_player(state, actor):
fn dvs_control_state(state, actors, events, cursor, player, selection, waiting):
fn dvs_player_options(actor):
fn dvs_select_player(state, id):
fn dvs_player_choice(actor, id):
```

## 10. 运行与验收范围

`dv_with_many` 和 `dv_with_path` 在返回可变记录时只复制更新路径；未变化的兄弟分支先保存为不可变快照，后续从该可变输出访问时由 Runtime 建立独立可变分支。`rt_snapshot` 的弱引用池按桶渐进清理，结构相等仍逐项核对。可选 `run_debug.py --profile` 对诊断生成程序的 DV 函数作嵌套计时，按函数及工作类型报告调用、独占/包含总耗时与平均耗时；平台 ABI 时间计入直接 DV 调用者。该诊断不测 Godot 画面、碰撞或输入。

构建和编辑器入口见 [语言总览](../README.md)、[编译器说明](../compiler/README.md) 与 [Session](../Session/README.md)。生成 C# 由相应工具产生，不手工修改生成物。一级逻辑输出入口为 `python language_core/tools/run_debug.py --out artifacts/partial-bootstrap-core/<新目录>`，输出 formal-model 正式会话的原始轨迹、调试投影与玩家可见正文；是否执行及次数遵循本轮用户授权。

构建/编译只能说明必要工程事实；formal-model 不含 Godot 碰撞、输入和画面。停止于行动上限、时间域、工程超时与实际剧情终局分别报告，未覆盖分支保留未知。项目禁止流程回归与以断言数、通过率或汇总计数代替实际验收。本文为源码参考，未声称完整两章完成、同条件提速或全部语言设计已经实现。

调试入口的 `debug_end.stop_evidence` 只读记录诊断行动窗口、会话动作上限、世界时域、时间不足拒绝、无排队/可执行动作及各主体剩余行动/认知额度。多个条件可以同时成立；narrative_completion 固定标为 not-inferred，须用实际关键节点证据另行判断。超时或初始化异常未产生 debug_end 时，不能补造停止状态。debug 投影只在 dv_record_like 成立时查询 action 字段，普通列表或原子动作保留可打印标识。

### 会话上下文与未来机会维护（2026-10-16）

动作可声明 `suppress_after_received_speech: {speaker, action_id}`，仅在本人已有非空来源与理解事件的匹配实际收闻后排除该动作；`response_once: true` 在本人成功 Complete 该动作后排除后续同动作回复。默认请求与专业回复仍保留原重问合同。这些门槛由共同主体资格入口核验，预测和候选不会制造完成或收闻。

未来等待仅从有来源、未完成且除正向时钟下界外其他条件在本人当前认识中成立的目标产生。第二章天气引用及其直接否定项在这一非时钟筛选中视为可随时间变化，不阻止夜间等待未来白天机会；实际行动仍核对当时天气。其他否定时钟分支与前提保留原判定；等待保留原费用和估值，不能预置未来成功。静态证物不因会话时间或无关信息刷新。源码：`verification/session/15-action-planning.dv`、`10-subjective.dv`、`verification/scenarios/chapter1/opening.dv`。

已知机会等待不参与依赖图构建，只在无其它当前可执行动作时于根层评估，不作为预测后继再次展开；根层已存在正向可执行动作时以 `current-affordance-root` 收束。首夜起居厅八人在 900 分钟前等待候选采用 30/60 分钟上界并收短到下一活动起点；第二章仅对尚未完成首夜跨章观测的 `selma`/`mora` 保留首夜等待候选，其余主体仅在章节切换、入厅状态或案件开启状态变化时刷新第二章目标。同场可见上下文仅记录同伴位置，仅在新增到场或位置变化时触发感知；主体在上一轮决策停止且收闻与同场检查后 `mind.revision`、`mind.time` 与 `chapter` 均未变化时跳过重复准备。第二章天气检视包含实际 `severe_air`，连廊换气与开启目标归属 `quer` 并核对本人获知的补风、密封和空气条件。相关生产内容见 `verification/scenarios/chapter2/content.dv`、`dialogue.dv`、`opening.dv` 与 `game/entry.dv`。这些是现行工程规则，实际章节达成须另据新会话记录判断。

病人现场检视的本人前提与执行前提均要求莫兰在诊所，避免已知其离开后继续发起注定被拒的检视。


2026-10-18 会话计算维护：`30-runner.dv` 的 `dvs_environment_boundary` 递归读取原 premise 中的时钟 `at_least`（含 not/all），保留原一分钟采样格及最终到期点。时钟相等或不可分析的时钟谓词回退一分钟；当前可产生新变化、重复增量或过程级联时继续一分钟，不跳过这些效果。`dvs_environment_next_time` 只调度 physical，不生成主体认识。内容进程可声明 `once: true`，仅在实际 applied 后 suspend；可重复进程保持活动。未接入开关输入的房间规律保留定义与采纳，暂不注册为周期进程。

`dvp_achievement_pending` 以本人采纳法则的实际写引用与待达成命题引用相交后才预测；无交集不能直接改变该命题。`dvp_forecast` 按完整节点 mind 和法则缓存纯预测，供目标评分、接近机会和后继展开复用。内部规划预测省略继承的 history/seen，保留单元来源及实际公共 dv_predict 合同；私有分支通过路径循环与状态去重控制重复展开。真实 dv_apply_law/dv_update_mind 在独立 stage 上追加完整 seen/history；不修改原态。规划备忘的 mind 省略 history/seen，保留 revision、objects、adoptions 和其余主体输入。

无活动话语目标时 `dvs_prepare_speech_choices` 原样返回主体；话语预选签名包含选中听者的完整模型、话语、目标、说话者字面真值及预测观察相对模型旧字段的时间门；说话者无关认知不入键。`dvs_argument_sources(events, actor)` 先增量补齐 support_events，再从其中展开本人 observation/listener_parse，保留顺序和来源。`c1n_index_goal_window` 缓存目标条件的正向时间下界和非时钟条件，使用时核对原条件未变化；不降低刷新时刻精度。章节检视范围与角色调查目标由内容声明，合并天气候选后保留原地点可见条件。


2026-10-18 集中补齐：正式prepare先用唯一调度器选主体，`dvs_prepare_actor`承接原prepare的本人认知、报价及排队实现；campaign只在此阶段刷新当前主体内容。宿主人工选择已有主体时不重新调度，pending与等待回执不重复决策。普通session入口仍按原prepare→complete调用。静态动作切片`dvp_room_actions`按动作目录/地点/位置引用缓存，保留源顺序，动态资格逐项重查；未知或争议地点不裁剪。私有规划与话语预测不继承审计日志，真实世界和公共预测仍保留完整来源及去重。

环境边界超过当前动作due时直接返回due，不向遥远阈值逐分钟计步。每日昼夜声明各标记once，实际执行后休眠；流式调试投影保留process、revision_before/after与suspended。目标时间元数据按原条件纯值缓存，当前主体刷新保留精确本人时刻。等待时长清单增加120分钟，费用、物理和预测规律均从同一清单安装。连廊单字段观察目标驱动原四字段实际检视；完整换气前提不删减。上述内容与搜索条件属于本轮实现，运行效果由新会话另证。

检视签名在同一mind节点及完整目标声明下共用`dvp_inspection_freshness`索引，证据下界刷新与可用性只计算一次；每个动作仍读取自己的完整grants与前提输入。索引不改变本人来源或证据新鲜度规则。


2026-10-18 因果接线与预算分离：普通聆听先从实际解析出的陈述生成证言，不能按缺少特殊话语标签判作闲聊并抹去观察；仅无可写观察时省略认识更新，仍保留listener_parse及真实收闻。专业回复必须由实际发言、传播、解析与原采信门槛取得，不能在询问时直接赠送答案。

依赖拓扑仅由实际active/infer采纳和静态方法scope/premise/owner_refs构建并缓存；闭包查询继续以seeds及dependency_budget为键，窗口改变会改变seeds，不能复用错误闭包。原plan_budget现在只限制行动树节点；内容声明dependency_budget=320独立限制闭包，缓存命中分别记零实际工作。这是计算上限合同修订，不是保持原总工作上限的等价优化。C2可声明positive_move_root_stop：根层比较完全部候选后，具有本人achieve收益的移动也允许按现有根层停止策略收束，未宣称全局最优。

C2导航意图从本人已知位置、已知开启的门和本人已采纳移动方法找有来源的下一站；尚未完成任务才产生中继/终点意图，原origin条件随实际移动失效，位置、通路、采纳和完成字段变化触发刷新。路径只是意图依据，真实每一步仍逐项核验动作前提、规范、费用与Complete；未知门不用于路径，不预写调查或章节达成。

认知快路仅复用一次完整处理前后输入相同的固定点，且当次时间、章节、mind、政策、模型、解释、支持来源和同处上下文均未变化。先处理实际收闻与现场感知，迟发生的论证/模型更新不能标成已稳定。重复零步reason_stop及相同why_not_ready可不另发事件，实际推知/观察/模型/选择/执行不裁剪。诊断缓存和工作累计不进入行动规划备忘键。

调试投影dv_debug_event返回冻结只读快照；默认每步输出，显式分批模式由game/debug-run.dv积累实际玩家呈现并按原事件游标刷新。分批模式要求在线DAG逐条保留完整原始事件，stdout只代表已刷新前缀；超时可能保留尚未投影的DAG尾部，不能声称正文完整。无模板行直接返回空行列表；phase_progress缺省关闭。分批输出不改变prepare/complete、预算或事件来源。

#### 批量投影与真实阶段恢复接口（2026-10-18）

`run_debug.py --raw-format dag --projection-every 32 --timeout 240 --out <新目录>` 每32次外层真实Complete后刷新积累的玩家呈现和事件投影，正常循环结束补齐剩余。默认`--projection-every 1`保留逐步投影；大于1只允许在线DAG，原`dvs_event`逐事件提交保持不变，Core原有的小型执行回执stdout也继续保留。`stdout.jsonl`及正文仅是已刷新范围。`result.json`分列`stdout_projection_coverage`、`raw_committed_events_not_yet_projected`和中断后未知的完成步数；未刷新尾不补猜为完整正文，原始DAG继续作为完整已提交事件来源。

`stage-day3`、`stage-day5`分别在正式绝对时钟跨2880、5760后的首次真实Complete结束时提交，不按运行开始时间重置日期；跨界前尚未完成的计算不生成checkpoint。记录含完整state、总调试steps、实际executed、无pending状态、配置、源码指纹，以及相互关联的host_completion/action_execution事件ID、原行动token和最终实际事件。恢复校验原始DAG帧链、提交位置、事件先后/主体/父链/行动token/执行状态、状态时钟及配置，不能仅凭after_complete标记建立资格。未发生边界完成时没有对应checkpoint。

DAG自身保留精确有理数节点；默认只登记已提交checkpoint的位置，不自动展开可能很大的Save副本。需要恢复时显式运行：

```powershell
python language_core/tools/debug_checkpoints.py <原始DAG或.partial> --checkpoint stage-day3 --out <新checkpoint目录>
python language_core/tools/resume_debug.py --checkpoint <新checkpoint目录>/manifest.json --out <独立新运行目录> --timeout 240 --projection-every 32
```

第一条只读导出已校验提交前缀中的实际checkpoint，保留`$dv_rational` Save标签；第二条才显式启动恢复。缺失、未提交、状态不符、来源文件改变、Save与原提交值不符或源码/配置不一致均拒绝；中断后的有效checkpoint可独立于未提交尾使用，原尾不改写。恢复从保存state直接进入同一个`dvg_debug_loop`的prepare/complete，不重新开局、不预置前两日、不注入结果。记录原时间及步骤作为恢复起点，新增DAG的initial保存继承状态、resume-origin保留原来源；新增事件与继承事件分别统计。显式传入冲突的角色配置被拒绝，诊断投影批量不改人物配置。源码路径与指纹当前按原工作区严格核对，不自动迁移或建立新基线。

C#工具加载器`tools/DebugCheckpointResume.cs`使用与Session Restore相同的512层标签解析，精确数还原为Rational，文本分数字面仍为Text；普通Trace字符串编码不能充当Save。`check_debug_structure.py --out <新工程目录>`仅生成main=false程序并选择中性codec入口，检查标签及通用DAG值，既不调用正式会话也不建立阶段状态；该工程检查与唯一授权的新正式会话分列，不自动续跑。


## 连续优化循环与局部复用（2026-10-18）

用户授权每轮集中完成已识别问题处理、接线与静态审读后，冻结源码运行一次最多180秒的一级正式会话；达到7200分钟即停止，否则最多四轮。每轮新增证据并独立审查，保留失败、插桩开销与未覆盖范围，提交并推送；不在改动之间穿插会话。

`dv_with_many`改由通用容器ABI `rt_record_with_many(record,updates)`完成，保持有序记录、更新全部重复键、更新表末值优先、空更新返回原记录及分支独立复制。算法仍只处理容器，不含剧情或世界执行。编译器arity与Core参考同步维护。

整个人物的认知缓存由局部推知固定点键替代：输入包含infer采纳、所有前提/输出字段、历史推导引用、来源单元（仅沿原推知语义剥离time）、推导、签名与剩余推理预算；只有推知前后完全不变且未阻塞时保存。时间条件所读时钟字段仍在键中；语用、假说、准备、论证与话语链继续每次执行，`reason_reused`与`cognition_reused`分列。新增效果读依赖时须扩展此键。

无参静态内容声明可以使用纯值备忘，不能缓存读取会话、时间、世界或人物的构造器。支持性陈述可声明answer_form=supporting-statement，不取得完整答辩奖励；权威判答和人工候选不因评分调整而放宽。询问不携带答复正文，已知其所问命题后不重复发问。姓名只从本人真实介绍收据显示。

诊断插桩每30秒原子保存已完成调用的累计耗时，超时保留最后采样；采样覆盖时长与全程时长分列，插桩运行不与原生运行作同条件吞吐比较。诊断采样只改工具输出，绝不决定人物行为。

第2轮：批量更新先冻结updates再枚举，避免record_find的惰性展开改写枚举器版本。通用二元ABI rt_has_value实现结构值成员查询，dv_has转发，沿rt_same_value的精确数/浮点比较，不展开只读子树、不改变集合或游戏规则。中性验证补充从快照恢复的惰性更新表、冻结更新表和嵌套数值成员判断；第一轮初始化异常独立保留。

第3轮：dv_local按完整bindings值建立分主体索引，重复物理映射仍在查询该身份时报告歧义；dv_validate_bindings仅缓存原顺序检查全部完成后的成功，修改bindings使缓存失效。dvs_sim_plan只缓存timeline/predict_laws的纯结构编译，不缓存模型成功或付费结果。答辩评分不再被通用话语默认进度覆盖，支持性陈述保持零完整答辩奖励；同一准备失败原因和同一模型不因无关revision变化重复发why_not_ready，实际来源事件仍保留。正式测试恢复无插桩，不能与前轮插桩作同条件速度比。

一级正文导出显式识别既有bodily-flash为触觉闪回；规范表态用换行附本人所知条文，不将全文嵌入重复引号。此处仅修复呈现分类与分段。

第4轮：调查导航一次只为一个尚未完成且处于本人已知时窗的任务生成到达/中继意图，优先当前地点可执行任务，随后可执行的异地任务，最后保留等待前提的首项任务；完整实质目标仍全部参加共同规划。导航缓存包括所有相关完成、要求、位置、通路与窗口真值，避免到达多个房间的短暂奖励相互往返。现场调查操作只在成功Complete后通过既有同处与可见性检查传播实际grants，不向异地人物预发结果。此为新行为条件，不作同条件性能比较。
绑定查询缓存每个主体/物理身份的单项结果；修改bindings仍失效。支持来源按完整候选事件值构建引用索引，合并模型接受事件时保留原事件顺序，最终仍核对完整claim、after和basis，不把索引命中当作支持。中性检查与唯一180秒正式逻辑会话分列。
内容移动正文含起终点，调查检视按本人真实字段显示操作完成状态；首夜递零嘴作为可见行动，实际同处观察不再伪装对白。未知姓名仍取本人信息，未取得介绍时不提前公开。

## DV 文件归并与七目录导出（2026-10-18）

生产DV按职责保留五份core、五份session、chapter1/chapter2两个内容目录，以及game的entry/debug-run入口。checks顶层为六份只读检查，authority、fixtures、report在checks子目录分别保存；生产entry不包含oracle、recall示例或验证配置。现役函数名、参数、声明体与字符串值保持原文；旧来源字符串是证据标识，路径迁移通过 artifacts/partial-bootstrap-core/dv-layout-20261018/path-map.json 追溯，不重写旧证据。

结构整理用声明原文与生产依赖闭包对照、编译及导出一致性检查确认工程接线，不执行流程回归或用它宣称游玩改善。新增源文件按职责进入现有目录；出现define/module作用域时须保留独立编译边界，不直接拼接进其他源模块。

一键源码审阅导出入口为 artifacts/partial-bootstrap-core/dv-layout-20261018/export/export_dv_package.bat，默认输出 artifacts/dv_consolidated_package_v2。七个目录各输出一份DV，保留原文件边界和来源指纹；该包用于阅读，不作为新执行入口。--check只校验导出是否与当前源码一致。旧导出与历史证据不改写。

时域边界（2026-10-18）：正式session在time<horizon允许新决策；time==horizon仅保留已有pending/到期队列的Complete，ready扫描不发起新决策；time>horizon停止。无队列全idle不自动跳时。debug_stop按time>=horizon记录world-horizon，不能代替剧情完成。中性证据位于artifacts/horizon-probes-01/，本次用户另授权一个240秒正式窗口。

## 接管修复接口与证据范围（2026-10-18）

`dvs_model_recheck_eligible` 拒绝 `evaluation_scope.kind=causal-refutation` 的局部反证切片；切片的完成状态保留其局部身份，不作为原完整计划的重比资格。

本轮跨区内容使用同一正式判答合同分别登记 `identity` 与 `reason` 阶段。`content_grade` 是声明与报告的内容范围元数据；部分人物辨认与完整原因各有独立条件和回执，它不改变 `truth_level`、不生成模型论证。题目字段由实际开题Complete与有来源的观察取得。配置与结果比较口径见本任务 `COUNTERFACTUAL-CRITERIA.md`，不将可用候选当作实际作答。

`dvs_model_recheck_eligible(actor, hypothesis, model, time)` 仅允许已完成且无执行失败、候选与本人revision/time、采纳、响应模型、有效Replay_Depth和模型起点一致的缓存走重比。其他情况从原付费模型入口重新推演；新增 `source_hypothesis`、`source_mind_time`、`source_replay_depth` 缺失时自然失效。此为保守缓存门，不增加计算预算。无计划的模型记录 `no-declared-plan`，遍历后未完整记录 `declared-plan-incomplete`；深度与预算停止各自保留。`plan_complete`、真实预测/比较、论证与实际行为仍是分别记录的事实。

- `c2_navigation_goals(actor, goals)`：接收含 `navigation_room` 的本人目标；`navigation_requirements` 为内容声明的非空间准备，`navigation_methods`/`method_id` 指向对应方法。本地优先调用完整 `dvp_eligible`，不因空 requirements 判定任务就绪。仅为一项机会增加已知开放路径下一步意图，原实质目标全部保留。
- `c2_navigation_route(actor, target)`：先复用 `c2i_known_route`。下游门尚未核实时，可返回已知开放前缀末端的安全侧地点，`requested_target` 与当前可执行 `target` 分列；不预测未知门、他人行动或后续达成。
- `dvg_refresh_goals` 按选中主体的真实认识/时间/章节刷新完整内容目标；旧章目标在本人实际入厅后清理。第二章行动规划深度恢复为2。`dvs_schedule` 不以一次无候选结果永久跳过主体；同一时刻仍按决策时间与修订避免重复准备。
- `c1_other_place_detective` 两档安装同一可接触内容；配置只通过认知初始化进入共同机制。首夜等待抑制通过实际安装动作的共享资格门核对，既有范围与成本不变。
- `WriteJsonFast`、`rt_json_stringify`、`rt_json_write` 完整保存传入容器值；历史、模拟、采纳、竞争模型及嵌套世界不按字段名清空。精确数在Trace仍输出原分数字符串。完整保存的耗时和文件大小与先前裁剪输出不能作同口径速度比较。
- `dv_debug_event` 对模拟/复核保留 hypothesis、result（假定、预测、比较、覆盖、工作与停止）、source_adoptions；剪枝保留证书及拒绝，论证保留竞争结果。默认JSON模式沿用完整值；显式DAG模式以 `raw_ref` 定位重复大值及原始事件。大 `world`/`predicted` 只在显式调试投影中省略，原值在完整trace或已提交DAG事件中；预测始终标为主观计算。反证切片、完整计划和论证比较分别标记 `calculation_scope`。
- `python language_core/tools/run_debug.py --out <新目录>` 自动追加 `actual-event-index.jsonl`、`reasoning-evidence.jsonl`、`evidence-summary.json`；一次流式整理保留源SHA256、source_line、batch_event_index、事件编号和字段有无。索引不重复整份模型，以 `projection_source`/`raw_event_ref` 指向原值；引用字段记 `exported-reference`。默认模式另存规范化 `events.jsonl`，DAG模式以原字节 `stdout.jsonl` 为唯一packet源。`--evidence-from <既有packet文件> --out <新目录>` 只整理既有输出，不构建或执行；缺失字段保持 not-exported。
- `result.json` 分列 debug_end、stop_reasons、world_horizon_observed、chapter_summary_reported、narrative_completion。后者由人工按来源和实际回执审查，正常退出、trace封存及world-horizon均不等于剧情达成。执行范围仍仅formal-model，不包含Godot画面、几何或输入。

源码索引：`verification/scenarios/chapter2/content.dv`（路由/目标/现场观察）、`dialogue.dv`（沟通与目标刷新）、`daily.dv`（真实日常及票数观察）、`scenarios/chapter1/campaign.dv`/`opening.dv`（投放与等待）、`verification/session/30-runner.dv`（调度）、`core/95-model-argument.dv`（投影）、`compiler/Runtime.cs`（值序列化）、`tools/run_debug.py`（封存与索引）。本节仅记录实现接口，验收结论见本任务新证据。


### 真实回执时间与无损封存

`dvs_receipt_clock_observations(story, mind, bindings, observations, action_event, execution_event, time)` 只读取声明的 `clock_ref` 与本人数值时钟，时间来自已结算 `execution.time`。同字段直接观察合并为同次 `perception/execution-receipt`，绑定实际执行事件；未初始化或未绑定的时钟不被补成已知。Complete单调推进本人mind.time，成功与拒绝均不再加一次时长；Prepare、预测和其他主体不被同步。此修复使窗口判断与真实行动时间一致，不改费用或推进世界时钟的规则。

`rt_json_write("new-trace.json.gz", value)` 可选择gzip无损存储，解压后的字节与同输入 `rt_json_stringify` 一致。默认 `.json` 仍直接JSON；二者均不裁剪字段并用 `.partial` 防止半写冒充完成。`run_debug.py --trace-compression gzip` 向生成程序传入该目标，result列出trace_path/trace_storage_encoding，timeout仍覆盖执行与封存全过程。命令示例：

```powershell
python language_core/tools/run_debug.py --out artifacts/partial-bootstrap-core/<新目录> --detective-profile Foolish_Detective --detective-zone zone_bing --trace-compression gzip --timeout 180
```

### 在线原始值日志与人工资格

显式 `--raw-format dag` 将原始值写入 `raw-events.dag.jsonl.partial`。`dvs_event`先冻结事件、调用记录器提交，再发布到正式事件表；记录器仅I/O，不参与认知、选择或结算。数组子树只定义一次，引用只指向先前定义；每帧保留序号、前帧摘要和本帧SHA-256。`initial` checkpoint保存开启后的完整会话；正常关闭后，`final`保存 `{trace,last_live_session}`，seal完成后才发布正式文件名。定义写完而事件尚未提交的尾部不算已发生事件。

```powershell
python language_core/tools/run_debug.py --out artifacts/partial-bootstrap-core/<新目录> --raw-format dag --timeout 180
python language_core/tools/raw_dag.py <raw-events.dag.jsonl.partial> --out <新summary.json>
python language_core/tools/raw_dag.py <raw-events.dag.jsonl> --event e123 --out <新event.json>
python language_core/tools/raw_dag.py <raw-events.dag.jsonl> --node 456 --out <新value.json>
python language_core/tools/raw_dag.py <raw-events.dag.jsonl> --checkpoint final --path '["trace"]' --out <新trace.json>
python language_core/tools/raw_dag.py <raw-events.dag.jsonl> --node 456 --value-format dv-tagged --out <新typed-value.json>
```

`raw_dag.py`只读原日志，用临时SQLite偏移索引按需流式展开原值。DAG内部以 `{rational:"2/3"}` 保留精确数类型，文本"2/3"仍为文本；默认 `--value-format trace-json`按旧Trace数字文本恢复，`dv-tagged`输出 `{$dv_rational:...}` 区分类型。校验错误或中断留下可验证提交前缀及原尾部；没有final/seal不重建终态，未提交的在途计算保持未知。`raw-dag-summary.json`与 `result.raw_sealing_complete`分别说明范围；`raw-event-index.jsonl`还保留prepare内已提交而尚未投影到stdout的事件引用。DAG文件不冒称 `trace.json`；`--trace-compression`仅用于默认JSON，与DAG互斥。默认未启用记录器时不遍历事件、不创建文件；成功finish恢复disabled，可向新路径重新开启。失败不自动续写，显式零元Bool `rt_raw_abort()`仅释放记录器并保留partial，不生成final/seal。`rt_json_print`直接输出UTF-8，编码语义同JSON接口。

动作可声明 `manual_only=true`。`dvp_eligible_base`要求临时 `actor.manual_input=true`；只有 `dvs_player_options/select_player/player_choice`创建该资格视图，持久状态不写入它。其余本人前提、来源、规范、解释及费用不变；自动规划拒绝这种动作。新的实际询问仍可能进入AI轨迹，手动限制只针对声明的候选。

`policy.known_opportunity_wait_choices`条目包含 `target_time`、`available_when`和 `personal_choice`。只有目标时间精确匹配、本人的条件为真且basis非空时，生成的真实付费等待才附带该选择；多项匹配报歧义。显式声明可与当前行动比较，未声明的默认等待继续受当前责任抑制。个人选择当前项与替代项共用非递归的本人资格及正向价值门，不用不可执行的备选阻断当前行动。

源码索引：`compiler/Runtime.cs`（UTF-8、冻结DAG与通用文本ABI）、`compiler/compiler.dv`（arity/返回类型及字符串发射）、`core/95-model-argument.dv`（原值引用投影）、`verification/session/10-subjective.dv`（事件提交与共同比较）、`15-action-planning.dv`（人工资格）、`30-runner.dv`（人工入口）、`game/debug-run.dv`（启用与关闭）、`tools/run_debug.py`（单次流式导出）和 `tools/raw_dag.py`（只读恢复）。

### 实际建议、本人表态与后续行为

话语可声明 `advice={id,topic,targets,proposed_action_ids,position,basis}`，其中position为consider、avoid或owner-decides。只有实际可听并完成listener_parse后，`dvs_receive_advice`才登记received_advice和历史advice_receipts；这是建议收据，不授予命题或行动结果。内容可声明带 `advice_decision={advice_id,accepted,assessment_action_id}` 的普通付费回应，由本人既有共鸣动机/风险或个人序数价值评估资格。回应不挂主动取回的resonance效果。

`dvs_complete_advice`只在真实成功Complete后登记advice_stances和advice_decision事件，关联实际speech、parse、selection与execution。接受表示纳入本人考虑，不执行目标行动、不改变既有权重。当前建议收据在认知截断时清理；历史记录不用于补回遗忘的认识。

`dvs_action_selection_evidence`只沿本次实际execution→host→quote→planning链寻找匹配的action_selected；`dvs_heard_before_selection`核对同一事件表中实际parse早于该选择，避免新到建议冒领已排队行动。`influence_consequences`与 `player_influence_history`保留真实来源、选择和后果；后者仍核对实际物理false/unknown→true。没有独立因果证据时，记录明确为 `post-advice-matching-behavior` / `causal_attribution=not-established`，可附选择前已发生的advice stance，不用时间相邻声称影响成立。

普通非request/非response话语只有显式repeatable且非once/response_once时允许已使用ID再进入资格；仍核验实际前提、本人选择与收益。请求、回复的一次性和上下文合同保持原路径。源码索引：`verification/session/10-subjective.dv`（资格、收据与顺序证据）、`20-speech.dv`（真实parse及建议表态Complete）、`15-action-planning.dv`（重复话语门）和 `30-runner.dv`（统一Complete接线）；具体建议声明在章节内容中。

### 内容侧协作与采样接口

`c2_work_specs` / `c2_cooperation_rows`声明本人承担、拒绝、实际邀请与回应；`c2_refresh_content_actions`只用本人真实收据刷新选择。`work.accepted.*`是本人承担记录，和 `support_active`/`guard_active`的实际操作结果分开。`c2i_physical_premise(row)`保留权威机械条件，本人承诺、规范采纳、搭档关系及指控状态不成为物理law的开门条件。操作须技能、位置、存活及真实持续支承；实际离岗移动或主动交接清active，重新就位仍须新Complete，已在岗不重复取得同一操作收益。

`c2_meter_fields`包含safe、environment_epoch和sample_location；`c2_sample_claim`只取实际direct perception，`c2_meter_fresh`核同一采样来源、地点、原时刻、30分钟寿命及本人已知环境变化。天气/转述/查旧表不刷新采样来源。`c2_safety_goals`生成有来源的复测与在岗目标；未采纳相应规程者仍可能按旧认识自主冒险，物理暴露由当下世界结算。安全侧仪器接近使用measurement_approach规范分类，真实门、移动和暴露合同保留。

实际登记取用分别保留pending.self/patient等用途计数；purpose_clarified表示当事人真实说明后的回执，补记只结算相应待核份额。普通记录人员依据本人的实际账页观察与问答核对，不读physical自动获知新取用；台账差额不自动建立有罪结论。源码索引：`verification/scenarios/chapter2/content.dv`（声明、执行law、采样与本人刷新）、`daily.dv`（持续值守）、`dialogue.dv`（请求/回应、目标和现场信息）。此处为实现合同，动态效果见本轮正式会话证据。

## 本轮反事实与现场机会接口（2026-10-18）

`dvg_open_config`的配置包含`seed`、`profile`、`detective_zone`、`su_initial_room`（默认hall；反事实可用R08/R05）及`private_execution_benefit_multiplier`（1/2）。正式调试入口通过`--opening-seed`、`--detective-profile`、`--detective-zone`、`--su-initial-room`、`--private-execution-benefit-multiplier`传入并封存实际配置。`Shura_Detective`只调整价值序列和操纵候选自动资格，其余认知参数沿标准配置。位置反事实仅改当前身体和本人位置，不改事故史。

`opportunity_requirement.mode`支持`all-present`、`at-least-one-present`与`none-present`。未知模式报错。`dvs_notice_peers`通过真实观察记录`visible_context_location`与`visible_context_clear`，co_presence事件保留地点与可见性；none-present只有地点匹配、观察可用且指定参与者均未见时满足。该主观判断不代替权威执行检查，也不证明世界上没有隐藏人物。未到达地点的空预测上下文不能满足该资格。

`private_execution`比较本人有来源的benefit乘政策倍率与resource_cost；倍率必须非负数，默认为1。净效用（benefit×倍率−resource_cost）同时进入共同规划的progress，并单列private_utility；倍率不改变账本扣费。源码入口为verification/session/10-subjective.dv，内容声明在chapter2/content.dv；不同倍率未必改变动作资格或结局，需实际轨迹判断。

本轮补充：`--homecoming-negotiation`默认关闭，仅显式协商反事实开启。chapter2/homecoming.dv提供真实提议、听闻与个人异议基础，自己的愿意与对侦探申请的同意分开；尚无安全实证、岛外承诺、照护接续验证或实际离岛生成器，归乡效用由本人drive、caution与对申请者的信任参与实际共同评分；动态异议仍须正式事件证明，不能由声明推定CF8达成。失败动作签名保留现场机会范围与可见上下文，真实变化才允许重新尝试。

实际Complete的话语同时记录actual_influence_actor与控制来源player/autonomous，允许正式自主侦探进入原影响记录；旧player字段保持兼容，control明确来源。只有字面真、真实收闻先于选择、指定动作实际执行且后果首次改变才登记。该时间链仍标causal_attribution=not-established，不冒称已证明因果诱杀。

通用`owner_utility`声明含`requires`、`basis`和`terms`，每项为`quantity`与数值`weight`；只在本人前提成立且有来源时求和。quantity可为数值、本人policy_key、本人ref或speaker（既有本人speaker_trust），不读世界。规划值保留逐项quantity、weight、contribution及来源；相关ref和requires进入依赖切片。效用不越过资格门、不发布结果。`owned_decision`重复沟通目标以本人相关上下文变化判断待办，已完成动作标记不永久封死重新表态。

`maintain_presence`是内容声明的有时窗在场意向：本人已达时不追加无关纯导航目标，尚未达时优先生成该意向的已知通路首步；原实质行动仍参加共同选择，不锁定位置或注入等待。该行为调整针对冻结02实际位置奖励折返，须以新会话核对。

`owned_decision.context_policy_keys`可显式列出本人政策字段；这些字段值同时进入决策上下文与来源快照，实际变化允许重新表态。缺省空列表保持原事实/请求比较，不读其他主体政策。

本轮性能维护：Runtime的JSON编码对不可变FrozenList按弱身份复用精确字符串，可变输入沿原编码路径；读取MemoryStream有效区间避免ToArray复制。无父节点的可变容器仍清除自身Backbone/Index后返回；有父节点沿原传播。DAG仅复用浅帧payload缓冲，保持字节、SHA256链、提交刷新与异常封存规则。动作检索按完整actions值与id纯缓存，仍最后匹配；输入或嵌套改变失效。内容刷新动作/规范数组按原顺序单次重建，静态目标模板只按主体id缓存，动态后缀及收据新鲜度照常计算。工程差分比较原始输出与别名/失效，不是剧情验收。

### 容器热路补充（2026-10-18）

`rt_list_with_index(items,index,value)`是三元List ABI，`dvs_replace`直接转发。每个整数位置按原`rt_eq`比较索引，命中采用value，未命中经原读取路径获取项；新根共享这些项，不属于`copy_value`独立复制。空列表不检查index，无匹配保持全部元素；原列表类型错误与读取顺序保留。

`rt_record_with_many`对正规字符串二元项表构建末值更新索引，保留原字段顺序、重复原键全部更新、新键首次出现位置及末值优先、每个替换值独立复制；其他输入回原路径。`rt_has_value`可对已有冻结列表或未失效backbone建立结构桶：数值统一桶标记，候选沿原顺序执行`same_value`。新建或失效查询只读计算至多八层新建列表的临时指纹，不缓存或展开查询；更深/循环、非法数值与不支持值回原扫描；索引不改变输出、观察或决策。

个人决定首次完成时previous_event为空，后续完成从既有真实决定回执读取前驱；缺少选择或执行证据仍不登记。批量更新索引仅用于原表至少16项，或至少8项且更新多于1项；较小表沿原扫描以减少分配。

章节导航只为本人已知实质前提成立的任务建立到达意向；前提为false/unknown时保留实质目标，现场检查由有来源的独立证据目标与真实方法驱动。观察下界取声明窗口，旧观察不因更新时钟自动变新；本次行为修正与容器性能优化分别验收。

结构相等的内部递归与成员桶核对使用原生Bool，公开 `rt_eq`/`rt_same_value` 仍返回同一DV Bool值；引用短路、精确数转换顺序、有效底本复用和原序比较不变。不建立新的相等规则。

run_debug的runtime_environment还记录DOTNET_Tiered/COMPlus_Tiered设置；运行时分层编译比较使用同源串行独立窗口并记录显式环境，不将该配置变化冒称DV算法收益。

### 180秒续作第八批：私有索引与静态目标

`dvp_inspection_freshness`保留原完整mind/declared缓存键及刷新/读取/真值顺序。索引从私有空根构造：每桶entries独立复制后按键首次位置追加或替换，重复ref的entries保持顺序，末尾才memo发布。该根不属于mind、预测分支或事件，不允许作为共享世界原地更新入口；返回值与输入goals/mind保持原隔离合同。

章节对话目标的静态前后段由`c2d_goal_head_build`/`c2d_goal_tail_build`生成，`c2d_goal_head`按完整tasks及id复用，`c2d_goal_tail`按id复用，调用方复制后在原位置组合。rows/tasks构造、动态row扫描、本人资格/收闻/决定和可用性仍按原顺序执行；模板只包含条件声明，不缓存选择或执行结果。

运行环境元数据筛选以大写键比较`DOTNET_GC`、`DOTNET_TIERED`、`COMPLUS_TIERED`前缀，保存实际环境键和值，兼容Windows环境名规范化。旧缺失记录不追补为实测。

`rt_eq`、`rt_same_value`与`rt_has_value`公开Bool结果复用不可变盒，内部判断与首错顺序不变；此改动仅减少返回值装箱，不改变DV相等与成员关系。小快照InlineArray缓冲候选未采纳，其局部CPU测量未支持收益。


### 规划引用键（2026-10-18）

`core/00-data.dv`的`dv_ref_lookup_key(ref)`仅服务依赖图与检视新鲜度索引。标准有序type/object/facet/field及可选subject文本字段使用R域、字段数与逐值长度分帧，保留空值和完整主体；异常结构、扩展/重复/缺字段、顺序变化、非文本、代理项及替换字符回到J域加原JSON。它不替代ref相等、本人绑定或持久化格式。`rt_is_text(value)`为无转换Bool类型谓词。

`15-action-planning.dv`的dependency_add/dependencies与inspection_freshness/inspection_signature同步使用该键，相关memo域标为ref-key-v1。BFS预算、原序、完整ref输出、来源与错误读取顺序保持；独立工程证据比较旧新键相等关系、依赖闭包、首错和别名。


### 有限空闲证书与内容构造复用（2026-10-18）

`verification/session/30-runner.dv`的`dvs_known_clock_boundary`只分析constant/not/all/equals/at_least条件的未来阈值；未知结构返回不完整，阈值只使证书失效，不改世界或本人时钟。`dvs_idle_proof_supported`排除活跃解释、付费话语预测、未处理收闻和未捕获话题等未证明路径。只有真实准备前后主体完整语义输入稳定、没有新增认知事件且实际无动作、无规划/依赖展开、无时长阻断时，才建立有限idle_guard。

`dvs_idle_guard_matches`核对本人输入、绑定、费用、章节、物理非时钟状态、真实同处上下文及新增事件；未知事件类型唤醒，侵蚀仍先按原流程计算。最长60游戏分钟及规划剩余时域边界仅作保守兜底，不能替代目标和动作条件边界。变化和期限均回原prepare；符合条件时仅更新调度标记，记录`planning_stop_reused`并引用原真实`planning_stop`，不伪造本次认知或行动。证书覆盖受上述证明范围限制，不能称全部主体均可休眠。

第二章`c2d_row_goal`与`c2m_row_goal`仅缓存完整row/id及最终条件等显式输入的声明构造；`c2m_observation_goal_templates`按完整groups/id缓存静态证据模板。动态请求、已作决定、本人资格、证据新鲜度、导航与等待仍按原链计算。`game/entry.dv`仍无全目标刷新守卫；这些构造缓存不能宣称消除了全部刷新或BFS成本。


## C# 容器与证据维护（2026-10-19）

用户已暂停Antigravity并授权在原工作区集中修改；本轮整合C#结构化、Session边界与已识别DV错误修复。正式会话前冻结全部实际源码；源修改期间不把工作区测量当作冻结证据，不并发运行正式会话。

快照驻留保留全部仍存活的规范身份。Gen2收集后增量清理弱驻留表的死亡桶，每64次维护触点最多处理128桶；memo与递归冻结均触发维护，不淘汰存活身份。结构相等仅以数值兼容的结构指纹排除不等；指纹相同仍依原序逐值比较，未知值沿原路径。JSON不按字段名裁剪；原始DAG逐帧复用SHA-256上下文，摘要输入字节、提交刷新与partial合同保持。

中性容器/别名/精确数差分、失活桶压力与DAG原始字节比较属于工程排错。工程样本耗时不外推正式五日耗时，JSON完整性修复与性能比较分别记录。证据目录为artifacts/partial-bootstrap-core/takeover-repair-20261018/motivation-counterfactual-20261018/csharp-runtime-20261019/。

后续集中C#修订：可变分支的首个弱父引用直接内置在MutableState，仅多父时分配附加表；保留多别名、死亡父引用清理和循环失效遍历。DAG写入器跨帧Reset复用两份Utf8JsonWriter，失败释放使用finally保证文件与摘要释放。先做多父/循环失效及原始帧字节工程差分，正式游戏结果按候选版本分列，不沿用前轮窗口作为新候选验收。

用户最新追加授权（2026-10-19）：允许多轮正式一级窗口，每轮最多240秒；每轮集中修改并先确认具体工程收益，再隔离冻结串行运行，实际进展和失败分别记录。同条件C#比较固定首轮DV内容及配置，Antigravity新增剧情另行集成，不跨版本拼接。


本轮最新用户目标（2026-10-19）：集中完成已识别优化、结构/接线与撤销错误捷径后再测试。事实等价允许结合严格分析与实际证据作有范围的工程取舍，但不能通过降低搜索层数、删除认知/目标或裁剪原始证据冒充同条件提速。实际覆盖外的分支保持未知。数值缓存候选虽然降低微负载分配，正式窗口未显示进步，现封存为未采纳候选；现役底本保留第二批的单父引用与DAG写出器复用。

冻结容器现以内部sealed IReadOnlyList<object>独占不可变object[]，直接采用冻结时新建的子数组，不再继承可变List或二次复制。R.Items返回只读接口，IsDvList/TryItems仍仅识别原生List及内部FrozenList，不扩张DV值域；写操作仅接受原生可变List，读取可变所有者时才解冻选中子分支并建立父失效关系。根复制和记录更新仍为O(n)，不是HAMT或持久向量。枚举器保留List的状态与错误语义；越界读交给私有空List产生原异常。Session的HostValue/SnapshotValue同步识别两种合法容器，Save标签、Trace字符串、原始DAG及独立副本必须集中工程核对。Items的CLR返回签名变化要求编译器自举与Session/宿主统一重建，禁止混用旧程序集。

空冻结集合的接口枚举委托私有原生空List，保留.NET空枚举器的独立错误状态；非空集合采用值类型枚举器。解冻由FrozenList.CopyRoot把私有数组一次复制到独占List，不暴露底本、不逐项扩容。单字段更新按原先校验/冻结/首匹配顺序，内部保留具体冻结类型以免逐项经接口分派；冻结索引仅在完整资格验证后读取具体冻结pair。中性保存/恢复、错误文本和DAG差分与分配样本分列，CPU噪声不包装成整局提速。

宿主正文来源：`dvs_session_presentation`透传动作`source_refs`，保留已有字符串surface/lines格式。Main按真实execution_event与输出字段/序号生成line_id，source_id表示动作ID；显示序号不是源声明索引。只读来源元数据跟随ChapterDialogue当前行进入实际绘制记录，缺失的源文件信息保持缺失，不影响人物知识或显示资格。

`game/entry.dv`的`dvg_geometry_bindings`声明按地图分组的sites（id/point/label）和props（id/texture/position/可选footprint）。`dvg_site_titles`只给已有几何提供物件标题。宿主载图后实例化，再使用同一图的锚点、栅格与碰撞执行；场景外观不授予读数或知识。snapshot带齐声明以支持在Complete前跨图的真实执行。生成室内geometry_sites与native标题分工见Godot模块维护合同。

`dvg_initial_anchor`产生主体的明确初次锚点，snapshot及people的`initial_anchor`供首次物化使用，地点`anchor`继续供目的地导航。宿主对两类来源保持区别，初次点仍核同房、可走与占位；保留身体不重新放置。户外地点由室外地图边界/可走性判定实际房间，不冒用室内地形标志。

冻结记录布局索引：`compiler/Runtime.cs`的`FrozenIndex`在原有8项阈值之后验证全部pair；纯文本键可共享原序键布局的末槽字典，命中仍完整Ordinal比较，重复键的末项读取与`record_with`首项更新各沿原合同。池只持有键和只读索引，不持有字段值；SnapshotLock保护，最多128布局、每布局128槽/4096 UTF16字符、累计65536字符。超限清池根后旧节点仍引用旧索引；混合键、大表、长布局回局部索引，坏pair回原扫描与首错。共享只减少metadata重复分配，不是HAMT或常数时间布局识别。

字面量分配：`compiler/compiler.dv::sf_emit_array`按AST已知项数发射`new List<object>(N) { ... }`，元素仍原序求值，空根也独立分配；不定长comprehension不预估结果数。`core/00-data.dv`的`dv_pair`、`dv_single`、`dv_list3`用对应array字面量装入已求值参数，保留子值引用与独占根。资源分配成功时值、通常错误次序、别名及显式费用保持；容量数组提前分配会改变OOM相对元素错误/副作用的时序，profile的helper调用归属也随函数体改变。此边界不属于全资源失败等价承诺。编译器改动须从源重建C0→C1→C2→C3并核C2/C3固定点，再重建Session/宿主。

### 普通游玩事件与完整审计（2026-10-19）

`rt_audit_mode() -> Bool`是无参数平台ABI。完整审计保留原始事件data；普通宿主的`dvs_event_storage(kind,data)`在冻结前排除只供审计的六种before/after大字段。正式因子证明实际读取的历史状态继续保留：inference的mind_before/mind_after、observation和listener_parse的mind_after、factor_prune的actor_before。此边界不关闭来源证明、不裁剪权威world/mind或改变费用与判定。

`DvSession.OpenConfiguredCampaign`选项`audit`默认false，`--dv-audit`设true；独立逻辑输出仍默认完整审计。存档保留audit_mode，普通关闭省略重复的全量session.json，完整采集工具显式启用审计。精简记录不能声称拥有完整逐步快照。实现索引：compiler/Runtime.cs的AuditScope/RecordHot/SnapshotValue，compiler/compiler.dv的native_arity/ck_type_raw，verification/session/10-subjective.dv的dvs_event_storage，Session/Session.cs的实例策略与持久化。

### 人工输入与阅读期推进（2026-10-19）

`dvs_prepare_manual_cognition(actor,events,time,parent,chapter)`用于player_actor：保持基础推理/语用/真实收闻，省去普通回合的假说/论证全量重建与speech_choice_values预测。实际成功replay产生manual_replay_refresh，下一次调用共同完整认知后清除。来源验证与候选准入继续执行。AI玩家使用普通自主链，不进入此分支。

`20-speech.dv`的speech_choice_prediction增加source_actor_revision，speaker_prediction读取这个小值，旧事件回退到actor_before.mind.revision；精简存储不因此保留整个人物副本。`Main.gd`可在阅读时发dv_next，并沿唯一正式执行器提前完成远处NPC动作，遇到玩家、同房间动作或新可见对白则交还前台。界面正文冻结与实际世界推进分开记录；后台执行仍使用真实回执和原费用。

### 源码与Python接口维护（2026-10-19）

现役DV组合以game/entry.dv与game/debug-run.dv的相对include为准。Python构建/编辑接口共用tools/dv_toolchain.py和dv_sources.py，生产类库由build_session.py生成，独立自举由bootstrap_compiler.py保存证据，一级输出由run_debug.py记录。旧Core独立构建包装、固定scratch语义回归包装及JSON多会话编排已退役；其历史源码只从Git查阅，不恢复为第二个游戏入口。

验证源码仍保留checks/独立算法、authority/oracle、report及现有编辑器组。删除的旧比较main包装与固定fixture没有生产include依赖；清理记录保留删除前字节指纹。此处仅维护工程入口，不宣称构建或游戏验收完成。

### 源侧门道执行起点（2026-10-19）

现场执行器以CampusMapView.departure_room核对正式移动起点：除室内地板/台阶外，源房间几何分区内实际可走的D门道格也是合法出发点。entered_room仍只核对真正进入的室内地面，不能以D格给出到达或观察。回执source_geometry区分interior与source-side-doorway，源/目标房间及实际位置仍保留。SpatialInput按真实门道邻接几何匹配现有准入ID，鼠标和E没有新增DV候选或费用路径。

## 被动阶段ABI（2026-10-19）
rt_phase(stage: Text, actor: Text)返回Bool true，使用Runtime.AsyncLocal观察器；无观察器不输出。Session.PhaseObserver在Run范围接收阶段与主体，不修改DV状态。生产game/entry.dv在Prepare记录schedule/goals起止，verification/session/30-runner.dv记录cognition/choice起止；GameEngine关联命令与墙钟。观察器不可作为选择、效果、费用或来源。
