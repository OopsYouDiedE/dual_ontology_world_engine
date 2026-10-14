# R18 流式关闭与持久化提案独立静态审查

日期：2026-10-15。只读给定 r18-protected-change-proposal.txt、现役 Session.cs、GameEngine.CloseTrace、Runtime JSON/Rational 转换及 SessionClose。未读取设计、草稿或私有轨迹，未修改源码、构建或运行会话。

## 结论

在保持两种既有 JSON 格式和关闭事件语义的前提下，直接向 FileStream 序列化可避免整份 JSON 字符串，以及 Save 的 SnapshotValue 全树数组/字典副本。先保存 inputs/visible、重型输出失败仍结束关闭，是合理的证据保全与故障恢复方向。它不改变 DV 决策、行动、判答、费用、预算或游戏时钟。

该方案只消除序列化边界的额外物化；完整状态和事件树仍驻留内存，SessionClose 构造或其他阶段仍可能失败，不能据此保证不再耗尽内存。

## 必须保持的格式区别

- 现役 Trace 调用 R.json_stringify，即默认 JsonSerializer.Serialize；Rational 的类型转换器写普通字符串，例如 "1/3"。TraceToFile 应直接序列化同一 SessionClose 返回值，保留这种格式。
- 现役 Save 先由 SnapshotValue 把 Rational 写为 {"$dv_rational":"1/3"}，列表保持数组。SaveToFile 的专用 converter 必须生成同样标签与精确 ToString 值，包括整数型 Rational，不能降为 double、改成普通字符串或输出 Numerator/Denominator 对象。
- 不要把 Save converter 应用于 Trace，或在 options 中启用 ReferenceHandler.Preserve 等会改变 JSON 结构的功能。保留列表/有序字段对表示，不能误用 HostValue 的对象投影。
- 缩进、转义、UTF-8/BOM、深度和数值处理应明确沿用既有口径；序列化失败应报告，不能静默丢字段或截断树。Restore 的标签解析与数组结构不变。

## SessionClose 的真实副作用

SessionClose 会向真实 events 追加 session_stopped，并打印停止记录。流式写入每次关闭只能调用一次该逻辑，不应先调用旧 Trace 再调用 TraceToFile，也不能在文件写失败后重新 SessionClose 作为重试。需要重试时复用已取得的关闭结果，另存新输出，不覆盖失败原件。

Save 在 Trace 之后执行会包含关闭后追加的事件，匹配现有关闭顺序。若改为先 Save，应承认存档内容发生变化，而非声称同值输出。格式兼容工程比较必须使用独立但相同的初态，不能在同一 live state 上重复调用有副作用的 Close 来比较。

## Host 关闭与证据完整性

1. 停止接受新行动并等待 in-flight work，避免边写边修改 List。inputs/visible 应在重型 trace/save 前写出；其冻结点与当前停止流程一致，不能提前遗漏最后交付记录。
2. try/finally 必须覆盖目录准备、等待 work 和轻量文件写入，不只是重型序列化，否则早期异常仍可能遗漏 TraceClosed。
3. 文件序列化可在 Task.Run 中进行；Godot API、ProjectSettings 路径解析及 EmitSignal 保留在主线程。finally 的关闭信号是宿主关闭完成通知，不代表游戏终局或所有证据写入成功。
4. 对 trace/save 分别标记阶段、成功、异常或 partial。trace 失败不应无说明地掩盖仍可能写出的 save；至少轻量输入和可见记录已保留。不要删除或覆盖部分文件，不把 partial 文件冒充有效 JSON。
5. close-error.json 应尽量轻量，只写阶段、文件和错误等必要信息，不序列化整个异常对象或会话。内存耗尽或磁盘失败时错误文件自身也可能失败，应保留可获得的最小日志并仍尝试 finally 信号；不能保证任意系统失败下错误文件必然落盘。
6. 新会话使用独立输出目录，文件写入失败/重试不能覆盖本轮已有证据或旧轮原件。可使用独立 partial/完成标记，完成标记只能在对应流成功 flush/dispose 后写出。

## 实施后最低核对

审查 exact diff 与 options/converter 的真实调用范围。用无剧情的中性嵌套值核对 Trace 普通 Rational 字符串、Save Rational 标签、列表/空列表/布尔/数字/文本及保存恢复，不用完整游戏重跑作格式测试。核对强制写入失败时轻量文件、partial、错误阶段与关闭回调，不把工程检查或计数作游戏验收。

此前 R17 缺失的原始输出保持缺失，不能由新序列化或另一轮会话补写。实施后的新正式会话须另立来源与程序集指纹，流式输出成功也不代替真实判答、画面或跨门验收。
