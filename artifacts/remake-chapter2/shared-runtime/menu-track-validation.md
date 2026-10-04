# 菜单滚动条局部实机自动验证

验证日期：2026-10-04。执行者为全新独立 Agent；未阅读设计文件、历史报告或历史试玩。仅审阅本轮 `menu_scroll_check.gd`、`Paper.gd`、菜单实现源码及宿主目录约束。

结论：本次局部自动验证通过。真实渲染截图可见 Q 菜单和人物菜单的 8px 轨道及滑块；底部按钮均在可见区域内通过鼠标输入事件点击，产生对应界面；返回关闭成功。日志无脚本错误，Godot 进程退出码为 0，结束后未发现该入口的残留进程。

## 范围与方法

只执行现有局部入口，没有全量构建或测试。命令为：

```powershell
& 'C:/Users/iii/Desktop/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64_console.exe' --path godot_csharp --rendering-method gl_compatibility --script res://scenes/Chapter2/Game/check/menu_scroll_check.gd
```

仅修改测试胶水两处输出文件名前缀为 `menu-track-`，未修改游戏实现。运行环境为 Godot 4.6.1 Mono / OpenGL Compatibility / NVIDIA RTX 3070，截图分辨率 1280×992。

Q 菜单由真实 Q 键输入打开。测试通过设置原生 VScrollBar 的 `value` 到最大值使其钳制到末端；随后向视口注入鼠标移动、按下、松开事件。底部按钮没有直接调用业务回调，也没有点击隐藏控件。人物菜单由测试调用 `_open_person` 打开，选择当前房间中具有最多行动的霍铎（11项），并以同样方法点击末项。Q 返回按钮实际鼠标点击；人物点击后的次级菜单以 Esc 返回。

这属于自动实机回归，**不是盲玩**。没有验证人物菜单从地图交互打开的完整链路，也没有用鼠标拖拽或滚轮验证滚动条手感。脚本的 `finish(0)` 并非充分断言，以下结论同时依据 JSON、日志和六张截图独立核验。

## 实测结果

| 项目 | Q 菜单 | 人物菜单 |
|---|---|---|
| 滚动条矩形 | (1078,250)，8×420px | (1078,250)，8×420px |
| 组合最小尺寸 | 8×0px | 8×0px |
| 场景可见 | true | true |
| 内容范围 / page | 581 / 420 | 425 / 420 |
| 前 / 后滚动值 | 0 → 161 | 0 → 5 |
| 底部按钮 | 输入命令（Enter） | 和霍铎打赌…… |
| 底部按钮矩形 | (194,635)，884×35px | (194,635)，884×35px |
| 点击中心 | (636,652.5)，视口及 ScrollContainer 内 | (636,652.5)，视口及 ScrollContainer 内 |
| 点击结果 | modal 关闭，输入框可见且聚焦 | 打开“猜谁今晚会去哪里”人物选择菜单 |
| 返回结果 | modal 关闭 | modal 关闭 |

`Paper.gd` 的 track 左右内容边距各为 4，实际组合最小宽度与渲染宽度均为 8px，没有 0px 宽问题。grabber 自身最小尺寸为 0×0，但实际滑块按原生轨道与内容比例绘制；Q 前图中滑块位于顶部、末端图中位于底部。人物内容仅超出 5px，滑块接近整条高度，前后图能看到顶/底端余量变化，末项在末端图中完整显示。六张原始截图均已查看。

## 证据

- [完整日志](menu-track-run.log)
- [原始指标与点击结果](menu-track-metrics.json)
- [Q 滚动前](menu-track-q-before.png)
- [Q 滚动到底](menu-track-q-bottom.png)
- [Q 底部点击后输入框](menu-track-q-input.png)
- [人物菜单滚动前](menu-track-person-before.png)
- [人物菜单滚动到底](menu-track-person-bottom.png)
- [人物底部点击后](menu-track-person-click.png)

日志含 `START_IDLE`（busy=false、intro_done=true、mode=main），四项 `SCROLL_METRIC` 和 `MENU_SCROLL_FINISH 0`，未含脚本错误。本次耗时约 6 秒，只用于局部界面回归，不能作为有效游玩时长。
