# Core 公式语法、统一求值与多解算器落地报告

本轮依据用户对 MATLAB 风格函数、公式定义、一键主客观绑定、同一函数验证/运行，以及按精度和时刻选择演算支持的要求，修改了真实 DV 编译器、Core 与 Session 时间分析入口。改动基于 `8dead4599e8038c4777fd04744b549c5f750406f`，工作分支为 `core/formula-functions-20261021`。剧情文档和当前界面开发保持各自的现有改动；本报告只评价本轮语言与机制工程。

## 1. 实际可用的作者语法

公式定义不再要求 `fn`、冒号和 return；具名输出采用明确的输入/输出函数头，编译时检查输出的赋值路径：

```dv
平方(x: Number) -> Number = x * x

function y: Number = 截断(x: Number)
    if x < 0
        y = 0
    elseif x > 1
        y = 1
    else
        y = x
    end
end

function [a: Number, b: Number] = 平分(total: Number)
    a = total / 2
    b = total - a
end
```

公式、具名输出、原 fn/axiom 都进入同一函数 AST、名称/元数/名义类型检查与 C# 后端；没有新增解释器或修改 seed 的领域职责。具名输出局部没有默认 false，读取前、裸 return 和函数结束前必须完成赋值，分支取交集，循环按可能零次执行处理。多输出返回有序列表，调用端用下标取值；当前没有 MATLAB 调用端多重赋值、nargout、矩阵广播或闭包。

实际 Core 已将一组基础构造器改成公式，并把 door/channel 的通用规则改成更紧凑的声明。`dv_matches(object, facet, fields)` 构造相等命题合取，`dv_writes` 构造有序赋值效果。例如一条开门规则可以写成：

```dv
开门(门) = dv_contract(
    "open", 1,
    dv_matches(门, "mechanical", {"inside_latch": false, "outside_latch": false}),
    dv_writes(门, "mechanical", {"closed": false}),
    dv_matches(门, "mechanical", {"closed": false})
)
```

数学外观首先降低定义和阅读负担。上述函数仍是明确的可执行定义，不能自动反向求解任意方程，也不构成对所有输入的数学证明。具体可执行边界与后续语言方案分列在 [SYNTAX.md](../../language_core/SYNTAX.md)。

## 2. 同一规则用于验证和运行

`dv_contract` 在既有 axiom 上增加可选后置条件。规则仍需经 `dv_adopt` 获得对应世界中的采用方式。`dv_evaluate(world, name, request)` 接受 operation、mode、event、time、revision；仅将 operation 从 verify 改成 run 即可使用同一求值器：

```dv
let request = {"operation": "verify", "mode": "enforce", "event": "open-1", "time": 1, "revision": world.revision}
let assessment = dv_evaluate(world, "open", request)
let execution = dv_evaluate(world, "open", dv_with(request, "operation", "run"))
```

共同路径核对修订、事件、时刻、采用范围、三值前提、原子效果和后置条件。verify 成功返回 verified，`.world` 保持输入，`.candidate` 标记为 verification，不追加历史或实际支付；assessment 记录输入快照和规则版本。run 对当前输入重新计算，再提交正式事件和修订，不直接消费旧候选。前提和后置条件的未知分别保留，失败整批回滚。predict 的成功与失败结果都标记为 prediction；验证失败仍返回原输入域。

旧 `dv_execute/dv_predict/dv_infer` 继续复用此路径。`dv_verify` 提供直接验证入口。对外部现场动作，原 Prepare → 真实宿主回执 → Complete 链继续负责执行与费用；本轮没有假设 I/O 可以像内存副本一样回滚，也没有通用 old() 或任意函数纯度证明。

## 3. 一次绑定主客观对象

```dv
let view = dv_view(physical, "observer", {"physical_door": "眼前的门", "physical_clock": "墙上时钟"})
let mind = view.mind
let bindings = view.bindings
```

新主体使用 `dv_view`，已有主体使用 `dv_bind(physical, mind, aliases)`。绑定校验显式的物理 ID → 本地 ID 对应，创建缺失的未知 perceived 对象，保留已有认识、采用表、历史和扩展字段。不存在的物理对象、空 ID、重复或歧义映射被拒绝。物理事实、对象种类和法则不会自动复制进 mind；实际可见字段仍由 grants、观察和正式认识更新取得。这使一键初始化省去重复数据装配，同时保留人物的信息边界。

## 4. 当前解算器与分析器的分工

| 组件 | 适用范围与行为 | 可作出的结论 |
| --- | --- | --- |
| enumerate | 按原混合进位顺序枚举有限笛卡尔域 | 精确候选；覆盖预算允许的同一前缀 |
| branch_bound | 在同一线性模型上计算精确有理数区间界，证明连续块不可行或不优 | 带独立覆盖证书的剪枝；保留原序与同价选择 |
| auto / dv_imprint_analyze | 比较候选前缀与界预处理规模，小模型、零维选择枚举 | 算法适用性和预算分析，选择启发式不承诺现实耗时 |
| dv_analyze_law | 分析已采用规则的当前前提依赖、未知与冲突 | 输入快照诊断，effects_verified 明确为 false |
| dv_analyze_time | 分析命题在给定模型时域内需要重新求值的边界 | 有条件的声明分析，不自行推进世界或同步人物认识 |

求解显式选择入口是 `dv_imprint_solve_with(capability, goal, variables, laws, limits, prices, algorithm)`，algorithm 取 auto、enumerate、branch_bound；原 `dv_imprint_solve` 和表层 imprint 声明继续走同一链。新 checker 从原模型重建 witness 与每个分块界，检查连续覆盖、对齐、长度、末块截断和证明种类，同时兼容旧枚举证书。

这里严格分开四个维度：数值是否精确、搜索是否完整、分析在什么时域有效、主体拥有什么证据。有限求解的数值采用 Rational；visited 表示覆盖前缀，收费仍是 `2 × visited`。剪枝减少完整候选求值，不扩大预算。只有覆盖全域才能给出 Optimal 或 Infeasible；预算截止分别保持 Feasible 或 Unresolved，并给出停止原因。candidate_evaluations、bound_checks、preprocessed_options 只作过程诊断，不能代替总工作或真实耗时。

时间分析采用两种策略：minute 保留原一分钟采样格及实际分数 due，invalidate 返回依赖应重评的边界，等式恰在 now 成立时可返回 now。Session 的两个现有入口现在复用 Core 的同一分析核；不支持或无法证明稳定的情况继续逐分钟结算，增量和有序级联由原调用者检查。过期时域不给出新的休眠窗口。

当前模型以离散状态和有限线性选项为主，因此本轮没有为了增加算法数量接入连续积分器。已在 [DESIGN.md](../../language_core/DESIGN.md) 写明扩展条件：出现连续状态及导数后引入误差容差和事件定位；长时域分支需要保守可达范围和精化；明确随机模型才引入种子、样本量与置信区间。它们是后续设计，当前不作为已实现能力。一般 Number 仍为有限 double，整个 Core 不能宣称全精确。

## 5. 审查中顺手修正的边界

时钟引用现在按 object/facet/field 与有效 subject 判断，记录键顺序及缺省/空 subject 不会使同一时钟被误判成稳定的其他字段；主体范围不确定时回退。过期 horizon 返回 complete=false、next=now，避免已有 idle 调用产生新异常。失败回执的 world、changes、basis 全部独立复制，修改失败验证结果不会反向改写输入采用依据。运行预测的所有结果统一为 prediction，消除了新旧入口在失败状态下的域差异。

这些修复来自源码与 Runtime 所有权语义审查，并加入对应中立执行用例；没有借此改写人物政策、剧情参数、预算或世界法则。

## 6. 实际验证与证据

本轮冻结源码后在 Windows 独立工作树执行，.NET SDK 为 8.0.400。中立检查只包含编译器和通用 Core，不导入 Session、章节、生产入口或 oracle，也不调用退役流程。

| 验证 | 实际结果 |
| --- | --- |
| C0 → C1 → C2 → C3 | 完成，C2/C3 生成源码逐字节一致 |
| 公式、具名输出、旧 fn 兼容 | 真实 C3 check/emit、C# 编译和执行完成 |
| 未赋值输出、读前赋值、缺 end 等错误 | 被预期诊断拒绝；非任意非零退出计作成功 |
| Core 快照、回滚、域和绑定边界 | 独立程序执行完成；过期版本、重复事件、倒退时刻等给出对应拒绝 |
| 两解算器、预算、证书 | 对照执行完成，包含精确分数、负系数、截断、零预算、零维和证书篡改 |
| 生产 LanguageCore.sln | Release 构建成功，0 警告、0 错误 |
| 30-session 的生产闭包编辑器检查 | success=true，diagnostics 为空；内容未执行 |

C2/C3 共同生成源 SHA-256：`20fe027f4943962b7f2e5f2870eff5f31fe1c216220c293fb2fbf12ab0e940dd`。一个十个二元变量、可安全成本剪枝的中立模型覆盖 1,024 个候选，枚举实际完整候选求值 1,024 次，分支剪枝求值 1 次；两者 witness、状态、覆盖范围与收费一致。该计数没有包含预处理和界检查，不构成 1,024 倍速度、Godot 帧率或整局收益结论。

证据位置：[中立检查摘要](evidence/neutral-summary.json)、[生产构建摘要](evidence/production-summary.json)、[自举清单](evidence/compiler-manifest.json)、[档案校验](evidence/archives.json)。[neutral-001.zip](evidence/neutral-001.zip) 保留原始命令 stdout/stderr、源快照、生成 C#、元数据和完成标记；[production-001.zip](evidence/production-001.zip) 保留实际构建、编辑器诊断、驱动与源码快照。ZIP 保留原始字节和换行；旁边的文本摘要仅为阅读便利。检查源码和复现命令见 [checks/README.md](checks/README.md)。

验证范围是语言工程与中立机制。本轮没有运行完整剧情、Godot 画面或实机交互，也没有据此宣称归乡结局、人物行为或游戏性能已验收。

## 7. 技术参照

[MATLAB function 官方文档](https://www.mathworks.com/help/matlab/ref/function.html) 用于具名输入/输出函数头的参照，DV 的输出赋值与类型规则仍按自身实现。[SciPy solve_ivp 官方文档](https://docs.scipy.org/doc/scipy/reference/generated/scipy.integrate.solve_ivp.html) 用于未来连续解算器的设计边界：积分方法、局部误差容差与事件检测是不同合同，单步内多个零点可能漏检。上述参考没有作为当前运行依赖，也不提供剧情设定。
