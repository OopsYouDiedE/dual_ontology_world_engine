# 第二章长菜单滚动条局部实机自动验证

日期：2026-10-04。结论：**本轮失败**。本轮只验证 `Paper.gd` 在 grabber 上新增 `content_margin_left/right = 4` 的版本；后续 track 修复未在本上下文复测。

这是自动局部 UI 检查，不是盲玩或完整游玩验收。未读取设计文件、历史测试报告，未构建或运行全量测试，未修改实现代码。

## 方法

启动 Godot 4.6.1 Mono 原第二章场景，实际 OpenGL Compatibility 窗口，NVIDIA RTX 3070，1280×992 viewport。通过原开场对白的 Space 输入到达 `busy=false / intro_done=true / mode=main / modal_kind=''`。Q 用真实按键事件打开原菜单，人物菜单用原 `_open_person` 方法打开初始房间原状态中动作最多的霍铎（11项）。

滚动操作调用原 `VScrollBar.value = max_value`，由原 Range 夹取到底；没有重新布局、注入游戏状态、调用按钮 pressed/callback 或点击隐藏按钮。底部点击与返回均通过 viewport 的鼠标/键盘输入事件执行。由于控件零宽，本轮不能证明玩家实际可以拖动滚动条。

## 结果

| 检查 | Q 行动菜单 | 霍铎人物菜单 |
|---|---|---|
| 滚动前 VScrollBar visible | true | true |
| 原滚动条矩形 | (1086,250), (0,420) | (1086,250), (0,420) |
| 原滚动条 combined minimum size | (0,0) | (0,0) |
| grabber stylebox minimum size | (8,0) | (8,0) |
| max_value / page | 581 / 420 | 425 / 420 |
| 拉到底后 value | 161 | 5 |
| 底部按钮 | 输入命令（Enter） | 和霍铎打赌…… |
| 底部按钮矩形 | (194,635), (892,35) | (194,635), (892,35) |
| 底部中心处于真实 scroll/viewport 内 | 是 / 是 | 是 / 是 |
| 真实点击结果 | 原菜单关闭，输入栏显示并聚焦 | 原“猜谁今晚会去哪里”人物选择菜单打开 |
| 返回 | 原返回按钮鼠标点击关闭 | Esc 关闭人物选择菜单 |

截图逐张目视核对：两种初始菜单右侧均没有可见滑块；滚动后底部按钮完整呈现在 viewport；Q点击后输入栏及光标可见；人物点击后原目标人物选择菜单可见。新增 grabber 边距没有让 VScrollBar 获得非零宽度，**滚动条可见且可拖动的要求未通过**。进程正常退出（PID 38684 已结束）；脚本退出码0只表示检查流程完成，不表示验收通过。

## 证据

- 自动入口：`godot_csharp/scenes/Chapter2/Game/check/menu_scroll_check.gd`
- 数值：`menu-scroll-metrics.json`
- 实机日志：`menu-scroll-stdout.log`、`menu-scroll-stderr.log`（stderr为空）
- 初始Q：`menu-scroll-q-before.png`
- 底部Q：`menu-scroll-q-bottom.png`
- 点击输入命令：`menu-scroll-q-input.png`
- 初始人物：`menu-scroll-person-before.png`
- 底部人物：`menu-scroll-person-bottom.png`
- 点击人物底部选项：`menu-scroll-person-click.png`

以上截图、指标、日志均在本报告同目录。
