# perf 分支工程内容整合

分支来源 `perf/chapter1-seamless-controls-20261021`，tip `54e8a340d156c39743beb223dc729a6163b2412b`，包含 `614d7826`、`492a54de`、`54e8a340`。本轮按主代理授权只修改工作树，不操作 index/ref，不执行正式 Git merge/commit、不构建/导入/运行。合并前工程文件完整保存在 `before-engineering.zip`，路径/首次三方结果在 `merge-summary.json`；冲突预演保留在 `conflicts/`，这些文件包含冲突标记供审读，不是生产源码。

## 合入范围

47 个分支工程路径成组处理：输入映射与 GameSettings 版本迁移，UIInput 与 UID，BookOpeningPage 与 UID，本地化 opening_ui.csv/import，原生主题 SVG/import 与生成器，书页分页/返回/焦点状态，Main/WorldView/CampusMapView/FieldResources/GameEngine 连续门道链，UI 控件与捕获工具，以及对应 AGENTS 和工程文档。新增 20 个此前缺失文件。精确路径见 merge-summary.json。

## 冲突取舍

- Main 原18块：保留现役 title-ready 的资源错误回传、身份/渲染准备屏障、shader揭幕和 begin_opening(kind)；删除三方无冲突自动拼接进来的旧开卷/重复揭幕块。保留 _opening/_opening_failed 与新增 _view_faulted，书签装配失败仍保留载入锁和备份。_drain 保留2ms分批让帧及 _try_bookmark_save，再增加视图错误门控。加载/换会话同时清理书签暂存、正式选项与连续请求。_field_input 先核对正式周期，再保存连续请求/清理UI选项。awaiting_input 释放旧连续请求、安装现场选项，并保持推演手稿选项投影。Complete 保留玩家接触直至本人回执；按连续行动身份释放请求，NPC回执不清理玩家接触。PlayerViewTrace 保留 opening 上下文及单次扫描context，增加实际手柄输入记录。
- WorldView 原8块：正式选择默认关闭，由Main接通。保留 prepare_presentation(initial) 的实际身体/精灵/碰撞/视野绑定核对，合入房间灯光/尘埃渲染预热与局部导航预备；仍须通过主机真实物理/绘制屏障才揭幕。保留当前镜头/视野初始化，新增连续请求/碰撞到达/取消与失败后的真实返回协调。_interact 保留可见/接触/available及无候选提示并加入当前权威选项核对。保留 request_wait 仅等待候选；完整保留 action_id/hypothesis_id payload 拆核与原封装派发。保留 release_submission/can_submit、E近门和视野过滤。
- TitleUI 原8块：采用较新的 title-ready 启动/错误/揭幕实现，旧perf的 begin_opening/show_start_error/transition_to_field 不接成第二条生产路径。BookOpeningPage文件保留为可复用组件。合入右键/B逐层返回、book_prev/next、页历史、书签确认返回与页面焦点/滚动保存。保留startup失败锁、loading文本与现有书签保护，消除重复函数/字段。
- UISystem 原5块：保留音乐presentation token与scene_music的标题阻塞门控；保留prepare_for_reveal及host_input_blocked，合入UIInput逐层返回与手柄处理。保留本轮manuscript的当前选项、hypothesis封装与request_wait通路。新增的set_host_input_enabled接口与host_input_blocked同时作为输入门，不替代后者。
- godot AGENTS / tools README 各1块（换行规范化后）：两侧是不同工程索引追加，均保留；更新连续门道工程文档中的旧启动接口为本轮最终实际接口，避免文档称第二条旧开卷状态机仍在生产。

## 已做静态检查与边界

四个主要脚本无残留冲突标记、无重复顶层func/var/const；字面preload路径存在。Main/TitleUI/UISystem私有函数调用可匹配定义；WorldView额外两项为既有static函数，不是缺定义。未执行构建、Godot导入、输入探针或任何会话，不继承旧分支报告为本轮通过结论。

本轮新增或变化的连续门道、失败返回协调、手柄/右键返回、输入绑定迁移和渲染预热均尚待主代理冻结最终源码后验证；尤其需检查prepared期间转身/开UI取消、真实到达后权威拒绝的返回、NPC与玩家回执交错、书签恢复边界、低动态揭幕、手稿payload以及近门E行为。缓存存在不证明门口速度，合并完成不证明游戏效果。

host_audit代理已在合并后继续修改WorldView的hover/HintOverlay._draw/SpatialInput._unhandled_input可发现性；本报告的初始整合快照与该后续修改应在最终冻结时统一封存。
