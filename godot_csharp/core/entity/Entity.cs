using DualOntologyWorldEngine.Core.Rules;

namespace DualOntologyWorldEngine.Core;

#region 核心枚举与常数定义

/// <summary>表示一个存在所携带的源质（Aspect）。</summary>
/// <remarks>
/// 源质描述物体、人物、地点、事件或其他存在在某种超自然原则上的象征力量。
/// 每种源质通常具有对应的强度值，并可被仪式、言灵、规则或其他机制作为条件、材料贡献或作用依据。
/// 我们的引擎要求所有属性均必须转换为一种源质的投影，以便在世界中进行统一的处理。
/// </remarks>
public enum Aspect
{
    /// <summary>
    /// 形体之力。
    /// 象征实体的物质形态、身体、结构、完整性以及其作为具体存在的力量。
    /// </summary>
    Form,

    /// <summary>
    /// 名称之力。
    /// 象征名字、身份、称谓以及“某物为何物”这一语义上的确定性。
    /// </summary>
    Name,

    /// <summary>
    /// 法则之力。
    /// 象征规则、秩序、权威、约束以及使事物按照既定原则运行的力量。
    /// </summary>
    Law,

    /// <summary>
    /// 心智之力。
    /// 象征意识、思维、认知、记忆、意志以及主体精神活动的力量。
    /// </summary>
    Mind,

    /// <summary>
    /// 联结之力。
    /// 象征人与人、人与物、事物彼此之间形成的关系、契约、归属与因果联系。
    /// </summary>
    Bond,

    /// <summary>
    /// 阈限之力。
    /// 象征边界、门槛、通路、内外之分，以及跨越不同状态或领域的力量。
    /// </summary>
    Threshold,

    /// <summary>
    /// 转化之力。
    /// 象征变化、炼成、重塑、性质转换以及使存在从一种状态转为另一种状态的力量。
    /// </summary>
    Transmutation,

    /// <summary>
    /// 守存之力。
    /// 象征保存、固定、维持、封存以及抵抗变化和消亡的力量。
    /// </summary>
    Preservation,

    /// <summary>
    /// 死亡之力。
    /// 象征死亡、终结、腐朽、失去生命以及存在走向终止的力量。
    /// </summary>
    Death,

    /// <summary>
    /// 欲望之力。
    /// 象征渴求、执念、诱惑、追求以及驱使主体趋向某一目标的力量。
    /// </summary>
    Desire,

    /// <summary>
    /// 争斗之力。
    /// 象征冲突、竞争、伤害、征服、破坏以及存在之间直接对抗的力量。
    /// </summary>
    Strife,

    /// <summary>
    /// 残迹之力。
    /// 象征过去遗留下来的痕迹、记忆、遗物、余响，以及已经消逝之物仍留存于世界中的力量。
    /// </summary>
    Vestige
}

/// <summary>表示主体可能具有的情感类型。</summary>
/// <remarks>
/// Emotion 描述主体实际存在的情感状态。
/// 情感通常具有强度，并且可以指向特定人物、物体、事件、概念或其他对象。
///
/// Emotion 本身不是 Aspect。
/// 情感类型、强度、指向对象以及其他上下文可以共同参与 Aspect 投影。
///
/// 具体投影关系与倍率由独立规则定义。
/// </remarks>
public enum Emotion
{
    /// <summary>
    /// 爱。
    /// 表示主体希望接近、维系、保护或与对象保持深层联系的情感。
    /// </summary>
    Love,

    /// <summary>
    /// 憎恨。
    /// 表示主体对对象持续存在的强烈敌意、排斥及伤害倾向。
    /// </summary>
    Hatred,

    /// <summary>
    /// 恐惧。
    /// 表示主体面对危险、未知、威胁或潜在损失时产生的回避与自我保护情感。
    /// </summary>
    Fear,

    /// <summary>
    /// 愤怒。
    /// 表示主体面对阻碍、冒犯、伤害或不公时产生的强烈对抗性情感。
    /// </summary>
    Anger,

    /// <summary>
    /// 喜悦。
    /// 表示主体因满足、安全、成功或积极经历而产生的正向情感。
    /// </summary>
    Joy,

    /// <summary>
    /// 悲伤。
    /// 表示主体面对失去、破裂、失败或不可恢复之事时产生的哀痛情感。
    /// </summary>
    Sorrow,

    /// <summary>
    /// 希望。
    /// 表示主体认为未来仍存在值得追求的积极可能，并期待状态得到改善。
    /// </summary>
    Hope,

    /// <summary>
    /// 绝望。
    /// 表示主体认为重要目标、未来可能或恢复途径已经严重受损或不存在。
    /// </summary>
    Despair,

    /// <summary>
    /// 悔恨。
    /// 表示主体因自身过去的行为及其后果产生负面评价，并希望进行修正或补偿。
    /// </summary>
    Remorse,

    /// <summary>
    /// 忠诚或奉献。
    /// 表示主体持续地将自身行为、利益或意志投入某个人、组织、原则或目标。
    /// </summary>
    Devotion,

    /// <summary>
    /// 执念。
    /// 表示主体对某个对象、目标或观念形成持续、强烈且难以放弃的追求。
    /// </summary>
    Obsession,

    /// <summary>
    /// 宁静。
    /// 表示主体处于稳定、平衡、自控并具有较低内在冲突的情感状态。
    /// </summary>
    Serenity
}

/// <summary>表示信息在主体认知中的处理阶段。</summary>
/// <remarks>
/// KnowledgeStage 描述一项信息从进入主体认知，到被理解并最终被接受的过程。
/// 阶段越高，表示该信息已经经过越深入的认知处理。
///
/// 此阶段描述的是主体对信息的认知状态，
/// 不表示该信息在客观世界中必然为真。
/// </remarks>
public enum KnowledgeStage
{
    /// <summary>
    /// 已接收。
    /// 表示主体已经获得该信息，但尚未充分理解或认可其内容。
    /// </summary>
    Received,

    /// <summary>
    /// 已理解。
    /// 表示主体已经理解该信息所表达的内容、关系或意义。
    /// 理解并不意味着主体认可该信息为真。
    /// </summary>
    Understood,

    /// <summary>
    /// 已接受。
    /// 表示主体已经将该信息纳入自身认知体系，
    /// 并将其视为有效事实、判断或信念。
    /// 这仍然不保证其与客观事实一致。
    /// </summary>
    Accepted
}

/// <summary>表示实体可以被作为何种对象使用的功能角色。</summary>
/// <remarks>
/// Role 描述实体在世界交互、制作、信息处理、社会活动或超自然作用中
/// “可以被当作什么使用”。
///
/// Role 本身没有强度，不描述实体是什么，也不描述实体当前处于什么状态。
/// 一个实体可以同时具有多个 Role。
///
/// Role 仅表示一种可用语义。实体在具体行为中是否能够发挥该作用，
/// 以及作用效果有多强，仍应由其 Quality、Capability、Relation、状态、
/// 当前环境及相关规则共同决定。
///
/// 例如，血液可以同时作为 Material、MarkingMedium、Reagent 与 RitualMaterial；
/// 一枚官方印章可以同时作为 Tool、Credential、AccessToken 与 RitualImplement。
/// </remarks>
public enum Role
{
    /// <summary>
    /// 工具。
    /// 表示实体可以被用于辅助主体完成某种操作或任务。
    /// 具体能够执行何种操作由其 Capability 及其他性质决定。
    /// </summary>
    Tool,

    /// <summary>
    /// 武器。
    /// 表示实体可以被用于攻击、伤害、威胁或进行直接对抗。
    /// 不规定其具体攻击方式或伤害强度。
    /// </summary>
    Weapon,

    /// <summary>
    /// 容器。
    /// 表示实体可以被用于容纳、保存、运输、包围或隔离其他实体。
    /// </summary>
    Container,

    /// <summary>
    /// 通行控制物。
    /// 表示实体可以被用于允许、阻止、限制或改变对象通过某个边界、
    /// 入口或受控区域。
    ///
    /// 门、闸门、栅栏、封印以及某些结界均可承担此角色。
    /// </summary>
    AccessControl,

    /// <summary>
    /// 通行凭据。
    /// 表示实体可以被用于满足、解除或绕过某种访问限制。
    ///
    /// 钥匙、门卡、令牌、权限凭证以及某些特殊印记均可承担此角色。
    /// </summary>
    AccessToken,

    /// <summary>
    /// 记录媒介。
    /// 表示实体可以被用于承载、保存、传播或表现信息。
    ///
    /// 纸张、书籍、照片、磁盘、碑石以及其他信息载体均可承担此角色。
    /// </summary>
    RecordMedium,

    /// <summary>
    /// 留痕媒介。
    /// 表示实体可以被用于在其他实体、表面或空间中留下文字、图案、
    /// 轨迹、符号或其他可识别痕迹。
    ///
    /// 墨水、染料、颜料、粉笔、血液、灰烬以及某些粉末均可承担此角色。
    /// 这些痕迹也可以进一步被规则解释为文字、标记、符号或仪式阵法。
    /// </summary>
    MarkingMedium,

    /// <summary>
    /// 凭证。
    /// 表示实体可以被用于证明身份、资格、许可、权限、地位
    /// 或其他被某套规则承认的事实。
    ///
    /// 身份证件、许可证、徽章、通行证、任命文书等均可承担此角色。
    /// </summary>
    Credential,

    /// <summary>
    /// 证据。
    /// 表示实体可以被用于支持、证明、质疑或反驳某项事实、主张或判断。
    ///
    /// 具有此角色并不意味着实体能够证明任意事实；
    /// 实际证明对象仍由其内容、来源、Relation 及相关规则决定。
    /// </summary>
    Evidence,

    /// <summary>
    /// 材料。
    /// 表示实体可以被用于制造、加工、构成、修复或改造其他实体。
    /// </summary>
    Material,

    /// <summary>
    /// 组件。
    /// 表示实体可以被用于作为另一个实体、装置或系统的组成部分。
    ///
    /// 组件通常在参与更大结构后发挥作用，但并不要求其失去独立实体身份。
    /// </summary>
    Component,

    /// <summary>
    /// 粘结媒介。
    /// 表示实体可以被用于连接、固定、封合或维持多个实体之间的结合。
    ///
    /// 胶、树脂、砂浆、焊料以及其他具有类似用途的物质均可承担此角色。
    /// </summary>
    BindingMedium,

    /// <summary>
    /// 反应材料。
    /// 表示实体可以被用于参与某种反应、加工、炼制、炼金或转化过程。
    ///
    /// 该角色仅表示其可以作为反应参与物使用，
    /// 不规定反应结果或具体反应性质。
    /// </summary>
    Reagent,

    /// <summary>
    /// 催化物。
    /// 表示实体可以被用于促成、加速、允许或改变某个过程，
    /// 而不要求其作为该过程的主要消耗材料。
    /// </summary>
    Catalyst,

    /// <summary>
    /// 光源。
    /// 表示实体可以被用于提供照明或其他可利用的光。
    ///
    /// 实际光照范围、亮度及作用效果由相关 Quality、Capability
    /// 与当前状态决定。
    /// </summary>
    LightSource,

    /// <summary>
    /// 能量源。
    /// 表示实体可以被用于向其他实体、设备或过程提供可利用能量。
    ///
    /// 不规定能源形式、输出强度或供能方式。
    /// </summary>
    EnergySource,

    /// <summary>
    /// 燃料。
    /// 表示实体可以通过消耗自身，为某个设备、反应或过程提供能量。
    /// </summary>
    Fuel,

    /// <summary>
    /// 消耗品。
    /// 表示实体可以通过使用而被消耗、减少、耗尽或发生不可逆改变。
    ///
    /// 该角色不规定实体因何种行为被消耗。
    /// </summary>
    Consumable,

    /// <summary>
    /// 穿戴物。
    /// 表示实体可以被人物或其他适合的实体穿戴、佩戴、装备或附着于自身。
    /// </summary>
    Wearable,

    /// <summary>
    /// 货币或交换媒介。
    /// 表示实体可以被用于支付、交换、计价或履行具有经济意义的义务。
    ///
    /// 其实际价值与接受范围由社会关系、制度及当前环境决定。
    /// </summary>
    Currency,

    /// <summary>
    /// 仪式材料。
    /// 表示实体可以在满足具体仪式规则时，
    /// 被作为仪式过程中的材料、媒介或投入物使用。
    ///
    /// 具有此角色不代表该实体适用于所有仪式，
    /// 也不代表其 Aspect 必然能够参与当前仪式结算。
    /// 具体资格仍由仪式自身的条件与来源规则决定。
    /// </summary>
    RitualMaterial,

    /// <summary>
    /// 仪式器具。
    /// 表示实体可以被用于执行、引导、组织、约束或维持仪式过程。
    ///
    /// 仪式器具通常承担操作性作用，
    /// 与作为仪式投入或被消耗的 RitualMaterial 相区别。
    /// </summary>
    RitualImplement,

    /// <summary>
    /// 祭品。
    /// 表示实体可以被用于向某个主体、规则、超自然存在或仪式
    /// 主动交付、放弃、损毁或消耗，以构成代价、交换或献祭。
    ///
    /// 是否构成有效祭品仍由具体规则决定。
    /// </summary>
    Offering,

    /// <summary>
    /// 遗物。
    /// 表示实体可以因其历史、来源、曾经的持有者、关联对象
    /// 或参与过的重要事件，而被作为具有历史或象征意义的对象使用。
    ///
    /// Relic 描述的是其可被作为“遗物”使用的语义角色，
    /// 具体能够产生何种 Aspect 仍由 Relation、历史事实及投影规则决定。
    /// </summary>
    Relic
}

/// <summary>表示实体之间可以通过 Facet 表达的轻量关系语义。</summary>
/// <remarks>
/// RelationFacet 描述一条依附于实体的关系陈述。
///
/// 关系通常以当前实体作为关系主体，并以 Facet.Value 保存关系所指向的对象。
///
/// 例如：
/// 某把剑具有 Owner 关系且 Value 为 Alice，
/// 表示“这把剑的所有者是 Alice”。
///
/// RelationFacet 本身只描述关系的含义，
/// 不规定该关系如何产生 Aspect，也不保证该关系在所有上下文中有效。
/// </remarks>
public enum FacetType
{
    /// <summary>所有关系。表示当前实体属于、受控于或被指定对象所拥有。</summary>
    Owner,

    /// <summary>血缘关系。表示当前实体与指定对象之间存在血统或亲缘上的联系。</summary>
    Bloodline,

    /// <summary>见证关系。表示当前实体曾直接见证、观察或经历与指定对象有关的事实或事件。</summary>
    Witness,

    /// <summary>描绘关系。表示当前实体以图像、雕塑、影像、符号或其他形式描绘指定对象。</summary>
    Depicts,

    /// <summary>记录关系。表示当前实体保存、记载或承载了关于指定对象的信息或事实。</summary>
    RecordOf,

    /// <summary>制作关系。表示当前实体由指定对象制造、创作、建造或直接形成。</summary>
    MadeBy,

    /// <summary>伤害关系。表示当前实体曾对指定对象造成伤害、损坏或其他敌对作用。</summary>
    Harmed,

    /// <summary>签发关系。表示当前实体由指定对象以其身份、职位或权威正式签发。</summary>
    IssuedBy,

    /// <summary>授予关系。表示当前实体能够向指定对象授予权限、资格、身份、地位或其他受规则承认的权利。</summary>
    Grants,

    /// <summary>解锁关系。表示当前实体能够解除指定对象所代表的锁定、封闭、通行限制或访问条件。</summary>
    Unlocks,

    /// <summary>真名关系。表示当前实体所承载的名称是指定对象的真实名称，并与该对象形成直接且明确的身份对应。</summary>
    TrueNameOf,

    /// <summary>意识主体绑定关系。表示当前角色身体实体与指定的灵魂认知主体（Soul）形成双向绑定关系。</summary>
    Soul,

    /// <summary>躯体角色绑定关系。表示当前灵魂认知主体所驱使并投射意识的角色身体实体（Character）。</summary>
    Character,

    /// <summary>辖域或作用域关系。表示规则、事件、特权、结界或禁令所约束和适用的场景、区域或概念空间。</summary>
    Domain,

    /// <summary>来源关系。表示规则补丁、状态影响、言灵效果或实体生成的原始出处、机制发起端或授权源。</summary>
    Source,

    /// <summary>行动主体关系。表示某一事件、动作行为、契约履行或状态变迁的主动发起者实体。</summary>
    Actor,

    /// <summary>因果起因关系。表示某一事件、损害事实或状态变迁的直接致因、诱发实体或前置事件。</summary>
    CausedBy,

    /// <summary>代价支付关系。表示在特定行为、仪式、言灵或契约履行中所消耗、让渡或献祭的目标代价。</summary>
    PaidFor,

    /// <summary>证据支持关系。表示当前实体作为物证、陈述或见证事实，直接支持、证明或关联的目标命题或断言。</summary>
    Evidence,

    /// <summary>论及对象关系。表示当前记录载体、知识实体、情报或命题陈述所讨论、记录或指向的目标客体。</summary>
    About,

    /// <summary>自定义扩展关系。表示未被预设枚举覆盖的用户定义或模组特定轻量语义关系。</summary>
    Custom
}

/// <summary>常见通用世界与状态标签枚举。</summary>
/// <remarks>
/// 用于替代魔法字符串标签，提供编译期强类型约束与 IDE 自动补全。
/// </remarks>
public enum CommonTag
{
    /// <summary>实体承担角色身体功能。</summary>
    Character,

    /// <summary>实体承担认知与意识主体功能。</summary>
    Soul,

    /// <summary>场景空间节点。</summary>
    Scene,

    /// <summary>拓扑通道节点。</summary>
    Passage,

    /// <summary>门实体。</summary>
    Door,

    /// <summary>可开合物（如门、箱、盒、抽屉）。</summary>
    Openable,

    /// <summary>容器实体（可容纳子实体）。</summary>
    Container,

    /// <summary>可加锁物。</summary>
    Lockable,

    /// <summary>处于锁定阻断状态。</summary>
    Locked,

    /// <summary>动态影响或临时状态（带时效与衰减）。</summary>
    Influence,

    /// <summary>认知知识实体。</summary>
    Knowledge,

    /// <summary>信息与文本载体记录。</summary>
    Record,

    /// <summary>处于发光/已照明状态。</summary>
    Lit,

    /// <summary>处于黑暗无照明状态。</summary>
    Dark,

    /// <summary>可被主体进行详细勘察。</summary>
    Examinable,

    /// <summary>可被主体阅读提取文本声明。</summary>
    Readable,

    /// <summary>可被拾取并放入背包携带。</summary>
    Portable,

    /// <summary>知识已通过主体验证并正式采信纳入认知环境。</summary>
    Accepted,

    /// <summary>知识已被主体审视并确定拒绝/证伪。</summary>
    Rejected
}

#endregion

#region 切面语义事实

/// <summary>表示依附于实体的轻量语义关系与功能切面事实。</summary>
/// <remarks>
/// Facet 描述依附在宿主实体上的单一语义命题、定向关系、条件声明或能力切面。
///
/// 核心哲学与约束：
/// 1. 轻量性：Facet 自身不是世界实体（Entity），不继承 <see cref="TreeMember"/>，没有父子层级、槽位与独立生命周期。
/// 2. 对象引用：在表达关系时，直接通过 <see cref="Value"/> 持有目标实体对象引用，绝不额外分配或维护间接寻址 ID。
/// 3. 条件生效：可通过 <see cref="When"/> 谓词指定激活条件（如特定观察视界、持有光照或处于特定状态时才生效）。
/// 4. 来源溯源：通过 <see cref="Source"/> 记录赋予该关系的机制或实体，用于审计追踪与防止重复叠加。
/// 5. 实体晋升边界：若一段关系本身需要被独立破坏、转移、挂载子物品或单独到期流转，应将其提升为独立实体，而非使用 Facet。
/// </remarks>
public sealed class Facet
{
    /// <summary>切面或关系的字符串键名。</summary>
    public string Key { get; }

    /// <summary>强类型语义关系类型。</summary>
    public FacetType Type { get; } = FacetType.Custom;

    /// <summary>切面所持有的目标值（实体对象、表达式、规则或委托闭包等）。直接持有对象引用。</summary>
    public object Value { get; }

    /// <summary>若 Value 是 Entity，便捷强类型访问。</summary>
    public Entity? TargetEntity => Value as Entity;

    /// <summary>关系或切面强度，数值越高表示联结越紧密或效力越强。</summary>
    public int Strength { get; }

    /// <summary>关系的来源或赋予者，用于溯源与防止重复结算。</summary>
    public object? Source { get; }

    /// <summary>条件激活判定谓词。签名为 (owner, context) => bool。</summary>
    public Func<Entity, IReadOnlyDictionary<string, object>?, bool>? When { get; }

    /// <summary>初始化轻量切面事实。</summary>
    public Facet(
        string key,
        object value,
        int strength = 1,
        Func<Entity, IReadOnlyDictionary<string, object>?, bool>? when = null,
        object? source = null)
    {
        Key = key;
        Value = value;
        Strength = strength;
        When = when;
        Source = source;
        if (Enum.TryParse<FacetType>(key, true, out var parsed))
        {
            Type = parsed;
        }
    }

    /// <summary>初始化强类型语义关系切面。</summary>
    public Facet(FacetType type, Entity value, int strength = 1, object? source = null)
        : this(type.ToString(), value, strength, null, source)
    {
        Type = type;
    }

    /// <summary>生成切面的调试诊断字符串。</summary>
    public override string ToString() =>
        $"Facet({Key} -> {(Value is Entity e ? e.Name : Value)}, Strength={Strength}, Source={Source})";
}

#endregion

#region 世界树节点与世界实体

/// <summary>世界树中的节点基类。统一下沉承载 Tags、Properties 与 Facets。</summary>
public abstract class TreeMember
{
    public string Name { get; set; }
    public TreeMember? Parent { get; private set; }
    private readonly Dictionary<TreeMember, int> _children = [];
    public IReadOnlyDictionary<TreeMember, int> Children => _children;
    public IReadOnlyList<TreeMember> Items => Children.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
    public Dictionary<string, int> Tags { get; }
    public Dictionary<string, object> Properties { get; }
    public List<Facet> Facets { get; }

    protected TreeMember(string name, TreeMember? parent = null, IDictionary<string, int>? tags = null,
        int position = 0, IDictionary<string, object>? properties = null, IEnumerable<Facet>? facets = null,
        IEnumerable<Enum>? enumTags = null)
    {
        Name = name;
        Tags = tags is not null ? new(tags) : [];
        Properties = properties is not null ? new(properties) : [];
        Facets = facets is not null ? [..facets] : [];
        if (enumTags is not null) foreach (var et in enumTags) SetTag(et, 1);
        if (parent is not null) Reparent(parent, position);
    }

    protected TreeMember(string name, TreeMember? parent, int position) : this(name, parent, null, position) { }

    public TreeMember Reparent(TreeMember? newParent, int position = 0)
    {
        Parent?._children.Remove(this);
        Parent = newParent;
        if (Parent is null) return this;
        var occupied = new HashSet<int>(Parent.Children.Values);
        while (occupied.Contains(position)) position++;
        Parent._children[this] = position;
        return this;
    }

    public int GetPosition() => Parent is not null && Parent.Children.TryGetValue(this, out var pos) ? pos : -1;

    #region 标签操作
    public int Tag(string name) => Tags.GetValueOrDefault(name, 0);
    public int Tag(Enum tag) => Tag(tag.ToString());
    public void SetTag(string name, int strength) { if (strength > 0) Tags[name] = strength; else Tags.Remove(name); }
    public void SetTag(Enum tag, int strength = 1) => SetTag(tag.ToString(), strength);
    public bool HasTag(string name) => Tag(name) > 0;
    public bool HasTag(Enum tag) => Tag(tag) > 0;
    public bool Has(string name) => HasTag(name);
    public int ChangeTag(string name, int amount) { var v = Math.Max(0, Tag(name) + amount); SetTag(name, v); return v; }
    public int ChangeTag(Enum tag, int amount) => ChangeTag(tag.ToString(), amount);
    public int Change(string name, int amount) => ChangeTag(name, amount);
    public TreeMember AddTags(params Enum[] tags) { foreach (var t in tags) SetTag(t, 1); return this; }
    public bool HasRole(Role role) => HasTag(role);
    public void AddRole(Role role) => SetTag(role, 1);
    public void RemoveRole(Role role) => SetTag(role, 0);
    #endregion

    #region 属性操作
    public T? Get<T>(string name, T? defaultValue = default) =>
        Properties.TryGetValue(name, out var val) && val is T typed ? typed : defaultValue;
    public object Set(string name, object value) => Properties[name] = value;
    public bool HasProperty(string name) => Properties.ContainsKey(name);
    #endregion

    #region 切面关系
    public Facet Attach(string key, object value, int strength = 1, Func<Entity, IReadOnlyDictionary<string, object>?, bool>? when = null, object? source = null)
    {
        var f = new Facet(key, value, strength, when, source);
        Facets.Add(f);
        return f;
    }

    public IEnumerable<Facet> Entries(string key, IReadOnlyDictionary<string, object>? context = null) =>
        Facets.Where(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase) && (f.When is null || (this is Entity e && f.When(e, context))));

    public Facet Relate(FacetType type, Entity target, int strength = 1, object? source = null)
    {
        var f = new Facet(type, target, strength, source);
        Facets.Add(f);
        return f;
    }

    public Facet Relate(string kind, object target, int strength = 1, object? source = null) =>
        Attach(kind, target, strength, source: source);

    public IEnumerable<Facet> Related(FacetType type, Entity? target = null) =>
        Facets.Where(f => f.Type == type && (target is null || ReferenceEquals(f.Value, target)));

    public IEnumerable<Facet> Related(string kind, object? target = null) =>
        Facets.Where(f => string.Equals(f.Key, kind, StringComparison.OrdinalIgnoreCase) && (target is null || Equals(f.Value, target)));

    public bool Detach(Facet facet) => Facets.Remove(facet);

    public IReadOnlyList<Facet> Detach(FacetType type, Entity? target = null, object? source = null)
    {
        var matched = Facets.Where(f => f.Type == type && (target is null || ReferenceEquals(f.Value, target)) && (source is null || Equals(f.Source, source))).ToList();
        Facets.RemoveAll(matched.Contains);
        return matched;
    }

    public IReadOnlyList<Facet> Detach(string kind, object? value = null, object? source = null)
    {
        var matched = Facets.Where(f => string.Equals(f.Key, kind, StringComparison.OrdinalIgnoreCase) && (value is null || Equals(f.Value, value)) && (source is null || Equals(f.Source, source))).ToList();
        Facets.RemoveAll(matched.Contains);
        return matched;
    }
    #endregion
}

/// <summary>运行时的世界实体，具体物理、人物、场景等对象的载体。</summary>
public class Entity(
    string name,
    TreeMember? parent = null,
    IDictionary<string, int>? tags = null,
    int position = 0,
    IDictionary<string, object>? properties = null,
    IEnumerable<Facet>? facets = null,
    IEnumerable<Enum>? enumTags = null)
    : TreeMember(name, parent, tags, position, properties, facets, enumTags)
{
    public Entity(string name, TreeMember? parent, IEnumerable<Enum> enumTags, int position = 0,
        IDictionary<string, object>? properties = null, IEnumerable<Facet>? facets = null)
        : this(name, parent, null, position, properties, facets, enumTags) { }

    public new Entity AddTags(params Enum[] tags) { base.AddTags(tags); return this; }

    #region 基础物品与装备操作（供 BaseActions 交互使用）
    public bool PickUp(TreeMember actor, int position = 0) { Reparent(actor, position); return true; }
    public bool Drop(int position = 0) { if (Parent?.Parent is null) return false; Reparent(Parent.Parent, position); SetTag("Held", 0); return true; }
    public bool PutInto(Entity container, int position = 0, Entity? fromScene = null)
    {
        if (!container.Put(this, position, fromScene)) return false;
        SetTag("Held", 0);
        return true;
    }
    public bool TakeFrom(Entity container, TreeMember actor, int position = 0, Entity? fromScene = null) => container.Take(this, actor, position, fromScene);
    public bool Hold(TreeMember actor) { if (Parent != actor) Reparent(actor); SetTag("Held", 1); return true; }
    public bool Unhold() { SetTag("Held", 0); return true; }
    public Dictionary<string, object?> Examine(Entity? observer = null) => new()
    {
        ["name"] = Name,
        ["tags"] = new Dictionary<string, int>(Tags),
        ["properties"] = new Dictionary<string, object>(Properties),
        ["aspects"] = this.GetAspects(target: null, currentTime: null).ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
        ["claims"] = Entries("Claim").Select(f => f.Value).ToList(),
        ["opened"] = Get<bool?>("opened"),
        ["items"] = Items.OfType<Entity>().Select(i => i.Name).ToList()
    };
    #endregion
}

/// <summary>两场景实体之间的拓扑通道连接。</summary>
public class Passage(string name, Entity left, Entity right, double cost = 1.0, double? reverseCost = null,
    TreeMember? parent = null, int position = 0, Func<Entity, Entity?, object?, bool>? condition = null)
    : TreeMember(name, parent, position: position)
{
    public IReadOnlyList<Expr> Requirements { get; set; } = [];
    public Entity Left { get; } = left;
    public Entity Right { get; } = right;
    private readonly Dictionary<Entity, double> _costs = new() { [left] = cost, [right] = reverseCost ?? cost };
    private readonly Dictionary<Entity, string> _directionalDesignations = [];
    public Func<Entity, Entity?, object?, bool>? Condition { get; set; } = condition;

    public (Entity Left, Entity Right) Endpoints => (Left, Right);
    public Entity? Other(Entity scene) => ReferenceEquals(scene, Left) ? Right : ReferenceEquals(scene, Right) ? Left : null;
    public void SetDesignation(Entity scene, string designation) => _directionalDesignations[scene] = designation;
    public string GetDesignationFrom(Entity scene) => _directionalDesignations.GetValueOrDefault(scene, Name);
    public double CostFrom(Entity scene) => _costs.GetValueOrDefault(scene, double.PositiveInfinity);

    public bool Allows(Entity scene, Entity? actor = null, object? env = null)
    {
        if (Other(scene) is null) return false;
        var doors = Children.Keys.OfType<Entity>().Where(e => e.HasTag("Door"));
        if (doors.Any(door => !door.IsAccessible(scene))) return false;
        return Condition is null || Condition(scene, actor, env);
    }
}

#endregion
