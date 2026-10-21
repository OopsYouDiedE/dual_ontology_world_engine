# Host 场景合同

## 所有权

Main 通过 `flow: HostFlow` 持有界面和会话交接状态；`GameEngine` 独占 DvSession；`DvFieldExecutor` 独占身体登记、离屏物理世界和动作事务。UISystem 读取观察者快照，发回纯数据输入。控制台开发修改经 GameEngine 转给 Core。

`GameEngine.Snapshot()` 返回最新已完成图的浅副本。嵌套字典和数组按只读使用。`Project(viewer)` 只转换 StateDelta.Changed，移除 Removed，保留上轮静态值。执行者与 viewer 相同复用图；其他主体使用 ExecutorProjection。持久化通过 CasebookPersistence 的文件入口处理。

## 输入

| kind | 字段 | 行为 |
| --- | --- | --- |
| start / load | path（load） | 打开会话并发出 view |
| view_ready / dv_next | 可选 schedule | 正式 Prepare |
| select | choice_id、interaction_id、site、可选 preview_token | 正式选择并准备动作 |
| prepare_ahead | choice_id | 分支 Prepare，返回令牌 |
| cancel_ahead | — | 丢弃分支结果 |
| npc_tick | — | 玩家等待期间处理同一时刻的 NPC 工作 |
| dv_complete | receipt | 结算已执行动作 |
| dev_command | command | inspect / tp / time，Core 开发模式授权 |
| replay_load | path | 载入独立回放播放器 |

`Main.dispatch` 另处理纯 UI 导航、书签和 quit。普通 UI 不发送 tp/time。空间页回传 spatial_select；它回到 WorldView 的当前选项与接触入口。旧 choice/card_action 通路已移除。

## 输出

| kind | 字段 | 主线程处理 |
| --- | --- | --- |
| view | resource、snapshot、casebook | 准备并装配新现场 |
| state | snapshot | 安装观察者投影；保留现场最高版本 |
| dv_action | intent、execution_snapshot | 处理 awaiting_input 或执行 prepared |
| dv_commit | action_event、actor、outcome | 立即提交或回滚对应身体事务 |
| dv_present | presentation | 排队呈现观察者收到的文字 |
| prepared_ahead | choice_id、token、state_version | 缓存同版本选择令牌 |
| dev_state | snapshot（development_resync） | 重建现场与保留实体 |
| dev_inspect / dev_error | data / message | 更新控制台 |
| replay_loaded | count | 更新回放面板 |

Complete 的顺序为 `dv_commit → state → dv_present`。dv_commit 不等待读完文本。阅读期间先接入完成的身体状态，后续文字在阅读队列中保持原顺序。Main 的正常状态快照不会覆盖更高版本的现场。

## 事务与几何

执行器记录 `{actor, map, room, position, resource, snapshot, serial}` 作为 rollback anchor。记录以 action_event 为键，开始移动时保留源占位。applied 后提交；拒绝后恢复源位置与原地图障碍。该规则覆盖 station、同图和跨图、玩家及 NPC。

WorldView 的 `restore_movement_anchor` 仅由结算回滚入口调用，并返回恢复是否完成。执行器在成功后释放 anchor；恢复失败保留该事务并通过 RecordHostFailure 进入明确失败封存。正常到达由真实路径和碰撞产生回执。开发重建通过 development_resync 使用权威当前 anchor，普通首次物化使用 initial_anchor。

GeometryCatalog 缓存 Contracts/geometry_contract.json。CampusMapView 从其 additions 装配额外站点和物件，用 rooms.rects 分类脚点，导航继续使用正式场景碰撞。地图 UI 使用同一几何、观察者 map_knowledge 和当前公开选项。

## 生命周期、审计和演出

退出统一调用 HostLifecycle.quit；它先广播 begin_shutdown，再等待 wait_for_shutdown，最后释放音频并退出。Main 停止新输入后继续接收已执行动作的 receipt / commit。尚未进入执行的 prepared 留在轨迹与关闭清单的 pending_action 中，存档取 Core 最近提交点；退出不生成失败尝试、收费或行动步数。手动过门在退出时返回 blocked，由 Complete 触发统一回滚。身体、地图和界面过渡保留到物理事务及 IsDispatchIdle 边界排空，然后关闭场景并调用 GameEngine.CloseTrace。后者关闭 Core、写独立记录与最新已提交存档，最后发 TraceClosed。

RuntimeDiagnostics 与 PlayerViewTrace 默认停用。`--dv-audit` 或 `Main.set_frame_audit(true)` 开启；关闭时移除绘制和控件信号。3D 页的文本记录只描述纹理内容，冻结旧页不扫描下一页控件。遮挡由实际渲染图像证明。

DevConsole layer=128；输入反引号/F12。性能页保存最近 120 帧。SmartReplay 为 Core 与 Godot 共用的只读记录播放器；回放示意独立于现场权威。

MockSession 与正式桥暴露相同 Godot 信号和调用面，使用 Contracts/presentation_mock_pack.json。样本执行只在样本状态中转换，正式书签不被修改；记录文件回放仍使用共享读取器。
