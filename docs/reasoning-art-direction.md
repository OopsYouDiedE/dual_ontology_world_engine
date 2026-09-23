# 推理界面：现实、札记与脑内工作区

当前入口为 `res://scenes/Reasoning/Reasoning.tscn`。本轮实现界面分离与控件主题，仍是待用户视觉确认的原型，不代表案件、美术或十至三十分钟体验已验收。

现实调查保留全幅 TileMap、侦探移动与现场物品。札记只收录已取得的材料，打开后集中阅读正文与来源；没有人物规则或模拟结论混入。脑内使用独立场景实例、人物和 World2D，复用房间资产以便识别地点，但不复用现实中的可变对象。返回现场保留侦探位置。模拟回看不会写回现实，也不会解锁新证据。

脑内工作区左侧显示重演和时间轴，右侧只编辑当前人物的行为假设。“当时所知”默认折叠，显示当前回看时刻的人物认知，明确标注为假设中的信息。语法帮助单独展开。查阅证据时遮蔽工作区并暂停自动推进；修改草稿后必须从头提交，旧回放不会被当成新方案。

主题集中于 `ReasoningTheme.Create`：墨蓝背景、灰绿强调色、浅色阅读面；8px圆角、24px面板留白、12px间距。按钮、编辑器、菜单、滚动条和回放滑轨统一定义状态；正文不叠纸纹或模糊。地图继续按整数倍率显示原有资产，不把UI主题更新当成房间素材重绘。

参考 Apple 的[布局指南](https://developer.apple.com/design/human-interface-guidelines/layout)、[按需展开](https://developer.apple.com/design/human-interface-guidelines/disclosure-controls)和[独立模式](https://developer.apple.com/design/human-interface-guidelines/modality)：借用层级、留白、明确退出与渐进呈现，不直接复制平台的玻璃外观。实现采用 Godot 官方[Theme / StyleBox](https://docs.godotengine.org/en/stable/tutorials/ui/gui_skinning.html)机制，主题作用于本场景。

按本轮要求参考旧动物园的 `archive/zoo/csharp-sample/zoo/zoo_simulator/README.md`：保留“人物相信什么、为何作出选择、世界实际结算”的区分。没有移植旧实验任意改写认知、制造幻觉的权限，亦未恢复旧对象系统。

验证使用实际 Godot 窗口：调查收录、规则非法提交保护、连续播放、回看、两个 World2D 相互独立、脑内按键不移动现实侦探、返回现场与草稿保留。截图位于 `artifacts/tea-reasoning/`。临时铃声时间、整案证据与多解仍按场景 README 的未完成边界记录。

## 自定义铺装资源

研究室已接入 `godot_csharp/ShareAsset/ReasoningStudy/Study.tres`：A2地面与地毯、A4墙顶与墙面按Terrain连接，A5普通材质与B家具独立。源图、派生规格、完整提示词、重建与预览入口见该目录README。地图仍由TileMapLayer铺装；中央工作桌、书柜、拱窗和工作台使用新家具切片，茶具显示落点与家具碰撞同步调整。窗光为独立地面叠层，尚未实现动态遮挡光照。
