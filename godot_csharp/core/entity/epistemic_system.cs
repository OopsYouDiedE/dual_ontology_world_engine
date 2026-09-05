using System;
using System.Collections.Generic;
using System.Linq;

namespace DualOntologyWorldEngine.Core;

/// <summary>
/// 观察所获得的认知结果。
/// </summary>
/// <param name="Subject">被观察的主体对象。</param>
/// <param name="Designation">观察者在当前视界下所识别出的名称。</param>
/// <param name="Claims">观察所直接获取到的命题或表征列表。</param>
public record ObservationResult(
    Entity Subject,
    string Designation,
    IReadOnlyList<object> Claims);

/// <summary>
/// 声明条目引用，记录声明所属的认知实体与具体命题。
/// </summary>
public record ClaimEntry(
    Entity Knowledge,
    object Claim);

/// <summary>
/// 认知（Cognition）、观察（Observation）与主观信念系统。
/// </summary>
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

    /// <summary>
    /// 获取实体上的声明命题列表。
    /// </summary>
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

    /// <summary>
    /// 获取实体上配置的观察揭示（Reveal）命题列表。
    /// </summary>
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

    /// <summary>
    /// 为实体注册一条可供观察揭示的命题。
    /// </summary>
    public static void AddReveal(this Entity entity, object claim)
    {
        entity.GetReveals().Add(claim);
    }

    #endregion

    #region 主体知识获取与检索

    /// <summary>
    /// 在观察者子树下建立或更新一条指向 subject 的 Knowledge 实体。
    /// </summary>
    /// <param name="observer">认知主体实体（如角色或意识）。</param>
    /// <param name="subject">该项知识所指向的客观实体。</param>
    /// <param name="claims">本次获知的命题集合。</param>
    /// <param name="source">信息来源对象。</param>
    /// <param name="confidence">置信度数值。</param>
    /// <param name="stage">达到的认知阶段。</param>
    /// <param name="name">知识实体的自定义命名。</param>
    /// <returns>挂载在观察者名下的 Knowledge 实体。</returns>
    public static Entity Learn(
        this Entity observer,
        Entity subject,
        IEnumerable<object>? claims = null,
        object? source = null,
        int confidence = 1,
        KnowledgeStage stage = KnowledgeStage.Understood,
        string? name = null)
    {
        // 寻找观察者直接子节点中是否已有针对同一目标且来源相同的 Knowledge
        var knowledge = observer.Children.Keys.OfType<Entity>().FirstOrDefault(child =>
            child.HasTag("Knowledge") &&
            ReferenceEquals(child.Get<Entity>("subject"), subject) &&
            Equals(child.Get<object>("source"), source));

        if (knowledge == null)
        {
            knowledge = new Entity(
                name ?? $"关于{subject.Name}的认知",
                observer,
                new Dictionary<string, int> { ["Knowledge"] = 1 },
                properties: new Dictionary<string, object>
                {
                    ["subject"] = subject,
                    ["source"] = source ?? observer,
                    ["confidence"] = confidence,
                    ["stage"] = stage
                });
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
            knowledge.SetTag("Accepted", 1);
            knowledge.SetTag("Rejected", 0);
        }

        return knowledge;
    }

    /// <summary>
    /// 查询观察者名下达到指定认知阶段、且指向 subject 的所有知识条目。
    /// </summary>
    public static IReadOnlyList<Entity> GetKnowledgeOf(
        this Entity observer,
        Entity subject,
        KnowledgeStage minStage = KnowledgeStage.Received)
    {
        return observer.Children.Keys.OfType<Entity>().Where(child =>
            child.HasTag("Knowledge") &&
            ReferenceEquals(child.Get<Entity>("subject"), subject) &&
            child.Get<KnowledgeStage>("stage", KnowledgeStage.Received) >= minStage).ToList();
    }

    /// <summary>
    /// 检查观察者是否获知特定目标，或者是否确切获知针对它的某条特定声明。
    /// </summary>
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

    /// <summary>
    /// 检索关于指定目标的所有声明条目，并可按采信立场与来源进行精确过滤。
    /// </summary>
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

    /// <summary>
    /// 设置单条知识实体的采信立场。
    /// </summary>
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

    /// <summary>
    /// 执行观察交互，提取公开揭示的命题，并可选地写入观察者的认知子树中。
    /// </summary>
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
        float? currentTime = null)
    {
        var knownSubjects = new HashSet<Entity>(
            observer.Children.Keys.OfType<Entity>()
                .Where(k => k.HasTag("Knowledge"))
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
