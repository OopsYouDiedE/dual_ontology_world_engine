# DV 编译器与平台 ABI

编译器源位于 [10-compiler.dv](10-compiler.dv)，同时处理 S 表达式启动语法与 surface-v2 可执行子集。这里保留的 `seed/Program.cs` 是最小 C0 编译器，包含 Reader/Value 和启动子集的 C# 发射；它不执行世界、人物或会话。C1 及后续生成编译器只引用 [Runtime.cs](Runtime.cs)，不再调用 C0 Reader。

## 构建链与生成物

共用入口为 [mcp/build/dv_toolchain.py](../../mcp/build/dv_toolchain.py) 的 `compiler()`，生产生成器为 [mcp/build/build_session.py](../../mcp/build/build_session.py)。在仓库根目录运行：

```powershell
dotnet build dv_core/LanguageCore.sln -c Release --disable-build-servers -p:NuGetAudit=false
python mcp/build/build_session.py --partition --out dv_core/host_bridge/obj/dv
python mcp/editor_lsp/check_dv.py --format json dv_core/compiler/10-compiler.dv
```

构建工具先构建 `compiler/seed/Seed.csproj`，用 C0 编译 `10-compiler.dv` 得到 C1，再由 C1 得到 C2、C2 得到 C3；每阶段把生成源与 Runtime 交给 .NET 8。C2/C3 的生成源码逐字节比较，固定点不同就停止。缓存位于 `dv_core/obj/dv-toolchain/<来源指纹>/`，缓存输入包含编译器源与 compiler 目录中除 bin/obj 外的文件，共用锁避免多个编辑器并发覆盖。

`build_session.py` 以单个模块为解析、语义分析与代码生成单元。每个模块使用从零开始的 UTF-16 源位置，导入依赖模块导出的函数名、参数元数、Mapping 标记和名义类型接口；编译器只处理本模块的函数体。AST 缓存由编译器指纹与模块文本寻址，生成缓存再加入导入接口、类声明和生成器版本。前一个文件长度改变不移动后续文件的位置；依赖正文改变且导出接口不变时，消费者的生成缓存继续有效。生成内容相同时保留文件时间戳。

`--base-only` 将20/30分别生成到 `base/obj/dv/modules/*.g.cs`，共同构成 `DuelView.Core.DVKernel`；`--partition` 将35–60分别生成到 `host_bridge/obj/dv/modules/*.g.cs`，共同构成引用基础程序集的 `DVProgram`。小型 `DVKernel.cs` / `DVProgram.cs` 只保留 partial 类声明，MSBuild 在生成步骤后纳入本次模块文件。完整工具程序使用同一模块生成流程；65与70保持各自的独立闭包。`source-manifest.json` 记录规范化源码、模块生成缓存、导出接口摘要和输出文件清单；旧合并源、范围文件和已退役输出由生成器清理。这些均为可重建输出。

MCP 与仓库内 LSP 的初始化通过 `mcp/build/prewarm.py` 启动后台预热，初始化请求不等待编译。预热进程与正式构建使用同一自举缓存锁，日志位于 `dv_core/obj/dv-toolchain/prewarm.log`。预热不属于游戏循环，完成状态以实际进程退出码为准。生成源固定点、编译完成、游戏行为和性能测量分别报告；未运行阶段保持未运行。

`mcp/build/dv_toolchain.py` 的 `source_graph()` 只解析从行首开始的相对 `#include`，返回依赖顺序、直接依赖图与各模块文本；`sources()` 保留原有调用接口。define/称谓和 UTF-16 位置属于各自源码。缩进正文中的 `#include` 保持文字；源适配只规范 CRLF/CR，不拆分 U+2028 等正文字符。编辑器使用 [dv-check.json](../dv-check.json) 选择编译器、生产或独立检查闭包，生产组不含 oracle。

裸编译器有两种工程接口：`<源> <输出> --check|--emit [范围JSON]` 保留整组诊断与自举调用；`<单模块源> <输出> --module <配置JSON>` 使用独立模块。模块配置依次为导入函数签名、导入名义类型、导出接口路径、目标类头和 AST 缓存路径；导出文件是 `[本模块函数签名, 本模块名义类型]`。名称、元数、重复导出及名义类型语义由 DV 编译器处理，Python 只组织依赖和缓存。

## 前端与当前类型边界

`sf_scan/sf_parse` 处理 Unicode、缩进、define 和文件内称谓。`nl_scan_prose` 在标点、注释及括号扫描前捕获叙述/台词/表述的原文 token；`nl_declaration` 把作者声明降到同一函数 AST，`sf_declaration` 继续展开 fn、axiom、公式、function 具名输出、mapping、acceptance、imprint。`sf_rename_body` 为局部绑定生成唯一目标名，`ck_program/ck_body/ck_expect` 核对名称、元数、显式名义类型、字面量范围及 let 可变性，随后直接发射 C#。

自然法则调用既有 `dv_ref/dv_fact/dv_at_least/dv_not/dv_all/dv_assign/dv_increment/dv_axiom`，不增加世界解释器。自然行动生成既有七字段及可选 once/surface 的有序记录，整体 `dv_clone` 保持传入容器隔离；它不依赖章节专用的 c1_action。自然条件中的明显布尔字面量会在根节点和非/合取/析取操作数上被拒绝，属性值仍可为真或假；未注解函数返回值仍受既有 Value 静态边界限制。所有新增名称与函数共用名称/元数检查，称谓替换保留引用处源位置，原文 token 不参与名称替换。完整作者合同见 [SYNTAX.md](../SYNTAX.md)。

公式 `f(x) [-> Type] = expression` 与 `function y[: Type] = f(x) ... end` 都降到既有 surface/defn 管线。具名输出用公共 declare 语句表达未初始化可变局部，不生成默认 false；`sf_output_flow` 在重命名前核对明确赋值、分支交集、循环可能零次和裸 return。多输出按序返回 List，调用侧仍是普通位置参数与单一返回值；不支持调用端解构或 nargout。end 风格块与原 fn 缩进块分别解析，共用后端与名义检查；输出局部的 C# definite assignment 仍接受实际 .NET 编译核对。

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

有限 double 在绝对值不超过 `2^53-1` 且本身为整数时，直接构造与原十进制往返表示相同的精确整数；其他数仍先取原往返十进制文本。0 与 ±1 仅共享不可变 Rational 常量，不保存可变数字缓存。同分母加减/比较、分母1及零/单位元运算跳过冗余解析、乘法与约分，仍返回相同精确值；除零保留异常。

## 独立复制与不可变快照

`rt_snapshot` 递归复制普通列表为不可变 FrozenList，并对相同不可变子树共享身份。哈希碰撞逐项核对；弱驻留不延长已释放快照的生命周期，也不淘汰仍存活的规范身份。源列表后续修改不会改变快照，`rt_push/rt_replace` 拒绝直接写快照。

FrozenList 是封闭的 `IReadOnlyList<object>`，独占底层数组；`R.Items` 返回只读接口，`IsDvList/TryItems` 只识别原生 List 与内部 FrozenList。CLR 数组等其他集合只由宿主证据编码器显式支持，不进入 DV 值域。枚举器及越界读取保持既有错误边界；公开接口签名改变时必须统一重建编译器、Session 与宿主，不能混用旧程序集。

`rt_copy_value` 返回独立可变根，子树暂存不可变快照；从可变父节点读取子列表时仅在该父节点内延迟实体化独立可变分支，从快照读取继续只读。原值、兄弟副本与事件快照相互隔离。弱父链接保留多别名、循环失效与死亡父引用清理；ABI 写入使自身和所有可变父节点的底本/索引失效，纯读取实体化保持未变底本有效。C# 不绕过 ABI 修改 Items。

一次父链失效使用该次操作的独立身份标记去重，单父路径迭代处理，仅真实分叉时建立待处理栈；多别名与循环仍全部失效，不以已经访问某个旧版本为理由跳过当前写入。标记只属于相应可变状态，不复用于其他写入。

宿主每个会话持有一个 `R.ValueContext`，以 `using (R.UseValues(context))` 覆盖初始化、恢复、DV 调用、交互记录与封存中的值访问；作用域退出恢复先前上下文，`ValueContext` 本身不需要 Dispose。可变底本、弱驻留、memo、记录布局、JSON编码与原始记录器状态均归该上下文，不以进程静态可变表跨会话共享。没有显式宿主作用域的编译器/独立程序在当前 AsyncLocal 执行流中按需创建默认上下文；并行独立会话仍须显式持有各自上下文。原始记录器继续由 finish/abort 负责释放，不由短期值作用域关闭。

驻留池在 Gen2 后按维护触点增量清理死亡桶，保留存活身份；memo 和递归冻结均参与维护。根复制与记录更新仍为 O(n)，当前结构不是持久向量或 HAMT。分配、CPU 或样本结果只说明实际覆盖，不外推整局性能。

## 有序记录与缓存

记录保持有序 pair 列表。`rt_record_index/rt_record_find` 从末项查找，重复键末项读取；`rt_record_with` 只替换首个匹配项，无同键则追加；`rt_record_with_many` 更新全部同键项、更新表末值优先，空更新返回原记录。批量更新先冻结更新表，避免惰性读取改变枚举器；未变分支按独立复制合同实体化。不能把读取和单项/批量写入的不同重复键语义统一改掉。

记录更新的替换字段直接保存不可变快照，首次经新记录的可变父节点读取时才建立独立可变分支。更新后未使用的大字段不提前整根复制；读到的可变值仍与原值、其他字段副本和历史快照隔离。已冻结值和原子值的 `rt_snapshot` 直接返回，普通可变根仍依底本和父链决定是否重新冻结。

文本键与已经冻结的有效 DV 字段值可直接组成驻留的不可变字段对，省去即刻冻结的一次性可变 pair、父链接与弱状态登记；驻留哈希、双精度位型比较、字段顺序及随后通过可变父节点读取时的独立实体化合同保持。未知宿主原子继续走原有构造路径，不提前改变其报错时机。非文本键与不规则更新仍沿原路径处理。

冻结普通的两元素文本头列表时复用相同字段对驻留路径，命中已有不可变值便无需先分配候选数组。原列表的可变状态和子列表父链接仍按原规则保留；不符合该有效值形状的输入继续走原冻结路径。该优化不把普通列表解释成领域记录，也不改变列表顺序或身份驻留规则。

浮点算术结果在同一处检查有限性并装箱。不可变的小整数原子（0–1024 和 -1）以及比较布尔值可共享，减少循环计数和标量运算中的装箱；负零保留其原位型。该原子池不包含会话、字段、缓存结果或其他可变状态，不改变精确 Rational 运算与转换。

记录槽位查询同样复用这些整数原子；列表根替换在普通 double 索引下直接比较已知有限整数位置，避免逐项装箱与布尔往返，其他索引类型仍沿原相等规则处理。未命中索引、负零、分数和非有限索引保持原有匹配结果，选中项之外的读取顺序不变。

正规文本键记录可以建立末槽索引，非文本键、不规则结构或未知条件回到原序线性路径。可变 pair 的父链失效必须覆盖键、字段对和记录修改；索引不得绕过全部结构验证或改变首错顺序。较小记录在重复读取后也可建立索引，不因优化阈值改变语言有效值范围。

冻结记录布局只共享有序文本键到末槽的只读字典，命中仍逐项 Ordinal 比较，不保存字段值。池上限为128布局、每布局128槽/4096 UTF-16字符、累计65536字符；超限清除池根，已发布节点继续持有自己的字典，不清内容。这些是缓存准入参数，不是全进程内存上限或合法记录限制。

结构相等可由相同引用/有效底本短路，只使用数值兼容哈希排除不等；存储身份哈希不替代值哈希，也不是持久摘要。同兼容哈希仍逐项深比，精确数、浮点数及正负零继续按原值规则。结构查询无法稳定哈希时回扫描；新建查询不被偷偷冻结或缓存成新状态。

`rt_memo_get/put` 由完整不可变输入身份和命名键区分结果，读取返回独立副本。每个输入按键首个文本项分区，分区最多512键、按插入次序淘汰，弱输入表不延长生命周期。源修改由失效链产生新的底本。memo 只用于纯值运算，不能缓存实际事件、费用结算、采信、执行结果或错误；新增读取依赖必须进入完整键。

已有 memo 分区的查询键可在调用内只读借用，用与冻结存储相同的内容哈希、列表顺序、原子类型及嵌套浮点位型逐项匹配；查询不保留可变键、不为临时键登记弱状态，也不使其子列表实体化。只有 put 冻结并保存键；新主体或分区缺失仍执行原冻结验证，get 不为一次未命中创建空分区。返回值依旧是独立副本，查找方式不放宽输入依赖或键相等条件。

每个值上下文持有自己的不可变空列表快照。冻结全新的空列表字面量不建立可变状态登记；已有父链接照常保留，随后 ABI 写入仍使所有关联父底本失效。空列表的内容哈希和证据编码不变。

## 审计、文件与阶段观察

`rt_audit_mode()` 读取 AsyncLocal 执行上下文，独立程序默认完整审计，Session 在各调用作用域覆盖。`rt_phase(stage,actor)` 调用外部只读阶段观察器，正常返回 true；无观察器不输出，不能作为 DV 时间、事件或完成回执。平台观察不得反向决定人物行为。

新证据由 Runtime 的 `BinaryEvidence` 与 DAG writer 写入校验二进制。`.dvlog` 使用 DVLOG2 类型帧，DAG 另有节点定义、先前节点引用、序号/前序摘要、事件、checkpoint、final 与 seal。Rational 原子保留类型和精确文本；浮点位型、字符串、字段顺序及未知字段不被裁剪。节点先于引用，真实事件提交在发布之前，失败保留 `.partial`，成功封存后原子发布并拒绝覆盖。

二进制编码器直接写入归档器私有的可复用字节缓冲，DAG 依不可变节点身份登记子节点，再按原序编码父节点。每个 payload 只编码一次；前序摘要校验直接读取该段字节，完整 envelope 在同一缓冲追加 sha256 字段后写帧，哈希实例也由该归档器独占复用。格式仍为 `dv-raw-dag-v2`，原有字段次序、类型标签、精确 Rational 标记、两层校验、提交刷新和 final/seal 均保留，现有 Python/.NET reader 无需新分支。保存时间是否降低须由完整会话实测，编码路径替换不等于已达到性能目标。

| ABI | 记录行为 |
| --- | --- |
| `rt_raw_open(path)` | 仅允许审计模式，在新路径独占开启 DAG |
| `rt_raw_event(value)` | 记录冻结原值；非审计模式不遍历参数并返回 true |
| `rt_raw_checkpoint(name,value)`、`rt_raw_ref(value)` | 保存实际阶段或已有值的引用，保留来源，不执行世界 |
| `rt_raw_finish(value)`、`rt_raw_abort()` | 完整结束或释放失败记录器；失败不自动重试，不覆盖 partial |
| `rt_json_print(value)` | 记录器开启或显式二进制 stdout 环境下输出二进制投影；其他情况服务编译/数据交换 |
| `rt_json_write(path,value)` | 保留识别以给出明确诊断，执行时拒绝新 JSON 文件记录 |

未开启记录器的事件/checkpoint/finish/ref 接口按各自现有返回合同处理，不遍历并构造伪证据。JSON 解析和字符串交换仍是进程内功能，旧 JSON 记录只读兼容。二进制输出与精简审计保留范围分别说明，序列化器不按任意字段名裁剪。存档和关闭语义见 [host_bridge/README.md](../host_bridge/README.md)，读取器为 `mcp/verification/binary_evidence.py` 与 `mcp/verification/raw_dag.py`。

`build_session.py --profile` 只在诊断生成函数中插入 `DvProfiler`，不会给普通类库计时。中性别名、冻结更新表、精确数、异常文本、DAG 字节及固定点的工程证据分别记录；构建或编解码成功均不能宣称人物、剧情或实机体验已验收。

CLI 性能选项 `DV_OPT_RECORDCACHE=1` 在会话值缓存上下文创建时读取；原生 `record_like_cached`、`ref_key_text_cached`、`ref_lookup_key_cached` 只返回纯结构布尔/字符串，使用有效 FrozenList/Backbone 身份，不改变记录读取、别名或精确数。DV 通过 `rt_record_cache_enabled` 选择，关闭保留原实现。原始 DAG 事件封包的 process_elapsed_ms 为记录时序元数据，不进入世界值。

激进优化 ABI 新增 cow_enabled/stage_store_cow、mask_enabled/cell_uncontested、lazyinternal_enabled 和 dirty_next。前三类开关在值缓存上下文创建时采样；dirty_next 使用上下文私有的反向依赖队列，按不可变声明索引及 subject 隔离，不写人物或物理事实。具体配置、断言及未覆盖路径见 ../../docs/cli-optimization.md。
