# Core 与 Godot 界面接线（2026-10-19）

本轮连接同一 GameEngine → DvSession → 生成 DV 类库；主入口在打开前通过 controller=player/ai 切换，viewer 与 campaign 参数交 DV 处理。玩家候选沿原共同资格和 Prepare/Complete；AI 沿原决策，不另建会话或宿主规划规则。输入串行排队、后台快照缓存、action_event 回执去重、在途现场存取拒绝及关闭证据边界见 HOST-SESSION-WIRING.md。几何与原生映射见 FIELD-EXECUTION-WIRING.md、NATIVE-UI-CONTRACT.md。

## 工程与实际呈现

集中修改和只读审查后统一构建。build-01.log记录 .NET 生成完成，耗时25.45秒；godot-import-01.log记录Godot导入完成，无运行脚本错误。构建本身不证明游戏行为。

- player-render-01：新会话8:00，实际显示四个DV候选；frame-01.png已查看，标题、时间、地图及菜单显示。无选择输入，关闭时仍awaiting_input。
- ai-render-01：新会话自行完成e130与e134，8:01实际显示侦探开场正文、立绘及地图。frame-01.png已查看。停在对白等待继续，没有假称后续剧情或移动完成。
- player-movement-engineering-01：工程脚本首次寻找菜单节点层级错误，未激活按钮，六帧仍8:00；原证据保留。这是探针定位失败，不是角色移动失败。
- player-movement-engineering-02：只修探针递归查找真实ChoiceMenu，不改生产源码。一次触发当前菜单首个按钮“前往走廊”的pressed信号；日志明确not_physical_input=true。真实Godot身体从[858,196]经寻路到[808,360]，源房zone_yi:north_1，终房zone_yi:corridor，serial18、action17、actor_instance_id82745231492；arrived且arrival_error=0。inputs中的唯一该动作Complete接受applied，e247记录时间482。实际查看frame-01.png，角色已在走廊，8:02显示行动正文。

这组是工程接线检查与有限UI呈现检查；按钮信号不等于真实鼠标键盘输入，没有执行正式一级或三级。截图由真实有窗口Godot视口采集。computer-use的node_repl初始化及reset后重试均报“failed to write kernel assets: 系统找不到指定的路径”，因此没有自动化键鼠验收。

各窗口close目录保存inputs、visible与close-result原字节；manifest记录AppData中原始session/save的绝对位置、大小与SHA-256，大文件不重复入Git。关闭结果均complete=true；此处只表示封存阶段无错误，不表示剧情完成。所有会话均在活动状态主动关闭。

## 当前边界

旧E1/E2/E11/E7/E10/E6站点和部分室外逻辑地点仍缺当前地图绑定，现场动作明确blocked；不能声称完整章节已连接或两章可通关。非move但显式travel.station也已进入真实几何，不能再用dv-world直接applied绕开接触。隔离门maps数据已接消费端；出生保留当前地图可用，跨门依权威门态，个人资格仍归DV。

人物原有mask=1（墙/地图）保持；人物之间不互阻，离屏每主体独立World2D。离屏跨图、缺站点失败、关闭中断、存档恢复、实体保留的全部动态分支尚未实测。未执行完整UI场景组合、碰撞/输入验收或性能五日会话，不作卡死根治或同条件提速声明。

## VS Code 与清理

默认solution与历史发现排除已改，真实来源为旧测试工程/检出，见VSCODE-PROJECT-DISCOVERY.md。未重载VS Code观察项目树。

用户选择删除全部旧artifacts/tmp/scratch，仅保留本任务。删除命令在创建进程前被策略拒绝（blocked by policy），没有实际删除或绕过。详情见CLEANUP-REQUEST.md。
