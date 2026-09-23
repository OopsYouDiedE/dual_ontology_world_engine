# Godot 游戏层维护与路径规范

本目录为 Godot 4.6.1 .NET 游戏宿主工程，承载画面渲染、场景树、碰撞、实时循环、UI 及游戏玩法呈现。

## 强制文件放置路径

AI在执行 Godot 相关开发、调试、资产转换或场景构建时，**所有产物必须严格放入以下指定子目录，严禁外溢到仓库根目录、根目录 tmp 或 scratch**，严禁跨轮次随手新建临时文件夹。

### 1. 场景与功能实现：`godot_csharp/scenes/<SceneName>/`
- 每个独立玩法、功能原型或分镜必须有且仅有一个归属目录，例如 `Story/`、`Reasoning/`、`CharacterAnimationPreview/`、`Parlor/`。
- 场景文件（`.tscn`）、场景绑定的 C# 控制脚本（`.cs`）、GDScript（`.gd`）必须全部就地存放在该场景目录内。
- 场景独立的命令行验证与自测工具，统一收敛在场景内部的 `check/` 子目录（如 `scenes/Reasoning/check/`），禁止放在外层。
- **跨轮次严禁复制新建**：后续轮次对同一场景进行修改时，必须直接编辑既有文件，严禁创建 `Story_v2`、`Reasoning_new` 等平行副本。

### 2. 共享游戏素材：`godot_csharp/ShareAsset/`

素材按“外部来源隔离、内部集中收敛”原则存放：

- **外部素材（按来源严格分文件夹）**：
  - 凡是引入的第三方素材包、开源资产、特定作者/商店资源（如 `ShareAsset/Victorian/` 等），**必须按来源名/包名/作者名独立建立文件夹**。
  - 保持其原始文件结构、许可说明（License/README）及原生切片，严禁将不同外部来源的素材混杂存放，严禁外部素材与内部自制素材混在一个目录。
- **内部素材（默认集中存放在同一文件夹，禁止随意新建碎片目录）**：
  - 项目自制、AI 生成或内部切片的图像资产，**除非明确指定，否则一律集中放在同一个默认文件夹内**。
  - 既有明确指定的专门子目录仅限：
    - 角色基准与动作图集：`ShareAsset/Images/characters/`
    - 随身携带身份物：`ShareAsset/Images/identity/`
    - 已明确立项的分镜打样：`ShareAsset/Images/<shot_name>/`
  - **严禁行为**：严禁 AI 擅自为每一个新道具、每一件零散物件或每一次生成批次随手创建新的碎片文件夹；未明确指定独立归宿的内部自制素材，必须统一收拢在指定的同一内部素材文件夹中。
- **规格硬性要求**：所有切片素材必须严格核对 32px 地图格或 32×48 角色规格，禁止直接将未切片的杂乱原图散落在 ShareAsset 根部。

### 3. 插件：`godot_csharp/addons/<plugin_name>/`
- 编辑器扩展与素材适配转换插件（如 `rpg_asset_adapter`）。插件自身的资源和代码必须封闭在对应插件目录内。

### 4. 引擎内工具与构建脚本：`godot_csharp/tools/`
- 所有使用 Godot 命令行或编辑器运行的自动化构建脚本（如 `build_parlor.gd`、`shots/build_shot01.gd`）必须集中存放在此。
- 严禁将调用 Godot API 或操作 Godot 场景的脚本放置到根目录 `tools/` 或根目录 `tmp/`。

### 5. 导入派生产物：`godot_csharp/imported_assets/`
- 仅供素材转换插件输出派生资源，受 `.gitignore` 保护，禁止手工在此建立非转换逻辑。

## 严禁行为（红线）

1. **严禁在 `godot_csharp/` 下建立 `artifacts/` 目录**：
   - 阶段性非运行时交付物（对比图、评测长图、独立运行报告）统一归于根目录 `artifacts/<task-name>/`；
   - 可交互的验收室直接使用场景 `scenes/CharacterAnimationPreview/` 或对应场景的 `check/` 工程。
2. **严禁 Godot 文件外溢**：
   - 严禁在仓库根目录、根目录 `tmp/` 或 `scratch/` 放置 `.gd`、`.tscn`、`.tres` 或游戏贴图切片。
3. **严禁同任务新建版本后缀目录**：
   - 多轮次执行同一任务时，必须先检查既有路径，就地覆盖或演进，严禁产生 `_v2`, `_new`, `_backup` 目录。
4. **不污染 Core**：
   - 纯领域逻辑由 `csharp/`（Core）承载，Godot 侧仅通过引用接入，不得把 Godot 特有类型反向带入 Core。
