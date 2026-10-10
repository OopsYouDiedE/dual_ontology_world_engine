# 共享控制器与位置元数据机制验证

本轮由新建独立验证 Agent 执行，只读取适用 AGENTS.md、相关源码与合同工具，未读取设计文件。未改生产文件、Core 或剧本，未提交或推送。构建与机制自动回归通过；一级全程记录、二级 UI 专项和三级独立盲玩均未执行，人物行为合理性、文学质量和实际游玩体验均未验证。

`PlayerControllerCheck/Program.cs` 增补中性夹具，保留已有 frontier 合同与其原期望。`neutral-observer` 带探索偏好，`neutral-feedback-only` 没有 untried 偏好；它们都在实际位置未变后改选可行替代，获得新采信材料后仍保留先前碰壁依据。拥有探索偏好的角色不会把请求落点当作已经走过的地面，新材料使原先暂缓的交互重新进入选择。空间切换后，相同坐标不会继承旧空间的碰壁、已走地面、输入尝试或已见线索地点记忆。远处的未回答目标仍使较近方向优先，候选排列交换不会改变该选择。

正式 `PlayerCandidateSession.Locate → Observe → Submit` 往返保持实测位置；提交请求中的目标坐标不会冒充位置，刷新后元数据保留，私有路径负载没有进入公开观察。空空间、超长空间、NaN 与无穷位置以 `invalid_field_observation` 拒绝，并关闭边界。

| 验证入口 | 结论 | 本轮证据 |
| --- | --- | --- |
| 宿主解决方案构建 | 通过，无编译错误或警告 | `host-build.log` |
| 控制器回归构建与运行 | 最终通过，保留原有 frontier 与隐真变形期望 | `controller-build-final.log`、`controller-run-final.log` |
| Python 控制器编译合同 | 通过 | `controller-python.log` |
| 策略集成合同构建与运行 | 通过，使用真实 Python 剧本编译往返 | `integration-build.log`、`integration-run.log` |
| 剧本源与生成物一致性 | 通过 | `story-check.log` |
| 中性 Godot 导航合同 | 修复后通过，未知短步不预读未知碰撞与路线 | `godot-navigation-retry1.log` |
| 中性 Godot 出口通行合同 | 通过，关闭出口实际碰撞、开放恢复通行、拒绝换图退回并取消路径 | `godot-campus-access.log` |

首次控制器构建失败来自本代理新夹具的 params 目标类型推断与集合重载歧义；只修正夹具语法，保留 `controller-build.log`。首次控制器运行的远距离夹具把待调查问题和机会写成已采信命题，导致其走入已有答案分支；将夹具修为未采信 Unknown，近远排序期望保持不变，保留 `controller-run.log`。首次 Godot 导航运行遇到 `PilotNavigation.gd:12` 动态 has 结果的类型推断错误；主代理改为显式 bool 后重跑通过，失败记录保留在 `godot-navigation.log`。挂起时只终止已核对命令行的本轮 navigation 子进程，未操作用户编辑器 PID 23084。

Godot 合同串行执行，没有运行 FullTest。验证结束时，本代理没有存活的 Godot 进程，编辑器继续保留。中性出口夹具中的初始位置准备和控制器认知快照构造属于机制验证，不是游玩、剧情或 UI 验收证据。`AgentPilot` 的完整正式游戏运行、地图源 ID 到不透明标识的运行时分配、人物表现和持续游玩反馈由主代理另行验收。

每次执行的命令、UTC 起点、结束码、耗时与日志 SHA-256 存于同名 JSON。原始合并输出直接写入二进制日志，没有删减或换行重写；局部 `.gitattributes` 以 `-text -diff` 保留日志字节。`source-hashes.json` 记录封存时相关源码状态，`evidence.sha256` 汇总本目录文件哈希。所有失败与复测文件独立保留。
