# 共享章节 UI 独立验收（2026-10-04）

本轮为独立自动 UI 验收，不是盲玩，不代表两章通关。验收 Agent 未读取设计文件或历史报告；仅核对当前运行源码、迁移前 UI 原件及实际窗口截图。未修改游戏实现，未重建 .NET，未运行全量/39/216 检查。

运行 Godot 4.6.1 stable mono，实际 OpenGL Compatibility 窗口，1280×992。入口为 `godot_csharp/scenes/Chapter2/Game/check/unified_ui_check.gd`。两章使用正式默认场景，未加载 Examples。第一章随后使用显式 `skip_intro`、`jump_to("investigate")`、C# progress 解锁及回忆标记注入；第二章通过原合法命令推进，不注入跳夜。

## 同源与原件核对

- 两章运行时 Dialogue 的 scene_file_path 均为 Shared/UI/ChapterDialogue.tscn；FieldUI 均为 Shared/UI/FieldUI.tscn，ChapterUI 和 ReplayManuscript 均实际使用同一 Shared/UI 脚本。
- Shared ChapterDialogue.tscn 与保存的第一章原件哈希一致，Shared HintOverlay.gd 与原件哈希一致。
- FieldUI 的 Vignette、TopShade、BottomShade、Title、Time、Hint 六节点，将换行统一后，其完整属性（包含 offset、theme、纹理引用）与原 Awakening.tscn 完全一致。
- Shared Paper.gd 对照原件仅有 VScrollBar track 左右 content_margin 各 4 的既有滚动可见性修补，纸张、字体、按钮等原样保留。

## 第一章结果

默认 Main2D 正式开场到 roam、H 历史开关、Q 原可用行动及 ESC 关闭均通过。注入调查后，未解锁 R 不开放，Q 仅展示当下可用笔记/历史/近处行动。

合法近处 E12 的 E 输入打开原证物卡，C# CardFacts 全文、来源与原图片保留，ESC 关闭；Tab 共享笔记保留原 E12 条目并可合上。共享重演五项显示、原第六项隐藏、回忆所证标记、未完成门槛均通过；真实鼠标选项与运行按钮调用原 C# RunReplay，结果对照、后续第六项显示、过期提示及 R 合上均通过。

逐张检查 c1-default-hud、c1-menu、c1-card、c1-notebook、c1-replay-unready、c1-replay-result：标题、原字距、图片、双页文字、选项和页脚都在窗口内，无裁切遮挡。结果页显示未查项属于本自动 fixture 的有限调查数据，不是全案匹配。

## 第二章结果

默认 Chapter2Game 正式开场到 main、共享场景/脚本身份、H/Q/ESC 均通过；默认启动未产生游戏 parse/script 异常。Q 原 global_actions 和全部 INFO 条目逐一保留，外层 scroll 最底“输入命令”可滚动到可见矩形内，真实点击后原输入框获得焦点；固定“返回现场”页脚保持可见。Tab 展示原“笔记”命令输出。

room_action 的 inspect 原接口经 _info 清空 card_texture，其原输出应进入 notebook：局部补测等待人物行动结束后确认原调查全文、返回现场及 Core 位置保留。实际实体中央公告板 thing 路径使用原 Objects 纹理并发送原“查”命令，成功呈现共享图片证物卡。

第二章默认 HUD、菜单顶部/底部、命令输出笔记、room inspect 输出及实体图片卡已逐张查看：均无裁切，菜单底部内容通过滚动可达，固定页脚可见。输入命令后原 log_panel 在模态后方保持显示，是原路径行为。

通过正式命令到第 3 日首夜重演开页，共享手稿身份、三项原假设区和全零禁用门槛通过。原 `state.replay_options` 此时为空，三组都只有“（不设这一项）”；fixture 预设 `[0][1]` 曾发生越界，日志完整保留。未注入/仿造假设，故本轮**未覆盖第二章异步 RunReplay 返回、结果页与 Core 回演现实位置保护**。空列表和禁用按钮属于原合法状态，不是迁移失败。逐张查看 c2-replay-unready，双页标题、三组选项、提示和运行/合上按钮完整，无裁切。

必要媒体定向检查在独立 4.4 秒实际窗口运行完成：使用当前正式中央厅的原纹理，显式注入一条 Image presentation，调用原 `_show_story_media`，共享 story_image 显示及 Space 经原 continuation 消耗媒体并关闭全部通过。没有加载 Examples、没有伪称正式剧情媒体顺序已通关；此项仅确认原渲染/输入回调仍有效。c2-media 截图的原图及“继续”固定页脚完整。

初次正式检查 58 个断言中 55 PASS，3 个 fixture 失败已明确归因并保留；第二章局部补测日志记录 36 PASS，媒体定向 4 PASS。补测至首夜重演时先前假定合法选项必存在导致胶水越界，已改为原列表为空时报告未覆盖，不对实现作兜底修改。日志为 `tmp/unified-ui/validation-initial-runtime.log`、`validation-c2-followup.log`、`validation-media.log`；第二章局部 PASS 从实际日志提取到 `unified-ui-c2-followup-checks.json`。没有把不同运行的重复断言相加当覆盖总数。

## 自动 fixture 的修正与限制

初次自动断言和原始日志保留在 `unified-ui-initial-checks.json` 与 `tmp/unified-ui/validation-initial-runtime.log`，没有删掉失败来掩盖结果。

1. 最早将 E1 的绘制 site_marker 当脚下坐标，实际落在生活翼起居厅，而原 C# 本地目标为门厅，SiteVisible 正常拒绝。改为合法 E12，明确 field_active/site_visible 后补测通过；原 E/Tab 失败为 fixture 几何错误。
2. 第一章 skip 回演后 fixture 用 drain 等“无任何 modal”，但原 afterReplay 会重开手稿，因此耗尽 60 秒等待。截图和最终结果已形成，不能把这段 fixture 无效等待称为 C# 阻塞或玩家卡顿。
3. 第二章最初把无纹理的 room_action inspect 期待成 card，且仅等待 1 秒就在原人物行动 busy 期间继续发送“歇”，原 send 合法早退。截图显示“人物正在行动…”和原调查/时段输出；原进程随后自主退出，未保存当时全部 busy/bridge 数值，因此不编造精确状态。补测按原接口等待 info notebook 呈现、关闭且 bridge idle 后再推进，保持正式命令，不注入跳夜。

第二章局部进程从 21:24:13 开始，21:25:40 前已完成默认开场、菜单/笔记、原 room inspect、实体图片卡和三次正式时段推进到首夜。原行动期间可见“人物正在行动…”；该自动轨迹约 87 秒包含原角色行动和 UI fixture 等待，不作为有效盲玩时长或卡顿指标。媒体进程自主退出；胶水越界停留的检查进程已用其 exec session 中断。确认仅剩用户既有 14:16:37 启动的 Godot editor（PID 30124），未触碰用户进程。

本轮不评价有效游玩时长、推理质量或通章体验；这些需要另一个全新上下文真实输入盲玩 Agent。自动推进对白、状态注入与针对 UI 的回调测试均不计作盲玩。
