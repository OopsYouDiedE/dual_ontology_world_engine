# 双重本体世界引擎 · 五日侦探原型

Godot .NET / C# 的五日可玩流程，正式入口为 `scenes/main/Main.tscn`。剧情、人物驱动力、认知、推演规则与事件锚点由 `assets/data/scenarios/campaign.dv` 及其引用的 DV 文件声明，房间沿用项目的建筑拓扑与已有场景、人物资产。

## 启动游戏

以下命令适用于 Linux x64，使用 Python 3.12。Windows x64 的对应命令及完整操作说明见 [五日试玩指南](docs/five-day-play.md)。

```bash
python3.12 -m venv .runtime/python
.runtime/python/bin/python -m pip install -r mcp/game/requirements.txt
.runtime/python/bin/python mcp/game/play.py --with-blender
```

启动器通过正式环境 MCP 安装或验证项目内 Godot .NET、.NET SDK 与 Blender，校验并还原场景模型，编译 C#，执行 Godot 资源导入，再打开游戏。首次启动需要网络下载依赖；后续复用校验通过的本地安装。标题页选择“新游戏”进入新局，“读取”继续正常存档。可用 `--ai` 打开全员自主模式，或在游戏中点击“交给 AI / 接管侦探”；用 `--editor` 打开 Godot .NET 编辑器。

Linux 运行图形宿主还需要可用的 X11 显示与 Vulkan 驱动。无桌面的云端须先准备虚拟显示；本轮实际验收使用项目私有 Xvfb 与 Mesa 软件渲染，未操作用户本地桌面。显示依赖的准备条件见 [正式游戏 MCP](mcp/game/README.md)，引擎准备命令不会代替操作系统显示依赖的安装。

## 玩法

点击人物、家具或右侧目标选择行动，侦探先沿真实可行走路径到达交互位置，再提交行动。WASD 移动，鼠标滚轮缩放；Tab 打开案卷，M 查看已经获知的空间关系。搜证会改变人物所知，并解锁后续行为；脑内推演需要选择已掌握的证据。NPC 日常行为由驱动力评估，接受建议另由独立的意见评估决定。言灵需要已证明的认知，会消耗资源、留下反噬，并在持续时间结束后失效。

侦探分别拥有任务规划路径与身体需要路径。紧急需要可以打断任务，实际完成补给并返回调查处后继续；手动接管时，这两条路径提供建议，行动与推演选证仍由玩家决定。界面区分建议与实际正在执行的路径，日记保留真实行动来源。当前房内的导航时间由 Godot 物理移动决定，跨房间路程由 Core 计时；开门后经过短暂过渡，从目标房间的真实入口进入，始终只加载一个房间。

前四天包含开局搜证与共同晚餐、粮仓协作与配给、文契／白塔／铁工／星仪四院实地调查、三种可能的人为案件、灾害救援及路线博弈。四院使用建筑拓扑中的真实接驳关系，各专业 NPC 实地巡检后留下签名记录，侦探将现场搜证与记录交叉推演。第五天取消剧情任务指示，继承此前的伤情、物资、认知和关系，人物自行选择补给、护理、休息、个人活动与交流。煮饭、饮水和清洗敷料会竞争资源，实际取用、携带、配送和接受建议会改变其他人的选择条件。

五日时间走完后显示“观察窗口结束”，不要求全员签字或按固定顺序入睡。案卷保留自己实际获得的线索、推演及事件；是否出现自主合作、改道或反馈，需要查看真实行动及其来源。界面提供暂停、时间倍率、手动保存与读档。第五天规则改变了新局的世界声明，请从标题页开始新游戏。

## 正式验证入口

```bash
.runtime/python/bin/python mcp/game/run_auto.py --backend cli --duration-seconds 600 --content-path assets/data/scenarios/campaign.dv --receipt .runtime/five-day-cli.json
.runtime/python/bin/python mcp/game/run_auto.py --backend godot --duration-seconds 7200 --content-path assets/data/scenarios/campaign.dv --receipt .runtime/five-day-godot.json
.runtime/python/bin/python -u mcp/game/input_client.py
```

自动运行使用正式宿主和新局，接管试玩通过持久输入 MCP 的 `on / step / observe / close` 操作。同一 Core 同时服务 CLI 和 Godot。命令、存档和交互协议详见 [试玩指南](docs/five-day-play.md) 与 [游戏 MCP](mcp/game/README.md)。观察窗口结束只表示时间范围完整，不能单独证明调查、玩法或自主交互已经成功。

## 资源与开发

必要模型以 `assets/packs/` 下带 SHA-256 清单的分片压缩包保存，准备环境或构建游戏时自动还原。`tools/building/publish_campaign_rooms.py` 发布房间与共享组件；四院家具布局由 `tools/building/designs/courtyard_prototype_rooms.json` 声明，可用发布器的 `--compositions-only` 单独重建。`tools/building/pack_campaign_assets.py` 重建模型包。当前 37 个逻辑空间使用 36 份独立房间场景；四院复用已有模型。引擎、Python 虚拟环境、Blender、构建缓存和原始运行日志均放在生成目录，不作为源码提交。人物动作使用 KayKit 免费 CC0 动作包，来源及许可证随 `assets/imported/kaykit/animations/` 保存。

剧情作者工具位于 [tools/story](tools/story/README.md)。工具从实际 DV 导出可检查的调查、推演、协商、资源代价与返程链，并生成供 AI 创作使用的约束提示；生成候选须经过内容检查、人工文学审阅和正式实机验证。运行日志、截图与验收报告保存在开发环境，不随游戏资源发布。
