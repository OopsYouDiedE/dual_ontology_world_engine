# Core 参考与内容修订结果（2026-10-16）

本轮补全 Core 参考文档及同步维护要求，并按用户附件修订生产源码。所有源码修订停止后执行唯一一次一级测试；该次会话在初始化时异常退出，未产生实际游戏事件或玩家正文。本轮不能认定全部问题已修复，也没有完成剧情与机制验收。依用户“测试一次，结果不论如何，直接汇报”的要求，失败后未修改源码、未重跑。

## 文档与实现范围

现行参考为 [language_core/core/README.md](../../../language_core/core/README.md)，包含可执行表层语法、启动语法、内置操作、Core 机制、声明字段、正式会话合同、宿主边界及源码接口索引；未实现的提案分别注明。根 AGENTS.md 给出路径并要求相关源码变化同轮更新说明、示例和索引，模块 AGENTS 与 README 同步。

确定性分块由 Sol 6.1 子代理完成，主代理依据设计规范核对并接线。修订覆盖认知配置与重演限制、共鸣和病理、真实收闻与操纵后果、第一章各寝区调查和答辩、第一夜、第二章日常和调查、连续章节接线。具体文件、工程参数和实现边界见 [source-review.md](source-review.md)。原始用户附件见 [request-audit.txt](request-audit.txt)。这些均为源码变更说明，不是已发生行为的证据。

## 唯一一级测试

执行命令：

```powershell
python language_core/tools/run_debug.py --out artifacts/partial-bootstrap-core/core-reference-alignment-20261016/level1-once --timeout 600
```

执行器为 formal-model，不含 Godot 几何。最终源码生成程序已完成编译，但运行进程在约 0.969 秒后以 exit_code=1 退出；该时间不是有效游玩或剧情推进时间。停止原因是初始化异常，不是行动上限、工程超时或剧情终局。

原始错误为 `System.Exception: Expected List`，调用链为 `R.Items → R.record_index → dv_has_key → c1dep_unify_demon_position → c1dep_install_scene → dvg_open_config → dvg_open`。源位置为 `language_core/verification/scenarios/chapter1-departure.dv:379`，递归调用位于第387行。源码审读显示该函数只判断值为列表，就调用要求有序记录结构的 dv_has_key；递归会访问列表成员，列表判断不足以保证记录形状。这是与栈位置对应的源码原因分析，运行没有封存具体触发成员。

错误发生在 debug_start 之前。stdout.jsonl、events.jsonl、调试轨迹.txt 均为空，没有 trace.json；玩家可见输出只有导出器说明行。result.json 中 output_well_formed=true 仅表示空输出未遇格式错误，text_exported=true 仅表示导出文件写出，均不表示会话完整。没有可引用的本轮事件编号、执行回执、后续观察或关键节点时刻。

证据： [result.json](level1-once/result.json)、[stderr.txt](level1-once/stderr.txt)、[源码指纹](level1-once/source-hashes.json)、[最终编译日志](level1-once/build-1.txt)。独立审查见 [independent-review.md](independent-review.md)：使用全新上下文，不读取设计文件，不修改或执行源码。

## 工程事实与保留边界

DV 生成与 .NET 构建日志分别保存在 engineering-emission-01.log、engineering-build-01.log；最终会话工具的编译日志在 level1-once/。源码指纹与运行前后字节一致，另在 source-snapshot/ 按原始字节保留指纹清单对应文件，避免 Git 换行归一化影响追溯。历史保护基线未改，原始证据只新增。

git diff --check 返回非零，记录部分修改行的尾随空白/换行警告，原始输出保存在 engineering-diff-check.log。未在失败后修改源码以清理格式。生成文件从工具产生，未手改。

构建仅说明相关生成源码能够编译。初始化错误仍未修复，后续可能存在的运行错误未知。共鸣的具体信任变化取决于内容声明；完整语义冲突覆盖、未执行的动态分支、所有新增行动的可达性与人物选择均未验证。新增地点的 Godot 现场绑定、碰撞、输入和画面未实测。

一级：实际启动但初始化失败，未形成剧情与机制验收证据。二级、三级：未执行。人物行为合理性与游玩体验没有可用的本轮行为证据；不以编译、文档覆盖或空输出代替验收。
