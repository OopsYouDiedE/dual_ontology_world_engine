# 独立工程检查：doorways-r21

范围：中性机制检查；未读取设计规范、剧本、生成剧情或历史轨迹。没有修改生产代码、AGENTS，也未提交。

- Debug宿主构建通过，见build-debug.log。
- 首次Godot import退出0，但日志含WorldView.gd:221未声明objects的Parse Error。直接WorldView check-only退出1确认，见import.log与worldview-check-only.log。AgentPilot单文件check-only退出0，不代表依赖均通过。
- 主代理修复后WorldView check-only退出0，见worldview-check-only-fixed.log。
- neutral_doorways.gd直接调用正式WorldView与PilotNavigation，19项中性断言通过，见neutral-doorways-1.log。覆盖全雾、屏外、隐藏、透明门不公开，正常露出只公开门槛坐标；双向跨门中心输入方向保持；两侧已知无门口探索；仅沿已知连通地面靠近，隔断后无靠近路径；未知侧无walkable查询。
- 上述是headless机制夹具，没有实机截图，不是二级UI验收、三级盲玩或人物行为合理性证据。
- 纯dotnet中性用例Program.cs已保存。首跑因测试项目引用路径多一级失败，见controller-neutral-1.log。测试项目路径已修正，但收到暂停要求后未再运行。控制器撤下选项、hide/destroy不作未完成求知、角色偏好仍能选择以及直接菜单无调查奖励均尚未获得本轮运行结论。
- 静态待查：PlayerController的UnfinishedMethods排除了hide/destroy，但evaluatesMaterial仍包含全部menu；带同名Opportunity材料的hide/destroy菜单是否获取seek奖励尚需运行中性用例确认。不能据此声称故障已复现。
- 保留全部失败日志。未运行完整PlayerControllerCheck、统一回归、一二三级验收或真实r21。收到主代理暂停指令后停止额外检查；所有本轮启动Godot命令均已退出，未动用户编辑器PID23084。
