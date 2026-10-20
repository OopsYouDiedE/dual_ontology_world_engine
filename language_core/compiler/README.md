# DV 编译器与平台 ABI

编译器源位于 [10-compiler.dv](../10-compiler.dv)，同时处理 S 表达式启动语法与 surface-v2 可执行子集。这里保留的 `seed/Program.cs` 是最小 C0 编译器，包含 Reader/Value 和启动子集的 C# 发射；它不执行世界、人物或会话。C1 及后续生成编译器只引用 [Runtime.cs](Runtime.cs)，不再调用 C0 Reader。

## 构建链与生成物

共用入口为 [tools/dv_toolchain.py](../tools/dv_toolchain.py) 的 `compiler()`，生产生成器为 [tools/build_session.py](../tools/build_session.py)。在仓库根目录运行：

```powershell
dotnet build language_core/LanguageCore.sln -c Release --disable-build-servers -p:NuGetAudit=false
python language_core/tools/build_session.py --out language_core/Session/obj/dv
python language_core/tools/check_dv.py --format json language_core/10-compiler.dv
```

构建工具先构建 `compiler/seed/Seed.csproj`，用 C0 编译 `10-compiler.dv` 得到 C1，再由 C1 得到 C2、C2 得到 C3；每阶段把生成源与 Runtime 交给 .NET 8。C2/C3 的生成源码逐字节比较，固定点不同就停止。缓存位于 `language_core/obj/dv-toolchain/<来源指纹>/`，缓存输入包含编译器源与 compiler 目录中除 bin/obj 外的文件，共用锁避免多个编辑器并发覆盖。

`build_session.py` 默认组合 `60-game.dv` 的 include 闭包，生成 `DVProgram.cs`、模块范围和来源清单；Session 项目引用 `obj/dv/DVProgram.cs`。这些是可重建输出，不能手改或冒称独立源实现。仅比较生成源固定点不等于形式化证明或正式游戏验收。环境缺失、SDK 错误或未运行阶段如实说明，不沿用历史构建记录。

`tools/dv_toolchain.py` 的 `sources()` 按当前文件解析相对 `#include`，保留 define 作用域与 UTF-16 源位置边界，拒绝循环和缺失依赖。裸编译器接口接收已经组合的源与范围数据；编辑器使用 [dv-check.json](../dv-check.json) 选择编译器、生产或独立检查闭包，生产组不含 oracle。

## 前端与当前类型边界

`sf_scan/sf_parse` 处理 Unicode、缩进与 define，`sf_declaration` 展开 fn、axiom、mapping、acceptance、imprint，`sf_rename_body` 为局部绑定生成唯一目标名，`ck_program/ck_body/ck_expect` 核对名称、元数、显式名义类型、字面量范围及 let 可变性，随后直接发射 C#。

当前表层支持普通位置参数、记录/列表、字段与下标、中缀运算、短路逻辑、if/elif/else、while 和单绑定列表推导。`elif` 按源码顺序降为嵌套分支；推导式先在外层环境求值 source 一次，再按原顺序处理 guard/projection，绑定仅在表达式内生效，支持嵌套。source 须为 List、guard 须为 Bool，元素保持 Value；需要短路的集合检查仍使用 while，不能用分配完整结果的推导式宣称等价的短路行为。

未注解值保持 Value，有限 double 仍是通用 Number，显式 Rational 提供精确数。名义区间/数值检查不能证明所有运行值、主体信息域或一般数学性质；通用泛型、单位推导、闭包、能力类型、完整领域声明和目标无关 Typed IR 尚未实现。完整方案与当前子集见 [SYNTAX.md](../SYNTAX.md)。编译器自身仍用启动语法，70-checks 也保留部分启动语法报告函数。

mapping 的乘法形式要求直接具名映射调用作为左项，尚非可传递的闭包值。acceptance 的程度与门槛分类、imprint 的有限域配平由 `20-core.dv` 库函数执行；独立证书核验重新检查可行性、成本与最优覆盖，不能把编译名义检查代替求解证明。

## 启动语法

```text
Program = (defn Name (Name*) Expr)* (main Expr)
Expr = Number | String | true | false | Name
     | (Function Expr*)
     | (if Expr Expr Expr)
     | (let (Name Expr ...) Expr)
     | (set Name Expr)
     | (do Expr+)
     | (while Expr Expr)
```

启动源保留前向引用、递归、按序实参和 let 初始化，if 只执行选中分支，while 返回 false。其名称采用 ASCII 字母/下划线及后续数字，分号注释延续至行尾，字符串支持 Unicode 与基本转义；表层的 Unicode 标识符和 define 由表层前端处理。AST 节点保存 kind、payload 与 UTF-16 偏移。

## 基础值和数值

Runtime 只承接基础值、容器、文件、控制台与执行适配，不实现 DV 语法解析、AST 求值、人物选择或世界法则。新增 ABI 时同步 10-compiler 的元数/返回类型和文档；seed 保持通用 rt_ 发射，不增加世界解释器。

| ABI | 合同 |
| --- | --- |
| `rt_is_number(text)` | 检查文本可否解析为有限浮点数，供词法扫描使用 |
| `rt_is_numeric_value(value)` | 只对合法有限 double / Rational 为真，不把数字文本、Bool 或容器转换成数值 |
| `rt_is_text(value)` | 只判断 Text，不解析、编码或构造值 |
| `rt_eq(a,b)` | 基础值按既有值规则，列表仍按身份，不等同结构比较 |
| `rt_same_value(a,b)`、`rt_has_value(items,value)` | 按原序结构比较与成员查询，保留精确/浮点混合数值相等规则 |
| `rt_text_join(values)` | 按原顺序拼接文本，用于生成代码等纯文本处理 |
| `rt_list_with_index(items,index,value)` | 返回新列表根，沿原索引相等规则替换命中项，其他元素保留引用；不等于深复制或原地替换 |

Rational 与同文字符串始终分型。double 显式转 Rational 使用其十进制表示，不抹去此前浮点误差。数字解析保持 Text/有限值校验、其他拼写和负零的既有边界；任何数值缓存必须保留这些语义与首错顺序。

## 独立复制与不可变快照

`rt_snapshot` 递归复制普通列表为不可变 FrozenList，并对相同不可变子树共享身份。哈希碰撞逐项核对；弱驻留不延长已释放快照的生命周期，也不淘汰仍存活的规范身份。源列表后续修改不会改变快照，`rt_push/rt_replace` 拒绝直接写快照。

FrozenList 是封闭的 `IReadOnlyList<object>`，独占底层数组；`R.Items` 返回只读接口，`IsDvList/TryItems` 只识别原生 List 与内部 FrozenList。CLR 数组等其他集合只由宿主证据编码器显式支持，不进入 DV 值域。枚举器及越界读取保持既有错误边界；公开接口签名改变时必须统一重建编译器、Session 与宿主，不能混用旧程序集。

`rt_copy_value` 返回独立可变根，子树暂存不可变快照；从可变父节点读取子列表时仅在该父节点内延迟实体化独立可变分支，从快照读取继续只读。原值、兄弟副本与事件快照相互隔离。弱父链接保留多别名、循环失效与死亡父引用清理；ABI 写入使自身和所有可变父节点的底本/索引失效，纯读取实体化保持未变底本有效。C# 不绕过 ABI 修改 Items。

驻留池在 Gen2 后按维护触点增量清理死亡桶，保留存活身份；memo 和递归冻结均参与维护。根复制与记录更新仍为 O(n)，当前结构不是持久向量或 HAMT。分配、CPU 或样本结果只说明实际覆盖，不外推整局性能。

## 有序记录与缓存

记录保持有序 pair 列表。`rt_record_index/rt_record_find` 从末项查找，重复键末项读取；`rt_record_with` 只替换首个匹配项，无同键则追加；`rt_record_with_many` 更新全部同键项、更新表末值优先，空更新返回原记录。批量更新先冻结更新表，避免惰性读取改变枚举器；未变分支按独立复制合同实体化。不能把读取和单项/批量写入的不同重复键语义统一改掉。

正规文本键记录可以建立末槽索引，非文本键、不规则结构或未知条件回到原序线性路径。可变 pair 的父链失效必须覆盖键、字段对和记录修改；索引不得绕过全部结构验证或改变首错顺序。较小记录在重复读取后也可建立索引，不因优化阈值改变语言有效值范围。

冻结记录布局只共享有序文本键到末槽的只读字典，命中仍逐项 Ordinal 比较，不保存字段值。池上限为128布局、每布局128槽/4096 UTF-16字符、累计65536字符；超限清除池根，已发布节点继续持有自己的字典，不清内容。这些是缓存准入参数，不是全进程内存上限或合法记录限制。

结构相等可由相同引用/有效底本短路，只使用数值兼容哈希排除不等；存储身份哈希不替代值哈希，也不是持久摘要。同兼容哈希仍逐项深比，精确数、浮点数及正负零继续按原值规则。结构查询无法稳定哈希时回扫描；新建查询不被偷偷冻结或缓存成新状态。

`rt_memo_get/put` 由完整不可变输入身份和命名键区分结果，读取返回独立副本。每个输入按键首个文本项分区，分区最多512键、按插入次序淘汰，弱输入表不延长生命周期。源修改由失效链产生新的底本。memo 只用于纯值运算，不能缓存实际事件、费用结算、采信、执行结果或错误；新增读取依赖必须进入完整键。

## 审计、文件与阶段观察

`rt_audit_mode()` 读取 AsyncLocal 执行上下文，独立程序默认完整审计，Session 在各调用作用域覆盖。`rt_phase(stage,actor)` 调用外部只读阶段观察器，正常返回 true；无观察器不输出，不能作为 DV 时间、事件或完成回执。平台观察不得反向决定人物行为。

新证据由 Runtime 的 `BinaryEvidence` 与 DAG writer 写入校验二进制。`.dvlog` 使用 DVLOG2 类型帧，DAG 另有节点定义、先前节点引用、序号/前序摘要、事件、checkpoint、final 与 seal。Rational 原子保留类型和精确文本；浮点位型、字符串、字段顺序及未知字段不被裁剪。节点先于引用，真实事件提交在发布之前，失败保留 `.partial`，成功封存后原子发布并拒绝覆盖。

| ABI | 记录行为 |
| --- | --- |
| `rt_raw_open(path)` | 仅允许审计模式，在新路径独占开启 DAG |
| `rt_raw_event(value)` | 记录冻结原值；非审计模式不遍历参数并返回 true |
| `rt_raw_checkpoint(name,value)`、`rt_raw_ref(value)` | 保存实际阶段或已有值的引用，保留来源，不执行世界 |
| `rt_raw_finish(value)`、`rt_raw_abort()` | 完整结束或释放失败记录器；失败不自动重试，不覆盖 partial |
| `rt_json_print(value)` | 记录器开启或显式二进制 stdout 环境下输出二进制投影；其他情况服务编译/数据交换 |
| `rt_json_write(path,value)` | 保留识别以给出明确诊断，执行时拒绝新 JSON 文件记录 |

未开启记录器的事件/checkpoint/finish/ref 接口按各自现有返回合同处理，不遍历并构造伪证据。JSON 解析和字符串交换仍是进程内功能，旧 JSON 记录只读兼容。二进制输出与精简审计保留范围分别说明，序列化器不按任意字段名裁剪。存档和关闭语义见 [Session/README.md](../Session/README.md)，读取器为 `tools/binary_evidence.py` 与 `tools/raw_dag.py`。

`build_session.py --profile` 只在诊断生成函数中插入 `DvProfiler`，不会给普通类库计时。中性别名、冻结更新表、精确数、异常文本、DAG 字节及固定点的工程证据分别记录；构建或编解码成功均不能宣称人物、剧情或实机体验已验收。
