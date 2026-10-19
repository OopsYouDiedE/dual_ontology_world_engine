# hitch-optimization-4x-02 实机记录

本轮独立Luna仅按本轮PNG和普通InputEvent自主探索，四倍物理、240Hz、每帧最多32个物理步；完整DV审计，托管采样请求200现实秒。具体阶段与限制见 ../hitch-optimization-analysis/REPORT.md。

实际捕获：240.000秒；游戏钟实际08:00→08:04。归档与宿主关闭：245.338秒；启动器生命周期248.500秒。三种时长独立，不按倍速换算DV游戏钟。

全部绘制间隔最大900.799ms，明确分列启动：5秒后最大187.969ms。每次>=50ms绘制间隔及组成见hitches.json和frame-gaps.csv；其中exclusive_wall_ms为互斥墙钟重叠分配，不是CPU耗时或因果证明。正常行走、阅读及操作者思考不作为计算卡顿。后台PNG编码/写盘只列background_archive，不重复加到主线程阻塞。

主线程纹理读回中位18.981ms，像素复制入队中位4.647ms；后台编码中位17.118ms。各中位数不相加冒充某一帧耗时。归档错误与背压没有记录；frames索引与成功后台任务逐文件对应，索引文件在本地存在。png-pixel-review.json只核对五张PNG解码RGBA与后台原像素SHA，并非全量像素验证。

实际画面与输入按OPERATOR-NOTES.md阅读，独立审查见INDEPENDENT-REVIEW.md。未消费的末尾input.json只保留请求，不当作已执行。现场位置只作空间证据，不代替剧情节点；本轮未完成章节或全程体验验收。关闭完整只表示相应原始证据保存完成。

source-before/source-after和result.source_changes记录本轮冻结；未出现源码/程序集变化。原始输入、可见正文、逐帧计时、测量和现场事件保留原字节。完整session/save/nettrace及未选PNG保留本地，字节指纹见evidence-sha256.json；Git只带选定原图和索引/日志。不覆盖既有证据。
