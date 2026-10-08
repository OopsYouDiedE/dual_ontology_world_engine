本轮完成第一章三处动作调度。分支为 `codex/ch1-staging`，开始时工作区干净；未读取设计文件，未提交或推送。

修改位置：

- `godot_csharp/scenes/Awakening/Script/story/第一章/阶段与流程.story:62`：烧水前先走到灶台、interact、等待 1.5 秒，再回相遇站位；领取时依次走到柜前、朝后 interact、等待 0.8 秒，霍铎随后保持 inspect；每人的核验场面播放后回原站位。侦探在 `.020` 前也走到柜前手动交互。离场分别在 `.004`、`.005`、`.006` 完成后发起单人 post，保留 `posts_assigned` 及其他流程的全员 `posts`。
- `godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:114`：实现非阻塞 `post` 和通用 `stage_action`；后者只在 `WalkOk` 成功后播放姿态和等待。`stage_actor` 支持带冒号的锚点名。走速为 130（动作走位）或现有 150（返回、岗位）。
- `godot_csharp/scenes/Awakening/Script/Chapter1Session.cs:212`：可立即启动作业的首个事件，使 post 的 walk 先于下一场对白输出，回执仍独立等待。
- `godot_csharp/scenes/Awakening/Script/story/剧情时长.story:15`：分摊拆分场面的时长，总计仍为 24 / 36 / 8 游戏分钟。
- `godot_csharp/scenes/Awakening/check/StoryPacketCheck/Program.cs:56`：原报偿效果检查遍历六个新片段，继续核验效果发生在对白回执之后。
- `godot_csharp/tools/compile_story.py:51`：登记新宿主命令；`godot_csharp/AGENTS.md:50` 同步接口说明。剧本生成物及 Godot 从 CSV 导入的三份 translation 已更新。

所有场面拆分都在 `godot_csharp/scenes/Awakening/Script/story/第一章/场面/01_开场.story`：

| 原场面 | 拆分后的场面（起始行） | 保留的原 ID |
| --- | --- | --- |
| s2_tea | s2_tea（22）、s2_tea_return（32） | .001–.007；.008–.015 |
| s4_rewards | s4_rewards（54）、s4_rewards_shen（61）、s4_rewards_huo（68）、s4_rewards_su（76）、s4_rewards_question（83）、s4_rewards_empty（91） | .001–.004；.006–.008；.009–.012；.013–.015；.016/.017/.018/.026；.020–.025 |
| s6_begin | s6_begin（117）、s6_begin_huo（124）、s6_begin_su（128）、s6_begin_detective（132） | .001–.004；.005；.006；.007 |

仅删除指定的 `s4_rewards.005`。没有新增对白行，没有改变或复用其他 ID；其余对白文字逐行不变，因此未运行 `--assign-ids`。现有音效资源没有烧水或接水音效，保留原 `.008` 的声音叙述。

新增锚点均在 `godot_csharp/scenes/Awakening/Script/story/第一章/提示与站位.story`：`备餐灶台`（72，394/558）、`领取柜`（73，500/132）。返回沿用四个 `meeting:` 锚点，离场沿用三个 `post:` 锚点。

独立验证 Agent 执行了以下检查，完整结论见 `independent-report.md`：

- 剧本 compile 和最终 `--check` 通过；`git diff --check` 通过。
- 指定的默认 `dotnet build ...sln` 和 `python tools/verify.py --only host` 均失败，日志只有“0 warnings / 0 errors”，诊断停在默认多节点 restore 子目标，尚未确定原因。最终单节点 `dotnet build godot_csharp/DualOntologyWorldEngine.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` 通过，剩一个 NuGet 网络不可达 NU1900 警告。原失败日志保留，未修改验证入口。
- 六个全新正式 Chapter1Session 连续会话通过：正常快速对白回执，以及灶台/四人领取动作分别返回 `WalkOk=false`。核验走位与对白的先后、三 post 不等待回执也继续到调查、`posts_assigned`、四人欲望各增加一次、原场面总时长。Sleep 用实际时间等待；此检查不代表真实游玩时长。
- 正式 WorldView 与 Awakening 地图渲染开发自检通过：全部新锚点、返回路径和三 post 均可达，130 速度下连续物理步进，interact/inspect 动画实际存在。7 张 PNG 位于 `render/`，由独立 Agent 查看。该检查注入起点和观察位置，不是盲玩或完整 UI 验收。

环境失败保留在日志中：Godot import 退出 0，但不能读取根证书库或写工作区外的 editor_settings；首次渲染不能打开工作区外 user 日志并崩溃。把 `--log-file` 指向本工作区并添加 `--disable-crash-handler` 后渲染成功，无需修改项目配置。WASAPI 初始化失败后回退 dummy，本轮未验证音频输出。

构建结论：单节点构建通过，默认 host 入口仍未通过。机制结论：本轮连续协议与导航/动画自检通过。人物行为合理性与整体游玩体验未作设计或文学判定。一级三份全程记录、二级完整 UI 专项、三级独立盲玩均未执行；按任务要求未运行已知无关故障的 `flow`。

未解决的视觉疑点：领取柜锚点位于铜口正下方，截图中没有明显独立柜体；本轮按指令新增锚点和手动交互调度，未制作柜体或分格开门资产。

证据：`independent-compile*.log`、`independent-build-final-single-node.log`、`independent-verify-host*.log`、`independent-protocol-smoke-final.log`、六份最终 `protocol-*.jsonl`、`independent-render-workspace-log.log` 与 `render/*.png`。早期验证夹具缺回执/漏记动画的失败记录也保留，后续修正只涉及临时夹具。

临时验证源码已归档到 `verification-source/`（原协议程序在 `scratch/ch1-staging-smoke/`），Godot 工具目录内的临时渲染脚本已移除。
