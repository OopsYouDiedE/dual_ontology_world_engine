using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public static class SpeechTopics
{
    public static readonly AuthorTermId Latch = new("maintenance.latch");
    public static readonly AuthorTermId Invitation = new("request.night-entry");
    public static readonly AuthorTermId Vacate = new("request.vacate");
    public static AuthorTermId Rule(int id) => new($"rule/{id}");
    public static AuthorTermId Lesson(int id) => new($"lesson/{id}");
}

public sealed partial class Game
{
    /// <summary>Actual action receipts; display text and basis wording are only presentation inputs.</summary>
    public sealed record ActionRecord(int Day, Slot Slot, Person Actor, Job? Job, Place Location,
        TypedEventV1<Essence?> Event, Projection? Basis, string Text, string BasisText)
    {
        public bool Succeeded => Event.Fields.Outcome == EventOutcomeV1.Succeeded;
        public AuthorTermId? ResponseTo => Event.Fields.Topic;
    }
    public readonly List<ActionRecord> ActionRecords = [];
    public readonly List<(int Night, Person Actor)> NightLedgerRecords = [];
    private void CaptureAction(Person p, Job? job, ActionExecution execution, string why)
    {
        var receipt = execution.Events.LastOrDefault(e => e is ActionEventV1<Essence?> && e.Fields.Phase == EventPhaseV1.Completed);
        if (receipt is null) return;
        var text = job is { } j ? Names.Of(j) : execution.Intent.Basis?.Name ?? "";
        ActionRecords.Add(new(Day, Slot, p, job, Where(p), receipt, execution.Intent.Basis, text, why));
    }
    public IEnumerable<ActionRecord> NightActions(int night, Person? actor = null)
        => ActionRecords.Where(a => a.Day == night && a.Slot == Slot.Night && a.Succeeded && (actor is null || a.Actor == actor));
}
