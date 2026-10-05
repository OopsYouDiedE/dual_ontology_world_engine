# 项目压缩与文件分析（2026-10-05）

本轮只归档和分析，没有优化实施、源码改写、素材重画或项目全量测试。既有暂存与未暂存修改均保留。分析依据当前磁盘文件、SHA256、工程配置和源码引用；不是按目录名猜测用途。

## 压缩交付与覆盖范围

`dual_ontology_world_engine-full.zip`：23,374 个文件、3,000 个目录；原文件总计 **7,040,143,658 字节（6.56 GiB）**，ZIP **5,718,397,761 字节（5.33 GiB）**，体积减少 **18.77%**。包含 `.git` 历史和索引、隐藏文件、忽略文件、未跟踪文件、缓存、临时输出、源码、素材与历史交付物。

唯一排除项为本轮新建的 `artifacts/project-audit/**`，防止压缩包包含自身。本报告、清单、审计脚本和验收报告作为包外配套交付。归档是逐文件读取的工作区快照，不是 Git 提交导出；读取时未检测到文件变化。没有文件系统快照锁，因此不声称其具有跨文件事务原子性。无扫描错误、无发现的符号链接。

ZIP SHA256：`5f1b6f99eae53edde18adc4e109f383eafcc70037c66affde4ec63493a1cc4e5`。

逐文件清单为 `files.csv`，记录相对路径、原字节数、读取前时间戳、SHA256、ZIP 内压缩字节数。`archive-summary.json` 给出归档汇总；独立逐文件解压验收结果见 `verification.json`。独立验收已通过：全部 23,374 个文件的 SHA256、大小、内置 CRC 以及 ZIP 整体哈希一致，遗漏、多余、重复与错误均为零，用时 27.613 秒。归档后的元数据复查发现 `.git/FETCH_HEAD` 时间戳变化，其内容哈希仍与包内一致，见 `post-archive-metadata-check.json`。

## 全部文件分类

下表互斥分类：先划分 Git、临时试验和缓存，再划分正式源及资产。GiB/MiB 均为二进制单位。

| 类别 | 文件数 | 原始体积 | 判断 |
| --- | ---: | ---: | --- |
| Git 内部数据 | 4,991 | 3.11 GiB | 最大体积来源；与当前游戏运行包无关，但完整项目备份必须保留 |
| 临时探索与转换试验 | 4,715 | 1.78 GiB | 包括 tmp、scratch、imported_assets；优先减少日常交付和扫描范围 |
| 缓存、构建输出、本地环境 | 8,398 | 476.11 MiB | 包括 .godot、bin、obj、.venv、__pycache__ 等；可再生成需分别确认 |
| 历史交付物 | 1,297 | 449.01 MiB | 报告、截图、Blender 工程等；不能当运行资产随意删除 |
| 美术生产源 | 536 | 349.36 MiB | AssetsSource；保留原图与生产来源 |
| 运行时资产 | 2,727 | 403.59 MiB | ShareAsset；以实际引用和动态加载判定交付范围 |
| 音乐源及导出 | 69 | 20.31 MiB | music；另有缓存已计入缓存类 |
| 源码、配置、文档及其他 | 641 | 6.42 MiB | 体积小，优先保持行为和结构稳定 |

Git 口径：当前磁盘中的已跟踪文件 4,479 个、905.26 MiB；忽略文件 10,434 个、1.54 GiB；未跟踪文件 3,470 个、1.03 GiB；另有 Git 内部数据。不能用 `git archive` 替代本轮全项目归档，否则遗漏当前未提交内容。

全项目发现 **3,927 组非空同内容文件**，按每组保留一份计算的重复字节为 **1,085,216,505（约 1.01 GiB）**。这只是重复统计，不能直接视为可删除空间。排除 Git、临时目录、报告、缓存、本地环境和转换试验后，正式文件仍有 **286 组、141.80 MiB** 的重复字节，最大的一批是 AssetsSource 与 ShareAsset 的原样副本。它们符合源与运行资产的职责划分，应保持。

明细：`inventory.json`（完整扫描）、`categories.json`、`git-classification.json`、`duplicates.json`、`formal-duplicates.json`。

## 优化方向与优先级

| 优先级 | 方向与证据 | 最小改动范围 | 验收与收益边界 |
| --- | --- | --- | --- |
| P0 | 保持原版完整复用，先固定可核对的基线 | 本次完整压缩包及逐文件哈希；后续明确原版包/版本与必要差异清单 | 本轮证明归档字节一致，不能证明与未提供的外部原版完全一致；任何重画、改写架构、替换对白/重演实现都不属于无损体积优化 |
| P1 | 日常交付按用途排除临时目录与转换试验 | 只改交付脚本的包含/排除清单，不删除工作区；tmp 1.45 GiB、scratch 3.42 MiB、imported_assets 343.39 MiB | 涉及约 1.78 GiB 原文件；完整备份仍保留。必须保留尚未正式归档的唯一生产源，不能承诺这些内容全部可删除 |
| P1 | 构建缓存和本地 Python 环境不进入日常交付 | .godot 约 312.65 MiB；插件 mcp/.venv 144.68 MiB；其余 bin/obj/pycache 等 | 分类总量约 476 MiB。先核对依赖安装清单；若需删除或迁移，只对明确生成项操作，游戏源码和插件本体保持原样 |
| P2 | Victorian 派生资源存储膨胀 | 69 个 .tscn/.tres 含 PackedByteArray，共 125.80 MiB；只调整派生纹理序列化和对应资源绑定 | 优先将同一原生纹理作为共享外部资源，保留全部像素、Atlas 区域、帧数/FPS、偏移、碰撞和动画。不要重画或缩图。需局部逐帧 RGBA 比对和对应场景加载；节省量尚未实测，不能把 125.80 MiB 全算成可回收空间 |
| P2 | 隔离开发工具与实际玩家发行内容 | .csproj 当前通过 Prototype 工程引用第二章剧本，Prototype 本身仅编译 Program.cs；正式剧本包含其余 Prototype/*.cs | 只在发行配置与引用边界做必要调整；Prototype 目录中的游戏规则仍在正式运行链中，不能整目录删除。引用与动态路径确认后才做定向构建 |
| P3 | 仅收拢已证实重复、未被当前入口使用的 UI 副本 | Chapter2/Game/HintOverlay.gd 与共享版 SHA256 相同；Game/Paper.gd、Game/ReplayManuscript.gd 未被当前宿主引用 | 先查所有工具/检查入口。变更限于重复文件及确实引用它们的路径；收益主要是减少维护歧义，不是大幅节省空间 |
| P3 | 让局部检查对应当前实际运行入口 | Chapter2/Game/check/unified_ui_check.gd 仍要求 Shared/UI/ChapterUI 与另一套重演路径；当前宿主使用原版 Shared/ReplayManuscript.gd | 只修正具体检查与当前既定行为的偏差；不能为通过旧检查换掉原版实现。后续每项改动仅运行相关检查 |
| P4 | Git 存储整理 | .git 3.11 GiB；git count-objects 报告 1,415 个 prune-packable 对象、74 个 garbage 项；lost-found/other 约 111.74 MiB | 完整备份验收后再单独检查可达性、恢复需求和维护策略。本轮不 gc/prune、不改历史，空间收益未验证；Git 体积不代表游戏运行内存 |
| P4 | 性能优化需先测实际瓶颈 | 文件大小分析不等于 CPU、GPU、内存或卡顿分析 | 后续若有具体卡顿，测对应场景再优化；不根据代码行数、PNG 大小直接改架构或牺牲素材规格 |

这些方向按保持原版与风险收益排序；优先优化交付边界和生成内容，最后才考虑源码重构或 Git 回收。P1 两项互斥分类共涉及约 **2.25 GiB** 原文件；这是交付排除范围，既不是 ZIP 减少量，也不是已批准的删除量。

## 源码与实际复用核对

- 实际入口是 `project.godot:14` 的 Title，而非 README 所述默认 Main2D；Title 可进入 Main2D 第一章或 Chapter2Game 第二章。第一章 Main2D 实例化 Awakening。
- `Chapter2Game.gd:1` 继承 `Chapter2Field.gd`，Field 属于当前运行链；不得按名称判为废弃。
- 两章直接使用 `Shared/ReplayManuscript.gd`（Awakening.gd:10、Chapter2Game.gd:12）。`Shared/UI/ReplayManuscript.gd` 是另一种接口：硬编码第一章标题、使用 replay_ui_*、改变证物按钮规则；直接替换会改变效果。
- 两章已共用 FieldUI 与 ChapterDialogue 场景，ChapterDialogue 实际调用 `Story/StoryDialogue.gd`。Paper、HintOverlay 也已有共享入口或一行继承包装。
- `Shared/UI/ChapterUI.gd` 当前无运行时入口引用；两章原有 HUD 的坐标、字号与布局不同，不能以复用之名强行接入。
- C# 第一章工程编入现有 Night/*.cs 并嵌入原内容和对白，第二章正式工程编入 Prototype 下除 Program 外的全部 C#。两章均引用 ChapterRuntime；不是只复制几段代码的替代内核。
- `scenes/FirstAwakening` 中仍有 GameLocale 自加载和项目翻译引用，旧章节不是默认案件入口也不等于整目录无依赖。
- Awakening.gd 2,100 行、Chapter2Game.gd 1,592 行、StoryDialogue.gd 846 行。大文件本身不是缺陷；只对已证明的相同正文考虑直接迁移/引用，保留宿主清理、输入和布局行为。

资源膨胀定位在 `embedded-images.json` 和 `resource-evidence.json`，后者给出像素数组行号。Victorian 原始目录现有 34 张 PNG；没有外部原版的完整清单或版本参照，因此不能声称原包已完整复用、所有现役图与外部原版完全一致。内部生成图、切片与原始整图用途不同，也不能因哈希不同即判为错误或擅自替换。

本轮没有游戏运行验收，不能从归档成功推导游戏可玩性或实机性能。独立 ZIP 验收只检查归档内容完整、字节未改变。
