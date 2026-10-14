# 十六人一体化、系统修复与旧路径彻底清理报告

## 一、 架构与空间统一（大地图第一章运行）

1. **废除孤岛地图，统一运行于 CampusMapView**：
   * 彻底删除旧乙区单场景 `Awakening.tscn` 及其适配器 `AwakeningMap.gd`。
   * 第一章自清晨 8:00 开局起，直接加载全局多地图适配器 `res://scenes/Chapter2/Field/CampusMapView.tscn`，初始地图切片为 `zone_yi`（乙区），侦探初始出生点为 R-05 宿舍床边。
   * 在 `language_core/game/presentation.dv` 中，将第一章乙区房间（`foyer`, `hall`, `pantry`, `corridor`, `R05`~`R08`）完整绑定到 `zone_yi`。当侦探走出乙区隔离门时，通过生活翼的连通出口直接踏入公共起居厅（`living_hall`），第一章与第二章在物理几何上彻底融为一体。

2. **十六人全生命周期在场**：
   * 在 `chapter1-campaign.dv` 中，十六名学员在清晨 8:00 晨安广播时，同时在生活翼的四个寝区真实就位：
     * 甲区（文契组）：雀儿、卢西安、沃德、顾平章（`zone_jia`）
     * 乙区（侦探组）：侦探、霍铎、沈鼎、苏泠（`zone_yi`）
     * 丙区（星仪组）：阮清月、伊莲娜、赛尔玛、莫兰（`zone_bing`）
     * 丁区（后勤组）：茉拉、贝尔特朗、维罗妮卡、玛格丽特（`zone_ding`）
   * 各自具备独立物理世界位置（Physical Position）与初始心智，受寝区隔离门与房间视界（SightMask）自然阻隔，无需在章节切换时生硬“转生”。

---

## 二、 时间线重构与戏剧碰撞

1. **甲区文契组的行为分流**：
   * **雀儿**：不耐烦被沃德质疑盗窃名录库，于 **10:30（第 630 分钟）** 凭自身手艺挑开甲区门闩溜出；
   * **卢西安**：偏袒雀儿，随雀儿一同离开甲区；两人于 **11:30（第 690 分钟）** 漫步至公共起居厅（`living_hall`）的乙区隔离门外；
   * **沃德**：坚守程序正义与法条尊严，视撬门逃离为“畏罪潜逃”，坚决不走非法之门，携顾平章留内按部就班核对拓片与手稿。沃德于 **14:00（第 840 分钟）** 正规答辩被恶魔接受，正式开门走向起居厅公告板，当众张贴《私取者戒》追究雀儿罪责。

2. **乙区隔离门交互与 AI 侦探节奏**：
   * **S5a（隔门相闻）**：11:30 雀儿与卢西安抵达乙区门外后，侦探在门厅靠近隔离门即可触发隔门声音。雀儿告知门已被她挑开并提议帮忙撬门，卢西安提醒规程禁令；
   * **理想状态（AI 侦探）**：由 Core 认知与动作规划自然驱动，AI 侦探在 12:30 左右完成四问答辩，恶魔依约放闩，**侦探在 13:00（下午一点）前离开乙区大厅**，与门外的雀儿、卢西安面对面相见（S11）；
   * **保底机制**：若 AI 侦探未能在 13:00 前完成答辩，**雀儿准时于 13:00（第 780 分钟）动手用铁丝挑开乙区外闩**，确保流程绝不卡死；
   * **丙区与丁区**：丙区第二日清晨出门，丁区第二日午间出门，随全岛气候起风与连廊风门开放自然登场。

---

## 三、 呈现层与 UI 修复

1. **彻底解决所有人冒领“回岩”（侦探）的问题**：
   * 在 `presentation.dv` 中输出全套十六人立绘字典 `cast`，供 `UISystem.gd` 初始化载入；
   * 重写 `UISystem.gd` 的 `_character` 与 `DEFAULT_SPEAKER_NAMES`，建立英文代号到中文标准名及立绘的映射。只有侦探自身说话时使用玩家自定义名字，苏泠、霍铎、沈鼎、雀儿等角色严格绑定自身姓名与立绘；
   * 在 `player-content.dv` 中纠正对话 Presentation 的 `actor` 绑定。

2. **长文本换页滚动防截断**：
   * 在 `ChapterDialogue.gd` 的 `advance()` 中接入纵向滚动条检测：当文本超出两行可见高度时，首次按 Enter/Space 仅向下平滑滚动一屏（`page * 0.85`）；仅当读完所有余文后，下一次按键才切换台词包，彻底杜绝关键线索被吞。

3. **恢复调查对白文学质感**：
   * 在 `player-content.dv` 中恢复声路试验、水槽茶杯、长桌石板速记、门框空管的现场真实反馈对白，体现各人职业口吻与生活记忆。

---

## 四、 DV Core 性能瓶颈与时钟修复

1. **规范推进心智时钟**：
   * 在 `language_core/core/20-observation.dv` 的 `dv_update_mind` 结尾增加 `mind = dv_with(mind, "time", latest_time)`，使裁定者（Judge）认知时钟与观察时间同步，旧检视判定得以正常失效，从而触发重新检视与放门闩。

2. **解除 SessionPrepare 通知深拷贝膨胀**：
   * 在 `language_core/verification/session/18-requests.dv` 中，将 `co_presence` 与 `observation` 事件载荷中庞大的 `physical` 和 `mind` 全量对象剥离，改为只记录增量版本与坐标。根治单步结算死锁卡顿，消除了 `session.json` 膨胀到 1.19GB 的根源。

---

## 五、 清理与退役文件列表

1. **删除的旧孤岛与废弃测试文件**：
   * `godot_csharp/scenes/Awakening/Awakening.tscn`（旧乙区孤立场景）
   * `godot_csharp/scenes/Awakening/AwakeningMap.gd`（旧孤岛适配器）
   * `godot_csharp/scenes/Awakening/check/flow_check.gd`（废止的流程回归）
   * `godot_csharp/scenes/Awakening/check/cabinet_fog_current_check.gd`
   * `godot_csharp/scenes/Awakening/check/hand_outline_check.gd`
   * `godot_csharp/scenes/Shared/check/chapter1_smoke.gd`
2. **清理的 GPT 冗余跑批垃圾与测试生成大文件**：
   * 彻底清除 `blind-level3-20261014-01/` 下 3400+ 张逐帧截图（`frame-*.png`）、全部录像（`recording.mp4`，合计近 4GB）及性能日志（`*.nettrace`、`*.speedscope.json`）；
   * 清除 `startup.png` 临时探针图像；
   * 移除已废弃的旧路径与测试脚本（`Awakening.tscn`、`AwakeningMap.gd`、`flow_check.gd` 等）；
   * 更新根目录及各子目录 `.gitignore`，全局忽略 `*.nettrace`、`*.speedscope.json`、`*.mp4` 及 `artifacts/**/frames/`，确保测试产物不入库。
