# 运行时数值谓词 ABI 静态审读（2026-10-16）

本记录只涉及源码静态审读；未运行构建、DV 编译、自举、检查脚本、测试或正式会话。未读取设计文档，未修改生成物，未提交。工作区既有音乐文件修改保持不动。

## 接口修订

`language_core/compiler/Runtime.cs` 新增 `R.is_numeric_value(object value)`，对应 DV 名称 `rt_is_numeric_value(value)`，一元调用，返回装箱的 Bool。实现只识别有限 `double` 或分母为正的 `Rational`；数字文本、普通文本、Bool、列表、记录、其他宿主对象、NaN 和无穷值均不满足谓词。检查不解析文本、不执行数值转换，也不访问容器成员。`Rational` 为 sealed record，正常构造器拒绝零分母、规范分母符号并约分；谓词仍显式检查正分母。

原 `rt_is_number(text)` 保持原实现：先要求 Text，再以 invariant culture 的 Float 格式解析有限 double；输入非文本依然报 Expected Text。扫描器继续对 token 文本使用该原接口。新增值谓词与文本解析接口分别保留其合同。

`language_core/compiler/compiler.dv` 的 `native_arity` 一元名称表新增 `rt_is_numeric_value`，`ck_type_raw` 的 Bool 返回名称表同步登记。未修改 Number 返回类型列表，因为该谓词返回 Bool。

## C0 与生成阶段的源码接线

`compiler/seed/Program.cs` 的 `Expr` 默认发射分支按 `rt_` 前缀剥离前三字符，通用输出 `R.<name>(...)`，因此新名称直接输出 `R.is_numeric_value(...)`，不需要 seed 专用分支或名称列表。Reader/Value 不维护 ABI 注册表。

`compiler.dv` 的 `emit_call` 从 `native_arity` 取得参数数，执行调用 arity 核对，再按相同前缀剥离规则发射运行时调用。`runtime_name` 按 `rt_` 前缀保留运行时函数名称，新增名称自然覆盖。源码搜索未发现 compiler 内其他需要同步的专用 ABI 类型列表。

只读审阅 `tools/bootstrap_compiler.py`：C0 由 seed 生成 C1 源码，后续阶段均编译同一 `compiler.dv`；每个阶段构建都复制当前 `Runtime.cs`。新增名称注册是 DV 编译器源的一部分，并会随正常 C0→C1→C2→C3 路径传播。此处是源码推导，尚未实际生成或确认阶段固定点。

## 复核与限制

本轮通过源码定位、阅读与 Git 差异审读核对了原文本 ABI 未改、新方法名称与发射名称一致、一元 arity 及 Bool 返回登记。尚未执行具体输入矩阵、编译、自举固定点或正式一级会话；不能据本记录宣称运行时结果或游戏行为改善。主线程负责调用方替换及工程参考/AGENTS 的同轮同步维护，并在整批修订完成后安排授权的统一验证。
