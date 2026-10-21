# 宿主补丁审阅说明

仅生成 artifacts 文件，未应用生产源码；基线冻结期间不运行游戏或构建。

- e-cross-map-door-proposal-v2.patch：替换 WorldView.gd:1523 的 E 近门候选收集，CampusMapView.gd:811 前增加实际 Exits/Shape 矩形近距查询。汇总同图、跨图 available doorway，按 action id 去重，仅唯一时提交；沿原 Prepare/真实执行/Complete。e-cross-map-door-proposal.patch 是被取代的 v1 草稿，保留不应用。
- hint-visibility-reduced-motion-proposal.patch：WorldView.gd:1170 附近增加 GameSettings 低动态读取，绘制前以 source.site_visible(id) 过滤，低动态保留固定半径2火花并停止动画时钟。正常模式仍使用现有火花动画。

两份现役提案已分别执行 git apply --check，均退出0，只确认补丁可套用；没有 GDScript 解析、编译、游戏、UI或碰撞验收。两补丁修改 WorldView 的不同段，可由主代理整合；应用时须核对工作区是否又发生变化。

建议有限新运行覆盖：E 唯一跨图出口、移出近距、关闭入口、多候选、同图门；可见与墙后 new 热点、运行时切换 reduce_motion。不能借 UI 场景夹具宣称完整正式会话或剧情已验收。
