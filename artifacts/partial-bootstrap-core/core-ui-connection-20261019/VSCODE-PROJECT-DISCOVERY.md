# VS Code C# 项目发现配置（2026-10-19）

本次仅修改 `.vscode/settings.json`，保留原有编辑器、Python、Godot、CodeLens、GitLens 等设置。未删除、搬移或改写任何历史工程、证据、源码、solution 和生成物；未启动 VS Code、重载语言服务、构建或执行会话。AGENTS/README 由主代理统一维护。

## 真实来源

静态枚举使用 `rg --files --hidden --no-ignore`，限定 csproj、sln、slnx、code-workspace，并排除 bin/obj 内部派生文件以免重复统计。这里的数量说明磁盘项目发现来源，不作游戏评价。

| 根目录 | csproj | solution | 来源 |
| --- | ---: | ---: | --- |
| artifacts | 288 | 4 | 封存版本、program 输出、历史探针与测量工程 |
| tmp | 127 | 10 | 基线检出、solution-review-workers、临时诊断和探针 |
| scratch | 80 | 4 | r20-recovery、short-r21 旧检出及临时程序 |
| godot_csharp | 1 | 1 | 现役 Godot 宿主 |
| language_core | 3 | 1 | Session、compiler/seed、Visual Studio 编辑器工程 |

代表性非现役来源包括 `artifacts/partial-bootstrap-core/verify-5days-240s-r9/program/Program.csproj`、`tmp/solution-review-workers/M01/` 和 `scratch/short-r21/`。普通受 ignore 限制的枚举只返回216个项目/solution相关文件，而显式包含 ignored 的枚举为519个；因此仅靠 Git ignore 不能说明编辑器不会接触这些历史文件。

## 本机设置依据

实际读取以下已安装扩展的 package.json 与 package.nls.json：

- `C:/Users/iii/.vscode/extensions/ms-dotnettools.csharp-2.160.4-win32-x64/`
- `C:/Users/iii/.vscode/extensions/ms-dotnettools.csdevkit-3.40.210-win32-x64/`

C# schema 声明 `dotnet.defaultSolution` 为 string，描述为工作区有多份 solution 时默认打开的 solution 路径。C# Dev Kit schema 声明 `dotnet.preferVisualStudioCodeFileSystemWatcher` 为 boolean，其本地描述建议配置 files.watcherExclude 时使用该选项。OmniSharp 兼容设置 `omnisharp.projectFilesExcludePattern` 为逗号分隔模式的 string。当前两个扩展公开 schema 未提供独立的现代 solution 扫描排除设置。

另静读本机 C# `dist/extension.js`：其 `excludePaths` getter 读取 `files.exclude`，defaultSolution 以工作区路径解析相对值。此事实支持这些设置有实际消费者；不能据此断言所有 C# 语言服务内部扫描、显式打开历史工程和第三方扩展都会遵守同一排除。

## 具体配置

- `dotnet.defaultSolution = godot_csharp/DualOntologyWorldEngine.sln`，锁定正式宿主 solution。该文件已包括 Godot 工程与 language_core/Session，无需新增重复根 solution。
- `dotnet.preferVisualStudioCodeFileSystemWatcher = true`，让 Dev Kit 优先使用能应用 VS Code watcherExclude 的文件系统监视。
- files.exclude 隐藏仓库根 artifacts、tmp、scratch；search.exclude 和 files.watcherExclude 排除相应 `目录/**`。文件原样保留，直接路径打开和独立打开历史检出仍可用。
- omnisharp.projectFilesExcludePattern 保留其 schema 默认 node_modules/.git/bower_components，并追加 artifacts/tmp/scratch，作为显式切换 OmniSharp 后的兼容配置。

没有排除 language_core、Session、compiler/seed，也没有新增全仓 bin/obj 排除。Session 实际显式编译 `obj/dv/DVProgram.cs`，本次保留其源生成文件的现有发现/监视行为，避免为清理历史项目而屏蔽现役生成代码变化。

默认 solution 不包含 Seed，因为默认编辑目标为实际 Godot 宿主。需要编译器工程视图时可用 C# 的 solution 选择入口切到 `language_core/LanguageCore.sln`；该现役 solution 包括 Seed 与 Session，未被任何新增排除模式覆盖。Visual Studio 编辑器工程也保留在原路径。

## 工程审阅范围与限制

已用 PowerShell JSON 解析读回 settings，并静读默认 solution 的工程条目和 Session 的显式生成源引用。未验证 VS Code 重载后的实际项目树、语言服务日志、CPU 或文件监视量，也未声称几百工程已经在正在运行的编辑器中卸载。设置应在下次打开仓库或重载现有 VS Code 窗口后由扩展读取；实际加载结果仍待该应用侧检查。
