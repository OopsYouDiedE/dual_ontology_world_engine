# DV 编译器

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


## Surface 编译过程

sf_scan/sf_parse 处理缩进、Unicode 与 define；sf_declaration 将 mapping、acceptance、imprint 展开到具体 DV 运算。sf_rename_body 为局部绑定分配唯一目标名。ck_program/ck_body/ck_expect 核对显式名义类型、字面量范围和可变性，随后生成普通 C# 函数与语句。定义与数学检查采用英文符号；源位置错误显示输入路径、行与列。

mapping 当前要求直接以具名映射调用作为乘法左项，尚非可任意传递的闭包值。名义范围构造不能由同名用户函数覆盖。Rational 是平台基础值，认知与求解代码均在 DV；初始 double 数值可显式转为其十进制表示对应的有理数，转换不会消除此前已经产生的浮点误差。

基本检查器核对程序声明；dv_imprint_check 核对有限域求解证书，二者不混用人物相信的公理充当数学证明。当前不是 Lean 等价的证明语言；泛型、量纲和一般精化证明的边界见 SYNTAX.md。

精确有理数的交换表示为规范分数字符串；有限域证书核验器在读取数值字段时重新构造有理数，因此保存后恢复的证书仍可核验，文本字段不自动转换为数字。
