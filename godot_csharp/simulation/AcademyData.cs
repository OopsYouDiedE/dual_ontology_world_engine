using System.Collections.Generic;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;

namespace DualOntologyWorldEngine.Simulation;

/// <summary>
/// 学员角色初始配置规格记录。
/// </summary>
/// <remarks>
/// 声明学员的职业领域、角色姓名、神秘学与言灵造诣阶数、开局场景、专属技能名称与 ID，以及团队职能定位。
/// </remarks>
/// <param name="Profession">职业领域标识（如 "侦探"、"主厨"、"律师" 等）。</param>
/// <param name="Name">学员角色姓名。</param>
/// <param name="OccultDegree">神秘学与言灵造诣阶数（0 为未入门，阶数越高越擅长高阶仪式）。</param>
/// <param name="InitialScene">第 1 日开局初始化所处的学园场景名称。</param>
/// <param name="ActionName">职业专属标志性行动的中文叙事名称。</param>
/// <param name="ActionId">底层推演算子标识（如 "question"、"ration" 等）。</param>
/// <param name="RoleDescription">在学园封闭群体中的角色职责描述。</param>
public record CharacterSpec(
    string Profession,
    string Name,
    int OccultDegree,
    string InitialScene,
    string ActionName,
    string ActionId,
    string RoleDescription);

/// <summary>
/// 学园设施与重点交互地标规格记录。
/// </summary>
/// <remarks>
/// 定义分布在各场景中的功能设施、所附加的标签字典、环境叙事文本以及交互功能性指示（Affordance）。
/// </remarks>
/// <param name="Name">设施全称。</param>
/// <param name="SceneName">安装挂载的学园场景名称。</param>
/// <param name="Tags">设施所携带的物理与神秘学功能标签字典。</param>
/// <param name="Description">设施外观与状态的环境叙事描写。</param>
/// <param name="Affordance">交互功能标识（如 "Research"、"Treatment"、"Cooking" 等）。</param>
public record FacilitySpec(
    string Name,
    string SceneName,
    IReadOnlyDictionary<string, int> Tags,
    string Description,
    string Affordance);

/// <summary>
/// 职业专属领域知识规格记录。
/// </summary>
/// <remarks>
/// 记录学员开局掌握的专属领域学术或实战学识，包含知识实体名称、理论阐述与底层断言命题集合。
/// </remarks>
/// <param name="Profession">所属职业标识。</param>
/// <param name="KnowledgeName">知识实体命名。</param>
/// <param name="Description">知识体系的理论概述与叙事阐述。</param>
/// <param name="Claims">知识所包含的形式化底层命题表达式标识列表。</param>
public record DomainKnowledgeSpec(
    string Profession,
    string KnowledgeName,
    string Description,
    string[] Claims);

/// <summary>
/// 异常感知学园五日推演的静态场景、角色配置与知识库数据源。
/// </summary>
/// <remarks>
/// 集中定义十六位学员的初始职业规格、源质倾向分布（AspectProfiles）、学园双向通道拓扑图（Connections）、
/// 各场景功能设施（Facilities）以及专属领域知识（DomainKnowledges）。
/// </remarks>
public static class AcademyData
{
    /// <summary>
    /// 十六位学员的初始配置规格只读列表。
    /// </summary>
    public static readonly IReadOnlyList<CharacterSpec> CharacterSpecs = new List<CharacterSpec>
    {
        new("侦探", "雾岛律", 1, "中央厅", "矛盾质询", "question", "临时协调者"),
        new("诗人", "鸣海澄", 2, "生活院", "即兴赋诗", "recite", "仪式咏唱者"),
        new("音乐家", "久世弦", 3, "共鸣厅", "声学测听", "listen", "声学侦察者"),
        new("博物学家", "野野宫蕾", 3, "博物与生态室", "活体样本提取", "sample", "素材采集者"),
        new("机械工程师", "真壁铸", 2, "动力总站", "构件快修", "repair", "动力设施负责人"),
        new("主厨", "榊火澄", 1, "后勤院", "特制配给", "ration", "后勤核心"),
        new("考古学家", "冬城史", 2, "教学图书馆", "古文字破译", "decipher", "遗迹解码者"),
        new("神偷", "月见里鸢", 2, "门楼与誓约庭", "无声潜入", "infiltrate", "游击前哨"),
        new("律师", "御影章", 1, "契约审议室", "规则诉讼", "litigate", "规则仲裁者"),
        new("职业棋手", "白峰局", 0, "公共娱乐室", "态势弃子", "strategize", "资源调度核心"),
        new("化学家", "赤星砾", 1, "化学实验室", "物质裂解", "analyze", "物质分析官"),
        new("神射手", "苍井砥", 1, "训练院", "视线封锁", "suppress", "通道警戒哨"),
        new("舞蹈艺术家", "绯宫巡", 2, "中央草场与露天运动场", "旋回步法", "dance_path", "仪式节拍主导者"),
        new("画家", "青栖绘", 2, "美术工房与冬园", "画层剥离", "reconstruct", "视觉情报记录者"),
        new("建筑师", "方城础", 2, "星盘讲堂", "局部加固", "reinforce", "空间路线规划官"),
        new("临床医生", "灰原朔", 2, "公共诊疗所", "应急缝合", "treat", "战场医师")
    };

    /// <summary>
    /// 各职业主体的象征源质资质（Aspect）倾向配置字典。
    /// </summary>
    /// <remarks>
    /// 决定角色在特定超自然领域的擅长方向、心智抵抗力与言灵共鸣强度。
    /// 例如侦探擅长心智（Mind）与法则（Law），神偷擅长阈限（Threshold），考古学家擅长残迹（Vestige）。
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> AspectProfiles =
        new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            ["侦探"] = new Dictionary<string, int> { ["Mind"] = 3, ["Law"] = 1 },
            ["诗人"] = new Dictionary<string, int> { ["Name"] = 2, ["Desire"] = 2 },
            ["音乐家"] = new Dictionary<string, int> { ["Mind"] = 3, ["Preservation"] = 2 },
            ["博物学家"] = new Dictionary<string, int> { ["Preservation"] = 3, ["Form"] = 1 },
            ["机械工程师"] = new Dictionary<string, int> { ["Form"] = 3, ["Preservation"] = 1 },
            ["主厨"] = new Dictionary<string, int> { ["Preservation"] = 3, ["Bond"] = 2 },
            ["考古学家"] = new Dictionary<string, int> { ["Vestige"] = 3, ["Name"] = 1 },
            ["神偷"] = new Dictionary<string, int> { ["Threshold"] = 3, ["Mind"] = 1 },
            ["律师"] = new Dictionary<string, int> { ["Law"] = 3, ["Name"] = 2 },
            ["职业棋手"] = new Dictionary<string, int> { ["Mind"] = 4 },
            ["化学家"] = new Dictionary<string, int> { ["Transmutation"] = 3, ["Form"] = 1 },
            ["神射手"] = new Dictionary<string, int> { ["Strife"] = 3, ["Threshold"] = 1 },
            ["舞蹈艺术家"] = new Dictionary<string, int> { ["Form"] = 2, ["Bond"] = 2 },
            ["画家"] = new Dictionary<string, int> { ["Mind"] = 2, ["Vestige"] = 2 },
            ["建筑师"] = new Dictionary<string, int> { ["Form"] = 2, ["Threshold"] = 2 },
            ["临床医生"] = new Dictionary<string, int> { ["Preservation"] = 3, ["Death"] = 1 }
        };

    /// <summary>
    /// 学园各场景之间的双向拓扑通道连接定义列表。
    /// </summary>
    /// <remarks>
    /// 每一项二元组定义了两个房间场景实体之间的双向通道连通性。
    /// 在初始化时自动生成对应的 <see cref="Core.Passage"/> 拓扑连接实例。
    /// </remarks>
    public static readonly (string From, string To)[] Connections =
    [
        ("中央厅", "契约审议室"),
        ("中央厅", "教学图书馆"),
        ("中央厅", "公共诊疗所"),
        ("中央厅", "后勤院"),
        ("中央厅", "训练院"),
        ("中央厅", "生活院"),
        ("中央厅", "门楼与誓约庭"),
        ("中央厅", "公共娱乐室"),
        ("门楼与誓约庭", "中央草场与露天运动场"),
        ("教学图书馆", "美术工房与冬园"),
        ("契约审议室", "教学图书馆"),
        ("公共诊疗所", "化学实验室"),
        ("后勤院", "动力总站"),
        ("训练院", "星盘讲堂"),
        ("星盘讲堂", "共鸣厅"),
        ("化学实验室", "博物与生态室"),
        ("动力总站", "星盘讲堂")
    ];

    /// <summary>
    /// 全校各场景中的功能性设施与危险设施规格列表。
    /// </summary>
    /// <remarks>
    /// 包含各设施的名称、所属场景、标签、叙事描写以及交互功能指示（Affordance）。
    /// </remarks>
    public static readonly IReadOnlyList<FacilitySpec> Facilities = new List<FacilitySpec>
    {
        new("古籍研读长桌与档案架", "教学图书馆", new Dictionary<string, int> { ["Facility"] = 1, ["Researchable"] = 1, ["Library"] = 1 }, "存有学园创立历史与神秘学契约典籍的古朴木桌", "Research"),
        new("古代审议石板", "契约审议室", new Dictionary<string, int> { ["Facility"] = 1, ["Researchable"] = 1, ["Law"] = 1, ["Anomaly"] = 1 }, "铭刻着规范判例与因果约束词条的黑曜石基台", "Deliberation"),
        new("高压蒸汽管网与联锁暗阀", "动力总站", new Dictionary<string, int> { ["Facility"] = 1, ["Maintenance"] = 1, ["Danger"] = 2 }, "嗡嗡轰鸣的供能加压管道，需要定期安全检视", "Maintenance"),
        new("五金构件快修台", "动力总站", new Dictionary<string, int> { ["Facility"] = 1, ["Maintenance"] = 1, ["Tools"] = 1 }, "摆放着扳手、仪表与备用齿轮的整备工作台", "Repair"),
        new("试剂蒸馏与分析反应台", "化学实验室", new Dictionary<string, int> { ["Facility"] = 1, ["Crafting"] = 1, ["Chemical"] = 1 }, "配备冷凝管与离心机的高纯度试样提取台", "Analyze"),
        new("急救药品柜与消毒灭菌台", "公共诊疗所", new Dictionary<string, int> { ["Facility"] = 1, ["Medical"] = 1, ["Consumable"] = 1 }, "存放有外伤止血纱布、镇静药剂与消毒酒精的药柜", "Treatment"),
        new("诊疗所卧榻病床", "公共诊疗所", new Dictionary<string, int> { ["Facility"] = 1, ["Resting"] = 1, ["Bed"] = 1 }, "提供给伤员与深度疲劳者调养的干净单人床", "Rest"),
        new("烹饪灶台与食材保鲜库", "后勤院", new Dictionary<string, int> { ["Facility"] = 1, ["FoodPrep"] = 1, ["Consumable"] = 1 }, "具备完整燃气灶与冷藏柜的学园后勤炊事区", "Cooking"),
        new("古典胡桃木对弈棋盘", "公共娱乐室", new Dictionary<string, int> { ["Facility"] = 1, ["Recreational"] = 1, ["Strategy"] = 1 }, "雕工精湛的国际象棋盘，适合两人推敲大局", "Gaming"),
        new("舒缓留声机与沙龙沙发座", "公共娱乐室", new Dictionary<string, int> { ["Facility"] = 1, ["Recreational"] = 1, ["Resting"] = 1 }, "播放着舒缓交响黑胶唱片的软垫休息区", "Relax"),
        new("沙袋立靶与战术格斗垫", "训练院", new Dictionary<string, int> { ["Facility"] = 1, ["Training"] = 1, ["Physical"] = 1 }, "用于维持体能与近身自卫反射的专业训练器械", "Training"),
        new("高耸防卫瞭望台", "训练院", new Dictionary<string, int> { ["Facility"] = 1, ["Training"] = 1, ["Watch"] = 1 }, "能俯瞰主通道视野的战术哨位", "Watch"),
        new("青铜声学谐振叉与调音板", "共鸣厅", new Dictionary<string, int> { ["Facility"] = 1, ["Acoustic"] = 1, ["Researchable"] = 1 }, "用以测试空间回声与频率干涉的精密音律装置", "Acoustic"),
        new("黄铜天球三维星图仪", "星盘讲堂", new Dictionary<string, int> { ["Facility"] = 1, ["Spatial"] = 1, ["Researchable"] = 1 }, "推演海岛地理经纬与天体方位的机械天球", "Spatial"),
        new("画架与矿物颜料调配案", "美术工房与冬园", new Dictionary<string, int> { ["Facility"] = 1, ["Creative"] = 1, ["Artistic"] = 1 }, "整齐陈列天然矿物色料与亚麻画布的创作室", "Art"),
        new("活体样本培养槽与标本柜", "博物与生态室", new Dictionary<string, int> { ["Facility"] = 1, ["Specimen"] = 1, ["Researchable"] = 1 }, "维持着恒温微生态的动植物标本采集装置", "Sampling"),
        new("学园全域广播主控台", "中央厅", new Dictionary<string, int> { ["Facility"] = 1, ["Broadcast"] = 1, ["Command"] = 1 }, "连通全校各房间扬声器的中央扩音控制柜", "Broadcast"),
        new("生活院学员标准就寝间", "生活院", new Dictionary<string, int> { ["Facility"] = 1, ["Bed"] = 1, ["Resting"] = 1 }, "带有整洁被褥与阅读台灯的学员宿舍单间", "Sleep"),
        new("门楼守夜前哨站", "门楼与誓约庭", new Dictionary<string, int> { ["Facility"] = 1, ["Watch"] = 1, ["Boundary"] = 1 }, "正对封锁界标的夜间值守长椅与煤油风灯", "Watch")
    };

    /// <summary>
    /// 十六位学员职业的专属领域知识与学术命题规范列表。
    /// </summary>
    /// <remarks>
    /// 每个职业包含一门专属领域知识，并在开局时将其命题注入学员认知心智。
    /// </remarks>
    public static readonly IReadOnlyList<DomainKnowledgeSpec> DomainKnowledges = new List<DomainKnowledgeSpec>
    {
        new("侦探", "痕迹物证与反向推论法", "基于细微物质残留反推行动轨迹与心理动机的刑侦推理体系", ["evidence_precedes_judgment", "subtle_trace_reveals_intent"]),
        new("律师", "契约法理与规范审查学", "研判因果规范边界、双方法律约束与免责要件的法理学识", ["covenant_requires_mutual_assent", "arbitrary_coercion_invalid"]),
        new("考古学家", "古代铭文考释与契约释义", "破译古文字隐喻、上古誓约仪式构成与禁忌界标的学术", ["ancient_inscriptions_bind_reality", "demon_bound_by_covenant"]),
        new("机械工程师", "热力学管网与机械联锁规程", "高压蒸汽阀门、联锁传动与故障快速排查的工程规范", ["pressure_systems_require_dual_check", "interlock_prevents_failure"]),
        new("化学家", "异常源质催化与试剂分析学", "提纯非自然试样、鉴别毒理反应与分子置换的实验方法", ["catalyst_purifies_miasma", "reagent_identifies_essence"]),
        new("临床医生", "创伤急救与神经镇静药理", "现场止血缝合、休克抢救与应对急性精神恐慌的医护手段", ["sedative_calms_acute_panic", "sterilization_prevents_corruption"]),
        new("博物学家", "生态相容性与活体标本辨析", "判断外异动植物生态习性与环境相容性指标的自然科学", ["specimens_adapt_to_mana", "biological_indicators_signal_danger"]),
        new("职业棋手", "非完全信息博弈与态势调度", "在迷雾中预判对手步数、评估资源弃保的最优博弈思维", ["preserve_options_under_fog", "sacrifice_pawn_for_tempo"]),
        new("建筑师", "空间受力结构与安全动线闭环", "建筑承重点应力核算、避难动线规划与局部空间加固技术", ["load_bearing_points_reinforce_security", "escape_routes_require_redundancy"]),
        new("音乐家", "声学共鸣测距与谐波相位干涉", "借由声音频率在空间回荡测定隐蔽虚空与吸收音纹的技能", ["frequency_cancels_sonic_aberration", "resonance_detects_hollow_space"]),
        new("诗人", "言灵韵律学与共鸣修辞格", "通过词律声调激发精神共鸣、平抚群体恐慌的抒情言语术", ["meter_amplifies_incantation", "metaphor_stabilizes_psyche"]),
        new("神射手", "弹道封锁与视线死角战术研判", "远距警戒射程覆盖、死角封堵与戒备压制的战术要领", ["overwatch_suppresses_hostile_approach", "blind_spots_invite_ambush"]),
        new("神偷", "潜行暗影步法与精巧机构解除", "绕开感知警戒、无声潜入与探寻密道机关的实战经验", ["silent_movement_evades_detection", "tumbler_bypass_unlocks_barrier"]),
        new("舞蹈艺术家", "动态平衡与空间节拍直觉", "控制身体重心理论、规避骤然冲击与在危机中维持从容的体魄", ["cadence_resists_kinetic_shock", "spatial_awareness_dodges_hazard"]),
        new("画家", "视觉光谱分析与隐匿画层复原", "辨识光线波长、剥离重叠图层以还原被遮蔽事实的视觉技艺", ["pigment_stratigraphy_reveals_hidden_glyphs", "visual_memory_preserves_truth"]),
        new("主厨", "长效配给保鲜与营养热量统筹", "极端封闭环境下食材防腐、热量精准配给与士气维持策略", ["balanced_calories_sustain_morale", "proper_salting_wards_decay"])
    };

    /// <summary>
    /// 十六位学员的随身普通实用道具配置列表。
    /// </summary>
    public static readonly IReadOnlyList<CharacterItemSpec> CharacterItems = new List<CharacterItemSpec>
    {
        new("侦探", "便携调查记事本", "Document", new Dictionary<string, int> { ["WritingSurface"] = 1 }, "案件疑点记录与证言备忘", false),
        new("侦探", "黄铜单目放大镜", "Tool", new Dictionary<string, int> { ["Optics"] = 1, ["Precision"] = 1 }, "", true),
        new("诗人", "未完手抄诗卷", "Document", new Dictionary<string, int> { ["Poetry"] = 1 }, "写满删改诗行的草稿", false),
        new("诗人", "黑羽墨水笔", "Tool", new Dictionary<string, int> { ["WritingTool"] = 1 }, "", true),
        new("音乐家", "标准共鸣调音叉", "Tool", new Dictionary<string, int> { ["Acoustic"] = 1, ["Mind"] = 1 }, "", true),
        new("音乐家", "声学测时秒表", "Device", new Dictionary<string, int> { ["Precision"] = 1 }, "", false),
        new("博物学家", "便携野外标本盒", "Box", new Dictionary<string, int> { ["Container"] = 1 }, "", false),
        new("博物学家", "精钢活体采样镊", "Tool", new Dictionary<string, int> { ["Precision"] = 1 }, "", true),
        new("机械工程师", "棘轮多用扳手", "Tool", new Dictionary<string, int> { ["Repair"] = 1, ["Form"] = 1 }, "", true),
        new("机械工程师", "防风手持焊枪", "Tool", new Dictionary<string, int> { ["HeatSource"] = 1 }, "", false),
        new("主厨", "折叠调味香料盒", "Box", new Dictionary<string, int> { ["Food"] = 1 }, "", false),
        new("主厨", "多功能切配厨刀", "Knife", new Dictionary<string, int> { ["Cooking"] = 1, ["Blade"] = 1 }, "", true),
        new("考古学家", "铭文拓印包", "Kit", new Dictionary<string, int> { ["Archaeology"] = 1 }, "", false),
        new("考古学家", "折叠古文字放大镜", "Tool", new Dictionary<string, int> { ["Optics"] = 1 }, "", true),
        new("神偷", "特种撬锁插针", "Key", new Dictionary<string, int> { ["Lockpicking"] = 1, ["Threshold"] = 1 }, "", true),
        new("神偷", "多股抓钩绳索", "Tool", new Dictionary<string, int> { ["Mobility"] = 1 }, "", false),
        new("律师", "学园管理规章折本", "Book", new Dictionary<string, int> { ["LawCode"] = 1, ["Law"] = 1 }, "《封闭期校园安全与契约准则》", false),
        new("律师", "公证黄铜手印章", "Tool", new Dictionary<string, int> { ["AuthorityToken"] = 1 }, "", true),
        new("职业棋手", "折叠硬木棋盘", "Table", new Dictionary<string, int> { ["Board"] = 1, ["Mind"] = 1 }, "", true),
        new("职业棋手", "黑白弃子收纳袋", "Container", new Dictionary<string, int>(), "", false),
        new("化学家", "耐酸微量反应管", "Flask", new Dictionary<string, int> { ["Chemistry"] = 1 }, "", true),
        new("化学家", "滴定分析吸管", "Tool", new Dictionary<string, int>(), "", false),
        new("神射手", "折叠警戒短弩", "Bow", new Dictionary<string, int> { ["Weapon"] = 1, ["Strife"] = 1 }, "", true),
        new("神射手", "淬火刻印箭筒", "Kit", new Dictionary<string, int> { ["Ammunition"] = 1 }, "", false),
        new("舞蹈艺术家", "节拍银质双腕铃", "Tool", new Dictionary<string, int> { ["Rhythm"] = 1, ["Bond"] = 1 }, "", true),
        new("舞蹈艺术家", "引导仪式丝缎带", "Clothing", new Dictionary<string, int>(), "", false),
        new("画家", "双层调色托盘", "Tool", new Dictionary<string, int> { ["Painting"] = 1 }, "", true),
        new("画家", "画层剥离细刮刀", "Knife", new Dictionary<string, int> { ["Blade"] = 1, ["Precision"] = 1 }, "", false),
        new("建筑师", "黄铜测绘分度罗盘", "Tool", new Dictionary<string, int> { ["Survey"] = 1, ["Form"] = 1 }, "", true),
        new("建筑师", "硬木折叠比例尺", "Tool", new Dictionary<string, int>(), "", false),
        new("临床医生", "急救清创缝合包", "Kit", new Dictionary<string, int> { ["Medical"] = 1, ["Preservation"] = 1 }, "", true),
        new("临床医生", "银质镇痛喷雾瓶", "Bottle", new Dictionary<string, int> { ["Medical"] = 1, ["Consumable"] = 1, ["Sedative"] = 1 }, "", false)
    };

    /// <summary>
    /// 全校各场景中的常设家具、陈设、读物、容器与器具规格列表。
    /// </summary>
    public static readonly IReadOnlyList<SceneItemSpec> SceneItems = new List<SceneItemSpec>
    {
        new("中央厅", "自鸣报时大挂钟", "Device", new Dictionary<string, int> { ["Law"] = 1, ["Preservation"] = 1 }, ""),
        new("中央厅", "中央会议实木长桌", "Table", new Dictionary<string, int> { ["Surface"] = 1 }, ""),
        new("中央厅", "皮质休息软长沙发", "Bed", new Dictionary<string, int> { ["RestingPlace"] = 1 }, ""),
        new("教学图书馆", "图书借阅索书柜", "Shelf", new Dictionary<string, int> { ["Container"] = 1, ["Openable"] = 1 }, ""),
        new("教学图书馆", "红木研读长桌", "Table", new Dictionary<string, int> { ["Surface"] = 1 }, ""),
        new("教学图书馆", "《异象溯源与阵法基座》", "Book", new Dictionary<string, int> { ["Mind"] = 1 }, "记录学园四院封印地基结构与调谐方式"),
        new("教学图书馆", "《离岛植被与真菌生态》", "Book", new Dictionary<string, int> { ["Preservation"] = 1 }, "记载南岸与温室耐盐灰疫孢子的原生特性"),
        new("生活院", "大容量陶瓷茶壶", "Pot", new Dictionary<string, int> { ["Vessel"] = 1, ["Openable"] = 1, ["Water"] = 1, ["Drink"] = 1, ["Consumable"] = 1, ["Servings"] = 4 }, ""),
        new("生活院", "白瓷饮水杯", "Bottle", new Dictionary<string, int> { ["Vessel"] = 1 }, ""),
        new("生活院", "公共会客圆木桌", "Table", new Dictionary<string, int> { ["Surface"] = 1 }, ""),
        new("生活院", "布艺休息单人沙发", "Chair", new Dictionary<string, int> { ["RestingPlace"] = 1 }, ""),
        new("后勤院", "双火口铸铁烹饪大灶", "Stove", new Dictionary<string, int> { ["HeatSource"] = 1, ["Transmutation"] = 1 }, ""),
        new("后勤院", "密封储备面粉大木桶", "Box", new Dictionary<string, int> { ["Container"] = 1, ["Ingredient"] = 1, ["Servings"] = 8 }, ""),
        new("后勤院", "炉用无烟煤炭铁箱", "Box", new Dictionary<string, int> { ["HeatSource"] = 1, ["Container"] = 1 }, ""),
        new("训练院", "实木冷兵器陈列排架", "Shelf", new Dictionary<string, int> { ["Surface"] = 1 }, ""),
        new("训练院", "皮革悬挂训练重沙袋", "Tool", new Dictionary<string, int> { ["Strife"] = 1 }, ""),
        new("训练院", "开刃练习硬木剑", "Sword", new Dictionary<string, int> { ["Strife"] = 1 }, ""),
        new("公共娱乐室", "标准红木台球桌", "Table", new Dictionary<string, int> { ["Surface"] = 1 }, ""),
        new("公共娱乐室", "棋牌游戏杂物柜", "Shelf", new Dictionary<string, int> { ["Container"] = 1, ["Openable"] = 1 }, ""),
        new("公共娱乐室", "老式铜喇叭手摇留声机", "Device", new Dictionary<string, int> { ["Mind"] = 1 }, ""),
        new("美术工房与冬园", "重型松木画架", "Table", new Dictionary<string, int> { ["Surface"] = 1 }, ""),
        new("美术工房与冬园", "半身石膏素描教具", "Tool", new Dictionary<string, int> { ["Form"] = 1 }, ""),
        new("美术工房与冬园", "清洗用松节油铁罐", "Pot", new Dictionary<string, int> { ["Vessel"] = 1, ["Chemical"] = 1, ["Solvent"] = 1, ["Flammable"] = 1 }, ""),
        new("公共诊疗所", "可移动急救检查病床", "Bed", new Dictionary<string, int> { ["RestingPlace"] = 1, ["Preservation"] = 1 }, ""),
        new("公共诊疗所", "双门玻璃器械药品柜", "Shelf", new Dictionary<string, int> { ["Container"] = 1, ["Openable"] = 1 }, ""),
        new("公共诊疗所", "临床水银柱体温计", "Tool", new Dictionary<string, int> { ["Precision"] = 1 }, ""),
        new("公共诊疗所", "脱脂医用灭菌棉卷", "Tool", new Dictionary<string, int> { ["Preservation"] = 1 }, ""),
        new("契约审议室", "橡木公证法官高背椅", "Chair", new Dictionary<string, int> { ["Law"] = 1 }, ""),
        new("契约审议室", "黄铜立式誓约双臂天平", "Device", new Dictionary<string, int> { ["Law"] = 1, ["Mind"] = 1 }, ""),
        new("契约审议室", "审议公证黄铜法槌", "Tool", new Dictionary<string, int> { ["Law"] = 1, ["Strife"] = 1 }, ""),
        new("共鸣厅", "大口径特制定音铜锣", "Tool", new Dictionary<string, int> { ["Acoustic"] = 1, ["Mind"] = 1 }, ""),
        new("共鸣厅", "高密度吸音软木防震台", "Table", new Dictionary<string, int> { ["Form"] = 1 }, ""),
        new("星盘讲堂", "大型黄铜浑天星盘", "Device", new Dictionary<string, int> { ["Mind"] = 1, ["Threshold"] = 1 }, ""),
        new("星盘讲堂", "阶梯讲堂长排木椅", "Chair", new Dictionary<string, int> { ["RestingPlace"] = 1 }, ""),
        new("星盘讲堂", "《天体共振与校准导引》", "Book", new Dictionary<string, int> { ["Mind"] = 1 }, "详述源质反应炉破裂前必须进行的主阵声学同步"),
        new("化学实验室", "负压排毒化学通风橱", "Mechanism", new Dictionary<string, int> { ["Preservation"] = 1 }, ""),
        new("化学实验室", "耐高温工业酒精喷灯", "Stove", new Dictionary<string, int> { ["HeatSource"] = 1 }, ""),
        new("化学实验室", "高硼硅耐热试管排架", "Shelf", new Dictionary<string, int> { ["Chemistry"] = 1 }, ""),
        new("博物与生态室", "密闭通气活体展示柜", "Shelf", new Dictionary<string, int> { ["Container"] = 1, ["Openable"] = 1 }, ""),
        new("博物与生态室", "灰疫菌丝分离培养皿", "Pot", new Dictionary<string, int> { ["Specimen"] = 1 }, ""),
        new("动力总站", "水轮发电机主离合操纵杆", "Mechanism", new Dictionary<string, int> { ["Form"] = 1, ["Preservation"] = 1 }, ""),
        new("动力总站", "工业机械重负荷润滑油桶", "Bottle", new Dictionary<string, int>(), ""),
        new("动力总站", "主蒸汽管路超压泄放阀", "Mechanism", new Dictionary<string, int> { ["Preservation"] = 1 }, ""),
        new("门楼与誓约庭", "守门铜钟与拉索", "Mechanism", new Dictionary<string, int> { ["Law"] = 1 }, ""),
        new("门楼与誓约庭", "门禁值班木条桌", "Table", new Dictionary<string, int> { ["Surface"] = 1 }, ""),
        new("中央草场与露天运动场", "训练击打草人靶", "Tool", new Dictionary<string, int> { ["Strife"] = 1 }, ""),
        new("中央草场与露天运动场", "休憩木制长椅", "Chair", new Dictionary<string, int> { ["RestingPlace"] = 1 }, ""),
        new("中央草场与露天运动场", "公共饮水喷泉水钵", "Pot", new Dictionary<string, int> { ["Vessel"] = 1 }, ""),

        // 扩充常设物品，大幅丰富各场景决策与互动空间
        new("中央厅", "公告板与磁吸贴片", "Board", new Dictionary<string, int> { ["Surface"] = 1, ["Law"] = 1 }, "供学员与管理方张贴公约、警示与寻人启事的木制公告板"),
        new("中央厅", "应急强光手电筒", "Device", new Dictionary<string, int> { ["Tool"] = 1, ["Optics"] = 1 }, ""),
        new("中央厅", "公共意见箱", "Box", new Dictionary<string, int> { ["Container"] = 1, ["Openable"] = 1 }, "投递匿名谏言与线索报告的铁制木箱"),

        new("教学图书馆", "《古代真言契约通典》", "Book", new Dictionary<string, int> { ["Book"] = 1, ["Law"] = 1, ["Mind"] = 1, ["Covenant"] = 1, ["Evidence"] = 1 }, "汇编上古海岛因果契约与真言誓约的原典注疏"),
        new("教学图书馆", "古籍研读放大台", "Table", new Dictionary<string, int> { ["Surface"] = 1, ["Optics"] = 1 }, ""),
        new("教学图书馆", "羊皮纸稿卷与墨水瓶", "Document", new Dictionary<string, int> { ["WritingTool"] = 1, ["Document"] = 1 }, "记录演算过程与草拟条文的空白稿纸"),

        new("生活院", "双层洗漱净水盆", "Pot", new Dictionary<string, int> { ["Vessel"] = 1 }, ""),
        new("生活院", "急救备用蜡烛包", "Box", new Dictionary<string, int> { ["HeatSource"] = 1, ["Consumable"] = 1 }, ""),
        new("生活院", "学员私人物品寄存柜", "Shelf", new Dictionary<string, int> { ["Container"] = 1, ["Openable"] = 1, ["Lockable"] = 1 }, "带有黄铜插锁的多格储物柜"),

        new("后勤院", "纯净蓄水过滤桶", "Pot", new Dictionary<string, int> { ["Vessel"] = 1, ["Water"] = 1, ["Drink"] = 1, ["Consumable"] = 1, ["Servings"] = 8 }, "盛装多级砂滤净水的陶瓷蓄水大桶"),
        new("后勤院", "粗盐与风干熏肉箱", "Box", new Dictionary<string, int> { ["Container"] = 1, ["Ingredient"] = 1, ["Lockable"] = 1, ["Servings"] = 6 }, "保鲜盐渍应急干粮的高密封木箱"),
        new("后勤院", "折叠金属餐盘排架", "Shelf", new Dictionary<string, int> { ["Surface"] = 1 }, ""),

        new("训练院", "防暴金属圆盾", "Tool", new Dictionary<string, int> { ["Tool"] = 1, ["Weapon"] = 1, ["Preservation"] = 1 }, ""),
        new("训练院", "武器养护磨刀石台", "Table", new Dictionary<string, int> { ["Tool"] = 1, ["Surface"] = 1 }, ""),
        new("训练院", "急救止血绷带卷", "Kit", new Dictionary<string, int> { ["Medical"] = 1, ["Consumable"] = 1, ["Bandage"] = 1 }, ""),

        new("公共娱乐室", "舒缓留声机黑胶唱片集", "Item", new Dictionary<string, int> { ["Record"] = 1, ["Recreational"] = 1, ["Acoustic"] = 1 }, "收录古典奏鸣曲与安神小调的黑胶唱片"),
        new("公共娱乐室", "飞镖靶盘与黄铜飞镖", "Tool", new Dictionary<string, int> { ["Strife"] = 1, ["Precision"] = 1 }, ""),

        new("美术工房与冬园", "天然矿物颜料调色台", "Table", new Dictionary<string, int> { ["Surface"] = 1, ["Painting"] = 1 }, ""),
        new("美术工房与冬园", "冬园耐寒温室盆栽", "Pot", new Dictionary<string, int> { ["Specimen"] = 1, ["Preservation"] = 1 }, ""),

        new("公共诊疗所", "外用浓缩碘伏消毒液", "Bottle", new Dictionary<string, int> { ["Medical"] = 1, ["Consumable"] = 1 }, ""),
        new("公共诊疗所", "无菌清创手术缝合钳", "Tool", new Dictionary<string, int> { ["Medical"] = 1, ["Precision"] = 1 }, ""),

        new("契约审议室", "古代契约拓印石泥", "Kit", new Dictionary<string, int> { ["Tool"] = 1, ["Archaeology"] = 1 }, ""),
        new("契约审议室", "铁铸保险密封公文箱", "Box", new Dictionary<string, int> { ["Container"] = 1, ["Openable"] = 1, ["Lockable"] = 1 }, "用于封存已签署核心法案与证物的重型铁箱"),

        new("共鸣厅", "声波振动金属悬丝", "Tool", new Dictionary<string, int> { ["Acoustic"] = 1, ["Precision"] = 1 }, ""),
        new("共鸣厅", "共鸣空腔调谐木箱", "Box", new Dictionary<string, int> { ["Container"] = 1, ["Openable"] = 1, ["Acoustic"] = 1 }, ""),

        new("星盘讲堂", "星盘投影分度圆盘", "Device", new Dictionary<string, int> { ["Tool"] = 1, ["Survey"] = 1 }, ""),
        new("星盘讲堂", "天体运行黄铜发条齿轮箱", "Box", new Dictionary<string, int> { ["Container"] = 1, ["Openable"] = 1, ["Mechanism"] = 1 }, ""),

        new("化学实验室", "酸碱中和分析滴定管", "Tool", new Dictionary<string, int> { ["Tool"] = 1, ["Chemistry"] = 1, ["Precision"] = 1 }, ""),
        new("化学实验室", "广口浓硫酸试剂瓶", "Bottle", new Dictionary<string, int> { ["Flask"] = 1, ["Chemistry"] = 1, ["Chemical"] = 1, ["Corrosive"] = 1, ["Acid"] = 1 }, ""),

        new("博物与生态室", "高倍单目光学显微镜", "Device", new Dictionary<string, int> { ["Tool"] = 1, ["Optics"] = 1, ["Precision"] = 1 }, ""),
        new("博物与生态室", "植物解剖不锈钢托盘", "Table", new Dictionary<string, int> { ["Surface"] = 1 }, ""),

        new("动力总站", "工业级重型棘轮扳手组", "Kit", new Dictionary<string, int> { ["Tool"] = 1, ["Repair"] = 1, ["Form"] = 1 }, ""),
        new("动力总站", "紧急切断手动制动阀", "Mechanism", new Dictionary<string, int> { ["Mechanism"] = 1, ["Preservation"] = 1 }, "管网发生极端超压时供手动泄压切断的主手轮"),

        new("门楼与誓约庭", "巡夜防风煤油马灯", "Device", new Dictionary<string, int> { ["Tool"] = 1, ["HeatSource"] = 1 }, ""),
        new("门楼与誓约庭", "铸铁门闩与重锁", "Mechanism", new Dictionary<string, int> { ["Mechanism"] = 1, ["Lockable"] = 1 }, ""),
        new("门楼与誓约庭", "警用鸣响铜哨", "Tool", new Dictionary<string, int> { ["Tool"] = 1, ["Acoustic"] = 1 }, ""),

        new("中央草场与露天运动场", "室外木制储物雨棚", "Shelf", new Dictionary<string, int> { ["Container"] = 1, ["Openable"] = 1 }, ""),
        new("中央草场与露天运动场", "草场石刻日晷台", "Device", new Dictionary<string, int> { ["Precision"] = 1, ["Surface"] = 1 }, ""),

        // 进一步扩充全校物理道具库，全面丰富学生与恶魔的决策与博弈空间
        new("中央厅", "应急求援信号枪箱", "Box", new Dictionary<string, int> { ["Container"] = 1, ["Openable"] = 1, ["Tool"] = 1 }, "存放有海岛应急受困求援用红色信号枪与引信的金属提箱"),
        new("中央厅", "黄铜磁性航海罗盘", "Device", new Dictionary<string, int> { ["Tool"] = 1, ["Precision"] = 1 }, "测定海岛洋流与地磁指向的便携罗盘"),
        new("中央厅", "全校备用铜钥匙盒", "Box", new Dictionary<string, int> { ["Container"] = 1, ["Openable"] = 1, ["Lockable"] = 1 }, "封存有数把通向功能性储藏室备用黄铜钥匙的小盒"),
        new("中央厅", "全员联络记事水笔", "Tool", new Dictionary<string, int> { ["WritingTool"] = 1 }, "插在公告板旁供自由书写线索的水笔"),

        new("教学图书馆", "《失落海岛地理志与洋流图》", "Book", new Dictionary<string, int> { ["Book"] = 1, ["Spatial"] = 1 }, "详细记载学园所在孤岛暗礁与洋流涨落周期的厚重古籍"),
        new("教学图书馆", "隐形墨水显影烘烤灯", "Mechanism", new Dictionary<string, int> { ["HeatSource"] = 1, ["Optics"] = 1 }, "用于微热烘烤出古籍夹层隐秘夹签的专用炭精灯"),
        new("教学图书馆", "蜡封保密羊皮卷筒", "Box", new Dictionary<string, int> { ["Container"] = 1, ["Openable"] = 1 }, "带有火漆印痕的黄铜密封筒，内有前人调查留下的草图"),
        new("教学图书馆", "黄铜开信与裁纸刀", "Knife", new Dictionary<string, int> { ["Blade"] = 1, ["Tool"] = 1 }, "用于拆封封泥与修整纸边的薄刃铜刀"),

        new("生活院", "防寒保暖厚羊毛毯", "Cloth", new Dictionary<string, int> { ["Consumable"] = 1, ["Preservation"] = 1 }, "叠放在会客角供夜间值守取暖的厚羊毛毯"),
        new("生活院", "铸铜炭火取暖手炉", "Pot", new Dictionary<string, int> { ["HeatSource"] = 1, ["Vessel"] = 1 }, "装有微温炭块的小巧暖手铜炉"),
        new("生活院", "发条机械铜铃闹钟", "Device", new Dictionary<string, int> { ["Precision"] = 1, ["Acoustic"] = 1 }, "定时发出清脆铜铃响声的机械闹钟"),

        new("后勤院", "生铁平底烹饪煎锅", "Tool", new Dictionary<string, int> { ["Tool"] = 1, ["Cooking"] = 1 }, "用于煎制肉类与加热面食的沉重铁锅"),
        new("后勤院", "干燥迷迭香香料袋", "Box", new Dictionary<string, int> { ["Food"] = 1, ["Container"] = 1 }, "带有浓烈芳香的料理调味草药袋"),
        new("后勤院", "应急高能压缩饼干箱", "Box", new Dictionary<string, int> { ["Food"] = 1, ["Container"] = 1, ["Openable"] = 1 }, "装满高热量军规应急干粮的密封铁箱"),
        new("后勤院", "玻璃发酵食用白醋瓶", "Bottle", new Dictionary<string, int> { ["Food"] = 1, ["Vessel"] = 1 }, "带有酸性去污与调味特性的食用醋瓶"),

        new("训练院", "硬橡木格斗短棍", "Tool", new Dictionary<string, int> { ["Strife"] = 1, ["Weapon"] = 1 }, "防身与近战自卫用的硬质木棍"),
        new("训练院", "双筒高倍率巡视望远镜", "Device", new Dictionary<string, int> { ["Tool"] = 1, ["Optics"] = 1, ["Precision"] = 1 }, "用于在瞭望台侦察海面与校门的军工望远镜"),
        new("训练院", "生肌活血草药软膏盒", "Box", new Dictionary<string, int> { ["Medical"] = 1, ["Consumable"] = 1 }, "用于跌打损伤快速止痛的深绿色草药膏"),

        new("公共娱乐室", "羊皮纸填字迷宫册", "Item", new Dictionary<string, int> { ["Puzzle"] = 1, ["Recreational"] = 1 }, "设计精巧的逻辑填字游戏手抄本，能有效缓解焦虑"),
        new("公共娱乐室", "雕花黄铜二十面骰", "Tool", new Dictionary<string, int> { ["Tool"] = 1, ["Mind"] = 1 }, "用于推演概率与下棋裁定的精工黄铜骰子"),
        new("公共娱乐室", "陶土烤制烟斗与火柴盒", "Tool", new Dictionary<string, int> { ["HeatSource"] = 1, ["Consumable"] = 1 }, "提供微弱火源与放松作用的传统烟斗器具"),

        new("美术工房与冬园", "精炼亚麻画布卷", "Item", new Dictionary<string, int> { ["Surface"] = 1, ["Form"] = 1, ["Material"] = 1 }, "可用于绘制拓扑动线或遮盖孔洞的致密麻布"),
        new("美术工房与冬园", "高碳钢雕刻平凿组", "Tool", new Dictionary<string, int> { ["Blade"] = 1, ["Tool"] = 1, ["Precision"] = 1 }, "用于修整石膏与撬动薄木板的木工雕刻凿"),
        new("美术工房与冬园", "干燥薰衣草安神花束", "Item", new Dictionary<string, int> { ["Specimen"] = 1, ["Mind"] = 1 }, "散发淡雅清香以舒缓神经紧绷的干花束"),

        new("公共诊疗所", "无菌医用脱脂纱布卷", "Kit", new Dictionary<string, int> { ["Medical"] = 1, ["Consumable"] = 1, ["Bandage"] = 1, ["Preservation"] = 1 }, "用于外伤清创包扎止血的标准医用纱布"),
        new("公共诊疗所", "浓缩缬草安眠浸膏瓶", "Bottle", new Dictionary<string, int> { ["Medical"] = 1, ["Consumable"] = 1, ["Sedative"] = 1 }, "用于平复急性惊恐与诱导深度休眠的镇静剂"),
        new("公共诊疗所", "医用不锈钢听诊器", "Tool", new Dictionary<string, int> { ["Precision"] = 1, ["Medical"] = 1 }, "用于体察心率脉搏与异常胸腔杂音的听诊器"),

        new("契约审议室", "上古契约条款拓印石泥", "Kit", new Dictionary<string, int> { ["Tool"] = 1, ["Archaeology"] = 1 }, "用于精准复制石碑与铭文符条的特种印泥"),
        new("契约审议室", "证物封存红泥印章台", "Tool", new Dictionary<string, int> { ["Tool"] = 1, ["Law"] = 1 }, "公证加盖有效印信的朱砂印泥盒"),
        new("契约审议室", "厚帆布证物保密袋", "Box", new Dictionary<string, int> { ["Container"] = 1, ["Openable"] = 1 }, "加盖公证火漆以封存关键凶器与证据的帆布袋"),

        new("共鸣厅", "纯银高音谐振三角铁", "Tool", new Dictionary<string, int> { ["Acoustic"] = 1, ["Precision"] = 1 }, "能激发特定微细高频声波以探测空洞结构的乐器"),
        new("共鸣厅", "软木隔音降噪耳塞盒", "Box", new Dictionary<string, int> { ["Container"] = 1, ["Preservation"] = 1 }, "用于抵御极端尖锐异变声波的软木耳塞"),

        new("星盘讲堂", "精工黄铜六分仪", "Device", new Dictionary<string, int> { ["Tool"] = 1, ["Precision"] = 1, ["Survey"] = 1 }, "用于测量地平高度与校对天体经纬的航海测量器"),
        new("星盘讲堂", "夜光矿物研磨石臼", "Tool", new Dictionary<string, int> { ["Tool"] = 1, ["Chemistry"] = 1 }, "用于将荧光石块研磨成粉剂的坚硬花岗岩石臼"),

        new("化学实验室", "活性炭颗粒滤毒漏斗", "Tool", new Dictionary<string, int> { ["Tool"] = 1, ["Chemistry"] = 1, ["Preservation"] = 1 }, "用于滤除腐蚀性酸雾与毒性气溶胶的高效滤斗"),
        new("化学实验室", "耐酸抗腐蚀石棉手套", "Clothing", new Dictionary<string, int> { ["Preservation"] = 1 }, "搬运强酸强碱与高温热源的安全防护手套"),
        new("化学实验室", "工业级结晶硫磺粉末瓶", "Bottle", new Dictionary<string, int> { ["Flask"] = 1, ["Hazard"] = 1, ["Chemistry"] = 1 }, "易燃且可作为强效催化剂的黄色硫磺结晶瓶"),

        new("博物与生态室", "海水盐度光学折射比重计", "Device", new Dictionary<string, int> { ["Tool"] = 1, ["Precision"] = 1 }, "通过折射率快速测定溶液矿物质浓度的精密光学仪"),
        new("博物与生态室", "福尔马林防腐浸制标本罐", "Bottle", new Dictionary<string, int> { ["Vessel"] = 1, ["Specimen"] = 1, ["Chemical"] = 1, ["Toxic"] = 1, ["Preservation"] = 1 }, "封存有南岸异变深海节肢动物胚胎的玻璃标本罐"),

        new("动力总站", "铜芯双针蒸汽压力表", "Device", new Dictionary<string, int> { ["Precision"] = 1, ["Mechanism"] = 1 }, "实时指示管网主副压力差的防爆仪表"),
        new("动力总站", "石墨盘根阀门耐磨填料绳", "Tool", new Dictionary<string, int> { ["Tool"] = 1, ["Repair"] = 1, ["Material"] = 1 }, "用于修补蒸汽泄漏与管路气密的绝缘耐高温密封绳"),
        new("动力总站", "厚帆布防烫劳保手套", "Clothing", new Dictionary<string, int> { ["Preservation"] = 1 }, "供巡查超温管道时避免灼伤的加厚手套"),

        new("门楼与誓约庭", "防暴巡夜浸油火把", "Tool", new Dictionary<string, int> { ["Tool"] = 1, ["HeatSource"] = 1 }, "浸润松脂油、能在海风中长时间燃烧的照明火把"),
        new("门楼与誓约庭", "重型铸铁防盗挂锁与钥匙", "Mechanism", new Dictionary<string, int> { ["Mechanism"] = 1, ["Lockable"] = 1 }, "用于锁死铁栅门的主安全重锁"),

        new("中央草场与露天运动场", "便携式双人防风帐篷", "Box", new Dictionary<string, int> { ["Container"] = 1, ["RestingPlace"] = 1 }, "可在草场搭建以提供户外遮风避雨的折叠帆布帐篷"),
        new("中央草场与露天运动场", "铸铁实心训练投掷铅球", "Tool", new Dictionary<string, int> { ["Tool"] = 1, ["Strife"] = 1 }, "沉重坚硬的铅球，可用于重力触发机关或压制重物")
    };

    /// <summary>
    /// 十六位学员初始差异化情感光谱预设表。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (Emotion Emotion, string Source, float Intensity, bool IsPersistent)[]> EmotionPresets =
        new Dictionary<string, (Emotion, string, float, bool)[]>
        {
            ["侦探"] = [(Emotion.Serenity, "职业冷静定力", 2.2f, true), (Emotion.Hope, "还原真相信念", 2.0f, true), (Emotion.Fear, "封锁悬疑疑虑", 0.6f, false)],
            ["律师"] = [(Emotion.Serenity, "法理秩序信念", 2.4f, true), (Emotion.Hope, "程序公约脱困", 1.8f, true), (Emotion.Anger, "对非法拘禁抵触", 1.2f, false)],
            ["神射手"] = [(Emotion.Hatred, "敌意戒备本能", 1.8f, true), (Emotion.Fear, "孤立狙击警戒", 1.5f, true), (Emotion.Serenity, "呼吸心率调控", 1.0f, false)],
            ["职业棋手"] = [(Emotion.Serenity, "绝对理性计算", 3.0f, true), (Emotion.Hope, "全局解法预期", 1.2f, true)],
            ["主厨"] = [(Emotion.Devotion, "投喂全员使命", 2.6f, true), (Emotion.Joy, "烹饪分享愉悦", 2.0f, false), (Emotion.Hope, "维系士气期待", 1.8f, true)],
            ["诗人"] = [(Emotion.Sorrow, "悲剧命运咏叹", 2.4f, true), (Emotion.Hope, "破晓诗行憧憬", 1.6f, false), (Emotion.Fear, "超自然威压战栗", 1.4f, false)],
            ["考古学家"] = [(Emotion.Joy, "古文献发现狂喜", 2.5f, false), (Emotion.Hope, "契约破译热情", 2.2f, true), (Emotion.Fear, "古代禁忌敬畏", 1.0f, false)],
            ["临床医生"] = [(Emotion.Devotion, "救死扶伤职责", 2.6f, true), (Emotion.Serenity, "医疗安神调养", 2.2f, true), (Emotion.Fear, "伤亡危机担忧", 0.8f, false)],
            ["机械工程师"] = [(Emotion.Serenity, "机械联锁掌控", 2.2f, true), (Emotion.Hope, "管网恢复信心", 1.6f, true), (Emotion.Anger, "设施过载焦躁", 0.9f, false)],
            ["化学家"] = [(Emotion.Joy, "试样反应兴奋", 2.1f, false), (Emotion.Hope, "催化中和构想", 1.8f, true), (Emotion.Fear, "毒理失控警惕", 0.7f, false)],
            ["神偷"] = [(Emotion.Fear, "潜行危机直觉", 2.0f, true), (Emotion.Hope, "暗道逃脱盘算", 1.7f, true), (Emotion.Serenity, "阴影隐匿从容", 1.2f, false)],
            ["舞蹈艺术家"] = [(Emotion.Joy, "身法节拍律动", 2.2f, false), (Emotion.Hope, "共舞突破希冀", 1.8f, true), (Emotion.Serenity, "肢体平衡从容", 1.5f, true)],
            ["画家"] = [(Emotion.Serenity, "色彩凝视沉静", 2.0f, true), (Emotion.Sorrow, "废墟光影感伤", 1.4f, false), (Emotion.Hope, "真实重绘追求", 1.6f, true)],
            ["建筑师"] = [(Emotion.Serenity, "结构力学沉着", 2.1f, true), (Emotion.Hope, "防御工事规划", 1.7f, true), (Emotion.Fear, "坍塌危险排查", 0.8f, false)],
            ["博物学家"] = [(Emotion.Joy, "活体样本好奇", 2.0f, false), (Emotion.Hope, "生态突围推断", 1.6f, true), (Emotion.Fear, "未知异种防备", 1.1f, false)],
            ["音乐家"] = [(Emotion.Serenity, "声波谐振宁静", 2.0f, true), (Emotion.Sorrow, "寂静封锁感怀", 1.5f, false), (Emotion.Hope, "共鸣解密向往", 1.7f, true)]
        };
}

/// <summary>
/// 学员随身普通实用道具规格记录。
/// </summary>
public record CharacterItemSpec(
    string Profession,
    string Name,
    string Kind,
    IReadOnlyDictionary<string, int> Tags,
    string Content,
    bool IsHeld);

/// <summary>
/// 场景常设物品与环境陈设规格记录。
/// </summary>
public record SceneItemSpec(
    string SceneName,
    string Name,
    string Kind,
    IReadOnlyDictionary<string, int> Tags,
    string Content);

