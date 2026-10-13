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

容器深拷贝由平台ABI的rt_copy_value执行；DV负责记录更新、推理与规划。记录构造批量复制字段，查找命中即结束；重复键保留末项优先。性能修改通过第一章新会话记录具体推进与实际进程耗时，出错直接修正对应实现，不运行回归套件。

规划评分对每个预测节点建立一个只读人物视图，复用静态动作与政策；评分不写入该视图，正式人物状态仍由会话更新。

专业回复可以声明offer_to；本人支持话语且看到该接收者时，可主动说明一次。已经实际收到的询问仍沿请求回复处理。

## 内容和机制

新内容采用具名axiom、defn和领域构造函数；原生DV执行后导出JSON作为交换数据。oracle为独立内容包，主体包引用公开合同。旧.story提供迁移来源并继续服务现役宿主。

本人推知、预测与物理执行分别使用infer、predict、enforce采纳。理解、采纳、模型、论证和实际行动保留各自来源。decision显式表达planning和admission；各旧入口的评分、门槛、平分与首步选择在对应DV库中迁移。

正式答辩分别记录公共结论等级与主体自己的接受回执。主体接受记录随实际交接更新，规划依据本人回执排除同等级重复提交；只读判定使用提交前的本人记录核对，不以他人的公共结论替代本人的作答。

session/18-requests.dv记录实际收闻的专业请求、本人回复及同处可见人物。请求不直接取得场景命题；回复由另一主体的正式行动、本人认识与实际听觉传播形成。公开动作观察按声明范围与实际同处过滤。规划将方法前提纳入依赖，并在整个搜索中保留预测状态的分数/耗时非支配路径；重复状态不重复展开。

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

run_verification的普通编译/会话执行持续写入当轮stdout/stderr原始文件，结束后记录进程时长与来源封存。可在会话期间查看已经实际发生的行动；完整停止轨迹与只读判定另行记录。

用户2026-10-12停止独立Agent复评的裁定继续适用。新语法方案先交付，随后实现并使用第一章内容实际运行。
