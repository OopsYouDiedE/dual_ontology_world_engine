# 宿主、输入、碰撞与案卷静态审读

日期：2026-10-21。分支：codex/detective-rpg-20261021。审读根目录及 godot_csharp/AGENTS.md、现役宿主/现场/UI 源码；未读取设计文件或历史轨迹，未启动游戏、会话、构建或流程回归。既有大量未提交改动全部保留，生产源未修改。以下区分确定的源码行为、条件性故障与待实机核查范围。

## H1：跨图或跨房动作中途失败留下真实身体与正式源身份分离（高风险、需核对 DV 拒绝合同）

代码位置：CampusMapView.gd:566-595；WorldView.gd:704-727、839-874、896-929；Main.gd:439-451、706-715、1258-1269。

执行器逐图走真实路线，WorldView.load_map 在转交同一身体、写入真实到达坐标后调用 note_map 更新保留实体的地图。CampusMapView 此后才核对最终 anchor，并调用 move_to_station；缺少站点、无路线或动态碰撞受阻均返回 blocked。Main 将该回执提交为 rejected，但没有物理回退。下一个动作又要求保留身体的 resource/map/源房间等于 execution_snapshot；不符就直接返回 retained_body_source_mismatch 或 retained_body_room_mismatch。

最小条件性复现：一个正式动作先成功跨到下一地图，再因最终 anchor 缺失、目标站点不可达或途中动态受阻失败；若 DV rejected 保留原逻辑地点，随后所有从原地点准备的移动立即失败。相同风险适用于同图先走入中间房间、再失败的动作。此审读未调用 DV，也未声称已在真实会话复现；主代理必须核对 rejected 的逻辑地点处理和新运行证据。

修补边界：在任何转交之前，预备全路线地图资源、最终 anchor/station、几何与当前可达性，可排除确定的静态失败；需要借用 prepared map record 检查，不能临时传送身体去做探测。但 preflight 不能保证未来动态人物碰撞、后续通行变化或中途资源故障不再发生，因此不能据此宣称失败一定保留源实体。完整修复需定义正式失败回执的真实部分终点处理，或使用已有准入的分段正式动作。直接改 retained 记录、强行把身体坐标写回源点、放宽源身份核对都掩盖问题且违反真实执行边界。现阶段保留缺口，不调整 DV 合同。

## H2：E 近门交互漏掉跨地图出口（确定输入接线缺口）

代码位置：WorldView.gd:1476-1480、1508-1532；CampusMapView.gd:801-809。E 在没有近热点时调用 _near_door；后者只调用 _door_option，而 _door_option 强制 destination.map == 当前 map。因此 doorway 选项明确指向别的地图时，E 永远不提交；WASD 的 _step 则另用 manual_exit_target 支持跨图出口。此处不是 DV 不准入，而是相同已准入跨图选项在 E 路径中被过滤。

最小复现条件：player 已有一个 available 的跨图 doorway 选项，站在实际出口近距内，且 nearest 热点为空；按 E 没有 select，继续走向实际出口却可进入正式执行链。尚待新会话的实际输入和截图核查，未宣称本轮实机复现。

可审阅修补为 e-cross-map-door-proposal-v2.patch（未应用）。CampusMapView 增加纯几何 manual_exit_targets_near，沿现有 Exits/Shape 矩形测量 24px 近距，并复核 armed/can_enter；SpatialInput 汇总附近同图门与跨图出口的当前 available doorway 选项，按 action id 去重，仅唯一选项时返回。后续继续使用原 _submit → Main → DV Prepare → 真实执行 → Complete，不传送、不生成新选项、不改变碰撞或世界位置。v1 草稿保留但已被 v2 取代，v2 还防止多个同图候选被 _door_option 的空返回吞掉而误选邻近跨图门。

应用前还需按根工作流同步 AGENTS/工程文档，做工程排错；有限 UI 核查应包括唯一近距出口 E、移出范围、关闭出口、多个准入候选、同图门及已有 WASD/鼠标路径。此提案未编译、未应用、未运行。

## H3：new 热点火花绕过视野过滤并画在迷雾上层（确定绘制路径，实际可见需截图）

代码位置：WorldView.gd:85、90、1175-1189。Sight 的 z_index 为 40，Hints 为 50。HintOverlay._draw 遍历所有 sites，new=true 直接绘制火花；这里没有 site_visible/in_sight 检查。热点悬停与交互本身有过滤，但火花不受同一过滤。若同地图未见区域内已有 new=true 的可投影热点且落在屏幕内，提示会在迷雾上层显示。该动画也没有读取 reduce_motion。

建议绘制前复用 source.site_visible(id)；reduce_motion 下用静态标识或省略呼吸火花。不能用该修复增加任何观察或取得记录。需要同一有限场景里“墙后/异房未知热点”和可见热点的实际截图；当前仅据源码确认绘制过滤缺口。

## 下一目标与提示接线盘点

FieldHUD.gd:50-65 只呈现 hud.room/title、hud.chapter、hud.time；WorldView.gd:573-577 为唯一现场持续 hint 来源，文本为热点名及 E 标记。Main.gd:685-690 在 awaiting_input 时存储 options 并计时，未把行动目标或随身操作入口映射到 HUD。UISystem 的 notebook/manuscript/map/wait 依赖快捷键，FieldHUD 只提供凡例按钮。因此可确认当前没有独立下一目标展示接线；是否遗漏已有 DV 的观察者可知目标字段，需主代理核对投影合同。不能由宿主自行补设目标或谜题指引。E 跨图出口也不会进入热点名提示的 nearest 集合。

## 案卷性能线索（未做性能结论）

UISystem.gd:387-390 每读完一行，把当前 presentation 累计全部行传给 CasebookData.record_presentation；后者在 CasebookData.gd:47-99 重建全文、深复制全部行并更新 evidence/item/person 条目。一个含 n 行的呈现，累计复制量随 1+2+…+n 增长；_upsert 还线性扫描历史 entries。属静态主线程热点候选，不能在无新采样下声称其造成已见停顿。优化可保留逐行来源、按行增量追加，不能缩减实际记录字段或从未读行预取正文。

## 未覆盖

没有构建、GDScript 解析、画面、遮挡、碰撞、输入、正式新会话或封存验收。没有检查谜题正确性、玩家所知事实或剧情达成。上述任何假设触发均需独立新运行确认，不用旧报告代替。
