# 仓库清理与工程验证 — 2026-10-20

基线提交：[`6166be88b96864271b33a1624c0568cb728581c7`](https://github.com/OopsYouDiedE/dual_ontology_world_engine/commit/6166be88b96864271b33a1624c0568cb728581c7)。

本提交清理经引用审查确认退役、孤立或可再生的文件，并修复相关生成器、插件与文档。逐文件路径、原 blob、修改后指纹见 [change-manifest.json](change-manifest.json)，工程结果及失败后复核记录见 [verification.json](verification.json)。旧文件仍可从基线提交取回，Git 历史未重写。

## 清理范围

删除 132 个文件，原始文件总大小 31,915,651 字节，约 30.44 MiB。当前工程还修改 34 个文件、新增或恢复 11 个文件；本目录的报告文件另计。工程内容净减少 31,990,426 字节，约 30.51 MiB，尚未计入本目录报告体积。以上为当前源码树的未压缩大小，不代表 Git 历史或压缩下载包的减少量。

| 类别 | 处理 |
| --- | --- |
| 音乐发行副本 | 删除九首曲目的 36 份 MP3、MIDI、PDF、MusicXML。保留全部原谱、SoundFont、源码和样式，后续导出默认进入被忽略的 `artifacts/releases/soundtrack/`，支持 `--export-dir`。 |
| 本地化生成物 | 删除 12 份 `.translation`，保留四份 CSV 及各自 `.csv.import`；运行和导出前由 Godot 导入重建。 |
| 旧资源 | 删除 `animation_v2/`、无现役消费者的 Flags 三文件、旧 mind 主题和纹理、旧 `Story/dialogue_theme.tres`、未接入的 `UITheme.tres`。关联 PNG 只删除对应侧车。 |
| 退役工具 | 删除旧动画 PowerShell 生成器、失效剧情 MCP 检查、Blender 相机实验和旧 JSON 盲玩汇总入口；历史实现保存在原提交。 |
| 碎片文件 | 删除 `GameEngine.Measurement.cs`，将原 `ReadMilestoneProgressJson()` 原样并入 `GameEngine.cs`，保留公开接口。 |
| 文档与配置 | 收敛根、Godot、语言工程中的旧目录、重复规范、历史任务窗口和机器状态；`docs/syntax.md` 统一导航到现役语法参考。撤销全局 `*.uid` 忽略，补充翻译及发行产物忽略规则。 |

现役 PixelCast、AnimeCast 回退、DialogueCast、共享家具、证物、地图、法线/relief 资源、字体、编辑器客户端和独立检查源码均保留。七份 DV、Runtime、Seed、Session 执行代码及 `docs/design-standard.md` 保持原 blob。

## 同步修复

- `capture_maps.gd` 使用当前 PixelCast；通用 MCP 地图预览同样从 PixelCast 的 `idle_front` 取单帧，保留移动、碰撞、相机和脚部偏移。
- `build_ui_theme.py` 去掉只服务于闲置 UITheme 的生成分支，现役 TitleTheme 与 DialogueTheme 的生成结果及仓内成品字节不变。
- MCP 清除已失效的初醒整场重建、旧 UI 生成和专用整场交接，保留通用转换、家具/材质配方、图像交接、计划、现役 UI 检查与字体测量。UI 检查限制在 `scenes/UI` 下，不实例化 Host。
- 从仓库历史 `1403465243def4fdc21d148d97208d70ca362b70` 恢复六个现役地图和家具依赖的纯绘图模块，逐个 blob 与原文件相同。
- 从历史 `51414f1461f09c045acf890ccfaa85c5fc8a8714` 恢复仍被生产 DV 引用的原始 `Outdoor/dial_meter.svg`。其 blob 为 `1466ed1806b9765945f228e3ce56f3baceb47248`；没有重画素材或改动位置、碰撞、DV 声明。历史没有该 SVG 的导入侧车，由首次 Godot 导入处理。
- 地图标签改读现有 manifest 或显式 `--text-source`，去掉对已删除章节 JSON 的依赖。全量室外生成需要 `--site-source`；人物生成器先检查原图和调色板，已知缺源时在写出资产前拒绝。
- `tools/audit_action_paths.py` 按七文件架构分类，历史删除清单改为可选 `--removed-manifest`。新增 `language_core/dv-check.json`，区分编译器、生产和独立检查闭包，生产不引入 oracle。
- `godot_csharp/NuGet.Config` 移除某台 Windows 电脑的绝对本地包源。共享配置使用 nuget.org，使新环境可以恢复工程依赖。

## 本轮验证

| 项目 | 实际结果与边界 |
| --- | --- |
| 游戏工程 | `dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false` 完成编译。首次因失效本地 NuGet 源失败；修复后重新构建成功，原错误记录保留。 |
| 语言工程 | `dotnet build language_core/LanguageCore.sln -c Release --disable-build-servers -p:NuGetAudit=false` 完成编译；构建调用现役 C0→C3 工具链与 C2/C3 固定点比较，生成 Session。 |
| DV 诊断 | 对 10、30、70 分别运行实际 C3 检查器，对应编译器、20–60 生产闭包和独立检查闭包；未报告编译诊断，不执行内容。 |
| Python 与配置 | 现役 Python 源码语法、修改模块导入、NuGet XML、DV 检查配置与 Git 空白差异核对完成。当前 manifest 的室内标签可解析且保留原文。 |
| 生成器边界 | 全量地图与四类人物生成入口在缺原始输入时明确拒绝，工作副本中既有场景和资源指纹未变。音乐导出用隔离临时目录及模拟 MuseScore 调用确认目录路由，未渲染音频。 |
| 主题 | 两个现役主题的旧/新生成文本逐字相同，仓内成品匹配。`build_ui_theme.py --check` 完成。 |
| 资源依赖 | 基于完整 2,082-blob 基线树核对删除、动态人物族、20 个地图、relief/normal 与侧车配对。保留引用中的 12 个翻译目标均有精确 CSV 导入来源；SVG 与预览旧引用在复核发现后修复。 |
| MCP | 同一 MCP 2.3.0 环境与未改协议，在允许本地 stdio 的环境中完成真实握手、工具发现、只读目录调用及 Host 路径拒绝。受限沙箱内最小空服务也超时，原失败记录保留，不借此修改或降级 SDK。 |
| 内容和设计 | 七份 DV、Runtime/Seed/Session 执行源、设计规范按原 blob 核验。没有改变剧情、规则、预算、答案或正式动作结算。 |

验证使用 .NET SDK 8.0.426、Godot.NET.Sdk 4.6.1 和 Python 3.12；MCP 临时依赖采用仓库声明的安装要求。编译与静态结果只说明对应工程事实，不代表游戏、画面、人物行为或性能改善。

## 首次运行与尚未覆盖的范围

1. 完整检出后先用 Godot 4.6.1 .NET 编辑器导入，例如 PowerShell：`& $env:GODOT --headless --editor --path godot_csharp --import`。本轮没有运行 Godot 资源导入、UI 截图或游戏会话，也没有运行流程回归。
2. 基线本来没有运行时 OGG。按 [音乐说明](../../../../music/README.md) 生成游戏音频；发行 MP3 不承担该路径。未安装 MuseScore 时的内置合成回退仍使用 `cues.py`，不能把 `--from-mscz` 当作强制原谱渲染保证。
3. 人物原图、调色板和完整室外场址/地形源仍未随基线提交。当前运行成品保留，生成器说明所需输入；本轮没有重建或主张视觉等价。
4. 静态审查以完整远程文件树为存在性依据；没有将未下载的二进制当作缺失文件。保留图像/字体侧车主要核对路径配对，未逐个重新导入。

报告中的静态引用复核保留审查时快照；`verification.json` 的最终修复记录明确标注 SVG、预览引用及 MCP 沙箱超时的处理，避免把较早的缺口误读为最终状态。
