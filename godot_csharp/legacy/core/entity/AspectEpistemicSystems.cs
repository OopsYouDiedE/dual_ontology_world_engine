namespace DualOntologyWorldEngine.Core;

#region 核心枚举与定义

/// <summary>客观物理与概念性质枚举，用于消除源质投影计算中的弱类型字符串匹配与类型嗅探。</summary>
public enum PhysicalProperty
{
    /// <summary>硬度（抗形变能力）。</summary>
    Hardness,

    /// <summary>耐久度（耐损耗程度）。</summary>
    Durability,

    /// <summary>锋利度（切割与伤害潜能）。</summary>
    Sharpness,

    /// <summary>渗透度 / 通透性（边界穿越潜能）。</summary>
    Permeability,

    /// <summary>反应性（化学或转化潜能）。</summary>
    Reactivity,

    /// <summary>生命力（生物活力与守存）。</summary>
    Vitality,

    /// <summary>致命性（致死杀伤力）。</summary>
    Lethality,

    /// <summary>辨识度（象征与名号显赫度）。</summary>
    Recognition,

    /// <summary>权威度（法则秩序约束力）。</summary>
    Authority,

    /// <summary>认知度（心智与智慧沉淀）。</summary>
    Cognition,

    /// <summary>亲和力（联结与因果吸引）。</summary>
    Affinity,

    /// <summary>动机 / 欲望驱动力。</summary>
    Motivation,

    /// <summary>冲突性（对抗与争斗倾向）。</summary>
    Conflict,

    /// <summary>古老度（历史残迹与岁月沉淀）。</summary>
    Antiquity
}

#endregion

#region 源质（Aspect）投影计算系统

/// <summary>源质贡献记录。</summary>
/// <param name="Source">提供此源质力量的实体或客观对象引用。</param>
/// <param name="Rule">产生此项贡献的投影规则标识（如 "intrinsic", "material", "property:sharpness", "relation:Witness"）。</param>
/// <param name="Aspect">贡献的源质类型。</param>
/// <param name="Strength">贡献的有效强度值。</param>
public readonly record struct AspectContribution(
    object Source,
    string Rule,
    Aspect Aspect,
    int Strength);

/// <summary>源质（Aspect）投影与超自然力量计算系统。</summary>
/// <remarks>
/// 源质描述实体在十二项超自然存在原则上的象征与干涉力量。
/// 源质不是静态硬编码的绝对数值，而是单次行动或交互时从客观实体中无副作用实时投影的结果。
/// 
/// 结算原则：
/// 1. 固有性（Intrinsic）：实体自身携带的无条件 Aspect 标签。
/// 2. 物质传承（Material）：从构成材料实体按完整度（Integrity）比例传承源质。
/// 3. 物性投影（Property）：硬度、锋利度、反应性等客观物理性质按常数规则投影。
/// 4. 关系定向（Relation）：指向目标实体的既成事实关系（所有、见证、描绘、伤害等）定向产生源质。
/// 5. 情感浸润（Emotion/Influence）：依附于实体的情感或动态影响随时间衰减后投影。
/// 
/// 源质计算纯函数化，无修改实体状态的副作用。
/// </remarks>
public static class AspectSystem
{
    /// <summary>14 项物性到源质的投影规则映射。(物性, 规则名, 目标源质与乘除数)</summary>
    private static readonly (PhysicalProperty Property, string RuleName, (Aspect Aspect, int Multiplier, int Divisor)[] Rules)[] PropertyProjections =
    [
        (PhysicalProperty.Hardness, "property:hardness", [(Aspect.Form, 1, 2), (Aspect.Preservation, 1, 4)]),
        (PhysicalProperty.Durability, "property:durability", [(Aspect.Preservation, 1, 1), (Aspect.Form, 1, 3)]),
        (PhysicalProperty.Sharpness, "property:sharpness", [(Aspect.Strife, 1, 1), (Aspect.Death, 1, 3)]),
        (PhysicalProperty.Permeability, "property:permeability", [(Aspect.Threshold, 1, 1)]),
        (PhysicalProperty.Reactivity, "property:reactivity", [(Aspect.Transmutation, 1, 1)]),
        (PhysicalProperty.Vitality, "property:vitality", [(Aspect.Preservation, 1, 1)]),
        (PhysicalProperty.Lethality, "property:lethality", [(Aspect.Death, 1, 1), (Aspect.Strife, 1, 2)]),
        (PhysicalProperty.Recognition, "property:recognition", [(Aspect.Name, 1, 1)]),
        (PhysicalProperty.Authority, "property:authority", [(Aspect.Law, 1, 1), (Aspect.Name, 1, 2)]),
        (PhysicalProperty.Cognition, "property:cognition", [(Aspect.Mind, 1, 1)]),
        (PhysicalProperty.Affinity, "property:affinity", [(Aspect.Bond, 1, 1)]),
        (PhysicalProperty.Motivation, "property:motivation", [(Aspect.Desire, 1, 1)]),
        (PhysicalProperty.Conflict, "property:conflict", [(Aspect.Strife, 1, 1)]),
        (PhysicalProperty.Antiquity, "property:antiquity", [(Aspect.Vestige, 1, 1)])
    ];

    /// <summary>物性到源质的投影规则映射只读字典。</summary>
    public static readonly IReadOnlyDictionary<PhysicalProperty, (Aspect Aspect, int Multiplier, int Divisor)[]> PropertyAspects =
        PropertyProjections.ToDictionary(p => p.Property, p => p.Rules);

    private static readonly (Aspect Aspect, string TagName)[] AspectTagCache =
    [
        (Aspect.Form, nameof(Aspect.Form)),
        (Aspect.Name, nameof(Aspect.Name)),
        (Aspect.Law, nameof(Aspect.Law)),
        (Aspect.Mind, nameof(Aspect.Mind)),
        (Aspect.Bond, nameof(Aspect.Bond)),
        (Aspect.Threshold, nameof(Aspect.Threshold)),
        (Aspect.Transmutation, nameof(Aspect.Transmutation)),
        (Aspect.Preservation, nameof(Aspect.Preservation)),
        (Aspect.Death, nameof(Aspect.Death)),
        (Aspect.Desire, nameof(Aspect.Desire)),
        (Aspect.Strife, nameof(Aspect.Strife)),
        (Aspect.Vestige, nameof(Aspect.Vestige))
    ];

    /// <summary>
    /// 实体语义关系到源质的定向投影强度映射表。
    /// 仅在交互目标与关系目标匹配时生效。
    /// </summary>
    private static readonly Dictionary<FacetType, (Aspect Aspect, int Multiplier)[]> RelationAspects = new()
    {
        [FacetType.Owner] = [(Aspect.Bond, 1), (Aspect.Name, 1)],
        [FacetType.Bloodline] = [(Aspect.Bond, 1)],
        [FacetType.Witness] = [(Aspect.Bond, 1), (Aspect.Law, 1), (Aspect.Vestige, 1)],
        [FacetType.Depicts] = [(Aspect.Bond, 1), (Aspect.Vestige, 1)],
        [FacetType.RecordOf] = [(Aspect.Vestige, 1)],
        [FacetType.MadeBy] = [(Aspect.Bond, 1), (Aspect.Vestige, 1)],
        [FacetType.Harmed] = [(Aspect.Bond, 1), (Aspect.Strife, 1), (Aspect.Vestige, 1)],
        [FacetType.IssuedBy] = [(Aspect.Law, 1), (Aspect.Name, 1)],
        [FacetType.Grants] = [(Aspect.Law, 1)],
        [FacetType.Unlocks] = [(Aspect.Threshold, 1), (Aspect.Bond, 1)],
        [FacetType.TrueNameOf] = [(Aspect.Name, 3), (Aspect.Bond, 1)],
    };

    /// <summary>情感类型到源质的投影规则。(主要全额源质, 次要半额源质)</summary>
    private static readonly Dictionary<Emotion, (Aspect[] Primary, Aspect[] Secondary)> EmotionAspects = new()
    {
        [Emotion.Love] = ([Aspect.Desire, Aspect.Bond], [Aspect.Preservation]),
        [Emotion.Hatred] = ([Aspect.Desire, Aspect.Strife], [Aspect.Death]),
        [Emotion.Fear] = ([Aspect.Desire, Aspect.Preservation], [Aspect.Threshold]),
        [Emotion.Anger] = ([Aspect.Desire, Aspect.Strife], [Aspect.Form]),
        [Emotion.Joy] = ([Aspect.Desire, Aspect.Preservation], [Aspect.Bond]),
        [Emotion.Sorrow] = ([Aspect.Death, Aspect.Vestige], [Aspect.Bond]),
        [Emotion.Hope] = ([Aspect.Desire, Aspect.Preservation], [Aspect.Threshold]),
        [Emotion.Despair] = ([Aspect.Death, Aspect.Desire], [Aspect.Mind]),
        [Emotion.Remorse] = ([Aspect.Vestige, Aspect.Law], [Aspect.Bond]),
        [Emotion.Devotion] = ([Aspect.Bond, Aspect.Law], [Aspect.Preservation]),
        [Emotion.Obsession] = ([Aspect.Desire, Aspect.Preservation], [Aspect.Mind]),
        [Emotion.Serenity] = ([Aspect.Mind, Aspect.Preservation], [Aspect.Law]),
    };

    /// <summary>计算指定实体的所有源质贡献明细。</summary>
    /// <param name="entity">计算主体实体。</param>
    /// <param name="target">当前交互或施法所指向的目标实体。关系型源质仅在目标匹配时生效。</param>
    /// <param name="currentTime">当前全局行动值时间，用于计算情感动态衰减。</param>
    /// <returns>源质贡献明细清单，保留来源溯源信息。</returns>
    public static IReadOnlyList<AspectContribution> GetAspectContributions(
        this Entity entity,
        Entity? target = null,
        double? currentTime = null)
    {
        var contributions = new List<AspectContribution>();

        // 1. 固有源质（Intrinsic Aspect Tags）
        foreach (var (aspect, tagName) in AspectTagCache)
        {
            var strength = entity.Tag(tagName);
            if (strength > 0)
            {
                contributions.Add(new AspectContribution(entity, "intrinsic", aspect, strength));
            }
        }

        // 2. 材料实体继承（Material Inheritance）
        var material = entity.Get<Entity>("material");
        if (material != null)
        {
            var (numerator, denominator) = entity.GetIntegrityFraction();
            var divisor = Math.Max(1, denominator);
            foreach (var (aspect, tagName) in AspectTagCache)
            {
                var matStrength = material.Tag(tagName);
                if (matStrength > 0)
                {
                    var inherited = (matStrength * numerator) / divisor;
                    if (inherited > 0)
                    {
                        contributions.Add(new AspectContribution(material, "material", aspect, inherited));
                    }
                }
            }
        }

        // 3. 具体客观物性投影（Property Projections）
        foreach (var (property, ruleName, rules) in PropertyProjections)
        {
            var propVal = entity.GetPhysicalProperty(property);
            if (propVal > 0)
            {
                foreach (var (aspect, mul, div) in rules)
                {
                    var projected = (int)(propVal * mul / div);
                    if (projected > 0)
                    {
                        contributions.Add(new AspectContribution(entity, ruleName, aspect, projected));
                    }
                }
            }
        }

        // 4. 情感与动态影响（Influence Projections）
        if (entity.HasTag(CommonTag.Influence) && entity.Properties.TryGetValue("emotion", out var emotionObj))
        {
            Emotion? emotion = emotionObj switch
            {
                Emotion e => e,
                string s when Enum.TryParse<Emotion>(s, true, out var parsed) => parsed,
                _ => null
            };

            var effective = entity.EffectiveIntensity(currentTime);

            if (emotion is { } validEmotion && EmotionAspects.TryGetValue(validEmotion, out var rules) && effective > 0)
            {
                foreach (var primary in rules.Primary)
                {
                    var amount = (int)Math.Round(effective, MidpointRounding.AwayFromZero);
                    if (amount > 0)
                    {
                        contributions.Add(new AspectContribution(entity, $"emotion:{validEmotion}", primary, amount));
                    }
                }

                foreach (var secondary in rules.Secondary)
                {
                    var amount = (int)Math.Round(effective / 2f, MidpointRounding.AwayFromZero);
                    if (amount > 0)
                    {
                        contributions.Add(new AspectContribution(entity, $"emotion:{validEmotion}:sub", secondary, amount));
                    }
                }
            }
        }

        // 5. 定向语义关系投影（Target-specific Relation Projections）
        if (target != null)
        {
            foreach (var facet in entity.Facets)
            {
                if (ReferenceEquals(facet.Value, target) && RelationAspects.TryGetValue(facet.Type, out var rules))
                {
                    foreach (var (aspect, mul) in rules)
                    {
                        var amount = facet.Strength * mul;
                        if (amount > 0)
                        {
                            contributions.Add(new AspectContribution(
                                facet.Source ?? facet.Value,
                                $"relation:{facet.Type}",
                                aspect,
                                amount));
                        }
                    }
                }
            }
        }

        // 去重：同一来源、同一规则、同一源质仅计算一次
        var unique = new List<AspectContribution>();

        foreach (var c in contributions)
        {
            if (!unique.Any(item =>
                ReferenceEquals(item.Source, c.Source) &&
                item.Rule == c.Rule &&
                item.Aspect == c.Aspect))
            {
                unique.Add(c);
            }
        }

        return unique;
    }

    /// <summary>计算指定实体的客观源质总值映射。</summary>
    /// <param name="entity">计算主体。</param>
    /// <param name="target">交互目标。</param>
    /// <param name="currentTime">当前行动值时刻。</param>
    /// <returns>每种源质对应的累计有效强度字典。</returns>
    public static Dictionary<Aspect, int> GetAspects(
        this Entity entity,
        Entity? target = null,
        double? currentTime = null)
    {
        var totals = new Dictionary<Aspect, int>();
        var contributions = entity.GetAspectContributions(target, currentTime);

        foreach (var c in contributions)
        {
            totals[c.Aspect] = totals.GetValueOrDefault(c.Aspect, 0) + c.Strength;
        }

        return totals;
    }

    #region 辅助解析函数

    private static (int Numerator, int Denominator) GetIntegrityFraction(this Entity entity)
    {
        if (entity.Properties.TryGetValue("integrity", out var val))
        {
            if (val is ValueTuple<int, int> vt) return vt;
            if (val is int[] arr && arr.Length >= 2) return (arr[0], arr[1]);
            if (val is float f) return ((int)(f * 100), 100);
            if (val is double d) return ((int)(d * 100), 100);
        }
        return (1, 1);
    }

    #endregion
}

#endregion

#region 认知（Cognition）、观察与信念系统

/// <summary>观察所获得的认知结果。</summary>
/// <param name="Subject">被观察的主体对象。</param>
/// <param name="Designation">观察者在当前视界下所识别出的名称。</param>
/// <param name="Claims">观察所直接获取到的命题或表征列表。</param>
public record ObservationResult(
    Entity Subject,
    string Designation,
    IReadOnlyList<object> Claims);

/// <summary>声明条目引用，记录声明所属的认知实体与具体命题。</summary>
public record ClaimEntry(
    Entity Knowledge,
    object Claim);

/// <summary>认知（Cognition）、观察（Observation）与主观信念系统。</summary>
/// <remarks>
/// 核心哲学：客观世界事实与主体主观认知严格解耦。
/// 
/// 1. 主体的认知（Knowledge）本身也是作为普通 Entity 挂载在主体（如 Character 或 Soul）的世界树子节点中。
/// 2. 认知分为三个严格单向递进的阶段（KnowledgeStage）：Received（已接收） -> Understood（已理解） -> Accepted（已采信）。
/// 3. 错误、虚假或受偏见扭曲的声明（Claim）可以在主体子树中被相信并参与主观决策，但绝不会写入客观世界的 Properties 或 Tags。
/// 4. 观察（Observe）与阅读（Read）受语言能力、遮蔽条件与采信立场制约。
/// 5. 认知估计（EstimateAspects）仅汇总观察者已知来源的力量，专供 UI 展示与 AI 推演使用，不改变客观源质实际结算。
/// </remarks>
public static class EpistemicSystem
{
    private const string ClaimsKey = "_claims";
    private const string RevealsKey = "_reveals";

    #region 声明与揭示的底层存取

    /// <summary>获取实体上的声明命题列表。</summary>
    public static List<object> GetClaims(this Entity entity)
    {
        if (entity.Properties.TryGetValue(ClaimsKey, out var val) && val is List<object> list)
        {
            return list;
        }
        var newList = new List<object>();
        entity.Properties[ClaimsKey] = newList;
        return newList;
    }

    /// <summary>获取实体上配置的观察揭示（Reveal）命题列表。</summary>
    public static List<object> GetReveals(this Entity entity)
    {
        if (entity.Properties.TryGetValue(RevealsKey, out var val) && val is List<object> list)
        {
            return list;
        }
        var newList = new List<object>();
        entity.Properties[RevealsKey] = newList;
        return newList;
    }

    /// <summary>为实体注册一条可供观察揭示的命题。</summary>
    public static void AddReveal(this Entity entity, object claim)
    {
        entity.GetReveals().Add(claim);
    }

    #endregion

    #region 主体知识获取与检索

    /// <summary>在观察者子树下建立或更新一条指向 subject 的 Knowledge 实体。</summary>
    /// <param name="observer">认知主体实体（如角色或意识）。</param>
    /// <param name="subject">该项知识所指向的客观实体。</param>
    /// <param name="claims">本次获知的命题集合。</param>
    /// <param name="source">信息来源对象。</param>
    /// <param name="confidence">置信度数值。</param>
    /// <param name="stage">达到的认知阶段。</param>
    /// <param name="name">知识实体的自定义命名。</param>
    private static IEnumerable<Entity> AllKnowledge(this Entity observer)
    {
        var claims = observer.Entries(FacetType.Claim).Select(f => f.Value).OfType<Entity>();
        var childKnowledge = observer.Children.Keys.OfType<Entity>().Where(c => c.HasTag(CommonTag.Knowledge) || c.HasTag("Knowledge"));
        return claims.Concat(childKnowledge).Distinct();
    }

    /// <summary>在观察者认知事实库中建立或更新一条指向 subject 的 Knowledge 实体（非世界实体，不挂载世界树）。</summary>
    /// <param name="observer">认知主体实体（如角色或意识）。</param>
    /// <param name="subject">该项知识所指向的客观实体。</param>
    /// <param name="claims">本次获知的命题集合。</param>
    /// <param name="source">信息来源对象。</param>
    /// <param name="confidence">置信度数值。</param>
    /// <param name="stage">达到的认知阶段。</param>
    /// <param name="name">知识实体的自定义命名。</param>
    /// <returns>关联在观察者名下的 Knowledge 实体。</returns>
    public static Entity Learn(
        this Entity observer,
        Entity subject,
        IEnumerable<object>? claims = null,
        object? source = null,
        int confidence = 1,
        KnowledgeStage stage = KnowledgeStage.Understood,
        string? name = null)
    {
        // 寻找观察者关联认知中是否已有针对同一目标且来源相同的 Knowledge
        var knowledge = observer.AllKnowledge().FirstOrDefault(child =>
            (child.HasTag(CommonTag.Knowledge) || child.HasTag("Knowledge")) &&
            ReferenceEquals(child.Get<Entity>("subject"), subject) &&
            Equals(child.Get<object>("source"), source));

        if (knowledge == null)
        {
            knowledge = new Entity(
                name ?? $"关于{subject.Name}的认知",
                parent: null,
                tags: new Dictionary<string, int> { ["Knowledge"] = 1 },
                properties: new Dictionary<string, object>
                {
                    ["subject"] = subject,
                    ["source"] = source ?? observer,
                    ["confidence"] = confidence,
                    ["stage"] = stage
                });
            observer.Attach(FacetType.Claim, knowledge);
        }
        else
        {
            // 阶段只能递增，不能倒退
            var currentStage = knowledge.Get<KnowledgeStage>("stage", KnowledgeStage.Received);
            if (stage > currentStage)
            {
                knowledge.Set("stage", stage);
            }
            var currentConf = knowledge.Get<int>("confidence", 0);
            knowledge.Set("confidence", Math.Max(confidence, currentConf));
        }

        if (claims != null)
        {
            var claimList = knowledge.GetClaims();
            foreach (var claim in claims)
            {
                if (!claimList.Contains(claim))
                {
                    claimList.Add(claim);
                }
            }
        }

        if (stage == KnowledgeStage.Accepted)
        {
            knowledge.SetTag(CommonTag.Accepted, 1);
            knowledge.SetTag(CommonTag.Rejected, 0);
        }

        return knowledge;
    }

    /// <summary>查询观察者名下达到指定认知阶段、且指向 subject 的所有知识条目。</summary>
    public static IReadOnlyList<Entity> GetKnowledgeOf(
        this Entity observer,
        Entity subject,
        KnowledgeStage minStage = KnowledgeStage.Received)
    {
        return observer.AllKnowledge().Where(child =>
            (child.HasTag(CommonTag.Knowledge) || child.HasTag("Knowledge")) &&
            ReferenceEquals(child.Get<Entity>("subject"), subject) &&
            child.Get<KnowledgeStage>("stage", KnowledgeStage.Received) >= minStage).ToList();
    }

    /// <summary>检查观察者是否获知特定目标，或者是否确切获知针对它的某条特定声明。</summary>
    public static bool Knows(
        this Entity observer,
        Entity subject,
        object? claim = null,
        KnowledgeStage minStage = KnowledgeStage.Received)
    {
        var entries = observer.GetKnowledgeOf(subject, minStage);
        if (claim == null)
        {
            return entries.Count > 0;
        }

        return entries.Any(k => k.GetClaims().Contains(claim));
    }

    /// <summary>检索关于指定目标的所有声明条目，并可按采信立场与来源进行精确过滤。</summary>
    public static IReadOnlyList<ClaimEntry> ClaimsAbout(
        this Entity observer,
        Entity subject,
        KnowledgeStage minStage = KnowledgeStage.Received,
        object? source = null,
        bool? accepted = null)
    {
        var results = new List<ClaimEntry>();

        foreach (var k in observer.GetKnowledgeOf(subject, minStage))
        {
            if (source != null && !Equals(k.Get<object>("source"), source))
            {
                continue;
            }

            var isAccepted = k.HasTag("Accepted") || k.Get<KnowledgeStage>("stage") == KnowledgeStage.Accepted;
            if (accepted.HasValue && isAccepted != accepted.Value)
            {
                continue;
            }

            foreach (var claim in k.GetClaims())
            {
                results.Add(new ClaimEntry(k, claim));
            }
        }

        return results;
    }

    /// <summary>设置单条知识实体的采信立场。</summary>
    /// <param name="knowledge">Knowledge 实体。</param>
    /// <param name="stance">立场："accepted" 采信；"rejected" 证伪拒绝；"withheld" 留存暂不判断。</param>
    public static Entity SetKnowledgeStance(this Entity knowledge, string stance)
    {
        switch (stance.ToLowerInvariant())
        {
            case "accepted":
                knowledge.SetTag("Accepted", 1);
                knowledge.SetTag("Rejected", 0);
                knowledge.Set("stage", KnowledgeStage.Accepted);
                break;
            case "rejected":
                knowledge.SetTag("Rejected", 1);
                knowledge.SetTag("Accepted", 0);
                knowledge.Set("stage", KnowledgeStage.Understood);
                break;
            case "withheld":
                knowledge.SetTag("Accepted", 0);
                knowledge.SetTag("Rejected", 0);
                knowledge.Set("stage", KnowledgeStage.Understood);
                break;
        }
        return knowledge;
    }

    #endregion

    #region 观察、阅读与主观估计

    /// <summary>执行观察交互，提取公开揭示的命题，并可选地写入观察者的认知子树中。</summary>
    public static ObservationResult Observe(
        this Entity subject,
        Entity observer,
        bool learn = true)
    {
        var revealedClaims = subject.GetReveals().ToList();

        if (learn && revealedClaims.Count > 0)
        {
            observer.Learn(subject, revealedClaims, source: subject, stage: KnowledgeStage.Understood);
        }

        return new ObservationResult(subject, subject.Name, revealedClaims);
    }

    /// <summary>
    /// 阅读信息记录载体（如信件、手稿、磁盘）。
    /// 若载体设置了语言实体限制，观察者必须掌握该语言才可成功解析。
    /// </summary>
    public static IReadOnlyList<object> Read(this Entity carrier, Entity observer)
    {
        var language = carrier.Get<Entity>("language");
        if (language != null && !observer.Knows(language, minStage: KnowledgeStage.Understood))
        {
            return Array.Empty<object>();
        }

        var claims = carrier.GetClaims().ToList();
        observer.Learn(carrier, claims, source: carrier, stage: KnowledgeStage.Understood);

        // 如果该载体还承载（Carries）了附属知识实体
        foreach (var facet in carrier.Related(FacetType.RecordOf))
        {
            if (facet.Value is Entity target)
            {
                var targetClaims = target.GetClaims();
                observer.Learn(target, targetClaims, source: carrier, stage: KnowledgeStage.Understood);
                claims.AddRange(targetClaims);
            }
        }

        return claims;
    }

    /// <summary>
    /// 计算观察者视角下能够主观估计出的源质分布。
    /// 仅汇总观察者已知实体来源的贡献，不暴露未知的客观源质。
    /// </summary>
    public static Dictionary<Aspect, int> EstimateAspects(
        this Entity entity,
        Entity observer,
        Entity? target = null,
        double? currentTime = null)
    {
        var knownSubjects = new HashSet<Entity>(
            observer.AllKnowledge()
                .Where(k => k.HasTag(CommonTag.Knowledge) || k.HasTag("Knowledge"))
                .Select(k => k.Get<Entity>("subject"))
                .Where(s => s != null)!);

        var estimatedTotals = new Dictionary<Aspect, int>();
        var objectiveContributions = entity.GetAspectContributions(target, currentTime);

        foreach (var c in objectiveContributions)
        {
            // 只有当源质来源主体为观察者所知晓，或观察者显式知晓该源质声明时，才予以计入估计
            if (c.Source is Entity sourceEntity && knownSubjects.Contains(sourceEntity))
            {
                estimatedTotals[c.Aspect] = estimatedTotals.GetValueOrDefault(c.Aspect, 0) + c.Strength;
            }
        }

        return estimatedTotals;
    }

    #endregion
}

#endregion
