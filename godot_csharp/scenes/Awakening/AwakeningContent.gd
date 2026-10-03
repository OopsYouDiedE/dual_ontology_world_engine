extends RefCounted
## 《门的这一侧》的证物卡与现场点位。检视文字只写看见了什么、从哪里看到；不写结论。

const PLACES := ["门厅", "活动厅", "备餐间", "R-05", "R-08", "四间宿舍"]

## 每件证物：名称（自然语言）、地点、来源、事实、现场试验、可请哪位同伴协助、可由谁触物回忆、局部图。
const EVIDENCE := {
	"E1": {
		"name": "闩上的隔离门", "place": "门厅", "source": "门厅 · 隔离门内侧",
		"facts": "门内侧的插闩落在闩槽里。\n门外侧另有一道横闩，从这一侧拉不动。\n门上的猫眼，玻璃外侧糊满烟灰，看出去一片黑。",
		"experiment": "拔闩推门",
		"experiment_result": "内闩拔得出来。门推不动——外侧的横闩卡着。\n你把内闩插回原处。",
		"recall": "detective", "picture": "door",
	},
	"E2": {
		"name": "门底的灰与木楔", "place": "门厅", "source": "门厅地面 · 门内与左侧墙角",
		"facts": "门内地面铺着一片扇形的灰，窄端贴着门缝，向门厅里散开。\n墙角躺着一枚木楔：上面一层灰，底面干净。楔尖有新的磨痕。\n门底边有一道擦痕。",
		"experiment": "比对木楔与灰扇",
		"experiment_result": "楔尖的新磨痕，对得上门底那道擦痕；楔背的厚度，与灰扇窄端的宽度一样。\n两者吻合的，是一道窄缝。",
		"picture": "wedge",
	},
	"E3": {
		"name": "灰上的靴印", "place": "门厅", "source": "门厅地面 · 插闩下方",
		"facts": "灰上有一枚马球靴印，踩得很清楚。\n鞋尖朝着门侧的插闩。\n鞋跟一端被什么东西压过。",
		"experiment": "检视自己的靴底",
		"experiment_result": "你抬起脚。\n马球靴的鞋底纹路，与灰上的印一模一样。纹槽里嵌着同样的灰。",
		"picture": "print",
	},
	"E4": {
		"name": "门底的湿布", "place": "门厅", "source": "门厅 · 门底",
		"facts": "一条拧过的湿抹布，沿门底压成一条直线，正好贴着闭合的门。\n布的一端压住了靴印的鞋跟边缘。",
		"help": "shen", "picture": "cloth",
	},
	"E5": {
		"name": "门框旁的音叉", "place": "门厅", "source": "门厅 · 门框右侧地面",
		"facts": "地上一把纯银音叉，柄上刻着“S.L.”。\n门框齐肩高的地方，有一个很小的凹痕。",
		"help": "su", "recall": "su", "picture": "fork",
	},
	"E6": {
		"name": "苏泠的乐谱", "place": "R-08", "source": "R-08 · 书桌",
		"facts": "一页手写乐谱，只有一小节。\n前两拍是弱音，各记着一个问号；后两拍重读，字是“开门”。同样的一小节记了两遍。\n旁注：“门外？”\n下方换了一种墨迹：“他们关了门。”",
		"help": "su", "picture": "score",
	},
	"E7": {
		"name": "霍铎的石板", "place": "活动厅", "source": "活动厅 · 长桌东端",
		"facts": "一块小石板，粉笔速记两行。\n第一行：G：⊥门 ✕开 ◎\n第二行：一个小小的放大镜图样，后面是“⊥✓”。",
		"help": "huo", "recall": "huo", "picture": "slate",
	},
	"E8": {
		"name": "铜口与管接头", "place": "活动厅", "source": "活动厅北墙铜口 · R-08 外墙 · 隔离门左侧门框",
		"facts": "铜口嵌在活动厅北墙上，下方铜牌刻着一个符号：◎。\n一根铜管从铜口后面出来，沿走廊的墙走，经过 R-08 的墙后，再绕到门厅，贴着墙通向隔离门。\nR-08 外墙（走廊东端）与门框左侧各有一个管接头，一碰就晃——接口松了。",
		"experiment": "声路试验", "picture": "tube",
	},
	"E9": {
		"name": "四只杯子", "place": "备餐间", "source": "备餐间 · 水槽",
		"facts": "水槽里四只用过的杯子，各不相同：\n一只青瓷小杯，杯底没有糖渍；\n一只刻着 H 的凹痕锡杯，杯底一层厚糖渍；\n一只缺口白杯；\n一只粗瓷大杯。",
		"help": "shen", "picture": "cups",
	},
	"E10": {
		"name": "酵种罐", "place": "备餐间", "source": "备餐间 · 架上",
		"facts": "罐里的酵种还活着，冒着细泡。\n罐上的标签刻着二十四道短线。最后一道刻歪了。",
		"help": "shen", "recall": "shen", "picture": "jar",
	},
	"E11": {
		"name": "架上的无尘方框", "place": "四间宿舍", "source": "R-05 至 R-08 · 书架",
		"facts": "四间宿舍的书架都积着灰。灰上留着几块干净的长方形，像相框和书立过很久的位置。\nR-05 的架沿上有半枚手印。",
		"experiment": "请同伴比手", "picture": "shelf",
	},
	"E12": {
		"name": "侦探的外衣口袋", "place": "R-05", "source": "R-05 · 衣架",
		"facts": "衣架上挂着你的深棕长风衣。\n口袋里：一支石墨笔，一把折叠放大镜，几只证物袋。\n放大镜是黄铜圆框，带一截短柄。",
		"picture": "coat",
	},
}

## 现场点位（地图坐标，脚底基准）。一个证物可有多个点位。
const SITES := {
	"E1": {"evidence": "E1", "point": Vector2(644, 994), "reach": 70.0, "title": "隔离门"},
	"E2": {"evidence": "E2", "point": Vector2(530, 950), "reach": 46.0, "title": "墙角的木楔与门底的灰"},
	"E3": {"evidence": "E3", "point": Vector2(664, 952), "reach": 40.0, "title": "灰上的鞋印"},
	"E4": {"evidence": "E4", "point": Vector2(616, 958), "reach": 40.0, "title": "门底的湿布"},
	"E5": {"evidence": "E5", "point": Vector2(714, 950), "reach": 42.0, "title": "地上的音叉"},
	"E6": {"evidence": "E6", "point": Vector2(1024, 166), "reach": 52.0, "title": "书桌上的乐谱"},
	"E7": {"evidence": "E7", "point": Vector2(372, 652), "reach": 56.0, "title": "长桌上的石板"},
	"E8": {"evidence": "E8", "point": Vector2(498, 534), "reach": 56.0, "title": "铜口"},
	"E8j": {"evidence": "E8", "point": Vector2(574, 950), "reach": 40.0, "title": "墙上的管接头"},
	"E8r": {"evidence": "E8", "point": Vector2(1212, 410), "reach": 52.0, "title": "墙上的管接头"},
	"E9": {"evidence": "E9", "point": Vector2(862, 552), "reach": 56.0, "title": "水槽里的杯子"},
	"E10": {"evidence": "E10", "point": Vector2(1134, 534), "reach": 56.0, "title": "架上的罐子"},
	"E11a": {"evidence": "E11", "point": Vector2(58, 96), "reach": 56.0, "title": "书架"},
	"E11b": {"evidence": "E11", "point": Vector2(378, 96), "reach": 56.0, "title": "书架"},
	"E11c": {"evidence": "E11", "point": Vector2(698, 96), "reach": 56.0, "title": "书架"},
	"E11d": {"evidence": "E11", "point": Vector2(1018, 96), "reach": 56.0, "title": "书架"},
	"E12": {"evidence": "E12", "point": Vector2(268, 276), "reach": 52.0, "title": "衣架上的风衣"},
}

const PERSON_NAMES := {"detective": "侦探", "huo": "霍铎", "shen": "沈鼎", "su": "苏泠"}
const ACTOR_NODES := {"detective": "侦探", "huo": "工程师", "shen": "厨师", "su": "音乐家"}
const ROOM_NAMES := {"R05": "R-05", "R06": "R-06", "R07": "R-07", "R08": "R-08", "corridor": "走廊", "hall": "活动厅", "pantry": "备餐间", "foyer": "门厅", "main": "主楼走廊"}

## 重演面板的六个变量。选项顺序与 C# 枚举一致；初始值只有 V6 预填（今晨的共识）。
const VARIABLES := [
	{"key": "V1", "question": "警铃响时，苏泠在哪", "options": ["她的房间（R-08）", "活动厅", "备餐间"], "evidence": ["E5", "E6"]},
	{"key": "V2", "question": "警铃响时，霍铎在哪", "options": ["活动厅铜口旁", "他的房间", "门厅"], "evidence": ["E7"]},
	{"key": "V3", "question": "那人是从哪里喊话的", "options": ["从主楼对着铜口说", "就在门外走廊"], "evidence": ["E8"]},
	{"key": "V4", "question": "那人说的是什么", "options": ["“闩门，别开”", "“开门，救我”"], "evidence": ["E6", "E7"]},
	{"key": "V5", "question": "侦探更信谁", "options": ["霍铎", "苏泠", "谁都不全信，自己去看"], "evidence": ["E3", "E7"]},
	{"key": "V6", "question": "昨夜之前，四人彼此", "options": ["素不相识", "早已相熟"], "evidence": ["E9", "E10", "E11"], "preset": 0, "preset_note": "今晨大家都这么以为"},
]

## 陈述：只来自玩家在重演中已设定的值（以及重演里侦探自己闩了门的结论）。
const STATEMENTS := {
	"me": "是我闩的门。",
	"V1_0": "苏泠当时在她房里。", "V1_1": "苏泠当时在活动厅。", "V1_2": "苏泠当时在备餐间。",
	"V2_0": "霍铎当时守在铜口旁。", "V2_1": "霍铎当时在他房里。", "V2_2": "霍铎当时在门厅。",
	"V3_0": "门外没有人。那声音是从主楼对着铜口说的，从管接头漏到了门边。", "V3_1": "门外走廊里确实有人在喊。",
	"V4_0": "他说的是：闩门，别开。", "V4_1": "他说的是：开门，救我。",
	"V5_0": "我信霍铎。", "V5_1": "我更信苏泠。", "V5_2": "我谁都不全信，自己去看了。",
	"trust": "我信霍铎——我们早已相熟。",
	"V6_0": "我们昨天才认识。", "V6_1": "我们早已相熟。",
}
