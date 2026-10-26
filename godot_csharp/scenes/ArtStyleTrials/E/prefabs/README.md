# 共享家具预制件

`PrefabLibrary.gd` 按稳定类型与材质变体缓存真正的 `PackedScene`。第一次构造源模型后，为整棵子树设置 owner 并封装；源节点随后释放。后续摆放调用 `PackedScene.instantiate()`，共享网格、材质资源，单独持有位置、旋转和节点状态。相同物体不在每个房间重新生成几何。

`areas/YiFurniture.gd` 是床、柜、椅、书桌、工作桌、床头桌、长凳、木箱、木桶及空书架的米制作者源。`furnish(kit, kind, at, title, palette={})` 返回实例，四参数旧调用保持兼容。可选颜色字段为 `wood`、`edge`、`panel`、`brass`、`cloth`、`linen`；颜色组合属于缓存键。前后朝向的椅子共用同一资源，只改变实例旋转。

`instantiate(key, builder)` 用于返回完整原点子树的作者函数。`place_helper(kit,key,at,title,builder)` 用于向 `kit.content_root` 添加节点的旧几何函数：临时捕获原点模型、恢复父节点，并为实例重新登记 OmniLight。键必须包含影响几何和材质的参数；不要在 builder 中修改人物、认知或游戏状态，也不要封装维护 `fire_materials` 的动态壁炉。

`PublicHallE._lamp`、`_sconce`、`_coatstand` 已使用共享预制件。灯具只缓存几何，光源仍按每次实例的房间和位置单独登记，避免复用源节点或旧房间标记。

预制件在程序装配时按需封装；`--residences` 采集同时发布到 `artifacts/style-e/residences/render/prefabs/*.tscn`，引用本项目的Shader。可在类型生成后调用 `PrefabLibrary.save(key, "res://…/name.tscn")` 保存同一资源；`keys()` 返回本次已生成键。导出文件由作者源再生，不手改。修改共享材质时应先复制资源，避免影响其他实例。正式交互点位和动作逻辑仍由 DV/宿主合同接线，本美术缓存不管理占用或权限。
