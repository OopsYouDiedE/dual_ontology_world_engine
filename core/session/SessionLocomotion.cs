using DuelView.Compiled;
using DuelView.Core;
using static DuelView.Core.DVKernel;

public sealed partial class DvSession
{
    public long LocomotionSequence => (long)R.Num(Field(Field(state, "story", false), "locomotion_sequence", 0d));

    public IReadOnlyDictionary<string, object?> PlayerControlProjection()
    {
        using var scope = R.UseValues(valueContext);
        return converter.Record(Run(() =>
        {
            var player = R.Text(Field(state, "player_actor", ""));
            var actors = R.Items(Field(state, "actors", false));
            var index = actors.ToList().FindIndex(a => R.Text(Field(a, "id", "")) == player);
            if (index < 0) return new List<object>();
            var pack = Field(content.Get("data", "campaign.pack"), "value", false);
            var conditionId = R.Text(Field(Field(pack, "player_locomotion_conditions", new List<object>()), player, ""));
            var canWalk = conditionId.Length == 0 || R.Text(F_dv_truth(Field(state, "physical", false), DvValueTemplate.Instantiate(conditionId))) == "true";
            var pending = Field(state, "pending", false);
            var occupied = IsActorInFlight(player) || (pending is not false && R.Text(Field(pending, "id", "")) == player);
            object options = new List<object>();
            if (!occupied)
            {
                var inputView = R.record_with(R.record_with(R.record_with(state, "pending", false), "cursor", (double)index), "awaiting_player", true);
                options = Field(CampaignRuntime.F_dvg_intent(inputView), "options", new List<object>());
            }
            return new List<object> { F_dv_pair("locomotion_enabled", canWalk && !occupied), F_dv_pair("player_options", options) };
        }));
    }

    // Walking is a physical observation, not a scheduled interaction. The host
    // supplies an ordered crossing after move_and_slide; Core applies the DV
    // movement law and its observation grants without taking an action slot.
    public void CommitLocomotion(IReadOnlyDictionary<string, object?> measurement)
    {
        using var valuesScope = R.UseValues(valueContext);
        var receipt = converter.ToDv(measurement);
        var next = Run(() => CommitLocomotionState(receipt));
        if (ReferenceEquals(next, state)) return;
        BeginTransition();
        state = next;
        RememberCommitted();
    }

    private object CommitLocomotionState(object receipt)
    {
        var actorId = R.Text(Field(receipt, "actor", ""));
        F_dv_require(actorId.Length > 0 && actorId == R.Text(Field(state, "player_actor", "")), "Walking receipt must belong to the controlled actor.");
        F_dv_require(Field(receipt, "basis", "") is "godot-3d-physics-displacement", "Walking needs actual native movement.");
        F_dv_require(Field(receipt, "content_hash", "") is string hash && hash == content.Hash, "Walking content changed.");
        var sequence = R.Num(Field(receipt, "sequence", 0d));
        var story = Field(state, "story", new List<object>());
        var previousSequence = R.Num(Field(story, "locomotion_sequence", 0d));
        if (sequence == previousSequence) return state;
        F_dv_require(sequence == previousSequence + 1 && double.IsFinite(sequence), "Walking receipts must stay ordered.");
        var positions = new[] { "from_position", "to_position" }.Select(key => R.Items(Field(receipt, key, false))).ToArray();
        F_dv_require(positions.All(p => p.Count == 3 && p.All(v => R.Bool(R.is_numeric_value(v)) && double.IsFinite(R.Num(v)))), "Walking positions must be finite.");
        var distance = Math.Sqrt(Enumerable.Range(0, 3).Sum(i => Math.Pow(R.Num(positions[0][i]) - R.Num(positions[1][i]), 2)));
        F_dv_require(distance > 0 && distance < 1, "Walking receipt must describe a local physics step.");
        var actors = Field(state, "actors", false);
        var index = R.Items(actors).ToList().FindIndex(a => R.Text(Field(a, "id", "")) == actorId);
        F_dv_require(index >= 0 && !IsActorInFlight(actorId), "Walking actor is occupied.");
        var pending = Field(state, "pending", false);
        F_dv_require(pending is false || R.Text(Field(pending, "id", "")) != actorId, "An interaction owns the walking actor.");
        var actor = R.at(actors, (double)index);
        var physical = Field(state, "physical", false);
        var location = F_dv_read(physical, Field(actor, "location_ref", false));
        F_dv_require(F_dv_uncontested(location), "Walking origin is unknown.");
        var origin = Field(location, "value", false);
        var fromRoom = R.Text(Field(receipt, "from_room", ""));
        var toRoom = R.Text(Field(receipt, "to_room", ""));
        var map = R.Text(Field(receipt, "map", ""));
        var fromPlace = CampaignRuntime.F_dvg_place(origin);
        F_dv_require(fromRoom != toRoom && R.Text(Field(fromPlace, "map", "")) == map && R.Text(Field(fromPlace, "room", "")) == fromRoom, "Walking origin differs from the committed location.");
        object action = false;
        foreach (var candidate in R.Items(Field(actor, "actions", false)))
        {
            var travel = Field(candidate, "travel", new List<object>());
            if (Field(candidate, "kind", "") is not "move" || Field(travel, "station", false) is not false
                || !R.Bool(R.eq(Field(travel, "from", false), origin))) continue;
            var destination = CampaignRuntime.F_dvg_place(Field(travel, "to", ""));
            if (R.Text(Field(destination, "map", "")) != map || R.Text(Field(destination, "room", "")) != toRoom) continue;
            if (R.Text(F_dv_truth(physical, Field(candidate, "authority_precondition", Field(candidate, "precondition", false)))) != "true") continue;
            action = candidate;
            break;
        }
        F_dv_require(action is not false, "The crossing has no enabled DV movement law.");
        var time = Field(physical, "time", 0d);
        var events = F_dv_clone(Field(state, "events", false));
        var eventId = F_dvs_event(events, "physical_crossing", actorId, time, new List<object>(), receipt);
        var execution = F_dv_execute(physical, Field(action, "law", ""), eventId, time, Field(physical, "revision", 0d));
        F_dv_require(Field(execution, "status", "") is "applied", "The movement law did not apply.");
        var after = Field(execution, "world", false);
        var bindings = Field(state, "bindings", false);
        var mind = Field(actor, "mind", false);
        var grants = Field(action, "grants", new List<object>());
        var observations = F_dv_perceive(after, mind, bindings, grants, eventId, time);
        var observed = Field(F_dv_update_mind(mind, observations, Field(mind, "revision", 0d), F_dvs_observation_profile(actor)), "world", false);
        actor = R.record_with(actor, "mind", observed);
        var observationEvent = F_dvs_event(events, "observation", actorId, time, F_dv_single(eventId),
            new List<object> { F_dv_pair("observations", observations), F_dv_pair("grants", grants), F_dv_pair("execution_id", eventId) });
        actor = R.record_with(actor, "last_cognitive_event", observationEvent);
        actors = F_dvs_replace(actors, (double)index, actor);
        actors = F_dvs_notice_movement_witnesses(actors, (double)index, physical, after, bindings, events, time, eventId);
        actor = F_dvs_notice_peers(actor, after, actors, bindings, events, time, eventId);
        actor = F_dvs_notice_visible_objects(actor, after, bindings, events, time, eventId);
        actors = F_dvs_replace(actors, (double)index, actor);
        story = R.record_with(story, "locomotion_sequence", sequence);
        var result = R.record_with(R.record_with(R.record_with(R.record_with(state, "physical", after), "actors", actors), "events", events), "story", story);
        if (pending is not false)
        {
            // Preserve the binding version handed to the native executor.
            pending = R.record_with(pending, "execution_state_version", Field(pending, "execution_state_version", (double)Version));
            result = R.record_with(result, "pending", pending);
        }
        // Chapter progression remains authored and runs at ordinary boundaries.
        return result;
    }
}
