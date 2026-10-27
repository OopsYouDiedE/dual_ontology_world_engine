# 共享物体预制件

`PrefabLibrary.gd` 按稳定类型与材质变体缓存 `PackedScene`，重复摆放共享网格和材质，位置、旋转、光源节点及交互播放状态属于各自实例；但是家具占用、站位可达性和行为授权仍由正式宿主与 Core 处理。

`instantiate(key, category, builder)` 接收原点子树构造函数，`place_helper(kit, key, at, title, category, builder)` 捕获旧几何函数添加的子树并重新登记实例光源；但是键必须包含影响几何和材质的参数，动态壁炉火焰保持实例独立。

`category_for(kind)` 通过 C# DV 解析器读取 `Content/presentation.dv` 的 `layout prop_physics`，家具创建 `Solid` 与 `Front`、`Back`、`ContactTarget` 点位，装饰只保留点位；但是加载旧版导出资源时也会重新应用分类，避免旧碰撞继续生效。

点位与碰撞在根节点局部坐标中生成，嵌套家具使用已有实体，装饰预制件子树不参与外层实体构造；但是自动前后站位只提供几何候选，不能代替具体坐姿、手部接触与路径校验。

`tools/ExportWorldPrefabs.gd` 是永久作者导出入口，将共享资源写入 `scenes/World/Prefabs/Generated/`，将程序化布局写入 `scenes/World/Maps3D/`，并保留目录中的旧文件供逐项清理；但是导出完成只证明资源生成，游戏验证仍使用正式 MCP。
