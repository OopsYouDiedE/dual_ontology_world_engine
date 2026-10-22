# CLI 章末观察停机修复

修改独立调试65及工具说明；未修改50、60、法则、策略或预算。stop_at_chapter_end 等待客观 chapter_summary 和记录观察者的正式章末检视。默认 detective；detective_zone=none 沿用逐人物正文记录集合，任一现役观察者在 central_hall 或 oath_court 的成功检视即可。两处现有检视读取同一 closing_fields，不要求跨地点重复检视。

证据核对 completed_actions、applied action_execution、直接 host_completion 与 action_event。报告分列 chapter_summary、council_ended 真值、closing_observation 的人物/动作/回执。候选及导航不算完成。普通与 lazytext 路径仍在退出循环后刷新已取得正文。

原活动条件、动作上限、时域和外部超时有效。无人检视仍可在这些边界停止，closing_observation=false。council_ended 不作为停机前提，截止不冒充签字成功。

工程验证：build_session.py 生成调试65闭包成功；生成闭包的 .NET Release 构建成功，0警告、0错误。完成源码差异审读。未运行正式会话、AI、Godot或UI；实际可见正文与行为待主代理整合后统一300秒验收。
