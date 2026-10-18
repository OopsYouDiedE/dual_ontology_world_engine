# DV 七文件源码审阅包

本目录由 `consolidate_dv.py` 从仓库指定的七个输入目录生成，供源码审阅和传递。
七份文件按目录拼接，但每个原始文件都保留独立来源标记；它们是源码审阅包，不是可直接联编或运行的程序，也不代表全仓库源码。
文件中的 `define`、`main`、`include` 等内容按原文保留。拼接不会改写这些声明之间的关系。

## 七份文件

- `01_dv_compiler.dv`：DV 编译器；来源目录 `language_core/compiler`，包含 1 个 `.dv` 文件。
- `02_dv_core_runtime.dv`：DV Core；来源目录 `language_core/core`，包含 5 个 `.dv` 文件。
- `03_dv_session_simulation.dv`：会话执行与调度；来源目录 `language_core/verification/session`，包含 5 个 `.dv` 文件。
- `04_dv_story_chapter1.dv`：第一章场景；来源目录 `language_core/verification/scenarios/chapter1`，包含 5 个 `.dv` 文件。
- `05_dv_story_chapter2.dv`：第二章场景；来源目录 `language_core/verification/scenarios/chapter2`，包含 3 个 `.dv` 文件。
- `06_dv_game_integration.dv`：游戏接线；来源目录 `language_core/game`，包含 2 个 `.dv` 文件。
- `07_dv_verification_checks.dv`：验证检查；来源目录 `language_core/verification/checks`，包含 11 个 `.dv` 文件。

## 范围

纳入范围：

- language_core/compiler/*.dv（仅直接子文件；不含 examples 或其他子目录）
- language_core/core/**/*.dv
- language_core/verification/session/**/*.dv
- language_core/verification/scenarios/chapter1/**/*.dv
- language_core/verification/scenarios/chapter2/**/*.dv
- language_core/game/**/*.dv
- language_core/verification/checks/**/*.dv（包括 report、fixtures、authority 子目录）

排除范围：

- compiler 下的子目录文件（包括 examples）
- 上述七个输入范围以外的仓库文件及非 .dv 文件
- verification/scenarios 下不属于 chapter1、chapter2 的其他内容
- 验证目录中 checks 以外的内容（除明确纳入的 session/scenario 范围）

## 来源校验

`manifest.json` 逐文件记录原始相对路径、原始字节 SHA-256、源文件行数，以及合并文件中的源正文行范围。空文件的结束行等于起始行减一，表示正文范围为空。文件按相对路径稳定排序。
