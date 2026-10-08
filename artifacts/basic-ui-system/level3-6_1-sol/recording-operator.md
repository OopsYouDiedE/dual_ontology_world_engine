# 实时盲玩录像操作（无路线信息）

在仓库根目录使用 PowerShell。启动命令同时启动真实渲染游戏和后台实时录像；不要另启同一会话。

```powershell
Start-Process python -ArgumentList 'godot_csharp/tools/record_blind_session.py run --session=level3-sol61-20261008-01 --agent=/root/level3_blind_6_1_sol' -WindowStyle Hidden
python godot_csharp/tools/record_blind_session.py status --session=level3-sol61-20261008-01
```

第一次玩家输入前必须读取 recording.json，确认 `state=recording`、`recording_ready=true`；此状态只在编码帧数大于零后发布。每次输入前检查状态；failed 时停止输入并报告。不要关闭、最小化或调整游戏窗口尺寸，不要点击游戏内退出按钮来终止录像。

录像状态：`artifacts/basic-ui-system/level3-6_1-sol/level3-sol61-20261008-01/recording.json`。

玩家可见界面与通用输入：

```powershell
python godot_csharp/tools/blind_playtest.py --session=level3-sol61-20261008-01 status
python godot_csharp/tools/blind_playtest.py --session=level3-sol61-20261008-01 snapshot
python godot_csharp/tools/blind_playtest.py --session=level3-sol61-20261008-01 action --file=<输入JSON文件> --name=<本步描述>
```

只按当前截图和可见内容选择输入；每次改变界面后观察新截图再决定下一步。输入对象可为以下通用形式（坐标为游戏视口像素，ID来自当前快照）：

- `{"type":"click","x":400,"y":300,"button":"left"}`
- `{"type":"tap","key":"Space"}`：也接受 Enter、Escape 等键名。Space/Enter先显示完整当前对白，再次按下才推进。
- `{"type":"hold","key":"D","ms":800}`
- `{"type":"wait","seconds":2}`
- `{"type":"wheel","x":400,"y":300,"direction":"down","steps":1}`
- `{"type":"click_control","control_id":"当前快照中的ID"}`
- `{"type":"text_control","control_id":"当前可见输入框ID","text":"输入文本","replace":true}`
- `{"type":"text","text":"输入文本"}`：需要输入框已获得焦点。

结束、遇到问题或决定停止时，先运行以下命令正常封装录像，再报告。工具先停止编码，再关闭它自己启动的游戏窗口，验证视频可解码，复制全部会话记录；不要强杀FFmpeg。

```powershell
python godot_csharp/tools/record_blind_session.py stop --session=level3-sol61-20261008-01
```

最终状态应为 `complete` 且 `decode_verified=true`。视频为该目录中的 `gameplay.mp4`，过程日志、probe.json、decode.log和session/一并保留。原始输入、截图和时间线仍保存在 `artifacts/remake-belief-replay/blind-level3-sol61-20261008-01/`。

视频为真实墙钟15fps H.264 CRF29静音录像；本机没有可用的游戏回环设备，因此不采集麦克风。不使用按帧加速或截图合成视频。
