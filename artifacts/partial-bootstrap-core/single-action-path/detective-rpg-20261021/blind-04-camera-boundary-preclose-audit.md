# blind-04在途可见截图与镜头/开放边界静态审读

只读candidate-06源码、场景以及blind-04已生成PNG，不读在途DAG、host-events、隐藏状态、回执或输入记录；未改根或06、未启动任何会话，也未向盲玩者提供操作建议。正式receipt结论留待关闭。

## 可见证据

实际查看frame-000119935.png、180098.png、230062.png、269956.png、382688.png、383696.png。119935时走廊内人物完整入镜；230062时HUD为“乙区·宿舍走廊”，角色的确只在屏幕最左保留一部分，左侧目的区域不能清楚辨认。该裁切是已见呈现事实。269956为案卷，不用于身体定位；382688/383696时画面已在苏泠卧室R08且人物可见，因此不能从先前窗口推断整局永久无法离开所有地点。

图像不能单独确定真实脚点、某个Prepare是否pending、候选资格或未移动原因；不从PNG伪造这些记录。

## 相机不是按旧snapshot房间取范围

candidate-06 WorldView._physics_process:548将camera.position设为adapter.camera_point(player.position)。CampusMapView.stage_bounds:421-428用真实脚点所在sight_region取缓存；camera_point:473-482用该范围约束焦点。WorldView.RoomStage._process:1343-1359同样以adapter.room_of(player.position)切换_bounds和缩放。以上均未用snapshot.execution_room来锁旧房间。因此源码不支持“身体已经跨过去但相机仍按旧DV snapshot房间”的特定解释。

若真实脚点跨入新region，镜头/RoomStage/视野会按实际几何区域更新；平滑可能有短暂过渡，但该机制和旧逻辑房间缓存不同。_control_room对连续请求阶段的逻辑门禁并不驱动camera范围。

## 已确定的镜头边界裁切风险

按同一stage_bounds区域划分静态复算，zone_yi:corridor范围为[x2048,y992]..[x2752,y1152]，宽704高160。1280×992视口下，该房间期望缩放因最小值被夹为2，水平可视宽640。camera_point最左焦点为2048+320=2368，屏幕最左世界坐标正好2048。

所以角色脚点接近走廊左逻辑边界x2048时，镜头不会再向左；角色身体/透明精灵有横向面积，脚点即使尚未跨房，部分身体也可能被屏幕裁掉。该风险不要求物理越界或旧snapshot。230062截图与此空间关系一致，但未读取实际位置，不能声称实测脚点恰为2048。

后续呈现修补可考虑镜头留出身体边距或在真实边界接近时共同构图；不能通过扩大碰撞/传送身体或提前给予目标房观察解决。当前仅报告，不改实现。

## 比家具更具体的连续过门缺口候选：开放地板边界无D格

相邻场景格如下，均来自实际terrain_rows与_region_in_map：

- (2032,1072)、(2032,1104)为普通`.`地板，归zone_yi:hall；
- (2064,1072)、(2064,1104)也是普通`.`地板，归zone_yi:corridor；
- 两房分界x2048位于开放走廊，没有D门道格。

candidate-06 _step:1812-1831遇next_room改变且尚未prepared便阻止本地跨界，并尝试_request_continuous。_request_continuous:1602-1616却要求当前脚点的doorway_rooms包含源/目标两房；CampusMapView.doorway_rooms仅在当前terrain_mark为D时才返回邻房，普通`.`立即返回空。_prepare_ahead对远处只做几何预备、不提交正式动作；_door_option与近门E也依D组件。

因此即使corridor→hall当下有合法选项，开放地板边界也可能陷入“不能先跨界、但也没有D可发起”的门禁循环。该条件属于交互/几何识别合同不覆盖开放边界，不是必须由家具碰撞解释，也不等于Prepare已进入pending。当前没有在途记录证明该次恰走这条分界，故保持机制缺口候选；关闭后需配对真实脚点、_continuous.phase、continuous_gate_wait、select/Prepare/Complete与当前options。

## 关闭后的核对与最小方向

先确认截图时身体仍在源侧还是已越过，以及是否存在正式request；再区分原墙/家具阻挡、真实continuous等待与无D边界未发请求。若确认开放边界缺口，应由既有明确相邻房/准入move及实际可走地板边界建立通道识别，沿原Prepare/碰撞/Complete执行，不将所有`.`任意跨房、不改剧情位置或预写到达。不应把新几何识别冒充06已验证行为。

本报告没有完整source receipt、真实连续坐标或关闭后独立审查，不能宣称该原因已在正式轨迹中最终坐实。
