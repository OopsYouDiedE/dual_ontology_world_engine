# 初醒 Demo：一次连续输入通关记录

日期：2026-09-26。入口：`res://scenes/FirstAwakening/FirstAwakening.tscn`。工作区包含未提交修改，基准提交 `91d3b62`；本记录依据当前工作区文件。

## 方法

使用 Godot 4.6.1 .NET 的非 headless 视口运行 `scenes/FirstAwakening/check/full_input_playthrough.gd`。脚本从新场景开始，通过 `Input.parse_input_event` 发送方向键、E、Tab、F、Enter、鼠标点击和中文 Unicode 按键。寻路只读取现有地图与碰撞来决定下一次方向键；检查只读取游戏状态。脚本没有传送角色、写入物证、行动序列、推演结果、答辩词槽或结局状态。行动草稿由按键逐行输入，三个词槽由菜单选择。

这是预先知道目标与可行答案的**玩家输入链回归**，不是未读谜底的探索试玩。截图均来自同一次通过的运行视口；测试初期用于修正驱动的失败截图不计入本次通过记录。

## 经过

1. [开场](full-playthrough-01-opening.png)：从床上醒来，推进对白；移动键起身。随后实际走到抽屉、包纸与 R-05 门牌调查。[宿舍调查后](full-playthrough-02-bedroom-evidence.png)。
2. 沿宿舍门和走廊实际行走，与工程师交谈，调查研习材料和同源撕口。靠近铜口按 E，先读[使用守则](full-playthrough-03-transmitter-rules.png)，再按 E 听广播；三名同伴在正常时间尺度下走到研习桌。[广播后现场](full-playthrough-04-broadcast.png)。
3. 实际走到备餐间取得口粮、湿布物证，再到门厅用 E 与 Tab 选中隔离门；通过可见按钮检查窄孔、门轴与配重。[门厅现场](full-playthrough-05-hall-evidence.png)。
4. 返回研习桌召开[四人会议](full-playthrough-06-meeting.png)，点击三种解释的反驳并推进对白，取得答辩代表资格。
5. 按 F 打开推演，在 TextEdit 中用键盘逐行写入[八句行动草稿](full-playthrough-07-draft.png)，点击“从头重演”。Core 返回无冲突结果；通过“下一幕”逐幕推进。[回放](full-playthrough-08-replay.png)、[末幕](full-playthrough-09-replay-end.png)。
6. 用推演页的“标记位置／核对”控件，回现场按 E 检查尚未取得的门底痕迹，再核对包纸转印与双套锁止。[三次核对后](full-playthrough-10-three-checks.png)显示剩余核对为 0。
7. 返回研习室铜口，重新接通并选择“提交论证”；在[答辩界面](full-playthrough-11-answer.png)的原生菜单选择“长期生活研习”“先撑门进灰 → 撤楔合门 → 沾灰后包纸”“生活翼内销 + 主楼侧外闩”。提交后出现[本段结束](full-playthrough-12-ending.png)，游戏状态为 `ended=true`。

## 结果与边界

- 这次连续输入路径通过，未触发产品代码修复。C# 构建 0 警告、0 错误。
- 独立快速回归 `check_first_awakening.gd` 同样通过，覆盖 Core 正反例、三次核对、会议反驳、两个接受答案和重启；它会直接设置场景状态，不能作为连续玩家通关的替代证据。
- 连续输入只走了一条已知解路径；错误答辩、另一接受答案、安全退场与真正不知谜底的推理体验，没有在这一次从开局连续重走。中文输入使用 Unicode 按键事件，没有验证系统 IME 组合输入。系统根证书读取报错未阻止离线游戏运行。

复跑命令（非 headless，截图需要渲染帧）：

```powershell
& 'C:\Users\iii\Desktop\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe' --path godot_csharp --script res://scenes/FirstAwakening/check/full_input_playthrough.gd --log-file 'C:\Users\iii\Desktop\dual_ontology_world_engine\artifacts\first-awakening\full-playthrough-godot.log'
```
