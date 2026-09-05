# 荒岛学园轨迹展示场景

`scenes/smallville_demo.tscn` 是项目的默认启动场景。展示层使用 70×37 个 16×16 单元构建真正的 Godot 4.6 `TileMapLayer`。海面、沙岸、草地与道路、建筑与植被分别位于独立图层，没有使用整张城市示例图作为背景。

校园按《异常感知学园：灰潮禁闭》设定组织：中央厅与誓约庭位于核心，文契院、星仪院、铁工院和白塔异象院分布四周；生活院、后勤院、诊疗所和训练设施构成公共层；南岸潮湾、西湾沙滩、种植梯田与温室、上水库、海崖信号塔、林地石场和环岛步道构成岛屿生存环。

建筑与道路使用 [Kenney Tiny Town](https://kenney.nl/assets/tiny-town)，海岸地形使用 OpenGameArt 的 [16x16 Overworld Tiles](https://opengameart.org/content/16x16-overworld-tiles-0)。人物仍使用 [Kenney RPG Urban Pack](https://kenney.nl/assets/rpg-urban-pack)。三套素材均采用 CC0 1.0 许可，来源与许可说明见 `assets/island_campus/ATTRIBUTION.md`。

场景展示十六名学员与一名人形恶魔，包含需求动机、分时日程、岗位活动、移动轨迹、近距离对话与事件记录。共同设施使用稳定站位槽，移动交汇时执行轻量角色分离，避免多人占用同一点。空间移动和可视化只存在于 Godot 游戏层，没有进入核心引擎。

操作：

- 单击人物：观察人物状态
- `Space`：暂停或继续
- `1`、`2`、`3`：切换 0.5、1、2 倍速度
- `T`：显示或隐藏轨迹
- `R`：重置模拟
