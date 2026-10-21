# Core 终端入口

`DvCli.csproj` 单向引用 host_bridge，不依赖 Godot、检查程序集或 oracle。
从仓库根目录启动 `dotnet run --project dv_core/cli/DvCli.csproj -- --tui`。
`--help` 和 `--replay` 不打开正式会话。单独构建不会执行会话。

## 房间分组与彩色终端

`--tui` 使用同一TerminalModel和ANSI真彩/Unicode单元画布。宽屏显示
房间分组、已知拓扑、事件和候选；窄屏改为纵向面板。中文按显示列宽处理。
`--no-color` 或 `NO_COLOR` 去掉颜色，文本及JSON-Lines入口继续保留。

每个房间标题后紧随其条目。地图只读观察者的map_knowledge；连接不代表
门已开或已获准通行，当前记录无门状态时明确标未知。当前房人物只读
观察者投影；其他房间没有人物记录时不写“空房”。不读取Inspect或世界真值，
也不虚构规划评分、置信度或外部行动理由。

无会话预览与同画布导出：

```powershell
dotnet run --project dv_core/cli/DvCli.csproj -- --preview --width 120 --height 42 --export-ui NEW.svg
python mcp/verification/render_terminal_export.py --svg NEW.svg --out NEW.png
```

预览的Room A/B/C和候选为显式UI示例，不启动Core或AI；导出是实际渲染器
单元画布，PNG不冒称操作系统终端截图。SVG/PNG路径必须不存在。截图工具需要
Pillow及中文字体，非Windows通过--font指定字体。MCP路由为diagnostics的
terminal-export。设计修订见[界面设计](../../docs/dv-core-terminal-ui.md)。

也可用`--tui --command status --export-ui NEW.svg`导出真实开局只读投影，
不调用Prepare或推进AI；options命令同样可导出。已有记录可用
`--replay FILE --tui --command step --export-ui NEW.svg`导出首帧，不创建会话。
导出不支持会推进动作的命令，也不导出自动循环。

`--width 60..240 --height 24..100`设置预览和重定向画布；交互终端按实际
窗口尺寸重排。height为最小高度，长内容纵向流入终端滚动区，不静默截断。
记录回放继续共享SmartReplay，restart清空显示缓存；未实现
左箭头回退、历史分叉、M热切换或LIVE游标，不提供这些误导快捷键。

## 会话命令

默认由用户控制 detective；`--controller ai` 保留引擎自主控制。
启动和 status/options 只读当前边界，不自动开始调度。`next` 每次只调用
一次 Prepare，或对已 prepared 的动作调用一次 CompleteFormal。
`act 01` 或 `act <action_id>` 先到达正式人工输入边界，重核当前候选，
再沿 Prepare → formal-model 执行 → Complete 推进到下一人工输入边界。
它不指定 NPC 决策、路线、答案、预算或测试上限。

`act` 也接受完整选择对象，例如 `act {"id":"当前真实候选ID",...}`；
实际字段以该选项的 replay_input 及 Core 选择合同为准，终端不补造答案。
编号仅对应当前 options 列表。options 显示 Core 提供的成本和交互位置，
status 显示本人位置、同房可见人物及最近实际观察。
执行者私有 pending action 不传给文本观察者。

`--jsonl` 从 stdin 接收一行一条文本命令或
`{"command":"act","argument":"01"}`，stdout 返回带 kind、scope、data 的
JSON-Lines；Core 的文本诊断走 stderr。TUI 和 JSON-Lines 为互斥输出模式。
`--command "status"` 执行一条命令后退出；stdin EOF 或 quit 结束交互。

inspect、tp、time 只允许显式 `--dev` 的开发会话；位置/时间修改交给同一
Core 开发法则入口。tp 的参数为当前观察者的目标地点，与 Core 合同一致。
开发模式不能替代正常行为验收。

## 保存与回放

`--restore <file.dvsave>` 恢复稳定提交点；`--save <new.dvsave>` 和
`--trace <new.dvdag>` 在退出时分别保存稳定状态和封存轨迹。
也可使用 save/trace 命令写新文件；trace 为只读，不关闭会话。
退出只调用一次 Close，并独立尝试两个输出，任何失败返回非零。
Ctrl+C 在当前同步 Core 调用返回后停止，仍经过相同封存链，退出码为 130。
所有二进制输出沿用 Core 的校验、partial 和不覆盖已有文件合同。

`--replay <file.dvdag|file.dvlog> --speed 4 --tui` 直接消费共享 SmartReplay，
不创建 DvSession。移动、调查、对白使用共享内容节拍，记录的现实等待不
进入回放时钟。播放时空格暂停/继续，右方向键单步，1/2/4/8 调速，Q 退出。
重定向输入没有键盘轮询；`--command step` 可只读取首帧。

formal-model 回执只验证逻辑，不包含 Godot 几何、碰撞或真实设备输入。
实现交付不表示 Core AI 测试已执行。测试须等用户批准并指定要求及指标。
