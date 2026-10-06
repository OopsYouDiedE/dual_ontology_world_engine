using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

/// <summary>What a speaker does with a question before choosing words.</summary>
public enum SpeechAct { Tell, Hedge, AskBack, Withhold, Refuse, Lie }

/// <summary>Where the speaker stands on the proposition asked about.</summary>
public enum Stance { Affirm, Deny, Unsure }

/// <summary>A person's speaking habits; authored in the character profile, default is plain candour.</summary>
public sealed record SpeechHabits(int Candor = 0, int Reticence = 0, int Curiosity = 0, bool NeverLies = true);

public sealed record SpeechChoice(SpeechAct Act, Stance Stance, double Score, ImmutableArray<string> Reasons);

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
        void Add(SpeechAct act, Stance said, double score, params string[] reasons) => result.Add(new(act, said, score, [.. reasons]));

        Add(SpeechAct.Tell, stance, 2 + trust + habits.Candor - (implicatesSpeaker ? 2 : 0),
            $"对听者的信任 {trust:+0;-0;0}", sure ? "心里有数" : "没有把握，照实说不知道", implicatesSpeaker ? "说了会牵连自己 -2" : "");
        Add(SpeechAct.Hedge, stance, 1 + (sure ? 0 : 2) + habits.Reticence, sure ? "可以含糊带过" : "拿不准，先含糊");
        Add(SpeechAct.AskBack, stance, habits.Curiosity + (!sure && trust >= 1 ? 1 : 0), "想先知道对方为何问");
        if (sure)
            Add(SpeechAct.Withhold, stance, (implicatesSpeaker ? 3 : 0) + Math.Max(0, -trust) + habits.Reticence,
                implicatesSpeaker ? "答案牵连自己" : "", trust < 0 ? "不信听者" : "");
        Add(SpeechAct.Refuse, stance, trust <= -2 ? 2 - trust : 0, trust <= -2 ? "不想跟此人说话" : "");
        if (sure && !habits.NeverLies)
            Add(SpeechAct.Lie, stance == Stance.Affirm ? Stance.Deny : Stance.Affirm,
                (implicatesSpeaker ? 2 : 0) + (trust < 0 ? 1 : 0) - 1, "说反话自保");
        // Ties keep the more forthcoming act (enum order).
        return result.Select(c => c with { Reasons = [.. c.Reasons.Where(r => r.Length > 0)] })
            .OrderByDescending(c => c.Score).ThenBy(c => (int)c.Act).ToArray();
    }

    /// <summary>The best act whose words the author has written for this speaker.</summary>
    public static SpeechChoice? Choose(double credence, int trust, SpeechHabits habits, bool implicatesSpeaker,
        Func<SpeechAct, Stance, bool> written)
        => Assess(credence, trust, habits, implicatesSpeaker).FirstOrDefault(c => written(c.Act, c.Stance));
}
