# 鼠标事件坐标与陈旧悬停：静态诊断及冻结后修补

正式blind-03运行期间只读冻结runtime，随后按主代理授权仅修改根工作区WorldView及工程说明。没有启动任何Godot、没有修改正在运行的冻结版，也没有注入输入或游戏状态。

## 确定的源码事实

冻结runtime dv_level3_capture.gd:111-123对单click输入包在同一函数连续Input.parse_input_event(motion)、press、release，中间没有process_frame。capture_saved_game.gd对普通左键直接调用该父实现。WorldView._process:570-581每个处理帧才重建hovered；_unhandled_input:1847却以field.hovered作为本次按下目标。这可造成当前事件的位置已经改变，而点击分支仍沿上帧热点的时间窗口。它不要求OS鼠标失配才成立。

另一个风险是地面点击在事件处理中另取field.get_global_mouse_position，而不使用这条InputEventMouseButton.position。Godot官方Input.parse_input_event文档说明它向游戏投递事件但不移动OS光标；CanvasItem文档说明get_global_mouse_position返回所属CanvasLayer中的鼠标位置。这两点不能单独证明本轮引擎视口缓存确实错误，更不能由“离屏”直接推断所有地面点击都读取OS旧值。准确诊断仍需同一次event.position、视口缓存、本地换算、旧hover和输入准入的配对记录。

来源：[Godot Input.parse_input_event](https://docs.godotengine.org/en/stable/classes/class_input.html#class-input-method-parse-input-event)、[CanvasItem坐标变换](https://docs.godotengine.org/en/stable/classes/class_canvasitem.html#class-canvasitem-method-get-global-transform-with-canvas)。

## 不动的其他合法原因

- _unhandled_input只有未被GUI消费才收到；FieldHUD TopBar在y20..68覆盖UI，模态、对白、屏幕过渡及标题均可占用输入或使enabled/field_active为false。HintLabel和HoverTip明确mouse_filter=IGNORE，不应单独拦地面点击。不能把界面遮挡下的不动当作寻路失败。
- _navigate当前在continuous非空且phase不是arrived时先取消旧过门请求并return，第一下点击可只完成取消；不保证同时建立新路线。
- 不在视野/邻接门准入范围、目标不可走、导航无路或目标已在脚下，均可能没有可见移动。点击家具中心也可能被原碰撞拒绝。

因此盲玩家记录的单次地面点击不动是待定位现象，不足以宣称产品鼠标输入已复现错误。上述陈旧hover依赖本身是可静态消除的实现风险。

## 已在根工作区完成的最小修补

WorldView.gd:566抽出pointer_hit(point)，输入为现场本地坐标。悬停和点击复用同一站点box/可见性、实际绘制/in_sight及sprite_sites映射家具优先规则；普通家具只作外观文案fallback。家具命中将本地point转回canvas全局再交既有furniture_hits。

WorldView.gd:1853鼠标press使用本次event.position经field.get_global_transform_with_canvas().affine_inverse()换为本地point，立即pointer_hit，再以该site调用原_approach或以同一点调用原_navigate。悬停显示仍每帧刷新，但不再决定实际点击目标。_unhandled_input、enabled、UI消费、视野、准入、碰撞、站点及reach均未放宽。

godot AGENTS及tools/README增补该合同。git diff --check退出0，仅文档换行提示；没有GDScript解析、构建、Godot或输入验收。此修补不在当前blind-03冻结runtime中，需主代理统一新冻结/构建/测试。

后续有限诊断应记录单次click的event视口点、本地换算点、query hit、旧hover、UI门状态、导航开始/拒绝原因，并核对普通桌面输入与离屏parse_input_event；不通过warp_mouse、延时一帧或代选路径掩盖生产行为。
