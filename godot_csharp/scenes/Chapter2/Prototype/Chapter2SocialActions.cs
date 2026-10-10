using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;
// STORY-HARDCODE[CH2-011][logic][production]: 本文件按本章具体社会行动实现角色互动及预写反馈。
namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Game
{
    private PlayerAction? _preparedAction;
    private Truth? _preparedRuleVerdict;

    private RF ActionFlags(PlayerAction action) => action.Cmd switch
    {
        Cmd.TellRule => action.Spec?.Flags ?? action.Card?.Flags(this) ?? RF.None,
        Cmd.ShareFood => RF.SickPriority | RF.Rations,
        Cmd.Escort => RF.EvidenceFirst,
        Cmd.Ask => action.Job switch
        {
            Job.Inform or Job.CountLedger => RF.Records,
            Job.Care or Job.FeedWeak => RF.SickPriority,
            Job.RepairLatch => RF.Safety,
            Job.GuardArchive => RF.SealArchive,
            _ => RF.None
        },
        _ => RF.None
    };
    private int Compatibility(Person recipient, PlayerAction action)
    {
        var flags = ActionFlags(action);
        var job = action.Cmd == Cmd.Help ? Job.Work : action.Job;
        var habits = recipient.Pro.Habits.Where(h => h.Job == job && h.Slots.Contains(Slot)).ToArray();
        return FlagCompatibility(recipient, flags) + habits.Sum(h => h.Weight + (h.Desire ? 2 : 0));
    }
    private static int FlagCompatibility(Person recipient, RF flags) =>
        recipient.Pro.Values.Where(v => flags.HasFlag(v.Key)).Sum(v => v.Value)
        + ((recipient.Pro.DesireFlags & flags) != RF.None ? Math.Max(1, recipient.Desire) : 0);

    private static Projection? KnownFoodTarget(Person recipient) => recipient.World.Projections
        .Where(target => target.Kind == ProjectionKind.Body && !ReferenceEquals(target, recipient.Self)
            && target.Tags.GetValueOrDefault(T.Hungry) > 0)
        .OrderBy(target => target.Name, StringComparer.Ordinal).FirstOrDefault();

    private Effect? RequestProcedure(Person recipient, Job job)
    {
        var fx = FxOf(Det);
        if (job == Job.FeedWeak)
            return KnownFoodTarget(recipient) is { } target && EssenceOf(recipient, target) is { } entity
                && Det.M.TryGetValue(entity, out var knownTarget)
                ? Ast.Bind(fx.FeedWeak(), new ActionContext(fx.R(recipient.Body), knownTarget)) : null;
        return fx.Job(job, job is Job.Rest or Job.Sleep or Job.Work ? recipient : null);
    }

    public int HostSocialScore(P who, PlayerAction action)
    {
        var p = this[who];
        if (!HostDormReleased(p) || p.Asleep || Where(p) != Where(Det)) return int.MinValue;
        if (action.Place is { } destination && !HostCanEnter(destination)) return int.MinValue;
        if (action.Cmd == Cmd.Help && !HostCanEnter(WorkPlace(p.Id))) return int.MinValue;
        if (action.Cmd == Cmd.Ask && action.Job is { } job)
        {
            if (job == Job.FeedWeak && KnownFoodTarget(p) is null) return int.MinValue;
            var jobDestination = job switch { Job.Care => Place.Clinic, Job.GuardArchive => Place.ArchiveFront, Job.Inform or Job.CountLedger or Job.RepairLatch or Job.FeedWeak => Place.Logistics, _ => Where(p) };
            if (!HostCanEnter(jobDestination)) return int.MinValue;
        }
        return SocialSystem.EvaluateSender(p.World, p.Self, p.M[Det.Body], Compatibility(p, action)).Score;
    }

    public bool HostRuleRelevant(RuleSpec spec) => spec.PostedAt.Contains(Where(Det))
        || spec.Flags.HasFlag(RF.Rations) && Where(Det) is Place.Logistics or Place.Clinic
        || (spec.Flags & (RF.OpenArchive | RF.CommitteeArchive | RF.SealArchive | RF.PactArchive)) != 0 && Where(Det) is Place.ArchiveFront or Place.Hall
        || spec.Flags.HasFlag(RF.EvidenceFirst) && Day >= CaseDay;

    public bool HostPrepareSocialAction(PlayerAction action)
    {
        if (action.Cmd is not (Cmd.Ask or Cmd.Escort or Cmd.TellRule or Cmd.Help or Cmd.ShareFood)) return true;
        var who = action.Who ?? throw new ArgumentException("A social action needs a recipient.");
        var p = this[who];
        if (HostSocialScore(who, action) <= 0)
        {
            ObserveSocialResult(p, action.Cmd.ToString(), false);
            Feed.Add(action.Cmd == Cmd.TellRule ? Voice(p, action.Spec ?? OwnRulePreview(action.Card), Truth.False) : Answer(p, SocialStance.Reject));
            return false;
        }
        _preparedAction = action;
        if (action.Cmd == Cmd.TellRule)
        {
            var spec = OwnSpec(action.Spec) ?? CardSpec(action.Card!, P.Det);
            _preparedRuleVerdict = Truth.True;
            var before = p.Acc.GetValueOrDefault(spec);
            p.Acc[spec] = Truth.True;
            p.Why[spec] = X("social.accepted");
            spec.Endorsers.Add(p.Id);
            WriteTold(p, spec, Truth.True);
            if (before != Truth.True) OnAccepted(p, spec);
            ObserveSocialResult(p, "tell:" + spec.Id, true);
        }
        else if (action.Cmd is Cmd.Ask or Cmd.Escort)
        {
            _asked.Clear();
            var procedure = action.Cmd == Cmd.Escort ? FxOf(Det).Escort(action.Place!.Value, Things_[action.Thing!].Thing)
                : RequestProcedure(p, action.Job!.Value) ?? throw new InvalidOperationException("The request has no known target.");
            var text = action.Cmd == Cmd.Escort ? Chapter2Design.Text("social.escort", ("place", Names.Of(action.Place!.Value)), ("thing", action.Thing)) : Chapter2Design.Text("social.request", ("job", Names.Of(action.Job!.Value)));
            Request(who, text, procedure, Compatibility(p, action));
            if (_asked.GetValueOrDefault(who) != SocialStance.Accept) { _preparedAction = null; return false; }
        }
        return true;
    }

    private RuleSpec OwnRulePreview(Card? card) => card is null ? RuanLedger : CardSpec(card, P.Det);
    private void ObserveSocialResult(Person p, string identity, bool accepted)
    {
        Observation.Observe(p.Soul, new ObservationInput(identity, ProjectionKind.Event)
        {
            Source = ObservationSource.Testimony, Existence = Truth.True, Time = Now,
            Tags = ImmutableDictionary<Tag,int?>.Empty.Add(Tags.Trust, accepted ? 1 : -1).Add(Tags.Responded, 1),
            AddRelations = [(Relations.Subject, p.M[Det.Body])]
        });
    }

    public IReadOnlyList<(P Who, bool Accepted)> HostMediate(string wording)
    {
        if (wording is not ("sick_first" or "sign_first" or "i_carry")) throw new ArgumentException("Unknown mediation wording.");
        FreeEvents.Add(new FreeEvent.Mediation(SlotIndex, wording));
        var results = new List<(P,bool)>();
        foreach (var who in new[] { P.Mora, P.Ruan })
        {
            var p = this[who];
            var flags = wording == "sick_first" ? RF.SickPriority : wording == "sign_first" ? RF.Records : RF.SickPriority | RF.Records;
            var compatibility = FlagCompatibility(p, flags);
            if (wording == "sick_first") compatibility -= p.Pro.Values.GetValueOrDefault(RF.Records);
            if (wording == "sign_first") compatibility -= p.Pro.Values.GetValueOrDefault(RF.SickPriority);
            var request = Observation.Observe(p.Soul, new ObservationInput("mediate:" + wording, ProjectionKind.Event)
                { Source = ObservationSource.Testimony, Existence = Truth.True, Time = Now, AddRelations = [(Relations.Subject, p.M[Det.Body])] });
            var accepted = SocialSystem.Evaluate(p.World, p.Self, request, compatibility).Stance == SocialStance.Accept;
            ObserveSocialResult(p, "mediate:" + wording, accepted);
            results.Add((who, accepted));
        }
        Mediated = results.All(r => r.Item2);
        return results;
    }
}
