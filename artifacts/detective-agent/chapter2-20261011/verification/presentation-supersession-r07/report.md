独立中性机制合同检查通过。生产 `AgentPilot.gd` 在公开呈现等待期间撤下刚选的旧 token 后，记录 `superseded_action`、不提交旧 token，也不关闭席位；同一席位随后从正常 `_loop` 观察与决策路径提交新 token。没有发现本轮范围内的机制缺口。

本轮仅由独立 Agent 根据通用输入协议、根 AGENTS.md、godot_csharp/AGENTS.md 及允许的生产机制源码完成。未读取设计文件、正式 `.story`、历史试玩、三份全程记录或其他 Agent 的证据。测试使用正式 GDScript `AgentPilot.gd` 与自行编写的中性假 engine/host/UI；没有装载正式主场景、调用章节 setup、推进章节或运行 FullTest。

本轮证据：

- `presentation_supersession.gd` 为独立 SceneTree 场景脚本；`run_fixture.py` 为独立启动与原始字节封存入口。日志路径不可复用，后续轮次必须另取运行名。
- `run-01-stdout.log` 含每项合同的实际结果与完整 `PRESENTATION_SUPERSESSION_COMPLETE` 结束记录；`run-01-stderr.log` 为空。
- `run-01-results.json` 保留断言、实际值及生产席位发出的观察、决定、动作和替代回执；`run-01-metadata.json` 保留实际命令、退出码、生产文件前后 SHA-256 和日志 SHA-256。
- 运行退出码为 0，没有超时或脚本/引擎错误。Godot PID 26120 已结束并释放，主代理可以接用 Godot。

输入合同实测结论：

| 公开状态与输入 | 实际行为 | 结论 |
| --- | --- | --- |
| 当前有效且已选 token | `_current_input` 原样返回输入，`_submit_current` 再次查询后交给普通 host 提交 | 通过 |
| 已选 token 已过期，当前公开观察 Turn 更晚且候选非空 | 发出关联旧 token、决策 Turn、新 Turn 的 `superseded_action`；不提交、不中止、不开关席位 | 通过 |
| 菜单停顿期间发生上述替代 | `_act` 先读取有效 token，等待正常计时后由 `_submit_current` 重新核验，旧 token 不提交 | 通过 |
| 读取对白并收到正常 presented 回执期间发生上述替代 | 正常对白推进与回执收取结束后重新核验，旧 token 不提交，回复门闩清空 | 通过 |
| 更晚观察但候选为空；相同或更旧 Turn 但候选非空 | 保留过期边界原因，停止并关闭席位；不记为替代 | 通过 |
| 其他边界错误，即使观察更晚且候选非空 | 保留原边界原因，停止并关闭席位 | 通过 |
| 未被本席位刚选的未知 token，或没有已选 token | 停止并关闭席位；不能借新候选绕过错误边界 | 通过 |
| 旧候选被替代后的下一次正常决策 | 生产 `_loop` 依次公开观察 Turn 20、21，分别决定 alpha、beta，只提交 beta，席位仍开放 | 通过 |

FullTest 共用接口的静态复核通过，未发现接口缺失：

- `scenes/Shared/Field/WorldView.gd:31、36、39` 持有 adapter、player、sight；`setup()` 在第 87 行将同一 adapter 交给 `sight.host`。FullTest 的 `main.field` 正是这套现场对象。
- `scenes/Shared/check/FullTest/FullTest.gd:121–125` 将脚底 position 转为 32 像素格后读取 `adapter.sight_interior()`、`adapter.sight_region()`，并同时保存 `entered_room()`、`room_of()` 和 `sight.active_region`。`AwakeningMap.gd:232、236` 与 `CampusMapView.gd:233、246` 均提供这两个接口；`SightMask.gd` 使用相同的脚底接口与默认 32 像素格驱动可见区域。
- `FullTest.gd:127–130` 在调查、入室回执变化、`sight.active_region` 变化或截图间隔到期时调用 `_shot("decision", receipt)`，之后更新 `last_entered` 与 `last_visible_region`。`_shot()` 将 receipt 复制到截图记录，并增补真实图片文件名，因此新的 interior/feet_region 与 token 能关联同张图片。
- 该结论仅确认生产接口及分支接线，不确认实际截图、视觉区域或玩家站位的画面正确性。本轮没有运行 FullTest 或查看游戏图片。

生产版本固定且运行前后相同：

| 文件 | SHA-256 |
| --- | --- |
| godot_csharp/scenes/Host/AgentPilot.gd | 3c6c48826c33413a481d93f41720a32abeac6dacc404d7877e1d481ec8d18faf |
| godot_csharp/scenes/Shared/check/FullTest/FullTest.gd | d0fe959c6b37924819de21757bc89e9b0729cb1e32aeb10ab494f3340da38002 |

失败项：没有本轮实测失败。没有以检查数量替代行为结论。没有修改生产源码、AGENTS.md、Core、剧本或生成物，没有提交或推送。证据文件的原始字节由本目录 `.gitattributes` 的 `-text -diff` 保留，完整文件哈希另见 `sha256.json`。

构建结论：本轮不重复主代理已完成的 C# 构建与导航回归；正式 AgentPilot 及其预加载依赖由 Godot 4.6.1 .NET 实际装载，未报告 GDScript 错误。FullTest 只做静态接口复核。

验收范围：本轮为自动机制合同检查，一级剧情与引擎、二级 Godot UI 专项、三级游戏本体均未执行；人物行为合理性、文学质量与游玩体验未验证。本轮结论不代替正式连续会话、画面检查或独立盲玩。
