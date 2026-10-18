# DV 编译器

表层执行子集支持elif与单绑定、可嵌套列表推导式；条件、作用域、求值顺序及动态类型边界见[Core参考](../core/README.md)。中性工程输入在examples/syntax-probes/，不运行游戏。编译器是生成C#的源码自举，平台容器/数值/文件ABI及#include源组合仍分别由Runtime.cs和构建工具承担，不能称为独立机器码或完整能力类型自举。

`compiler.dv` 同时实现 S 表达式启动语法与 surface-v2 执行子集的扫描、解析、领域展开、名义检查及 C# 输出。`seed/` 是启动编译器 C0，C1及后续阶段使用生成的C#与Runtime.cs。新的书写格式见[SYNTAX.md](../SYNTAX.md)。

## 构建与使用

```powershell
python language_core/tools/bootstrap_compiler.py --out artifacts/partial-bootstrap-core/compiler-<new-run>
dotnet artifacts/partial-bootstrap-core/compiler-<new-run>/c3/bin/Release/net8.0/Program.dll input.dv output.cs
```

构建工具依次生成C1、C2、C3。生成的output.cs与Runtime.cs组成.NET 8程序。

## 保留的启动语法

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

函数支持前向引用、递归和相互递归。let从左到右初始化，参数按顺序求值，if求值选中的分支，while返回false。局部赋值使用set。

值域为Number、Text、Bool、List；Number为有限double，List为可变容器。rt_eq对基本值比较值，对List比较身份。

标识符采用ASCII字母/下划线开头，后续可含数字；rt_为平台函数前缀。注释从分号延续到行尾。字符串支持基本转义和Unicode文本。AST节点为`[kind, payload, offset]`，位置为UTF-16偏移。

## 公理与平台

`(axiom Name Version (Parameters...) Premise Effects)`展开为返回dv_axiom数据的具名函数。领域算法由core/*.dv提供。

Runtime.cs提供数值、Bool、文本、列表、文件、命令行、JSON交换和Block调用。compiler.dv负责语法、作用域和代码生成。surface-v2 前端已由 DV 实现；完整目标无关 Typed IR 仍待实现。

平台新增一元Text ABI `rt_text_join(values)`，依原顺序串接文本片段；`cs_string`以片段表一次合并，保留原Unicode与目标C#转义合同。`rt_json_print(value)`是一元Bool ABI，直接向标准输出写UTF-8 JSON和平台换行，数值/容器编码与`rt_json_stringify`相同，避免整包UTF-16中间字符串。

显式调试DAG ABI为 `rt_raw_open(path)`、`rt_raw_event(frozen_event)`、`rt_raw_finish(value)`（一元Bool）、`rt_raw_checkpoint(name,value)`（二元Bool）、`rt_raw_ref(value)`（一元Value）及 `rt_raw_abort()`（零元Bool）。未启用记录器时event/checkpoint/finish/ref/abort返回false，不遍历输入；默认正式宿主没有文件I/O。成功finish恢复disabled，同进程可向新路径开启记录；失败记录器只可显式abort释放，保留partial，不自动重试或续写。启用后只记录冻结原值，不执行世界、推演或人物选择。节点先定义，事件最后提交，帧链使用SHA-256；只有initial、final与seal完整且原子发布才构成完整封存。编译器arity和名义返回类型与上述ABI同步。文件格式、命令及中止边界见[Core参考](../core/README.md)。


## Surface 编译过程

sf_scan/sf_parse 处理缩进、Unicode 与 define；sf_declaration 将 mapping、acceptance、imprint 展开到具体 DV 运算。sf_rename_body 为局部绑定分配唯一目标名。ck_program/ck_body/ck_expect 核对显式名义类型、字面量范围和可变性，随后生成普通 C# 函数与语句。定义与数学检查采用英文符号；源位置错误显示输入路径、行与列。

mapping 当前要求直接以具名映射调用作为乘法左项，尚非可任意传递的闭包值。名义范围构造不能由同名用户函数覆盖。Rational 是平台基础值，认知与求解代码均在 DV；初始 double 数值可显式转为其十进制表示对应的有理数，转换不会消除此前已经产生的浮点误差。

基本检查器核对程序声明；dv_imprint_check 核对有限域求解证书，二者不混用人物相信的公理充当数学证明。当前不是 Lean 等价的证明语言；泛型、量纲和一般精化证明的边界见 SYNTAX.md。

精确有理数的交换表示为规范分数字符串；有限域证书核验器在读取数值字段时重新构造有理数，因此保存后恢复的证书仍可核验，文本字段不自动转换为数字。

通用三元容器ABI `rt_list_with_index(items,index,value)` 返回新的列表根，逐项沿 `rt_eq` 比较索引；匹配项采用value，其他项按原顺序读取并保留引用。非整数、越界或其他无匹配索引返回原元素的新根；空列表不检查index。它不属于独立复制，不等同于原地 `rt_replace`。

运行时数值解析对精确文本 `0`/`1`/`-1` 复用不可变数值盒；先校验Text，其余拼写（包括负零）沿原invariant Float解析与有限值检查。冻结成员索引允许新建查询计算至多八层新建列表的临时指纹，超深/循环/不支持值回原扫描；查询不冻结、不缓存，候选仍用原完整值比较。

结构相等的内部递归与成员桶核对使用原生Bool，公开 `rt_eq`/`rt_same_value` 仍返回同一DV Bool值；引用短路、精确数转换顺序、有效底本复用和原序比较不变。不建立新的相等规则。

`rt_eq`、`rt_same_value`与`rt_has_value`公开Bool结果复用不可变盒，内部判断与首错顺序不变；此改动仅减少返回值装箱，不改变DV相等与成员关系。小快照InlineArray缓冲候选未采纳，其局部CPU测量未支持收益。


`rt_is_text(value)` 是一元、无转换的运行时值类型谓词，返回 Bool；任意非文本值返回 false，不调用 Text 校验或 JSON 编码。引用键快路据此辨别字符串字段，其他输入保留原序列化路径。
