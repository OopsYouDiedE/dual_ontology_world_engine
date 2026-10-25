# E 区域美术库与探索记忆呈现

2026-10-26。用户要求将 E 扩展到游戏室内、第二章室外、六幕录制布景和16名学员，并要求各区域并行制作。本文记录实现接口，不新增世界设定。

`godot_csharp/scenes/ArtStyleTrials/E/library.tscn` 为独立可选资产预览入口；`LibraryE.select_asset` 按需装配一个资源。`build_e_library.py` 从现役几何合同与室内陈设配方提取64个空间（包含空寝室、走廊及服务空间），保留原家具脚点；不是64个新增剧情地点。四翼、公共学术、后勤诊疗温室分别由areas模块提供家具，基础墙体由RoomGrid统一。

16名学员沿用相同头、躯干、四肢和整体尺度，由CastE提供服装、发型与道具差异。录制布景覆盖现役六幕，但静态模型没有冒充完整动作、碰撞或镜头演出接线。

第二章室外涵盖正式六张地图。旧CampusSite部分源缺失；Outdoors保持现有道路/海岸PNG、地图锚点及无法可靠恢复的复杂成品，仅将可对齐碰撞的建筑与植被重建为三维。报告列出混合表示，不宣称全室外重绘完成。

## 认知呈现接口

用户当前规则：同地图内当前房间正常显示；已进入的非当前房间保留最后所见静态家具、压暗并覆半透明地面雾，隐藏人物和实时变化；未探索隐藏。墙只绘朝内一面，各区域暂不设计门；门模型将按当前房间的本人认知选择。

`RoomKnowledgeView.register_room(id,map_id,node,door_model)`只注册几何。`supply_observed_snapshot`接收观察者最后实际获得的静态画面模型与地面矩形，冻结副本；`apply_observer_view`消费当前房间和已探索集合。它不采集隐藏房间、不自动将注册/点击转换为知识。门节点只允许当前房间所属模型可见；本轮未新绘门。

`knowledge-demo`是显式三房间夹具：左侧为已探索记忆，中间为当前房间，右侧未探索隐藏。此夹具不运行Core。正式Host仍为现役2D呈现，尚未迁移到此3D库，也尚未将观察流接到RoomKnowledgeView。必须保留此边界，不能把独立预览报告成正式游戏的认知验收。

## 重建与观察

```powershell
python mcp/art_style_trials/build_e_library.py
python mcp/art_style_trials/e_outdoors/extract.py
python mcp/art_style_trials/capture_e_library.py --godot <Godot控制台程序完整路径>
python mcp/art_style_trials/report_e_library.py
```

采集使用纯GDScript临时工程、Forward+和1600方形视口，不执行Core。固定输出`artifacts/style-e/library-render`只在旧采集退出后替换；保留当前来源SHA256、控制台、图像与二进制观察封包。报告列出目标图、实机取景和差异。首次全库采集仅用于发现构图/遮挡问题，不是游戏碰撞、全动作或正式录制验收。

## 乙区参考图重设计

用户后续提供明确乙区目标图，保存在`artifacts/style-e/zone-yi-redesign/target.png`。本版按目标分区建立紧凑的米制美术布局：在初次紧凑方案上按用户追加要求长宽各扩大15%，北寝室3.91×4.60m、南寝室3.91×4.14m，公共厅5.06×6.90m，走廊11.73×2.30m；家具和墙高不缩放。尺寸为建模推定，不是概念图实测的建筑数据。来源家具身份从原catalog读取，摆放由YiRegion转换；不修改正式DV地点、碰撞或站点。两个原空寝室和侦探空书架仍保留源身份。

YiArchitecture为每室建立完整四面所属墙，移除外侧背面而保留室内框芯板、厚度截面和门洞；朝相机墙为1.05m剖切，其余3m。YiFurniture通过分件床帮/排骨条、柜框芯板、桌椅横撑、箱板及桶箍表达结构。YiRegion负责厨房、洗漱柜、米制材质及房间布置。此前暂停的UV图集不用于这版。

`zone_yi:overview`是作者总览，不读取或授予玩家探索知识；单室视图用于检查结构。22°透视、58°俯视；总览1600×1000，单室1600×1600。使用现役`e-library-capture`及`e-library-report`的`--zone-yi`参数，固定输出在`artifacts/style-e/zone-yi-redesign/render`，不覆盖全库历史截图。正式Host接线、碰撞与行动验收仍未完成。
