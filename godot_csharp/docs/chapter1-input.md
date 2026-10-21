# 第一章共享输入工程说明

本文说明宿主输入的实现、默认映射与定向排错入口，不是运行验收记录。输入设置、菜单焦点和书页导航属于 Godot 呈现层，不写入 Core、DV 会话、世界存档或案卷记录。

## 默认输入

`project.godot` 保留原有键盘操作，并为同一动作补充手柄输入。以下采用 Godot 的标准手柄名称，具体设备按操作系统映射提供事件。

| 用途 | 键盘／鼠标 | 手柄 |
| --- | --- | --- |
| 现场移动 | WASD／方向键 | 左摇杆／十字键，沿既有 `move_*` 路径 |
| 交互、对白继续、当前控件确认 | 原有交互键／Enter／Space | A |
| 返回当前层 | Esc／鼠标右键 | B |
| 在现场打开案卷 | Tab | Start／View |
| 推演 | R | Y |
| 校园地图 | M | X；对白活跃时 X 用于回看 |
| 对白回看 | H | X |
| 书页翻阅（适用范围见下） | PageUp／PageDown | LB／RB |
| 菜单条目选择 | 方向键 | 左摇杆／十字键 |
| 页面或正文滚动 | 滚轮及原生滚动条 | 右摇杆上下 |

返回只在按下边沿执行一次；释放事件也由当前输入层接收，避免退层后再次触发下层。未读完的对白接收返回但不跳过正文。没有活跃对白或弹层时，现场返回沿既有入口打开设置。书内返回关系由 `TitleUI` 维护：书签确认页先回书签列表，其余页面沿访问历史逐层返回；游戏内案卷在目录且无返回历史时合卷，无历史的非目录页先返回目录。标题书页逐层回到封面，封面上的返回不退出游戏。书内“目录”按钮直接回目录并清空返回历史。

书内 Tab、Start／View 同样执行逐层返回，优先于导航器的焦点循环；书页条目使用方向键或左摇杆／十字键选择。Tab／Shift+Tab 的焦点循环仅适用于未将这些键用于其他操作的弹层。

PageUp／PageDown 与 LB／RB 在案卷列表中翻阅前后分页，抵达边界时停留当前页；在凡例和续读页之间则切换这两页。封面、记录详情不使用肩键翻页，文本编辑框获得焦点时也不翻页。

## 共享导航与输入所有权

`scenes/Shared/Input/UIInput.gd` 是呈现层辅助器。静态 `is_back(event, pressed_only)` 识别返回；每个活动 UI 各自使用 `handle(root, event)`、`tick(root, delta)` 和 `reset()` 处理事件、持续导航与状态清除。`handle()` 返回是否已消费事件。

左摇杆以 0.55 进入导航、低于 0.35 释放，初次按住后等待 380 ms，再每 115 ms 重复。右摇杆滚动死区为 0.22，现场 `move_*` 死区为 0.24。重复采用现实时间，不随游戏播放倍率加快。设备断开、页面切换、窗口失焦、关闭或外部阻挡时清除导航状态。

导航使用原生 `Control` 的邻居与焦点接口，循环限制在当前页或弹层，跳过隐藏、禁用和不可聚焦控件。水平滑杆由左右输入调节，上下输入离开；原生按钮仍处理 A 的按下和释放，原生文本框仍负责编辑。右摇杆优先滚动焦点所在的正文容器。

`BookProjection3D.forward(event) -> bool` 在页面 SubViewport 内运行同一导航器，并把其余事件交给原生 Controls；鼠标继续使用既有相机射线和书页平面的逆映射。`update()` 驱动重复，`reset_navigation()` 供翻页和失焦时调用。宿主收到已消费结果后须标记自己的 viewport 输入已处理。

`ModalPanel` 和 `DialogueUI` 在 GUI 分发前拦截返回，避免正文或按钮吞掉右键。`ModalPanel` 沿 `UITransition` 管理过渡、外部阻挡和焦点恢复，`UISystem` 串行切换弹层。对白回看接管输入时不能同时驱动现场，`FieldHUD` 的鼠标设置按钮不抢现场的手柄确认焦点。

`UISystem.set_host_input_enabled(enabled)` 可在 `add_child()` 前设为 `false`：开卷期间现场 HUD 可以绘制，UI 不打开弹层、不抢焦点，也不消费标题事件；Main 移除标题后再启用。该门不改变世界规则或正式动作准入。

开卷呈现、首次现场准备与错误恢复接口见 [第一章连续现场与开卷衔接](../../docs/chapter1-continuity.md)。

## 设置兼容

`GameSettings` 分别序列化键盘、鼠标、手柄按钮和摇杆方向，`event_label()` 提供中、日、英的可读名称。`CONTROL_BINDINGS_VERSION = 1`：载入旧版 `settings.cfg` 时保留玩家键鼠绑定，并给已保存的动作补上默认手柄事件。保存过新版控制设置后，玩家明确移除的手柄绑定保持移除；不在每次启动时重新添加。`reset_action()` 也保留默认按钮及摇杆事件。迁移只作用于 `user://settings.cfg`。

## 有限输入探针

临时工具工程中的 `res://tools/probe_ui_input.gd` 使用生产 `UIInput`、`BookProjection3D`、`ModalPanel`、`GameSettings` 与原生 Controls，向 viewport 注入按下／释放和轴事件，观察漂移、禁用控件跳过、A 激活、滑杆、按住重复、回中停止、滚动、两层返回以及设置往返和迁移。

夹具不含正式剧情文本，不运行 Main、不创建 DV 会话、不执行 Core。投影辅助器使用最小舞台对象的非鼠标接口，因此不验证真实书页射线命中、渲染、地图碰撞、第一章性能或物理手柄兼容性。

`mcp/godot_workspace.py --csharp` 创建隔离的临时工具工程、导入所需资源并编译工具侧 C#。`FrameArchive.cs` 只在临时工程编译，正式游戏工程不包含采集工具。工作区负责隔离工具的用户目录，Python 入口需要在 `PATH` 找到 `dotnet`。获得本轮探针授权后，在仓库根执行以下命令；`GODOT` 指向 Godot .NET 程序：

```bash
python mcp/godot_workspace.py --csharp --script res://tools/probe_ui_input.gd --godot "$GODOT" --headless -- --out=/tmp/dv-input-evidence-NEW
```

`--out` 必须改为本轮尚不存在的新绝对路径。设置观察写入工作区自己的偏好，已有 `user://settings.cfg` 时探针拒绝执行。Linux 与 Windows 都通过同一工作区入口隔离用户目录，保留正式项目的设置。Windows 示例：

```powershell
python mcp/godot_workspace.py --csharp --script res://tools/probe_ui_input.gd --godot $env:GODOT --headless -- --out=C:/Temp/dv-input-evidence-NEW
```

完成后通过 `FrameArchive.WriteRecord` 写入 `input-observations.dvlog`，保留实际注入事件、单调时间、观察、失败项和范围标志。退出码 0 表示该次有限观察无报告失败，1 表示观察失败，2 表示参数、用户数据或 .NET 前置条件不满足；引擎也可能在脚本加载错误时提前退出。保留原始引擎日志和失败证据，不覆盖目录，不为得到成功输出而放宽条件。

构建、解析、资源导入和输入观察分别报告。未导入的 `.translation`、缺失的字体／图像须单列；没有二进制记录时不能宣称观察已执行。命令和本文本身均不表示检查已通过。
