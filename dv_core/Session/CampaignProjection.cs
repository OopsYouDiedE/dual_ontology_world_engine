using DuelView.Compiled;
using static DuelView.Core.DVKernel;

// Locations, labels, identity rules and cards enter through campaign content.
// Projection exposes existing state and never grants observations.
public static partial class CampaignRuntime
{
    public static object F_dvg_self_purpose(object action)
    {
        var kind = R.Text(F_dv_get(action, "kind"));
        if (R.Bool(F_dv_find(action, "known_opportunity_wait", false)) || kind == "wait") return "wait";
        return kind is "replay" or "infer" ? "reasoning" : "personal";
    }

    public static object F_dvg_intent(object state)
    {
        var intent = F_dvs_session_intent(state);
        var status = R.Text(F_dv_get(intent, "status"));
        if (status == "awaiting_input")
        {
            var options = new List<object>();
            var actor = R.at(F_dv_get(state, "actors"), F_dv_get(state, "cursor"));
            foreach (var option in R.Items(F_dv_get(intent, "options")))
            {
                var action = F_dvp_find_action(F_dv_get(actor, "actions"), F_dv_get(option, "id"));
                var kind = F_dv_get(action, "kind");
                var offered = F_dv_with_many(option, CampaignRecord(
                    ("label", F_dvg_observer_action_label(state, F_dv_get(actor, "id"), action)),
                    ("kind", kind), ("interaction", F_dvg_interaction(action)),
                    ("cost", F_dv_get(F_dv_get(F_dv_get(state, "costs"), kind), "actual"))));
                if (R.Text(kind) == "replay")
                    offered = F_dv_with(offered, "replay_input", F_dvg_manual_replay_input(actor, action));
                options.Add(offered);
            }
            return F_dv_with(intent, "options", options);
        }
        if (status != "prepared") return intent;
        var viewer = F_dv_find(state, "player_actor", "");
        if (R.Text(viewer) != "")
        {
            var action = F_dv_get(intent, "action");
            intent = F_dv_with(intent, "action", F_dv_with(action, "label", F_dvg_observer_action_label(state, viewer, action)));
        }
        var preparedAction = F_dv_get(intent, "action");
        var travel = F_dv_find(preparedAction, "travel", new List<object>());
        var hasStation = R.Bool(F_dv_has_key(travel, "station"));
        if (R.Text(F_dv_get(preparedAction, "kind")) != "move" && !hasStation) return intent;
        var target = F_dv_find(travel, "to", "");
        var place = F_dvg_place(target);
        var destination = F_dv_with_many(place, CampaignRecord(
            ("execution_room", F_dv_get(place, "room")), ("logical_location", target)));
        if (hasStation) destination = F_dv_with(destination, "departure_station", F_dv_get(travel, "station"));
        return F_dv_with(intent, "destination", destination);
    }

    public static object F_dvg_person_motion(object actor, object now)
    {
        var queued = F_dv_find(actor, "scheduled_action", false);
        if (queued is false) return false;
        var action = F_dv_get(queued, "action");
        if (R.Text(F_dv_get(action, "kind")) != "move") return false;
        var travel = F_dv_find(action, "travel", false);
        if (travel is false) return false;
        var from = F_dv_get(travel, "from");
        var to = F_dv_get(travel, "to");
        var origin = F_dvg_place(from);
        var destination = F_dvg_place(to);
        if (R.Text(F_dv_get(origin, "map")) == "" || R.Text(F_dv_get(destination, "map")) == "") return false;
        var start = F_dv_get(queued, "start");
        var due = F_dv_get(queued, "due");
        object progress = 0d;
        if (R.Bool(R.lt(start, due)))
            progress = F_dv_min(1d, F_dv_max(0d, R.div(R.sub(now, start), R.sub(due, start))));
        return CampaignRecord(
            ("action_id", F_dv_get(action, "id")),
            ("from", CampaignRecord(("logical_location", from), ("map", F_dv_get(origin, "map")), ("execution_room", F_dv_get(origin, "room")))),
            ("to", CampaignRecord(("logical_location", to), ("map", F_dv_get(destination, "map")), ("execution_room", F_dv_get(destination, "room")))),
            ("start", start), ("due", due), ("now", now), ("progress", progress),
            ("scope", "host-presentation-only-awaits-formal-receipt"));
    }
}
