# 卷首到现场的加载与揭页

## 玩家可见顺序

点击“开始阅读”时，在当前封面立即启动正式会话准备，封面保持原位；从“续读”选择书签时，同样保留当前书签页。原按钮或书页小字显示实际正在进行的准备阶段，不使用模拟百分比、固定等待时间或“案卷正在展开”的中转页面。地图、人物、立绘、视野、灯光、镜头和现场抬头装配并完成首帧绘制后，才揭开卷首，直接露出下方的真实现场。进入游戏的整个过程只执行一次揭页，不串接标题淡出、黑屏和现场淡入。

减少动态效果沿用相同就绪门限，只省略揭页运动。普通动画按现实时间执行，不跟随世界播放倍率。凡例、目录与书签之间仍使用已有实体书翻页；游戏内跨视图与读档替换仍由原屏幕遮罩保护装配过程。

## 宿主与呈现合同

`Main.dispatch(start/load)` 取得单次开局所有权，置 `_opening` 并调用 `TitleUI.begin_opening`；命令行自动开始也经过同一入口。标题向宿主立即发出事件，不先等待一个动画。会话打开和快照构建仍使用 `GameEngine` 原有后台串行队列，不把 Node 或 Resource 交给后台线程。

`Main._view` 保留标题层，先等待地图适配器的 `prepare_map`，再在标题后实例化生产 `WorldView` 与 `UISystem`。UI 在加入场景树前设置 `host_input_blocked`，现场输入同时受宿主 `_opening` 锁控制；加载期间的确认、返回和重复点击不能触发下层菜单或世界动作。实际场景保持渲染与处理，不能用隐藏节点或停止处理冒充首帧就绪。

`WorldView.prepare_presentation(initial)` 复核已安装地图、玩家、同图在场人物、动画资源、碰撞与呈现组件，等待地图的延期几何准备，初始化视野、物件可见度、房间光效及初始镜头。它不移动人物、不提交 DV 行动。`UISystem.prepare_for_reveal` 等待 `DialogueUI.configure_cast` 已请求的立绘并落定 HUD 进入状态；可选立绘缺失沿用原有纯文字降级，不补造内容。最后由 Main 等待布局处理、物理帧和 `RenderingServer.frame_post_draw`，记录 `view_prepared_for_reveal` 后调用 `TitleUI.transition_to_field`。

`title_reveal.gdshader` 采样现有 `StageViewport` 桌面纹理与既有纸面纹理，透明区域露出下方已绘制的现场。标题文字恢复正常墨色后，源视口在 GPU 上冻结一次；生产路径不调用 `get_image`、不回读主视口、不生成假游戏预览。揭页结束才移除标题、解除宿主输入锁并发出原有 `view_ready`。封面仍遮盖时，玩家可见记录不把预备中的现场物件记作已经看见。

校验或无效书签失败发生在安装会话之前时，恢复原页交互；已安装会话后的现场准备失败保留标题错误说明与退出入口，停止后继派发，不在原会话上盲目重试。动画被释放或取消时必须完成等待。既有底层绑定失败仍可能终止进程，其诊断不能写成已恢复成功。

开局就绪检查同时核对实际身体。`DvFieldExecutor._actor_binding` 以 `initial_anchor` 选择新身体的出生锚点；没有声明时仍从原房间的合法且未占用 Stand/Spawn 选择。地点的 `anchor` 属于移动目的地，不能把同处一室的所有人物都塞到同一坐标，再因占位冲突默默略去其他人物。既有身体继续保留真实位置，正式移动目的地与成功判定不变。地图配方的 `stand_capacity` 保证各声明区域有足够的已发行站位；窄发布入口 `build_chapter2_scenes.py --stands-only zone_jia zone_bing zone_ding` 只补齐 Stand 标记，再运行 `build_living_floor.py --stands-only` 同步到生活翼，保持家具、碰撞、门道和世界初始房间不变。

无图形的工程诊断只等待处理帧并立即交接，计时记录注明 `rendered = false`；不将其认作实际画面验收。

## 二级 UI 观察

完成当前工程的 C# 构建后，用支持图形渲染的 Godot .NET 执行：

```powershell
& $env:GODOT --path godot_csharp --script res://tools/capture_start_reveal.gd -- --out=<新的绝对目录>
& $env:GODOT --path godot_csharp --script res://tools/capture_start_reveal.gd -- --out=<另一个新目录> --reduce-motion --resolution=1600x900
```

该入口通过真实投影输入打开凡例、回到封面、开始阅读，并在同一加载帧补发重复确认与返回输入。它采集加载阶段、揭页和首个现场，以现场输入已启用且存在交互选项，或自然出现的首句实际对白为观察终点；不假设每次开局都会自动对白。它不选择世界动作、路线或谜题答案，不执行流程回归。动态设置仅在该进程内临时预览并恢复，不写玩家配置。输出 PNG 与带校验的 `opening-ui.dvlog`；Main 另保留正常会话、输入、可见记录与关闭证据。默认 1280×720，60 秒观察软限、80 秒硬限，失败保留已有证据。

抓帧存在 GPU 回读和编码开销，本入口用于画面与输入衔接检查，不据此比较日常帧率或宣布剧情通关。实现、工程排错、实际观察结果和未覆盖范围分别记录到本轮 `artifacts/title-opening/` 子目录。
