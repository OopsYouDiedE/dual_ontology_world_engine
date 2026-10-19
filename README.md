# dual_ontology_world_engine

正式游戏只使用编译 DV 的动作实现：`Prepare → 现场执行/世界法则回执 → Complete → 呈现`。主体决策、费用、效果、观察、传播与结算由 DV 完成，Godot 负责物理移动、输入、画面和音效。

人工探索使用WASD或鼠标在当前房间内接近，连续碰撞与寻路由宿主执行，不扣DV时间或行动次数。靠近物件或人物按E/空格/Enter交互，多项动作只在该接触点显示局部选择；跨房间在门口提交原DV移动候选，真实执行成功后结算。等待输入不弹出全局动作列表。R/T访问随身或等待操作，实际可用内容仍由DV资格表提供。

| 目录 | 职责 |
| --- | --- |
| language_core/core、verification/session | 唯一动作机制与主体推演 |
| language_core/game、verification/scenarios | 原生 DV 生产内容 |
| language_core/compiler、Session | DV 编译器、平台 ABI 与序列化边界 |
| godot_csharp | 场景、物理与纯呈现宿主 |
| docs/design-standard.md | 唯一设计规范 |
| artifacts | 封存证据与阶段报告 |

旧 C# Core、旧解释链、旧章节会话和旧 AI 坐席及其运行工具已移除，源码可从 Git 历史查阅。旧 `.story`、译文和历史证据保留作迁移来源。现场移动只有 Main → DvFieldExecutor → WorldView.execute_move → 完成回执；没有旧 stage 执行分派。命令行逻辑执行器复用 DV prepare/complete，不替代 Godot 现场验收。

```powershell
dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false
dotnet build language_core/LanguageCore.sln -c Release --disable-build-servers -p:NuGetAudit=false
python language_core/tools/check_dv.py --format json <source.dv>
```

通过环境变量 GODOT 指定 Godot 4.6.1 .NET，正常启动使用 `--path godot_csharp`。构建时可用 `-p:DvPython=<Python路径>` 指定生成器。

工作约束见 [AGENTS.md](AGENTS.md)，完整语法与特性见 [DV Core 参考](language_core/core/README.md)，宿主接口见 [DV Session](language_core/Session/README.md)。禁止流程回归，不以构建结果证明游戏验收。一级使用 `python language_core/tools/run_debug.py --out artifacts/partial-bootstrap-core/<新目录>` 运行无窗口正式逻辑会话；三级保护测量工具仍待重建。旧路径清理证据见 [报告](artifacts/partial-bootstrap-core/single-action-path/report.md)。

玩家与 AI 侦探共用正式宿主。游戏启动参数在 `--` 后传入，例如：

```powershell
& $env:GODOT --path godot_csharp -- --dv-controller=player --dv-speed=4
& $env:GODOT --path godot_csharp -- --dv-controller=ai --dv-speed=4
```

Main 场景也可通过 controller、viewer、campaign_options 导出属性设置；只在打开会话前生效。可选 `--dv-auto-start` 跳过标题页的开始按钮，不自动选择玩家候选。缺失几何、人物资源或站点会产生失败回执，不能作为成功移动。

VS Code 默认打开 `godot_csharp/DualOntologyWorldEngine.sln`（游戏与 Session），语言工具使用解决方案选择器切换 `language_core/LanguageCore.sln`。历史 artifacts、tmp、scratch 保留在磁盘，工作区排除其自动发现与监视，避免测试快照被当作现役工程。

宿主播放默认四倍速，可用 `--dv-speed=1` 或 Main.playback_speed 调整。倍率应用到Godot时间，物理帧频随倍率增加以保持单步物理间隔；DV分钟费用、预算和判断仍由原动作结算。输入证据记录所用倍率和物理帧频，四倍播放不作为Core纯计算性能提升的证据。

普通实机游玩使用精简事件记录；需要完整来源快照时加 `--dv-audit`。`profile_dv_capture.py`已固定带该参数。普通模式仍保留实际输入、可见正文、分段计时与存档，关闭清单注明记录策略；两种模式的速度和证据完整性分别解释。房间镜头、局部光影与对白布局本批已修改，按用户要求暂未验证。
