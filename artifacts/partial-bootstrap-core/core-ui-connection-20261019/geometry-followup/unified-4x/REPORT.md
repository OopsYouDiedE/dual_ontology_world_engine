# 集中修复与四倍播放：本批结果

本轮先完成已知修改并静读，后冻结源码串行运行player-render、geometry-probe、ai-render。host_configuration记录playback_speed=4、physics_ticks_per_second=240；Main默认四倍，--dv-speed可调整。DV游戏分钟、预算、判定和费用没有按倍率重写。

## 已完成的修订

当前乙区补回六个真实证物物件/站点；四寝区扶手椅移出原走廊通路，乙区工具台移出备餐门前，连同四张底图烘焙阴影从配方重生。原E10半径中有合法接近格(328,520)，有限方向取点漏选，另有粗格自由但连续脚点撞柜的情况；通用接近列表现在补齐同半径合法格中心，Campus.walkable复核实际脚点的矩形碰撞。

室外新增原生仪器阵/高地空间声明与矢量表具；通用宿主创建真实marker、sprite、solid。依据用户授权补定设计中的通路位置，设计文档同步。夹道绑定已有合法lane_east出生点，旧site:lane实际落在建筑碰撞内，未继续采用。室外entered_room改用真实地图边界和walkable，出生initial_anchor与导航anchor分开且都核占位。

正文保留真实execution_event、动作source_id、输出字段/序号组成的line_id以及原source_refs，并沿ChapterDialogue进入已绘制控件。显示序号不冒称源文件行号。C#同一行动者/观察者只导出一次immutable JSON，再分别Parse为独立事件字典；不同主体仍独立导出。此处为消除重复工作，未建立Core纯计算耗时收益。

## 实际证据

player-render在新正式会话显示真实候选，工程脚本触发首项按钮pressed（明确not_physical_input=true），沿原Prepare/现场/Complete移动。action17返回真实身体到达zone_yi:corridor、arrival_error=0；e247 applied @482；frame-01已实视，8:02人物站走廊并显示行动正文。界面按钮工程触发不等于物理鼠标键盘验收。

geometry-probe是独立纯几何夹具，不创建DvSession，不调用Prepare/Complete，不授予观察或知识。闭起居厅门条件下，E1/E2实际到门厅，E6到R08，E7到活动厅，E10到备餐柜侧合法格(328,520)，E11到R05置物架；各站点保留同一身体的实际起终点/房间/半径/误差。室外表具、高地与夹道也有真实follower到达回执及截图。E7距离原值46.0000038146973、半径46，属于单精度边界尾差，原值未改，不用严格distance<=46描述它。所有截图已查看；夹道人物带建筑遮挡的半透明表现，未称完全无遮挡。

ai-render在4倍配置下由本人共同决策实际完成侦探开场行动e134 @481，8:01显示正文、立绘与地图。visible记录中trace_id/presentation_id=e134、source_id=detective.opening.wake、line_id=e134:presentation.lines:0:0，source_refs保留原声明来源。停在对白等待继续，不把窗口未推进当成已根治卡死或完整剧情达成。

两份正式短会话close-result均complete=true且无在途动作/未处理命令；这只说明本窗口封存完成，活动会话均由采集脚本关闭。inputs/visible/close-result保留原字节，原始大trace/save仍在AppData，manifest给出路径、大小与SHA。几何夹具此次日志无上轮watchdog造成的ObjectDB退出警告；旧geometry-probe-01阻塞与warning完整保留。

## 工程与口径

编译在geometry-followup/build-01.log，.NET完成24.77秒。该编译对应的所有DV模块在本冻结阶段重新用dv_sources.sources标准化后与Session/obj/dv/source-manifest核对，generated-source-consistency差异为空；没有为纯地图/GDScript修改反复重建不变的C#生成库。最终Godot导入见import.log。raw文件字节的source-hashes与after-source-hashes逐项无变化；该口径与编译器规范化DV源码SHA分列，不能直接交叉比同文件哈希。

本批是工程接线与有限UI显示复测；未运行正式一级或三级，也没有完成全部UI组合。不能据局部动作与纯几何夹具宣称全章节、五日覆盖、Core根治卡死或稳定性能提升。跨图/离屏/多人占位的全部动态分支、恢复存档及真实键鼠输入仍未全面验证。人物既有mask=1和离屏独立World2D边界保留。
