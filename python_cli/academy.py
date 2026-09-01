"""《异常感知学园》正式内容层。

人物、历史、记忆、设施、职业行动和封印部件都使用核心的 Entity、Facet、
Intent、RulePatch、Recipe、Rite 与 Covenant。此模块只负责游戏内容、胜负、
NPC 决策分支和终端版所需的流程，不向核心加入房间内空间或 UI 概念。
"""

from core.python.engine import (
    ActionRuntime,
    Character,
    Directive,
    E,
    Entity,
    F,
    InformationCarrier,
    Intent,
    Knowledge,
    KotodamaSystem,
    Mode,
    Passage,
    Recipe,
    Rule,
    RuleEnvironment,
    RulePatch,
    RulePublication,
    RuleSystem,
    Soul,
    Timeline,
    WorldSpace,
)


ASPECTS = {
    "Form": "形质", "Name": "名契", "Law": "律令", "Mind": "心识",
    "Bond": "缘系", "Threshold": "门隙", "Transmutation": "嬗变",
    "Preservation": "存续", "Death": "终寂", "Desire": "欲求",
    "Strife": "争斗", "Vestige": "遗痕",
}


# 内容表只在建造世界时解析。运行时关系、目标、场景和材料均保存对象引用。
CHARACTER_SPECS = (
    ("侦探", "雾岛律", 1, "中央厅", "矛盾质询", "question", "临时协调者"),
    ("诗人", "鸣海澄", 2, "生活院", "即兴赋诗", "recite", "仪式咏唱者"),
    ("音乐家", "久世弦", 3, "共鸣厅", "声学测听", "listen", "声学侦察者"),
    ("博物学家", "野野宫蕾", 3, "博物与生态室", "活体样本无害化提取", "sample", "素材采集者"),
    ("机械工程师", "真壁铸", 2, "动力总站", "构件快修", "repair", "动力设施负责人"),
    ("主厨", "榊火澄", 1, "后勤院", "特制配给", "ration", "后勤核心"),
    ("考古学家", "冬城史", 2, "教学图书馆", "古文字破译", "decipher", "遗迹解码者"),
    ("神偷", "月见里鸢", 2, "门楼与誓约庭", "无声潜入", "infiltrate", "游击前哨"),
    ("律师", "御影章", 1, "契约审议室", "规则诉讼", "litigate", "规则仲裁者"),
    ("职业棋手", "白峰局", 0, "公共娱乐室", "态势弃子", "strategize", "资源调度核心"),
    ("化学家", "赤星砾", 1, "化学实验室", "物质裂解", "analyze", "物质分析官"),
    ("神射手", "苍井砥", 1, "训练院", "视线封锁", "suppress", "通道警戒哨"),
    ("舞蹈艺术家", "绯宫巡", 2, "中央草场与露天运动场", "旋回步法", "dance_path", "仪式节拍主导者"),
    ("画家", "青栖绘", 2, "美术工房与冬园", "画层剥离", "reconstruct", "视觉情报记录者"),
    ("建筑师", "方城础", 2, "星盘讲堂", "局部加固", "reinforce", "空间路线规划官"),
    ("临床医生", "灰原朔", 2, "公共诊疗所", "应急缝合", "treat", "战场医师"),
)


STORY_SPECS = (
    {
        "title": "被撤回的第二名录",
        "scene": "文献工房",
        "summary": "言灵导师发现第二名录会暴露恶魔的语义地址，命神偷取走拓片。侦探追查盗窃，律师依保密誓约撤回证词，考古学家把原文拆成四段保存。",
        "memories": {
            "侦探": ("沾银粉的指纹卡", "Card", "我确认神偷进入过名录库，却没有找到任何为私利盗取的证据。"),
            "神偷": ("归还的旧银币", "Token", "导师委托我偷走拓片；我故意留下银币，让侦探还能追到真相。"),
            "律师": ("封蜡撤证书", "Manuscript", "我删去了能洗清神偷嫌疑的证词，因为公开它会说出第二名录的语义地址。"),
            "考古学家": ("四折铭文拓片", "Rubbing", "我把真名结构拆成四段，分别交给四座研究院保管。"),
        },
        "bonds": (
            ("侦探", "神偷", "Distrust", "Protects", 2),
            ("律师", "考古学家", "KeepsSecretFor", "Entrusts", 3),
        ),
    },
    {
        "title": "无声首演事故",
        "scene": "共鸣厅",
        "summary": "四人排练稳定主阵的仪式乐章。诗人为保护听觉受损的音乐家改掉禁句，舞者补走缺拍，画家看见并画下并不存在的第五位指挥。",
        "memories": {
            "诗人": ("删改过的咏唱稿", "Manuscript", "我改掉禁句不是炫技；原句的共鸣会永久损伤音乐家的听觉。"),
            "音乐家": ("裂纹节拍蜡筒", "Cylinder", "诗人的改句救了我，却让主阵短暂失拍；我一直把责任推给诗人。"),
            "舞蹈艺术家": ("磨破的左舞靴", "Relic", "我独自补完缺失的第十三拍，脚伤来自替所有人承受阵线回冲。"),
            "画家": ("第五指挥速写", "Drawing", "台上只有四个人，镜中却有一位戴白手套的指挥提前给出节拍。"),
        },
        "bonds": (
            ("诗人", "音乐家", "Protects", "Resents", 3),
            ("舞蹈艺术家", "画家", "TrustsVisionOf", "Admires", 2),
        ),
    },
    {
        "title": "温室灰疫封锁",
        "scene": "种植梯田与温室",
        "summary": "源质孢子混入温室灌溉。化学家承认试剂泄漏，博物学家发现真正源头来自旧阵土，主厨销毁整季配给，医生隐瞒四人的暴露以免他们被学园长解剖。",
        "memories": {
            "博物学家": ("灰疫压叶标本", "Specimen", "孢子来自温室旧阵土，不是化学家的烧瓶；我当时没有勇气公开反驳学园长。"),
            "主厨": ("焚粮配给账", "Ledger", "我烧掉了整季粮食，因为最先变灰的是准备分给病房的面粉。"),
            "化学家": ("自认事故报告", "Manuscript", "我故意认下泄漏，好让学园长停止追查博物学家的活体标本。"),
            "临床医生": ("未登记病历夹", "CaseFile", "我们四人都曾暴露；我伪造阴性结果，避免任何人被送进白塔解剖室。"),
        },
        "bonds": (
            ("博物学家", "化学家", "OwesTruthTo", "Shelters", 3),
            ("主厨", "临床医生", "ReliesOn", "KeepsAlive", 3),
        ),
    },
    {
        "title": "西侧断电救援",
        "scene": "机械联锁室",
        "summary": "西侧断电困住一批学员。建筑师拆开承重墙，工程师过载水轮机，神射手压制失控役魔，棋手放弃名录库供电，把最后电力送往诊疗所。",
        "memories": {
            "机械工程师": ("熔断继电器", "MachinePart", "我知道过载会烧毁机械联锁，仍选择把电送回西侧货梯。"),
            "职业棋手": ("弃子供电图", "Diagram", "我切断名录库供电不是误算；两处只能救一处，我选择了仍有人呼吸的诊疗所。"),
            "神射手": ("刻印空弹壳", "Relic", "我守住货梯口，却在役魔喊出同伴声音时差点向救援队开枪。"),
            "建筑师": ("西墙拆改蓝图", "Blueprint", "承重墙本可安全拆开；学园长早就让人在墙内埋了未登记的导管。"),
        },
        "bonds": (
            ("机械工程师", "建筑师", "TrustsJudgmentOf", "SharesResponsibilityWith", 3),
            ("职业棋手", "神射手", "SacrificedFor", "Guards", 2),
        ),
    },
)


SCENE_GROUPS = {
    "岛屿生存环": (
        "南岸潮湾", "西湾沙滩与海浴场", "中央草场与露天运动场", "环岛步道与观景庭园",
        "海崖信号塔", "种植梯田与温室", "上水库与水务站", "林地石场与潮间观测站",
    ),
    "公共学园层": (
        "门楼与誓约庭", "中央厅", "教学图书馆", "生活院", "后勤院", "训练院",
        "公共娱乐室", "美术工房与冬园", "公共浴室与温泉池", "公共诊疗所",
    ),
    "文契院": ("文契院", "文献工房", "声纹与记录室", "契约审议室", "名录库"),
    "星仪院": ("星仪院", "星盘讲堂", "共鸣厅", "灵体观察室", "沉静室与心理记录室", "阵法基座室"),
    "铁工院": ("铁工院", "物理测定室", "精密工房", "电气实验室", "动力总站", "机械联锁室"),
    "白塔异象院": ("白塔异象院", "化学实验室", "博物与生态室", "手术与解剖室", "摄影与早期X光室", "污染标本库", "学园长隔离室"),
    "地下勤务与封印层": ("勤务环廊", "总仓与回收间", "废物处理站", "四锚封印廊", "源质反应炉", "核心隔离舱"),
}


CONNECTIONS = (
    ("南岸潮湾", "门楼与誓约庭", 2), ("门楼与誓约庭", "中央厅", 1),
    ("中央厅", "文契院", 1), ("中央厅", "白塔异象院", 1),
    ("中央厅", "铁工院", 1), ("中央厅", "星仪院", 1),
    ("文契院", "白塔异象院", 2), ("白塔异象院", "铁工院", 2),
    ("铁工院", "星仪院", 2), ("星仪院", "文契院", 2),
    ("中央厅", "教学图书馆", 1), ("教学图书馆", "文契院", 1),
    ("中央厅", "公共诊疗所", 1), ("公共诊疗所", "白塔异象院", 1),
    ("中央厅", "后勤院", 1), ("后勤院", "铁工院", 1),
    ("中央厅", "训练院", 1), ("训练院", "星仪院", 1),
    ("中央厅", "生活院", 1), ("生活院", "后勤院", 1),
    ("生活院", "公共诊疗所", 1), ("生活院", "公共娱乐室", 1),
    ("公共娱乐室", "后勤院", 1), ("生活院", "公共浴室与温泉池", 1),
    ("教学图书馆", "美术工房与冬园", 1), ("美术工房与冬园", "星仪院", 1),
    ("训练院", "中央草场与露天运动场", 2), ("中央草场与露天运动场", "西湾沙滩与海浴场", 2),
    ("中央草场与露天运动场", "生活院", 2), ("中央草场与露天运动场", "种植梯田与温室", 2),
    ("南岸潮湾", "西湾沙滩与海浴场", 2), ("西湾沙滩与海浴场", "中央草场与露天运动场", 2),
    ("种植梯田与温室", "上水库与水务站", 2), ("上水库与水务站", "林地石场与潮间观测站", 3),
    ("林地石场与潮间观测站", "海崖信号塔", 3), ("海崖信号塔", "环岛步道与观景庭园", 2),
    ("环岛步道与观景庭园", "门楼与誓约庭", 2),
    ("文契院", "文献工房", 1), ("文契院", "声纹与记录室", 1),
    ("文契院", "契约审议室", 1), ("文献工房", "名录库", 3, "名录库"),
    ("星仪院", "星盘讲堂", 1), ("星仪院", "共鸣厅", 1),
    ("星仪院", "灵体观察室", 1), ("星仪院", "沉静室与心理记录室", 1),
    ("星盘讲堂", "阵法基座室", 3, "阵法基座室"),
    ("铁工院", "物理测定室", 1), ("铁工院", "精密工房", 1),
    ("铁工院", "电气实验室", 1), ("电气实验室", "动力总站", 2),
    ("动力总站", "机械联锁室", 3, "机械联锁室"),
    ("白塔异象院", "化学实验室", 1), ("白塔异象院", "博物与生态室", 1),
    ("白塔异象院", "手术与解剖室", 1), ("白塔异象院", "摄影与早期X光室", 1),
    ("博物与生态室", "污染标本库", 3), ("手术与解剖室", "学园长隔离室", 3, "学园长隔离室"),
    ("上水库与水务站", "勤务环廊", 3), ("后勤院", "勤务环廊", 3),
    ("白塔异象院", "勤务环廊", 3), ("动力总站", "勤务环廊", 3),
    ("勤务环廊", "总仓与回收间", 2), ("勤务环廊", "废物处理站", 2),
    ("废物处理站", "总仓与回收间", 2),
    ("名录库", "四锚封印廊", 1), ("阵法基座室", "四锚封印廊", 1),
    ("机械联锁室", "四锚封印廊", 1), ("学园长隔离室", "四锚封印廊", 1),
    ("四锚封印廊", "源质反应炉", 2, "源质反应炉"),
    ("源质反应炉", "核心隔离舱", 2, "核心隔离舱"),
    ("四锚封印廊", "核心隔离舱", 3, "核心隔离舱"),
)


class AcademyGame:
    """群像 NPC 自主行动与玩家指令共用的学园内容运行时。"""

    def __init__(self, autonomous=True):
        self.autonomous = autonomous
        self.reset()

    def reset(self):
        self.world = WorldSpace("灰潮岛异常感知学园")
        self.env, self.rules, self.timeline = RuleEnvironment(), RuleSystem(), Timeline()
        self.words = KotodamaSystem(self.env, self.rules)
        self.log = ["记忆封存后的第一声钟响。恶魔通过中央厅广播宣布禁闭游戏开始。"]
        self.ended = self.won = False
        self.ending = ""
        self.round = 0
        self.history_archive = Entity("被封存的学园历史", self.world, {"Archive": 1})
        self.rule_archive = Entity("规则与契约", self.world, {"Archive": 1})
        self.team_inventory = Entity("共同调查箱", self.world, {"Container": 1, "Shared": 1})
        self.scenes = {}
        self.passages = []
        self.characters = {}
        self.souls = {}
        self.history = []
        self.memory_carriers = []
        self.seal_components = []
        self.requests = []
        self.rule_patches = []
        self._build_map()
        self._build_cast()
        self._build_faculty_and_demon()
        self._build_histories()
        self._build_rules()
        self._build_kotodama()
        self._build_decision_trees()
        self.runtime = ActionRuntime(
            self.timeline, self.env, self.rules, self.words,
            event_parent=self.world, witnesses=self._witnesses,
        )
        self.runtime.patches.extend(self.rule_patches)
        if self.autonomous:
            for index, (role, actor) in enumerate(self.characters.items()):
                if actor is self.player:
                    continue
                self.runtime.activate(
                    actor, self.souls[role], delay=20 + index * 2,
                    context={"source": actor, "idle_cost": 80},
                    utility=lambda intent, actor=actor: self._npc_utility(actor, intent),
                )

    def _build_map(self):
        restricted = {"名录库", "阵法基座室", "机械联锁室", "学园长隔离室", "源质反应炉", "核心隔离舱"}
        hazardous = {"灵体观察室", "污染标本库", "勤务环廊", "废物处理站", "四锚封印廊", "源质反应炉", "核心隔离舱"}
        for layer, names in SCENE_GROUPS.items():
            for name in names:
                tags = {"Scene": 1, "Discovered": 1}
                if name in restricted:
                    tags["Locked"] = 1
                if name in hazardous:
                    tags["Hazard"] = 2 if name not in {"源质反应炉", "核心隔离舱"} else 4
                self.scenes[name] = Entity(name, self.world, tags, properties={"layer": layer, "progress": 0})
        for index, raw in enumerate(CONNECTIONS, 1):
            left, right, cost, *gate_name = raw
            gate = self.scenes[gate_name[0]] if gate_name else None

            def condition(_from, _actor, env, gate=gate):
                return gate is None or not gate.has("Locked") or bool(env and env.evaluate(E("unlocked", gate)))

            passage = Passage(
                f"通道{index}：{left}—{right}", self.scenes[left], self.scenes[right], cost,
                tags={"Restricted": 1} if gate else None, condition=condition,
            )
            self.world.add_passage(passage)
            self.passages.append(passage)

    def _build_cast(self):
        aspect_profiles = {
            "侦探": {"Mind": 3, "Law": 1}, "诗人": {"Name": 2, "Desire": 2},
            "音乐家": {"Mind": 3, "Preservation": 2}, "博物学家": {"Preservation": 3, "Form": 1},
            "机械工程师": {"Form": 3, "Preservation": 1}, "主厨": {"Preservation": 3, "Bond": 2},
            "考古学家": {"Vestige": 3, "Name": 1}, "神偷": {"Threshold": 3, "Mind": 1},
            "律师": {"Law": 3, "Name": 2}, "职业棋手": {"Mind": 4},
            "化学家": {"Transmutation": 3, "Form": 1}, "神射手": {"Strife": 3, "Threshold": 1},
            "舞蹈艺术家": {"Form": 2, "Bond": 2}, "画家": {"Mind": 2, "Vestige": 2},
            "建筑师": {"Form": 2, "Threshold": 2}, "临床医生": {"Preservation": 3, "Death": 1},
        }
        for role, name, occult, home, action, operation, team_role in CHARACTER_SPECS:
            actor = Character(
                f"{name}·{role}", self.scenes[home],
                {"Student": 1, "Health": 6, "Panic": 1, "Occult": occult},
            )
            actor.properties.update(
                role=role, personal_name=name, home=self.scenes[home],
                profession_action=action, operation=operation, team_role=team_role,
            )
            soul = Soul(
                f"{name}的心识", self.world,
                {"Stability": 8, "Identity": 6, "Law": 1, "Name": 1, **aspect_profiles[role]},
            )
            actor.bind(soul)
            self.characters[role], self.souls[role] = actor, soul
            self.env.alias(role, actor)
            self.env.alias(name, actor)
            self.env.assert_fact(E("located_at", actor, actor.parent))
            soul.observe(E("located_at", actor, actor.parent))
        self.player = self.characters["侦探"]
        self.player_soul = self.souls["侦探"]

    def _build_faculty_and_demon(self):
        mentor_specs = (
            ("鸦羽静文", "言灵与文书学导师", "契约担保者"),
            ("星见慈", "阵法与仪式学导师", "能量封印核心"),
            ("铁堂轰", "动力工程与自动化导师", "物理阻隔控制者"),
            ("御门玄理", "异象与医学导师／学园长", "事故第一目击者"),
        )
        self.mentors = []
        for name, discipline, duty in mentor_specs:
            mentor = Entity(
                f"{name}·{discipline}", self.history_archive,
                {"Mentor": 1, "Vestige": 3, "Authority": 3},
                properties={"discipline": discipline, "duty": duty, "status": "牺牲或成为封印载体"},
            )
            self.mentors.append(mentor)
        self.demon = Character(
            "校内代管者", self.scenes["核心隔离舱"],
            {"Demon": 1, "Authority": 5, "Name": 4, "Law": 4, "Desire": 4},
        )
        self.demon.designate("恶魔", 1)
        self.demon_soul = Soul("代管者意志", self.world, {"Stability": 12, "Mind": 4, "Law": 4})
        self.demon.bind(self.demon_soul)
        self.env.alias("恶魔", self.demon)
        self.env.assert_fact(E("confined", self.demon, self.world))

        anchors = tuple(self.scenes[name] for name in ("名录库", "阵法基座室", "机械联锁室", "学园长隔离室"))
        covenant = Entity(
            "禁闭游戏契约", self.rule_archive,
            {"Covenant": 1, "Active": 1, "DyingWord": 1},
            properties={"active": True, "parties": (self.demon, *self.mentors), "fired": True},
        )
        forbidden = [F("leave", self.demon, self.world), F("break_seal", self.demon, self.world)]
        forbidden.extend(F(op, self.demon, student) for student in self.characters.values() for op in ("kill", "possess", "control"))
        forbidden.extend(F("damage", self.demon, anchor) for anchor in anchors)
        for effect in forbidden:
            covenant.attach("HardConstraint", Rule(E("true"), (Directive(Mode.FORBID, effect),), "三导师临终契约"))
        for mentor in self.mentors[:3]:
            covenant.relate("GuaranteedBy", mentor)
        self.words.contracts.append(covenant)
        self.demon_covenant = covenant

    def _build_histories(self):
        for spec in STORY_SPECS:
            event = Entity(
                spec["title"], self.history_archive,
                {"History": 1, "Truth": 1, "SealedMemory": 1},
                properties={"scene": self.scenes[spec["scene"]], "summary": spec["summary"]},
            )
            event.relate("OccurredAt", self.scenes[spec["scene"]])
            self.history.append(event)
            for role, (material_name, medium, claim) in spec["memories"].items():
                actor = self.characters[role]
                memory = Knowledge(
                    f"{actor.name}关于“{event.name}”的记忆", event, claim,
                    subject=event, source=actor, tags={"Memory": 1, "Subjective": 1, "Sealed": 1},
                )
                memory.relate("Witness", actor)
                carrier = InformationCarrier(
                    material_name, medium, self.scenes[spec["scene"]], memory,
                    creator=actor, tags={"MemoryCarrier": 1, "Vestige": 2, "Hidden": 1},
                )
                carrier.relate("RecordOf", event)
                carrier.relate("Owner", actor)
                self.memory_carriers.append(carrier)
                event.relate("Witness", actor)
            for left_role, right_role, left_kind, right_kind, strength in spec["bonds"]:
                left, right = self.characters[left_role], self.characters[right_role]
                left.relate(left_kind, right, strength, source=event)
                right.relate(right_kind, left, strength, source=event)
                left.relate("PastBond", right, strength, source=event)
                right.relate("PastBond", left, strength, source=event)

        accident = Entity(
            "源质反应炉泄露与临终契约", self.history_archive,
            {"History": 1, "Truth": 1, "SealedMemory": 1, "FinalHistory": 1},
            properties={
                "scene": self.scenes["源质反应炉"],
                "summary": "苏醒周期校准误差使言灵定义漏过一个时空节点。反应炉破裂后，学园长留在隔离舱拖延恶魔，其余三导师以存在为质押建立禁闭游戏契约。",
            },
        )
        accident.relate("OccurredAt", self.scenes["源质反应炉"])
        self.history.append(accident)
        witness_groups = tuple(tuple(self.characters.values())[index:index + 4] for index in range(0, 16, 4))
        sites = ("中央厅", "阵法基座室", "动力总站", "学园长隔离室")
        names = ("停摆的总课表", "星盘夹层合影", "焦黑联锁手记", "沾血解剖日志")
        claims = (
            "广播先报出错误校准时刻，随后才响起反应炉破裂声。",
            "阵法导师以石化肉身承受主阵，要求学员不要把她从基座移走。",
            "工程导师把生物指纹熔入联锁，切断恶魔通向地表的机械权限。",
            "学园长知道校准结果存在误差，仍批准了源质提取实验。",
        )
        for group, site, name, claim in zip(witness_groups, sites, names, claims):
            first = group[0]
            memory = Knowledge(
                f"关于事故的共同记忆：{name}", accident, claim,
                subject=accident, source=first, tags={"Memory": 1, "Subjective": 1, "Sealed": 1},
            )
            for actor in group:
                memory.relate("Witness", actor)
                accident.relate("Witness", actor)
            carrier = InformationCarrier(
                name, "Record", self.scenes[site], memory, creator=first,
                tags={"MemoryCarrier": 1, "Vestige": 3, "Hidden": 1},
            )
            carrier.relate("RecordOf", accident)
            self.memory_carriers.append(carrier)

    def _build_rules(self):
        rules = (
            ("恶魔不得直接伤害学员", Rule(E("true"), (Directive(Mode.FORBID, F("bargain_life", "actor", "demon")),), "广播第一条")),
            ("封印层不得单独行动", Rule(E("state", "actor", "InSealLayer"), (Directive(Mode.FORBID, F("move_alone", "actor", "seal_layer")),), "广播第二条")),
            ("规则交易必须留下书面见证", Rule(E("true"), (Directive(Mode.PREFER, F("record_bargain", "actor", "demon")),), "广播第三条")),
        )
        self.public_rules = []
        for title, rule in rules:
            carrier = InformationCarrier(title, "Broadcast", self.scenes["中央厅"], tags={"Guarantee": 2, "Law": 2})
            publication = RulePublication(rule, self.demon, carrier=carrier, scope=self.world, version=1)
            publication.name = title
            self.public_rules.append(publication)
            for role, soul in self.souls.items():
                belief = soul.receive(publication, confidence=2)
                soul.understand(rule)
                if role not in {"神偷", "职业棋手", "化学家"}:
                    soul.accept(rule)
                else:
                    soul.reject(rule)

    def _build_kotodama(self):
        self.tuning_fork = Entity(
            "黄铜共鸣音叉", self.team_inventory,
            {"Tool": 1, "Instrument": 1, "Mind": 2, "Bond": 2, "Preservation": 1},
        )
        self.threshold_token = Entity(
            "誓约庭重型钥匙", self.team_inventory,
            {"Tool": 1, "Authority": 2, "Name": 2, "Law": 2, "Threshold": 2},
        )
        self.rule_ink = Entity(
            "言灵导师的红墨水", self.team_inventory,
            {"Tool": 1, "Authority": 3, "Name": 2, "Law": 3},
            properties={"uses": 4},
        )
        self.calm_word = Recipe(
            "镇心三拍",
            slots={"focus": {"tags": {"Instrument": 1}, "held": True}},
            thresholds={"Mind": 3, "Bond": 2, "Preservation": 1},
            requirements=(lambda scope: scope["target"].tag("Panic") >= 2 and self._same_scene(scope["actor"], scope["target"]),),
            effects=(F("change", "target", "Panic", -2),),
            parent=self.rule_archive, tags={"Incantation": 1, "Kotodama": 1},
        )
        self.open_word = Recipe(
            "门槛正名",
            slots={"token": {"tags": {"Authority": 1, "Threshold": 1}, "held": True}},
            thresholds={"Name": 3, "Law": 2, "Threshold": 2},
            requirements=(lambda scope: scope["target"].has("Locked") and self._adjacent(scope["actor"].parent, scope["target"]),),
            effects=(F("change", "target", "Locked", -1), F("assert", E("unlocked", "target"))),
            parent=self.rule_archive, tags={"Incantation": 1, "Kotodama": 1},
        )
        self.word_recipes = (self.calm_word, self.open_word)

        true_name = Entity(
            "逆写第七码真名结构", self.scenes["名录库"],
            {"SealComponent": 1, "TrueName": 1, "Name": 5, "Law": 2, "Vestige": 2},
            properties={"true_text": "阿斯塔罗特·逆写第七码"},
        )
        true_name.relate("TrueNameOf", self.demon, 2)
        seal_map = Entity(
            "四院闭合封印阵图", self.scenes["阵法基座室"],
            {"SealComponent": 1, "SealPattern": 1, "Form": 2, "Law": 2, "Threshold": 3},
        )
        interlock = Entity(
            "导师指纹机械联锁", self.scenes["机械联锁室"],
            {"SealComponent": 1, "IsolationMechanism": 1, "Form": 4, "Preservation": 2},
        )
        vessel = Entity(
            "铅玻璃源质安全载体", self.scenes["学园长隔离室"],
            {"SealComponent": 1, "SafeVessel": 1, "Preservation": 4, "Threshold": 2},
        )
        self.seal_components.extend((true_name, seal_map, interlock, vessel))
        self.final_recipe = Recipe(
            "四院真名再封印",
            slots={
                "true_name": {"tags": {"TrueName": 1}, "relation": "TrueNameOf", "target": "target", "held": True},
                "seal_map": {"tags": {"SealPattern": 1}, "held": True},
                "interlock": {"tags": {"IsolationMechanism": 1}, "held": True},
                "vessel": {"tags": {"SafeVessel": 1}, "held": True},
            },
            thresholds={"Name": 5, "Law": 4, "Form": 5, "Threshold": 4, "Preservation": 4},
            requirements=(lambda scope: scope["actor"].parent is self.scenes["核心隔离舱"],),
            steps=tuple({
                "name": name,
                "costs": (F("spend", "speaker", "Stability", 1, 1),),
                "effects": (F("assert", E("seal_step", name)),),
            } for name in ("校验真名", "复位阵图", "闭合联锁", "注入安全载体")),
            effects=(F("remove", "target"), F("assert", E("sealed", "target"))),
            constraints=(Rule(E("sealed", "target"), (Directive(Mode.FORBID, F("return", "target", "world")),)),),
            parent=self.rule_archive,
            tags={"Rite": 1, "Covenant": 1, "Seal": 1},
        )
        self.active_final_rite = None

    def _build_decision_trees(self):
        for role, actor in self.characters.items():
            soul = self.souls[role]
            soul.install_step(self._memory_step, 80, when=lambda _owner, _context, actor=actor: bool(self._memory_here(actor)), source=actor)
            soul.install_step(self._assist_step, 70, when=lambda _owner, _context, actor=actor: bool(self._request_here(actor)), source=actor)
            soul.install_step(self._profession_step, 50, source=actor)
            soul.install_step(self._travel_step, 20, when=lambda _owner, _context, actor=actor: self._travel_goal(actor) is not None, source=actor)
            soul.install_step(self._rest_step, 0, source=actor)
        self.demon_soul.install_step(self._demon_step, 20, source=self.demon)

    def _same_scene(self, left, right):
        return left.parent is right.parent and left.parent is not None and left.parent.has("Scene")

    def _adjacent(self, left, right):
        return right in self.world.links.get(left, {})

    def _witnesses(self, event, _context):
        actor = event.get("actor")
        if not isinstance(actor, Entity) or not actor.parent:
            return ()
        event.relate("OccurredAt", actor.parent)
        return tuple(soul for role, soul in self.souls.items() if self.characters[role].parent is actor.parent)

    def _memory_here(self, actor):
        soul = actor.souls[0]
        return tuple(
            carrier for carrier in actor.parent.children
            if isinstance(carrier, Entity) and carrier.has("MemoryCarrier")
            and not soul.knows(carrier.related("RecordOf")[0].value, stage="understood")
        )

    def _request_here(self, actor):
        return next((request for request in self.requests if request.has("Active") and request.get("scene") is actor.parent and request.get("requester") is not actor), None)

    def _travel_goal(self, actor):
        soul = actor.souls[0]
        for carrier in self.memory_carriers:
            event = carrier.related("RecordOf")[0].value
            if not soul.knows(event, stage="understood") and carrier.parent and carrier.parent.has("Scene"):
                route = self.world.route(actor.parent, carrier.parent, actor, self.env)
                if route and len(route.nodes) > 1:
                    return route.nodes[1]
        home = actor.get("home")
        if home is not actor.parent:
            route = self.world.route(actor.parent, home, actor, self.env)
            if route and len(route.nodes) > 1:
                return route.nodes[1]
        return None

    def _memory_step(self, soul, actor, _env, _context):
        carrier = next(iter(self._memory_here(actor)), None)
        return self._intent(actor, "recover_memory", carrier, 45, lambda: self._recover_now(actor, carrier), f"恢复记忆：{carrier.name}") if carrier else None

    def _assist_step(self, soul, actor, _env, _context):
        request = self._request_here(actor)
        if not request:
            return None

        def resolve():
            request.tags.pop("Active", None)
            request.change("Resolved", 1)
            request.relate("AnsweredBy", actor)
            request.get("scene").change("Progress", 1)
            self.log.append(f"{actor.name}回应了{request.get('requester').name}的协助请求。")

        return self._intent(actor, "assist", request, 55, resolve, "协助同伴")

    def _profession_step(self, soul, actor, _env, _context):
        scene = actor.parent
        if scene.tag("Hazard") > actor.tag("Occult") + scene.tag("Progress"):
            if not any(request.has("Active") and request.get("requester") is actor for request in self.requests):
                return self._intent(actor, "request_help", scene, 20, lambda: self._request_help_now(actor), "寻求协助")
            return None
        return self._profession_intent(actor)

    def _travel_step(self, soul, actor, _env, _context):
        goal = self._travel_goal(actor)
        return self._move_intent(actor, goal) if goal else None

    def _rest_step(self, soul, actor, _env, _context):
        return self._intent(
            actor, "rest", actor.parent, 70,
            lambda: (actor.change("Panic", -1) if actor.tag("Panic") else 0), "短暂休整",
        )

    def _demon_step(self, soul, actor, _env, _context):
        target = max(self.characters.values(), key=lambda item: item.tag("Panic"))
        return self._intent(actor, "offer_bargain", target, 90, lambda: self.log.append(f"恶魔向{target.name}提出一项措辞暧昧的交易。"), "诱导交易")

    def _npc_utility(self, actor, intent):
        base = {"recover_memory": 80, "assist": 65, "request_help": 60, actor.get("operation"): 45, "move": 30, "rest": 5}
        return base.get(intent.effect.op, 0) - intent.motion_cost / 10

    def _intent(self, actor, operation, target, cost, resolve, label, validate=lambda: True):
        return Intent(actor, actor.souls[0], F(operation, actor, target), cost, resolve, label, validate)

    def _move_intent(self, actor, destination):
        route = self.world.route(actor.parent, destination, actor, self.env) if destination else None
        if not route:
            return None
        start = actor.parent

        def resolve():
            self.env.retract(E("located_at", actor, start))
            actor.souls[0].unobserve(E("located_at", actor, start))
            actor.reparent(destination)
            self.env.assert_fact(E("located_at", actor, destination))
            actor.souls[0].observe(E("located_at", actor, destination))
            if destination.tag("Hazard") > actor.tag("Occult"):
                actor.change("Panic", destination.tag("Hazard") - actor.tag("Occult"))
            self.log.append(f"{actor.name}抵达{destination.name}。")

        return self._intent(actor, "move", destination, max(10, route.cost * 20), resolve, f"移动至{destination.name}")

    def move(self, actor, destination):
        intent = self._move_intent(actor, destination)
        return bool(intent and self._submit(intent, {"domains": (actor.parent, destination)}))

    def _recover_now(self, actor, carrier):
        claims = carrier.read(actor.souls[0])
        carrier.relate("RecoveredBy", actor)
        carrier.tags.pop("Hidden", None)
        actor.change("Panic", -1) if actor.tag("Panic") else None
        self.log.append(f"{actor.name}从《{carrier.name}》恢复了一段主观记忆。")
        return claims

    def recover_memory(self, actor, carrier):
        if carrier.parent is not actor.parent or not carrier.has("MemoryCarrier"):
            return False
        return self._submit(self._intent(actor, "recover_memory", carrier, 45, lambda: self._recover_now(actor, carrier), f"恢复记忆：{carrier.name}"))

    def _request_help_now(self, actor):
        request = Entity(
            f"{actor.name}的协助请求", self.world, {"HelpRequest": 1, "Active": 1},
            properties={"requester": actor, "scene": actor.parent, "need": actor.get("operation")},
        )
        request.relate("RequestedBy", actor)
        request.relate("Domain", actor.parent)
        self.requests.append(request)
        self.log.append(f"{actor.name}在{actor.parent.name}寻求协助。")
        return request

    def _profession_intent(self, actor):
        operation, scene = actor.get("operation"), actor.parent

        def resolve():
            scene.change("Progress", 1)
            self.env.assert_fact(E("worked", actor, scene, operation))
            match operation:
                case "recite":
                    for other in self.characters.values():
                        if other.parent is scene and other.tag("Panic"):
                            other.change("Panic", -1)
                case "listen":
                    scene.change("Hazard", -1) if scene.tag("Hazard") else None
                case "sample":
                    Entity(f"{scene.name}安全样本", self.team_inventory, {"Sample": 1, "Preservation": 2, "Form": 1})
                case "repair" | "reinforce":
                    scene.change("Damaged", -1) if scene.tag("Damaged") else scene.change("Operational", 1)
                case "ration":
                    actor.change("Panic", -1) if actor.tag("Panic") else None
                case "decipher" | "reconstruct" | "question":
                    for item in scene.children:
                        if isinstance(item, Entity):
                            item.tags.pop("Hidden", None)
                case "infiltrate":
                    for neighbor in self.world.links.get(scene, {}):
                        if neighbor.has("Locked"):
                            neighbor.tags.pop("Locked", None)
                            self.env.assert_fact(E("unlocked", neighbor))
                            break
                case "litigate":
                    actor.souls[0].change("Law", 1)
                case "strategize":
                    self.demon.change("Delayed", 1)
                case "analyze":
                    scene.change("Hazard", -1) if scene.tag("Hazard") else None
                case "suppress" | "dance_path":
                    scene.change("SafeRoute", 1)
                case "treat":
                    patient = min((item for item in self.characters.values() if item.parent is scene), key=lambda item: item.tag("Health"), default=actor)
                    patient.change("Health", 1)
            self.log.append(f"{actor.name}在{scene.name}执行“{actor.get('profession_action')}”。")

        return self._intent(actor, operation, scene, 55, resolve, actor.get("profession_action"))

    def profession_action(self, actor):
        return self._submit(self._profession_intent(actor), {"domains": (actor.parent,)})

    def collect(self, actor, item):
        if item.parent is not actor.parent or not item.has("SealComponent"):
            return False

        def resolve():
            item.reparent(self.team_inventory)
            item.relate("RecoveredBy", actor)
            self.log.append(f"{actor.name}取得封印部件《{item.name}》。")

        return self._submit(self._intent(actor, "collect", item, 35, resolve, f"取得{item.name}"))

    def invoke_word(self, actor, recipe, target):
        inputs = {"focus": self.tuning_fork} if recipe is self.calm_word else {"token": self.threshold_token}

        def resolve():
            result = self.words.invoke(
                recipe, actor.souls[0], inputs, target=target,
                context={"actor": actor, "holder": self.team_inventory, "world": self.world},
            )
            if result:
                self.log.append(f"{actor.name}施行言灵“{recipe.name}”。")
            else:
                self.log.append(f"言灵“{recipe.name}”未成立：{self.words.last_failure}。")
            return result

        return self._submit(self._intent(actor, "invoke_word", target, 65, resolve, f"言灵：{recipe.name}"), {"domains": (actor.parent, target)})

    def establish_rule(self, actor, witness, operation, subject, target, scope):
        if actor.parent is not self.scenes["契约审议室"] or witness.parent is not actor.parent or self.rule_ink.get("uses", 0) <= 0:
            self.log.append("规则设立需要在契约审议室由另一名在场者见证，并消耗红墨水。")
            return False
        rule = Rule(E("true"), (Directive(Mode.FORBID, F(operation, subject, target)),), f"{actor.name}的规则设立")

        def resolve():
            self.rule_ink.set("uses", self.rule_ink.get("uses") - 1)
            carrier = InformationCarrier(
                f"设立规则：禁止{subject.name}{operation}{getattr(target, 'name', target)}",
                "Manuscript", self.rule_archive, tags={"Law": 2, "Guarantee": 2},
            )
            publication = RulePublication(rule, actor, carrier=carrier, scope=scope, version=self.env.version)
            publication.relate("Witness", witness)
            patch = RulePatch(publication.name, rule, self.rule_archive, publication, scope, priority=actor.souls[0].tag("Law"))
            patch.relate("Witness", witness)
            self.rule_patches.append(patch)
            self.runtime.patches.append(patch)
            for participant, confidence in ((actor, 3), (witness, 2)):
                soul = participant.souls[0]
                soul.receive(publication, confidence)
                soul.understand(rule)
                soul.accept(rule)
            self.log.append(f"{actor.name}与{witness.name}在{scope.name}设立了一条客观规则。")
            return patch

        intent = self._intent(actor, "establish_rule", scope, 90, resolve, "设立局部规则")
        return self._submit(intent, {"domains": (scope,)})

    def begin_final_seal(self, actor):
        if any(item.parent is not self.team_inventory for item in self.seal_components):
            self.log.append("最终封印需要四座研究院的全部封印部件。")
            return False
        inputs = dict(zip(("true_name", "seal_map", "interlock", "vessel"), self.seal_components))

        def resolve():
            self.active_final_rite = self.words.begin(
                self.final_recipe, actor.souls[0], inputs, target=self.demon,
                context={"actor": actor, "holder": self.team_inventory, "world": self.world},
            )
            if self.active_final_rite:
                self.log.append("四院真名再封印已经建立为持续 Rite。")
            else:
                self.log.append(f"最终仪式无法开始：{self.words.last_failure}。")
            return self.active_final_rite

        return self._submit(self._intent(actor, "begin_rite", self.demon, 80, resolve, "开始最终封印"), {"domains": (self.scenes["核心隔离舱"],)})

    def advance_final_seal(self, actor):
        rite = self.active_final_rite
        if not rite or rite.has("Completed"):
            return False

        def resolve():
            result = self.words.advance(rite)
            if result and result.has("Covenant"):
                self.words.trigger()
                self._check_end()
            self.log.append(f"最终封印推进至：{rite.get('phase')}。")
            return result

        return self._submit(self._intent(actor, "advance_rite", rite, 70, resolve, "推进最终封印"), {"domains": (actor.parent,)})

    def _submit(self, intent, context=None):
        return self.runtime.submit(
            intent, context=context,
            on_review=lambda allowed, decision: self.log.append(
                f"{intent.label}被客观规则阻止。" if not allowed else f"决策评估：{decision.reason}。"
            ),
        )

    def wait(self, amount=60):
        target = self.timeline.now + amount
        while self.timeline.entries and self.timeline.entries[0][0] <= target and not self.ended:
            self.timeline.next()
        self.round += 1
        return True

    def known_memories(self, soul=None):
        soul = soul or self.player_soul
        return tuple(
            knowledge for knowledge in soul.children
            if knowledge.has("Knowledge") and knowledge.get("subject") in self.history
        )

    def local_people(self, actor=None):
        actor = actor or self.player
        return tuple(item for item in self.characters.values() if item.parent is actor.parent)

    def local_records(self, actor=None):
        actor = actor or self.player
        return tuple(item for item in actor.parent.children if isinstance(item, Entity) and item.has("MemoryCarrier"))

    def local_components(self, actor=None):
        actor = actor or self.player
        return tuple(item for item in actor.parent.children if isinstance(item, Entity) and item.has("SealComponent"))

    def reachable_scenes(self, actor=None):
        actor = actor or self.player
        return tuple(scene for scene in self.scenes.values() if scene is not actor.parent and self.world.route(actor.parent, scene, actor, self.env))

    def locked_neighbors(self, actor=None):
        actor = actor or self.player
        return tuple(scene for scene in self.world.links.get(actor.parent, {}) if scene.has("Locked"))

    def _check_end(self):
        if self.demon.parent is None and self.env.evaluate(E("sealed", self.demon)):
            self.won = self.ended = True
            self.ending = "四院防卫重新闭合。恶魔真名被安全载体收束，学员保留已恢复的记忆。"

    def smoke(self):
        if not (len(self.characters) == 16 and len(self.scenes) >= 40 and len(self.passages) >= 60):
            raise RuntimeError("ACADEMY_SMOKE_FAIL: content counts")
        if any(not actor.related("PastBond") for actor in self.characters.values()):
            # 有些角色只在共同事故中相连；补查是否至少作为历史见证者进入内容。
            if any(not any(event.related("Witness", actor) for event in self.history) for actor in self.characters.values()):
                raise RuntimeError("ACADEMY_SMOKE_FAIL: isolated character")
        if any(not soul.entries("Step") for soul in self.souls.values()):
            raise RuntimeError("ACADEMY_SMOKE_FAIL: decision tree")

        actor = self.player
        first_record = self.memory_carriers[0]
        actor.reparent(first_record.parent)
        if not self.recover_memory(actor, first_record) or not self.known_memories():
            raise RuntimeError("ACADEMY_SMOKE_FAIL: memory")

        actor.reparent(self.scenes["契约审议室"])
        witness = self.characters["律师"]
        witness.reparent(actor.parent)
        if not self.establish_rule(actor, witness, "move", self.demon, self.scenes["中央厅"], self.scenes["中央厅"]):
            raise RuntimeError("ACADEMY_SMOKE_FAIL: rule establishment")
        blocked = self._intent(self.demon, "move", self.scenes["中央厅"], 1, lambda: None, "受禁移动")
        allowed, _ = self.runtime.review(blocked, {"domains": (self.scenes["中央厅"],)})
        if allowed:
            raise RuntimeError("ACADEMY_SMOKE_FAIL: rule patch")

        target = self.characters["诗人"]
        target.reparent(actor.parent)
        target.change("Panic", 2)
        if not self.invoke_word(actor, self.calm_word, target) or target.tag("Panic") != 1:
            raise RuntimeError("ACADEMY_SMOKE_FAIL: kotodama")

        for scene in self.scenes.values():
            if scene.has("Locked"):
                scene.tags.pop("Locked", None)
                self.env.assert_fact(E("unlocked", scene))
        for component in self.seal_components:
            actor.reparent(component.parent)
            if not self.collect(actor, component):
                raise RuntimeError("ACADEMY_SMOKE_FAIL: seal component")
        actor.reparent(self.scenes["核心隔离舱"])
        if not self.begin_final_seal(actor):
            raise RuntimeError("ACADEMY_SMOKE_FAIL: rite begin")
        while self.active_final_rite and not self.active_final_rite.has("Completed"):
            if not self.advance_final_seal(actor):
                raise RuntimeError(f"ACADEMY_SMOKE_FAIL: rite advance {self.words.last_failure}")
        self._check_end()
        if not self.won:
            raise RuntimeError("ACADEMY_SMOKE_FAIL: ending")
        return (
            f"ACADEMY_SMOKE_PASS characters={len(self.characters)} scenes={len(self.scenes)} "
            f"passages={len(self.passages)} histories={len(self.history)} memories={len(self.memory_carriers)} "
            f"rules={len(self.rule_patches)} rites={len(self.words.rites)}"
        )


# 正式运行时已迁移到独立内容模块。直接导入 academy_game 时，它会先读取本文件
# 的内容表；此处避开模块尚未完成定义的短暂阶段，随后由 academy_game 回填公开类。
import sys as _sys

_runtime_module = _sys.modules.get(f"{__package__}.academy_game")
if _runtime_module is None or hasattr(_runtime_module, "AcademyGame"):
    from .academy_game import AcademyGame


__all__ = ["AcademyGame", "ASPECTS", "CHARACTER_SPECS", "STORY_SPECS", "SCENE_GROUPS"]
