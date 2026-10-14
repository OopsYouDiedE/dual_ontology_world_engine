# R18 最终补充独立静态审查

日期：2026-10-15。只读当前 dv_level3_capture.gd 与 GameEngine.cs 实际差异及相关采集上下文，核对渲染诊断在 tools/scenes 中的调用和消费引用。未读设计、私有会话或草稿，未修改生产源码、构建或运行会话；原最终审查保留。

## 诊断边界

新增 `_capture_render_state` 仅由原 `_capture` 在截图、帧索引及 measurement 输出之后调用。它读取现有 field.actors、Node2D 位置/透明度/可见性及现有 AnimatedSprite2D 的动画名、帧与纹理路径；未发现设置属性、加载新资源、调用 play、修改输入、分派动作或写入 DV 世界的语句。

未新增时速或调度设置，原 Time.get_ticks_msec 与至少200ms的采样判断、last_capture 更新、elapsed/filename 计算均保留。诊断复用对应截图的 real_ms 和 file，表示关联该次截图，不是独立测量其自身执行时刻。额外遍历/JSON/磁盘写入会增加实际开销，可能扩大采样间隙；需在新基线和实际采样时间中保留，不宣称稳定连续实采5Hz或零开销。

数据只写入 render-state.jsonl，并标记 private-passive-render-diagnostic。相关 tools/scenes 源码中未发现该文件被读取后传入游戏、可见记录或主体决策的路径。文件包含不可见人物的位置等信息，仍须由主代理维持私有证据隔离，不向盲玩者提供目录内容或诊断结果。

## 空节点与记录限制

host/field 使用 is_instance_valid；无效或非 Node2D actor 跳过，失效 child 跳过。SpriteFrames 非空且具有当前动画、当前帧索引有效才取得纹理；空纹理写空路径。诊断文件打开失败直接返回，不阻塞后续采样逻辑。

纹理已存在但没有 resource_path（例如内存子资源）也可能记录空字符串，不能仅凭空路径认定人物没有纹理；仍须结合动画、可见性及实际 PNG。跳过的无效 actor 不产生该 actor 行，也应按缺记录解释，不能推定其世界状态。

本次源码核对未发现空节点导致的新增必然错误；实际 GDScript 加载、运行及人物消失原因尚未由本审查验证。

## FileAccess 限定

GameEngine 的 FileStream 访问枚举明确为 System.IO.FileAccess.Write，与原 FileStream 用途一致，只消除 Godot.FileAccess 同名歧义。关闭阶段、错误记录、信号、DV 动作和时钟逻辑没有因该限定改变。

## 结论

本次补充差异符合被动私有诊断范围，未发现修改输入、动作、动画、游戏计时规则或向盲测主体提供诊断的源码路径。尚需主代理的独立工程检查及新会话实际截图/时间证据；本结论不解释既有人物消失原因，也不代表画面、判答或跨门验收。
