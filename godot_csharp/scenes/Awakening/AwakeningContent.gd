extends RefCounted
## 《门的这一侧》的证物卡与现场点位。检视文字只写看见了什么、从哪里看到；不写结论。

static var PLACES: Array

## 每件证物：名称（自然语言）、地点、来源、事实、现场试验、可请哪位同伴协助、可由谁触物回忆、局部图。
static var EVIDENCE: Dictionary

## 现场点位（地图坐标，脚底基准）。一个证物可有多个点位。
const SITES := {
	"E1": {"evidence": "E1", "point": Vector2(150, 372), "reach": 56.0, "title": "隔离门"},
	"E2": {"evidence": "E2", "point": Vector2(270, 436), "reach": 46.0, "title": "墙角的木楔与门底的灰"},
	"E3": {"evidence": "E3", "point": Vector2(162, 410), "reach": 40.0, "title": "灰上的鞋印"},
	"E4": {"evidence": "E4", "point": Vector2(154, 390), "reach": 40.0, "title": "门底的湿布"},
	"E5": {"evidence": "E5", "point": Vector2(186, 434), "reach": 42.0, "title": "地上的音叉"},
	"E6": {"evidence": "E6", "point": Vector2(652, 176), "reach": 52.0, "title": "书桌上的乐谱"},
	"E7": {"evidence": "E7", "point": Vector2(500, 242), "reach": 56.0, "title": "长桌上的石板"},
	"E8": {"evidence": "E8", "point": Vector2(500, 92), "reach": 56.0, "title": "铜口"},
	"E8j": {"evidence": "E8", "point": Vector2(164, 330), "reach": 40.0, "title": "墙上的管接头"},
	"E8r": {"evidence": "E8", "point": Vector2(592, 206), "reach": 52.0, "title": "墙上的管接头"},
	"E9": {"evidence": "E9", "point": Vector2(352, 524), "reach": 56.0, "title": "水槽里的杯子"},
	"E10": {"evidence": "E10", "point": Vector2(512, 502), "reach": 56.0, "title": "碗柜上的罐子"},
	"E11a": {"evidence": "E11", "point": Vector2(862, 96), "reach": 56.0, "title": "书架"},
	"E11b": {"evidence": "E11", "point": Vector2(862, 512), "reach": 56.0, "title": "书架"},
	"E11c": {"evidence": "E11", "point": Vector2(638, 512), "reach": 56.0, "title": "书架"},
	"E11d": {"evidence": "E11", "point": Vector2(638, 96), "reach": 56.0, "title": "书架"},
	"E12": {"evidence": "E12", "point": Vector2(1010, 244), "reach": 52.0, "title": "衣架上的风衣"},
}

static var PERSON_NAMES: Dictionary
static var ACTOR_NODES: Dictionary
static var ROOM_NAMES: Dictionary

## 重演面板的六个变量。选项顺序与 C# 枚举一致；初始值只有 V6 预填（今晨的共识）。
static var VARIABLES: Array

## 陈述：只来自玩家在重演中已设定的值（以及重演里侦探自己闩了门的结论）。
static var STATEMENTS: Dictionary

## 数据由 C# 章节内容提供；点位与 Vector2 仍属地图宿主。
static func apply_content(data: Dictionary) -> void:
	PLACES = data["PLACES"]
	EVIDENCE = data["EVIDENCE"]
	PERSON_NAMES = data["PERSON_NAMES"]
	ACTOR_NODES = data["ACTOR_NODES"]
	ROOM_NAMES = data["ROOM_NAMES"]
	VARIABLES = data["VARIABLES"]
	STATEMENTS = data["STATEMENTS"]
