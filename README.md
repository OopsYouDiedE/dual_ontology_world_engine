# 双重本体世界引擎

这是一个以实体、行动值、Soul 决策、规则与言灵为核心的 Python 引擎与学园主题运行时。

无 2D/3D 表现不改变世界规则。同一逻辑场景内的实体忽略几何距离，可以直接交互；跨场景行动仍需沿空间拓扑寻路，并支付每条边对应的穿越代价。Parent、所有权、绑定、作用域、规则和行动值等机制保持不变。

## 主要目录

- `core/python/entity_system.py`：Entity、Facet、知识采信、关系与 Aspect。
- `core/python/soul.py`：Soul Step、Disposition 人格偏好、开放 ResponseMode、完整 Intent、认知规则、RulePatch 与 Event。
- `core/python/kotodama.py`：语义配方、言灵、Rite、Covenant 与真实代价。
- `core/python/engine.py`：空间、Timeline、条件 EventNode 与嵌套行动结算。
- `python_cli/academy.py`：异常感知学园内容表。
- `python_cli/academy_game.py`：Objective 承诺网络、群像决策、记忆、言灵、压力与多人封印流程。
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

终端不支持 ANSI 颜色时，可以使用：

```powershell
python -m python_cli --no-color
```

## 文档索引

- [文档总索引](docs/README.md)
- [引擎公理设定](docs/engine-axioms-zh.md)
- [Entity System 统一模型](docs/entity-system-model-zh.md)
- [回合、意图、规则与言灵模型](docs/turn-and-kotodama-model-zh.md)
- [异常感知学园内容层设计](docs/academy-content-design-zh.md)
