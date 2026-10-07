using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>第一章自动回归探针（读状态、注入局面）；游玩路径不经过这里。</summary>
public sealed partial class Chapter1Session : IChapterProbe
{
    private readonly Dictionary<int, AwakeningContentSession.InteractionPlan> _probePlans = new();
    private int _probePlanKey;

    public object? Probe(string name, object?[] args)
    {
        string A(int index) => index < args.Length ? args[index]?.ToString() ?? "" : "";
        switch (name)
        {
            case "progress": return Session.GetProgress(A(0)).ToDictionary(p => p.Key, p => (object?)p.Value);
            case "set_progress":
                Session.Set(A(0), A(1), args[2] is long n ? (int)n : args[2]!);
                if (A(0) == "examined") Session.Observe("detective", A(1));
                return null;
            case "remove_progress": Session.Remove(A(0), A(1)); return null;
            case "hooks": return _world.Hooks(PersonOf(A(0)));
            case "total_hooks": return _world.TotalHooks;
            case "desire": return _world.DesireStrength(PersonOf(A(0)));
            case "amplify": return _world.Amplify(PersonOf(A(0)), A(1));
            case "whisper": _world.Whisper(PersonOf(A(0)), A(1)); return null;
            case "whispered": return _world.Whispered(PersonOf(A(0)));
            case "apply_recall": _content.ApplyRecall(_world, A(0), A(1)); return null;
            case "is_disturbed": return _world.IsDisturbed(A(0));
            case "posted_rules": return MorningWorld.PostedRules.Cast<object?>().ToArray();
            case "content": return Content.GetRawText();
            case "v6_open": return _content.V6Open;
            case "statements":
            {
                var replay = Session.GetProgress("replay_result");
                return _content.Statements(((object[])State["replay_vars"]).Select(Convert.ToInt32).ToArray(),
                    Convert.ToInt32(State["replay_count"]), replay.GetValueOrDefault("detective_bolted") is true).Cast<object?>().ToArray();
            }
            case "sound_test":
            {
                var map = new Dictionary<Person, Room>();
                foreach (var (who, room) in (Dictionary<string, object?>)args[1]!) map[PersonOf(who)] = (Room)Array.IndexOf(Names.RoomKeys, room?.ToString());
                _content.RecordSoundTest();
                var sentence = Math.Clamp(Convert.ToInt32(args[0]), 0, TestSentences.All.Length - 1);
                return _world.SoundTest(TestSentences.All[sentence], map).Select(h => (object?)new Dictionary<string, object?>
                {
                    ["who"] = Names.PersonKeys[(int)h.Who], ["room"] = Names.RoomKeys[(int)h.Where], ["text"] = h.Text, ["outside"] = h.SeemsFromOutside,
                    ["hearing"] = h.Hearing.ToString(), ["direction"] = h.Direction,
                }).ToArray();
            }
            case "consider": Start(Consider(A(0))); return null;
            case "say": Start(Say(A(0))); return null;
            case "examine": Examine(A(0)); return null;
            case "replace_progress":
            {
                var group = Session.GetProgress(A(0));
                group.Clear();
                foreach (var (key, value) in (Dictionary<string, object?>)args[1]!) group[key] = value!;
                return null;
            }
            case "set_detail": ((Dictionary<string, object>)Session.GetProgress(A(0))[A(1)])[A(2)] = args[3] is long detail ? (int)detail : args[3]!; return null;
            case "locate": Locate(A(0), A(1)); return null;
            case "card_notes": return _content.CardNotes(_world, A(0)).Cast<object?>().ToArray();
            case "plan_begin_experiment":
                _probePlans[++_probePlanKey] = _content.StartPlan(() => _content.BeginExperiment(_world, A(0), A(1)));
                return _probePlanKey;
            case "plan_next":
            {
                var key = Convert.ToInt32(args[0]);
                var plan = _probePlans[key];
                var output = _content.NextMeeting(plan);
                if (plan.Finished) _probePlans.Remove(key);
                return output;
            }
            case "plan_presented": _content.Presented(_probePlans[Convert.ToInt32(args[0])]); return null;
            case "stage": return Stage;
            case "jobs": return _jobs.Count;
            case "clock": return _content.ClockText;
            case "publish": ChapterWorldContinuity.Publish(_world.ExportContinuation(Session)); return null;
            default: throw new ArgumentException("未知探针：" + name);
        }
    }
}
