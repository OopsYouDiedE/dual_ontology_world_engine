# DV 会话与独立检查源码

正式游戏与逻辑会话由 `../game/entry.dv` 组合 core、session、scenarios/chapter1 和 chapter2；一级逻辑输出由 `../game/debug-run.dv` 进入同一 prepare/complete 链。

```powershell
python language_core/tools/run_debug.py --out artifacts/partial-bootstrap-core/<新目录>
```

该命令是显式会话入口，本轮清理没有执行它。逻辑输出不包含Godot几何与画面验收，具体授权、时限及证据要求见根目录和本目录AGENTS。

| 路径 | 用途 |
| --- | --- |
| session/ | 生产主体认知、规划、话语、模型与调度 |
| scenarios/chapter1、chapter2 | 现役原生内容，由生产entry明确include |
| checks/ | 独立只读判定算法 |
| checks/authority/ | 隔离oracle，不进入主体 |
| checks/fixtures/ | 独立机制示例与明确标记的未实现草案 |
| checks/report/ | 独立判定的可读投影 |
| main-causal.dv | dv-check.json独立检查组的main包装 |

旧JSON多会话计划、相应Python编排、仅由该编排调用的比较/参数main包装，以及未接线的旧语法提案和回忆fixture已退役。历史源码从Git查阅，已有artifacts保留原字节。独立检查源码仍保留，不能把移除旧编排误称为已完成其验收。

接口与目录职责见 [DESIGN.md](DESIGN.md) 和 [Core参考](../core/README.md)。
