using System.Collections.Generic;
using System.Linq;
using DualOntologyWorldEngine.Core.Rules;

namespace DualOntologyWorldEngine.Core.Mind;

/// <summary>
/// 单条规则规范对意图产生的规范压力记录。
/// </summary>
/// <param name="Amount">规范压力数值（正数表示鼓励或必须履行，负数表示禁止或违反惩戒）。</param>
/// <param name="Belief">施加该规范压力的主体已采信规则信念实体（RuleBelief）。</param>
/// <param name="Directive">匹配触发的具体规约指令对象（Mode + Effect）。</param>
/// <param name="Matched">候选意图效果是否在当前求值环境中命中了指令模式。</param>
public record RulePressure(float Amount, Entity Belief, Directive Directive, bool Matched);

/// <summary>
/// 单条人格特质、社交态度或情境偏好产生的动机偏好压力记录。
/// </summary>
/// <param name="Amount">偏好动机得分（正数代表正向激励与执行倾向，负数代表抗拒或心理不适）。</param>
/// <param name="Source">动机来源对象或文本描述（如人格标签、社交态度模式或特定情绪源）。</param>
public record PreferencePressure(float Amount, object Source);

/// <summary>
/// 规则系统与心智对候选行动意图的综合评价与决策裁决结果。
/// </summary>
/// <remarks>
/// Decision 是主观认知决策管线的产出。
/// 
/// 结算模型：
/// 1. 规范合规性（<see cref="Complies"/>）：只要存在命中 Forbid 模式或未满足 Require 模式的刚性规则，即视为违规。
/// 2. 效用综合评分（<see cref="Score"/>）：聚合所有规则压力（<see cref="Pressures"/>）与人格社交偏好（<see cref="Preferences"/>）的总和。
/// 3. 主导依据（<see cref="Basis"/>）与理由（<see cref="Reason"/>）：提取绝对权重大、影响力最强的核心信念实体与规约模式作为向外部汇报的理由。
/// </remarks>
public class Decision
{
    /// <summary>被评价的目标效果表达式 AST。</summary>
    public Effect Intent { get; }

    /// <summary>是否完全符合主体已知且采信的全部规则规范。</summary>
    public bool Complies { get; }

    /// <summary>综合效用总得分。分数越高表示主体越倾向于执行该行动。</summary>
    public float Score { get; }

    /// <summary>主导该决策评价结果的核心规则信念实体引用（若存在）。</summary>
    public Entity? Basis { get; }

    /// <summary>用于日志审计与对话呈现的决策理由文本说明。</summary>
    public string Reason { get; }

    /// <summary>与此评价绑定的具体可执行行动候选实例（若已绑定）。</summary>
    public Intent? Action { get; }

    /// <summary>来自已采信客观规则的所有规范压力明细列表。</summary>
    public IReadOnlyList<RulePressure> Pressures { get; }

    /// <summary>来自人格偏好、情绪与社交态度的动机压力明细列表。</summary>
    public IReadOnlyList<PreferencePressure> Preferences { get; }

    /// <summary>连续相同动作施加的边际效用扣减惩罚分。</summary>
    public float RepetitionPenalty { get; init; } = 0f;

    /// <summary>推进长线承诺目标所获得的惯性加分。</summary>
    public float CommitmentInertia { get; init; } = 0f;

    /// <summary>
    /// 初始化完备的意图决策评价结果。
    /// </summary>
    /// <param name="intent">待评价的效果 AST。</param>
    /// <param name="complies">是否合规。</param>
    /// <param name="score">综合效用得分。</param>
    /// <param name="basis">主导依据实体。</param>
    /// <param name="reason">决策理由。</param>
    /// <param name="action">绑定的可执行行动。</param>
    /// <param name="pressures">规则压力列表。</param>
    /// <param name="preferences">偏好压力列表。</param>
    /// <param name="repetitionPenalty">重复动作惩罚扣分。</param>
    /// <param name="commitmentInertia">承诺目标惯性加分。</param>
    public Decision(
        Effect intent,
        bool complies,
        float score,
        Entity? basis,
        string reason,
        Intent? action = null,
        IEnumerable<RulePressure>? pressures = null,
        IEnumerable<PreferencePressure>? preferences = null,
        float repetitionPenalty = 0f,
        float commitmentInertia = 0f)
    {
        Intent = intent;
        Complies = complies;
        Score = score;
        Basis = basis;
        Reason = reason;
        Action = action;
        Pressures = pressures != null ? pressures.ToList() : [];
        Preferences = preferences != null ? preferences.ToList() : [];
        RepetitionPenalty = repetitionPenalty;
        CommitmentInertia = commitmentInertia;
    }

    /// <summary>
    /// 兼容历史签名的快捷构造函数。
    /// </summary>
    /// <param name="action">行动意图。</param>
    /// <param name="score">得分。</param>
    /// <param name="reason">理由。</param>
    /// <param name="legacyPressures">旧式动机元组集合。</param>
    public Decision(
        Intent action,
        float score,
        string reason,
        IEnumerable<(string Motivation, float Amount)>? legacyPressures = null)
        : this(
            action.Effect,
            complies: true,
            score: score,
            basis: null,
            reason: reason,
            action: action,
            pressures: null,
            preferences: legacyPressures?.Select(p => new PreferencePressure(p.Amount, p.Motivation)))
    {
    }

    /// <summary>
    /// 派生并绑定具体执行行动的决策副本。
    /// </summary>
    /// <param name="action">新绑定的行动意图。</param>
    /// <param name="newScore">可选覆盖的效用得分。</param>
    /// <param name="newPreferences">可选追加或替换的偏好压力。</param>
    /// <returns>绑定行动后的新决策实例。</returns>
    public Decision WithAction(Intent action, float? newScore = null, IEnumerable<PreferencePressure>? newPreferences = null)
    {
        return new Decision(
            Intent,
            Complies,
            newScore ?? Score,
            Basis,
            Reason,
            action,
            Pressures,
            newPreferences ?? Preferences);
    }

    /// <summary>
    /// 生成决策结果的调试诊断字符串。
    /// </summary>
    /// <returns>格式化后的操作原语、合规状态、分数与理由文本。</returns>
    public override string ToString() =>
        $"Decision: [{Intent.Op}] Complies={Complies}, Score={Score:F2}, Reason='{Reason}'";
}
