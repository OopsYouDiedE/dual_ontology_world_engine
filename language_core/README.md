# DuelView 独立语言 Core

DuelView简称DV，源码后缀为`.dv`。它表达认知切片、个人理解、采纳、假说、模型轨迹、规划和动作选择，并提供实现编译器与算法的通用计算能力。

[SYNTAX.md](SYNTAX.md)给出 surface-v2 语法方案与当前实现范围，[DESIGN.md](DESIGN.md)说明语言和编译结构。Core 与第一章现已采用可执行的缩进语法、普通调用、记录/列表及中缀运算；mapping 的乘法形式、acceptance 的门槛转换和 imprint 的有限域最小代价配平均已接入编译链。核心代码采用英文，define 提供其他语言的 token 别名。编译器自身仍用 S 表达式启动源。

## 唯一编译执行链

compiler/compiler.dv 生成 C#，Runtime.cs 提供平台 ABI，core/session 执行领域算法；旧 Kernel/bootstrap 解释执行链已经移除。seed 只保留 C0 的语法读取类型，不含世界执行器。

编译器启动链为C0→C1→C2→C3。当前编译器具备启动子集的扫描、解析、作用域与调用处理、C#输出；已接入 surface 前端与基本名义数值检查；完整能力类型、单位推导、目标无关 IR、模块和闭包仍未完成。

## 编译领域库

| 模块 | 内容 |
| --- | --- |
| core/00-data | 世界、对象、引用和切片 |
| core/10-laws | 公理实例与enforce/predict/infer/norm采纳 |
| core/20-observation | 投影、来源、证据聚合和本人更新 |
| core/30-transport | 通道传播与保留片段 |
| core/40-value、50-response | 本人价值、关系、状态、回应意图和表达 |
| core/55-cognition | 接纳门槛、有限域配平、证书核验和刻画提交 |
| core/60-object-laws | 通用门与通道规律构造 |
| core/70-reasoning-cost | 论证代价与合理度 |
| core/80-inspection | 历史检查点和切片查询 |
| core/90-action-cost | 多维报价、预留、执行和结算 |
| core/95-model-argument | 模型论证、动态槽位与可撤回采纳 |
| core/98-debug | 只读事件调试投影 |
| verification/session | 本人认知、话语、模型、规划、调度和正式 prepare/complete |

完整现行参考见 **[Core README：语法、全部领域接口与正式会话](core/README.md)**，包含可执行表层与启动语法、平台 ABI、世界/知识边界、prepare/complete、模块声明索引和未实现范围。SYNTAX.md 中的完整语言方案与该参考中的现行实现分别阅读。

## 编辑器检查

`python language_core/tools/check_dv.py --format json <source.dv>` 调用真实 C3 编译器检查文件所属的 `dv-check.json` 编译组，不执行剧情。每个源文件有独立 define 作用域。错误保留文件、行、列；编译器启动产物按源码指纹缓存。

[VS Code 客户端](editors/vscode/README.md)通过 `tools/dv_lsp.py` 检查未保存的编辑内容，打包为 `DuelView.VSCode.vsix`。[Godot 插件](../godot_csharp/addons/dv_checker/README.md)在底部面板提供 DV 编辑、保存检查和错误定位。两者复用同一个编译器，不额外实现语言语义。此前的 [Visual Studio 2022 客户端](editors/visualstudio/README.md)是另一种插件接口。当前提供编译诊断，尚无 Lean 式完整证明状态、自动补全和跨文件定义导航。

## 第一章

原生内容位于verification/scenarios，会话位于verification/session。本人推知、话语理解、重演、竞争解释、论证、规划和回答协议由编译DV运行。配置、场景和oracle分别导出为交换数据。

现役 Godot 保留现场、输入和呈现链，生产项目已引用由原生 DV 编译生成的 Session，移除旧 Core 项目引用。两章内容与门口交接已编入 game/modules.json；完整两章游玩、离屏人物执行和最终清理仍未完成。[第一章源码反推](../artifacts/partial-bootstrap-core/chapter1-reverse-language-requirements-2026-10-13.md)说明语言需求；运行方式见[verification/README.md](verification/README.md)。

## 运行

```powershell
dotnet build language_core/LanguageCore.sln -c Release --disable-build-servers -p:NuGetAudit=false
```

生产开局可用 `dvg_open_config({seed: 1, profile: "Standard_Detective", detective_zone: "zone_yi"})`；低配档为 `Foolish_Detective`，跨区配置可用 `zone_jia`、`zone_bing`、`zone_ding`。两档当前重演因果深度初值为64/2，行动规划深度为6/2，两种深度独立。开局采样、抗语用、侵蚀与成功结算、夜间正式等待及五日时域边界见 [Core参考](core/README.md#生产认知配置与有据等待)，均为当前工程实现，尚须本轮实际轨迹验收。

正式动作接口见 [Session](Session/README.md)。逻辑会话与 Godot 使用同一 prepare/complete，前者不验证几何。历史源与证据不覆写。
