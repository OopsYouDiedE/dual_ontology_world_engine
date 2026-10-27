# 六幕录制布景 E

`RecordingStages.gd` 提供 `static build(kit: Node3D, id: String) -> Dictionary`。调用者预先创建 `kit.content_root` 并加入树，提供 E `LibraryE` / `PublicHallE` / `HallCraft` 的基础建模方法。返回 `size`、`camera_size`、`target`、静态人物与门墙范围；ID 使用当前 `Contracts/highlight_stages.json` 的六个稳定 ID。

六幕分别为铆接悬廊、玄武岩母碑、十九路临窗棋席、四锚祭坛、饺子工作台和粮仓服务梯。布景以当前 `Highlights.gd` 的构图及演员关系为依据；没有读取设计文档，没有改变对白、剧情或正式行动。

墙面使用朝内的单面 QuadMesh，不生成背面墙；没有门模型。悬廊和祭坛是开放布景。所有新增物件为真实三维几何、程序材质或现有 E 位图材质，不生成 SVG。围棋盘为十九条线，初始棋盘为空，落子仍应由录制动作驱动。

16 名学员直接调用公共 `_character` 模板，不改身体。顾平章使用 `archaeologist`，阮清月使用 `chessplayer`。代管者为局部独立 `DemonStageFigure`，不追加学生身份或修改 `CastE`；仅作为静态站位模型，厨房窗外虚影保留 `DemonWindowApparitionAnchor`，没有将其错误实体化到室内。

这批是静态布景模型，未接入旧二维 Highlights 的走位、碰撞、持物、坐姿、抽杖、落子、蒸汽、星尘或搬运演出。剑是独立道具，人物没有被伪装成正在完成录制动作。主代理统一进行真实渲染检查；模块完成不代表六幕动作录制已经完成。构件尺度沿 E 统一场景单位，未声称为正式逻辑地图坐标。

第一次统一截图逐张观察后，修正了悬廊斜撑缺横向连接梁、棋室木地面被方格分块、粮袋误读为木桶、服务梯苗床误用成熟盆栽的问题；工作台饺子改为扁长褶边轮廓，并增加厨房挂锅和备餐器皿。上述修正等待主代理下一轮统一渲染，未将源码修改当成画面验收完成。
