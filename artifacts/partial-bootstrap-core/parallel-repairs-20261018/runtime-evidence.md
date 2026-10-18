# 封存与自举边界核实（2026-10-18）

范围：阅读附件 pasted-text-1.txt、根与 language_core/verification 的适用 AGENTS 及现役源码；未读取设计文件，未运行会话、构建、自举或流程回归。以下是源码审读与工程修改，不是游戏验收。

## 已修改的工程边界

- `language_core/compiler/Runtime.cs` 的 `json_write` 原先用 `FileMode.Create` 覆盖 `.partial`，并用 `File.Move(..., true)` 覆盖正式证据。改为拒绝已有正式目标、独占新建 `.partial`、非覆盖发布。已有证据与失败留下的部分文件均保留；发布时发生竞争也不覆盖既有文件。
- `language_core/Session/Session.cs` 的 `Save()` 原先先把完整状态转换成另一棵托管容器，再以默认深度序列化；`WriteSave()` 已经有流式精确数转换器。现在两者共用 `SnapshotOptions` 与 `$dv_rational` 格式，深度上限和 Trace 一致为 512，Restore 的解析深度同步。没有改变状态、快照或精确数的含义。
- `language_core/tools/run_debug.py` 原先返回成功不要求完整 trace 文件。现在 `evidence_complete` 同时要求进程成功、正文流格式有效、实际出现 `debug_end`、正式 trace 存在、没有遗留 partial 以及来源未变。记录 partial 是否存在；超时在出现 `debug_end` 后标为 `trace-close-or-serialization`，此前标为 `logical-session`。该阶段判断依据实际协议标记，不声称确定序列化内部热点。拒绝非正超时参数。

所有 `physical_before/after`、`mind_before/after` 保持原样。未采用附件提出的省略快照开关，也没有改变事件不可变性、账本、行动条件或预算。Runtime 更改已与 compiler_syntax 代理协调。

## 对附件封存诊断的结论

`game/debug-run.dv` 在逻辑循环之后先输出 `debug_end`，再调用 `dvs_session_close` 并写完整 trace；进程超时涵盖整个阶段。已有 `continuous-20261018/round-4/run/result.json` 仅记录 180 秒外部超时、trace_written=false、正文格式有效，不能单凭这份 JSON 断定耗时全部发生在序列化。新的阶段字段为以后运行提供直接证据，不回填旧证据。

`Runtime.snapshot` 已使用冻结容器、不可变快照共享及弱引用池；`copy_value` 保留独立可变复制合同。不能把每个事件的逻辑全量快照简单等同为每次都重新深拷贝整棵堆。JSON 导出仍展开每个逻辑快照，因此共享堆不保证输出体积小；在没有本轮完整测量前，不作“数十秒降为毫秒”之类结论。

## 自举和运行时边界

`compiler/compiler.dv` 定义解析、检查、代码输出与 ABI 白名单；`tools/bootstrap_compiler.py` 描述 C0→C1→C2→C3，比较 C2/C3 生成源码并执行另写计算样例。本文没有执行该链，不能作为本轮固定点证据。

生成物依赖 .NET/C# `Runtime.cs` 提供数值、容器、快照、记忆化、文件与 JSON 等平台 ABI；`tools/dv_sources.py` 展开相对 `#include`。所以可称 DV 编译器源码的转译自举，不能称完全摆脱 C#/.NET 的机器码或字节码闭环。编译器没有完整泛型、能力类型和目标无关 Typed IR 的事实也不因源码自举而消失。附件“全量语法/完整类型检查”和固定“37 个原语”不应作为现役完整能力说明。

seed/Program.cs 本身包含最低 C# 发射器，使用 Reader/Value；不包含世界决策/解释会话。不能把“seed 只持有 Reader/Value”误读为 seed 完全没有启动编译器代码。

## 内容死法则核实（只报告）

`verification/scenarios/chapter2/content.dv` 的 `c2_room_actions` 返回空列表，`c2_close_room_law` 没有现役调用；但 `c2_shelter_law` 与 `c2_seepage_law` 在同文件域初始化中被 `dv_adopt_into(..., "enforce", ...)` 注册。附件把三者一概称为“未接通死法则”不准确。是否需要新增房间动作属于内容设计决定，本文未改章节。

`checks/fixtures/cognitive-semantics.dv` 的 `cognitive_specimen` 被现存显式验证计划引用，是隔离机制标本，不能仅因不在生产包中称为无用代码；这些旧计划没有执行。

## 已退役入口

现役目录未发现 `verify.py`、`guarded_level3.py`、`flow_check.gd`、`check_flow.gd` 入口。完整目录枚举确实发现 `scratch/r20-recovery`、`scratch/short-r21` 与 `tmp/` 的旧隔离检出仍保存 verify/flow 源码；它们没有执行，也不能借旧检出恢复正式动作路径。本轮没有删除用户既有隔离工作目录。`run_verification.py`、checks fixtures 和 bootstrap_compiler.py 是仍存在的不同用途工具，不能与已删除的旧根 tools/verify.py 混为一谈。

## 验证范围

修改后的 run_debug.py 仅经 Python AST 解析，相关变更经 `git diff --check` 检查。未构建 C#，未验证深状态 Save/Restore 的执行结果，也未测量序列化速度。一级、二级、三级验收均未执行。公共工程文档与 AGENTS 合同由主代理统一更新。
