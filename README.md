# dual_ontology_world_engine

DuelView（DV）语言与 Godot 游戏宿主。正式动作由编译后的 DV 执行：`Prepare → 现场执行/世界法则回执 → Complete → 呈现`。DV 负责主体决策、费用、效果、观察、传播与结算；Godot 负责物理移动、输入、画面与音效。

## 工程结构

| 路径 | 职责 |
| --- | --- |
| `language_core/10-compiler.dv` | DV 编译器 |
| `language_core/20-core.dv`、`30-session.dv` | 通用机制、认知与会话调度 |
| `language_core/40-chapter1.dv`、`50-chapter2.dv` | 原生 DV 生产内容 |
| `language_core/60-game.dv` | 组合生产依赖与宿主入口 |
| `language_core/70-checks.dv` | 独立检查、oracle 与夹具；不进入生产编译闭包 |
| `language_core/compiler/`、`Session/` | 最小自举编译器、平台 ABI 与 .NET 会话边界 |
| `godot_csharp/` | 场景、物理与呈现宿主 |
| `music/` | 可编辑乐谱与音频生成工具 |
| `docs/design-standard.md` | 唯一设计规范 |
| `artifacts/` | 任务报告与证据；发行副本放在被忽略的 `artifacts/releases/` |

当前宿主接线是 `GameEngine → DvSession → 生成的 DV 类库`。旧 C# Core、解释链、旧章节会话及运行工具已退役，迁移来源可从 Git 历史查阅。

## 构建与启动

需要 .NET 8 SDK、Godot 4.6.1 .NET 和 Python 3。构建会调用 DV 工具链生成 Session 源码：

```sh
dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false
dotnet build language_core/LanguageCore.sln -c Release --disable-build-servers -p:NuGetAudit=false
python language_core/tools/check_dv.py --format json language_core/30-session.dv
```

可用 `-p:DvPython=<Python路径>` 指定构建时的 Python。工程从 nuget.org 恢复依赖；个人离线包源应在本机配置，不将用户目录的绝对路径提交到共享 `NuGet.Config`。VS Code 默认打开游戏解决方案；维护编译器或 seed 时，切换到 `language_core/LanguageCore.sln`。VS Code、Visual Studio 2022 与 Godot 的 DV 编辑器接入说明见 [语言工程 README](language_core/README.md)。

Godot 首次打开工程时导入资源。CSV 本地化源及其 `.csv.import` 设置保留在版本控制中，生成的 `.translation` 由 Godot 重建；资源 `.import` 设置与需要的 `.uid` 应保留。游戏 OGG 音频需按 [音乐构建说明](music/README.md) 生成。地图完整重建的原始素材前提见 [Chapter2 资源说明](godot_csharp/ShareAsset/Internal/Props/Chapter2/README.md)。

PowerShell 启动示例，`GODOT` 指向 Godot .NET 程序：

```powershell
& $env:GODOT --headless --editor --path godot_csharp --import
& $env:GODOT --path godot_csharp -- --dv-controller=player
& $env:GODOT --path godot_csharp -- --dv-controller=ai --dv-speed=4
```

游戏参数放在 `--` 后。默认一倍速；`--dv-speed=` 调整宿主播放速度，DV 费用与预算仍按正式动作结算。`--dv-auto-start` 跳过标题页开始按钮。`--dv-audit` 启用完整来源快照；普通游玩的精简事件策略与审计策略分别记录，性能和证据完整性分别解释。

## 操作与执行边界

人工探索使用 WASD 或鼠标在当前房间内接近目标，连续碰撞与寻路由宿主执行，不扣 DV 时间或行动次数。靠近物件或人物后按 E、空格或 Enter 交互，多项动作在接触点显示局部选择；R/T 访问随身或等待操作，可用内容由 DV 资格表提供。跨房间在门口提交原 DV 移动候选，真实执行成功后结算。局部行走本身不授予观察或答案，缺失几何、人物资源或站点须返回失败回执。

## 维护与验证

工作规则见 [AGENTS.md](AGENTS.md)，语言与实现状态见 [语言工程 README](language_core/README.md)、[语法参考](language_core/SYNTAX.md)，宿主接口见 [DV Session](language_core/Session/README.md)。

`python tools/audit_action_paths.py --out <新JSON路径>` 提供静态路径审计。编译、语法与资源生成检查用于工程排错；游戏与人物行为结论需要对应正式会话证据。项目禁止流程回归，也不以断言数量或通过率评价游戏。正式无窗口逻辑会话、Godot UI 检查与完整游戏验收按 AGENTS 中的触发条件分别执行。
