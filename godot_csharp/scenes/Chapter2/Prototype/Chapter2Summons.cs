using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Game
{
    private string? _interactionLifetime;
    public void BeginInteractionLifetime(string owner)
    {
        if (_interactionLifetime == owner) return;
        EndInteractionLifetime();
        _interactionLifetime = owner;
    }
    public void EndInteractionLifetime()
    {
        if (_interactionLifetime is not { } owner) return;
        DualOntologyWorldEngine.Story.RuleLifetimes.End(People.Select(p => p.Soul), LifetimeScope.Interaction, owner, Now);
        if (DualOntologyWorldEngine.Story.RuleLifetimes.EndSources(Things, LifetimeScope.Interaction, owner)) Rt.Sync();
        _interactionLifetime = null;
    }
    private readonly HashSet<(int Slot, string Id)> _deliveredSummons = [];

    /// <summary>Authored broadcast invitation uses the existing social request and decision mechanisms; movement is committed by Travel.</summary>
    public void HostSummon(string id)
    {
        var declaration = _access.GetProperty("summons").GetProperty(id);
        if (!_deliveredSummons.Add((SlotIndex, id))) return;
        var sender = this[AccessPerson(declaration.GetProperty("sender").GetString()!)];
        var to = AccessPlace(declaration.GetProperty("to").GetString()!);
        var recipients = declaration.GetProperty("people").EnumerateArray().Select(p => this[AccessPerson(p.GetString()!)])
            .Where(HostDormReleased).Where(p => Where(p) != to).ToArray();
        var requests = new Dictionary<Person, Projection>();
        foreach (var person in recipients)
        {
            // This explicit message names the now-open route. Other remembered routes keep their previous state.
            ObserveAccess(person, [to], ObservationSource.Testimony);
            requests[person] = Observation.Observe(person.Soul, new ObservationInput(declaration.GetProperty("text").GetString()!, ProjectionKind.Event)
            {
                Source = ObservationSource.Testimony, Time = Now, Existence = Truth.True,
                AddRelations = [(Relations.Subject, person.M[sender.Body])],
                Procedure = new Effect.Act(ActionOp.Travel, person.M[Way[to]]),
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(SocialSystem.RequestCompatibility, declaration.GetProperty("compatibility").GetInt32())
            });
        }
        FreeEvents.Add(new FreeEvent.Summon(SlotIndex, id));
        // Let a person finish the current task, then consider the invitation with their own habits and accepted rules.
        for (var step = 0; step < 16 && requests.Any(r => Where(r.Key) != to && r.Value.Tags.GetValueOrDefault(Tags.Responded) == 0); step++)
        {
            foreach (var person in recipients.Where(p => Where(p) != to && requests[p].Tags.GetValueOrDefault(Tags.Responded) == 0)) Decide(person);
            S.CTB.RunUntil(Now + TickLength, 100_000);
        }
        SyncChapterRooms();
    }
}
