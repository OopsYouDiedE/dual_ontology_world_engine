# 初醒室内物件生产源

使用场景：`scenes/FirstAwakening/FirstAwakening.gd`、`RoomDetails.gd`、`RoomDoor.gd` 与复用现场的 `ReplayStage.gd`。运行时只加载 `ShareAsset/Internal/Props/FirstAwakeningInterior/`。当前统一重建入口：`tools/build_first_awakening_interior.py`，实际生产代码位于 `tools/first_awakening_art/`。

`alarm_regulations.svg` 是 32×48 原生整数像素警铃与规程铜牌生产源。家具重建入口将其原样复制到 ShareAsset 同族（包括 `--scene-only`），并在 `manifest.json` 记录源路径、运行时路径、实际尺寸与 SHA-256；场景以左上角锚点、`centered=false` 装配在 `(536, 962)`。铜牌与侧光检查鞋印使用 `sites.tres` 的点位及 48 像素交互半径生成实体 Area；检视盖向下观察、记谱辨认及两份人物证言由对话取得，不生成额外实体 Area。

以下透明底图是 2026-09-26 的历史 image_gen 源稿与提示记录。它们保留用于来源追溯，不是当前室内物件和建筑的重建输入；当前运行时图由 `tools/first_awakening_art/` 的原生像素配方直接绘制。用户提供的 RPG 房间截图只用于构图、材质密度、俯视角和遮挡研究，没有作为图块切片，也未复制其角色与剧情。用户明确允许按既有素材协议进行改色和二次创作；本族没有覆盖 External/Victorian 原始包或其授权资料。

| 历史源稿 | 原始内容与当前关系 |
| --- | --- |
| `interior-sheet.png` | 4×4：单人床、衣柜、书桌、书柜、窗、研习桌、洗手柜、餐具柜、机械隔离门、铜口、立灯、前向椅、背向椅、床头灯柜、收纳箱、蕨类。原研习桌和隔离门单元保留，但被下方针对性修改稿取代。 |
| `detail-sheet.png` | 4×2：宿舍门、铸铁止挡座、机械钟、纸篓、研习纸与量具、缺口杯、四份口粮、门轴结构图。 |
| `desktop-sheet.png` | 4×2：缩短桌腿的研习桌、量具、私人笔记、谱纸、小盒、外衣、鞋、规则牌。宽桌略越过生成单元边界，重建脚本明确扩大该单元裁切，未截掉右腿。完整提示见 `desktop-prompt.txt`。 |
| `mechanical-door.png` | 移除烘入的内销与销座，保留门框、固定加强条、检视盖、滑轮绳索与配重；内销由场景独立绘制，以响应实际状态。完整提示见 `mechanical-door-prompt.txt`。 |
| `room-door.png` | 以旧宿舍门为编辑目标，将石砌边框换为连续深胡桃木框；覆盖detail-sheet中的旧宿舍门派生结果。为既有门扇分片固定适配52×74，非等比缩放，外侧门框由场景补到64px门洞宽度。 |

前两组提示摘要（不是逐字原始请求）：统一低饱和胡桃木、灰绿织物、黄铜；面向南的正交俯视 RPG 物件；各单元独立、透明底、不带地面、文字或人物。第一组为16种家具及门机构的4×4图集；第二组以上图为材质参考，生成8种生活/机械细节的4×2图集。后两组完整提示与编辑目标随源稿保存。上述图像和提示仅记录早期生成尝试，当前重建脚本不读取或裁切它们。

当前室内家具、门、道具与生活翼建筑由 `tools/first_awakening_art/` 中的 `pixel.py`、`palette.py`、`props.py`、`architecture.py`、`layout.py` 和 `placement.py` 原生像素配方按游戏像素尺寸绘制。`build_first_awakening_interior.py` 直接调用这些配方并写入运行时目录，不会读取历史 image_gen 图像，也不做下采样、色板量化或最近邻缩放。实际画布尺寸见运行时 `manifest.json`。这描述生产来源，不代表全部美术已通过艺术验收。

床分成后层床体和前层被褥以容纳角色；地毯、铺地、墙面和接触阴影由场景/地图配方在整数像素坐标绘制，不烘入物件。灯光是运行时局部光，不代表室外时间。门的开闭、内销与回放状态分别由游戏层和 Core 结果驱动。

`mind_seal.png` 是推理页专用的 24×24 原生像素印记，逐点绘制配方见 `tools/first_awakening_art/mind_ui.py`；由该模块对应入口更新本目录源图及 ShareAsset 中的同名运行时图，不经过 `build_first_awakening_interior.py` 的家具生产链。

`mind-ui-*.png`、同名`.aseprite`及`mind-ui.json`是推演页8种原生九宫格切片。唯一生成入口为既有MCP的`build_diegetic_ui`（实现`addons/rpg_asset_adapter/mcp/ui_workflow.py`），与家具和印记各司其职。清单记录原生尺寸、切线、内容边距、色板与文件哈希；Aseprite每件一层，未在Aseprite应用内验收。画师修改后重建会拒绝覆盖，需保留修改并明确选择后续生产源，不能伪造清单绕过保护。

宿舍木门编辑使用内置image_gen，完整提示：

> Use case: precise-object-edit. Edit target is the supplied closed RPG room door sprite. Replace ALL grey masonry blocks of the frame with a continuous aged dark walnut WOOD door casing and wooden lintel, no stone or brick anywhere. Preserve the existing straight frontal orthographic silhouette and proportions, narrow central closed wooden panel door and small brass handle on right. Muted Victorian interior, dark brown grain and restrained bevels. Same muted palette as the central existing leaf. Outer frame is about 10/52 of the width on EACH side, lintel 12/74 of height, threshold 3/74, central leaf 32/52 width. Continuous straight vertical wooden jambs; no perspective sidewalls or floor patch. No writing, number plaque, labels or added ornament. Transparent background outside the sprite. Crisp pixel-art-inspired texture, clean readable at final 52x74 pixel size. Entire frame visible, small transparent margin.
