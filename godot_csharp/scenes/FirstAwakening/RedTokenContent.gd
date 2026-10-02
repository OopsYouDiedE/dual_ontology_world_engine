extends RefCounted
## Chapter-local authored content. Fact acquisition and causal decisions stay in C#.

const ROOM_SITES = {
 "ROOM_COAT": ["衣架外衣与衣袋凭据", Vector2(268,282)],
 "ROOM_DESK": ["书桌上的冷餐、水杯与残纸", Vector2(68,178)],
 "ROOM_DOOR": ["五号房门牌与内栓", Vector2(192,382)],
 "ROOM_WINDOW": ["铁格窗外的海雾", Vector2(108,104)]
}

const RED_SITES = {
 "E01": ["凭证与领取柜", Vector2(498,556), "S03"],
 "E02": ["连页规程与安全退出", Vector2(320,652), "S11"],
 "E03": ["断水空载试柄", Vector2(420,700), "S12"],
 "E04": ["门缝与压力铜管", Vector2(600,884), "S14"],
 "E05": ["案脚缺楔与木纤维", Vector2(300,688), "S06"],
 "E06": ["配重门、双锁与灰带", Vector2(644,954), "S06"],
 "E07": ["五号房纸包与灰楔", Vector2(108,202), "S07"],
 "E08": ["布夹折边与地板纤维", Vector2(904,580), "S08"],
 "E09": ["双进水、当班捕集片与副联", Vector2(858,580), "S15"],
 "E10": ["器材架接掌存根", Vector2(392,652), "S13"],
 "E11": ["指令板背具名特例", Vector2(368,640), "S13"],
 "E12": ["触物短片段：两人的声音", Vector2(376,700), "S13"],
 "E13": ["单向归还匣与双槽接收印", Vector2(448,652), "S18"],
 "E14": ["白片与四人返房签条", Vector2(512,420), "S19"],
 "E15": ["椅上红筹、断封与半手印", Vector2(420,748), "S20"],
 "E16": ["铜口：质询纸带与封条矛盾", Vector2(498,556), "S22"]
}

const FIELDS = [
 [["cloth_before_flow","布夹在排灰前",["unknown","yes","no"]], ["flow","经过哪条进水",["unknown","ash","water"]], ["wipe","擦地阶段",["unknown","before","after"]], ["wedge","楔灰与包纸",["unknown","dirty_then_wrap","clean_wrap"]]],
 [["engineer_exception","工程师动作前特例",["unknown","read","unread"]], ["holder","持续持柄岗位",["unknown","engineer","cook"]], ["cook_filter","主厨先夹滤确认",["unknown","yes","no"]], ["sound_gap","合门后门侧声路",["unknown","quiet","ringing"]], ["sound_pipe","合门后台侧声路",["unknown","quiet","ringing"]], ["release","放手时机",["unknown","after_confirmation","before"]]],
 [["returned","红筹曾进入归还匣",["unknown","yes","no"]], ["take","前抽屉取筹",["unknown","after_return","never"]], ["return_before_room","交接先于四人返房",["unknown","yes","no"]], ["identity","后取者身份",["unknown","receptionist","student"]]]
]

const WORDS = {"unknown":"未知／未证明","yes":"是","no":"否","ash":"排灰支路","water":"厨房清水","before":"之前","after":"之后","dirty_then_wrap":"沾灰后包纸","clean_wrap":"干净时包纸","read":"已读并接受具名特例","unread":"未读，仅知日常复位","engineer":"工程师","cook":"主厨","quiet":"安静","ringing":"仍响","after_confirmation":"盆侧观察转清确认后","after_return":"交回后开抽屉再取","never":"从未取出","receptionist":"接待者","student":"四人之一"}

static func defense_sections() -> Array:
	return [
		["open","开态一定违规？",["E02+E11","E02","E03"],[["special_hold","有具名特例并完成确认的持续岗位"],["daily_reset","依日常复位条款，将当前开态判作违规"],["unexplained","空载试柄可回位；实际持续开态尚无解释"]],[["inner_bolt_operator","内销者与四人精确顺序仍未知"],["none","全场细节都已证明"]]],
		["token","椅上红筹等于未归？",["E13+E14","E15","E09"],[["returned_then_taken","曾单向交回，后来开前抽屉再置椅"],["never_returned","据椅上现筹，将其解释为从未交回"],["white_means_complete","据已领白片，将其解释为排灰已完成"]],[["taker_without_E16","未取现实移物单，后取者仍未知"],["identity_supported_E16","移物单与自认支持接待者的一次后取"],["none","后取可证明全部物证伪造"]]],
		["memory","空白能证明学生操作致失忆？",["E14","E16","E02"],[["no_material_causal_link","现有材料没有把排灰操作连接到失忆"],["operation_caused_loss","将排灰后出现的记忆空白解释为操作致失忆"]],[["sealing_scope","封存具体时刻、范围与原因仍未证明"],["none","已证明失忆与排灰绝无任何关系"]]]
	]


static func defense_statements() -> Dictionary:
	# Optional examples retain the original field values. Showing them never
	# changes the draft; only explicitly choosing an example writes three fields.
	return {
		"open": [["E02+E11","special_hold","inner_bolt_operator"],["E02","daily_reset","none"],["E03","unexplained","inner_bolt_operator"]],
		"token": [["E13+E14","returned_then_taken","taker_without_E16"],["E13+E14","returned_then_taken","identity_supported_E16"],["E15","never_returned","none"],["E09","white_means_complete","none"]],
		"memory": [["E14","no_material_causal_link","sealing_scope"],["E16","operation_caused_loss","none"],["E02","operation_caused_loss","none"]]
	}


const DELIVERIES = {
	"侦探": "差旅酬金：定额支票与验讫双联；主楼文献室调阅凭照载明查阅权限及随时离校终止权，骑缝暗记与本人凭据对齐。主楼开放后可核验调阅资格。",
	"工程师霍铎": "试件：拆壳核验内螺纹齿距、青铜轴衬与渗碳淬火外层，并按出厂规制复原紧固构件；确认真货与淬火硬度。",
	"主厨沈鼎": "定额首笔酬劳：逐枚核验币种、银数、成色与分量；凭收两联的印记及数目与约定额度相符，存根副联由本人保管。",
	"音乐家苏泠": "合金簧片：以均等力度敲击，核对首枚泛音与次枚基频；互换工位复敲，确认声学特征一致与实物真伪。"
}

const MATERIAL_NAMES = {"E02+E11": "连页规程＋具名特例", "E02": "连页规程", "E03": "空载试柄", "E13+E14": "归还匣接收印＋返房签条", "E15": "椅上红筹与断封", "E09": "水盆与转清副联", "E14": "白片与返房签条", "E16": "现实巡检移物单"}

const MATERIAL_SCOPE = {"E02+E11": "常规与特例相互核对", "E02": "有复位条款，仍须核对特例", "E03": "仅见断水空载的回位", "E13+E14": "接收痕迹与返房记录相互核对", "E15": "见现筹与断封，单独不能倒推未归", "E09": "记载转清，不代替归还匣交接记录", "E14": "交接与返房记录，不记失忆成因", "E16": "巡检移物记录，不记失忆成因"}

# Add or repair a safe interaction here; C# still validates and commits it.
const SAFE_INTERACTIONS = {
	"E03": [["press","按住试柄","空载弹簧试件"],["release","松开试柄","弹簧回位"]],
	"E04": [["gap","门侧站位轻敲听声","门缝声路"],["close","合上试件门缝","门缝合拢"],["pipe","台侧站位轻敲听声","压力铜管声路"]],
	"E13": [["red","放入筹试片","放入筹试片"],["push_red","推动筹接收件","推动筹接收件"],["handle","放入柄试片","放入柄试片"],["push_handle","推动柄接收件","推动柄接收件"],["white","领取白片","领取白片"]]
}

const FOLLOWUP_INSPECTIONS = {
	"E09": [["E09.deep","查看捕集片"],["E09.completion","核对签记"]],
	"E10": [["E12","请工程师与音乐家触物并具名转述"]],
	"E11": [["E12","请工程师与音乐家触物并具名转述"]]
}

static func observation_title(id: String) -> String:
	return id if ROOM_SITES.has(id) else "OBS_" + id.replace(".","_")

static func safe_interaction_site(operation: String) -> String:
	if operation.begins_with("reward:"): return "E01"
	for id in SAFE_INTERACTIONS:
		for entry in SAFE_INTERACTIONS[id]:
			if entry[0] == operation: return id
	return ""
