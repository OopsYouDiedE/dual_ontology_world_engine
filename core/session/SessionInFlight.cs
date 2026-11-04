using DuelView.Compiled;
using DuelView.Core;

public sealed partial class DvSession
{
    private sealed record InFlightMovement(object Pending, long PreparedVersion, string ContentHash,
        string OriginMap, string OriginRoom, string OriginLocation, bool BackgroundActivity = false);
    private readonly Dictionary<string, InFlightMovement> inFlightMovements = new(StringComparer.Ordinal);
    public bool HasInFlightMovements => inFlightMovements.Count > 0;
    public bool HasPreparedAction => HasPending(state);
    public IReadOnlyList<string> InFlightActionEvents => inFlightMovements.Keys.ToArray();

    private bool IsActorInFlight(string actor) => inFlightMovements.Values
        .Any(movement => R.Text(Field(movement.Pending, "id", "")) == actor);

    /// <summary>Release the scheduling slot after the host has started a genuine movement.
    /// The actor remains at the committed origin and cannot act again until its receipt.
    /// Keep the original intent, spatial binding and execution cost for that receipt.</summary>
    public bool DetachPreparedMovement(string actionEvent) => DetachPreparedAction(actionEvent, false);

    public bool DetachPreparedActivity(string actionEvent) => DetachPreparedAction(actionEvent, true);

    public CaseComputationState GetInFlightComputationStatus(string actionEvent)
    {
        using var valuesScope = R.UseValues(valueContext);
        if (!inFlightMovements.TryGetValue(actionEvent, out var activity))
            return new("unknown_action", false, true, "The in-flight action is absent.");
        return activity.BackgroundActivity
            ? GetCaseComputationStatus(R.Text(Field(activity.Pending, "id", "")))
            : new("not_required", true, false, null);
    }

    private bool DetachPreparedAction(string actionEvent, bool backgroundActivity)
    {
        using var valuesScope = R.UseValues(valueContext);
        if (IsClosed) throw new InvalidOperationException("The session is closed.");
        var pending = Field(state, "pending", false);
        if (pending is false || R.Text(Field(pending, "action_event", "")) != actionEvent) return false;
        var action = Field(pending, "action", new List<object>());
        if (R.Bool(DVKernel.F_dvs_action_requires_argument(action))) return false;
        if (backgroundActivity)
        {
            // Only explicitly declared, actor-owned cognition may use this path.
            // Spatial and multi-person work still requires its normal executor.
            if (!R.Bool(Field(action, "background_activity", false))
                || !R.Bool(Field(action, "case_computation_required", false))
                || R.Text(Field(action, "kind", "")) is "move" or "talk"
                || R.Items(Field(action, "travel", new List<object>())).Count > 0
                || R.Text(Field(action, "spatial_execution_ref", "")).Length > 0
                || R.Items(Field(Field(action, "opportunity", new List<object>()), "participants", new List<object>())).Count > 0)
                return false;
        }
        else if (R.Text(Field(action, "kind", "")) != "move") return false;
        var actorId = R.Text(Field(pending, "id", ""));
        if (IsActorInFlight(actorId)) throw new InvalidOperationException("Actor already has an in-flight movement.");
        var origin = ExecutorProjection(actorId);
        var movement = new InFlightMovement(R.snapshot(pending), Version, content.Hash,
            HostValues.Text(origin, "map"), HostValues.Text(origin, "room"), HostValues.Text(origin, "logical_location"), backgroundActivity);
        var index = R.Num(Field(pending, "index", -1d));
        var actors = Field(state, "actors", new List<object>());
        var actor = R.record_with(R.at(actors, index), "in_flight_action", actionEvent);
        var story = Field(state, "story", new List<object>());
        var waitingCursor = Field(story, "background_waiting_cursor", false);
        var events = DVKernel.F_dv_clone(Field(state, "events", new List<object>()));
        Run(() => DVKernel.F_dvs_event(events, backgroundActivity ? "background_activity_dispatched" : "movement_dispatched", actorId,
            Field(pending, "time", 0d), DVKernel.F_dv_single(Field(pending, "parent", "")),
            new List<object> { DVKernel.F_dv_pair("action_event", actionEvent),
                DVKernel.F_dv_pair("prepared_version", (double)Version),
                DVKernel.F_dv_pair("arrival_committed", false) }));
        BeginTransition();
        inFlightMovements.Add(actionEvent, movement);
        state = R.record_with(state, "actors", DVKernel.F_dvs_replace(actors, index, actor));
        state = R.record_with(state, "events", events);
        state = R.record_with(R.record_with(state, "pending", false), "host_motion_wait", false);
        if (actorId == R.Text(Field(state, "player_actor", "")))
        {
            // The player waits for this real receipt while NPCs keep their own
            // background scheduling lane. Intent hides choices under this lock.
            state = R.record_with(R.record_with(state, "cursor", index), "awaiting_player", true);
            state = R.record_with(state, "player_selection", "");
        }
        else if (waitingCursor is not false)
        {
            state = R.record_with(state, "story", DVKernel.F_dv_without(story, "background_waiting_cursor"));
            state = R.record_with(R.record_with(state, "cursor", waitingCursor), "awaiting_player", true);
        }
        else
        {
            state = R.record_with(state, "cursor", (index + 1) % R.Items(actors).Count);
            state = R.record_with(state, "awaiting_player", false);
        }
        // In-flight state is intentionally not a durable checkpoint.
        RememberCommitted();
        return true;
    }

    private IReadOnlyDictionary<string, object?> CompleteInFlightMovement(object receipt, string viewer, string actionEvent)
    {
        if (HasPending(state)) throw new InvalidOperationException("Complete the prepared action before delivering an in-flight receipt.");
        var movement = inFlightMovements[actionEvent];
        if (movement.ContentHash != content.Hash) throw new InvalidDataException("In-flight content changed.");
        var interruption = Field(Field(receipt, "field", new List<object>()), "movement_interruption", false);
        if (interruption is not false)
        {
            if (movement.BackgroundActivity) throw new InvalidDataException("Cognitive activity cannot supply a movement interruption.");
            ValidateMovementInterruption(movement, receipt, interruption);
        }
        var pending = movement.Pending;
        var actors = Field(state, "actors", new List<object>());
        var index = R.Num(Field(pending, "index", -1d));
        var actor = R.at(actors, index);
        if (R.Text(Field(actor, "id", "")) != R.Text(Field(pending, "id", ""))
            || R.Text(Field(actor, "in_flight_action", "")) != actionEvent)
            throw new InvalidDataException("In-flight actor binding changed.");
        var physical = Field(state, "physical", new List<object>());
        var oldQuote = Field(pending, "quote", new List<object>());
        // Quote against the current ledger/world; completion rechecks the original
        // action's preconditions and native arrival proof against this current state.
        // No saved world, actor cognition or ledger is restored from the dispatch.
        var quote = DVKernel.F_dv_action_cost_quote(Field(actor, "ledger", new List<object>()), physical,
            Field(oldQuote, "action", ""), actionEvent, Field(oldQuote, "bounds", new List<object>()));
        quote = R.record_with(quote, "scheduled_start", Field(oldQuote, "scheduled_start", 0d));
        actor = DVKernel.F_dv_without(actor, "in_flight_action");
        pending = R.record_with(R.record_with(pending, "actor", actor), "mind", Field(actor, "mind", new List<object>()));
        pending = R.record_with(R.record_with(pending, "ledger", Field(actor, "ledger", new List<object>())), "quote", quote);
        pending = R.record_with(pending, "time", Field(physical, "time", 0d));
        var before = state;
        var beforeEventCount = Events(before).Count;
        var waitingCursor = Field(state, "cursor", 0d);
        var wasAwaiting = R.Bool(Field(state, "awaiting_player", false));
        var events = DVKernel.F_dv_clone(Field(state, "events", new List<object>()));
        var revalidation = Run(() => DVKernel.F_dvs_event(events, movement.BackgroundActivity ? "background_activity_revalidated" : "movement_revalidated", Field(pending, "id", ""),
            Field(physical, "time", 0d), DVKernel.F_dv_single(Field(pending, "parent", "")),
            new List<object> { DVKernel.F_dv_pair("action_event", actionEvent),
                DVKernel.F_dv_pair("dispatch_quote", oldQuote), DVKernel.F_dv_pair("current_quote", quote) }));
        pending = R.record_with(pending, "parent", revalidation);
        state = R.record_with(state, "actors", DVKernel.F_dvs_replace(actors, index, actor));
        state = R.record_with(state, "events", events);
        state = R.record_with(R.record_with(state, "pending", pending), "host_motion_wait", false);
        try
        {
            var presentation = CompletePreparedDv(receipt, viewer, movement.PreparedVersion);
            inFlightMovements.Remove(actionEvent);
            // A background arrival must not steal an unrelated player's input slot.
            if (wasAwaiting && !HasPending(state))
                state = RestoreInFlightWaitingCursor(state, waitingCursor, index);
            InvalidateInFlightProjection();
            RememberCommitted();
            if (interruption is not false)
            {
                var interrupted = new Dictionary<string, object?>(presentation, StringComparer.Ordinal)
                {
                    ["movement_interrupted"] = true,
                    ["movement_interruption"] = converter.FromDv(interruption)
                };
                return HostValues.Freeze(interrupted);
            }
            return presentation;
        }
        catch
        {
            // Projection/reporting can fail after Core has committed a real receipt.
            // Keep that evidence; only an uncommitted execution may roll back.
            var committed = Events(state).Skip(beforeEventCount).Any(entry =>
                R.Text(Field(entry, "type", "")) == "host_completion"
                && R.Text(Field(Field(entry, "data", new List<object>()), "action_event", "")) == actionEvent);
            if (committed)
            {
                inFlightMovements.Remove(actionEvent);
                if (wasAwaiting && !HasPending(state))
                    state = RestoreInFlightWaitingCursor(state, waitingCursor, index);
                RememberCommitted();
            }
            else state = before;
            InvalidateInFlightProjection();
            throw;
        }
    }

    private static object RestoreInFlightWaitingCursor(object current, object waitingCursor, double completedIndex)
    {
        // Finishing the waiting player's own action reopens normal preparation;
        // another actor's receipt preserves the player's locked waiting lane.
        return R.record_with(R.record_with(current, "cursor", waitingCursor),
            "awaiting_player", R.Num(waitingCursor) != completedIndex);
    }

    private void ValidateMovementInterruption(InFlightMovement movement, object receipt, object proof)
    {
        void Require(bool valid, string detail)
        {
            if (!valid) throw new InvalidDataException("Invalid movement interruption: " + detail);
        }
        string Text(object record, string key) => R.Text(Field(record, key, ""));
        double Number(object record, string key)
        {
            var value = Field(record, key, false);
            return R.Bool(R.is_numeric_value(value)) ? R.Num(value) : double.NaN;
        }
        double[] Position(string key, int dimensions)
        {
            var value = Field(proof, key, false);
            Require(R.Bool(R.is_list(value)), key);
            var items = R.Items(value);
            Require(items.Count == dimensions && items.All(item => R.Bool(R.is_numeric_value(item)) && double.IsFinite(R.Num(item))), key);
            return items.Select(R.Num).ToArray();
        }
        var actorId = Text(movement.Pending, "id");
        var field = Field(receipt, "field", new List<object>());
        Require(Text(receipt, "executor") == "godot" && Text(receipt, "outcome") == "cancelled", "receipt outcome");
        Require(Text(field, "content_hash") == movement.ContentHash
            && Number(field, "state_version") == movement.PreparedVersion, "prepared binding");
        Require(Text(proof, "protocol") == "native-nearby-interception-v1" && Text(proof, "actor") == actorId, "actor identity");
        Require(R.Bool(Field(proof, "same_world", false)) && R.Bool(Field(proof, "stopped", false))
            && R.Bool(Field(proof, "identity_verified", false)) && R.Bool(Field(proof, "final_on_floor", false)), "physical stop");
        Require(movement.OriginMap.Length > 0 && movement.OriginRoom.Length > 0 && movement.OriginLocation.Length > 0
            && Text(proof, "map") == movement.OriginMap && Text(proof, "source_room") == movement.OriginRoom
            && Text(proof, "actual_room") == movement.OriginRoom, "movement left the origin room");
        var interceptorId = Text(proof, "interceptor");
        Require(interceptorId.Length > 0 && interceptorId != actorId && !IsActorInFlight(interceptorId), "interceptor identity");
        var current = ExecutorProjection(actorId);
        Require(HostValues.Text(current, "map") == movement.OriginMap && HostValues.Text(current, "room") == movement.OriginRoom
            && HostValues.Text(current, "logical_location") == movement.OriginLocation, "committed origin changed");
        var actors = R.Items(Field(state, "actors", new List<object>()));
        var actor = actors.Single(item => Text(item, "id") == actorId);
        var interceptor = actors.FirstOrDefault(item => Text(item, "id") == interceptorId);
        Require(interceptor is not null, "unknown interceptor");
        var peer = ExecutorProjection(interceptorId);
        Require(HostValues.Text(peer, "map") == movement.OriginMap && HostValues.Text(peer, "room") == movement.OriginRoom
            && HostValues.Text(peer, "logical_location") == movement.OriginLocation, "interceptor room");
        double Range(object person)
        {
            var autonomy = Field(Field(person, "policy", new List<object>()), "sims_autonomy", new List<object>());
            return Number(Field(autonomy, "conversation", new List<object>()), "maximum_distance_m");
        }
        var limit = Math.Min(Range(actor), Range(interceptor!));
        var position = Position("physical_position", 3);
        var peerPosition = Position("interceptor_physical_position", 3);
        _ = Position("actual_position", 2);
        var distance = Math.Sqrt(position.Zip(peerPosition, (a, b) => (a - b) * (a - b)).Sum());
        Require(double.IsFinite(limit) && limit > 0 && distance <= limit, "outside DV conversation range");
        var reported = Number(proof, "distance_m");
        Require(double.IsFinite(reported) && reported >= 0 && Math.Abs(reported - distance) <= 0.001, "distance evidence");
    }

    private void InvalidateInFlightProjection()
    {
        intentProjection = null;
        nativeIntent = null;
        executors.Clear();
        foreach (var viewer in viewers.Values) viewer.Version = -1;
        ClearObserverProjection();
    }

    private void RecordUnfinishedMovementsAtClose()
    {
        if (!HasInFlightMovements) return;
        using var valuesScope = R.UseValues(valueContext);
        var events = DVKernel.F_dv_clone(Field(state, "events", new List<object>()));
        foreach (var movement in inFlightMovements.Values)
        {
            var pending = movement.Pending;
            Run(() => DVKernel.F_dvs_event(events, movement.BackgroundActivity ? "background_activity_unfinished_at_close" : "movement_unfinished_at_close", Field(pending, "id", ""),
                Field(Field(state, "physical", new List<object>()), "time", 0d),
                DVKernel.F_dv_single(Field(pending, "parent", "")),
                new List<object> { DVKernel.F_dv_pair("action_event", Field(pending, "action_event", "")),
                    DVKernel.F_dv_pair("arrival_committed", false), DVKernel.F_dv_pair("reason", "host-closed-before-receipt") }));
        }
        state = R.record_with(state, "events", events);
    }
}
