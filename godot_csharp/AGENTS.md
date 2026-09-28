# Godot 游戏层维护与路径规范

本目录为 Godot 4.6.1 .NET 游戏宿主，负责场景树、渲染、房内空间与碰撞、实时循环、UI 和游戏内容。通过现有 ProjectReference 单向引用 Core；Godot 类型、游戏专属状态和素材不得反向进入 Core。场景沿用现有对象绑定，不为目录整理增设通用桥接层、定位 ID 或重复状态。

## 唯一归宿

- `scenes/<SceneName>/`：可玩场景、绑定的 C# / GDScript 控制逻辑；场景专属验证归该场景 `check/` 或既有验证入口。原型和验收室也是场景，不能搬到根目录 artifacts。转换器生成的可复用物件 `.tscn` 可随所属资产族保存在 ShareAsset，不与手写玩法场景混淆。
- `tools/`：素材转换、图集生产、场景组装以及 Godot 命令行工具。插件专属转换实现留在对应插件内。
- `addons/<plugin_name>/`：插件及插件自身代码、资源与许可证，保持封闭。
- `AssetsSource/`：游戏美术生产源，根目录保留 `.gdignore`。外部包归 `External/<SourcePackage>/`，保留原包结构和许可证；内部归 `Internal/Characters/`、`Internal/Identity/` 或 `Internal/Props/`。
- `ShareAsset/`：唯一的正式运行时素材根。外部归 `External/<SourcePackage>/`，内部归 `Internal/Characters/`、`Internal/Identity/` 或 `Internal/Props/`。不再创建 AssetsCompiled 等平行运行时根。
- `imported_assets/`：受 gitignore 保护的转换试验输出，不得成为正式场景依赖；正式输出由配置写入 ShareAsset。

Characters 保存人物基准与动作；Identity 保存身份物与徽记；其余内部家具、道具、装饰和地形统一归 Props。不按镜头、生成批次或单件道具建立子目录。存在共同格式、配置和重建入口的图集族可保留已登记目录（如 Props/ReasoningStudy），不能借此恢复镜头碎片目录。已迁入的历史动作版本沿用其既有职责，后续直接修改，不再派生 `_v2`、`_new`、`_backup`。

音乐源及原声工程仍归根目录 `music/`；非运行时概念原案、评测长图与报告归 `artifacts/<task-name>/`；实际用于重建的美术生产源归 AssetsSource。发行包归 `artifacts/releases/game/`。禁止在本目录建立 artifacts。游戏场景、代码、运行时纹理及可复用 Godot 工具不得放入根目录 tmp、scratch 或 tools。

## 避免迁移后断链和旧路径复生

- 按实际引用判断源与运行时用途，不能把名为 source 的整个文件夹直接忽略。运行时禁止依赖 AssetsSource；源纹理如需原样使用，由生产链路复制到 ShareAsset 的明确位置，再由游戏加载。
- 修改路径须同时检查 `.tscn`、`.tres`、二进制 `.res` 依赖、代码动态拼接路径、插件默认配置、生成脚本及文档。不能只做文本替换就认定二进制资源已经迁移。
- 同名文件先比较内容；相同资源可去重，不同内容按用途命名，禁止覆盖。源文件、许可证和可追溯关系必须保留；自动生成缓存不冒充源文件。
- AssetsSource 不由 Godot 导入；生产工具通过文件读取源图。`.gdignore` 不等于 C# 编译排除，源区也不得放入将被宿主编译的脚本。
- 迁移后验证导入、受影响场景和实际重建入口；重建不得恢复废弃目录。确认没有依赖、来源损失或重建用途后才删除旧目录。
- 地图基础格32px，人物基础设计32×48；现有动作图集64×64为透明承载格，不放大人物。外部48px素材按原生规格读取，不以批量替换尺寸完成适配。制作验收统一遵循[设计规范](../docs/design-standard.md)。

每个资产族在既有 README 或清单维护源路径、转换入口、派生结果与使用场景；不为每张图增加独立报告体系。技术构建、实机交互和艺术验收分别记录。
