# 本轮工程与有限界面证据独立审查

2026-10-19。本代理未读取设计文档，未运行构建、会话或生产修改；仅只读日志、当前接线源码和本轮证据。本报告不作剧情、动机、推理正确性或体验改善判断。

## 工程记录

build-01.log记录Session与Godot程序集生成结束；godot-import-01.log记录Godot 4.6.1导入到编辑器布局就绪，所读日志未出现脚本/资源错误。这里只能说明该次工程运行结果，不证明完整游戏行为。

当前Main的player/ai导出参数与命令行配置均进入GameEngine.ConfigureSession，Session.OpenConfiguredCampaign调用生成DV HostOpen。玩家select进入同一Session.Prepare；AI未见另设宿主评分入口。Prepared携带执行主体快照，移动及显式站点行动等待DvFieldExecutor，核对actor/serial/action/map/room及站点误差后Main提交唯一dv_complete，GameEngine核对pending并去重后Session.Complete。非移动无站点行动使用DV世界规则。以上为静读事实，完整分支未运行覆盖。

旧静审报告所指保存现场pending问题，当前GameEngine已增加RequireNoPendingExecution，异步等待后再次检查；这是源码修订，保存/恢复的实际一致性仍未验证。当前截图状态包含access.maps；这里只确认投影字段存在，门禁碰撞未验证。

## 实际查看的画面和结果

使用view_image实际查看player-render-01的frame-00/03、ai-render-01的frame-00/03、player-movement-engineering-01的frame-05、player-movement-engineering-02的frame-01/05。图片为Godot viewport渲染采集；本轮未获得原生桌面键鼠交互证据，node_repl初始化失败不能描述为已完成桌面操作。

player-render-01可见地图、HUD 8:00与居中候选菜单，文字、分隔线和各选项未见截断；state为player_control=true、awaiting_input。AI画面可见房间中的角色、立绘、姓名、正文及继续按钮，HUD 8:01；state为player_control=false。正文仍等待继续，固定窗口中保持同一页不能据此判定游戏停滞。

player-movement-engineering-01未实际触发候选：日志无engineering_button_activation，末态仍8:00/awaiting_input，geometry仍north_1，画面仍菜单。主代理定位探针把modal_root wrapper当成ChoiceMenu；此轮应保留为未执行移动的工程定位记录。之后仅采集探针递归查找控件的修改，不是生产修复。

player-movement-engineering-02的日志明确记录工程触发“前往走廊”，not_physical_input=true。close/inputs.jsonl同时记录select与device=control_signal；不存在据此冒称鼠标或键盘输入的依据。该文件最后的action17回执记录：actor detective、serial 18、行动detective.move.R05.corridor、source_position [858,196]、actual_position [808,360]、source_room zone_yi:north_1、room zone_yi:corridor、status arrived、execution_world visible-godot、arrival_error 0及outcome applied。日志e247记录该行动于482完成；state-01与geometry-01的房间相符。frame-01与05实际显示走廊中的角色、8:02及“沿通路走到相连的房间。”，相机取景随位置改变。由此支持一次正常候选接线后的真实现场移动回执和可见结果，不能扩写成全图路径、跨图交接、遮挡或所有碰撞情况均已验收。五秒采样未捕捉完整移动过程。

## 正文来源和封存边界

PlayerViewTrace把button pressed单列为control_signal，键鼠另由InputEvent记录；该区分与本次工程触发相符。movement-02 visible.jsonl确实记录正文由partial到full，content_clipped=false，与实视末帧相符。但是trace_id、presentation_id、source_id、line_id均为空，当前可见正文不能仅凭这些字段直接逐行关联DV源事件；需要人工结合动作回执、日志和快照核对，完整来源编号接线仍有缺口。不能宣称已完成逐段剧情来源验收。

各窗口close-result.json均记录complete=true、无pending_action/未处理命令，未记录关闭错误；各运行日志均以host-closed-during-active-session结束，属于主动关闭的局部会话，不是剧情终局。逐一重新计算各manifest指向原始inputs/visible/close-result/session/save的SHA-256和长度，与manifest一致。大session/save仍保存在manifest指定的AppData绝对路径，未复制入本任务Git目录；本报告未完整解码大文件，关闭complete及指纹不替代内容逐事件审读，也不提供跨机器的原始大文件保全保证。

## 结论与未验证

本轮支持有限界面专项观察与一次按钮语义信号驱动的现场移动工程结果；未完成覆盖各场景的二级UI验收。没有执行一级正式逻辑验收或三级游戏本体验收，亦非独立盲玩。真实键鼠选择、更多候选、长正文分页、站点接触、跨图/离屏人物执行、群体碰撞、动态门禁、现场保存恢复、完整剧情与五日范围均未验证。人物碰撞互阻和独立World2D范围继续受原静审报告所列边界约束。

历史目录清理请求见CLEANUP-REQUEST.md：删除操作在启动前被policy拒绝，未删除、移动或绕过；不能称旧记录已清理。本报告不改写任何既有证据。
