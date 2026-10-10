using System.Text.Json;
using DualOntologyWorldEngine.Core;
using static DualOntologyWorldEngine.Story.ChapterEvents;

namespace DualOntologyWorldEngine.Awakening;

public sealed partial class Chapter1Session
{
    private JsonElement StrategyTrack => Content.GetProperty("STRATEGY_TRACK");
    private bool AtEvidenceObject(string id) => Layout.GetProperty("sites").EnumerateArray().Any(site => site.GetProperty("evidence").GetString() == id
        && F("detective").Near.Contains(site.GetProperty("id").GetString()!));

    private IEnumerable<Op> AlterEvidenceObject(string id, bool destroy)
    {
        CloseModal();
        if (!AtEvidenceObject(id) || !StrategyTrack.GetProperty("object_choices").TryGetProperty(id, out var allowed)
            || !allowed.EnumerateArray().Any(a => a.GetString() == (destroy ? "destroy" : "hide"))) yield break;
        var succeeded = destroy ? _content.DestroyEvidence(_world, id) : _world.TakeEvidence("detective", id);
        if (!succeeded) yield break;
        if (!destroy) Session.Set("hidden", id, true);
        _out.Add(StageOp("prop", ("id", id), ("state", "taken")));
        _out.Add(Event("object_changed", ("action", destroy ? "destroy" : "hide"), ("minutes", _content.GameMinutes)));
    }
    private int CaseProgress => new[] { "examined", "testimony", "experiments", "recalls" }.Sum(g => Session.GetProgress(g).Count)
        + Convert.ToInt32(Session.GetProgress("answer_state").GetValueOrDefault("step", 0));

    private void ObserveStrategySituation()
    {
        if (!StageFlag("daily") || !Content.TryGetProperty("STRATEGY_TRACK", out var track)) return;
        var progress = CaseProgress;
        var prior = Convert.ToInt32(Session.GetProgress("strategy_progress").GetValueOrDefault("value", 0));
        if (progress != prior)
        {
            Session.Set("strategy_progress", "value", progress);
            _out.Add(Event("case_progress", ("minutes", _content.GameMinutes), ("value", progress)));
        }
        foreach (var entry in track.GetProperty("people").EnumerateObject())
        {
            var who = entry.Name;
            var local = Session.IsLocalTarget(who, "detective") && F(who).Visible;
            var observed = Session.GetProgress("strategy_observed");
            var minute = _content.GameMinutes;
            var last = Convert.ToInt32(observed.GetValueOrDefault(who + ":last", minute));
            var stalled = Convert.ToInt32(observed.GetValueOrDefault(who + ":stall", 0));
            var absence = Convert.ToInt32(observed.GetValueOrDefault(who + ":absence", 0));
            if (!local) absence += Math.Max(0, minute - last);
            // Gaps in attendance reset the continuous visual basis. Global progress is sampled only when witnessed.
            if (!local || observed.GetValueOrDefault(who + ":local") is not true) stalled = 0;
            else stalled += Math.Max(0, minute - last);
            observed[who + ":last"] = minute; observed[who + ":stall"] = stalled; observed[who + ":local"] = local;
            observed[who + ":absence"] = local ? 0 : absence;
            if (!local) continue;
            var pressure = Math.Max(stalled, absence);
            var reactions = _world.StrategyBasis(who);
            if (reactions.GetValueOrDefault("observed_refusal") >= 1 || reactions.GetValueOrDefault("observed_wrong") >= 2) pressure = Math.Max(pressure, 20);
            var values = new Dictionary<string, int> { ["observed_stall"] = pressure, ["observed_absence"] = absence, ["present"] = 1 };
            _world.ObserveStrategyState(who, values, "visible_activity:" + stalled);
            if (pressure >= 20 && reactions.GetValueOrDefault("observed_stall") < 20)
                _out.Add(Event("strategy_deviation", ("actor", who), ("minutes", minute), ("cognition", _world.StrategyBasis(who))));
            _world.NoticeMissingKnownEvidence(who, entry.Value.GetProperty("clues").EnumerateArray().Select(c => c.GetString()!));
        }
    }

    private bool TryStrategyAction()
    {
        if (!Content.TryGetProperty("STRATEGY_TRACK", out var track)) return false;
        var cooldown = track.GetProperty("cooldown_minutes").GetInt32();
        var after = Convert.ToInt32(Session.GetProgress("strategy_progress").GetValueOrDefault("after", 0));
        if (_content.GameMinutes < after) return false;
        foreach (var entry in track.GetProperty("people").EnumerateObject())
        {
            var who = entry.Name;
            if (!Session.IsLocalTarget(who, "detective") || !F(who).Visible || LifeBusy(who) || _staged.ContainsKey(who)) continue;
            var clues = entry.Value.GetProperty("clues").EnumerateArray().Select(c => c.GetString()!).ToArray();
            var clue = clues.FirstOrDefault(id => _world.KnownEvidenceText(who, id) != "" && !Has("strategy_reported", who + ":" + id));
            var methods = entry.Value.GetProperty("methods").EnumerateObject()
                .Where(m => m.Name is not ("report" or "remember") || clue is not null)
                .Where(m => _content.GameMinutes >= Convert.ToInt32(Session.GetProgress("strategy_cooldown").GetValueOrDefault(who + ":" + m.Name, 0)))
                .Select(m => m.Name).ToArray();
            var choice = _world.ChooseStrategyMethod(who, methods);
            if (choice is null) continue;
            var method = MorningWorld.StrategyMethodKey(choice);
            var scene = entry.Value.GetProperty("methods").GetProperty(method).GetString()!;
            Session.Set("strategy_progress", "after", _content.GameMinutes + cooldown);
            Session.Set("strategy_cooldown", who + ":" + method, _content.GameMinutes + cooldown * 2);
            _out.Add(Event("strategy_action", ("actor", who), ("method", method), ("source", choice.Decision.Basis.Name),
                ("reasons", choice.Reasons.ToArray()), ("minutes", _content.GameMinutes),
                ("cognition", _world.StrategyBasis(who)), ("known_evidence", clue)));
            Start(StrategyAction(who, method, scene, clue, choice));
            return true;
        }
        return false;
    }

    private IEnumerable<Op> StrategyAction(string who, string method, string scene, string? clue, DecisionAssessment choice)
    {
        var experience = _world.BeginStrategyMethod(who, choice);
        var outcome = DecisionOutcomeV1.Interrupted;
        try
        {
        // Only the initiating actor approaches. The detective retains the choice of where to go and what to answer.
        yield return new Run(Summon(who));
        if (!Session.IsLocalTarget(who, "detective") || !F(who).Visible) { outcome = DecisionOutcomeV1.NoProgress; yield break; }
        yield return new Run(Say(scene));
        if (method is "report" or "remember" && clue is not null && _world.ReportKnownEvidence(who, clue, out var text))
        {
            Session.Set("strategy_reported", who + ":" + clue, true);
            Session.Set("testimony", clue, new Dictionary<string, object> { ["speaker"] = who, ["text"] = text });
            ObserveVisibleInvestigation("testimony:" + clue);
            Aside(text);
        }
        else if (method == "invite" && clue is not null)
            yield return new Run(WalkTo(who, new Dictionary<string, object?> { ["approach_site"] = clue, ["inset"] = 14.0 }, 130.0));
        outcome = DecisionOutcomeV1.Succeeded;
        }
        finally { _world.CompleteStrategyMethod(who, experience, outcome); }
    }

    private void ObserveAnswerAdvance(int priorStep)
    {
        var step = Convert.ToInt32(Session.GetProgress("answer_state").GetValueOrDefault("step", 0));
        if (priorStep >= 1 && step > priorStep) ObserveVisibleInvestigation("answer:" + step);
    }

    private void ObserveVisibleInvestigation(string material)
    {
        if (!Content.TryGetProperty("STRATEGY_TRACK", out var track)) return;
        foreach (var who in track.GetProperty("people").EnumerateObject().Select(p => p.Name))
        {
            if (!Session.IsLocalTarget(who, "detective") || !F(who).Visible || Has("strategy_witnessed", who + ":" + material)) continue;
            Session.Set("strategy_witnessed", who + ":" + material, true);
            Session.Set("strategy_observed", who + ":stall", 0);
            _world.ObserveStrategyState(who, new Dictionary<string, int> {
                ["observed_stall"] = 0, ["observed_refusal"] = 0, ["observed_wrong"] = 0
            }, "witnessed_investigation:" + material);
        }
    }

    private void ObserveStrategyRefusal(string choice)
    {
        if (!Content.TryGetProperty("STRATEGY_TRACK", out var track) || !track.TryGetProperty("decline_choices", out var declines)
            || !declines.TryGetProperty(choice, out var recipient)) return;
        var who = recipient.GetString()!;
        if (!Session.IsLocalTarget(who, "detective") || !F(who).Visible) return;
        var prior = _world.StrategyBasis(who).GetValueOrDefault("observed_refusal");
        _world.ObserveStrategyState(who, new Dictionary<string, int> { ["observed_refusal"] = prior + 1 }, "heard_reply:" + choice);
    }

    private void ObserveStrategySource(string scene)
    {
        var sources = Content.TryGetProperty("STRATEGY_TRACK", out var track) && track.TryGetProperty("source_scenes", out var aliases)
            ? aliases.EnumerateObject().Where(source => source.Value.EnumerateArray().Any(s => s.GetString() == scene))
                .Select(source => source.Name).Append(scene).Distinct(StringComparer.Ordinal).ToArray()
            : new[] { scene };
        foreach (var who in Companions.Where(w => Session.IsLocalTarget(w, "detective") && F(w).Visible))
            foreach (var source in sources) _world.AcquireStrategySource(who, source);
    }

    private void ObserveStrategyScene(string title)
    {
        if (!Content.TryGetProperty("STRATEGY_TRACK", out var track)) return;
        if (track.GetProperty("world_resolution_scenes").EnumerateArray().Any(s => s.GetString() == title))
            _out.Add(Event("world_resolution", ("minutes", _content.GameMinutes)));
        if (!track.GetProperty("wrong_scenes").EnumerateArray().Any(s => s.GetString() == title)) return;
        foreach (var who in track.GetProperty("people").EnumerateObject().Select(p => p.Name))
            if (Session.IsLocalTarget(who, "detective") && F(who).Visible)
            {
                var wrong = _world.StrategyBasis(who).GetValueOrDefault("observed_wrong");
                _world.ObserveStrategyState(who, new Dictionary<string, int> { ["observed_wrong"] = wrong + 1 }, "heard_response:" + title);
            }
    }
}
