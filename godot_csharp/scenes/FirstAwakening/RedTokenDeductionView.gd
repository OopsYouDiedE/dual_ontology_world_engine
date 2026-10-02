extends Control
## Unified Deduction UI for "The Unreturned Red Token" (未归的红筹 · 脑内推演)
## Provides both Event Sequence Deduction (事件时序) and Motivation & Rules Deduction (规则与动机).
## Strictly NO emojis. Matter-of-fact, restrained forensic tone.
## Full trilingual support (zh_CN, en, ja).

signal return_requested
signal deduction_evaluated(result: Dictionary)

const FONT_WRITING_ZH = preload("res://ShareAsset/External/Yozai/Yozai-Regular.ttf")
const FONT_WRITING_JA = preload("res://ShareAsset/External/ZenKurenaido/ZenKurenaido-Regular.ttf")
const FONT_WRITING_EN = preload("res://ShareAsset/External/playwriteustrad/PlaywriteUSTrad[wght].ttf")
const FONT_PRINT_ZH = preload("res://ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf")
const FONT_PRINT_JA = preload("res://ShareAsset/External/newtegomin/NewTegomin-Regular.ttf")

const COLOR_INK := Color("241b12")
const COLOR_MUTED := Color("5a5348")
const COLOR_COMPATIBLE := Color("205026")
const COLOR_CONFLICT := Color("85261e")
const COLOR_UNPROVEN := Color("666056")
const COLOR_CARD_BG := Color(0.92, 0.88, 0.82, 0.6)
const COLOR_CARD_BORDER := Color(0.35, 0.30, 0.25, 0.22)
const COLOR_BTN_BG := Color(0.24, 0.20, 0.16, 0.03)
const COLOR_BTN_BORDER := Color(0.24, 0.20, 0.16, 0.28)
const COLOR_BTN_SELECTED_BG := Color("344c39")
const COLOR_BTN_SELECTED_BORDER := Color("213c27")
const COLOR_BTN_HOVER_BG := Color(0.24, 0.20, 0.16, 0.08)

var demo: Node # Reference to RedTokenDemo
var current_mode := "sequence" # "sequence" or "motivation"
var selected_actor := "engineer" # "engineer", "chef", "musician", "taker"

# UI Containers
var lang_buttons: Dictionary = {}
var mode_tab_sequence: Button
var mode_tab_motivation: Button
var btn_return: Button
var right_scroll: ScrollContainer
var right_content: VBoxContainer
var concordance_container: GridContainer
var concordance_title: Label
var finding_label: RichTextLabel
var btn_details: Button
var btn_evaluate: Button
var btn_reset: Button
var preset_label: Label
var btn_preset_h1: Button
var btn_preset_h2: Button
var candidate_summary: RichTextLabel
var candidate_missing: RichTextLabel
var candidate_semantics: RichTextLabel
var candidate_box: VBoxContainer

# Evidence keys to display in Concordance list
const EVIDENCE_KEYS: Array[String] = [
	"E05", "E06", "E07", "E08",
	"E09", "E09.deep", "E09.completion", "E10",
	"E11", "E12", "E13", "E14",
	"E15", "E16"
]

const I18N := {
	"TITLE": {
		"zh_CN": "未归的红筹 · 脑内推演",
		"en": "The Unreturned Red Token · Deduction Log",
		"ja": "帰らぬ赤の賭け金 · 推理手帳"
	},
	"RETURN": {
		"zh_CN": "返回现场",
		"en": "Return to Scene",
		"ja": "現場に戻る"
	},
	"TAB_SEQUENCE": {
		"zh_CN": "事件时序推演",
		"en": "Event Sequence",
		"ja": "出来事の時系列"
	},
	"TAB_MOTIVATION": {
		"zh_CN": "规则与动机推演",
		"en": "Rules & Motivations",
		"ja": "規則と動機の推演"
	},
	"EVALUATE": {
		"zh_CN": "执行推演 · 重演",
		"en": "Replay & Deduce",
		"ja": "推演実行・再現"
	},
	"RESET": {
		"zh_CN": "重置假说",
		"en": "Reset",
		"ja": "仮説リセット"
	},
	"PRESET_HEADER": {
		"zh_CN": "快速对照：",
		"en": "Presets:",
		"ja": "候補照合："
	},
	"PRESET_H1": {
		"zh_CN": "候选 H1：排灰未交筹",
		"en": "H1: Ash Unreturned",
		"ja": "H1：灰未返還"
	},
	"PRESET_H2": {
		"zh_CN": "候选 H2：清水误记后取",
		"en": "H2: Wash Misrecord",
		"ja": "H2：水誤記後取"
	},
	"CONCORDANCE_TITLE": {
		"zh_CN": "现场物证核对",
		"en": "Material Evidence Concordance",
		"ja": "現場物証の照合"
	},
	"STATUS_COMPATIBLE": {
		"zh_CN": "[推演相容]",
		"en": "[Replay matches]",
		"ja": "[推演適合]"
	},
	"STATUS_CONFLICT": {
		"zh_CN": "[冲突]",
		"en": "[Conflict]",
		"ja": "[矛盾]"
	},
	"STATUS_UNPROVEN": {
		"zh_CN": "[仍未知]",
		"en": "[Still unknown]",
		"ja": "[なお不明]"
	},
	"STATUS_UNINSPECTED": {"zh_CN": "[未检视]", "en": "[Not inspected]", "ja": "[未調査]"},
	"STATUS_UNSUBMITTED": {"zh_CN": "[已检视·未提交]", "en": "[Read · not submitted]", "ja": "[調査済・未提出]"},
	"INITIAL_FINDING": {
		"zh_CN": "尚未提交完整因果推演。请在右页选择事件顺序与岗位规程后执行推演。",
		"en": "Deduction not yet submitted. Configure event sequence and actor protocols on right page to execute.",
		"ja": "因果推演が未提出です。右ページで順序と規則を設定後に推演を実行してください。"
	},
	# Stage 1
	"STAGE_1_TITLE": {
		"zh_CN": "阶段一：门底木楔与合门",
		"en": "Stage 1: Door Wedge & Closure",
		"ja": "段階一：扉の木楔と閉扉"
	},
	"FIELD_WEDGE": {
		"zh_CN": "木楔撤除与包纸时序",
		"en": "Wedge Removal & Wrapping",
		"ja": "木楔の撤去と包装時期"
	},
	"OPT_DIRTY_THEN_WRAP": {
		"zh_CN": "沾灰后包纸",
		"en": "Wrap ashed",
		"ja": "煤付着後包装"
	},
	"OPT_CLEAN_WRAP": {
		"zh_CN": "干净时包纸",
		"en": "Wrap clean",
		"ja": "清浄時包装"
	},
	"OPT_UNKNOWN": {
		"zh_CN": "未确定",
		"en": "Unspecified",
		"ja": "未確定"
	},
	# Stage 2
	"STAGE_2_TITLE": {
		"zh_CN": "阶段二：阀门分流与捕灰",
		"en": "Stage 2: Valve Divert & Filtration",
		"ja": "段階二：弁分流と灰捕集"
	},
	"FIELD_HOLDER": {
		"zh_CN": "持续持柄岗位",
		"en": "Valve Operating Post",
		"ja": "弁保持の担当岗位"
	},
	"OPT_HOLDER_ENG": {
		"zh_CN": "工程师 霍铎",
		"en": "Engineer Huo Duo",
		"ja": "技師 ホード"
	},
	"OPT_HOLDER_CHEF": {
		"zh_CN": "主厨 沈鼎",
		"en": "Chef Shen Ding",
		"ja": "主厨 シェン・ディン"
	},
	"FIELD_FLOW": {
		"zh_CN": "进水分流介质",
		"en": "Inlet Fluid Source",
		"ja": "流入流体の種別"
	},
	"OPT_FLOW_ASH": {
		"zh_CN": "排灰支路",
		"en": "Ash divert",
		"ja": "灰排出ライン"
	},
	"OPT_FLOW_WATER": {
		"zh_CN": "厨房清水",
		"en": "Fresh water",
		"ja": "厨房清水"
	},
	"FIELD_CLOTH": {
		"zh_CN": "滤布夹持时机",
		"en": "Cloth Clamping Timing",
		"ja": "濾過布の固定時期"
	},
	"OPT_CLOTH_BEFORE": {
		"zh_CN": "通流前夹滤",
		"en": "Before flow",
		"ja": "通水前固定"
	},
	"OPT_CLOTH_AFTER": {
		"zh_CN": "此处未安排先夹滤",
		"en": "No prior clamping in this stage",
		"ja": "この段階では事前固定なし"
	},
	"FIELD_WIPE": {
		"zh_CN": "地面擦拭时机",
		"en": "Floor Wiping Timing",
		"ja": "床面拭掃の時期"
	},
	"FIELD_RELEASE": {"zh_CN": "持柄终止时机", "en": "Handle release timing", "ja": "柄を離す時期"},
	"OPT_RELEASE_CONFIRMED": {"zh_CN": "收到具名转清确认后松柄", "en": "Release after named clear-water confirmation", "ja": "記名の清澄確認後に離す"},
	"OPT_RELEASE_BEFORE": {"zh_CN": "未收到确认便松柄", "en": "Release before confirmation", "ja": "確認前に離す"},
	"DETAILS": {"zh_CN": "查看差异与已读材料", "en": "Discrepancies & read evidence", "ja": "差異と既読の物証"},
	"CANDIDATE_NOTE": {"zh_CN": "候选说明 · 须执行重演核验", "en": "Candidate explanation · replay required", "ja": "候補の説明・再現検証が必要"},
	"OPT_WIPE_AFTER": {
		"zh_CN": "排灰后擦地",
		"en": "Wipe after",
		"ja": "排出後拭掃"
	},
	"OPT_WIPE_BEFORE": {
		"zh_CN": "排灰前擦地",
		"en": "Wipe before",
		"ja": "排出前拭掃"
	},
	# Stage 3
	"STAGE_3_TITLE": {
		"zh_CN": "阶段三：器具交接与红筹去向",
		"en": "Stage 3: Tool Return & Token",
		"ja": "段階三：器具引渡と賭け金"
	},
	"FIELD_RETURN": {
		"zh_CN": "红筹是否入归还匣",
		"en": "Red Token Deposited into Box",
		"ja": "賭け金の返還箱投入"
	},
	"OPT_RETURN_YES": {
		"zh_CN": "已单向交回",
		"en": "Deposited",
		"ja": "返還箱投入済"
	},
	"OPT_RETURN_NO": {
		"zh_CN": "未曾交回",
		"en": "Not deposited",
		"ja": "未返還のまま"
	},
	"FIELD_TAKE": {
		"zh_CN": "前抽屉取筹记录",
		"en": "Front Drawer Retrieval",
		"ja": "前抽斗の再取出記録"
	},
	"OPT_TAKE_AFTER": {
		"zh_CN": "交回后开屉再取",
		"en": "Taken after return",
		"ja": "返還後再取"
	},
	"OPT_TAKE_NEVER": {
		"zh_CN": "从未开屉取出",
		"en": "Never extracted",
		"ja": "取出なし"
	},
	"FIELD_SIGN": {
		"zh_CN": "四人返房时机与签条",
		"en": "Room Return & Sign Slips",
		"ja": "帰室と受領札の点検"
	},
	"OPT_SIGN_YES": {
		"zh_CN": "取白片签条后返房",
		"en": "Signed slips return",
		"ja": "白票札点検後"
	},
	"OPT_SIGN_NO": {
		"zh_CN": "未交接直接返房",
		"en": "Unsigned return",
		"ja": "引渡未了帰室"
	},
	"FIELD_TAKER": {
		"zh_CN": "后取者推定身份",
		"en": "Presumed Taker Identity",
		"ja": "後取者の推定身元"
	},
	"OPT_TAKER_REC": {
		"zh_CN": "接待者（移物单记录）",
		"en": "Receptionist (via docket)",
		"ja": "受付者（伝票記録）"
	},
	"OPT_TAKER_STU": {
		"zh_CN": "四人之一（无移物单）",
		"en": "One of the Four",
		"ja": "四人のうち一人"
	},
	# Motivation Actors
	"ACTOR_ENG": {
		"zh_CN": "工程师 霍铎",
		"en": "Engineer Huo Duo",
		"ja": "技師 ホード"
	},
	"ACTOR_CHEF": {
		"zh_CN": "主厨 沈鼎",
		"en": "Chef Shen Ding",
		"ja": "主厨 シェン・ディン"
	},
	"ACTOR_MUSIC": {
		"zh_CN": "音乐家 苏泠",
		"en": "Musician Su Ling",
		"ja": "音楽家 スー・リン"
	},
	"ACTOR_TAKER": {
		"zh_CN": "接待与后取者",
		"en": "Receptionist / Taker",
		"ja": "受付者と後取者"
	},
	# Motivation Sections
	"MOTIVATION_RULE_HEADER": {
		"zh_CN": "规程依据与操作规范",
		"en": "Operating Protocols & Directives",
		"ja": "規程根拠と操作規範"
	},
	"MOTIVATION_SENSE_HEADER": {
		"zh_CN": "感知通道与现场直觉",
		"en": "Sensory Channels & Perceptions",
		"ja": "知覚経路と現場直観"
	},
	"MOTIVATION_EVAL_HEADER": {
		"zh_CN": "动机一致性判定",
		"en": "Forensic Consistency Assessment",
		"ja": "動機整合性の判定"
	},
	# Engineer Motivation
	"FIELD_ENG_RULE": {
		"zh_CN": "持柄动作依据的规程",
		"en": "Valve Holding Directive Basis",
		"ja": "弁操作の根拠規程"
	},
	"OPT_ENG_DAILY": {
		"zh_CN": "日常复位条例",
		"en": "Daily Reset Rule",
		"ja": "日常復帰条令"
	},
	"OPT_ENG_EXCEPTION": {
		"zh_CN": "具名排灰特例 E11",
		"en": "Exception E11",
		"ja": "特例条令E11"
	},
	"DESC_ENG_DAILY": {
		"zh_CN": "松手弹簧回位停流；工作完毕必须将手柄复位归架。",
		"en": "Spring resets handle to stop flow upon release; must return to rack.",
		"ja": "離手でばね復帰・流体停止。作業完了後は架へ返却。"
	},
	"DESC_ENG_EXCEPTION": {
		"zh_CN": "持柄分流直至主厨具名转清确认；双联特例承诺方可执行。",
		"en": "Hold handle to divert flow until Chef's named clarity confirmation; requires docket agreement.",
		"ja": "主厨の清浄確認受領まで弁を保持。指令板裏の特例承諾が必要。"
	},
	"FIELD_ENG_PIPE": {
		"zh_CN": "台侧压力铜管感知",
		"en": "Bench Copper Pipe Acoustic Channel",
		"ja": "台側圧力銅管の知覚"
	},
	"OPT_ENG_PIPE_RING": {
		"zh_CN": "管壁受压持续振颤",
		"en": "Fluid pipe vibrates",
		"ja": "管壁の圧力振動持続"
	},
	"OPT_ENG_PIPE_QUIET": {
		"zh_CN": "铜管无振颤静止",
		"en": "Pipe silent and still",
		"ja": "銅管の振動なし"
	},
	# Chef Motivation
	"FIELD_CHEF_RULE": {
		"zh_CN": "出水转清与收班规程",
		"en": "Discharge Clarity & Cleanup Protocol",
		"ja": "出水清浄と業務終了規程"
	},
	"OPT_CHEF_VERIFY": {
		"zh_CN": "转清核验规程",
		"en": "Verify Clarity",
		"ja": "清浄確認規程"
	},
	"OPT_CHEF_MISTAKEN": {
		"zh_CN": "常规洗布作业",
		"en": "Routine Wash",
		"ja": "日常洗浄作業"
	},
	"DESC_CHEF_VERIFY": {
		"zh_CN": "盆侧直视出水浑转清，发出具名转清确认方可收班洗布。",
		"en": "Visually confirm water transition from turbid to clear before issuing sign-off and cleanup.",
		"ja": "水盆側の出水が濁りから清浄へ転じるのを目視し、確認合図後に業務終了。"
	},
	"DESC_CHEF_MISTAKEN": {
		"zh_CN": "清水洗布未核同号联，洗布浑水转清误记为排污完成。",
		"en": "Washed cloth with fresh water without verifying docket; wash water misrecorded as clear.",
		"ja": "同番号控え未確認のまま清水洗浄。洗布の濁水を清浄と誤記。"
	},
	"FIELD_CHEF_SENSE": {
		"zh_CN": "主厨夹滤岗位确认",
		"en": "Chef's filter-post confirmation",
		"ja": "主厨の濾過担当確認"
	},
	"OPT_CHEF_EYE_CLEAR": {
		"zh_CN": "先夹滤并具名确认到位",
		"en": "Clamp filter and confirm readiness",
		"ja": "事前固定と記名の準備確認"
	},
	"OPT_CHEF_EYE_NONE": {
		"zh_CN": "未确认夹滤岗位",
		"en": "No readiness confirmation",
		"ja": "固定準備の確認なし"
	},
	# Musician Motivation
	"FIELD_MUSIC_GAP": {
		"zh_CN": "门缝合拢后门侧声路",
		"en": "Door Gap Acoustic Channel",
		"ja": "閉扉後の扉側音路"
	},
	"OPT_MUSIC_GAP_QUIET": {
		"zh_CN": "闭门后空气声路静止",
		"en": "Silent after closure",
		"ja": "閉門後音路静粛"
	},
	"OPT_MUSIC_GAP_RING": {
		"zh_CN": "门侧漏音仍响",
		"en": "Sound leaks through",
		"ja": "隙間から異音持続"
	},
	# Taker Motivation
	"FIELD_TAKER_RULE": {
		"zh_CN": "归还匣与前抽屉 · 材料参照",
		"en": "Receiver & Front Drawer · Material Reference",
		"ja": "返還箱と前引き出し・資料参照"
	},
	"DESC_TAKER_BOX": {
		"zh_CN": "E13 已检视：红筹与手柄由各自槽口单向落入接收件；槽口不能反向取物。前抽屉没有锁，只有一次性铅封；现场所见铅封已断。这是机构与现状记录，不代替事件选择。",
		"en": "Inspected E13: token and handle fall inward through separate one-way slots. Objects cannot be pulled back through them. The front drawer has no lock, only a single-use lead seal, found broken. These are observed mechanisms and conditions, not an event choice.",
		"ja": "E13調査済：赤い賭け金と柄は別々の一方向の投入口から受領部へ落ち、投入口から逆に取り出せません。前引き出しに錠はなく、一度限りの鉛封だけがあり、現場では切れています。機構と現状の記録であり、出来事の選択ではありません。"
	},
	"DESC_TAKER_SEAL": {
		"zh_CN": "E15 已检视：椅上同一缺角红筹、断封、拉手擦痕与抽屉灰中半手印可相互比对。现筹与断封本身不指定取筹时机或身份，需结合其他已读材料核对。",
		"en": "Inspected E15: the chair token has the same chipped corner; the broken seal, handle scuff and half handprint in drawer dust can be compared. These observations alone do not assign a retrieval time or identity; compare other inspected materials.",
		"ja": "E15調査済：椅子の赤い賭け金は同じ角欠けを持ち、切れた封印、取っ手の擦れ、引き出しの灰の半分の手形を照合できます。これらだけでは取出しの時期や人物を指定できず、他の調査済資料との照合が必要です。"
	},
	"DESC_TAKER_BOX_UNREAD": {
		"zh_CN": "E13 未检视：请到研习台旁的归还匣查看原物。",
		"en": "E13 not inspected: examine the receiver beside the study bench.",
		"ja": "E13未調査：研習台のそばの返還箱で現物を確認してください。"
	},
	"DESC_TAKER_SEAL_UNREAD": {
		"zh_CN": "E15 未检视：请到值守椅与归还匣前查看原物。",
		"en": "E15 not inspected: examine the objects at the duty chair and receiver front.",
		"ja": "E15未調査：当番椅子と返還箱の前で現物を確認してください。"
	},
	"TAKER_CURRENT_EVENT": {
		"zh_CN": "当前候选 · 前抽屉取筹：%s\n请到“事件时序推演”编辑这一事件；阅读本页不会更改候选。",
		"en": "Current candidate · front-drawer retrieval: %s\nEdit this event in Event Sequence. Reading this page does not change the candidate.",
		"ja": "現在の候補・前引き出しからの取出し：%s\nこの出来事は「出来事の時系列」で編集してください。このページを読んでも候補は変わりません。"
	},
	# Evidence Names
	"EV_E05": {"zh_CN": "E05 案脚木纤维", "en": "E05 Desk Foot Fiber", "ja": "E05 机脚の木繊維"},
	"EV_E06": {"zh_CN": "E06 配重门灰带", "en": "E06 Door Ash Band", "ja": "E06 扉の灰帯"},
	"EV_E07": {"zh_CN": "E07 纸包转印灰", "en": "E07 Wrapped Ash Mark", "ja": "E07 包み紙の転写灰"},
	"EV_E08": {"zh_CN": "E08 布夹折边与地板纤维", "en": "E08 Cloth Clip & Fiber", "ja": "E08 布留めと床繊維"},
	"EV_E09": {"zh_CN": "E09 水盆留灰", "en": "E09 Basin Residue", "ja": "E09 水盆の残留灰"},
	"EV_E09.deep": {"zh_CN": "E09 捕集片新灰", "en": "E09 Collector Fresh Ash", "ja": "E09 捕集片の新規灰"},
	"EV_E09.completion": {"zh_CN": "E09 同号转清副联", "en": "E09 Clarity Stub", "ja": "E09 同番号清浄控え"},
	"EV_E10": {"zh_CN": "E10 器材架存根", "en": "E10 Rack Handle Stub", "ja": "E10 機材棚引取半券"},
	"EV_E11": {"zh_CN": "E11 指令板背特例", "en": "E11 Emergency Exception", "ja": "E11 指令板裏の特例"},
	"EV_E12": {"zh_CN": "E12 触物听觉感知", "en": "E12 Acoustic Perception", "ja": "E12 触物聴覚知覚"},
	"EV_E13": {"zh_CN": "E13 归还匣接收印", "en": "E13 Receiver Box Stamp", "ja": "E13 返還箱の受領印"},
	"EV_E14": {"zh_CN": "E14 白片与返房签条", "en": "E14 White Slip & Sign", "ja": "E14 白票と帰室札"},
	"EV_E15": {"zh_CN": "E15 椅上红筹断封", "en": "E15 Chair Token & Seal", "ja": "E15 椅子の賭け金と断封"},
	"EV_E16": {"zh_CN": "E16 质询移物单", "en": "E16 Transfer Docket", "ja": "E16 移管伝票"}
}

func tr_str(key: String) -> String:
	var loc := _get_locale()
	if I18N.has(key):
		var item: Dictionary = I18N[key]
		if item.has(loc): return item[loc]
		if item.has("zh_CN"): return item["zh_CN"]
	return key

func _get_locale() -> String:
	var l := TranslationServer.get_locale()
	if l.begins_with("ja"): return "ja"
	if l.begins_with("en"): return "en"
	return "zh_CN"

func _font_writing() -> Font:
	var loc := _get_locale()
	if loc == "ja": return FONT_WRITING_JA
	if loc == "en": return FONT_WRITING_EN
	return FONT_WRITING_ZH

func _font_print() -> Font:
	var loc := _get_locale()
	if loc == "ja": return FONT_PRINT_JA
	return FONT_PRINT_ZH

func _init() -> void:
	custom_minimum_size = Vector2(1280, 992)
	offset_right = 1280.0
	offset_bottom = 992.0
	mouse_filter = Control.MOUSE_FILTER_IGNORE

func setup(p_demo: Node) -> void:
	demo = p_demo
	_build_ui()
	refresh()

func _build_ui() -> void:
	for child in get_children():
		child.queue_free()

	# --- Left Page Concordance Container (offset_left=36, top=584, width=628, height=376) ---
	var left_box := VBoxContainer.new()
	left_box.position = Vector2(36, 608)
	left_box.size = Vector2(628, 352)
	left_box.add_theme_constant_override("separation", 6)
	add_child(left_box)

	concordance_title = Label.new()
	concordance_title.add_theme_font_override("font", _font_print())
	concordance_title.add_theme_font_size_override("font_size", 17)
	concordance_title.add_theme_color_override("font_color", COLOR_INK)
	concordance_title.text = tr_str("CONCORDANCE_TITLE")
	left_box.add_child(concordance_title)

	var scroll_conc := ScrollContainer.new()
	scroll_conc.custom_minimum_size = Vector2(628, 220)
	scroll_conc.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll_conc.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	left_box.add_child(scroll_conc)

	concordance_container = GridContainer.new()
	concordance_container.columns = 1
	concordance_container.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	concordance_container.add_theme_constant_override("h_separation", 14)
	concordance_container.add_theme_constant_override("v_separation", 4)
	scroll_conc.add_child(concordance_container)
	btn_details = Button.new()
	btn_details.text = tr_str("DETAILS")
	btn_details.add_theme_font_override("font", _font_print())
	btn_details.add_theme_font_size_override("font_size", 17)
	_apply_paper_btn_style(btn_details, false)
	btn_details.pressed.connect(func(): demo._open_red_feedback())
	left_box.add_child(btn_details)

	finding_label = RichTextLabel.new()
	finding_label.custom_minimum_size = Vector2(628, 64)
	finding_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	finding_label.bbcode_enabled = true
	finding_label.fit_content = true
	finding_label.scroll_active = false
	finding_label.add_theme_font_override("normal_font", _font_writing())
	finding_label.add_theme_font_size_override("normal_font_size", 16)
	finding_label.add_theme_color_override("default_color", COLOR_INK)
	finding_label.text = tr_str("INITIAL_FINDING")
	left_box.add_child(finding_label)

	# --- Right Page Top Bar (offset_left=710, top=24, width=534, height=46) ---
	var right_top_bar := HBoxContainer.new()
	right_top_bar.position = Vector2(710, 24)
	right_top_bar.size = Vector2(534, 44)
	right_top_bar.add_theme_constant_override("separation", 8)
	add_child(right_top_bar)

	# Language switcher buttons
	var lang_box := HBoxContainer.new()
	lang_box.add_theme_constant_override("separation", 2)
	right_top_bar.add_child(lang_box)

	for loc_info in [["zh_CN", "中文"], ["en", "EN"], ["ja", "日"]]:
		var l_btn := Button.new()
		l_btn.text = loc_info[1]
		l_btn.add_theme_font_override("font", _font_print())
		l_btn.add_theme_font_size_override("font_size", 16)
		l_btn.custom_minimum_size = Vector2(36, 30)
		_apply_paper_btn_style(l_btn, false)
		var code: String = loc_info[0]
		l_btn.pressed.connect(func(): _set_language(code))
		lang_box.add_child(l_btn)
		lang_buttons[code] = l_btn

	var sep := Control.new()
	sep.custom_minimum_size = Vector2(8, 0)
	right_top_bar.add_child(sep)

	# Keep the mode tabs on their own row so all languages fit the page.
	var mode_bar := HBoxContainer.new()
	mode_bar.position = Vector2(710, 70)
	mode_bar.size = Vector2(534, 44)
	mode_bar.add_theme_constant_override("separation", 8)
	add_child(mode_bar)
	mode_tab_sequence = Button.new()
	mode_tab_sequence.text = tr_str("TAB_SEQUENCE")
	mode_tab_sequence.clip_text = false
	mode_tab_sequence.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	mode_tab_sequence.add_theme_font_override("font", _font_print())
	mode_tab_sequence.add_theme_font_size_override("font_size", 18)
	mode_tab_sequence.custom_minimum_size = Vector2(120, 32)
	_apply_paper_btn_style(mode_tab_sequence, current_mode == "sequence")
	mode_tab_sequence.pressed.connect(func(): _switch_mode("sequence"))
	mode_tab_sequence.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	mode_bar.add_child(mode_tab_sequence)

	mode_tab_motivation = Button.new()
	mode_tab_motivation.text = tr_str("TAB_MOTIVATION")
	mode_tab_motivation.clip_text = false
	mode_tab_motivation.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	mode_tab_motivation.add_theme_font_override("font", _font_print())
	mode_tab_motivation.add_theme_font_size_override("font_size", 18)
	mode_tab_motivation.custom_minimum_size = Vector2(130, 32)
	_apply_paper_btn_style(mode_tab_motivation, current_mode == "motivation")
	mode_tab_motivation.pressed.connect(func(): _switch_mode("motivation"))
	mode_tab_motivation.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	mode_bar.add_child(mode_tab_motivation)

	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right_top_bar.add_child(spacer)

	btn_return = Button.new()
	btn_return.text = tr_str("RETURN")
	btn_return.add_theme_font_override("font", _font_print())
	btn_return.add_theme_font_size_override("font_size", 17)
	btn_return.custom_minimum_size = Vector2(96, 32)
	_apply_paper_btn_style(btn_return, false)
	btn_return.pressed.connect(func(): return_requested.emit())
	right_top_bar.add_child(btn_return)

	# --- Right Page Content Scroll Area (offset_left=710, top=74, width=534, height=790) ---
	right_scroll = ScrollContainer.new()
	right_scroll.position = Vector2(710, 384)
	right_scroll.size = Vector2(534, 480)
	right_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(right_scroll)

	right_content = VBoxContainer.new()
	right_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right_content.add_theme_constant_override("separation", 10)
	right_scroll.add_child(right_content)
	var summary_box := VBoxContainer.new()
	candidate_box = summary_box
	summary_box.position = Vector2(710, 122)
	summary_box.size = Vector2(534, 254)
	summary_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	summary_box.add_theme_constant_override("separation", 5)
	add_child(summary_box)
	candidate_summary = RichTextLabel.new()
	candidate_summary.name = "CurrentCandidateSummary"
	candidate_missing = RichTextLabel.new()
	candidate_missing.name = "CandidateMissingFields"
	candidate_semantics = RichTextLabel.new()
	for label in [candidate_summary, candidate_missing, candidate_semantics]:
		label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		label.bbcode_enabled = true
		label.fit_content = true
		label.scroll_active = false
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		label.add_theme_font_override("normal_font", _font_print())
		label.add_theme_font_size_override("normal_font_size", 15)
		label.add_theme_color_override("default_color", COLOR_INK)
		summary_box.add_child(label)

	# --- Right Page Bottom Bar Area (offset_left=710, top=870, width=534, height=90) ---
	var bottom_container := VBoxContainer.new()
	bottom_container.position = Vector2(710, 870)
	bottom_container.size = Vector2(534, 90)
	bottom_container.add_theme_constant_override("separation", 6)
	add_child(bottom_container)

	# Row 1: Presets line
	var preset_row := HBoxContainer.new()
	preset_row.add_theme_constant_override("separation", 8)
	bottom_container.add_child(preset_row)

	preset_label = Label.new()
	preset_label.text = tr_str("PRESET_HEADER")
	preset_label.hide()
	preset_label.add_theme_font_override("font", _font_print())
	preset_label.add_theme_font_size_override("font_size", 16)
	preset_label.add_theme_color_override("font_color", COLOR_MUTED)
	preset_row.add_child(preset_label)

	btn_preset_h1 = Button.new()
	btn_preset_h1.text = tr_str("PRESET_H1")
	btn_preset_h1.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	btn_preset_h1.clip_text = false
	btn_preset_h1.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	btn_preset_h1.add_theme_font_override("font", _font_print())
	btn_preset_h1.add_theme_font_size_override("font_size", 16)
	_apply_paper_btn_style(btn_preset_h1, false)
	btn_preset_h1.pressed.connect(func(): _load_preset("H1"))
	preset_row.add_child(btn_preset_h1)

	btn_preset_h2 = Button.new()
	btn_preset_h2.text = tr_str("PRESET_H2")
	btn_preset_h2.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	btn_preset_h2.clip_text = false
	btn_preset_h2.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	btn_preset_h2.add_theme_font_override("font", _font_print())
	btn_preset_h2.add_theme_font_size_override("font_size", 16)
	_apply_paper_btn_style(btn_preset_h2, false)
	btn_preset_h2.pressed.connect(func(): _load_preset("H2"))
	preset_row.add_child(btn_preset_h2)

	# Row 2: Action buttons
	var action_row := HBoxContainer.new()
	action_row.add_theme_constant_override("separation", 10)
	bottom_container.add_child(action_row)

	btn_evaluate = Button.new()
	btn_evaluate.name = "ExecuteDeduction"
	btn_evaluate.text = tr_str("EVALUATE")
	btn_evaluate.add_theme_font_override("font", _font_print())
	btn_evaluate.add_theme_font_size_override("font_size", 18)
	btn_evaluate.custom_minimum_size = Vector2(170, 36)
	_apply_paper_btn_style(btn_evaluate, true)
	btn_evaluate.pressed.connect(_on_evaluate_pressed)
	action_row.add_child(btn_evaluate)

	btn_reset = Button.new()
	btn_reset.text = tr_str("RESET")
	btn_reset.add_theme_font_override("font", _font_print())
	btn_reset.add_theme_font_size_override("font_size", 17)
	btn_reset.custom_minimum_size = Vector2(100, 36)
	_apply_paper_btn_style(btn_reset, false)
	btn_reset.pressed.connect(_on_reset_pressed)
	action_row.add_child(btn_reset)

func _set_language(code: String) -> void:
	if has_node("/root/GameLocale"):
		get_node("/root/GameLocale").call("set_language", code)
	else:
		TranslationServer.set_locale(code)
	refresh()

func _switch_mode(mode: String) -> void:
	current_mode = mode
	right_scroll.scroll_vertical = 0
	refresh()

func refresh() -> void:
	var loc := _get_locale()
	var scroll_position := right_scroll.scroll_vertical
	# Update language button styling
	for code in lang_buttons:
		var btn: Button = lang_buttons[code]
		_apply_paper_btn_style(btn, code == loc)

	# Update mode tab styling
	_apply_paper_btn_style(mode_tab_sequence, current_mode == "sequence")
	_apply_paper_btn_style(mode_tab_motivation, current_mode == "motivation")

	mode_tab_sequence.text = tr_str("TAB_SEQUENCE")
	mode_tab_motivation.text = tr_str("TAB_MOTIVATION")
	btn_return.text = tr_str("RETURN")
	btn_evaluate.text = tr_str("EVALUATE")
	btn_reset.text = tr_str("RESET")
	preset_label.text = tr_str("PRESET_HEADER")
	btn_preset_h1.text = tr_str("PRESET_H1")
	btn_preset_h2.text = tr_str("PRESET_H2")
	concordance_title.text = tr_str("CONCORDANCE_TITLE")
	btn_details.text = tr_str("DETAILS")
	if finding_label:
		finding_label.add_theme_font_override("normal_font", _font_writing())
		if last_engine_message != "":
			finding_label.text = _translate_engine_message(last_engine_message)
		else:
			finding_label.text = tr_str("INITIAL_FINDING")

	_render_concordance()
	_render_candidate_summary()
	if current_mode == "sequence":
		_render_sequence_view()
	else:
		_render_motivation_view()
	right_scroll.set_deferred("scroll_vertical", scroll_position)

func _render_candidate_summary() -> void:
	var specs := [
		["wedge", "木楔", "FIELD_WEDGE", {"dirty_then_wrap":"OPT_DIRTY_THEN_WRAP", "clean_wrap":"OPT_CLEAN_WRAP"}],
		["holder", "持柄", "FIELD_HOLDER", {"engineer":"OPT_HOLDER_ENG", "cook":"OPT_HOLDER_CHEF"}],
		["flow", "进水", "FIELD_FLOW", {"ash":"OPT_FLOW_ASH", "water":"OPT_FLOW_WATER"}],
		["cloth_before_flow", "夹滤", "FIELD_CLOTH", {"yes":"OPT_CLOTH_BEFORE", "no":"OPT_CLOTH_AFTER"}],
		["wipe", "擦地", "FIELD_WIPE", {"after":"OPT_WIPE_AFTER", "before":"OPT_WIPE_BEFORE"}],
		["release", "松柄", "FIELD_RELEASE", {"after_confirmation":"OPT_RELEASE_CONFIRMED", "before":"OPT_RELEASE_BEFORE"}],
		["returned", "交筹", "FIELD_RETURN", {"yes":"OPT_RETURN_YES", "no":"OPT_RETURN_NO"}],
		["take", "前屉", "FIELD_TAKE", {"after_return":"OPT_TAKE_AFTER", "never":"OPT_TAKE_NEVER"}],
		["return_before_room", "返房", "FIELD_SIGN", {"yes":"OPT_SIGN_YES", "no":"OPT_SIGN_NO"}],
		["identity", "身份", "FIELD_TAKER", {"receptionist":"OPT_TAKER_REC", "student":"OPT_TAKER_STU"}],
		["engineer_exception", "持柄规程", "FIELD_ENG_RULE", {"read":"OPT_ENG_EXCEPTION", "unread":"OPT_ENG_DAILY"}],
		["sound_pipe", "台侧声", "FIELD_ENG_PIPE", {"ringing":"OPT_ENG_PIPE_RING", "quiet":"OPT_ENG_PIPE_QUIET"}],
		["completion_record", "转清规程", "FIELD_CHEF_RULE", {"actual":"OPT_CHEF_VERIFY", "mistaken":"OPT_CHEF_MISTAKEN"}],
		["cook_filter", "夹滤岗位", "FIELD_CHEF_SENSE", {"yes":"OPT_CHEF_EYE_CLEAR", "no":"OPT_CHEF_EYE_NONE"}],
		["sound_gap", "门侧声", "FIELD_MUSIC_GAP", {"quiet":"OPT_MUSIC_GAP_QUIET", "ringing":"OPT_MUSIC_GAP_RING"}]
	]
	var current: Array[String] = []
	var missing: Array[String] = []
	var captions_en := ["Wedge","Holder","Flow","Clamping","Wiping","Release","Deposit","Drawer","Room return","Identity","Handle rule","Pipe sound","Clarity rule","Filter post","Door sound"]
	var captions_ja := ["木楔","保持担当","流体","布固定","床拭き","離手","返還","前抽斗","帰室","身元","保持規程","管の音","清澄規程","固定担当","扉の音"]
	for index in specs.size():
		var spec: Array = specs[index]
		var key: String = spec[0]
		var value := _get_field_val(key)
		var caption: String = spec[1] if _get_locale() == "zh_CN" else captions_en[index] if _get_locale() == "en" else captions_ja[index]
		if value == "unknown" and key != "identity": missing.append(caption)
		if (current_mode == "sequence" and index < 10) or (current_mode == "motivation" and (index >= 10 or key == "take")):
			var shown := tr_str(spec[3].get(value, "OPT_UNKNOWN"))
			if key == "identity" and value == "unknown":
				shown = "未知（合法保留）" if _get_locale() == "zh_CN" else "Unknown (allowed)" if _get_locale() == "en" else "不明（保留可）"
			current.append(caption+"："+shown)
	var loc := _get_locale()
	candidate_summary.text = ("本页当前候选 · " if loc == "zh_CN" else "Current page · " if loc == "en" else "現在の候補 · ")+"；".join(current)
	candidate_missing.text = ("尚未指定动作/规程：" if loc == "zh_CN" else "Unspecified actions/rules: " if loc == "en" else "未指定の動作・規程：")+("、".join(missing) if not missing.is_empty() else "无" if loc == "zh_CN" else "None" if loc == "en" else "なし")
	candidate_semantics.text = "未确定＝这份模拟没有指定该动作；可先提出并核验。证据仍未知不等于不能提出候选；身份未知可合法保留。" if loc == "zh_CN" else "Unspecified means this replay has no assigned action. You may submit and inspect its gaps. Unknown evidence does not bar a candidate; an unknown identity is allowed." if loc == "en" else "未確定は再現に動作を指定していない意味です。候補を提出して不足を確認できます。証拠の不明は候補の禁止ではなく、身元不明は保留できます。"
	_layout_candidate_summary.call_deferred()

func _layout_candidate_summary() -> void:
	await get_tree().process_frame
	if not is_instance_valid(candidate_box): return
	candidate_box.size.y = candidate_box.get_combined_minimum_size().y
	var bottom := candidate_box.position.y + candidate_box.size.y + 12
	right_scroll.position.y = bottom
	right_scroll.size.y = maxf(864 - bottom, 100)

func _render_concordance() -> void:
	for child in concordance_container.get_children():
		concordance_container.remove_child(child)
		child.queue_free()

	var predictions: Dictionary = {}
	if demo and demo.has_meta("last_predictions"):
		predictions = demo.get_meta("last_predictions")
	var known: Array = demo.red.call("State").get("known", []) if demo else []
	var submitted: bool = demo != null and not demo.red_feedback.is_empty()

	for ev_id in EVIDENCE_KEYS:
		var item_card := HBoxContainer.new()
		item_card.custom_minimum_size = Vector2(608, 30)
		item_card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		item_card.add_theme_constant_override("separation", 6)
		concordance_container.add_child(item_card)

		var ev_name_key: String = "EV_" + str(ev_id)
		var ev_name: String = tr_str(ev_name_key)
		if ev_name == ev_name_key: ev_name = str(ev_id)

		var name_lbl := Label.new()
		name_lbl.text = ev_name
		name_lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		name_lbl.add_theme_font_override("font", _font_print())
		name_lbl.add_theme_font_size_override("font_size", 16)
		name_lbl.add_theme_color_override("font_color", COLOR_INK)
		name_lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		item_card.add_child(name_lbl)

		var status_str: String = predictions.get(ev_id, "unproven")
		var badge_text := ""
		var badge_color := COLOR_UNPROVEN
		if not ev_id in known:
			badge_text = tr_str("STATUS_UNINSPECTED")
		elif not submitted:
			badge_text = tr_str("STATUS_UNSUBMITTED")
		elif status_str == "compatible":
			badge_text = tr_str("STATUS_COMPATIBLE")
			badge_color = COLOR_COMPATIBLE
		elif status_str == "conflict":
			badge_text = tr_str("STATUS_CONFLICT")
			badge_color = COLOR_CONFLICT
		else:
			badge_text = tr_str("STATUS_UNPROVEN")
			badge_color = COLOR_UNPROVEN

		var badge_lbl := Label.new()
		badge_lbl.text = badge_text
		badge_lbl.add_theme_font_override("font", _font_print())
		badge_lbl.add_theme_font_size_override("font_size", 16)
		badge_lbl.add_theme_color_override("font_color", badge_color)
		item_card.add_child(badge_lbl)

func _render_sequence_view() -> void:
	for child in right_content.get_children():
		right_content.remove_child(child)
		child.queue_free()

	# Stage 1: Door Wedge & Closure
	var card1 := _create_card(tr_str("STAGE_1_TITLE"))
	right_content.add_child(card1)
	_add_segmented_choice(card1, tr_str("FIELD_WEDGE"), "wedge", [
		["dirty_then_wrap", tr_str("OPT_DIRTY_THEN_WRAP")],
		["clean_wrap", tr_str("OPT_CLEAN_WRAP")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])

	# Stage 2: Valve Flow & Filtration
	var card2 := _create_card(tr_str("STAGE_2_TITLE"))
	right_content.add_child(card2)
	_add_segmented_choice(card2, tr_str("FIELD_HOLDER"), "holder", [
		["engineer", tr_str("OPT_HOLDER_ENG")],
		["cook", tr_str("OPT_HOLDER_CHEF")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])
	_add_segmented_choice(card2, tr_str("FIELD_FLOW"), "flow", [
		["ash", tr_str("OPT_FLOW_ASH")],
		["water", tr_str("OPT_FLOW_WATER")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])
	_add_segmented_choice(card2, tr_str("FIELD_CLOTH"), "cloth_before_flow", [
		["yes", tr_str("OPT_CLOTH_BEFORE")],
		["no", tr_str("OPT_CLOTH_AFTER")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])
	_add_segmented_choice(card2, tr_str("FIELD_WIPE"), "wipe", [
		["after", tr_str("OPT_WIPE_AFTER")],
		["before", tr_str("OPT_WIPE_BEFORE")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])
	_add_segmented_choice(card2, tr_str("FIELD_RELEASE"), "release", [
		["after_confirmation", tr_str("OPT_RELEASE_CONFIRMED")],
		["before", tr_str("OPT_RELEASE_BEFORE")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])

	# Stage 3: Tool Return & Token Trajectory
	var card3 := _create_card(tr_str("STAGE_3_TITLE"))
	right_content.add_child(card3)
	_add_segmented_choice(card3, tr_str("FIELD_RETURN"), "returned", [
		["yes", tr_str("OPT_RETURN_YES")],
		["no", tr_str("OPT_RETURN_NO")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])
	_add_segmented_choice(card3, tr_str("FIELD_TAKE"), "take", [
		["after_return", tr_str("OPT_TAKE_AFTER")],
		["never", tr_str("OPT_TAKE_NEVER")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])
	_add_segmented_choice(card3, tr_str("FIELD_SIGN"), "return_before_room", [
		["yes", tr_str("OPT_SIGN_YES")],
		["no", tr_str("OPT_SIGN_NO")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])
	_add_segmented_choice(card3, tr_str("FIELD_TAKER"), "identity", [
		["receptionist", tr_str("OPT_TAKER_REC")],
		["student", tr_str("OPT_TAKER_STU")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])

func _render_motivation_view() -> void:
	for child in right_content.get_children():
		right_content.remove_child(child)
		child.queue_free()

	# Actor selector bar
	var actor_bar := GridContainer.new()
	actor_bar.columns = 2
	actor_bar.add_theme_constant_override("separation", 6)
	right_content.add_child(actor_bar)

	var actors := [
		["engineer", tr_str("ACTOR_ENG")],
		["chef", tr_str("ACTOR_CHEF")],
		["musician", tr_str("ACTOR_MUSIC")],
		["taker", tr_str("ACTOR_TAKER")]
	]
	for a_info in actors:
		var btn := Button.new()
		btn.text = a_info[1]
		btn.clip_text = false
		btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		btn.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		btn.custom_minimum_size.y = 40
		btn.add_theme_font_override("font", _font_print())
		btn.add_theme_font_size_override("font_size", 16)
		var a_id: String = a_info[0]
		_apply_paper_btn_style(btn, a_id == selected_actor)
		btn.pressed.connect(func():
			selected_actor = a_id
			right_scroll.scroll_vertical = 0
			refresh())
		actor_bar.add_child(btn)

	# Content according to selected actor
	match selected_actor:
		"engineer":
			_render_engineer_motivation()
		"chef":
			_render_chef_motivation()
		"musician":
			_render_musician_motivation()
		"taker":
			_render_taker_motivation()

func _render_engineer_motivation() -> void:
	var rule_card := _create_card(tr_str("MOTIVATION_RULE_HEADER"))
	right_content.add_child(rule_card)
	_add_segmented_choice(rule_card, tr_str("FIELD_ENG_RULE"), "engineer_exception", [
		["unread", tr_str("OPT_ENG_DAILY")],
		["read", tr_str("OPT_ENG_EXCEPTION")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])
	var ex_val := _get_field_val("engineer_exception")
	if ex_val == "read":
		_add_desc_label(rule_card, tr_str("DESC_ENG_EXCEPTION"))
	elif ex_val == "unread":
		_add_desc_label(rule_card, tr_str("DESC_ENG_DAILY"))

	var sense_card := _create_card(tr_str("MOTIVATION_SENSE_HEADER"))
	right_content.add_child(sense_card)
	_add_segmented_choice(sense_card, tr_str("FIELD_ENG_PIPE"), "sound_pipe", [
		["ringing", tr_str("OPT_ENG_PIPE_RING")],
		["quiet", tr_str("OPT_ENG_PIPE_QUIET")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])

	var eval_card := _create_card(tr_str("MOTIVATION_EVAL_HEADER"))
	right_content.add_child(eval_card)
	var eval_text := _get_engineer_eval_text()
	_add_eval_label(eval_card, eval_text)

func _render_chef_motivation() -> void:
	var rule_card := _create_card(tr_str("MOTIVATION_RULE_HEADER"))
	right_content.add_child(rule_card)
	_add_segmented_choice(rule_card, tr_str("FIELD_CHEF_RULE"), "completion_record", [
		["actual", tr_str("OPT_CHEF_VERIFY")],
		["mistaken", tr_str("OPT_CHEF_MISTAKEN")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])
	var comp_val := _get_field_val("completion_record")
	if comp_val == "actual":
		_add_desc_label(rule_card, tr_str("DESC_CHEF_VERIFY"))
	elif comp_val == "mistaken":
		_add_desc_label(rule_card, tr_str("DESC_CHEF_MISTAKEN"))

	var sense_card := _create_card(tr_str("MOTIVATION_SENSE_HEADER"))
	right_content.add_child(sense_card)
	_add_segmented_choice(sense_card, tr_str("FIELD_CHEF_SENSE"), "cook_filter", [
		["yes", tr_str("OPT_CHEF_EYE_CLEAR")],
		["no", tr_str("OPT_CHEF_EYE_NONE")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])

	var eval_card := _create_card(tr_str("MOTIVATION_EVAL_HEADER"))
	right_content.add_child(eval_card)
	var eval_text := _get_chef_eval_text()
	_add_eval_label(eval_card, eval_text)

func _render_musician_motivation() -> void:
	var sense_card := _create_card(tr_str("MOTIVATION_SENSE_HEADER"))
	right_content.add_child(sense_card)
	_add_segmented_choice(sense_card, tr_str("FIELD_MUSIC_GAP"), "sound_gap", [
		["quiet", tr_str("OPT_MUSIC_GAP_QUIET")],
		["ringing", tr_str("OPT_MUSIC_GAP_RING")],
		["unknown", tr_str("OPT_UNKNOWN")]
	])

	var eval_card := _create_card(tr_str("MOTIVATION_EVAL_HEADER"))
	right_content.add_child(eval_card)
	var eval_text := _get_musician_eval_text()
	_add_eval_label(eval_card, eval_text)

func _render_taker_motivation() -> void:
	var rule_card := _create_card(tr_str("FIELD_TAKER_RULE"))
	right_content.add_child(rule_card)
	var known: Array = demo.red.call("State").get("known", []) if demo else []
	_add_desc_label(rule_card, tr_str("DESC_TAKER_BOX" if "E13" in known else "DESC_TAKER_BOX_UNREAD"))
	_add_desc_label(rule_card, tr_str("DESC_TAKER_SEAL" if "E15" in known else "DESC_TAKER_SEAL_UNREAD"))
	var take_val := _get_field_val("take")
	var take_caption := tr_str("OPT_TAKE_AFTER" if take_val == "after_return" else "OPT_TAKE_NEVER" if take_val == "never" else "OPT_UNKNOWN")
	_add_desc_label(rule_card, tr_str("TAKER_CURRENT_EVENT") % take_caption)

	var eval_card := _create_card(tr_str("MOTIVATION_EVAL_HEADER"))
	right_content.add_child(eval_card)
	var eval_text := _get_taker_eval_text()
	_add_eval_label(eval_card, eval_text)

func _get_engineer_eval_text() -> String:
	var loc := _get_locale()
	var val_ex: String = _get_field_val("engineer_exception")
	var val_h: String = _get_field_val("holder")
	if val_h == "engineer" and val_ex == "unread":
		if loc == "ja": return "特例条令の記録なし。動作が日常復帰規程と矛盾。"
		if loc == "en": return "Missing authorization docket. Action contradicts daily reset protocol."
		return "缺少特许条令记录。动作与日常复位规程冲突。"
	elif val_h == "engineer" and val_ex == "read":
		if loc == "ja": return "特例条令E11に準拠。主厨の確認受領まで弁を保持、特例規程に適合。"
		if loc == "en": return "Bound by emergency docket E11. Holding handle pending chef confirmation conforms to procedure."
		return "已受特例条令 E11 约束。持续持柄等待主厨确认，符合特例规程。"
	else:
		if loc == "ja": return "台側圧力銅管：閉扉後も流体圧力による管壁振動が継続し、音響を伝達。"
		if loc == "en": return "Bench Copper Pipe: fluid vibration continues after door closure, maintaining acoustic path."
		return "合门后门侧缝隙阻断，空气声路静止；台侧压力铜管通过管壁振颤传递流体声响。"

func _get_chef_eval_text() -> String:
	var loc := _get_locale()
	var flow: String = _get_field_val("flow")
	var comp: String = _get_field_val("completion_record")
	if flow == "unknown" or comp == "unknown" or _get_field_val("cook_filter") == "unknown" or _get_field_val("release") == "unknown":
		if loc == "ja": return "候補は濾過担当・清澄確認・停止時期をまだ確定していません。実行後に左頁で照合してください。"
		if loc == "en": return "Filter post, clarity confirmation, or release timing remains unspecified. Execute and check the left page."
		return "候选尚未完整安排夹滤岗位、转清确认或停流时机。执行后以左页物证核对为准。"
	if flow == "water" or comp == "mistaken":
		if loc == "ja": return "候補は清水洗浄を確認記録に結び付けています。実行後、既知の捕集片と照合してください。"
		if loc == "en": return "Candidate links fresh-water washing to the confirmation record. Replay against the inspected capture plate."
		return "候选把清水洗布与确认记录联系起来；须执行后与已读捕集片材料核对。"
	else:
		if loc == "ja": return "候補の濾過・確認・停止動作は、実行後の物証照合で検証されます。"
		if loc == "en": return "Candidate filtering, confirmation and release actions require replay and evidence verification."
		return "夹滤、确认与停流是当前候选安排；是否能执行、能否生成痕迹，以重演与物证核对为准。"

func _get_musician_eval_text() -> String:
	var loc := _get_locale()
	var gap: String = _get_field_val("sound_gap")
	if gap == "unknown":
		if loc == "ja": return "候補は閉扉後の門側の音をまだ指定していません。"
		if loc == "en": return "Candidate has not specified the door-side sound after closure."
		return "候选尚未指定合门后门侧听到的声音。"
	if gap == "quiet":
		if loc == "ja": return "扉の隙間：閉扉後に空気音路が閉塞し、広間側は静粛を維持。"
		if loc == "en": return "Door Gap: air acoustic path blocked after closure; hall remains silent."
		return "门侧缝隙：合门后空气声路阻断，门厅侧保持静止。"
	else:
		if loc == "ja": return "扉の隙間：音漏れの記録は密閉状態の物理条件と不整合。"
		if loc == "en": return "Door Gap: acoustic leakage contradicts sealed door physical state."
		return "门侧缝隙：漏音记录与合门物理状态不符。"

func _get_taker_eval_text() -> String:
	var loc := _get_locale()
	var ret: String = _get_field_val("returned")
	var take: String = _get_field_val("take")
	if ret == "unknown" or take == "unknown":
		if loc == "ja": return "候補は返還または前引き出しからの取出しをまだ指定していません。調査済資料は上の参照欄に示します。出来事を選び、実行後に左頁で照合してください。"
		if loc == "en": return "Return or front-drawer retrieval is unspecified in this candidate. Inspected materials appear above. Choose events, then execute and compare on the left page."
		return "当前候选尚未指定交还或前抽屉取筹事件。已知材料见上方参照；选择事件后执行，以左页核对结果为准。"
	if ret == "yes" and take == "after_return":
		if loc == "ja": return "候補は返還後に前引き出しから取り出す動作を指定しています。史実の確定ではありません。実行後、調査済の受領記録と現物に照合してください。"
		if loc == "en": return "This candidate assigns retrieval through the front drawer after return. It is a proposed sequence; execute and compare with inspected receipt records and objects."
		return "当前候选安排“交还后开前抽屉再取”，尚不是历史结论；须执行后与已检视接收记录及原物核对。"
	if take == "never":
		if loc == "ja": return "候補は前引き出しからの取出しを行わないと指定しています。椅子の賭け金まで説明できるかは、実行後の調査済物証との照合で確かめてください。"
		if loc == "en": return "This candidate assigns no front-drawer retrieval. Execute and compare with inspected materials to check whether it accounts for the chair token."
		return "当前候选安排“从未取出”；是否能解释椅上现筹，须执行后与已检视材料核对。"
	if loc == "ja": return "候補は未返還と返還後の取出しを組み合わせています。出来事の条件が成立するか、実行後の結果で確認してください。"
	if loc == "en": return "This candidate combines no return with retrieval after return. Execute to check whether its event conditions can be met."
	return "当前候选同时安排未交还与交还后取筹；事件条件是否成立，以执行核对结果为准。"

func _create_card(title: String) -> PanelContainer:
	var panel := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = COLOR_CARD_BG
	style.border_color = COLOR_CARD_BORDER
	style.border_width_left = 1
	style.border_width_top = 1
	style.border_width_right = 1
	style.border_width_bottom = 1
	style.corner_radius_top_left = 3
	style.corner_radius_top_right = 3
	style.corner_radius_bottom_left = 3
	style.corner_radius_bottom_right = 3
	style.content_margin_left = 10
	style.content_margin_top = 7
	style.content_margin_right = 10
	style.content_margin_bottom = 7
	panel.add_theme_stylebox_override("panel", style)

	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 5)
	panel.add_child(box)

	var title_lbl := Label.new()
	title_lbl.text = title
	title_lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	title_lbl.add_theme_font_override("font", _font_print())
	title_lbl.add_theme_font_size_override("font_size", 17)
	title_lbl.add_theme_color_override("font_color", COLOR_INK)
	box.add_child(title_lbl)

	return panel

func _add_segmented_choice(card: PanelContainer, label_text: String, field_key: String, options: Array) -> void:
	var box: VBoxContainer = card.get_child(0)

	var row := VBoxContainer.new()
	row.add_theme_constant_override("separation", 3)
	box.add_child(row)

	var lbl := Label.new()
	lbl.text = label_text
	lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	lbl.add_theme_font_override("font", _font_print())
	lbl.add_theme_font_size_override("font_size", 17)
	lbl.add_theme_color_override("font_color", COLOR_MUTED)
	row.add_child(lbl)

	var btn_row := VBoxContainer.new()
	btn_row.add_theme_constant_override("separation", 5)
	row.add_child(btn_row)

	var current_val: String = _get_field_val(field_key)

	for opt in options:
		var opt_id: String = opt[0]
		var opt_label: String = opt[1]
		var btn := Button.new()
		btn.text = opt_label
		btn.clip_text = false
		btn.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
		btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		btn.custom_minimum_size.y = 40
		btn.toggle_mode = true
		btn.set_pressed_no_signal(opt_id == current_val)
		btn.add_theme_font_override("font", _font_print())
		btn.add_theme_font_size_override("font_size", 17)
		_apply_paper_btn_style(btn, opt_id == current_val)
		btn.pressed.connect(func():
			_set_field_val(field_key, opt_id)
			refresh())
		btn_row.add_child(btn)

func _add_desc_label(card: PanelContainer, text: String) -> void:
	var box: VBoxContainer = card.get_child(0)
	var lbl := RichTextLabel.new()
	lbl.text = text
	lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	lbl.bbcode_enabled = true
	lbl.fit_content = true
	lbl.scroll_active = false
	lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	lbl.add_theme_font_override("normal_font", _font_print())
	lbl.add_theme_font_size_override("normal_font_size", 17)
	lbl.add_theme_color_override("default_color", COLOR_MUTED)
	box.add_child(lbl)

func _add_eval_label(card: PanelContainer, text: String) -> void:
	var box: VBoxContainer = card.get_child(0)
	var lbl := RichTextLabel.new()
	lbl.text = tr_str("CANDIDATE_NOTE") + "\n" + text
	lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	lbl.bbcode_enabled = true
	lbl.fit_content = true
	lbl.scroll_active = false
	lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	lbl.add_theme_font_override("normal_font", _font_writing())
	lbl.add_theme_font_size_override("normal_font_size", 18)
	lbl.add_theme_color_override("default_color", COLOR_INK)
	box.add_child(lbl)

func _get_field_val(key: String) -> String:
	if not demo: return "unknown"
	for round_draft in demo.drafts:
		if round_draft.has(key):
			return str(round_draft[key])
	return "unknown"

func _set_field_val(key: String, val: String) -> void:
	if not demo: return
	if _get_field_val(key) == val: return
	var target_round := 0
	if key in ["wedge", "flow", "cloth_before_flow", "wipe"]:
		target_round = 0
	elif key in ["holder", "engineer_exception", "cook_filter", "sound_gap", "sound_pipe", "release", "completion_record"]:
		target_round = 1
	else:
		target_round = 2

	demo.drafts[target_round][key] = val
	demo.red.call("InvalidateCandidate")
	demo.red_feedback = {}
	demo.remove_meta("last_predictions")
	last_engine_message = ""
	demo._install_red_snapshots([])
	demo.verified = [false, false, false]

var last_engine_message := ""

func _translate_engine_message(msg: String) -> String:
	var loc := _get_locale()
	if loc == "zh_CN": return msg
	var translations := {
		"候选通过已知材料核对；未证明的部分继续保留。": {
			"en": "Hypothesis matches known evidence; unproven parts remain pending.",
			"ja": "仮説は既知の物証と適合。未証の箇所は保留されます。"
		},
		"候选与已知材料冲突，请查看具体差异。": {
			"en": "Hypothesis conflicts with known evidence; review discrepancies.",
			"ja": "仮説は既知の物証と矛盾しています。差異を確認してください。"
		},
		"候选有无法提交的动作；请检查器具所有权、岗位和持续占用。": {
			"en": "Hypothesis contains invalid actions; check tool possession, posts, and ongoing hold.",
			"ja": "実行不能な動作が含まれています。器具の所持、岗位、占有状態を確認してください。"
		},
		"这份候选尚未形成可核对的交接因果链。": {
			"en": "This hypothesis has not established a verifiable causal chain of handover.",
			"ja": "この仮説は検証可能な引渡の因果連鎖を形成していません。"
		},
		"这一片段尚无材料可验证。": {
			"en": "No material evidence available yet to verify this segment.",
			"ja": "この場面を検証するための物証がまだありません。"
		}
	}
	if translations.has(msg):
		var item: Dictionary = translations[msg]
		if item.has(loc): return item[loc]
	return msg

func _on_evaluate_pressed() -> void:
	if not demo: return
	var candidate := {}
	for frag in demo.drafts: candidate.merge(frag, true)
	demo.red.call("ClearCandidate")
	var result: Dictionary = demo.red.call("EvaluateRound", 3, candidate)
	var preds: Dictionary = result.get("predictions", {})
	if preds.is_empty():
		for comp in result.get("comparisons", []):
			preds[str(comp.get("source", ""))] = str(comp.get("status", "unproven"))
	demo.set_meta("last_predictions", preds)
	demo.red_feedback = result
	var accepted: bool = result.get("ok", false)
	demo.verified = [accepted, accepted, accepted]
	last_engine_message = str(result.get("message", "推演未形成可核对结论。"))
	finding_label.text = _translate_engine_message(last_engine_message)
	demo._install_red_snapshots(result.get("snapshots", []))
	deduction_evaluated.emit(result)
	refresh()

func _on_reset_pressed() -> void:
	if not demo: return
	demo.drafts = [{}, {}, {}]
	demo.red.call("ClearCandidate")
	demo.verified = [false, false, false]
	demo.remove_meta("last_predictions")
	demo.red_feedback = {}
	demo._install_red_snapshots([])
	last_engine_message = ""
	finding_label.text = tr_str("INITIAL_FINDING")
	refresh()

func _load_preset(kind: String) -> void:
	if not demo: return
	demo._load_competitor(kind)
	_on_evaluate_pressed()

func _apply_paper_btn_style(btn: Button, is_selected: bool) -> void:
	var s_normal := StyleBoxFlat.new()
	var s_hover := StyleBoxFlat.new()
	var s_pressed := StyleBoxFlat.new()

	var bg: Color = COLOR_BTN_SELECTED_BG if is_selected else COLOR_BTN_BG
	var border: Color = COLOR_BTN_SELECTED_BORDER if is_selected else COLOR_BTN_BORDER
	var border_w: int = 2 if is_selected else 1

	for s in [s_normal, s_hover, s_pressed]:
		s.corner_radius_top_left = 3
		s.corner_radius_top_right = 3
		s.corner_radius_bottom_left = 3
		s.corner_radius_bottom_right = 3
		s.content_margin_left = 7
		s.content_margin_top = 4
		s.content_margin_right = 7
		s.content_margin_bottom = 4

	s_normal.bg_color = bg
	s_normal.border_color = border
	s_normal.border_width_left = border_w
	s_normal.border_width_top = border_w
	s_normal.border_width_right = border_w
	s_normal.border_width_bottom = border_w

	s_hover.bg_color = COLOR_BTN_HOVER_BG
	s_hover.border_color = COLOR_BTN_SELECTED_BORDER
	s_hover.border_width_left = border_w
	s_hover.border_width_top = border_w
	s_hover.border_width_right = border_w
	s_hover.border_width_bottom = border_w

	s_pressed.bg_color = COLOR_BTN_SELECTED_BG
	s_pressed.border_color = COLOR_BTN_SELECTED_BORDER
	s_pressed.border_width_left = 2
	s_pressed.border_width_top = 2
	s_pressed.border_width_right = 2
	s_pressed.border_width_bottom = 2
	var s_focus := s_hover.duplicate() as StyleBoxFlat
	s_focus.bg_color = Color(0, 0, 0, 0)
	s_focus.border_color = Color("a76320")
	s_focus.set_border_width_all(2)

	btn.add_theme_stylebox_override("normal", s_normal)
	btn.add_theme_stylebox_override("hover", s_hover)
	btn.add_theme_stylebox_override("pressed", s_pressed)
	btn.add_theme_stylebox_override("focus", s_focus)
	btn.add_theme_stylebox_override("disabled", s_normal)

	var text_col: Color = Color("fff8e8") if is_selected else COLOR_INK
	btn.add_theme_color_override("font_color", text_col)
	btn.add_theme_stylebox_override("hover_pressed", s_pressed)
	btn.add_theme_color_override("font_hover_color", COLOR_INK)
	btn.add_theme_color_override("font_pressed_color", Color("fff8e8"))
	btn.add_theme_color_override("font_hover_pressed_color", Color("fff8e8"))
	btn.add_theme_color_override("font_focus_color", text_col)
