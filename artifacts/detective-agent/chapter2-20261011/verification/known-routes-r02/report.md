# 已见路线与真实输入独立验证

本轮自动机制验证通过。宿主构建、控制器构建及运行、中性导航、实体碰撞与关闭出口合同均有本轮新日志支持。未运行 FullTest、正式连续会话、一级全程记录、二级 UI 或三级盲玩；人物行为合理性及游玩体验未验证。测试 Agent 未读取设计规范、正式剧本、正式编译剧情内容或 FullTest 历史。

## 实测

- 记忆网格：未见 U 形通路保持封闭；实际看见后提供通往已见地点的普通短路标。路标先远离目的地仍可成立，携带公开 `value` 和 `purpose=approach`。隐藏通行的开启与关闭不改变记忆或候选，进入视线后才更新对应格子。端点核对了不同横纵分辨率、offset 与非零 region。[中性导航日志](neutral-navigation.log)
- 控制器：中性角色凭自己的当前问题选择有关的绕路，区别同一坐标的无关目的地；必要返程在重复选择后保持相同评分，同时保留访问历史并消耗新鲜度。不同目的地不共用碰壁身份，新采信材料不抹去原路线的碰壁记录。这是合成认知与公开候选的机制验证，不是游玩轨迹。[控制器运行日志](controller-runtime-formal-candidates.log)
- 窄路：中性 24px 狭缝在旧中心采样下无路，8px 中心采样产生可行路线；原 `StoryActor` 碰撞体真实行走到终点，直接朝邻墙移动仍由物理碰撞阻止，未扩大角色或放宽通行判定。[物理与输入日志](neutral-physics-input-drained.log)
- 键盘：先让前一帧忙碌，再以引擎时间倍率 4 执行正式输入；回调墙钟 352ms，实际按下到释放 359ms，满足至少 350ms。主动停止在 104ms 释放。原 `WorldView` 输入循环实际驱动角色，正常输入派发后物理键态和席位按键列表均清空。回调刚返回时释放事件仍在派发队列里，该瞬间键态为按下，已在原始记录中保留。[物理与输入日志](neutral-physics-input-drained.log)
- 出口：关闭出口的物理阻挡和导航一致，另一出口不受影响；重新开放恢复实际通行，重复快照不叠加障碍；真实入口回调中的拒绝换图退回有效位置并取消点击路径。[出口完整结束记录](neutral-physics-input-drained.log)

## 静态与构建边界

`GameEngine.AgentObserveField` 的公开 `value` 通过 `ChapterEvents.Text(o, "value")` 写入 `PlayerFieldOffer.Value`，由静态读取与[宿主构建](host-build.log)支持。本轮没有在 Godot 中注入中性 `GameSession` 对该 C# 转换做独立运行验证；控制器的公开 Value 语义已通过纯运行回归。[控制器构建](controller-build-formal-candidates.log)

## 失败记录及处理

- [初次物理检查](neutral-physics-input.log)：新增测试误用了 PackedVector2Array 的 `all()`，属于测试语法错误；改用 Array 转换后再运行，未改生产。
- [随后物理检查](neutral-physics-input-repaired.log)：在 `_keyboard_move` 和 `stop` 返回的同一帧过早读取键态，只采到按下事件；改为保留回调时间并等待正常 process/physics 派发，再核对真实释放边沿与键态。350ms 下限和停止要求均保留，未改生产。
- [初次控制器运行](controller-runtime.log)：U 形对照夹具错误地提供了距起点 496px 的普通移动候选，超过正式席位该类候选范围；该输入得到的探索评分击败绕路，断言失败。改回正式 32px 普通方向尝试后相关绕路被选择。这不证明任意超范围可信候选都优先绕路，报告仅确认正式候选范围的合同。

所有运行日志保留原始字节；每次命令、退出状态、墙钟耗时及 SHA-256 存于同名 JSON，汇总在 [manifest.json](manifest.json)。失败日志不覆盖。完成时任务 Godot 进程均已退出，用户编辑器 PID 23084 仍保留。独立 Agent 只增补中性测试与本证据目录，没有修改生产、Core、正式剧本，没有提交或推送。
