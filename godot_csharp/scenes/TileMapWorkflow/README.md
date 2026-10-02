# TileMap制作验收室

入口 `Preview.tscn`，由 `addons/rpg_asset_adapter/mcp/export_map.gd` 生成；数据与重建入口见 [工作流](../../../docs/design-standard.md#第五部分32px-像素美术与地图工作流)。不改变项目主场景。

WASD / 方向键移动。人物使用现行32×48正面静态帧，仅供尺度、碰撞、前后遮挡检查。镜头固定房间中心，保持整数倍率。预览不包含剧情、开关门状态或正式人物行走动画。

`check.gd` 从当前构建JSON选择测试位置，以真实CharacterBody2D物理查询检查墙阻挡、门洞通行和茶几footprint；检查47种墙顶地形及绘制层级。非headless运行会输出viewport到 `artifacts/tilemap-workflow/runtime.png`。

生成资源成功和此检查通过只证明上述范围；场景审美、家具完整通路和最终游戏交互须分别验收。
