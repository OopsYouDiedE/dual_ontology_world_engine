# 第一章连续现场与开卷宿主

本文说明同地图移动与开卷呈现接口，不定义世界内容、行动费用、观察或剧情规则。输入映射与设置迁移见 `../godot_csharp/docs/chapter1-input.md`。

## 同地图过门

第一章生活层的房间共用同一个 `CampusMapView`。跨房间不卸载地图，不重新创建侦探，也不把角色传送至房间锚点。`WorldView.SpatialInputInner` 保留键盘、摇杆和鼠标路线输入，以真实 `CharacterBody2D.move_and_slide()` 穿过门道。

房内靠近门时只准备几何与导航资源。正式跨门请求限于当前可用的可见门选项与真实源侧门道，附带 request、choice、actor、map、from_room、to_room；Main 将这些值与独立返回的 DV prepared intent 核对，才给执行目的地加上 `continuous_walk` 标记。标记不进入 Core，不替代正常的来源、实体、序号、站点及碰撞核对。

请求期间仍可在源房间行走。Prepare 完成后只放行这次请求的目标边界；首次进入真实目标内室发一次到达回执。等待 Complete 时允许目标房间内局部行走，下一次跨门必须等待权威快照。到达前松开、转回或打开界面会取消该真实过门尝试；若人物已在厚门道的另一逻辑侧，仍通过真实碰撞走回源侧再发失败回执。正式尝试的失败费用继续由原 DV 规则结算，普通房内接近和转身不发跨门动作。

接口：`continuous_request()` 提供本次请求；`is_continuous_action()` 核对当前请求与目的地；`reject_continuous_request()` 释放未接受请求或已完成失败回执；`synchronize_continuous_snapshot()` 只在权威目标房间提交后清除成功请求。

## 呈现准备与缓存

`CampusMapView` 按实际地图、几何绑定与门禁状态复用准备结果；这些输入变化时使相关缓存失效。房间边界、灯位与窗区一次计算；相邻房间的导航网格按局部边界裁剪，动态人物占用每次重新叠加。缓存不得略去门禁改变或移动中的人物碰撞。

`WorldView.prepare_presentation()` 在初始书页覆盖期间准备当前地图的房间舞台、灯光、窗光、尘埃和局部导航。资源路径查询与角色资源准备被复用；房间切换调整已存在的呈现节点，避免在门口第一次创建它们。所有场景树、物理和绘制资源操作仍在 Godot 场景线程完成。

## 新游戏开卷

TitleUI 的开始按钮只发一次 `start`，同时开始实体书翻页。Main 保留标题书页，后台会话初始化与初始投影组装期间继续显示开卷页；地图、角色、房间舞台准备完成，并经过真实物理/绘制帧后，`transition_to_field()` 翻过首张纸页并显露现场。

Main 在现场装配前调用 `UISystem.set_host_input_enabled(false)`，使场景可以绘制而不抢走书页输入；开卷完成后恢复。`_field_ui_busy()` 同时考虑开卷、模态阅读、关闭和视图失效状态。初始化或视图失败显示在书页上并停止后续事件消费，防止只剩半个现场却继续运行；已开始的会话不可在原对象上重复新建。

TitleUI 接口为 `begin_opening()`、`wait_for_opening()`、`set_opening_status()`、`transition_to_field()` 和 `show_start_error()`。自动开局也复用这条路径。减少动态效果偏好继续即时落页。

标题、案卷、设置、对白、现场 HUD、推演窗口及原生通知共享 `build_ui_theme.py` 的纸张、墨色、字体与焦点样式；原生关闭和菜单图标也由生成器输出。书页内容保持原生 Control，实物书本几何与镜头不承担界面状态。

## 验证口径

构建、资源导入、主题生成一致性、原生输入机制、有限 UI 画面与真实门口移动分别记录。几何夹具可以说明真实碰撞、单次回执和取消路径，不能证明 Core Prepare 的实际耗时；注入标准 InputEvent 可以检查游戏输入处理，不能代替实物手柄驱动验收。

`continuous_request_begin`、`continuous_passage_prepared`、`continuous_gate_wait`、`continuous_physical_arrival` 和 `continuous_arrival_committed` 是宿主计时点。真实 Prepare 超过源侧门道的行走余量时仍必须阻止未授权越界；不能删除此门限换取表面流畅。性能结论需引用本轮运行、实际输入、帧采样与截图，不以缓存存在或平均帧率代替门口体验，也不把有限移动检查称作全剧情验收。
