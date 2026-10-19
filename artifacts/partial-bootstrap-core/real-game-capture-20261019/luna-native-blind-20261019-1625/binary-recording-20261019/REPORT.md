# 二进制记录与磁盘清理（2026-10-19）

用户授权：暂停Luna盲玩并释放磁盘；保留本次及Gemini当前轨迹，删除其余历史轨迹；禁止新轨迹记录使用JSON，批准执行。

## 已执行

pip官方cache purge释放约1.31GB；随后按../disk-cleanup-20261019/deletion-manifest.json逐文件校验路径、大小、修改时间与排除目录，删除旧trace/session及其未完成、压缩原始轨迹。删除文件逻辑大小178751795231字节，删除前实际占用167174326008字节；当时C盘可用190338097152字节。原始逐文件结果位于deletion-results.json，错误列表为空；它只记录文件操作，不评价游戏。保留本轮目录、full-process-3600s-r3及两个关联宿主目录。报告、存档save、源码与素材不在本次删除清单。Shell删除被自动审批拒绝后，用户再次明确批准，使用严格清单校验的本地文件操作完成，不执行递归目录删除。

此前尝试NTFS压缩旧scratch文本和部分历史轨迹；首次因磁盘满失败，腾出缓存后成功处理部分文件，用户要求先等待时停止compact进程。未改变文件解压后的内容。逻辑文件大小与物理占用分别记录，不能把二者混作同一个释放量。

## 存储修订

新的原始DV轨迹使用dv-raw-dag-v2（.dvdag），恢复值文件使用.dvsave；逐条输入、可见信息、计时、画面采样索引与分析输出使用DVLOG2（.dvlog）。类型化二进制帧有长度与SHA-256，原始DAG另有序号/前帧链摘要，按节点引用共享不可变子树；精确有理数、同文字符串、负零、字段顺序及未知值保留。事件先提交后发布；完整checkpoint/final/seal及拒绝覆盖、保留.partial的边界不变。没有通过删字段或删推演原值减小证据。

JSON不再是运行记录格式；宿主内部JSON消息、编译配置与源码指纹仍是工程交换数据。run_debug只接受DAG，原rt_json_write落盘入口拒绝执行。旧JSON原始文件只读兼容；读取器导出完整值时同样写二进制，避免重新展开巨型JSON。Session的Save/Restore字符串仅为内存兼容，新的文件恢复为RestoreFile。

Godot的FrameArchive在后台把内存交换消息编码为二进制；PNG仍是原RGBA无损编码。Session写文件自己处理.partial和正式发布，宿主不重复改名。过程关闭回执也是二进制，失败不能称完整封存。工程文档与AGENTS同步更新。

## 工程事实与限制

- dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false完成，生成Session和宿主程序集。构建只说明工程可编译。
- 中性C#值文件检查：精确2/3与字符串2/3、负零、Unicode/控制字符、共享不可变子树均可还原；既有文件字节保持，JSON路径、损坏和截断文件被拒绝。
- Python校验C#输出，重新导出的二进制DAG字节一致；中性1000个共享引用的样本文件为26598字节。这是存储样本事实，不是游戏速度或体积收益测量。
- check_debug_structure.py的main=false生成/构建及中性事件、DAG、二进制stdout/读取结束，缺失真实阶段checkpoint被拒绝；未调用正式会话或恢复循环。记录见debug-neutral-r2/engineering-result.json。
- Godot的probe_frame_archive.gd只加载记录工作器，生成3x2中性RGBA。实际解码像素一致，后台记录先后保持，已存在PNG字节保留；重复目的文件失败明确报告。未加载Main或推进DV。
- 中性packet投影生成二进制索引、分析摘要/result及文本输出，没有JSON记录文件。编辑的Python经ast解析，Git差异空白审阅完成。
- 中性工程首次csproj相对路径错误已改正；第一次debug-neutral因新增源码路径集合混入str而中止，修正为Path后另建debug-neutral-r2，原目录不覆盖。

本轮没有运行新的一级、二级或三级正式会话，没有重跑Gemini的游戏。新格式的真实长会话性能、剧情、几何与UI行为仍未验证；不能用上述工程事实声称游戏改善。Luna仍暂停。

Gemini旧实机报告的独立核验见 ../../full-process-3600s-r3/independent-audit-20261019/REPORT.md：可确认五日时域到达，不能确认完整剧情通关；写盘失败、采样缺口、未投票/未签署与人物叠像分别有本轮原始来源。
