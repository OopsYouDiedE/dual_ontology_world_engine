// @mechanism: native_3d_architect_and_prefabs
// @layer: core/spatial
// @contracts: NavigationGraph

using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using DuelView.Runtime;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVKernel;

// --- Source: NavigationGraph.cs ---
namespace DuelView.Runtime
{


public sealed record RouteNode(string Id, string Room);
public sealed record RouteEdge(string From, string To, double Meters, Condition Gate);
public sealed record RoutePlan(IReadOnlyList<RouteNode> Nodes, double Meters);

/// <summary>The logical graph lives for the session. Scene residency never deletes a
/// node, a door condition or an actor's journey. Search work is resumable.</summary>
public sealed class NavigationGraph
{
    private readonly Dictionary<string, RouteNode> nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<RouteEdge>> edges = new(StringComparer.Ordinal);

    public NavigationGraph(JsonElement declaration)
    {
        foreach (var item in declaration.Get("nodes").Items())
        {
            var id = item.Get("id").Text();
            var room = item.Get("room").Text();
            if (id.Length == 0 || room.Length == 0) throw new InvalidDataException("Route node needs identity and room.");
            nodes.Add(id, new(id, room));
            edges.Add(id, []);
        }
        foreach (var item in declaration.Get("edges").Items())
        {
            var from = item.Get("from").Text();
            var to = item.Get("to").Text();
            var meters = item.Get("meters").Number(double.NaN);
            if (!nodes.ContainsKey(from) || !nodes.ContainsKey(to) || !double.IsFinite(meters) || meters <= 0)
                throw new InvalidDataException("Invalid route edge.");
            var gate = Condition.Compile(item.Get("gate"), "world");
            edges[from].Add(new(from, to, meters, gate));
            if (item.Get("bidirectional").Flag()) edges[to].Add(new(to, from, meters, gate));
        }
    }

    public RouteSearch Begin(string from, string to, Func<FieldKey, Fact?> read)
    {
        if (!nodes.ContainsKey(from) || !nodes.ContainsKey(to)) throw new InvalidOperationException("Unknown route endpoint.");
        return new(this, from, to, read);
    }

    public sealed class RouteSearch
    {
        private readonly NavigationGraph graph;
        private readonly string start, goal;
        private readonly Func<FieldKey, Fact?> read;
        private readonly PriorityQueue<string, double> frontier = new();
        private readonly Dictionary<string, double> distance = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> previous = new(StringComparer.Ordinal);
        private readonly HashSet<string> closed = new(StringComparer.Ordinal);
        private string? expanding;
        private int edgeCursor;
        private string? rebuilding;
        private readonly List<RouteNode> reverse = [];
        public bool Finished { get; private set; }
        public RoutePlan? Result { get; private set; }
        public long EdgeEvaluations { get; private set; }

        internal RouteSearch(NavigationGraph graph, string start, string goal, Func<FieldKey, Fact?> read)
        {
            this.graph = graph; this.start = start; this.goal = goal; this.read = read;
            frontier.Enqueue(start, 0); distance.Add(start, 0);
        }

        public void Step(int operationBudget, double milliseconds)
        {
            if (operationBudget <= 0 || !double.IsFinite(milliseconds) || milliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(operationBudget));
            var timer = Stopwatch.StartNew();
            for (var count = 0; count < operationBudget && !Finished && timer.Elapsed.TotalMilliseconds < milliseconds; count++)
            {
                if (rebuilding is { } cursor)
                {
                    reverse.Add(graph.nodes[cursor]);
                    if (cursor == start)
                    {
                        reverse.Reverse();
                        Result = new(reverse.AsReadOnly(), distance[goal]);
                        Finished = true;
                    }
                    else rebuilding = previous[cursor];
                    continue;
                }
                if (expanding is null)
                {
                    if (!frontier.TryDequeue(out var next, out _)) { Finished = true; continue; }
                    if (!closed.Add(next)) continue;
                    if (next == goal) { rebuilding = goal; continue; }
                    expanding = next; edgeCursor = 0;
                }
                var adjacent = graph.edges[expanding];
                if (edgeCursor == adjacent.Count) { expanding = null; continue; }
                var edge = adjacent[edgeCursor++];
                EdgeEvaluations++;
                if (closed.Contains(edge.To) || edge.Gate.Evaluate(read) != Truth.True) continue;
                var candidate = distance[expanding] + edge.Meters;
                if (!double.IsFinite(candidate)) throw new InvalidDataException("Route distance exceeds numeric range.");
                if (distance.TryGetValue(edge.To, out var old) && old <= candidate) continue;
                distance[edge.To] = candidate; previous[edge.To] = expanding;
                frontier.Enqueue(edge.To, candidate);
            }
        }
    }
}
}

// --- Source: PathJourney.cs ---
namespace DuelView.Runtime
{

public readonly record struct PathPoint(double X,double Y,double Z)
{
    public double DistanceTo(PathPoint other)
    {
        var x=X-other.X;var y=Y-other.Y;var z=Z-other.Z;
        return Math.Sqrt(x*x+y*y+z*z);
    }
}
public sealed record PathJourneyState(PathPoint[] Points,double Distance,bool CompletionTaken);

/// <summary>A single host-produced navigation path. Distance, not frame count,
/// is authoritative while an actor is outside the physical simulation range.</summary>
public sealed class PathJourney
{
    private readonly PathPoint[] points;
    private readonly double[] cumulative;
    public double Distance { get; private set; }
    public double Length => cumulative[^1];
    public bool Arrived => Distance>=Length;
    public bool CompletionTaken { get; private set; }
    public PathJourneyState SaveState()=>new(points.ToArray(),Distance,CompletionTaken);
    public static PathJourney RestoreState(PathJourneyState state)
    {
        var journey=new PathJourney(state.Points);
        if(!double.IsFinite(state.Distance)||state.Distance<0||state.Distance>journey.Length || state.CompletionTaken && state.Distance<journey.Length)
            throw new InvalidDataException("Invalid path journey checkpoint.");
        journey.Distance=state.Distance;journey.CompletionTaken=state.CompletionTaken;return journey;
    }

    public PathJourney(IEnumerable<PathPoint> route)
    {
        points=route.ToArray();
        if(points.Length<2 || points.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||!double.IsFinite(p.Z)))
            throw new InvalidDataException("A journey requires a finite navigation path.");
        cumulative=new double[points.Length];
        for(var i=1;i<points.Length;i++)cumulative[i]=cumulative[i-1]+points[i-1].DistanceTo(points[i]);
        if(!double.IsFinite(Length)||Length<=0)throw new InvalidDataException("A journey requires a nonzero path.");
    }

    public PathPoint Position
    {
        get => PositionAt(Distance);
    }
    private PathPoint PositionAt(double distance)
    {
            var i=Array.BinarySearch(cumulative,distance);
            if(i>=0)return points[i];
            i=~i;
            var start=points[i-1];var end=points[i];
            var fraction=(distance-cumulative[i-1])/(cumulative[i]-cumulative[i-1]);
            return new(start.X+(end.X-start.X)*fraction,start.Y+(end.Y-start.Y)*fraction,start.Z+(end.Z-start.Z)*fraction);
    }

    public PathPoint Preview(double elapsedSeconds,double speed)
    {
        if(!double.IsFinite(elapsedSeconds)||elapsedSeconds<0||!double.IsFinite(speed)||speed<=0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        return PositionAt(Math.Min(Length,Distance+elapsedSeconds*speed));
    }

    public void Advance(double elapsedSeconds,double speed)
    {
        if(!double.IsFinite(elapsedSeconds)||elapsedSeconds<0||!double.IsFinite(speed)||speed<=0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        Distance=Math.Min(Length,Distance+elapsedSeconds*speed);
    }

    public bool TakeCompletion()
    {
        if(!Arrived||CompletionTaken)return false;
        CompletionTaken=true;return true;
    }
}
}

// --- Source: Navigation.cs ---
namespace DuelView.Core
{


/// <summary>Planning / Navigation: native Core mechanisms.</summary>
public static partial class DVSpatialKernel
{
    public static object F_dvs_sims_autonomy_score(object v_actor, object v_action, object actualSeconds)
    {
        object v_profile__394013 = F_dv_find(F_dv_get(v_actor, "policy"), "sims_autonomy", false);
        if (R.Bool(R.eq(v_profile__394013, false)))
        {
            return R.number("0");
        }
        object v_bonus__394119 = R.number("0");
        object v_room__394137 = "";
        if (R.Bool(F_dv_has_key(v_actor, "location_ref")))
        {
            object v_pos__394201 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "location_ref"));
            if (R.Bool((R.Bool(F_dv_uncontested(v_pos__394201)) ? (object)(R.is_text(F_dv_get(v_pos__394201, "value"))) : (object)(false))))
            {
                v_room__394137 = F_dv_get(v_pos__394201, "value");
            }
        }
        object v_habits__394342 = F_dv_find(v_profile__394013, "implicit_habits", new System.Collections.Generic.List<object>(0) { });
        object v_i__394399 = R.number("0");
        while (R.Bool(R.lt(v_i__394399, R.count(v_habits__394342))))
        {
            object v_habit__394449 = R.at(v_habits__394342, v_i__394399);
            if (R.Bool((R.Bool(R.eq(F_dv_find(v_habit__394449, "trigger", ""), v_room__394137)) ? (object)(R.eq(F_dv_find(v_habit__394449, "kind", ""), F_dv_get(v_action, "kind"))) : (object)(false))))
            {
                v_bonus__394119 = R.add(v_bonus__394119, F_dv_find(v_habit__394449, "bonus", R.number("0")));
            }
            v_i__394399 = R.add(v_i__394399, R.number("1"));
        }
        object v_weights__394644 = F_dv_find(v_profile__394013, "autonomy_weights", new System.Collections.Generic.List<object>(0) { });
        var actionWeights = F_dv_find(v_profile__394013, "action_weights", new List<object>());
        var weight = F_dv_find(actionWeights, F_dv_get(v_action, "kind"), false);
        if (weight is not false)
            v_bonus__394119 = R.add(v_bonus__394119, R.mul(F_dv_get(weight, "scale"),
                F_dv_get(v_weights__394644, F_dv_get(weight, "need"))));
        var perceived = F_dv_find(v_action, "perceived_need_effects", false);
        if (perceived is false) perceived = F_dv_find(F_dv_find(v_profile__394013, "action_predictions", new List<object>()), F_dv_get(v_action, "kind"), false);
        if (perceived is not false)
        {
            static Dictionary<string, double> Numbers(object record) => R.Items(record).ToDictionary(
                pair => R.Text(R.at(pair, 0d)), pair => R.Num(R.at(pair, 1d)), StringComparer.Ordinal);
            var score = SpatialAutonomy.Score(
                Numbers(F_dvs_autonomy_needs(v_actor)), Numbers(v_weights__394644), Numbers(perceived),
                R.Num(F_dv_find(v_action, "predicted_seconds", actualSeconds)),
                R.Num(F_dv_find(v_profile__394013, "time_cost", 0d)));
            v_bonus__394119 = R.add(v_bonus__394119, score);
        }
        return v_bonus__394119;
    }

    // A journey declared for an observation remains useful only while that
    // owner's evidence is missing or older than the declared trigger.
    public static object F_dvs_navigation_evidence_pending(object actor, object goal)
    {
        var targets = F_dv_find(goal, "navigation_evidence_refs", new List<object>());
        if (R.Bool(R.eq(R.count(targets), R.number("0")))) return true;
        var goals = F_dvp_refresh_current_evidence(actor,
            F_dv_find(F_dv_get(actor, "policy"), "goals", new List<object>()));
        foreach (var target in R.Items(targets))
        foreach (var evidence in R.Items(goals))
        {
            if (!R.Bool(R.eq(F_dv_find(evidence, "kind", ""), "evidence")) ||
                !R.Bool(F_dv_same(F_dv_find(evidence, "ref", false), target)) ||
                !R.Bool(R.eq(F_dv_truth(F_dv_get(actor, "mind"),
                    F_dv_find(evidence, "available_when", F_dv_true())), "true")) ||
                !R.Bool(F_dvp_evidence_due(actor, evidence))) continue;
            if (!R.Bool(F_dvp_source_supported_since(F_dv_read(F_dv_get(actor, "mind"), target),
                F_dv_find(evidence, "source", "perception"),
                F_dv_find(evidence, "minimum_time", R.number("0"))))) return true;
        }
        return false;
    }

    public static object F_dvs_pending_achieve_goals(object v_actor) => PendingAchieveGoals(v_actor, false);

    private static object ReadPendingAchieveGoals(object actor) => PendingAchieveGoals(R.snapshot(actor), true);

    private static object PendingAchieveGoals(object v_actor, bool readOnly)
    {
        var key = new List<object> { "owned-pending-achieve-goals-v1" };
        var cached = readOnly ? ReadCachedValue("plan", v_actor, key) : F_dvs_plan_cache_get(v_actor, key);
        if (R.Bool(R.at(cached, R.number("0")))) return R.at(cached, R.number("1"));
        object v_goals__395037 = F_dv_find(F_dv_get(v_actor, "policy"), "goals", new System.Collections.Generic.List<object>(0) { });
        object v_result__395088 = new System.Collections.Generic.List<object>(0) { };
        object v_i__395108 = R.number("0");
        while (R.Bool(R.lt(v_i__395108, R.count(v_goals__395037))))
        {
            object v_goal__395157 = R.at(v_goals__395037, v_i__395108);
            if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(F_dv_find(v_goal__395157, "kind", ""), "achieve")) ? (object)(F_dvp_topic(v_actor, v_goal__395157)) : (object)(false))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_goal__395157, "available_when", F_dv_true())), "true")) : (object)(false))) ? (object)(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_goal__395157, "proposition")), "true"))) : (object)(false))))
            {
                if (R.Bool(F_dvs_navigation_evidence_pending(v_actor, v_goal__395157)))
                    _ = R.push(v_result__395088, v_goal__395157);
            }
            v_i__395108 = R.add(v_i__395108, R.number("1"));
        }
        var result = F_dvs_plan_cache_put(v_actor, key, v_result__395088);
        return readOnly ? R.snapshot(result) : result;
    }

    public static object F_dvs_navigation_guard(object v_condition, object v_location, object v_room)
    {
        object v_op__395660 = F_dv_find(v_condition, "op", "");
        if (R.Bool((R.Bool(R.eq(v_op__395660, "equals")) ? (object)(F_dv_same(F_dv_find(v_condition, "ref", false), v_location)) : (object)(false))))
        {
            if (R.Bool(R.eq(F_dv_find(v_condition, "value", ""), v_room)))
            {
                return F_dv_true();
            }
            return F_dv_not(F_dv_true());
        }
        if (R.Bool(R.eq(v_op__395660, "not")))
        {
            return F_dv_not(F_dvs_navigation_guard(F_dv_get(v_condition, "item"), v_location, v_room));
        }
        if (R.Bool(R.eq(v_op__395660, "all")))
        {
            object v_items__396015 = new System.Collections.Generic.List<object>(0) { };
            object v_i__396038 = R.number("0");
            while (R.Bool(R.lt(v_i__396038, R.count(F_dv_get(v_condition, "items")))))
            {
                _ = R.push(v_items__396015, F_dvs_navigation_guard(R.at(F_dv_get(v_condition, "items"), v_i__396038), v_location, v_room));
                v_i__396038 = R.add(v_i__396038, R.number("1"));
            }
            return F_dv_all(v_items__396015);
        }
        return v_condition;
    }

    public static object F_dvs_navigation_distance(object v_actor, object v_origin, object v_target)
    {
        if (R.Bool(R.eq(v_origin, v_target)))
        {
            return R.number("0");
        }
        object v_key__396348 = new System.Collections.Generic.List<object>(5) { "declared-navigation-distance-v1", F_dv_get(v_actor, "actions"), F_dv_get(v_actor, "location_ref"), v_origin, v_target };
        object v_cached__396449 = F_dvs_plan_cache_get(F_dv_get(v_actor, "mind"), v_key__396348);
        if (R.Bool(R.at(v_cached__396449, R.number("0"))))
        {
            return R.at(v_cached__396449, R.number("1"));
        }
        object v_rooms__396545 = new System.Collections.Generic.List<object>(1) { v_origin };
        object v_distances__396570 = new System.Collections.Generic.List<object>(1) { R.number("0") };
        object v_cursor__396594 = R.number("0");
        while (R.Bool(R.lt(v_cursor__396594, R.count(v_rooms__396545))))
        {
            object v_room__396653 = R.at(v_rooms__396545, v_cursor__396594);
            object v_i__396686 = R.number("0");
            while (R.Bool(R.lt(v_i__396686, R.count(F_dv_get(v_actor, "actions")))))
            {
                object v_edge__396751 = R.at(F_dv_get(v_actor, "actions"), v_i__396686);
                if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_edge__396751, "kind"), "move")) ? (object)(F_dv_has_key(v_edge__396751, "travel")) : (object)(false))) ? (object)(R.eq(F_dv_get(F_dv_get(v_edge__396751, "travel"), "from"), v_room__396653)) : (object)(false))) ? (object)(R.not(R.eq(F_dv_get(F_dv_get(v_edge__396751, "travel"), "to"), v_room__396653))) : (object)(false))) ? (object)(R.lt(R.number("0"), R.count(F_dv_find(v_edge__396751, "source_refs", new System.Collections.Generic.List<object>(0) { })))) : (object)(false))))
                {
                    object v_guard__396969 = F_dvs_navigation_guard(F_dv_get(v_edge__396751, "precondition"), F_dv_get(v_actor, "location_ref"), v_room__396653);
                    if (R.Bool((R.Bool(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), v_guard__396969), "true")) ? (object)(R.not(F_dv_has(v_rooms__396545, F_dv_get(F_dv_get(v_edge__396751, "travel"), "to")))) : (object)(false))))
                    {
                        object v_distance__397163 = R.add(R.at(v_distances__396570, v_cursor__396594), R.number("1"));
                        if (R.Bool(R.eq(F_dv_get(F_dv_get(v_edge__396751, "travel"), "to"), v_target)))
                        {
                            return F_dvs_plan_cache_put(F_dv_get(v_actor, "mind"), v_key__396348, v_distance__397163);
                        }
                        _ = R.push(v_rooms__396545, F_dv_get(F_dv_get(v_edge__396751, "travel"), "to"));
                        _ = R.push(v_distances__396570, v_distance__397163);
                    }
                }
                v_i__396686 = R.add(v_i__396686, R.number("1"));
            }
            v_cursor__396594 = R.add(v_cursor__396594, R.number("1"));
        }
        return F_dvs_plan_cache_put(F_dv_get(v_actor, "mind"), v_key__396348, R.sub(R.number("0"), R.number("1")));
    }

    private static object GoalNavigationTargets(object v_actor, object v_pending_achieve)
    {
        v_actor = R.snapshot(v_actor);
        v_pending_achieve = R.snapshot(v_pending_achieve);
        // Every move candidate queries the same owned opportunity set. The
        // complete actor snapshot and the explicit pending-goal argument own
        // this cache, including mind, requests, policy and action parameters.
        var key = new List<object> { "owned-navigation-target-rooms-v3", v_pending_achieve };
        var cached = ReadCachedValue("plan", v_actor, key);
        if (R.Bool(R.at(cached, R.number("0")))) return R.at(cached, R.number("1"));
        // This declaration selects the derived navigation scope. Actual
        // actions still undergo choose's ordinary eligibility and cost checks;
        // unselected hypothetical destinations need no additional admission.
        object selected = new List<object>();
        bool hasSelection = false;
        foreach (var goal in R.Items(F_dv_find(F_dv_get(v_actor, "policy"), "goals", new List<object>())))
        {
            if (!R.Bool(F_dv_has_key(goal, "navigation_intention"))) continue;
            var proposition = F_dv_get(goal, "proposition");
            var positive = R.lt(R.number("0"), F_dv_find(goal, "weight", R.number("1")));
            var available = F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(goal, "available_when", F_dv_true()));
            var based = R.lt(R.number("0"), R.count(F_dv_find(goal, "basis", new List<object>())));
            if (!R.Bool(positive) || !R.Bool(based) || !R.Bool(R.eq(available, "true")) ||
                !R.Bool(R.eq(F_dv_find(goal, "kind", ""), "achieve")) ||
                !R.Bool(R.eq(F_dv_find(proposition, "op", ""), "equals"))) continue;
            if (!R.Bool(F_dv_same(F_dv_get(proposition, "ref"), F_dv_get(v_actor, "location_ref")))) continue;
            var room = F_dvp_extract_required_room(proposition, F_dv_get(v_actor, "location_ref"));
            if (R.Bool(R.eq(room, ""))) continue;
            hasSelection = true;
            if (!R.Bool(F_dv_has(selected, room))) _ = R.push(selected, room);
        }
        if (hasSelection) return R.snapshot(F_dvs_plan_cache_put(v_actor, key, selected));
        object v_target_rooms__397793 = new System.Collections.Generic.List<object>(0) { };
        object v_i__397819 = R.number("0");
        while (R.Bool(R.lt(v_i__397819, R.count(v_pending_achieve))))
        {
            object v_room__397878 = F_dvp_extract_required_room(F_dv_get(R.at(v_pending_achieve, v_i__397819), "proposition"), F_dv_get(v_actor, "location_ref"));
            var weight = F_dv_find(R.at(v_pending_achieve, v_i__397819), "weight", R.number("1"));
            if (R.Bool(R.lt(R.number("0"), weight)) && R.Bool((R.Bool(R.not(R.eq(v_room__397878, ""))) ? (object)(R.not(F_dv_has(v_target_rooms__397793, v_room__397878))) : (object)(false))))
            {
                _ = R.push(v_target_rooms__397793, v_room__397878);
            }
            v_i__397819 = R.add(v_i__397819, R.number("1"));
        }
        object v_actions__398087 = F_dv_find(v_actor, "actions", new System.Collections.Generic.List<object>(0) { });
        object? relevant = null;
        v_i__397819 = R.number("0");
        while (R.Bool(R.lt(v_i__397819, R.count(v_actions__398087))))
        {
            object v_other__398182 = R.at(v_actions__398087, v_i__397819);
            if (R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_other__398182, "kind"), "move"))) ? (object)((R.Bool((R.Bool(F_dvp_communication_progress(v_actor, v_other__398182)) ? (object)(true) : (object)(R.lt(R.number("0"), F_dv_get(F_dvp_achievement_pending_work(v_actor, v_other__398182, v_pending_achieve), "value"))))) ? (object)(true) : (object)((R.Bool(F_dv_has_key(v_other__398182, "response_topic")) ? (object)(R.lt(R.number("0"), R.count(F_dvs_pending_request(v_actor, v_other__398182)))) : (object)(false))))) : (object)(false))))
            {
                object v_req_room__398463 = F_dvp_extract_required_room(F_dv_get(v_other__398182, "precondition"), F_dv_get(v_actor, "location_ref"));
                if (R.Bool((R.Bool(R.not(R.eq(v_req_room__398463, ""))) ? (object)(R.not(F_dv_has(v_target_rooms__397793, v_req_room__398463))) : (object)(false))))
                {
                    object v_guarded__398947 = F_dv_with(v_other__398182, "precondition", F_dvs_navigation_guard(F_dv_get(v_other__398182, "precondition"), F_dv_get(v_actor, "location_ref"), v_req_room__398463));
                    if (R.Bool(F_dvp_eligible(v_actor, v_guarded__398947, F_dv_get(v_actor, "mind"), relevant ??= ReadGoalRefs(v_actor))))
                    {
                        _ = R.push(v_target_rooms__397793, v_req_room__398463);
                    }
                }
            }
            v_i__397819 = R.add(v_i__397819, R.number("1"));
        }
        return R.snapshot(F_dvs_plan_cache_put(v_actor, key, v_target_rooms__397793));
    }

    public static object F_dvs_goal_navigation_score(object v_actor, object v_action, object v_pending_achieve)
    {
        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_action, "kind"), "move"))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_action, "travel"))))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_actor, "location_ref"))))))
        {
            return R.number("0");
        }
        if (R.Bool(R.eq(R.count(F_dv_find(v_action, "source_refs", new System.Collections.Generic.List<object>(0) { })), R.number("0"))))
        {
            return R.number("0");
        }
        object v_target_rooms__397793 = GoalNavigationTargets(v_actor, v_pending_achieve);
        object v_i__397819;
        if (R.Bool(R.eq(R.count(v_target_rooms__397793), R.number("0"))))
        {
            return R.number("0");
        }
        object v_position__399274 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "location_ref"));
        if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_position__399274))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_position__399274, "value"), F_dv_get(F_dv_get(v_action, "travel"), "from")))))))
        {
            return R.number("0");
        }
        object v_dest__399427 = F_dv_get(F_dv_get(v_action, "travel"), "to");
        object v_score__399459 = R.number("0");
        v_i__397819 = R.number("0");
        while (R.Bool(R.lt(v_i__397819, R.count(v_target_rooms__397793))))
        {
            object v_before__399529 = F_dvs_navigation_distance(v_actor, F_dv_get(v_position__399274, "value"), R.at(v_target_rooms__397793, v_i__397819));
            object v_after__399614 = F_dvs_navigation_distance(v_actor, v_dest__399427, R.at(v_target_rooms__397793, v_i__397819));
            if (R.Bool((R.Bool((R.Bool(R.lt(R.number("0"), v_before__399529)) ? (object)(R.not(R.lt(v_after__399614, R.number("0")))) : (object)(false))) ? (object)(R.lt(v_after__399614, v_before__399529)) : (object)(false))))
            {
                object v_progress__399749 = R.number("1.25");
                if (R.Bool(R.eq(v_after__399614, R.number("0"))))
                {
                    v_progress__399749 = R.number("1.5");
                }
                v_score__399459 = F_dv_max(v_score__399459, v_progress__399749);
            }
            v_i__397819 = R.add(v_i__397819, R.number("1"));
        }
        return v_score__399459;
    }

    public static object F_dvs_current_action_score(object v_actor, object v_action, object v_bindings, object v_relevant, object v_actual)
    {
        object v_declared__400147 = F_dv_find(v_action, "simulation_utility", R.number("0"));
        object v_information__400207 = R.number("0");
        object v_scope__400231 = F_dvp_scope(v_actor, v_action, v_bindings);
        object v_i__400282 = R.number("0");
        while (R.Bool(R.lt(v_i__400282, R.count(v_scope__400231))))
        {
            var reference = R.at(v_scope__400231, v_i__400282);
            if (R.Bool(F_dv_has(v_relevant, reference)) &&
                (!R.Bool(F_dv_uncontested(F_dv_read(F_dv_get(v_actor, "mind"), reference))) ||
                 R.Bool(F_dvp_evidence_action_progress(v_actor, v_action, reference, v_bindings))))
            {
                v_information__400207 = R.add(v_information__400207, F_dv_find(F_dv_get(v_actor, "policy"), "information_weight", R.number("1")));
            }
            v_i__400282 = R.add(v_i__400282, R.number("1"));
        }
        object v_private__400525 = F_dvp_private_execution_utility(v_actor, v_action, F_dv_get(v_actor, "mind"));
        object v_owner__400600 = F_dv_get(F_dvp_owner_utility(v_actor, v_action, F_dv_get(v_actor, "mind")), "value");
        object v_pending_achieve__400667 = ReadPendingAchieveGoals(v_actor);
        object achieveResult = F_dvp_achievement_pending_work(v_actor, v_action, v_pending_achieve__400667);
        object v_achieve_work__400726 = R.mul(F_dv_get(achieveResult, "value"), R.number("2"));
        object v_comm_work__400820 = R.number("0");
        if (R.Bool(F_dvp_communication_progress(v_actor, v_action)))
        {
            v_comm_work__400820 = R.number("2");
        }
        object v_nav_work__401073 = R.number("0");
        if (R.Bool(R.eq(v_achieve_work__400726, R.number("0"))))
        {
            v_nav_work__401073 = F_dvs_goal_navigation_score(v_actor, v_action, v_pending_achieve__400667);
        }
        object v_personal__401197 = R.number("0");
        if (R.Bool(F_dv_has_key(v_action, "personal_choice")))
        {
            v_personal__401197 = F_dvp_personal_motivation(v_actor, v_action);
        }
        object v_answer__401322 = R.number("0");
        if (R.Bool(F_dv_has_key(v_action, "answer")))
        {
            v_answer__401322 = F_dvp_answer_reward(v_actor, v_action, F_dv_get(v_actor, "mind"));
        }
        object v_response__401440 = R.number("0");
        if (R.Bool((R.Bool(F_dv_has_key(v_action, "response_topic")) ? (object)(R.lt(R.number("0"), R.count(F_dvs_pending_request(v_actor, v_action)))) : (object)(false))))
        {
            v_response__401440 = R.number("2");
        }
        object v_exploration__401733 = R.number("0");
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_action, "kind"), "move")) ? (object)(F_dv_has_key(v_action, "travel")) : (object)(false))) ? (object)(R.eq(v_nav_work__401073, R.number("0"))) : (object)(false))) ? (object)(R.not(F_dv_has(F_dv_find(v_actor, "completed_actions", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_action, "id")))) : (object)(false))))
        {
            if (R.Bool(R.not(F_dv_has(F_dv_find(v_actor, "visited_locations", new System.Collections.Generic.List<object>(0) { }), F_dv_get(F_dv_get(v_action, "travel"), "to")))))
            {
                v_exploration__401733 = F_dv_find(F_dv_get(v_actor, "policy"), "exploration_utility", R.number("1"));
            }
        }
        object v_time__402211 = F_dv_find(v_actor, "decision_time", F_dv_get(F_dv_get(v_actor, "mind"), "time"));
        object v_daytime__402275 = R.sub(v_time__402211, R.mul(R.floor(R.div(v_time__402211, R.number("1440"))), R.number("1440")));
        object v_schedules__402329 = F_dv_find(F_dv_get(v_actor, "policy"), "offscreen_schedules", new System.Collections.Generic.List<object>(0) { });
        object v_schedule_value__402398 = R.number("0");
        v_i__400282 = R.number("0");
        while (R.Bool(R.lt(v_i__400282, R.count(v_schedules__402329))))
        {
            object v_schedule__402474 = R.at(v_schedules__402329, v_i__400282);
            if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_schedule__402474, "action_id"), F_dv_get(v_action, "id"))) ? (object)(R.not(R.lt(v_daytime__402275, F_dv_get(v_schedule__402474, "from")))) : (object)(false))) ? (object)(R.lt(v_daytime__402275, F_dv_get(v_schedule__402474, "to"))) : (object)(false))))
            {
                v_schedule_value__402398 = R.add(v_schedule_value__402398, F_dv_get(v_schedule__402474, "utility"));
            }
            v_i__400282 = R.add(v_i__400282, R.number("1"));
        }
        object v_sims_autonomy__402686 = F_dvs_sims_autonomy_score(v_actor, v_action, R.mul(F_dv_get(v_actual, "time"), 60d));
        var autonomyProfile = F_dv_find(F_dv_get(v_actor, "policy"), "sims_autonomy", false);
        var authoredScale = autonomyProfile is false ? 0d : F_dv_find(autonomyProfile, "score_scale", 0d);
        object v_sims_effective__402749 = R.mul(v_sims_autonomy__402686, F_dv_find(F_dv_get(v_actor, "policy"), "sims_autonomy_scale", authoredScale));
        object v_progress__402838 = R.add(R.add(R.add(R.add(R.add(R.add(R.add(R.add(R.add(R.add(R.add(v_declared__400147, v_private__400525), v_owner__400600), v_achieve_work__400726), v_comm_work__400820), v_nav_work__401073), v_personal__401197), v_answer__401322), v_response__401440), v_exploration__401733), v_schedule_value__402398), v_sims_effective__402749);
        object caseUtility = SpatialCaseStrategyBridge.Utility(v_actor, F_dv_get(v_actor, "mind"), v_action);
        v_progress__402838 = R.add(v_progress__402838, caseUtility);
        object v_value__403001 = R.add(v_information__400207, v_progress__402838);
        object v_penalty__403040 = R.mul(F_dv_get(v_actual, "time"), F_dv_find(F_dv_get(v_actor, "policy"), "time_cost", R.number("0.01")));
        object result = new System.Collections.Generic.List<object>(11) { F_dv_pair("action_value", v_value__403001), F_dv_pair("information_component", v_information__400207), F_dv_pair("progress_component", v_progress__402838), F_dv_pair("time_penalty", v_penalty__403040), F_dv_pair("total", R.sub(v_value__403001, v_penalty__403040)), F_dv_pair("achievement_goals", F_dv_find(achieveResult, "matched_goals", new List<object>())), F_dv_pair("components", new System.Collections.Generic.List<object>(12) { F_dv_pair("declared", v_declared__400147), F_dv_pair("private", v_private__400525), F_dv_pair("owner", v_owner__400600), F_dv_pair("achieve", v_achieve_work__400726), F_dv_pair("communicate", v_comm_work__400820), F_dv_pair("navigate", v_nav_work__401073), F_dv_pair("personal", v_personal__401197), F_dv_pair("answer", v_answer__401322), F_dv_pair("response", v_response__401440), F_dv_pair("exploration", v_exploration__401733), F_dv_pair("schedule", v_schedule_value__402398), F_dv_pair("sims_autonomy", v_sims_autonomy__402686) }), F_dv_pair("scope", "subject-current-immediate"), F_dv_pair("formula", "action_value - time_penalty"), F_dv_pair("strategy", "approximate-v1"), F_dv_pair("approximation", true) };
        return F_dv_with_many(result, new List<object> {
            F_dv_pair("case_strategy_component", caseUtility),
            F_dv_pair("case_strategy_receipts", SpatialCaseStrategyBridge.ForAction(v_actor, F_dv_get(v_actor, "mind"), v_action)
                .Select(choice => (object)new List<object> {
                    F_dv_pair("action", choice.Action), F_dv_pair("ready", choice.Ready),
                    F_dv_pair("utility", choice.Utility), F_dv_pair("theory_hash", choice.TheoryHash),
                    F_dv_pair("basis", choice.Basis.Cast<object>().ToList()),
                    F_dv_pair("evidence_nodes", choice.EvidenceNodes.Cast<object>().ToList())
                }).ToList()) });
    }
}
}

// --- Source: NavigationTasks.cs ---
namespace DuelView.Core
{


public static partial class DVSpatialKernel
{
    public static object F_dvs_navigation_task_completed(object actor, object task)
    {
        foreach (object item in R.Items(F_dv_find(actor, "navigation_task_history", new List<object>())))
            if (R.Bool(R.eq(F_dv_find(item, "reason", ""), "completed")) &&
                R.Bool(F_dv_same(F_dv_find(item, "task_id", false), F_dv_get(task, "task_id"))) &&
                R.Bool(F_dv_same(F_dv_find(item, "phase", false), F_dv_get(task, "phase")))) return true;
        return false;
    }
    // Only authored tasks with a complete contract and an explicit next hop
    // produce candidates. Base actions remain available and unchanged.
    // Request contexts are tied to the actually received request and the
    // recipient's own recorded acceptance, never inferred from proximity.
    public static object F_dvs_navigation_request_status(object actor, object task)
    {
        object context = F_dv_get(task, "request_context");
        if (!R.Bool(R.is_list(context)) || !R.Bool(F_dv_record_like(context))) throw new InvalidOperationException("navigation_task: request_context must be a record");
        object kind = F_dv_find(context, "kind", "");
        if (R.Bool(R.eq(kind, "none"))) return new List<object> { F_dv_pair("status", "valid"), F_dv_pair("source", "") };
        if (!R.Bool(R.eq(kind, "received"))) throw new InvalidOperationException("navigation_task: unsupported request_context kind");
        string[] fields = { "requester", "topic", "source_event", "parse_event", "decision_id", "decision_event" };
        foreach (string field in fields)
            if (F_dv_find(context, field, false) is not string text || string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException($"navigation_task {R.Text(F_dv_get(task, "task_id"))}: request_context requires nonempty {field}");
        object request = false;
        foreach (object item in R.Items(F_dv_find(actor, "received_requests", new List<object>())))
            if (R.Bool(R.eq(F_dv_find(item, "requester", ""), F_dv_get(context, "requester"))) &&
                R.Bool(R.eq(F_dv_find(item, "topic", ""), F_dv_get(context, "topic"))) &&
                R.Bool(R.eq(F_dv_find(item, "source_event", ""), F_dv_get(context, "source_event"))) &&
                R.Bool(R.eq(F_dv_find(item, "parse_event", ""), F_dv_get(context, "parse_event")))) { request = item; break; }
        if (R.Bool(R.eq(request, false))) return new List<object> { F_dv_pair("status", "unknown"), F_dv_pair("source", "") };
        foreach (object change in R.Items(F_dv_find(actor, "received_request_changes", new List<object>())))
            if (R.Bool(F_dv_find(change, "matched_received_request", false)) &&
                R.Bool(R.eq(F_dv_find(change, "request_source_event", ""), F_dv_get(context, "source_event"))) &&
                R.Bool(R.eq(F_dv_find(change, "requester", ""), F_dv_get(context, "requester"))))
                return new List<object> { F_dv_pair("status", "withdrawn"), F_dv_pair("source", F_dv_find(change, "parse_event", "")) };
        object decision = F_dv_find(F_dv_find(actor, "owned_decisions", new List<object>()), F_dv_get(context, "decision_id"), false);
        if (R.Bool(R.eq(decision, false))) return new List<object> { F_dv_pair("status", "unknown"), F_dv_pair("source", "") };
        if (!R.Bool(R.eq(F_dv_find(decision, "event", ""), F_dv_get(context, "decision_event"))) || !R.Bool(R.eq(F_dv_find(decision, "stance", ""), "accepted")))
            return new List<object> { F_dv_pair("status", "acceptance-changed"), F_dv_pair("source", F_dv_find(decision, "event", "")) };
        object decisionContext = F_dv_find(decision, "context", new List<object>());
        foreach (object entry in R.Items(F_dv_find(decisionContext, "requests", new List<object>())))
        {
            object receipt = F_dv_find(entry, "receipt", false);
            if (R.Bool(R.eq(receipt, false))) continue;
            if (R.Bool(R.eq(F_dv_find(entry, "topic", ""), F_dv_get(context, "topic"))) &&
                R.Bool(R.eq(F_dv_find(receipt, "requester", ""), F_dv_get(context, "requester"))) &&
                R.Bool(R.eq(F_dv_find(receipt, "source_event", ""), F_dv_get(context, "source_event"))) &&
                R.Bool(R.eq(F_dv_find(receipt, "parse_event", ""), F_dv_get(context, "parse_event"))))
                return new List<object> { F_dv_pair("status", "valid"), F_dv_pair("source", F_dv_get(context, "decision_event")) };
        }
        return new List<object> { F_dv_pair("status", "unknown"), F_dv_pair("source", "") };
    }

    public static object F_dvs_navigation_task_candidates(object actor)
    {
        object result = new List<object>();
        object actions = F_dv_get(actor, "actions");
        object goals = F_dv_find(F_dv_get(actor, "policy"), "goals", new List<object>());
        object mind = F_dv_get(actor, "mind");
        var heldCommitment = F_dv_find(actor, "navigation_commitment", false);
        var holdMovement = false;
        if (heldCommitment is not false)
        {
            var heldTask = F_dv_get(heldCommitment, "task");
            holdMovement = R.Text(F_dv_truth(mind, F_dv_get(heldTask, "valid_when"))) == "true"
                && R.Text(F_dv_truth(mind, F_dv_get(heldTask, "complete_when"))) == "false"
                && R.Text(F_dv_get(F_dvs_navigation_request_status(actor, heldTask), "status")) == "valid";
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (object action in R.Items(actions)) ids.Add(R.Text(F_dv_get(action, "id")));
        var tasks = new List<object>();
        var taskKeys = new HashSet<string>(StringComparer.Ordinal);
        static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        foreach (object goal in R.Items(goals))
        {
            object task = F_dv_find(goal, "navigation_task", false);
            if (R.Bool(R.eq(task, false))) continue;
            if (!R.Bool(R.is_list(task)) || !R.Bool(F_dv_record_like(task))) throw new InvalidOperationException("navigation_task must be a record");
            object labelValue = F_dv_find(task, "task_id", "");
            string taskLabel = labelValue is string label ? label : "<invalid>";
            string[] required = { "task_id", "phase", "request_context", "priority", "valid_when", "complete_when", "preempt_when", "origin", "next", "target", "base_action_id" };
            foreach (string field in required) if (!R.Bool(F_dv_has_key(task, field))) throw new InvalidOperationException($"navigation_task {taskLabel}: missing {field}");
            if (F_dv_get(task, "task_id") is not string id || string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("navigation_task: task_id must be nonempty text");
            if (F_dv_get(task, "phase") is not string phase || string.IsNullOrWhiteSpace(phase)) throw new InvalidOperationException($"navigation_task {id}: phase must be nonempty text");
            object priority = F_dv_get(task, "priority");
            if (!R.Bool(R.is_numeric_value(priority))) throw new InvalidOperationException($"navigation_task {id}: priority must be a finite number");
            if (R.Bool(F_dvs_navigation_task_completed(actor, task))) continue;
            string key = Encode(id) + ":" + Encode(phase);
            if (!taskKeys.Add(key)) throw new InvalidOperationException($"navigation_task {id}: duplicate task_id and phase");
            object context = F_dv_get(task, "request_context");
            if (!R.Bool(R.is_list(context)) || !R.Bool(F_dv_record_like(context)) || !R.Bool(F_dv_has_key(context, "kind"))) throw new InvalidOperationException($"navigation_task {id}: invalid request_context");
            object requestStatus = F_dvs_navigation_request_status(actor, task);
            if (!R.Bool(R.eq(F_dv_get(requestStatus, "status"), "valid"))) continue;
            object basis = F_dv_find(goal, "basis", new List<object>());
            if (!R.Bool(R.eq(F_dv_find(goal, "kind", ""), "achieve")) || !R.Bool(R.lt(R.number("0"), F_dv_find(goal, "weight", R.number("1")))) ||
                !R.Bool(R.is_list(basis)) || !R.Bool(R.lt(R.number("0"), R.count(basis))) ||
                !R.Bool(R.eq(F_dv_truth(mind, F_dv_find(goal, "available_when", F_dv_true())), "true")) ||
                !R.Bool(R.eq(F_dv_truth(mind, F_dv_get(task, "valid_when")), "true")) ||
                !R.Bool(R.eq(F_dv_truth(mind, F_dv_get(task, "complete_when")), "false"))) continue;
            object commitment = F_dv_find(actor, "navigation_commitment", false);
            if (!R.Bool(R.eq(commitment, false)))
            {
                object held = F_dv_get(commitment, "task");
                bool same = R.Bool(F_dv_same(F_dv_get(held, "task_id"), id)) && R.Bool(F_dv_same(F_dv_get(held, "phase"), phase));
                object heldValid = F_dv_truth(mind, F_dv_get(held, "valid_when"));
                object heldComplete = F_dv_truth(mind, F_dv_get(held, "complete_when"));
                if (same && (!R.Bool(F_dv_same(F_dv_get(held, "target"), F_dv_get(task, "target"))) ||
                    !R.Bool(F_dv_same(F_dv_get(held, "request_context"), F_dv_get(task, "request_context"))) ||
                    !R.Bool(R.eq(heldValid, "true")) || !R.Bool(R.eq(heldComplete, "false")))) continue;
                if (!same && (!R.Bool(R.lt(F_dv_get(held, "priority"), priority)) ||
                    !R.Bool(R.eq(F_dv_truth(mind, F_dv_get(held, "preempt_when")), "true")))) continue;
            }
            tasks.Add(task);
        }
        foreach (object action in R.Items(actions))
        {
            if (R.Bool(R.eq(F_dv_find(action, "kind", ""), "move")) && R.Bool(F_dv_has_key(action, "travel")))
            {
                object travel = F_dv_get(action, "travel");
                foreach (object task in tasks)
                {
                    if (!R.Bool(R.eq(F_dv_get(task, "origin"), F_dv_find(travel, "from", false))) || !R.Bool(R.eq(F_dv_get(task, "next"), F_dv_find(travel, "to", false))) ||
                        !R.Bool(R.eq(F_dv_get(task, "base_action_id"), F_dv_get(action, "id")))) continue;
                    string baseId = R.Text(F_dv_get(action, "id"));
                    string candidateId = "nav-task:" + Encode(baseId) + ":" + Encode(R.Text(F_dv_get(task, "task_id"))) + ":" + Encode(R.Text(F_dv_get(task, "phase")));
                    if (!ids.Add(candidateId)) throw new InvalidOperationException($"navigation_task {R.Text(F_dv_get(task, "task_id"))}: duplicate or conflicting candidate {candidateId}");
                    object variant = F_dv_with_many(action, new List<object> { F_dv_pair("id", candidateId), F_dv_pair("base_action_id", baseId), F_dv_pair("navigation_task", task) });
                    _ = R.push(result, variant);
                }
            }
            // An unbound move cannot silently abandon a valid commitment. At an
            // unknown gate the actor may inspect or wait, not alternate goals on
            // each step. Explicit simulation-priority actions remain interruptible.
            if (!holdMovement || R.Text(F_dv_find(action, "kind", "")) != "move"
                || R.Bool(R.eq(F_dv_get(action, "id"), F_dv_find(actor, "simulation_priority_action", ""))))
                _ = R.push(result, action);
        }
        return result;
    }

    public static object F_dvs_navigation_task_diagnostics(object actor, object choice)
    {
        object rows = new List<object>();
        object mind = F_dv_get(actor, "mind");
        object goals = F_dv_find(F_dv_get(actor, "policy"), "goals", new List<object>());
        object plans = F_dv_find(choice, "plans", new List<object>());
        object selected = F_dv_find(choice, "action", false);
        if (!R.Bool(R.eq(selected, false)) && R.Bool(R.eq(F_dv_find(selected, "kind", ""), "move")))
        {
            foreach (object plan in R.Items(plans))
            {
                object path = F_dv_find(plan, "path", new List<object>());
                if (!R.Bool(R.lt(R.number("0"), R.count(path))) || !R.Bool(R.eq(R.at(path, R.number("0")), F_dv_get(selected, "id")))) continue;
                object currentScore = F_dv_find(F_dv_find(plan, "value", new List<object>()), "current_score", new List<object>());
                object attribution = F_dv_find(currentScore, "achievement_goals", new List<object>());
                if (R.Bool(R.lt(R.number("0"), R.count(attribution))))
                    _ = R.push(rows, new List<object> { F_dv_pair("declared", false), F_dv_pair("kind", "selected-move-achievement"), F_dv_pair("action_id", F_dv_get(selected, "id")), F_dv_pair("goals", attribution) });
                break;
            }
        }
        foreach (object action in R.Items(F_dv_get(actor, "actions")))
        {
            if (!R.Bool(F_dv_has_key(action, "owned_request_requirement"))) continue;
            object id = F_dv_get(action, "id");
            object score = false;
            foreach (object plan in R.Items(plans))
            {
                object path = F_dv_find(plan, "path", new List<object>());
                if (R.Bool(R.lt(R.number("0"), R.count(path))) && R.Bool(R.eq(R.at(path, R.number("0")), id))) { score = F_dv_find(plan, "score", false); break; }
            }
            _ = R.push(rows, new List<object> { F_dv_pair("declared", false), F_dv_pair("kind", "owned-request-action"), F_dv_pair("action_id", id),
                F_dv_pair("request_ready", F_dvp_owned_request_ready(actor, action)), F_dv_pair("subjective_precondition", F_dv_truth(mind, F_dv_get(action, "precondition"))),
                F_dv_pair("pending_request_available", R.lt(R.number("0"), R.count(F_dvs_pending_request(actor, action)))), F_dv_pair("score", score),
                F_dv_pair("selected", !R.Bool(R.eq(selected, false)) && R.Bool(R.eq(F_dv_find(selected, "id", ""), id))) });
        }
        foreach (object action in R.Items(F_dv_get(actor, "actions")))
        {
            if (!R.Bool(R.eq(F_dv_find(action, "kind", ""), "inspect"))) continue;
            bool matchesFrontier = false;
            foreach (object goal in R.Items(goals))
            {
                if (!R.Bool(F_dv_find(goal, "navigation_gate_probe", false))) continue;
                foreach (object grant in R.Items(F_dv_find(action, "grants", new List<object>())))
                {
                    if (!R.Bool(R.is_list(grant)) || !R.Bool(F_dv_record_like(grant)) || !R.Bool(F_dv_has_key(grant, "type")) ||
                        !R.Bool(F_dv_has_key(grant, "object")) || !R.Bool(F_dv_has_key(grant, "facet")) || !R.Bool(F_dv_has_key(grant, "field"))) continue;
                    if (R.Bool(F_dv_same_local_ref(F_dv_get(actor, "id"), grant, F_dv_get(goal, "ref")))) { matchesFrontier = true; break; }
                }
                if (matchesFrontier) break;
            }
            if (!matchesFrontier) continue;
            object id = F_dv_get(action, "id");
            object score = false;
            foreach (object plan in R.Items(plans))
            {
                object path = F_dv_find(plan, "path", new List<object>());
                if (R.Bool(R.lt(R.number("0"), R.count(path))) && R.Bool(R.eq(R.at(path, R.number("0")), id))) { score = F_dv_find(plan, "score", false); break; }
            }
            _ = R.push(rows, new List<object> { F_dv_pair("declared", false), F_dv_pair("kind", "gate-probe-action"), F_dv_pair("action_id", id), F_dv_pair("score", score),
                F_dv_pair("reason", R.Bool(R.eq(score, false)) ? "not-scored-or-ineligible" : "scored") });
        }
        foreach (object goal in R.Items(goals))
            if (R.Bool(F_dv_find(goal, "navigation_gate_probe", false)))
                _ = R.push(rows, new List<object> { F_dv_pair("declared", false), F_dv_pair("kind", "gate-probe-goal"), F_dv_pair("ref", F_dv_get(goal, "ref")), F_dv_pair("available", F_dv_truth(mind, F_dv_find(goal, "available_when", F_dv_true()))) });
        object commitment = F_dv_find(actor, "navigation_commitment", false);
        if (!R.Bool(R.eq(commitment, false)))
        {
            object heldTask = F_dv_get(commitment, "task");
            object heldValid = F_dv_truth(mind, F_dv_get(heldTask, "valid_when"));
            object heldComplete = F_dv_truth(mind, F_dv_get(heldTask, "complete_when"));
            object heldRequestStatus = F_dv_get(F_dvs_navigation_request_status(actor, heldTask), "status");
            string heldReason = R.Bool(R.eq(heldRequestStatus, "unknown")) ? "request-unknown-paused" : !R.Bool(R.eq(heldValid, "true")) && !R.Bool(R.eq(heldValid, "false")) ? "validity-unknown-paused" :
                !R.Bool(R.eq(heldComplete, "true")) && !R.Bool(R.eq(heldComplete, "false")) ? "completion-unknown-paused" :
                R.Bool(R.eq(heldComplete, "true")) ? "completion-awaiting-objective-verification" : "held";
            _ = R.push(rows, new List<object> { F_dv_pair("declared", false), F_dv_pair("held", true), F_dv_pair("task_id", F_dv_get(heldTask, "task_id")),
                F_dv_pair("phase", F_dv_get(heldTask, "phase")), F_dv_pair("task_valid", heldValid), F_dv_pair("task_complete", heldComplete), F_dv_pair("reason", heldReason) });
        }
        foreach (object goal in R.Items(goals))
        {
            object task = F_dv_find(goal, "navigation_task", false);
            if (R.Bool(R.eq(task, false)))
            {
                if (R.Bool(F_dv_has_key(goal, "navigation_intention")))
                    _ = R.push(rows, new List<object> { F_dv_pair("declared", false), F_dv_pair("reason", "no-task-declaration") });
                continue;
            }
            if (!R.Bool(R.is_list(task)) || !R.Bool(F_dv_record_like(task)) || !R.Bool(F_dv_has_key(task, "task_id")) ||
                !R.Bool(F_dv_has_key(task, "phase")) || !R.Bool(F_dv_has_key(task, "valid_when")) ||
                !R.Bool(F_dv_has_key(task, "complete_when")) || !R.Bool(F_dv_has_key(task, "priority")) || !R.Bool(F_dv_has_key(task, "preempt_when")) ||
                !R.Bool(F_dv_has_key(task, "origin")) || !R.Bool(F_dv_has_key(task, "next")) ||
                !R.Bool(F_dv_has_key(task, "target")) || !R.Bool(F_dv_has_key(task, "request_context")) || !R.Bool(F_dv_has_key(task, "base_action_id")))
            { _ = R.push(rows, new List<object> { F_dv_pair("declared", true), F_dv_pair("reason", "invalid-task-declaration") }); continue; }
            object goalTruth = F_dv_truth(mind, F_dv_find(goal, "available_when", F_dv_true()));
            object valid = F_dv_truth(mind, F_dv_get(task, "valid_when"));
            object completed = F_dv_truth(mind, F_dv_get(task, "complete_when"));
            int matching = 0;
            foreach (object action in R.Items(F_dv_get(actor, "actions")))
            {
                object travel = F_dv_find(action, "travel", false);
                if (!R.Bool(R.eq(travel, false)) && R.Bool(R.eq(F_dv_find(action, "kind", ""), "move")) &&
                    R.Bool(R.eq(F_dv_find(travel, "from", false), F_dv_get(task, "origin"))) && R.Bool(R.eq(F_dv_find(travel, "to", false), F_dv_get(task, "next"))) &&
                    R.Bool(R.eq(F_dv_get(action, "id"), F_dv_get(task, "base_action_id")))) matching++;
            }
            object basis = F_dv_find(goal, "basis", new List<object>());
            object context = F_dv_find(task, "request_context", false);
            object held = F_dv_find(actor, "navigation_commitment", false);
            bool supportedContext = R.Bool(R.is_list(context)) && R.Bool(F_dv_record_like(context)) && R.Bool(R.eq(F_dv_get(F_dvs_navigation_request_status(actor, task), "status"), "valid"));
            bool allowedByHeld = true;
            if (!R.Bool(R.eq(held, false)))
            {
                object heldTask = F_dv_get(held, "task");
                bool same = R.Bool(F_dv_same(F_dv_get(heldTask, "task_id"), F_dv_get(task, "task_id"))) && R.Bool(F_dv_same(F_dv_get(heldTask, "phase"), F_dv_get(task, "phase")));
                allowedByHeld = same ? R.Bool(F_dv_same(F_dv_get(heldTask, "target"), F_dv_get(task, "target"))) &&
                    R.Bool(F_dv_same(F_dv_get(heldTask, "request_context"), F_dv_get(task, "request_context"))) &&
                    R.Bool(R.eq(F_dv_truth(mind, F_dv_get(heldTask, "valid_when")), "true")) && R.Bool(R.eq(F_dv_truth(mind, F_dv_get(heldTask, "complete_when")), "false")) :
                    R.Bool(R.lt(F_dv_get(heldTask, "priority"), F_dv_get(task, "priority"))) && R.Bool(R.eq(F_dv_truth(mind, F_dv_get(heldTask, "preempt_when")), "true"));
            }
            string reason = !R.Bool(R.eq(F_dv_find(goal, "kind", ""), "achieve")) ? "goal-kind" :
                !R.Bool(R.lt(R.number("0"), F_dv_find(goal, "weight", R.number("1")))) ? "goal-weight" :
                !R.Bool(R.is_list(basis)) || !R.Bool(R.lt(R.number("0"), R.count(basis))) ? "goal-basis" :
                !supportedContext ? "unsupported-request-context" : !R.Bool(R.eq(goalTruth, "true")) ? "goal-not-known-available" :
                !R.Bool(R.eq(valid, "true")) ? "task-not-known-valid" :
                !R.Bool(R.eq(completed, "false")) ? "completion-not-known-false" : !allowedByHeld ? "held-task-or-preemption" :
                matching == 0 ? "no-matching-move" : "eligible-for-binding";
            _ = R.push(rows, new List<object> { F_dv_pair("declared", true), F_dv_pair("task_id", F_dv_get(task, "task_id")), F_dv_pair("phase", F_dv_get(task, "phase")),
                F_dv_pair("goal_available", goalTruth), F_dv_pair("task_valid", valid), F_dv_pair("task_complete", completed), F_dv_pair("matching_moves", (double)matching), F_dv_pair("reason", reason) });
        }
        return rows;
    }

    public static object F_dvs_navigation_task_dispatch(object actor, object action, object actionEvent)
    {
        object held = F_dv_find(actor, "navigation_commitment", false);
        if (R.Bool(R.eq(held, false)) || !R.Bool(R.eq(F_dv_find(held, "candidate_id", false), F_dv_find(action, "navigation_candidate_id", false)))) return actor;
        return F_dv_with(actor, "navigation_commitment", F_dv_with(held, "pending_action_event", actionEvent));
    }

    public static object F_dvs_navigation_task_sync(object actor, object events, object time, object physical)
    {
        object held = F_dv_find(actor, "navigation_commitment", false);
        if (R.Bool(R.eq(held, false))) return actor;
        object task = F_dv_get(held, "task");
        object mind = F_dv_get(actor, "mind");
        string reason = "";
        object requestStatus = F_dvs_navigation_request_status(actor, task);
        object requestState = F_dv_get(requestStatus, "status");
        object source = F_dv_find(held, "last_execution_event", F_dv_find(held, "selection_event", ""));
        if (R.Bool(R.eq(requestState, "withdrawn")) || R.Bool(R.eq(requestState, "acceptance-changed")))
        { reason = R.Bool(R.eq(requestState, "withdrawn")) ? "request-withdrawn" : "acceptance-changed"; source = F_dv_get(requestStatus, "source"); }
        object actionEvent = F_dv_find(held, "pending_action_event", F_dv_find(held, "last_action_event", ""));
        object cursor = F_dv_find(held, "event_cursor", R.count(events));
        if (R.Bool(R.lt(R.count(events), cursor))) cursor = R.count(events);
        for (object i = cursor; reason.Length == 0 && R.Bool(R.lt(i, R.count(events))); i = R.add(i, R.number("1")))
        {
            object evt = R.at(events, i);
            if (!R.Bool(R.eq(F_dv_get(evt, "actor"), F_dv_get(actor, "id"))) || !R.Bool(R.eq(F_dv_get(evt, "type"), "action_execution"))) continue;
            object data = F_dv_get(evt, "data");
            object action = F_dv_find(data, "action", false);
            if (R.Bool(R.eq(action, false)) || !R.Bool(R.eq(F_dv_find(action, "navigation_candidate_id", false), F_dv_find(held, "candidate_id", false))) ||
                R.Bool(R.eq(F_dv_find(held, "pending_action_event", false), false)) ||
                !R.Bool(R.eq(F_dv_find(action, "navigation_action_event", false), F_dv_get(held, "pending_action_event")))) continue;
            object receipt = F_dv_find(data, "receipt", false);
            object execution = R.Bool(R.eq(receipt, false)) ? false : F_dv_find(receipt, "execution", false);
            if (!R.Bool(R.eq(execution, false)))
            {
                source = F_dv_get(evt, "id");
                actionEvent = F_dv_find(action, "navigation_action_event", "");
                if (!R.Bool(R.eq(F_dv_find(execution, "status", ""), "applied"))) reason = "move-not-applied";
            }
        }
        if (reason.Length == 0 && R.Bool(R.eq(F_dv_truth(mind, F_dv_get(task, "complete_when")), "true")) &&
            R.Bool(R.eq(F_dv_truth(physical, F_dv_get(task, "complete_when")), "true"))) reason = "completed";
        else if (reason.Length == 0 && R.Bool(R.eq(F_dv_truth(mind, F_dv_get(task, "valid_when")), "false"))) reason = "condition-invalidated";
        if (reason.Length != 0)
        {
            object release = F_dvs_event(events, "navigation_task_released", F_dv_get(actor, "id"), time, F_dv_single(source),
                new List<object> { F_dv_pair("task_id", F_dv_get(task, "task_id")), F_dv_pair("phase", F_dv_get(task, "phase")), F_dv_pair("reason", reason), F_dv_pair("action_event", actionEvent) });
            object history = F_dv_append(F_dv_find(actor, "navigation_task_history", new List<object>()),
                new List<object> { F_dv_pair("task_id", F_dv_get(task, "task_id")), F_dv_pair("phase", F_dv_get(task, "phase")), F_dv_pair("reason", reason), F_dv_pair("event", release) });
            return F_dv_with_many(actor, new List<object> { F_dv_pair("navigation_commitment", false), F_dv_pair("navigation_task_history", history) });
        }
        return F_dv_with(actor, "navigation_commitment", F_dv_with_many(held, new List<object> { F_dv_pair("event_cursor", R.count(events)), F_dv_pair("last_execution_event", source), F_dv_pair("last_action_event", actionEvent) }));
    }

    public static object F_dvs_navigation_task_commit(object actor, object action, object events, object time, object parent)
    {
        object task = F_dv_find(action, "navigation_task", false);
        if (R.Bool(R.eq(task, false))) return actor;
        object old = F_dv_find(actor, "navigation_commitment", false);
        if (!R.Bool(R.eq(old, false)))
        {
            object oldTask = F_dv_get(old, "task");
            bool same = R.Bool(F_dv_same(F_dv_get(oldTask, "task_id"), F_dv_get(task, "task_id"))) && R.Bool(F_dv_same(F_dv_get(oldTask, "phase"), F_dv_get(task, "phase")));
            if (!same)
            {
                if (!R.Bool(R.lt(F_dv_get(oldTask, "priority"), F_dv_get(task, "priority"))) ||
                    !R.Bool(R.eq(F_dv_truth(F_dv_get(actor, "mind"), F_dv_get(oldTask, "preempt_when")), "true"))) return actor;
                _ = F_dvs_event(events, "navigation_task_released", F_dv_get(actor, "id"), time, F_dv_single(parent),
                    new List<object> { F_dv_pair("task_id", F_dv_get(oldTask, "task_id")), F_dv_pair("phase", F_dv_get(oldTask, "phase")), F_dv_pair("reason", "preempted") });
            }
            else task = F_dv_with_many(oldTask, new List<object> { F_dv_pair("origin", F_dv_get(task, "origin")), F_dv_pair("next", F_dv_get(task, "next")), F_dv_pair("base_action_id", F_dv_get(task, "base_action_id")) });
        }
        object evtId = F_dvs_event(events, "navigation_task_committed", F_dv_get(actor, "id"), time, F_dv_single(parent),
            new List<object> { F_dv_pair("task_id", F_dv_get(task, "task_id")), F_dv_pair("phase", F_dv_get(task, "phase")), F_dv_pair("candidate_id", F_dv_get(action, "navigation_candidate_id")), F_dv_pair("base_action_id", F_dv_get(action, "id")) });
        return F_dv_with(actor, "navigation_commitment", new List<object> { F_dv_pair("task", task), F_dv_pair("selection_event", evtId), F_dv_pair("candidate_id", F_dv_get(action, "navigation_candidate_id")), F_dv_pair("event_cursor", R.count(events)) });
    }

}
}

namespace DuelView.Core
{
    public static partial class DVSpatialKernel
    {
        private static object ReadCachedValue(string group, object owner, object key) =>
            R.Bool(R.memo_group_enabled(group)) ? R.memo_read(owner, key) : R.snapshot(new List<object> { false, false });

        public static Func<object, object>? ReadGoalRefsHandler { get; set; }
        private static object ReadGoalRefs(object actor) => ReadGoalRefsHandler != null ? ReadGoalRefsHandler(actor) : new List<object>();
    }

    public static class SpatialAutonomy
    {
        public static Func<IReadOnlyDictionary<string, double>, IReadOnlyDictionary<string, double>, IReadOnlyDictionary<string, double>, double, double, double>? ScoreHook { get; set; }
        public static double Score(IReadOnlyDictionary<string, double> needs,
            IReadOnlyDictionary<string, double> weights, IReadOnlyDictionary<string, double> perceivedEffects,
            double seconds, double timeCost)
        {
            if (ScoreHook != null) return ScoreHook(needs, weights, perceivedEffects, seconds, timeCost);
            if (!double.IsFinite(seconds) || seconds <= 0 || !double.IsFinite(timeCost) || timeCost < 0)
                throw new FormatException("Invalid drive duration or time cost");
            double score = -seconds * timeCost;
            foreach (var (need, delta) in perceivedEffects)
            {
                if (!needs.TryGetValue(need, out var level) || !weights.TryGetValue(need, out var weight) ||
                    !double.IsFinite(level) || level < 0 || level > 100 || !double.IsFinite(weight) || weight < 0 || !double.IsFinite(delta))
                    throw new FormatException("Undeclared or invalid need: " + need);
                static double Satisfaction(double value) => value - value * value / 200;
                score += weight * (Satisfaction(Math.Clamp(level + delta, 0, 100)) - Satisfaction(level));
            }
            return double.IsFinite(score) ? score : throw new FormatException("Non-finite drive score");
        }
    }

    public sealed record StrategyReceiptStub(string Action, bool Ready, double Utility, string TheoryHash,
        IReadOnlyList<string> Basis, IReadOnlyList<string> EvidenceNodes);

    internal static class SpatialCaseStrategyBridge
    {
        public static Func<object, object, object, object>? UtilityHook { get; set; }
        public static Func<object, object, object, IEnumerable<StrategyReceiptStub>>? ForActionHook { get; set; }

        public static object Utility(object actor, object mind, object action) =>
            UtilityHook != null ? UtilityHook(actor, mind, action) : 0d;

        public static IEnumerable<StrategyReceiptStub> ForAction(object actor, object mind, object action) =>
            ForActionHook != null ? ForActionHook(actor, mind, action) : Enumerable.Empty<StrategyReceiptStub>();
    }
}
