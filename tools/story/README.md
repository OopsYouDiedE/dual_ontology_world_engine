# 剧情作者入口

这里把 AI 生成候选约束为能在游戏里查验的内容。生成前先导出当前正式 DV，再生成带实际标识和来源要求的提示词；生成后用相同解析器初始化候选，检查调查链、意见、言灵、分支后果、返程与任务认知门槛。工具不连接生成服务，也不发送大纲原稿或私人运行日志；作者可以把生成出的提示词及所需项目上下文交给自己使用的 Gemini 或其他服务。

## 正式命令

依赖由项目内 `python mcp/game/run_environment.py` 准备。已有项目依赖时，在仓库根目录运行：

```bash
python tools/story/author.py export --content assets/data/scenarios/campaign.dv --output .runtime/story/author-view.json
python tools/story/author.py check --view .runtime/story/author-view.json --output .runtime/story/checks.json
python tools/story/author.py prompt --view .runtime/story/author-view.json --output .runtime/story/generator-prompt.md
```

`export` 使用正式 CLI 的 `--story-authoring-view`，通过 `DvDocument`、`CampaignScenario`、`CharacterSessionDefinition` 展开和初始化。它不调用 `Step`，不提交行动，不生成游戏验收回执。生成视图保留实际读取文件的路径与 SHA-256、内容指纹及来源集合指纹；`check` 会拒绝源文件已经变化的陈旧视图。

候选可以放在项目内独立目录，保留 `assets/...` 相对结构。例如候选只改一份剧情时，其他文件仍从正式源读取：

```bash
python tools/story/author.py export --overlay .runtime/story/candidate --output .runtime/story/candidate-view.json
python tools/story/author.py check --view .runtime/story/candidate-view.json --output .runtime/story/candidate-checks.json
```

候选目录不会自动写回正式源。涉及事实初值、角色规划或剧情链的改动应明确要求新局；不能把新事件补进旧存档冒称玩家已经玩过。

## 自动检查能证明什么

`story-contract.json` 约束前四日剧情与第五日自主观察。每条调查明确列出获取行动、推演、所得认知和被解锁行为；三种第三日布局分别列出物品变化、实际归还和结案行动。第五日的记录、复查和声学调查均为可选，任务前沿在自主阶段停止指导；观察窗结束不检查全员签字、归档或入睡。四院签名报告必须由本人已经实查的证据驱动，关窗报告路径不能要求或授予四项亲见痕迹。可选房与四院返程不能依赖已经过期的风窗或已离开的 NPC。声明中的意见有独立人物档案、证据、成本和风险；言灵必须有资源代价、反噬、时限，并影响实际协作或物理工作。

推演设计由生产 `CaseTheory` 检查：假定声明的证据都已取得时能否证明结论、空证据是否不能证明、证书是否通过校验，以及逐项去掉证据后的结果。这些是假设性作者检查，证据从未进入任何游戏人物的记忆。来源检查说明“存在声明的获取途径”，不能证明角色在所有时序下都一定能走到那里。

任务检查核对本人行动引用、有限无环依赖、真实完成认知与入场门槛；第三日只能用本人已读到的案件类型解锁任务，不能读取阮清月的内部方案。生理阈值与任务图分开声明，取水路线必须引用本人真实行动、已知水源、耗时与正向恢复效果，并有返回原调查处的正常行动。检查不会代玩家喝水，也不调整阈值。正确的门槛和 DAG 仍需正式运行验证实际抢先照护与任务续接。

`check` 同时调用 `focus_audit.py`，核对所有演员具有固定房间门槛的实体互动，是否按正式 presentation 绑定指向已发布房间的可见组件或同房人物；每个已发布逻辑房间必须有可见的门。脑内推演和谋划明确归为自身认知，不能用侦探身体代替缺失的外部家具。没有固定房间的休息等动作单独列出。旁边生成的 `.focus.json` 保留具体目标与所读场景的哈希；这项检查只证明静态目标存在，无法证明导航网格和实时障碍允许到达。也可独立运行：

```bash
python tools/story/focus_audit.py --view .runtime/story/author-view.json --output .runtime/story/focus-audit.json
```

维护检查器时可执行有意破坏的作者反例；它们只在内存中修改已导出的作者视图，不能当作游戏运行记录：

```bash
python tools/story/author.py negative-checks --view .runtime/story/author-view.json --output .runtime/story/negative-checks.json
```

## 人工审阅与实机门槛

文学审阅逐项核对玩家先见、反证、旧证据的新解释、人物动机和代价，并标出节奏上的局限。第五日只能回读实际发生的生活与选择，不能先写一个转折再用自动事件兑现。自动检查不检查文本是否出现“反转”等词，也不会给文学性颁发 PASS。生成稿必须保留未排除项，不能把一个有效推论扩成读心、绝对无辜或全世界唯一解释。审阅结果及运行回执保留在开发环境，属于工作记录，不作为游戏发布资源。

自主生活检查核对观察窗、第五日无剧情任务和定时事件、无强制收束门槛、增量资源有数值初值和世界侧消费门槛、多个独立角色能作用于同一资源，以及重复感知有在场条件。作者契约同时列出每种库存的实际房间与五类携带物的起讫地点，检查器会拒绝跨房透视库存、远处消耗物资或改动他人的持有量。 患者自身状态必须有不依赖既成护理结果的真实获取途径；五类携物在所有自主日可达地点均须具有明确投递路，领取同类物品前须确认自己未持有它。`cooldown_minutes` 约束重复动作的成功完成间隔；`repeat_on_change` 只在真实字段版本更新且本人仍能看见时重新取得见闻。库存预留、实际携带、耗时与需求成本仍须实机核对。

涌现评阅须从实际因果审计重建至少三次分别作出的决定、至少两名角色，以及中间真实改变的资源、身体、认知或机会。还要查看当时合法的替代行为、饥饿或疲劳等成本，并优先寻找后续反馈如何改变前一个人的下一步。固定高优先度顺序、预写事件和既定分支被选中都不能独立证明涌现。观察结束可以没有可确认的涌现，不能将两种结果合并成一个通过旗标。可中断的同桌交谈以 interruptible_opportunity 明确声明机会失效条件；只有真实前提变化、零提交且显式失效条件得到核验时才归为可恢复取消，原始失败原因仍保留，既不获得成功结果也不奖励身体需求。

内容结构通过之后，仍须使用正式 `dual-ontology-auto.GodotMCP` 完成全 AI 五日，并由独立 SubAgent 通过 `dual-ontology-input` 新局接管侦探。真实观察、搜证、选证推演、独立意见和言灵后的后果必须留有原始回执；CLI 和作者检查都不能替代 3D 导航与实机试玩。旧版本报告保持原内容指纹，不能重标为新规划版本已通过。

## 人类行记

正式 CLI 关闭会话时派生中文阅读副本与 `.events.json` 来源详情，`NativeCli` 返回 `readable_log_path` 并在关闭后标明是否写成。也可对已有真实机器文件生成单独副本：

```text
dotnet <本项目正式构建目录>/DuelView.Cli.dll --render-cli-log <actions.yaml> assets/data/scenarios/campaign.dv <项目内输出.txt>
```

阅读副本只显示本人行动、亲见、实际收到的报告和推演，按天整理，并折叠同日重复生活动作。任务／身体／玩家来源取自行动开始时保存的 `decision_intent`，旧记录没有该字段时不猜测补写。行动文件和状态文件的数量、时段、SHA-256 与不完整尾块分别标出，状态记录不会被补写成缺失的行动。Godot 公共日记采用同一 [readable-log-contract.json](readable-log-contract.json)，不得从完整作者日志泄露其他人物暗谋。
