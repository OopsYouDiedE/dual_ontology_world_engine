using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Timeline;

namespace DualOntologyWorldEngine.Core.Mind;

/// <summary>言语行为标准施事原语（FIPA-ACL Performatives）。</summary>
public enum Performative
{
    /// <summary>断言：告知事实 (Inform / StateClaim)。</summary>
    Inform,

    /// <summary>断言：出示物证 (Testify / Evidence)。</summary>
    Testify,

    /// <summary>指令：询问信息 (Query / Ask)。</summary>
    Query,

    /// <summary>指令：质询前置断言 (Challenge)。</summary>
    Challenge,

    /// <summary>承诺：提议互惠交换 (Propose / Offer)。</summary>
    Propose,

    /// <summary>承诺：承诺接受提议 (AcceptProposal)。</summary>
    AcceptProposal,

    /// <summary>承诺：拒绝提议 (RejectProposal)。</summary>
    RejectProposal,

    /// <summary>承诺：提出反向提议 (CounterPropose)。</summary>
    CounterPropose,

    /// <summary>宣告与撤回：撤回此前言论与主张 (Retract)。</summary>
    Retract,

    /// <summary>宣告与撤回：官方权威宣告改变语义 (Declare)。</summary>
    Declare
}

/// <summary>标准言语行为消息不可变记录。</summary>
/// <param name="ConversationId">会话交互线索标识。</param>
/// <param name="Sender">发言主体实体引用。</param>
/// <param name="Receiver">接收目标主体引用。</param>
/// <param name="Performative">施事原语类型。</param>
/// <param name="Content">命题载荷（Expr 表达式、物证或陈述事实）。</param>
/// <param name="Timestamp">发言行动时刻。</param>
/// <param name="Context">时空情境上下文。</param>
public sealed record SpeechActMessage(
    string ConversationId,
    Entity Sender,
    Entity Receiver,
    Performative Performative,
    object? Content,
    double Timestamp,
    ActionContext? Context = null);

#region 会话交互协议状态机

/// <summary>质询-查验协议状态枚举。</summary>
public enum ChallengeProtocolState
{
    Stated,
    Challenged,
    Verified,
    Maintained,
    Retracted
}

/// <summary>质询-查验会话协议状态机。</summary>
public sealed class ChallengeVerificationSession(string conversationId, Entity speaker, Entity claimTopic, Expr proposition)
{
    public string ConversationId { get; } = conversationId;
    public Entity Speaker { get; } = speaker;
    public Entity ClaimTopic { get; } = claimTopic;
    public Expr Proposition { get; } = proposition;
    public ChallengeProtocolState State { get; private set; } = ChallengeProtocolState.Stated;
    public Entity? Challenger { get; private set; }
    public Entity? EvidenceProvided { get; private set; }

    /// <summary>对方发起质询。</summary>
    public bool Challenge(Entity challenger)
    {
        if (State != ChallengeProtocolState.Stated) return false;
        Challenger = challenger;
        State = ChallengeProtocolState.Challenged;
        return true;
    }

    /// <summary>原断言者出示确凿物证回应。</summary>
    public bool Testify(Entity evidence)
    {
        if (State != ChallengeProtocolState.Challenged) return false;
        EvidenceProvided = evidence;
        State = ChallengeProtocolState.Verified;
        return true;
    }

    /// <summary>原断言者拒绝出示证据，坚持主张。</summary>
    public bool Maintain()
    {
        if (State != ChallengeProtocolState.Challenged) return false;
        State = ChallengeProtocolState.Maintained;
        return true;
    }

    /// <summary>原断言者撤回主张。</summary>
    public bool Retract()
    {
        if (State != ChallengeProtocolState.Challenged && State != ChallengeProtocolState.Stated) return false;
        State = ChallengeProtocolState.Retracted;
        return true;
    }
}

/// <summary>协商交换协议状态枚举。</summary>
public enum NegotiationProtocolState
{
    Proposed,
    CounterProposed,
    Accepted,
    Rejected,
    Closed
}

/// <summary>协商交换会话协议状态机。</summary>
public sealed class NegotiationSession(string conversationId, Entity initiator, Entity respondent, Entity offer, Entity request)
{
    public string ConversationId { get; } = conversationId;
    public Entity Initiator { get; } = initiator;
    public Entity Respondent { get; } = respondent;
    public Entity CurrentOffer { get; private set; } = offer;
    public Entity CurrentRequest { get; private set; } = request;
    public NegotiationProtocolState State { get; private set; } = NegotiationProtocolState.Proposed;

    /// <summary>受邀方接受提议。</summary>
    public bool Accept()
    {
        if (State != NegotiationProtocolState.Proposed && State != NegotiationProtocolState.CounterProposed) return false;
        State = NegotiationProtocolState.Accepted;
        return true;
    }

    /// <summary>受邀方拒绝提议。</summary>
    public bool Reject()
    {
        if (State != NegotiationProtocolState.Proposed && State != NegotiationProtocolState.CounterProposed) return false;
        State = NegotiationProtocolState.Rejected;
        return true;
    }

    /// <summary>提出反向提议。</summary>
    public bool CounterPropose(Entity newOffer, Entity newRequest)
    {
        if (State != NegotiationProtocolState.Proposed && State != NegotiationProtocolState.CounterProposed) return false;
        CurrentOffer = newOffer;
        CurrentRequest = newRequest;
        State = NegotiationProtocolState.CounterProposed;
        return true;
    }
}

#endregion
