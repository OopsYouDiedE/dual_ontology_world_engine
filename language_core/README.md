# DuelView 独立语言 Core

DuelView简称DV，源码后缀为`.dv`。它表达认知切片、个人理解、采纳、假说、模型轨迹、规划和动作选择，并提供实现编译器与算法的通用计算能力。

[SYNTAX.md](SYNTAX.md)给出 surface-v2 语法方案与当前实现范围，[DESIGN.md](DESIGN.md)说明语言和编译结构。Core 与第一章现已采用可执行的缩进语法、普通调用、记录/列表及中缀运算；mapping 的乘法形式、acceptance 的门槛转换和 imprint 的有限域最小代价配平均已接入编译链。核心代码采用英文，define 提供其他语言的 token 别名。编译器自身仍用 S 表达式启动源。

## 两条执行链

| 链路 | 入口和实现 |
| --- | --- |
| 解释链 | Kernel读取bootstrap，DV解释器载入推演模块，WorldCore提供宿主入口 |
| 编译链 | compiler/compiler.dv生成C#，Runtime.cs提供平台ABI，core/session执行领域算法 |

编译器启动链为C0→C1→C2→C3。当前编译器具备启动子集的扫描、解析、作用域与调用处理、C#输出；已接入 surface 前端与基本名义数值检查；完整能力类型、单位推导、目标无关 IR、模块和闭包仍未完成。

## 编译领域库

| 模块 | 内容 |
| --- | --- |
| core/00-data | 世界、对象、引用和切片 |
| core/10-laws | 公理实例与enforce/predict/infer/norm采纳 |
| core/20-observation | 投影、来源、证据聚合和本人更新 |
| core/30-transport | 通道传播与保留片段 |
| core/40-value、50-response | 本人价值、关系、状态、回应意图和表达 |
| core/70-reasoning-cost | 论证代价与合理度 |
| core/80-inspection | 历史检查点和切片查询 |
| core/90-action-cost | 多维报价、预留、执行和结算 |
| core/95-model-argument | 模型论证、动态槽位与可撤回采纳 |

接口说明见[core/README.md](core/README.md)。

## 编辑器检查

`python language_core/tools/check_dv.py --format json <source.dv>` 调用真实 C3 编译器检查文件所属的 `dv-check.json` 编译组，不执行剧情。每个源文件有独立 define 作用域。错误保留文件、行、列；编译器启动产物按源码指纹缓存。

[Visual Studio 2022 客户端](editors/visualstudio/README.md)通过 `tools/dv_lsp.py` 检查未保存的编辑内容。[Godot 插件](../godot_csharp/addons/dv_checker/README.md)在底部面板提供 DV 编辑、保存检查和错误定位。两者复用同一个编译器，不额外实现语言语义。当前提供编译诊断，尚无 Lean 式完整证明状态、自动补全和跨文件定义导航。

## 第一章

原生内容位于verification/scenarios，会话位于verification/session。本人推知、话语理解、重演、竞争解释、论证、规划和回答协议由编译DV运行。配置、场景和oracle分别导出为交换数据。

现役Godot/C#保留现场、输入和呈现链，隔离DV有自己的逻辑会话。[第一章源码反推](../artifacts/partial-bootstrap-core/chapter1-reverse-language-requirements-2026-10-13.md)说明语言需求；运行方式见[verification/README.md](verification/README.md)。

## 解释链模块

bootstrap中的00-base提供数据工具，10-evaluator提供作用域与解释求值，20-world提供世界与规则，30-logic提供合一与推导，40-belief提供证据权重和模型，45-deduction提供本人推知，50-planning提供有界规划与经历。

解释语法使用def!、let*、if、do、fn*和quote。编译语法使用defn、let、set、while、main及axiom，详见[compiler/README.md](compiler/README.md)。

## 运行

需要.NET 8和Python 3。

```powershell
dotnet build language_core/LanguageCore.sln -c Release --disable-build-servers
dotnet run --project language_core/Cli/Language.Cli.csproj -c Release -- demo
dotnet run --project language_core/Cli/Language.Cli.csproj -c Release -- eval "(+ 20 22)"
dotnet run --project language_core/Cli/Language.Cli.csproj -c Release -- self "(+ 20 22)"
dotnet run --project language_core/Cli/Language.Cli.csproj -c Release -- run language_core/examples/subjective-inference.dv
```

编译领域库与编译器使用tools/build_core.py、tools/bootstrap_compiler.py；会话使用tools/run_verification.py。成果位于[artifacts/partial-bootstrap-core](../artifacts/partial-bootstrap-core/)。
