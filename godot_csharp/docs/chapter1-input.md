# 第一章共享输入工程说明

本文说明宿主输入的实现、默认映射与定向排错入口，不是运行验收记录。输入设置、菜单焦点和书页导航属于 Godot 呈现层，不写入 Core、DV 会话、世界存档或案卷记录。

## 默认输入

`project.godot` 保留原有键盘操作，并为同一动作补充手柄输入。以下采用 Godot 的标准手柄名称，具体设备按操作系统映射提供事件。

| 用途 | 键盘／鼠标 | 手柄 |
| --- | --- | --- |
| 现场移动 | WASD／方向键 | 左摇杆／十字键，沿既有 `move_*` 路径 |
| 交互、对白继续、当前控件确认 | 原有交互键／Enter／Space | A |
| 返回当前层 | Esc／鼠标右键 | B |
| 案卷 | Tab | Start／View |
| 推演 | R | Y |
| 校园地图 | M | X；对白活跃时 X 用于回看 |
| 对白回看 | H | X |
| 前一页／后一页 | PageUp／PageDown | LB／RB |
| 菜单导航 | 方向键／Tab／Shift+Tab | 左摇杆／十字键 |
| 页面或正文滚动 | 滚轮及原生滚动条 | 右摇杆上下 |

返回只在按下边沿执行一次；释放事件也由当前输入层接收，避免退层后再次触发下层。未读完的对白接收返回但不跳过正文。没有活跃对白或弹层时，现场返回沿既有入口打开设置。书内目录、详情和确认页的返回关系由 `TitleUI` 自己维护。

## 共享导航与输入所有权

`scenes/Shared/Input/UIInput.gd` 是呈现层辅助器。静态 `is_back(event, pressed_only)` 识别返回；每个活动 UI 各自使用 `handle(root, event)`、`tick(root, delta)` 和 `reset()` 处理事件、持续导航与状态清除。`handle()` 返回是否已消费事件。

左摇杆以 0.55 进入导航、低于 0.35 释放，初次按住后等待 380 ms，再每 115 ms 重复。右摇杆滚动死区为 0.22，现场 `move_*` 死区为 0.24。重复采用现实时间，不随游戏播放倍率加快。设备断开、页面切换、窗口失焦、关闭或外部阻挡时清除导航状态。

导航使用原生 `Control` 的邻居与焦点接口，循环限制在当前页或弹层，跳过隐藏、禁用和不可聚焦控件。水平滑杆由左右输入调节，上下输入离开；原生按钮仍处理 A 的按下和释放，原生文本框仍负责编辑。右摇杆优先滚动焦点所在的正文容器。

`BookProjection3D.forward(event) -> bool` 在页面 SubViewport 内运行同一导航器，并把其余事件交给原生 Controls；鼠标继续使用既有相机射线和书页平面的逆映射。`update()` 驱动重复，`reset_navigation()` 供翻页和失焦时调用。宿主收到已消费结果后须标记自己的 viewport 输入已处理。

`ModalPanel` 和 `DialogueUI` 在 GUI 分发前拦截返回，避免正文或按钮吞掉右键。`ModalPanel` 沿 `UITransition` 管理过渡、外部阻挡和焦点恢复，`UISystem` 串行切换弹层。对白回看接管输入时不能同时驱动现场，`FieldHUD` 的鼠标设置按钮不抢现场的手柄确认焦点。

`UISystem.set_host_input_enabled(enabled)` 可在 `add_child()` 前设为 `false`：开卷期间现场 HUD 可以绘制，UI 不打开弹层、不抢焦点，也不消费标题事件；Main 移除标题后再启用。该门不改变世界规则或正式动作准入。

## 设置兼容

`GameSettings` 分别序列化键盘、鼠标、手柄按钮和摇杆方向，`event_label()` 提供中、日、英的可读名称。`CONTROL_BINDINGS_VERSION = 1`：载入旧版 `settings.cfg` 时保留玩家键鼠绑定，并给已保存的动作补上默认手柄事件。保存过新版控制设置后，玩家明确移除的手柄绑定保持移除；不在每次启动时重新添加。`reset_action()` 也保留默认按钮及摇杆事件。迁移只作用于 `user://settings.cfg`。

## 有限输入探针

`tools/probe_ui_input.gd` 使用生产 `UIInput`、`BookProjection3D`、`ModalPanel`、`GameSettings` 与原生 Controls，向 viewport 注入按下／释放和轴事件，观察漂移、禁用控件跳过、A 激活、滑杆、按住重复、回中停止、滚动、两层返回以及设置往返和迁移。

夹具不含正式剧情文本，不运行 Main、不创建 DV 会话、不执行 Core。投影辅助器使用最小舞台对象的非鼠标接口，因此不验证真实书页射线命中、渲染、地图碰撞、第一章性能或物理手柄兼容性。

先构建 Godot .NET 工程并导入资源，保证 `FrameArchive.cs` 可实例化；Python 生成器也需要在 `PATH` 找到 `dotnet`。以下命令在仓库根执行，`GODOT` 指向 Godot .NET 程序：

```bash
dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false -p:DvPython=python3
"$GODOT" --headless --editor --path godot_csharp --import
input_run="$(mktemp -d /tmp/dv-input.XXXXXX)"
XDG_DATA_HOME="$input_run/user-data" XDG_CONFIG_HOME="$input_run/config" \
  "$GODOT" --headless --path godot_csharp --script res://tools/probe_ui_input.gd \
  -- --out="$input_run/evidence" >"$input_run/native.log" 2>&1
```

`--out` 必须是尚不存在的绝对路径。每次使用新用户数据目录，因为设置观察会写入夹具自己的偏好；已有 `user://settings.cfg` 时探针拒绝执行。Windows 的单独工作树不会自动隔离同名项目用户数据，应在一次性项目副本设置唯一项目名并保持 `use_custom_user_dir = false`，或使用已确认有效的其他隔离方式，再执行：

```powershell
& $env:GODOT --headless --path C:/Temp/dv-input-NEW/godot_csharp --script res://tools/probe_ui_input.gd -- --out=C:/Temp/dv-input-evidence-NEW
```

完成后通过 `FrameArchive.WriteRecord` 写入 `input-observations.dvlog`，保留实际注入事件、单调时间、观察、失败项和范围标志。退出码 0 表示该次有限观察无报告失败，1 表示观察失败，2 表示参数、用户数据或 .NET 前置条件不满足；引擎也可能在脚本加载错误时提前退出。保留原始引擎日志和失败证据，不覆盖目录，不为得到成功输出而放宽条件。

构建、解析、资源导入和输入观察分别报告。未导入的 `.translation`、缺失的字体／图像须单列；没有二进制记录时不能宣称观察已执行。命令和本文本身均不表示检查已通过。
