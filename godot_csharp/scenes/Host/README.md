# Host 场景合同

## 所有权

Main 通过 `flow: HostFlow` 的三个状态轴和集中 transition 持有界面、会话和执行交接状态；`GameEngine` 独占正式 DvSession；`DvFieldExecutor` 独占身体登记、离屏物理世界和动作事务。UISystem 读取观察者快照，发回纯数据输入。控制台开发修改经 GameEngine 转给 Core。

Core 投影提供 `view_kind`、`actor`、`location_title` 等中立语义字段。`HostPresentation.gd` 根据 `Contracts/presentation_catalog.json` 增补 Godot 场景和人物资源，Core 不输出 res 路径。Main 只在正式会话路径动态加载 C# 入口；纯 GDScript Mock 使用相同呈现边界。

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
| view | view_kind、snapshot、casebook；资源由宿主补充 | 绑定呈现目录并装配新现场 |
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

执行器记录 `{actor, map, room, position, resource, snapshot, serial}` 作为 rollback anchor，并保留实际走过的 trail。记录以 action_event 为键，开始移动时保留源占位。applied 后提交；拒绝后玩家沿 trail 反向执行真实碰撞行走，恢复原地图障碍；离屏 NPC 可直接恢复已保存的源锚点。该规则覆盖 station、同图和跨图、玩家及 NPC。

实际 move 事务把归一化 trail 写入原回执的 `receipt.field.trajectory`。跨图轨迹记录 `entered_by` 的真实 from/to 映射，供回放还原门道坐标变换。轨迹附属于原行动事件，不产生额外权威提交。

WorldView 的 `restore_movement_anchor` 仅由结算回滚入口调用，并返回恢复是否完成。执行器在成功后释放 anchor；恢复失败保留该事务并通过 RecordHostFailure 进入明确失败封存。正常到达由真实路径和碰撞产生回执。开发重建通过 development_resync 使用权威当前 anchor，普通首次物化使用 initial_anchor。

GeometryCatalog 缓存 `Contracts/geometry_contract.json`，提供 map_data、additions、point、rect、markers、room_at、room_data、anchor、routes 和 bind_layout。路由、站点、站位、出生点、房间与出口全部来自该合同；正式场景提供画面与碰撞实例。出口保留 path、point、rect、target、spawn、internal_gate 和可选 gate，室外分区保留 origin/core/cores/origins 坐标。跨图目标和出生点在发布时完成 living_hall → living_floor 的显式映射，内部门禁不作为跨图路线。

CampusMapView 从 additions 装配额外站点和物件，用 rooms.rects 分类脚点，导航使用正式场景碰撞。地图 UI 使用同一几何、观察者 map_knowledge 和当前公开选项。完整合同校对属于 `mcp/sandboxes/GeometryContractValidator.gd`，MapSandbox 在运行绑定前调用；生产 GeometryCatalog 只读取和绑定几何。

## 生命周期、审计和演出

退出统一调用 HostLifecycle.quit；它先广播 begin_shutdown，再等待 wait_for_shutdown，最后释放音频并退出。Main 停止新输入后继续接收已执行动作的 receipt / commit。尚未进入执行的 prepared 留在轨迹与关闭清单的 pending_action 中，存档取 Core 最近提交点；退出不生成失败尝试、收费或行动步数。手动过门在退出时返回 blocked，由 Complete 触发统一回滚。身体、地图和界面过渡保留到物理事务及 IsDispatchIdle 边界排空，然后关闭场景并调用 GameEngine.CloseTrace。后者关闭 Core、写独立记录与最新已提交存档，最后发 TraceClosed。

PlayerViewTrace 的完整原始输入事件记录常开，新增控件通过 node_added 接线。`--dv-audit` 或 `Main.set_frame_audit(true)` 开启逐帧扫描和可见记录，关闭审计保留输入事件记录。呈现样本采用 `kind: presentation`，与 SmartReplay 的读取合同一致；记录保留完整身体样本和原始输入。

3D 页的文本记录描述页面纹理成员与冻结页身份，冻结旧页不扫描下一页控件。G7 记录不证明逐像素遮挡，实际遮挡由渲染图像承担证据。记录存在、页面纹理有内容与玩家实际可见分别表述。

DevConsole layer=128；输入反引号/F12。Inspector 页每 0.25 秒刷新 Physical、Mind 和按 score 排序的候选 Tree，离页停止刷新。性能页保存固定 120 帧环，记录 FPS、frame、prepare、complete、navigation，并按帧累计阶段耗时。性能页关闭时停止诊断采样。

## 记录回放

SmartReplay 是 Core 文本入口和 Godot 共用的只读记录播放器。ReplayStage 在独立 SubViewport 中使用 ReplaySession、Main、WorldView、StoryActor 与真实 3D 书页 UI，按演出时钟播放、暂停、单步和调速。舞台仍在消费当前帧时不提前推进下一段。

交互采用 `focus → confirm` 两拍。定位帧安装已记录的前态与选中项；确认帧从 `data.result_kind` 读取移动、对白、调查或拒绝类型，演出后安装已记录后态。`cancel` 清除定位。`ReplaySession.SetInteraction` 只更新展示元数据；每次时钟更新最多交付一个新的可见拍，零时长元数据也有消费上限。记录中的思考停顿不进入播放时钟。

回放消费记录的输入、事件回执和 `receipt.field.trajectory`，经过 WorldView 路径执行与 StoryActor 的真实碰撞，并采用记录的 entered_by from/to 处理跨图门道。输入与回执通过 action_event 去重。实际身体样本和原始输入用于重现已记录动作；回放不提交新的正式世界行动。

旧日志没有 trajectory 时，播放器按正式几何进行语义重建并标明范围；缺少源位置时只保留原文本，不捏造人物移动。这种记录的显示不代表原始逐帧画面，也不补造缺失证据。

回放模式把设置端点注入为独立内存 `ReplayPreferences`。书签页展示记录清单，回放界面不调用正式世界保存/恢复，也不打开系统文件选择器。回放 UI 与正式现场状态分别持有。

## 纯 GDScript 演出样本

MockSession 与正式桥暴露相同 Godot 信号和调用面，使用 `Contracts/presentation_mock_pack.json`，通过 ReplayClock 消费样本。样本包含 casebook、form、dialogue、door 与地图呈现，状态转换只发生在 Mock 内部，正式书签不被修改。

从仓库根目录运行 `python mcp/godot_workspace.py --mock-ui`，或调用统一 MCP 的 `ui_mock`，会建立以 `Main.tscn` 为主场景的纯 GDScript 工程并传入 `--dv-mock`。该路径不创建 C# 项目、不构建或打开正式 DvSession，工具用户数据和临时工程随窗口关闭清理。

本轮仅完成实现、接线与文档，没有执行测试、检查、构建或探针；本文不声明运行通过。
