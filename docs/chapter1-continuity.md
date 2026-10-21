# 第一章连续现场与开卷宿主

本文说明同地图移动与开卷呈现接口，不定义世界内容、行动费用、观察或剧情规则。输入映射与设置迁移见 `../godot_csharp/docs/chapter1-input.md`。

## 同地图过门

第一章生活层的房间共用同一个 `CampusMapView`。跨房间不卸载地图，不重新创建侦探，也不把角色传送至房间锚点。`WorldView.SpatialInputInner` 保留键盘、摇杆和鼠标路线输入，以真实 `CharacterBody2D.move_and_slide()` 穿过门道。

房内靠近门时只准备几何与导航资源。正式跨门请求限于当前可用的可见门选项与真实源侧门道，附带 request、choice、actor、map、from_room、to_room；Main 将这些值与独立返回的 DV prepared intent 核对，才给执行目的地加上 `continuous_walk` 标记。标记不进入 Core，不替代正常的来源、实体、序号、站点及碰撞核对。`formal_selection_ready` 另行确认本轮玩家选项已交付，且没有待呈现事件、延后输入或执行中的动作；普通房内行走不依赖这个正式选择门限。

请求期间仍可在源房间行走。Prepare 完成后只放行这次请求的目标边界；首次进入真实目标内室发一次到达回执。等待 Complete 时允许目标房间内局部行走，下一次跨门必须等待权威快照。到达前松开、转回或打开界面会取消该真实过门尝试；若人物已在厚门道的另一逻辑侧，仍通过真实碰撞走回源侧再发失败回执。正式尝试的失败费用继续由原 DV 规则结算，普通房内接近和转身不发跨门动作。

接口：`continuous_request()` 提供本次请求；`is_continuous_action()` 核对当前请求与目的地；`reject_continuous_request()` 释放未接受请求或已完成失败回执。现场执行器在接管前拒绝请求时，Main 同样释放该请求，避免永久保留提交锁。

`synchronize_continuous_snapshot()` 在权威目标房间提交后清除成功请求。如果人物已实际到达，而新的权威结果明确回到源房间且 pending 为 `idle` 或 `awaiting_input`，进入 `reconciling`，通过原身体、局部导航和真实碰撞走回源侧后再释放。旧 `prepared` 快照不会触发回退；UI 占用暂停返回，动态阻挡保留返回状态并重新查询路径。此恢复过程不发送第二次选择、到达回执或 Complete。

## 呈现准备与缓存

`CampusMapView` 按实际地图、几何绑定与门禁状态复用准备结果；这些输入变化时使相关缓存失效。房间边界、灯位与窗区一次计算；相邻房间的导航网格按局部边界裁剪，动态人物占用每次重新叠加。缓存不得略去门禁改变或移动中的人物碰撞。

`WorldView.prepare_presentation(initial)` 在初始书页覆盖期间准备当前地图的房间舞台、灯光、窗光、尘埃和局部导航。资源路径查询与角色资源准备被复用；房间切换调整已存在的呈现节点，避免在门口第一次创建它们。所有场景树、物理和绘制资源操作仍在 Godot 场景线程完成。

## 新游戏开卷

TitleUI 的开始按钮只发一次 `start`，`begin_opening(kind)` 锁住当前印刷页面并显示准备状态。Main 保留不透明标题，完成地图、身体、碰撞、视野、房间舞台、局部导航、UI 与真实物理/绘制帧后，`transition_to_field()` 用现役纸页揭幕显露已绘制现场。整合沿用较新的 title-ready 揭幕，不重新接入旧开卷页状态机；`BookOpeningPage` 作为可复用组件保留，不作为第二条生产启动路径。

Main 在现场装配前设置 `UISystem.host_input_blocked`，揭幕后经 `set_host_input_blocked(false)` 释放。`_field_input_busy()` 同时考虑开卷、模态阅读、关闭、书签和视图失效状态；`_formal_selection_ready()` 另核对正式选项周期。初始化或视图失败停止后续事件消费；书签装配失败保留载入锁和备份提示，已开始的会话不可在原对象上重复新建。

TitleUI 接口为 `begin_opening(kind)`、`set_loading_stage(stage)`、`cancel_opening()`、`transition_to_field()` 和 `show_start_error()`。自动开局复用这条路径。减少动态效果偏好直接完成揭幕并恢复输入。更完整的装配/揭幕合同见 `title-opening.md`。

标题、案卷、设置、对白、现场 HUD、推演窗口及原生通知共享 `build_ui_theme.py` 的纸张、墨色、字体与焦点样式；原生关闭和菜单图标也由生成器输出。书页内容保持原生 Control，实物书本几何与镜头不承担界面状态。

## 验证口径

构建、资源导入、主题生成一致性、原生输入机制、有限 UI 画面与真实门口移动分别记录。几何夹具可以说明真实碰撞、单次回执和取消路径，不能证明 Core Prepare 的实际耗时；注入标准 InputEvent 可以检查游戏输入处理，不能代替实物手柄驱动验收。

`continuous_request_begin`、`continuous_passage_prepared`、`continuous_gate_wait`、`continuous_physical_arrival` 和 `continuous_arrival_committed` 是宿主计时点。真实 Prepare 超过源侧门道的行走余量时仍必须阻止未授权越界；不能删除此门限换取表面流畅。性能结论需引用本轮运行、实际输入、帧采样与截图，不以缓存存在或平均帧率代替门口体验，也不把有限移动检查称作全剧情验收。
