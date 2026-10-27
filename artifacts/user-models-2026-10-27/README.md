# 用户人物模型静态替换

2026-10-27 已导入用户提供的 17 个 GLB，来源、SHA-256、角色对应关系与使用位置记录在 `godot_csharp/ShareAsset/Internal/Characters/UserModels/manifest.json`；但是外部许可证未独立核实，使用依据是用户明确要求将其模型放入场景。

角色资源映射由 `Content/character_art.dv` 声明，共用 HallCraft 入口加载完整网格、材质和骨骼，以整体等比缩放统一到 1.32 米并对齐地面，展厅包含全部 17 人；但是目前保留双臂平举的绑定姿态，不播放 afraid，不宣称行走或坐起动作完成。

六个 recording 场景已通过正式资产导出器更新为 GLB 实例，宣传 Demo 和公共大厅的共用构造入口已接线；但是这次替换不改变二维正式游戏宿主的 StoryActor，也不构成完整游戏三维化验收。

`gallery.png` 为真实 Godot 人物展厅画面，`gallery-receipt.json` 保存 MCP 单场景实机回执且 host_errors 为空；但是该证据只覆盖静态展厅。最后一次调整将自动播放关闭提前到节点入树前，六个场景随后重新导出成功。

宣传 Demo 的后续 MCP 启动回执保存于 `demo-receipt.json`，当前 DV 编译在调用 c1n_manual_wait_actions 的位置报 unknown function or primitive；但是相关剧情模块未在本次任务中修改，Demo 此轮实机验证因此未完成。
