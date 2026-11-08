# 四院室内原型组合

`compose_courtyard_rooms.py` 从 `designs/courtyard_prototype_rooms.json` 读取家具布局，复用已发布的 `campaign_rooms/components` 与 `models`。它是资产作者工具；游戏剧情、认知、可用动作、风窗和门禁由 DV/Core 声明。工具不修改共同组件、模型、剧情、呈现注册表或资产压缩包。

| 房间 | 用途 | 调查实体 | 主楼连接层 |
| --- | --- | --- | --- |
| W-102 | 文契院纸墨分析 | W102.workbench | 1F |
| B-102 | 白塔院配药清洁制备 | B102.cabinet | 1F |
| F-104 | 铁工院备件与成品 | F104.spares | 2F |
| S-102 | 星仪院精密仪器 | S102.instruments | 2F |

四室采用原尺度 R13 建筑壳体，室内为 7.8×6.7 米。其用途、陈设和通道分别适配；地图位置、院室标识、原始平面范围和绝对标高来自 `campus-r3.json`。呈现片段同时保留原始拓扑范围和室内原型尺寸，供后续替换专属建筑壳体。F-104 使用修订后的二层公共进路，其平面中心为 `(70.2, 75.3)`。

默认先生成候选：

```sh
.runtime/python/bin/python tools/building/compose_courtyard_rooms.py
```

审阅候选后发布当前布局声明拥有的四对场景与脚本：

```sh
.runtime/python/bin/python tools/building/compose_courtyard_rooms.py --publish
```

发布命令拒绝覆盖不属于本工具的既有房间脚本。主家具绑定写入 `candidates/courtyard_rooms/presentation-fragments.json`，由正式呈现注册表的维护者合并 `rooms` 和 `focus_bindings`；房门通过现有通用门解析。每个 `.tscn` 保有专属 `.gd`，脚本只提供组件查找。

正式呈现注册表合入四院后，可独立刷新组合房间及发布清单：

```sh
.runtime/python/bin/python tools/building/publish_campaign_rooms.py --compositions-only
```

该模式根据 `source_composition` 重新生成所属房间，核验既有共享依赖和全部旧输出的内容摘要，更新 `publication.json` 的实际房间、输出与字节统计。旧候选目录不必存在，旧房间与呈现注册表不会改写。已经相同的场景文件也不会重复写入。清单把原始候选闭包的来源统计与组合布局、生成器来源分别记录；原模型包继续使用。全量发布模式同样单独处理组合房间，不把它们当作旧候选场景读取。

家具碰撞盒取真实 GLB 网格在根坐标中的包围范围，计入层级变换，不缩放模型。作者预核检查家具互不重叠、室内边界、出生点、出口、主家具 Front/Back 站位与指定次要站位，并用 0.23 米人物半径加 0.07 米余量进行网格连通检查。报告记录实际组件、模型、布局、拓扑和输出文件的 SHA-256。该检查覆盖静态几何；正式 Godot 宿主仍负责导航网格、角色移动、交互到达判定和玩法验收。

场景正面保持既有俯视剖面方式。纸墨室摆放样书、墨瓶和登记台；制备室包含药柜、洁净台和洗手设施；备件室陈列备件箱、工具柜与联锁手柄；仪器室使用仪表台、机械天平和校准记录台。所有正式依赖均为已发布的 `res://` 资源。
