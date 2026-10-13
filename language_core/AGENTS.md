# DV 模块维护

本目录继承仓库根AGENTS.md。语言名为DuelView（DV），源码后缀为`.dv`，实现位于language_core。

## 文档

SYNTAX.md维护完整书写格式，DESIGN.md说明语言与编译架构。设计和指南直接描述语法、机制、模块和命令。第一章源码需求保存在artifacts/partial-bootstrap-core/chapter1-reverse-language-requirements-2026-10-13.md。

## 模块

| 路径 | 职责 |
| --- | --- |
| compiler/compiler.dv | 扫描、解析、名称/调用处理和C#输出 |
| compiler/seed / Runtime.cs | 启动编译器与平台ABI |
| core/*.dv | 世界、规则、观察、传播、价值、回应、费用和论证 |
| verification/session | 正式DV逻辑会话 |
| verification/scenarios | 原生人物、世界、话语、公理实例及配置 |
| verification/checks / report | 只读判定和中文报告 |
| Kernel / bootstrap | 当前解释启动链 |
| Terms / InferenceModels | 宿主数据合同 |
| WorldCore | 宿主身份、观察与事务适配 |
| tools | 构建及进程编排 |

core、session及编译器的算法使用DV。平台ABI承接数值、容器、文件、控制台和最低执行适配。现役Godot仍引用csharp/，隔离DV会话有独立接线。

## 内容和机制

新内容采用具名axiom、defn和领域构造函数；原生DV执行后导出JSON作为交换数据。oracle为独立内容包，主体包引用公开合同。旧.story提供迁移来源并继续服务现役宿主。

本人推知、预测与物理执行分别使用infer、predict、enforce采纳。理解、采纳、模型、论证和实际行动保留各自来源。decision显式表达planning和admission；各旧入口的评分、门槛、平分与首步选择在对应DV库中迁移。

## 命令

```powershell
dotnet build language_core/LanguageCore.sln -c Release --disable-build-servers
dotnet run --project language_core/Cli/Language.Cli.csproj -c Release -- demo
python language_core/tools/build_core.py --out artifacts/partial-bootstrap-core/domain-build-<new-run>
python language_core/tools/bootstrap_compiler.py --out artifacts/partial-bootstrap-core/compiler-<new-run>
python language_core/tools/run_verification.py --config <process-plan.json> --out artifacts/partial-bootstrap-core/<new-run>
```

## 工作安排

源码和目标生成物由对应DV构建链连接。架构、接口和命令变动同步本文件；内容设计合并至docs/design-standard.md。阶段成果继续使用artifacts/partial-bootstrap-core/，每轮修改按根流程提交与推送。

用户2026-10-12停止独立Agent复评的裁定继续适用。新语法方案先交付，随后实现并使用第一章内容实际运行。
