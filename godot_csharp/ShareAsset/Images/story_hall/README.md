# 中央厅打样的装修补件

`stone_slabs.res/png`：64×64 图集，四枚32px石砖。源是 `ShareAsset/TileMap/Academy/Academy_Inside_A2.png` 的 (288,432,96,96) 区域，按原48px规格取两行两列，再最近邻派生为32px地图用图。不是原生逐像素重绘。补原Shot01实际地面缺少可见石砖接缝的问题。

`banner_1/2/3.res/png`：既有科学协会、神秘学会、学园旗的32×64派生组件，来源 `shot01/revision2/banner_*.png`。保持现有徽记，不创建新阵营。

重建入口：在Godot工程运行 `--headless --script res://scenes/Story/build_room_tiles.gd`。重建脚本仅供开发环境；运行时读取随项目保存的 `.res`，不读取源PNG进行转换。原图、提示词与原授权信息仍保留原位置。

地砖、墙、踢脚线、地毯、拱门与固定窗进入TileMapLayer。机械钟与长椅作为可交互Sprite2D。源门廊仍是建筑画面，本样片只到门槛，不宣称已实现四院通行。
