# 宿主修补落地记录

主代理确认隔离副本完成并授权后，已将以下三个提案局部应用到共享生产源，保留此前改动：e-cross-map-door-proposal-v2.patch、hint-visibility-reduced-motion-proposal.patch、field-controls-door-hint-proposal.patch。

- CampusMapView.gd:813 新增 actual Exits/Shape 几何近距查询；WorldView.gd:1544 汇总唯一 available doorway，沿原提交链。
- WorldView.gd:1184 起读取现有低动态设置，1199 绘制前复用 site_visible；低动态保留固定半径火花。
- WorldView.gd:575 起在 active、无热点提示、can_submit 成立时显示当前准入门 label 或三语通用操作提示；未从隐藏状态生成目标。
- godot_csharp/AGENTS.md、godot_csharp/tools/README.md 仅增补对应接口、有限验收边界及未修复缺口说明，没有重排原文。

已与 manual_replay 子代理说明不修改 spatial.select，采用 git apply 局部接入。git diff --check 在两个源码和两份文档上退出0，仅提示文档工作树 CRLF 在下次 Git 操作时将转LF。没有构建、语法探针或游戏运行；由主代理在性能基线完成后统一验证。

跨图/跨房正式动作中途失败后真实实体与DV源地点分离的风险尚未修复；未改DV合同、未回写身体位置或放宽源身份核对。
