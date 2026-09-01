# 双重本体世界引擎

这是一个以实体、行动值、空间拓扑、Soul 决策、规则与言灵为核心的可玩原型。核心按目标语言拆分为三个版本：

- Godot C#：完整图形原型的主实现。
- Python：无 2D/3D 几何表现的参考运行时，用于快速验证机制与流程。
- TypeScript：预留迁移边界；当前不提供实现，也不作为本项目结束前的开发目标。

无 2D/3D 表现不改变世界规则。同一逻辑场景内的实体忽略几何距离，可以直接交互；跨场景行动仍需沿空间拓扑寻路，并支付每条边对应的穿越代价。Parent、所有权、绑定、作用域、规则和行动值等机制保持不变。

## 主要目录

- `core/godot-csharp/`：Godot C# 核心机制，包括实体、空间导航、行动值、Soul、规则与言灵。
- `core/python/entity_system.py`：Entity、Facet、知识、关系与 Aspect。
- `core/python/soul.py`：Soul Step、完整 Intent、认知规则、RulePatch 与 Event。
- `core/python/kotodama.py`：语义配方、言灵、Rite、Covenant 与真实代价。
- `core/python/engine.py`：空间、Timeline 与嵌套行动结算。
- `core/typescript/`：TypeScript 迁移预留目录，不包含运行时代码。
- `Game/`：Godot 可玩原型。
- `python_cli/academy.py`：异常感知学园内容表与旧实现沿革；公开入口指向正式运行时。
- `python_cli/academy_game.py`：Objective 承诺网络、群像决策、记忆、言灵、压力与多人封印流程。
- `python_cli/simulation.py`：协作成功和无人干预失败的确定性完整轨迹导出。
- `python_cli/game.py`：保留的“抄名室”机制原型。
- `python_cli/ui.py`：可复用的终端 UI 组件。
- `python_cli/app.py`：终端应用入口。
- `docs/`：设计规范、公理设定、模型与测试主题文档。

## 运行 Python 版本

需要 Python 3.10 或更高版本。终端表现使用 `Rich`：

```powershell
pip install -r requirements.txt
```

```powershell
python -m python_cli
```

运行正式学园的完整机制冒烟流程：

```powershell
python -m python_cli --smoke
```

运行保留的“抄名室”原型冒烟流程：

```powershell
python -m python_cli --archive-smoke
```

运行确定性主线模拟，并同时导出协作成功、无人干预失败两组不省略事件的 Markdown 与 JSONL 轨迹：

```powershell
python -m python_cli --simulate
```

终端不支持 ANSI 颜色时，可以使用：

```powershell
python -m python_cli --no-color
```

## 运行 Godot 版本

用 Godot 打开 `project.godot` 并运行项目。也可以先检查 C# 构建：

```powershell
dotnet build dual_ontology_world_engine.sln
```

## 文档索引

- [文档总索引](docs/README.md)
- [引擎公理设定](docs/engine-axioms-zh.md)
- [Entity System 统一模型](docs/entity-system-model-zh.md)
- [回合、意图、规则与言灵模型](docs/turn-and-kotodama-model-zh.md)
- [异常感知学园游戏设计书](docs/academy-game-design-zh.md)
- [异常感知学园内容层设计](docs/academy-content-design-zh.md)
