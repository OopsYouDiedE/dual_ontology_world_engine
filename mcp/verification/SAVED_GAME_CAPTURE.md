# 存档辅助的实机采集

这两个工具使用生产 Main、GameEngine 和真实 Godot 现场。侦探模式由引擎选择行动；采集器只用普通 Space 输入读完已经显示的正文。工具不提供路线、答案、状态注入或替代物理执行。

## 准备

从仓库根目录执行。把 `GODOT` 设为本机 Godot 4.6.1 .NET 可执行文件路径，或为启动器传 `--godot`。启动器调用 `mcp/godot_workspace.py` 创建临时工具工程，顺序完成 C# 构建和资源导入，再冻结采集指纹并启动渲染进程。

```powershell
$env:GODOT = "C:/Tools/Godot/Godot_v4.6.1-stable_mono_win64.exe"
```

缺少游戏音频时按 [音乐管线说明](../music_pipeline/README.md) 使用已有曲谱生成。FrameArchive 只编入工具工程，通过 `res://tools/FrameArchive.cs` 加载；生产项目不包含采集程序集。原有存档和证据保留。

## 新开局与检查点

```powershell
python mcp/verification/run_saved_game_capture.py --out artifacts/gameplay-capture/new-001 --seconds 180 --mode detective --speed 4 --checkpoint 30,80,130
```

每次使用全新的输出目录。默认窗口离屏但正常渲染；`--window-mode window` 显示窗口。帧取证启动器显式启用 `--dv-audit`，保留宿主计时与可见记录。这个配置与普通游玩的精简记录分别报告。

`--checkpoint` 的数字是从采集开始计算的现实秒，只表示请求时刻。Main 等当前真实动作、Complete、正文与地图装配完成后，暂缓下一次 Prepare，再调用生产 `SaveBookmark`。保存实际发生时间写入二进制事件；正文较长时会晚于请求时间。保存后通过原延后输入队列继续推进。书页已经占用且边界未落定时仍须先关闭书页、读完正文，不能绕过安全门限。

每份书签在 `user://saves` 新建，不覆盖旧文件；采集目录中的配对副本为：

- `run/checkpoints/checkpoint-001.dvsave`
- `run/checkpoints/checkpoint-001.casebook.dvlog`

副本检查源/目标存档哈希，要求两份文件均存在。复制失败记录 `copy_failed`、停止后续检查点采集并使该轮返回失败；保留生产原件和已有副本供排查。

## 从检查点继续

```powershell
python mcp/verification/run_saved_game_capture.py --out artifacts/gameplay-capture/resume-001 --seconds 90 --speed 4 --load artifacts/gameplay-capture/new-001/run/checkpoints/checkpoint-002.dvsave --checkpoint 30,60
```

载入使用生产 `load/path`，保留存档的控制者、世界状态与绑定案卷。倍率只配置宿主播放、物理步频与每帧物理步数；4 倍对应 240 Hz 和最多 32 步。默认倍率为 1。`--mode` 对新开局选择控制者，不能把已保存的 AI 会话改成玩家会话。

恢复前核对来源轮次、完整 Git 提交、源码/程序集/运行资源指纹及配置。源码变化后的恢复注明为兼容性诊断；最终新开局另跑。关闭时的自动 `save.dvsave` 取 Session 最近已提交的稳定状态；现场仍有 prepared 动作时，完整轨迹保留该动作，存档回到最近可恢复边界。显式书签继续等待生产安全边界并绑定案卷。

## 证据与失败判定

启动器封存 `source-before.json`、`source-after.json` 与 `launch.json` 等工程元数据，包含完整提交、命令、实际进程和资源指纹。源码范围包括 `dv_core`、生产宿主、构建工具及采集器；程序集取自实际临时工程的输出，同时记录该工程配置与导入资源。导入日志为 `asset-import.log`。构建和导入发生在冻结指纹之前，临时工程在启动器退出时清理。

原始输入、宿主事件、快照、计时、帧索引、检查点与关闭结果使用 `.dvlog`，世界存档使用 `.dvsave`，截图使用 PNG。

`result.dvlog` 记录进程退出、脚本错误、加载错误、源码变化、检查点是否完整及宿主封存结果。输出目录已有内容、错误、超时、缺少结束记录或未完成请求的检查点都不能算成功。采集结束与剧情结束分别报告；计时上限到达不代表通关。

截图、快照记录与复制/哈希会带来采集开销。帧间隔反映此诊断配置，不直接代表未插桩帧率。存储/复制附近的停顿与普通呈现分别说明。

## 玩家输入诊断

新开局使用 `--mode player`；只测试卷首可加 `--title`。向 `run/input.json` 写入单个普通输入包，确认被消费后再写下一包。文件消费后删除，实际输入留在二进制记录。

```json
{"kind":"key","key":"D","duration_ms":700}
```

另支持 `click` 的 `x/y` 和可选 `button: "right"`、`joy_button` 的 `button/pressed/duration_ms`、`joy_axis` 的 `axis/value/duration_ms`，以及 `stop`。这些都经过 `Input.parse_input_event`；提供输入能力不表示游戏已配置相应手柄或鼠标绑定。关闭会释放工具仍持有的按键和摇杆输入。

UI 输入诊断、引擎侦探实机和独立盲玩分别记录。不得用工具接口代选世界动作或提交预置答案。
