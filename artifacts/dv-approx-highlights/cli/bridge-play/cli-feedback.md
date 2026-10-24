# CLI 有限人工体验（传输桥）

本轮只开一局正式 `formal-model` 会话，控制者为人工、检阅者为侦探。源码基线由主代理给定为 `df87a9d0`，叠加工作区未提交的 CLI、会话性能与元数据、Python 传输桥改动；本 Agent 未读取设计或剧情源码，未构建或改动程序。入口为 `python -u mcp/verification/cli_player_bridge.py serve --out artifacts/dv-approx-highlights/cli/bridge-play`，桥 URL `http://127.0.0.1:62577`；每次输入都通过现成 `request` 命令发送，`request_id` 与 `command_complete` 配对。`quit` 返回 `exit_code=0`，服务进程亦以 0 退出。

## 实际输入和可见结果

| 顺序 | 输入 | 本人可见结果 |
| --- | --- | --- |
| 1–2 | `status`、`options` | 08:00，乙区卧室 R-05，idle。候选含出门、床边回想、E11/E12 检视。 |
| 3 | `act detective.opening.wake` | 先逐步接续一项恶魔开场动作，再一次完成侦探床边回想；游戏时间 08:01。回想正文三行，但本次初期客户端控制台编码错误，中文原文不可可靠转录。 |
| 4 | `act detective.inspect.E11` | 两项屏外行动先于侦探行动结算，返回给侦探的投影均称未收到新可见信息。E11 先排程，再在同一请求中出现第二组 focus/confirm，最后只有一次 E11 可见检视回执 `e136` / `action5`；08:02，仍在 R-05。初期中文同样乱码。 |
| 5–7 | `act detective.invalid.NO_SUCH_ACTION`、`status`、`options` | 无效 ID 返回 `success=false`、`ArgumentException`；随后状态仍 active、idle、版本 14、08:02，候选可继续查询，E11 从候选消失。 |
| 8 | `act detective.opening.wear-coat` | 一次 focus/confirm 和一次本人可见回执；08:03，仍在 R-05。 |
| 9–10 | `act detective.move.R05.corridor`、`options` | 抵达乙区宿舍走廊，正式移动回执 `e155` / `action7`，08:05；走廊显示一名“陌生学员·4”。可选前往其他卧室或活动厅。 |
| 11–14 | `focus detective.move.corridor.hall`、`confirm`、`status`、`options` | 聚焦后版本 21；确认仅排程，版本 22、08:07，人物仍在走廊，候选记录该行动为 `scheduled`。此后客户端中文输出已恢复正常。 |
| 15–18 | `next`、`confirm`、`next`、`next` | 先处理一项屏外待结算行动，本人未收到新可见信息；再聚焦同一已选活动厅移动并确认，只有一次移动回执 `e180` / `action9`，抵达乙区活动厅，08:07。显示两名陌生学员。 |
| 19 | `quit` | `success=true`、`closing=true`，CLI `exit_code=0`，stderr 为空。 |

初期中文乱码来自 Windows Python 请求客户端 stdout 的代码页，主代理在本局进行中修正该客户端输出编码；桥服务和 CLI 会话未重启。修正后 `status/options/focus/confirm/next` 的中文可正常阅读。初期乱码限制了我对开场台词、回想和 E11 检视文案的体验判断，不据此推断剧情内容有错。

## 体验与证据边界

`act` 及 `next` 在排程、屏外行动和本人 Complete 之间显示多个阶段，语义可循，但同一选项的 focus/confirm 可能重复出现在一次 `act` 的输出里，需要依 `scheduled`、实际执行事件和最终位置辨别是否真的做了两次。本轮 E11 只看到一次本人检视结果；活动厅移动也只看到一次到达结果。屏外行动的“未收到新的可见信息”在短时间内重复，信息量低。单次请求实测约 0.5–5.8 秒，整局采样现实 108.830769 秒；未观察到请求卡死。正式游戏从 08:00 到 08:07，推进 7 游戏分钟。这只是一段有限人工试玩，未检验谜题、章节交接、画面、碰撞或整局体验。

关闭后 `session.dvdag`、`session.dvsave`、`performance.dvlog` 和性能报告均存在，未留本目录 `.partial` 文件；CLI 与桥均正常退出。`performance.md` 报告 `simulation_mode=approximate-v1`、近似模式为 true，10 次正式选择（人工 5、引擎 5）、10 次完成、在途 0，trace/durable 的游戏时刻均为 487，`selections_not_in_save=0`。这些是性能报告与文件落盘的核对；本 Agent 没有独立解析二进制 seal 或执行回放校验，故不把存在文件等同于逐字节验证完整性。

本轮 CLI、来源元数据及传输桥源码随后收录于提交442080ba；采样开始时仍为工作区内容。正式原始轨迹/存档在本地同目录保留，报告没有替代二进制记录。
