# 独立证据审查：hitch-walk-01

审查范围仅限本目录中的 `hitches.json`、`frame-gaps.csv`、`run/keyframes.jsonl`、`run/live-timing.jsonl`、`host-session/interaction-timing.jsonl`、`host-session/close-result.json`、`result.json`、`OPERATOR-NOTES.md` 及指定 PNG。未读取源码、设计文件或历史试玩记录；未修改原始证据。

## 计时口径与结果

`hitches.json` 将帧间隔定义为实际 `frame_post_draw` 回调之间不少于 50 ms 的间隔，并注明各分解是互斥墙钟区间，不能解释为 CPU 时间或因果证明。全程记录 1,143 个帧间隔，持续时间合计 147,896.254 ms；`frame-gaps.csv` 对应这些区间。逐区间相加 `exclusive_wall_ms` 分解后合计同为 147,896.254 ms，浮点误差约 0.00000003 ms，算术一致。

最长间隔为 896.844 ms（3,565,597–4,462,441 μs）。其分解为 unattributed_wall_time 812.410 ms、capture_PNG 78.435 ms、capture_metadata 2.940 ms、DV_choice 0.198 ms、host_delivery_other 1.767 ms、UI_apply 0.481 ms、diagnostic_journal 0.613 ms，合计 896.844 ms。未归属墙钟占绝大部分；这些阶段仍是墙钟归类，不是 CPU 根因证据。不能把该间隔概括成“DV/CPU 导致卡顿”或仅据此断言界面主线程是根因。

PNG 写入是单独记录的阶段：全程帧间隔分解中的 `capture_PNG` 合计 78,738.234 ms，`capture_metadata` 合计 2,136.048 ms。最长间隔内 PNG 阶段为 78.435 ms；PNG 开销不应并入 DV 或 UI 工作的描述。启动命令的最长墙钟耗时为 2,739.461 ms，其中 `session_open` 1,743.570 ms、`capture_PNG` 946.835 ms、未归属 21.919 ms，另含快照、元数据和日志等短阶段；这是启动命令的分解，不等同于纯 CPU 时间。

## 截图与场景边界

我实际查看了卧室截图 `run/frame-000026336.png`、走廊截图 `run/frame-000072736.png`，以及最长绘制间隔之后的 `run/frame-000004925.png`。画面分别显示卧室角色与“风衣 [E]”提示、走廊和引导对白、以及最长间隔后仍在卧室的角色与“床 [E]”提示。三张采样截图只能说明这些时点的可见内容；稀疏 PNG 未呈现明显卡顿，不能作为整段无帧级卡顿的证据。操作者记录也明确说明没有逐帧观察。

`keyframes.jsonl` 在初始记录中把侦探标为 `zone_yi:north_1`，并把 Huo、Shen、Su 标为 `zone_yi:hall`。这是离屏物理位置记录，不是“同伴已在截图中可见”的证据。初始画面截图中没有这些大厅同伴，不能用位置日志冒称截图确认其可见。操作者记录说明本轮最终未探索到大厅；因此没有大厅画面或同伴可见性确认。记录确实显示从卧室进入走廊、一次按 W 折返卧室后再返回走廊，但没有完成进入大厅。

## 结论与限制

计时分解算术自洽，PNG 写入作为单独墙钟阶段保留。证据足以指出若干长帧间隔及墙钟阶段的重叠，不能据此将重叠分解提升为 CPU 根因判断。截图支持卧室和走廊的局部可见状态；不支持“全程无卡顿”、大厅到达或同伴在画面中可见等结论。`close-result.json` 记录关闭完成、无未处理命令、无 pending action 且错误列表为空；`result.json` 记录采集窗口结束于 240,005 ms、游戏时间从 08:00 推进至 08:06。此处仅核验记录状态，不将关闭成功或窗口时长作为体验改善结论。
