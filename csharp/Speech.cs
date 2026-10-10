using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

/// <summary>What a speaker does with a question before choosing words.</summary>
public enum SpeechAct { Tell, Hedge, AskBack, Withhold, Refuse, Lie }

/// <summary>Where the speaker stands on the proposition asked about.</summary>
public enum Stance { Affirm, Deny, Unsure }

/// <summary>A person's speaking habits; authored in the character profile, default is plain candour.</summary>
public sealed record SpeechHabits(int Candor = 0, int Reticence = 0, int Curiosity = 0, bool NeverLies = true);

public enum SpeechReasonCode { ListenerTrust, Sure, Unsure, Implicated, HedgeSure, HedgeUnsure, Curious, Distrust, Refusal, SelfProtection }
public sealed record SpeechReason(SpeechReasonCode Code, int Value = 0);
public sealed record SpeechChoice(SpeechAct Act, Stance Stance, double Score, ImmutableArray<SpeechReason> Reasons);

/// <summary>
/// Core speech-act rules. The speaker's own credence, trust in the listener, whether the answer
/// implicates the speaker, and the speaker's habits decide the act; the words are chosen afterwards.
/// More trust never makes a speaker less forthcoming, and a speaker who never lies never lies.
/// </summary>
public static class SpeechPolicy
{
    public const double Sure = 0.75;

    public static Stance StanceOf(double credence) => credence >= Sure ? Stance.Affirm : credence <= 1 - Sure ? Stance.Deny : Stance.Unsure;

    public static IReadOnlyList<SpeechChoice> Assess(double credence, int trust, SpeechHabits habits, bool implicatesSpeaker = false)
    {
        var stance = StanceOf(credence);
        var sure = stance != Stance.Unsure;
        trust = Math.Clamp(trust, -3, 3);
        var result = new List<SpeechChoice>();
        void Add(SpeechAct act, Stance said, double score, params SpeechReason?[] reasons)
            => result.Add(new(act, said, score, [.. reasons.OfType<SpeechReason>()]));

        Add(SpeechAct.Tell, stance, 2 + trust + habits.Candor - (implicatesSpeaker ? 2 : 0),
            new(SpeechReasonCode.ListenerTrust, trust), new(sure ? SpeechReasonCode.Sure : SpeechReasonCode.Unsure), implicatesSpeaker ? new(SpeechReasonCode.Implicated, -2) : null);
        Add(SpeechAct.Hedge, stance, 1 + (sure ? 0 : 2) + habits.Reticence, new SpeechReason(sure ? SpeechReasonCode.HedgeSure : SpeechReasonCode.HedgeUnsure));
        Add(SpeechAct.AskBack, stance, habits.Curiosity + (!sure && trust >= 1 ? 1 : 0), new SpeechReason(SpeechReasonCode.Curious));
        if (sure)
            Add(SpeechAct.Withhold, stance, (implicatesSpeaker ? 3 : 0) + Math.Max(0, -trust) + habits.Reticence,
                implicatesSpeaker ? new(SpeechReasonCode.Implicated) : null, trust < 0 ? new(SpeechReasonCode.Distrust) : null);
        Add(SpeechAct.Refuse, stance, trust <= -2 ? 2 - trust : 0, trust <= -2 ? new SpeechReason(SpeechReasonCode.Refusal) : null);
        if (sure && !habits.NeverLies)
            Add(SpeechAct.Lie, stance == Stance.Affirm ? Stance.Deny : Stance.Affirm,
                (implicatesSpeaker ? 2 : 0) + (trust < 0 ? 1 : 0) - 1, new SpeechReason(SpeechReasonCode.SelfProtection));
        // Ties keep the more forthcoming act (enum order).
        return result.OrderByDescending(c => c.Score).ThenBy(c => (int)c.Act).ToArray();
    }

    /// <summary>The best act whose words the author has written for this speaker.</summary>
    public static SpeechChoice? Choose(double credence, int trust, SpeechHabits habits, bool implicatesSpeaker,
        Func<SpeechAct, Stance, bool> written)
        => Assess(credence, trust, habits, implicatesSpeaker).FirstOrDefault(c => written(c.Act, c.Stance));
}
