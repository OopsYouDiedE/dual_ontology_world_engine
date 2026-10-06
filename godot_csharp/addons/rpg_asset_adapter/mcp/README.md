# RPG TileMap MCP

stdio MCP入口：现行Claude初醒配方、像素检查与Aseprite交接、对白审查、制作任务图，以及旧TileMap制作工具。[制作与验收规范](../../../../docs/design-standard.md#第五部分32px-像素美术与地图工作流)。不依赖Core，不调用网络绘图服务。

## 安装和启动

仓库根目录执行：

```powershell
uv venv godot_csharp/addons/rpg_asset_adapter/mcp/.venv
uv pip install --python godot_csharp/addons/rpg_asset_adapter/mcp/.venv/Scripts/python.exe -r godot_csharp/addons/rpg_asset_adapter/mcp/requirements.txt
New-Item godot_csharp/addons/rpg_asset_adapter/mcp/.venv/.gdignore -ItemType File -Force
$env:GODOT_BIN = 'C:/Users/iii/Desktop/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64_console.exe'
godot_csharp/addons/rpg_asset_adapter/mcp/.venv/Scripts/python.exe godot_csharp/addons/rpg_asset_adapter/mcp/server.py
```

stdio面向MCP客户端，不是交互命令行。SDK最低版本来自实际使用的2.2 MCPServer API；.venv被git忽略，.gdignore防止Godot扫描第三方库。

本机已通过 `codex mcp add` 登记 `rpg-tilemap`，以实际用户配置读取确认 enabled=true。若迁移项目，需要更新该条目的绝对启动路径；安装方式依据[官方MCP配置说明](https://developers.openai.com/codex/mcp)。当前任务工具列表不会因此自动新增；新会话是否加载应以客户端实际工具发现为准，本次另用真实stdio客户端完成了发现与调用验证。

## 工具契约

现有客户端登记的服务仍叫 `rpg-tilemap`，无需另建MCP或插件。当前共37个工具。Python依赖为SDK、Pillow和现行配方需要的NumPy；升级既有虚拟环境时重跑上面的 `uv pip install ... -r ...`。新增工具需客户端重新发现/重开会话，本次已通过独立真实stdio客户端验证，不能视为本聊天工具面板已热加载。

### 现行初醒原生配方

- `native_art_catalog()`：材质色阶、五种连续地面材料、37个可调用物件配方、尺寸和源文件哈希。
- `read_native_recipe(module)`：pixel/palette/props/architecture/layout/placement/mind_ui中指定模块的真实源码。修改配方后重启MCP；常驻进程发现源变更会拒绝提供旧配方的新来源证明。
- `render_native_prop(name,recipe,parameters={},replace=false)`：例如 `recipe="bed", parameters={"room":0}`；按原配方原尺寸绘制，记录底部居中锚点，不推测碰撞。历史原稿与人工改动源不可覆盖。
- `render_native_material(name,material,crop,replace=false)`：material为oak/corridor/study/prep/hall，crop为原生像素[x,y,w,h]。从连续材料场裁切，不重采样，不保证该裁片可周期平铺。
- `rebuild_native_interior(mode="verify")`：完整调用单一builder。verify只比较现有运行时像素/layout并写报告；rebuild覆盖该builder拥有的PNG与清单。它不导入Godot、不进行实机验收。
- `native_art_contact_sheet()`：全部56个物件的离线最近邻审查图。
- `export_native_handoff(replace=false)`：58份静态Aseprite源、物件PNG/JSON图集及交接清单。R-05按底图/家具分层；单物件内部仍是一层。工程归FirstAwakeningInterior源族，图集归其运行时族。replace会覆盖交接源，必须先保留画师改动。Python重建入口不自动消费这些工程。

### 通用像素与工程交接

以下通用工具沿用TileMapWorkflow的源/运行时目录，不冒充FirstAwakening当前场景的自动替换。

- `inspect_pixel_style(path,palette=null,seam_axes=null)`：可见RGB数量、透明度、色板外RGB与横/纵边界差异。palette只接受#RRGGBB；alpha单独判定。边界差异不等于接缝失败。
- `quantize_asset(name,source,palette,target_size=null,replace=false)`：显式最近邻缩放和无抖色RGB量化，保留alpha与原文件。派生品不称原生像素绘制。默认不对Claude材质运行此步骤。
- `layered_sprite(name,width,height,layers,metadata=null,replace=false)`：layers按从底到顶给出`[{"name":"wood","path":"...png"}]`，每层同画布。生成独立RGBA cel的静态Aseprite文件和PNG；metadata可给anchor/collision，二者不互相推导。总像素预算16M。本次验证是规范结构检查，尚未进行Aseprite应用内打开验收。
- `export_aseprite(name,source,replace=false)`：读取AssetsSource内.ase/.aseprite，通过本机 `ASEPRITE_BIN` 或PATH里的Aseprite，以固定参数导出PNG与json-array图集，含layers/tags/slices。不接受任意脚本或额外命令，不缩放/裁边。缺少CLI会明确报错；本机未安装，CLI路径仅做mock契约检查。

### 对白和有边界的制作任务

- `review_dialogue(characters,turns,max_lines=3,max_chars_per_line=24,max_turns=200,pixel_width=null,font_size=null)`：角色卡为id/want/secret/voice/known_facts，turn为speaker/text及可选action/claims。逐turn换行预算可配置；claims只与明示known_facts比较，非自然语言事实推断。词汇提示不自动判文学好坏，像素宽度只估算。
- `create_plan(goal,tasks,replace_completed=false)`：tasks含id/goal/acceptance数组/depends_on数组和可选preparation。固定保存 `artifacts/tilemap-workflow/production-plan.json`；不会启动后台代理。只有旧计划完成且证据有效、明确传replace_completed时才可替换。
- `read_plan()`：重读计划并检查完成证据及验收定义。
- `update_task(task_id,status=null,acceptance_confirmed=null,evidence_paths=null,note=null)`：status为todo/in_progress/done，证据是仓库相对文件路径。done需要依赖有效完成、作者声明验收及存在且哈希一致的证据。文件检查不证明艺术或玩法；验收目标/依赖变化后须重新确认。
- `next_task()`：返回就绪任务与准备信息，以及证据失效的已完成项。调用代理继续已授权目标内的工作，遇到方向分歧再问用户；MCP本身不唤醒、不扩任务范围。

### Godot UI

正式剧情维护 `scenes/Awakening/Script/chapter_01.story`（含 `续篇 chapter2`）；本 MCP 的旧 `.dialogue` 剧情修订工具已退出，使用章节编译器及独立测试流程。
- `ui_design_contract()`：核实后的Godot样式规则、当前色板和尺寸边界。保留像素焦点，不用低可读性代替低饱和。
- `build_diegetic_ui(replace=false)`：固定FirstAwakeningInterior族，生成8张PNG切片及Aseprite源、共享Theme、`MindThemePreview.tscn`、带切线与内容边距的源清单。replace也拒绝覆盖未经登记或画师改过的文件。首次生成后用Godot编辑器导入资源；不会自行改玩法脚本或剧情。
- `inspect_godot_ui(viewport=[1280,992])`：实际Godot加载上述固定样板，允许960–2560×640–1440，审查解析后的样式、布局与Button六态。需要`GODOT_BIN`和已导入的资源；报告固定归`artifacts/tilemap-workflow/`。样板通过不证明动态游戏全界面自适应。
- `measure_dialogue_text(texts,width=1088,height=110,font_size=23,font_path=null)`：真实TextParagraph测量1–200段文本。默认Microsoft YaHei UI/Noto Sans CJK SC，font_path可指定ShareAsset内字体；报告每段实际行数、宽高是否容纳。系统字体依机器变化；不自动插换行、不改台词。

### 保留的TileMap工具

- tilemap_workflow()：制作顺序和尺寸契约。
- inspect_image(path,native_cell=32)：只读尺寸、透明度、哈希。
- draw_asset(name,width,height,palette,commands,replace=false)：点线矩形和配方。示例palette为{"base":"#35444b"}，commands为[{"op":"rect","xy":[0,0,31,31],"color":"base"}]，整数坐标包含终点。
- adapt_asset(name,source,crop,target_size,native_cell,mode,color_key)：crop是源像素[x,y,w,h]；native/tile/fit三种模式，显式色键可选。48px源crop=[0,0,48,48]到target_size=[32,32]时，native_cell=48且mode=tile，结果标记缩放派生。
- build_tiles(wall,floor,face,trim,face_lower)：前三个是32px不透明材质，trim为从外到内3–8个颜色，下墙面可选。
- audit_layout(rows,spawn,footprints,doors,interaction_points)：只读空间审查。footprints为{name,position:[像素x,y],collision:[相对x,y,宽,高]}，不要求纹理；doors为{name,rect:[格x,y,宽,深],facing:"north|south|east|west"}；interaction_points为{name,position:[像素x,y],radius:32}，表示玩家接近位置与容差。返回家具穿墙、占地交叠、门洞方向与净空、带家具的16×12脚底扫掠寻路结果。16px采样不能替代连续移动实测。
- compose_map(rows,spawn,props,doors,interaction_points)：spawn为地面格[x,y]；props含texture、像素position地面锚点、图片anchor、相对地面的collision=[x,y,w,h]（无阻挡[]）、ground地面装饰、overhead固定前景。普通家具按地面Y排序。写入前调用audit_layout，有空间错误则拒绝覆盖现有地图。doors与interaction_points契约同上；未声明方向的D格会警告。
- export_godot()：只运行固定导出器，输出res://scenes/TileMapWorkflow/Preview.tscn，不执行任意传入命令。

写入仅限规定本族路径；源读取允许绝对路径以接收新绘制图。派生重建会覆盖同族结果，原源同名冲突拒绝覆盖。

## 重建与验证

现行初醒与新增工具：

```powershell
# 真实stdio发现与调用：全量像素/layout比对，不改当前游戏纹理
godot_csharp/addons/rpg_asset_adapter/mcp/.venv/Scripts/python.exe godot_csharp/addons/rpg_asset_adapter/mcp/check_native_art.py
# 格式/量化/来源保护和任务图边界，全部使用临时目录
godot_csharp/addons/rpg_asset_adapter/mcp/.venv/Scripts/python.exe godot_csharp/addons/rpg_asset_adapter/mcp/check_pixel_handoff.py
godot_csharp/addons/rpg_asset_adapter/mcp/.venv/Scripts/python.exe godot_csharp/addons/rpg_asset_adapter/mcp/check_production.py
```

首次交接可给check_native_art.py加 `--handoff`；已有工程时不重复导出覆盖。验证报告为仓库 `artifacts/tilemap-workflow/native-mcp-check.json`。以下旧样板检查会重导出TileMapWorkflow，不能替代当前初醒验收：

```powershell
# 重建旧47形TileMap样板；不是当前初醒地图入口
godot_csharp/addons/rpg_asset_adapter/mcp/.venv/Scripts/python.exe godot_csharp/tools/rebuild_first_awakening_tilemap.py
# 真实stdio客户端：发现工具、读调用、非法输入、导出调用
godot_csharp/addons/rpg_asset_adapter/mcp/.venv/Scripts/python.exe godot_csharp/addons/rpg_asset_adapter/mcp/check.py
& $env:GODOT_BIN --headless --path godot_csharp --log-file artifacts/tilemap-workflow/runtime-check.log --script res://scenes/TileMapWorkflow/check.gd
& $env:GODOT_BIN --path godot_csharp res://scenes/TileMapWorkflow/Preview.tscn
```

`demo.py` 是旧样板配方，运行会覆盖 TileMapWorkflow 样板资产。当前初醒使用FirstAwakeningInterior/life_wing.png与layout.tres，重建入口为 `godot_csharp/tools/build_first_awakening_interior.py`，也可通过 `rebuild_native_interior` 调用同一builder。旧样板支持WASD/方向键移动；map-1x.png是离线布局图，不是实机截图。结果保存在仓库artifacts/tilemap-workflow；技术通过不等于艺术验收。

旧TileMap样板管线只导出建筑；当前初醒builder绘制建筑及物件，家具装配与实际碰撞由FirstAwakening场景维护。建筑报告不能代表家具已审查，实际家具碰撞须另传audit_layout并实机检查。严禁把床、柜子的整张透明画布当占地；门叶必须以墙体地面线为锚点，画面可以向上延伸，但不得让角色在本应属于门厚度的区域内站立。机关的剖面示意属于检视画面。

RPG Maker格式仍由父插件处理，ReasoningStudy原链路不变。这里补充新图适配与MCP制作入口。


