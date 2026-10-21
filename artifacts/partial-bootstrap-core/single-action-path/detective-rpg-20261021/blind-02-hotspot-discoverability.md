# blind-02 石板热点可发现性补充

只读候选源码、本轮end/host-events及已查看截图；不改源、不运行，等待integration_agent整合WorldView。

## 文案为什么变成“长桌”

候选 `language_core/60-game.dv:297` 的 dvg_site_titles 明确将E7.title设为“活动厅长桌”；WorldView._build_sites:267用该title覆盖几何title。正式已准入action.label虽为“检视长桌上的石板”，HUD/hover仍读sites.title，没有使用label。因此玩家得到泛化家具名称，未被引向长桌东端的具体石板。Table精灵未映射E7，只有Slate→E7，所以桌中段显示通用“长桌”更不代表该位置已经是石板交互点。

最小文案修补可在内容投影把E7.title改为已交付的“长桌上的石板”；或在宿主提示中，site拥有唯一当前available option时直接使用该已交付option.label。多动作仍保留具体site名并待玩家近距选择，不以隐藏动作标签推断内容。无需改变world、碰撞、站点、reach或观察。

## 火花为什么没有出现

本轮end snapshot.sites的全部条目仅含title，没有new。WorldView._build_sites:273对缺new默认false。HintOverlay只为new=true画火花，因此本轮没有新热点火花来源；先前的视野/低动态修补只过滤已有标记，不能让不存在的new出现。

若要提升可发现性，建议新增“当前可交互”静态标记：只能依现有site_visible、spatial.can_submit、choices_for中available选项，沿已显示物件sprite/marker显示。该标记含义不是“新证据”“尚未读过”或“已经取得”，不得用隐藏world状态推断new。低动态同样静态；模态/对白期间按现有active门禁隐藏。主代理核对是否采用此呈现调整。

## 命中矩形与实际石板

E7.point=(1968,962)，site_box为x1952..1984、y936..966（32×30）；_process悬停检测grow(3)后为x1949..1987、y933..969。Slate.position=(1968,1000)、offset=(-10,-44)，20×14不透明PNG实际可见rect为x1958..1978、y956..970。原box覆盖实际石板10/14行；grow后覆盖13/14行。命中中心比实际石板中心偏上18px，整体并非完全错位，但矩形大部分面积落在物件上方，最下缘仍有很窄漏区。

最小几何呈现修补仅调整点击/悬停框来源：有明确sprite_sites绑定时，使用该可见Sprite2D的真实get_rect变换作为点击框，且先复核site_visible；保留逻辑point/reach/站点和真实接近完全不变。不能把整个长桌碰撞区域当作已接触石板。生产Slate→E7映射已经存在，无需按剧情ID解锁纹理。

## 家具fallback吞掉精确物件

CampusMapView.furniture_hits按Objects原场景子节点顺序收集命中Sprite2D。WorldView._process在没有site_box命中时按该顺序处理第一项后立即break；StudyTable在Slate之前。两者重叠时、尤其点击物件最下窄缘，先得到StudyTable：未映射site、显示“长桌”、不置hovered。后续Slate就不会被考虑；鼠标click走_navigate，桌内walkable=false，因而没有接近或反馈。

建议fallback采用两阶段：先在实际可见命中物中选已有sprite_sites绑定且site_visible、具当前eligible选项的精确物件；若没有，再显示原普通家具外观名。不要扫描几何制造资格，不把普通家具文案变成行动。该变更只修选择优先级，仍由_approach在原reach内找合法点。

## 无人物候选时E反馈

当前_interact本来有UI提示“此处暂时没有可进行的调查或交谈”，但E只从_pick_nearest得到有eligible选项的最近site；没有人选项时nearest为空，_near_door也空，就直接结束，已有提示路径未触发。

最小修补是E分支在nearest与唯一door都为空时，再只查实际可见、处于原reach内的已有site/person，发现接近物后发interaction_hint“此处暂时没有可执行的交互”；不提交select、不改options、不消耗游戏行动。没有近物件时可同样给通用UI反馈或保持原操作提示，由主代理决定。不能自动开放苏泠/霍铎对话，也不声称内容条件已满足。

上述是静态具体修补建议，尚未实施或验收。
