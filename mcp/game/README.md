# 正式游戏 MCP

`server.py --service input` 提供独立会话的 `on`、`step`、`observe`、`close`。所有命令都在工程根目录运行。完整的窗口操作、五日流程和存档说明见 [五日试玩指南](../../docs/five-day-play.md)。

## 环境准备

需要已有 Python 3.12，以及 Windows x64 或 Linux x64。首次创建项目虚拟环境并安装固定的 MCP 版本；环境准备器会核验 Godot .NET 4.7.2、.NET 8 SDK 和版本化模型包。Python 本身、操作系统共享库和 Linux 显示服务不由准备器安装。首次下载需要访问 Python 包源、Godot 官方发行源、微软 .NET 源及 NuGet；选择 Blender 时还需要 Blender 官方发行源。

**Windows PowerShell：**

```powershell
py -3.12 -m venv .runtime/python
.\.runtime\python\Scripts\python.exe -m pip install -r mcp/game/requirements.txt
.\.runtime\python\Scripts\python.exe mcp/game/run_environment.py --local-copy
.\.runtime\python\Scripts\python.exe -u mcp/game/input_client.py
```

**Linux：**

```bash
python3.12 -m venv .runtime/python
.runtime/python/bin/python -m pip install -r mcp/game/requirements.txt
.runtime/python/bin/python mcp/game/run_environment.py --local-copy
.runtime/python/bin/python -u mcp/game/input_client.py
```

已经创建虚拟环境时从环境准备命令开始即可。需要编辑模型时，在 `run_environment.py` 命令末尾加 `--with-blender`，准备器会核验并安装 Blender 4.5.3；运行已发布场景不需要 Blender。Godot 优先使用项目目录中的安装，`--local-copy` 要求 Godot 位于项目目录；.NET 会优先使用项目中的 SDK，也可复用系统中已经有效的 .NET 8 SDK。

## 无桌面的 Linux 云主机

已有有效 `DISPLAY` 时，正式宿主直接使用该显示服务。没有 `DISPLAY` 时，宿主使用项目私有的 Xvfb 与 Mesa 软件渲染环境，至少需要以下实际安装路径及它们依赖的系统共享库：

```text
.runtime/linux-display/usr/bin/Xvfb
.runtime/linux-display/usr/lib/x86_64-linux-gnu/
.runtime/linux-display/usr/share/vulkan/icd.d/lvp_icd.json
```

当前云端五日验收使用这一私有环境，在云端真实渲染并生成观察图，没有连接或控制用户的本地桌面。`run_environment.py` 不负责安装这个显示包；它和 Python、Godot、.NET、Blender 的安装目录均不随源码提交。

对可正常使用系统 X11 的 Ubuntu 24.04 x64 主机，可以先用发行版包管理器安装显示依赖，再通过 `xvfb-run` 给正式客户端提供 `DISPLAY`：

```bash
sudo apt-get update
sudo apt-get install --no-install-recommends xvfb xauth mesa-vulkan-drivers
xvfb-run -a -s "-screen 0 1600x900x24" .runtime/python/bin/python -u mcp/game/input_client.py
```

这是标准系统环境的准备建议，尚未在另一台干净机器复演；实际验收采用的是上面的项目私有显示环境。禁止 Unix 域套接字的隔离执行器需要与当前私有环境相同的 TCP 显示配置，不能直接假定默认 `xvfb-run` 适用。没有图形显示的 CLI 全 AI 运行无需 Xvfb。软件渲染、高倍率和持续截图会影响墙钟耗时，验收期限不是目标硬件帧率承诺。

## 持久输入连接

已连接的 MCP 服务不会自动刷新工具参数；需要新参数时，启动新的持久客户端。每个代理使用自己的连接和存档槽。客户端每行读取一个 JSON 请求，并在同一连接内保留一个会话：

```json
{"tool":"on","arguments":{"backend":"godot","seed":1,"viewer":"detective","content_path":"assets/data/scenarios/campaign.dv","background_capture":true,"save_slot":"five-day-player"}}
{"tool":"step","arguments":{"operation":{"type":"start"}}}
{"tool":"observe","arguments":{}}
```

先读取上一条返回中的完整观察，再按当前合法行动提交下一条 `step`。正常结束发送 `{"tool":"close","arguments":{}}`。标准输出显示摘要及回执路径；客户端为后续请求携带当前会话 ID，将原始返回和图像写入 `.runtime/input-client/<连接ID>/`。EOF 会尝试关闭尚存的会话。保留相同 `save_slot` 才能在新连接中读取同一存档；已有进度应使用 `load`，不要再次发送 `start`。

桌面环境中把 `background_capture` 设为 `false` 可以直接操作可见窗口；无桌面的云环境仍通过正常观察与输入接管真实 Godot 游戏。手动接管不会由建议界面自动选行动或证据。全 AI 则使用独立的正式自动入口：

```bash
.runtime/python/bin/python mcp/game/run_auto.py --backend godot --duration-seconds 7200 --seed 1 --content-path assets/data/scenarios/campaign.dv --receipt .runtime/runs/five-day-godot-auto.json
.runtime/python/bin/python mcp/game/run_auto.py --backend cli --duration-seconds 600 --seed 1 --content-path assets/data/scenarios/campaign.dv --receipt .runtime/runs/five-day-cli-auto.json
```

Windows 使用上面的 `.exe` Python 路径；通过系统 Xvfb 运行时，给 Godot 命令加同样的 `xvfb-run` 前缀。期限包含构建和启动，`deadline_reached` 不构成通关；必须核对最终正常观察中的 `campaign_completed=true`，并保持报告所记录的宿主、源码版本与覆盖范围。

## 录像

后台 Godot 会话可在 `on` 的 arguments 中增加 `"movie_filename":"example.avi"` 和可选的 `"movie_fps":30`。文件名只接受简单 AVI 名称，保存在项目 `artifacts/recordings/`，已有文件不会覆盖。正常 `close` 等待 Godot 退出封装，回执给出 `graceful_exit`、`movie_finalized`、路径和大小；异常中断不能当作录像完成。Godot 原生 Movie Maker 使用固定步进，录像不能作为实时帧率或卡顿基线。
