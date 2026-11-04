# 正式游戏 MCP

`server.py --service input` 提供独立会话的 `on`、`step`、`observe`、`close`。已连接的 MCP 服务不会自动刷新工具参数；需要新参数时可运行持久客户端，启动新的正式 stdio MCP 连接：

```powershell
.\mcp\game\.venv\Scripts\python.exe -u mcp/game/input_client.py
```

客户端每行读取一个 JSON 请求，并在同一连接内保留一个会话。先发送 `{"tool":"on","arguments":{"backend":"godot","background_capture":true}}`，再按最新观察发送 `{"tool":"step","arguments":{"operation":{...}}}` 或 `{"tool":"observe","arguments":{}}`；结束发送 `{"tool":"close","arguments":{}}`。标准输出只显示摘要及回执路径；操作前应读取该路径中的完整最新观察。客户端为后续请求携带当前会话 ID，将原始返回和图像写入 `.runtime/input-client/<连接ID>/`。EOF 会尝试关闭尚存的会话。

后台 Godot 会话可在 `on` 的 arguments 中增加 `"movie_filename":"example.avi"` 和可选的 `"movie_fps":30`。文件名只接受简单 AVI 名称，保存在项目 `artifacts/recordings/`，已有文件不会覆盖。正常 `close` 等待 Godot 退出封装，回执给出 `graceful_exit`、`movie_finalized`、路径和大小；异常中断不能当作录像完成。Godot 原生 Movie Maker 使用固定步进，录像不能作为实时帧率或卡顿基线。
