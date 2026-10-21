# 冻结后独立修补批次：对白称谓

依据主代理明确决定，UI-only表单采集全部结束并退出Godot后，仅修改根工作区 DialogueUI.gd 与 godot_csharp/AGENTS.md；没有修改tmp/detective-rpg-integrated-20261021冻结runtime，也未再启动Godot。

实际查看perf-integrated-90s-x4-03/run/frame-000084969.png，确认对白称谓显示内部ID `demon`。根因是旧_render_current_line在speaker_name为空时将speaker写入_speakers并用其作当前称谓和回看标题；已有缓存还会替后续缺称谓的行补旧名称。

修补后 DialogueUI.gd:199-204 当前显示及回看只使用本条speaker_name，缺失/空则留空；移除_speakers缓存。内部speaker保留用于portrait lookup，原始line_read及来源字段不改写。不从其他条目推定当前观察者的人物身份。

git diff --check退出0，仅AGENTS文档换行提示；没有构建、UI对白fixture或新会话验收。此修补不属于已冻结整合90秒性能/盲测版本，不能引用其历史截图宣布已修复。

根DialogueUI SHA256：2b9c17bc5fe0eb5ad37a69d203e722886690874a7f62c400c59cb6b2fedf2cd4。
冻结runtime DialogueUI SHA256：500d5edb82278643e900bcf38f0e406d31341927078bcc34689953b38560a48a。

表单UI采集与此前证据链仍完整保留在ui-replay-01/report.md及其二进制索引；该采集未使用此称谓修补。
