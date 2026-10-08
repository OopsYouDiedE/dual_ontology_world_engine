# 一级验收：正式 UI 投映独立复测（2026-10-08）

## 范围与结论

本轮只核对既有 02 引擎输出经正式 Godot UI 逐段呈现后的 02→03 投映。结论：**当前样本对齐，通过**。未修改 01、02、产品代码或既有报告；未读取项目设计文件和首轮报告。本记录不是盲玩、玩家时长、移动碰撞或二级 UI 专项验收。

## 本轮证据

- 先运行 `draw_player_transcript_layout.py` 并实际查看六张布局规划图；再以 Godot 4.6.1 .NET 的 Vulkan 渲染器（NVIDIA GeForce RTX 3070，非 headless）运行 `capture_player_transcript.gd -- --layout-reviewed`，退出码 0。
- 输入为 `player-traces/engine-full-output.jsonl`，共 **1558** 个连续引擎步骤；本轮 `player-traces/ui-full-output.jsonl` 有 **5777** 行，包括 **3126** 个 `ui_visible` 画面、**1558** 个 `packet_rendered`、**1** 个 `render_finished`。尾记录：`finished=true`、`ended_component_drawn=true`、`ended_displayed=true`、`engine_ended=true`、`source_sequence=1558`、`final_panel=ending`。
- `export_ui_transcript.py` 成功生成 `03-UI实际显示的内容.txt`。实际终局截图 `full-ui/06-ending.png` 显示终局标题、结局正文及“返回标题”按钮，终局并非只在数据中标记。
- 六张本轮实际截图均已逐张查看，分别覆盖开场对白、调查证物卡、对账选项、校园对白、议事选项和终局。它们仅作画面辅助证据，**数量不作为一级验收门槛**。

## 02→03 核对

按引擎步骤对照原始输出和 `ui_visible` 的画面文字：对白、卡片、候选项、表单、旁白与终局均有对应可见呈现。逐字段原样匹配统计为：对白 **355/362**、卡片 **284/285**、候选项 **260/260**、表单 **27/27**、旁白 **8/8**、终局 **21/21**；另有 **1** 张卡片被同一步后续卡片在绘制前替代，记录为 `superseded`。原样匹配未计入的 8 处经逐段可见记录复核：1 处为卡片富文本颜色标记被 UI 正常去除；7 处为长对白换行、分段显示，前后片段覆盖原文，属于显示方式差异，未发现内容丢失。原始记录中 `content_clipped=true` 的 21 个文本片段对应长对白的分页过程；本轮捕获工具的 `_text_covered` 在每段播放时执行全文覆盖检查，运行未报错。

特别核对 E1 长证物卡：步骤 **106** 和 **114** 的卡片画面均有完整 `facts` 正文，`content_clipped=false`、`complete=true`；步骤 114 另显示新增试验笔记。调查截图只是一帧辅助，正文完整性以逐段可见 trace 为据。

## 限制

本轮只证明既有 02 会话在正式 UI 的投映；没有重跑逻辑层，也没有按设计规范评价剧情、人物合理性或实际游玩体验。一级的 01→02 与审美项由主代理依据独立证据判定；二级、三级未验证。截图齐全的成功门槛已从捕获脚本静态移除；本轮渲染在该门槛移除前成功完成，未因这一静态调整重复整局。
