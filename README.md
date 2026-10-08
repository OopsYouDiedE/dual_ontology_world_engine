# dual_ontology_world_engine

双本体论世界引擎（Dual Ontology World Engine）。包含独立的 Core 逻辑类库与基于 Godot .NET 的游戏呈现宿主。

开发、制作与机制判定遵循 [docs/design-standard.md](docs/design-standard.md)；日常开发、流程与验收规范遵循 [AGENTS.md](AGENTS.md)。

## 项目结构总览

```
dual_ontology_world_engine/
├── csharp/                      # 独立 Core 世界引擎类库（不依赖 Godot）
│   ├── AGENTS.md                # Core 维护规约与同步规则
│   ├── README.md                # Core API 与独立打包说明
│   ├── Core.sln                 # Core 解决方案
│   ├── DualOntologyWorldEngine.Core.csproj
│   └── *.cs                     # 本体模型、AST、CTB、决策推理等纯逻辑实现
│
├── godot_csharp/                # Godot 游戏宿主（单向引用 Core）
│   ├── AGENTS.md                # 游戏层维护与资产归宿规范
│   ├── project.godot            # Godot 4.6.1 .NET 项目配置
│   ├── DualOntologyWorldEngine.sln
│   ├── scenes/                  # 游戏场景与控制逻辑
│   │   ├── Title/               # 标题与选章入口
│   │   ├── Awakening/           # 醒来场景与剧本源（chapter_01.story 等）
│   │   ├── Chapter2/            # 续篇场景与推演原型
│   │   └── Shared/              # 跨章节共享组件（UI、现场、语言等）
│   ├── AssetsSource/            # 美术与动画生产源（隔离引擎默认导入）
│   ├── ShareAsset/              # 运行时正式资产（角色、道具、地图切片）
│   ├── addons/                  # 插件目录（如 rpg_asset_adapter）
│   └── tools/                   # 剧本编译与宿主辅助工具
│
├── docs/                        # 设计规范与主控文档
│   └── design-standard.md       # 唯一主控设计规范
│
├── tools/                       # 仓库级通用构建与自动回归工具
│   └── verify.py                # 统一自动回归入口
│
├── music/                       # 音乐工程与原声
└── artifacts/                   # 阶段成果、验证报告与测试交付物
```

## 核心架构分工

1. **Core 引擎（`csharp/`）**：
   - 采用 Essence–Projection 双本体模型与离散行动值（CTB）调度。
   - 包含主观投射演化、强类型 AST、两阶段提交事务、主体决策与推演判定。
   - 保持引擎独立性，完全不依赖图形渲染、空间物理碰撞与 UI。
   - 详见 [csharp/README.md](csharp/README.md) 与 [csharp/AGENTS.md](csharp/AGENTS.md)。

2. **游戏宿主（`godot_csharp/`）**：
   - 单向引用 Core 类库，负责场景树节点、物理与空间碰撞、实时交互、UI 与视觉呈现。
   - 详见 [godot_csharp/AGENTS.md](godot_csharp/AGENTS.md)。

3. **剧本与本地化数据流**：
   - 剧本与剧情统一维护在 `.story` 文件中，通过 `godot_csharp/tools/compile_story.py` 编译生成 C# 代码与结构化 JSON 数据。
   - 严禁在 C# 引擎及宿主代码中硬编码具体剧情、设定或对白。

## 常用命令与入口

### 构建与编译

```powershell
dotnet build csharp/Core.sln                           # 构建 Core 类库
dotnet build godot_csharp/DualOntologyWorldEngine.sln  # 构建游戏宿主
```

### 剧本编译

```powershell
python godot_csharp/tools/compile_story.py             # 从 .story 重新编译生成物
python godot_csharp/tools/compile_story.py --check     # 校验生成物是否与源一致
```

### 自动化回归与测试

```powershell
python tools/verify.py                                 # 统一全量自动回归
python tools/verify.py --only core scope host          # 仅编译并运行 Core 作用域回归
```

### 资产导入与刷新

```powershell
& $env:GODOT --headless --editor --path godot_csharp --import
```
