# Godot 游戏层维护与路径规范

本目录为 Godot 4.6.1 .NET 游戏宿主，负责场景树、渲染、房内空间与碰撞、实时循环、UI 和游戏内容。通过现有 ProjectReference 单向引用 Core；Godot 类型、游戏专属状态和素材完全由游戏层管理。场景沿用现有对象绑定，保持简洁直接的架构。制作验收统一遵循 [设计规范](../docs/design-standard.md)。

## 唯一归宿

- `scenes/<SceneName>/`：可玩场景、绑定的 C# / GDScript 控制逻辑；场景专属验证归该场景 `check/` 或既有验证入口。转换器生成的可复用物件 `.tscn` 随所属资产族保存在 ShareAsset。
- `tools/`：素材转换、图集生产、场景组装以及 Godot 命令行工具。插件专属转换实现留在对应插件内。
- `addons/<plugin_name>/`：插件及插件自身代码、资源与许可证，保持封闭。
- `AssetsSource/`：游戏美术生产源，根目录配置 `.gdignore`。外部包归 `External/<SourcePackage>/`，保留原包结构和许可证；内部归 `Internal/Characters/`、`Internal/Identity/` 或 `Internal/Props/`。
- `ShareAsset/`：正式运行时素材根。外部归 `External/<SourcePackage>/`，内部归 `Internal/Characters/`、`Internal/Identity/` 或 `Internal/Props/`。
- `imported_assets/`：受 gitignore 保护的本地转换试验输出，正式输出由配置写入 ShareAsset。

Characters 保存人物基准与动作；Identity 保存身份物与徽记；其余内部家具、道具、装饰和地形统一归 Props。存在共同格式、配置和重建入口的图集族保留已登记目录（如 Props/ReasoningStudy）。历史动作图集直接在既有文件上重构演进。

音乐源及原声工程归根目录 `music/`；阶段性概念原案与独立报告归 `artifacts/<task-name>/`；实际参与资产重建的美术生产源归 AssetsSource。发行包归 `artifacts/releases/game/`。游戏场景、代码、运行时纹理及 Godot 工具统归本工程管理。

## 资源流转与管线维护

- 资源属性依据实际引用判定：生产源存放于 AssetsSource，运行时资产由生产链路规范发布至 ShareAsset 的明确路径，游戏宿主统一加载 ShareAsset 资源。
- 路径调整同步核对 `.tscn`、`.tres`、`.res` 依赖、代码动态路径、插件配置与生成脚本。
- 同名资源比对内容后妥善归类；源文件、许可证和追溯信息完整保留。
- AssetsSource 通过文件流供生产工具读取，配置 `.gdignore` 隔离引擎默认导入。
- 迁移后同步验证引擎导入与场景运行。
- 地图基础格为 32×32 像素，角色基础设计为 32×48 像素，64×64 像素为透明承载格。外部素材严格依照原生规格读取并完成切片适配。

每个资产族在既有说明中维护源路径、转换入口、派生结果与使用场景。技术构建、实机交互和艺术验收分别记录。
