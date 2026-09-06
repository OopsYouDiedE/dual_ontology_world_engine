using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Timeline;

namespace DualOntologyWorldEngine.Core.Mind;

#region 物理角色身体实体 (Character)

/// <summary>承担物理世界行动与感官承载的角色身体实体。</summary>
/// <remarks>
/// Character 是实体体系中扮演物理承载者与行动执行者的具体特化。
///
/// 架构定位：
/// 1. 身心二元（Dual Ontology）：Character 表达肉体、位置、背包持有与物理交互，而 Soul 表达认知、信念与主观决策。两者通过双向 Facet 绑定。
/// 2. 空间归属：Character 挂载于场景实体（Scene）的世界树子节点中，通过向上溯源确定当前所在房间。
/// 3. 物品承载：挂载在 Character 下的非 Soul 子实体均视为角色的随身物品（Inventory）。
/// 4. 驱动解耦：单个 Character 可被多个 Soul 驱动（例如附身、双重意识），单个 Soul 也可支配多个身体。
/// </remarks>
public class Character : Entity
{
    /// <summary>与当前角色绑定的主要认知意识主体（首选支配 Soul）。</summary>
    public Soul? Mind => Souls.FirstOrDefault();

    /// <summary>当前与角色绑定的全部 Soul 实体列表。</summary>
    public IReadOnlyList<Soul> Souls =>
        Related(FacetType.Soul).Select(f => f.Value).OfType<Soul>()
              .ToList();

    /// <summary>获取角色当前随身携带的全部物品（背包）。排除认知主体（Soul）。</summary>
    public IReadOnlyList<Entity> Inventory =>
        Children.Keys.OfType<Entity>().Where(e => !e.HasTag(CommonTag.Soul)).ToList();

    /// <summary>向上溯源获取角色当前所处的场景房间实体（带有 Scene 标签的祖先实体）。</summary>
    public Entity? CurrentScene => TopologySystem.GetEnclosingScene(Parent);

    /// <summary>创建一个新的物理角色身体实体。</summary>
    /// <param name="name">角色调试名称或唯一称谓。</param>
    /// <param name="parent">所属场景或父节点。为 null 时表示脱离场景树游离状态。</param>
    /// <param name="tags">附加标签字典。系统会自动合并 Character=1 基础标签。</param>
    /// <param name="position">在父节点中的槽位序号。</param>
    public Character(
        string name,
        TreeMember? parent = null,
        IDictionary<string, int>? tags = null,
        int position = 0)
        : base(name, parent, MergeTags(tags), position: position)
    {
    }

    /// <summary>合并基础 Character 标签与外部传入的扩展标签。</summary>
    /// <param name="tags">外部自定义标签映射。</param>
    /// <returns>合并后的标签字典。</returns>
    private static Dictionary<string, int> MergeTags(IDictionary<string, int>? tags)
    {
        var result = new Dictionary<string, int> { ["Character"] = 1 };
        if (tags != null)
        {
            foreach (var (k, v) in tags) result[k] = v;
        }
        return result;
    }

    /// <summary>建立 Character 身体与 Soul 心智之间的双向对象引用关系。</summary>
    /// <param name="soul">待绑定的灵魂认知主体。</param>
    public void Bind(Soul soul)
    {
        if (!Souls.Contains(soul))
        {
            Relate(FacetType.Soul, soul);
        }
        if (!soul.Characters.Contains(this))
        {
            soul.Relate(FacetType.Character, this);
        }
    }

    /// <summary>移除 Character 身体与 Soul 心智之间的双向绑定关系。</summary>
    /// <param name="soul">待解绑的灵魂认知主体。</param>
    public void Unbind(Soul soul)
    {
        Detach(FacetType.Soul, soul);
        soul.Detach(FacetType.Character, this);
    }
}

#endregion

#region 认知心智主体 (Soul)

/// <summary>候选行动生成步骤委托。</summary>
/// <param name="soul">主导决策的认知心智主体。</param>
/// <param name="actor">执行行动的角色身体实体。</param>
/// <param name="env">当前认知求值环境。</param>
/// <param name="context">外部附加情境上下文。</param>
/// <returns>在当前环境可行的行动意图序列，无可用意图时返回 null 或空序列。</returns>
public delegate IEnumerable<Intent>? StepDelegate(
    Soul soul,
    Entity actor,
    RuleEnvironment env,
    IReadOnlyDictionary<string, object>? context);

/// <summary>主观偏好与动机打分委托。</summary>
/// <param name="soul">进行主观价值权衡的认知主体。</param>
/// <param name="intent">待评估的候选行动意图。</param>
/// <param name="env">当前主观认知环境。</param>
/// <param name="context">附加情境上下文字典。</param>
/// <returns>动机偏好评分（正分为正向激励，负分为心理抵触）。</returns>
public delegate float PreferenceDelegate(
    Soul soul,
    Intent intent,
    RuleEnvironment env,
    IReadOnlyDictionary<string, object>? context);

/// <summary>保存观察、信念、人格偏好、决策步骤与历史决策的独立认知主体。</summary>
/// <remarks>
/// Soul 是世界引擎中代表心智、意识、情绪与主观意志的核心实体。
///
/// 架构设计与哲学：
/// 1. 主客观二元严格隔离：Soul 持有的观察（<see cref="Observations"/>）、学识与规则信念（<see cref="Beliefs"/>）属于主观认知世界。即便主体采信了虚假信念，也绝不改写客观世界的真实物理性质。
/// 2. 非线性对数情绪体系：支持多个不同来源的同类情绪叠加。采用公式 \(E = \log_b(1 + \sum (b^{I_i} - 1))\) 聚合，兼顾单次强烈冲击与多重微小累积，天然抑制数值线性爆炸。
/// 3. 双周期情绪衰减：支持长效驻留情绪与短时半衰情绪的夜间结算机制。
/// 4. 动机与决策管线：通过装配行动步骤（<see cref="StepDelegate"/>）生成候选行动，结合已采信规则压力与人格偏好（<see cref="PreferenceDelegate"/>）进行多维度效用权衡。
/// </remarks>
public class Soul : Entity
{
    /// <summary>与认知绑定的主要角色物理身体（首选躯体）。</summary>
    public Character? Body => Characters.FirstOrDefault();

    /// <summary>该认知主体当前能够驱动的角色实体列表。</summary>
    public IReadOnlyList<Character> Characters =>
        Related(FacetType.Character).Select(f => f.Value).OfType<Character>()
              .ToList();

    /// <summary>处于当前认知环境中的事实观察表达式列表。</summary>
    public IReadOnlyList<Expr> Observations =>
        Entries("Observation").Select(f => f.Value).OfType<Expr>().ToList();

    /// <summary>该 Soul 持有的规则信念实体（RuleBelief）。</summary>
    public IReadOnlyList<Entity> Beliefs =>
        Children.Keys.OfType<Entity>().Where(child => child.Has("RuleBelief")).ToList();

    /// <summary>已记录的决策历史结果列表。</summary>
    public IReadOnlyList<Decision> Decisions =>
        Entries("Decision").Select(f => f.Value).OfType<Decision>().ToList();

    /// <summary>直接属于该 Soul 的稳定人格倾向实体。</summary>
    public IReadOnlyList<Entity> Dispositions =>
        Children.Keys.OfType<Entity>().Where(child => child.Has("Disposition")).ToList();

    #region 长线目标承诺（Commitment）体系

    /// <summary>当前心智所承诺推进的长期长线目标对象（实体、场景或概念）。</summary>
    public object? CurrentCommitment => Entries("CommittedTo").FirstOrDefault()?.Value;

    /// <summary>推进当前长线承诺目标时获得的动机惯性加分强度。</summary>
    public float CommitmentInertia => Entries("CommittedTo").FirstOrDefault()?.Strength ?? 0;

    /// <summary>单次连续重复动作所施加的边际效用衰减惩罚分（默认 30 分）。</summary>
    public float PenaltyPerRepetition { get; set; } = 30f;

    /// <summary>为心智确立一个长线目标承诺（CommittedTo），赋予持续推进的动机惯性。</summary>
    /// <param name="goal">目标实体或意图对象。</param>
    /// <param name="inertia">动机惯性加分强度，默认为 25 分。</param>
    public void CommitTo(object goal, int inertia = 25)
    {
        ClearCommitment();
        Attach("CommittedTo", goal, inertia);
    }

    /// <summary>清除当前心智持有的长线承诺。</summary>
    public void ClearCommitment()
    {
        Detach("CommittedTo");
    }

    #endregion

    #region 非线性对数情绪来源体系

    /// <summary>对数情绪聚合的底数基数（默认 2.0）。</summary>
    public float EmotionBase { get; set; } = 2.0f;

    /// <summary>
    /// 各情感类型下按来源划分的离散强度字典。
    /// 结构为：[情感类型 -> [来源标识 -> 强度分值]]。
    /// </summary>
    public Dictionary<Emotion, Dictionary<string, float>> EmotionSources { get; } = new()
    {
        [Emotion.Fear] = new(),
        [Emotion.Anger] = new(),
        [Emotion.Hope] = new(),
        [Emotion.Joy] = new(),
        [Emotion.Despair] = new(),
        [Emotion.Sorrow] = new(),
        [Emotion.Hatred] = new(),
        [Emotion.Serenity] = new(),
        [Emotion.Devotion] = new(),
        [Emotion.Remorse] = new()
    };

    /// <summary>经过非线性聚合后的各情感类型有效总强度缓存。</summary>
    public Dictionary<Emotion, float> EmotionIntensities { get; } = new();

    public float CalculateAggregatedEmotion(Emotion emotion)
    {
        if (!EmotionSources.TryGetValue(emotion, out var sources) || sources.Count == 0) return 0.0f;
        var b = EmotionBase;
        double sum = 0.0;
        foreach (var (_, intensity) in sources) if (intensity > 0.0f) sum += Math.Pow(b, intensity) - 1.0;
        return sum <= 0.0 ? 0.0f : Math.Clamp((float)(Math.Log(1.0 + sum) / Math.Log(b)), 0.0f, 10.0f);
    }

    public HashSet<string> PersistentEmotionSources { get; } = new();

    public void SetEmotionSource(Emotion emotion, string source, float intensity, bool isPersistent = false)
    {
        if (!EmotionSources.TryGetValue(emotion, out var sources)) EmotionSources[emotion] = sources = new();
        if (isPersistent) PersistentEmotionSources.Add(source);
        if (intensity <= 0.0f) { sources.Remove(source); PersistentEmotionSources.Remove(source); }
        else sources[source] = intensity;
        RecalculateEmotion(emotion);
    }

    public void RemoveEmotionSource(Emotion emotion, string source)
    {
        PersistentEmotionSources.Remove(source);
        if (EmotionSources.TryGetValue(emotion, out var sources) && sources.Remove(source)) RecalculateEmotion(emotion);
    }

    public void RecalculateEmotion(Emotion emotion)
    {
        var aggregated = CalculateAggregatedEmotion(emotion);
        EmotionIntensities[emotion] = aggregated;
        SetTag(emotion, (int)Math.Round(aggregated));
    }

    public void AdjustEmotion(Emotion emotion, int delta, string reason, bool isPersistent = false)
    {
        if (!EmotionSources.TryGetValue(emotion, out var sources)) EmotionSources[emotion] = sources = new();
        if (isPersistent) PersistentEmotionSources.Add(reason);
        var updated = sources.GetValueOrDefault(reason, 0.0f) + delta;
        if (updated <= 0.0f) { sources.Remove(reason); PersistentEmotionSources.Remove(reason); }
        else sources[reason] = updated;
        RecalculateEmotion(emotion);
    }

    public Dictionary<Emotion, (float OldVal, float NewVal)> SettleDailyEmotions(float transientDecay = 0.5f, float persistentDecay = 0.95f)
    {
        var deltas = new Dictionary<Emotion, (float OldVal, float NewVal)>();
        foreach (var emo in Enum.GetValues<Emotion>())
        {
            float oldVal = EmotionIntensities.GetValueOrDefault(emo, 0.0f);
            if (EmotionSources.TryGetValue(emo, out var sources) && sources.Count > 0)
            {
                foreach (var key in sources.Keys.ToList())
                {
                    bool isPersistent = PersistentEmotionSources.Contains(key);
                    float updated = sources[key] * (isPersistent ? persistentDecay : transientDecay);
                    if (updated < 0.15f && !isPersistent) sources.Remove(key);
                    else sources[key] = updated;
                }
            }
            RecalculateEmotion(emo);
            float newVal = EmotionIntensities.GetValueOrDefault(emo, 0.0f);
            if (Math.Abs(newVal - oldVal) > 0.01f || newVal > 0.1f) deltas[emo] = (oldVal, newVal);
        }
        return deltas;
    }

    public string GetEmotionalSummary(bool includeSources = false)
    {
        var list = new List<string>();
        foreach (var emo in Enum.GetValues<Emotion>())
        {
            var val = EmotionIntensities.GetValueOrDefault(emo, 0.0f);
            if (val > 0.05f)
            {
                if (includeSources && EmotionSources.TryGetValue(emo, out var sources) && sources.Count > 0)
                    list.Add($"{emo}:{val:F2}({string.Join("; ", sources.Select(s => $"{s.Key}:{s.Value:F1}"))})");
                else
                    list.Add($"{emo}:{val:F2}");
            }
        }
        return list.Count > 0 ? string.Join(", ", list) : "平和(Neutral)";
    }

    #endregion

    public Soul(string name, TreeMember? parent = null, IDictionary<string, int>? tags = null, int position = 0)
        : base(name, parent, MergeTags(tags), position: position)
    {
        foreach (var emo in Enum.GetValues<Emotion>()) RecalculateEmotion(emo);
    }

    private static Dictionary<string, int> MergeTags(IDictionary<string, int>? tags)
    {
        var result = new Dictionary<string, int> { ["Soul"] = 1 };
        if (tags != null) foreach (var (k, v) in tags) result[k] = v;
        return result;
    }

    public double Fatigue { get; private set; }
    public double ChangeFatigue(double delta)
    {
        if (!double.IsFinite(delta)) throw new InvalidOperationException("疲劳变化必须为有限量");
        return Fatigue = Math.Clamp(Fatigue + delta, 0d, Core.Content.Balance.FatigueHardLimit);
    }

    public void SetFatigue(double value)
    {
        if (!double.IsFinite(value)) throw new InvalidOperationException("疲劳值必须为有限量");
        Fatigue = Math.Clamp(value, 0d, Core.Content.Balance.FatigueHardLimit);
    }
    public Dictionary<Soul, int> BondLevels { get; } = new();
    public int GetBondLevel(Soul other) => BondLevels.GetValueOrDefault(other, 0);
    public int IncreaseBond(Soul other, int delta = 1)
    {
        int updated = Math.Clamp(GetBondLevel(other) + delta, 0, 3);
        BondLevels[other] = updated;
        other.BondLevels[this] = updated;
        return updated;
    }

    public void Bind(Character character) => character.Bind(this);

    public (KnowledgeStage Stage, string Description) LearnKnowledge(Entity knowledge, Soul mentor)
    {
        var claims = knowledge.GetClaims().ToList();
        var profession = knowledge.Get<string>("profession");
        var existing = Children.Keys.OfType<Entity>().FirstOrDefault(k => k.Name == knowledge.Name);
        if (existing != null)
        {
            var curStage = existing.Get<KnowledgeStage>("stage");
            if (curStage == KnowledgeStage.Received)
            {
                existing.Set("stage", KnowledgeStage.Understood);
                existing.ChangeTag("Understood", 1);
                existing.Tags.Remove("Received");
                foreach (var c in claims) if (c is Expr expr) Observe(expr);
                return (KnowledgeStage.Understood, "在二次请教与反复推敲后，终于理解了其中的专业原理！（进阶至 Understood）");
            }
            if (curStage == KnowledgeStage.Understood)
            {
                existing.Set("stage", KnowledgeStage.Accepted);
                existing.ChangeTag("Accepted", 1);
                return (KnowledgeStage.Accepted, "融会贯通，已经能够将该专业知识熟练运用于实战与规则判定！（进阶至 Accepted）");
            }
            return (curStage, "已完全掌握该专业领域学识。");
        }

        var stage = (Tag("Mind") >= 3 || (Tag("OccultDegree") >= 2 && profession == "考古学家")) ? KnowledgeStage.Understood : KnowledgeStage.Received;
        var newKnowledge = new Entity(knowledge.Name, this, new Dictionary<string, int> { ["Knowledge"] = 1, ["DomainKnowledge"] = 1, [stage.ToString()] = 1 }, properties: new Dictionary<string, object> { ["profession"] = profession ?? "", ["stage"] = stage, ["mentor"] = mentor.Name });
        foreach (var c in claims)
        {
            newKnowledge.Attach("Claim", c);
            if (stage >= KnowledgeStage.Understood && c is Expr expr) Observe(expr);
        }
        return (stage, stage == KnowledgeStage.Understood ? "凭借扎实的心智底蕴迅速领悟了其中原理（达到 Understood 阶段）" : "初次接触异专业体系，仅建立初步印象（处于 Received 阶段，尚待消化实践）");
    }

    public Facet Observe(Expr fact, object? source = null) => Entries("Observation").FirstOrDefault(item => Equals(item.Value, fact)) ?? Attach("Observation", fact, 1, null, source);
    public IReadOnlyList<Facet> Unobserve(Expr fact) => Detach("Observation", fact);

    public RuleEnvironment CognitiveEnvironment(RuleEnvironment? world = null)
    {
        var view = new RuleEnvironment();
        if (world != null) view.CopyAliasesFrom(world);
        foreach (var fact in Observations) view.AssertFact(fact);
        foreach (var k in Children.Keys.OfType<Entity>().Where(c => c.Has("Knowledge")))
        {
            if (!k.Has("Accepted") && !string.Equals(k.Get<string>("stage"), "accepted", StringComparison.OrdinalIgnoreCase) && k.Get<KnowledgeStage>("stage") != KnowledgeStage.Accepted) continue;
            foreach (var claim in k.Entries("Claim")) if (claim.Value is Expr expr) view.AssertFact(expr);
            foreach (var c in k.GetClaims()) if (c is Expr expr) view.AssertFact(expr);
        }
        return view;
    }

    public Entity Receive(Entity publication, int confidence = 1)
    {
        var rule = publication.Get<Rule>("rule");
        var found = Beliefs.FirstOrDefault(b => ReferenceEquals(b.Get<Entity>("publication"), publication));
        if (found != null) return found;

        return new Entity(
            $"对“{publication.Name}”的理解",
            this,
            new Dictionary<string, int> { ["RuleBelief"] = 1, ["Belief"] = 1, ["Received"] = 1 },
            properties: new Dictionary<string, object>
            {
                ["publication"] = publication,
                ["rule"] = rule!,
                ["issuer"] = publication.Get<Entity>("issuer")!,
                ["confidence"] = confidence,
                ["stance"] = CognitiveStage.Received,
                ["stage"] = "received"
            });
    }

    public static Entity Stance(Entity belief, CognitiveStage stance)
    {
        belief.Tags.Remove("Received");
        belief.Tags.Remove("Understood");
        belief.Tags.Remove("Accepted");
        belief.Tags.Remove("Rejected");
        belief.Change(stance.ToString(), 1);
        belief.Set("stance", stance);
        belief.Set("stage", "understood");
        return belief;
    }

    public Entity Understand(Rule rule)
    {
        var belief = Beliefs.First(item => ReferenceEquals(item.Get<Rule>("rule"), rule) || Equals(item.Get<Rule>("rule"), rule));
        if (belief.Has("Received")) Stance(belief, CognitiveStage.Understood);
        return belief;
    }

    public Entity Consider(Rule rule, Entity source, int confidence = 1, string guaranteeTag = "Guarantee")
    {
        var publication = source.Children.Keys.OfType<Entity>()
            .FirstOrDefault(child => child.Has("RulePublication") && (ReferenceEquals(child.Get<Rule>("rule"), rule) || Equals(child.Get<Rule>("rule"), rule)))
            ?? RuleEntityFactory.CreateRulePublication(rule, source, guaranteeTag, source);

        var belief = Receive(publication, confidence);
        Stance(belief, CognitiveStage.Understood);
        var issuer = publication.Get<Entity>("issuer") ?? source;
        int support = confidence + issuer.Tag(guaranteeTag) * 2 + issuer.Tag("Authority");
        return Stance(belief, support >= 4 ? CognitiveStage.Accepted : CognitiveStage.Rejected);
    }

    public Entity Accept(Rule rule) => Stance(Beliefs.First(item => ReferenceEquals(item.Get<Rule>("rule"), rule) || Equals(item.Get<Rule>("rule"), rule)), CognitiveStage.Accepted);

    public Entity Reject(Rule rule) => Stance(Beliefs.First(item => ReferenceEquals(item.Get<Rule>("rule"), rule) || Equals(item.Get<Rule>("rule"), rule)), CognitiveStage.Rejected);

    public Facet InstallStep(StepDelegate step, int priority = 0, Func<Entity, IReadOnlyDictionary<string, object>?, bool>? when = null, object? source = null) => Attach("Step", step, priority, when, source);
    public IReadOnlyList<Facet> UninstallSteps(object source) => Detach("Step", source: source);
    public Facet InstallPreference(PreferenceDelegate preference, int strength = 1, Func<Entity, IReadOnlyDictionary<string, object>?, bool>? when = null, object? source = null) => Attach("Preference", preference, strength, when, source);
    public IReadOnlyList<Facet> UninstallPreferences(object source) => Detach("Preference", source: source);

    public IReadOnlyList<PreferencePressure> PreferencePressures(Intent action, RuleEnvironment world, IReadOnlyDictionary<string, object>? context = null)
    {
        var cognitive = CognitiveEnvironment(world);
        var local = new Dictionary<string, object>(context ?? new Dictionary<string, object>()) { ["actor"] = action.Actor, ["intent"] = action, ["soul"] = this, ["env"] = cognitive };
        var pressures = new List<PreferencePressure>();
        foreach (var facet in Entries("Preference", local))
        {
            if (facet.Value is PreferenceDelegate pref)
            {
                float amount = pref(this, action, cognitive, local) * facet.Strength;
                if (Math.Abs(amount) > 0.0001f) pressures.Add(new PreferencePressure(amount, facet.Source ?? facet));
            }
        }
        return pressures;
    }

    public IReadOnlyList<Intent> Choices(Entity actor, RuleEnvironment env, IReadOnlyDictionary<string, object>? context = null)
    {
        var ctx = new Dictionary<string, object>(context ?? new Dictionary<string, object>()) { ["actor"] = actor, ["soul"] = this, ["env"] = env };
        var results = new List<Intent>();
        foreach (var facet in Entries("Step", ctx).OrderByDescending(f => f.Strength))
        {
            if (facet.Value is StepDelegate step)
            {
                var candidateResult = step(this, actor, env, ctx);
                if (candidateResult != null)
                {
                    foreach (var act in candidateResult)
                    {
                        if (act != null && !results.Contains(act)) results.Add(act);
                    }
                }
            }
        }
        return results;
    }

    /// <summary>判定指定行动意图是否有助于推进当前承诺的长线目标。</summary>
    private bool AdvancesCommitment(Intent action, object commitment)
    {
        if (ReferenceEquals(action.Target, commitment) || Equals(action.Target, commitment))
        {
            return true;
        }

        if (action.Effect.Args.Any(arg => ReferenceEquals(arg, commitment) || Equals(arg, commitment)))
        {
            return true;
        }

        if (commitment is TreeMember targetMember && action.Actor is Character character && character.CurrentScene != null)
        {
            var next = TopologySystem.GetNextStep(character.CurrentScene, targetMember);
            if (next.Passage != null)
            {
                if (action.Effect.Args.Any(arg => ReferenceEquals(arg, next.Passage) || ReferenceEquals(arg, next.NextScene)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>统计指定主体在近期历史事件中连续重复执行相同操作与目标的次数。</summary>
    public static int CountRecentRepetitions(
        IEnumerable<Entity>? historyEvents,
        Entity actor,
        Effect effect,
        int maxWindow = 40)
    {
        if (historyEvents == null) return 0;

        var target = effect.Args.Length > 1 ? effect.Args[1] : (effect.Args.Length > 0 && !ReferenceEquals(effect.Args[0], actor) ? effect.Args[0] : null);
        var op = effect.Op;

        int count = 0;
        var list = historyEvents.TakeLast(maxWindow).Reverse();

        foreach (var ev in list)
        {
            var evActor = ev.Get<Entity>("actor") ?? ev.Entries("Actor").FirstOrDefault()?.Value as Entity;
            if (!ReferenceEquals(evActor, actor))
            {
                continue;
            }

            var evEffect = ev.Get<Effect>("effect");
            if (evEffect == null) break;

            var evTarget = evEffect.Args.Length > 1 ? evEffect.Args[1] : (evEffect.Args.Length > 0 && !ReferenceEquals(evEffect.Args[0], actor) ? evEffect.Args[0] : null);

            if (string.Equals(evEffect.Op, op, StringComparison.OrdinalIgnoreCase) && Equals(evTarget, target))
            {
                count++;
            }
            else
            {
                break;
            }
        }

        return count;
    }

    /// <summary>对传入的一组候选行动意图进行全维度规则合规性评估与综合效用打分。</summary>
    /// <param name="candidates">候选意图序列。</param>
    /// <param name="rules">规则系统实例。</param>
    /// <param name="world">客观环境。</param>
    /// <param name="utility">可选的外部额外效用函数。</param>
    /// <param name="context">附加上下文。</param>
    /// <returns>按综合得分降序排列的决策裁决结果列表。</returns>
    public IReadOnlyList<Decision> Assess(
        IEnumerable<Intent> candidates,
        RuleSystem rules,
        RuleEnvironment world,
        Func<Intent, float>? utility = null,
        IReadOnlyDictionary<string, object>? context = null)
    {
        var cognitive = CognitiveEnvironment(world);
        var assessed = new List<Decision>();

        IEnumerable<Entity>? historyEvents = null;
        if (context != null)
        {
            if (context.TryGetValue("events", out var evObj) && evObj is IEnumerable<Entity> evList)
                historyEvents = evList;
            else if (context.TryGetValue("runtime", out var rtObj) && rtObj is ActionRuntime rt)
                historyEvents = rt.Events;
        }

        var commitment = CurrentCommitment;
        var inertia = CommitmentInertia;

        foreach (var action in candidates)
        {
            var d = rules.Evaluate(this, action, cognitive, context);
            var prefs = PreferencePressures(action, world, context);
            float prefScore = prefs.Sum(p => p.Amount);
            float u = utility != null ? utility(action) : 0f;

            int repeats = CountRecentRepetitions(historyEvents, action.Actor, action.Effect);
            float repPenalty = repeats * PenaltyPerRepetition;

            float inertiaBonus = 0f;
            if (commitment != null && AdvancesCommitment(action, commitment))
            {
                inertiaBonus = inertia;
            }

            float totalScore = d.Score + prefScore + u + inertiaBonus - repPenalty;

            assessed.Add(new Decision(
                action.Effect,
                d.Complies,
                totalScore,
                d.Basis,
                d.Reason,
                action,
                d.Pressures,
                prefs,
                repetitionPenalty: repPenalty,
                commitmentInertia: inertiaBonus));
        }

        return assessed.OrderByDescending(d => d.Score).ToList();
    }

    /// <summary>主体执行单次自主认知决策的主驱动入口。</summary>
    /// <remarks>
    /// 完整生命周期：
    /// 1. 调用 <see cref="Choices"/> 收集所有步骤生成的行动候选；
    /// 2. 过滤满足自检与外部物理许可的意图；
    /// 3. 调用 <see cref="Assess"/> 综合规则压力与人格偏好排序；
    /// 4. 通过选择器决选最优意图并挂载决策审计记录到自身子节点。
    /// </remarks>
    /// <param name="actor">执行身体。</param>
    /// <param name="rules">规则系统。</param>
    /// <param name="world">客观环境。</param>
    /// <param name="context">上下文信息。</param>
    /// <param name="utility">额外效用函数。</param>
    /// <param name="allows">外部硬性许可拦截器。</param>
    /// <param name="chooser">决选策略委托（默认为选取效用最高者）。</param>
    /// <returns>最终选定执行的行动意图实例。若无可用行动则返回 null。</returns>
    public Intent? Step(
        Entity actor,
        RuleSystem rules,
        RuleEnvironment world,
        IReadOnlyDictionary<string, object>? context = null,
        Func<Intent, float>? utility = null,
        Func<Intent, bool>? allows = null,
        Func<IReadOnlyList<Decision>, Intent>? chooser = null)
    {
        var candidateList = Choices(actor, world, context)
            .Where(act => act.Validate() && (allows == null || allows(act)))
            .ToList();

        if (candidateList.Count == 0) return null;

        var decisions = Assess(candidateList, rules, world, utility, context);
        if (decisions.Count == 0) return null;

        var chosenAction = chooser != null ? chooser(decisions) : decisions[0].Action;
        if (chosenAction == null) return null;

        var chosenDecision = decisions.FirstOrDefault(d => ReferenceEquals(d.Action, chosenAction))
            ?? throw new InvalidOperationException("选择器必须返回本次候选中的行动。");
        object? source = null;
        if (context != null && context.TryGetValue("source", out var src))
        {
            source = src;
        }

        Attach("Decision", chosenDecision, 1, null, source);
        return chosenAction;
    }

    /// <summary>针对一组抽象效果表达式直接通过规则系统进行决策评估。</summary>
    /// <param name="candidates">效果表达式集合。</param>
    /// <param name="rules">规则系统。</param>
    /// <param name="world">客观环境。</param>
    /// <param name="utility">效用函数。</param>
    /// <returns>最优决策结果。</returns>
    public Decision Decide(
        IEnumerable<Effect> candidates,
        RuleSystem rules,
        RuleEnvironment world,
        Func<Effect, float>? utility = null)
    {
        return rules.Decide(this, candidates, CognitiveEnvironment(world), utility);
    }
}

#endregion
