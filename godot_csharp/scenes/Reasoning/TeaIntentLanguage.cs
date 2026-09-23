using System.Text.RegularExpressions;
using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Game.Reasoning;

// A task expands into the same physical actions as a handwritten rule. It never sets an outcome.
public enum TeaIntentKind { Borrow, Replace, Cloth, Store, Action }
public enum TeaIntentStyle { None, Remember, Inspect, Stack, Separate }
public sealed record TeaIntent(int Notice, TeaIntentKind Kind, Essence Target, Essence? Destination, Essence? Other, TeaIntentStyle Style, TeaVerb Verb, int Line);

public sealed partial class TeaCase
{
    public static readonly string[] Notices = ["准备时", "第一声铃后", "收布通知时", "第二声铃后", "收柜通知时"];
    private static readonly Tag HeardNotice = new("已收到通知");
    private readonly Dictionary<TeaPerson, List<TeaIntent>> _intents = [];
    private readonly Dictionary<TeaPerson, List<TeaIntent>> _pendingIntentConfiguration = [];
    private readonly Dictionary<TeaPerson, Queue<TeaIntent>> _noticeTasks = [];
    private readonly Dictionary<TeaPerson, Queue<TeaDecisionRule>> _intentSteps = [];
    private readonly Dictionary<TeaPerson, Action> _afterInspection = [];
    private readonly HashSet<TeaPerson> _activeIntent = [];
    private readonly HashSet<(TeaPerson Person, int Line, string Memory)> _attempted = [];
    private int _notice;
    private int _noticeTicks;
    private bool _clothSpill;

    private string CompileIntents(TeaPerson p, string text, List<string> errors, out List<TeaIntent> tasks)
    {
        var collected = new List<TeaIntent>(); tasks = collected;
        var legacy = new List<string>();
        var lines = text.Replace("\r", "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Split('#', 2)[0].Trim();
            var header = Regex.Match(line, "^(.+?)[：:](.+)$");
            if (!header.Success) { legacy.Add(lines[i]); continue; }
            legacy.Add("");
            try
            {
                var phase = Array.IndexOf(Notices, header.Groups[1].Value.Trim());
                if (phase < 0) throw new ArgumentException("通知可用：" + string.Join("、", Notices));
                var body = Regex.Replace(header.Groups[2].Value, @"\s+", "").TrimEnd('。');
                Essence Object(string name) => Find(name) ?? throw new ArgumentException("不认识的对象：" + name);
                void Add(TeaIntentKind kind, Essence target, Essence? dest = null, Essence? other = null, TeaIntentStyle style = TeaIntentStyle.None, TeaVerb verb = TeaVerb.Inspect) =>
                    collected.Add(new(phase, kind, target, dest, other, style, verb, i + 1));
                var m = Regex.Match(body, "^借用(.+?)给(.+?)[，,]用后放回(.+)$");
                if (m.Success)
                {
                    var cup = Object(m.Groups[1].Value); var dest = Object(m.Groups[2].Value);
                    if (!Cups.Contains(cup)) throw new ArgumentException("借用需要选择一只杯子");
                    if (dest != Basin && dest != Tray && dest != Sink && dest != Chair) throw new ArgumentException("茶的用途应为温瓶盆、染纸盘、洗具台或椅面");
                    var back = m.Groups[3].Value == "原处" ? null : Object(m.Groups[3].Value);
                    if (back is not null && !Sites.Contains(back) && back != Saucer && back != Cloth) throw new ArgumentException("归还处应为地点、续茶碟或椅布");
                    Add(TeaIntentKind.Borrow, cup, dest, back); continue;
                }
                m = Regex.Match(body, "^用(.+?)替换(.+?)[，,]旧杯暂放(.+)$");
                if (m.Success)
                {
                    var next = Object(m.Groups[1].Value); var old = Object(m.Groups[2].Value); var temp = Object(m.Groups[3].Value);
                    if (!Cups.Contains(next) || !Cups.Contains(old) || next == old) throw new ArgumentException("换杯需要两只不同的杯子");
                    if (!Sites.Contains(temp) && temp != Cloth && temp != Saucer) throw new ArgumentException("暂放处应为地点、椅布或续茶碟");
                    Add(TeaIntentKind.Replace, old, temp, next); continue;
                }
                if (body is "照记忆收起椅布" or "检查后收起椅布")
                { Add(TeaIntentKind.Cloth, Cloth, style: body.StartsWith("照记忆") ? TeaIntentStyle.Remember : TeaIntentStyle.Inspect); continue; }
                m = Regex.Match(body, "^收存(.+?)和(.+?)[，,]方式(叠放|分开放)$");
                if (m.Success)
                {
                    var cup = Object(m.Groups[1].Value); var plate = Object(m.Groups[2].Value);
                    if (!Cups.Contains(cup) || plate != Saucer) throw new ArgumentException("收存需要一只杯子和续茶碟");
                    Add(TeaIntentKind.Store, cup, Cabinet, plate, m.Groups[3].Value == "叠放" ? TeaIntentStyle.Stack : TeaIntentStyle.Separate); continue;
                }
                // Raw actions remain available inside a once-per-notice task.
                var parsed = TeaRuleLanguage.Parse("当 总是，则 " + header.Groups[2].Value.Trim());
                if (parsed.Errors.Count != 0) throw new ArgumentException("任务句式：借用…给…，用后放回… / 用…替换…，旧杯暂放… / 照记忆收起椅布 / 检查后收起椅布 / 收存…和续茶碟，方式叠放或分开放；也可直接写搬到、倒入、查看、锁上。");
                var cmd = parsed.Rules.Single().Command;
                var target = Object(cmd.Target); var destination = cmd.Destination is { } name ? Object(name) : null;
                if (cmd.Verb is TeaVerb.Inspect or TeaVerb.Move && !Sites.Contains(target)) throw new ArgumentException("查看/前往需要地点");
                if (cmd.Verb == TeaVerb.Lock && target != Cabinet || cmd.Verb == TeaVerb.Pull && target != Cloth) throw new ArgumentException("此对象不能执行这个动作");
                if (cmd.Verb is TeaVerb.Carry or TeaVerb.Pour && !Objects.Contains(target)) throw new ArgumentException("只能搬运器物");
                if (cmd.Verb == TeaVerb.Pour && (!Cups.Contains(target) && target != Pot || destination != Basin && destination != Tray && destination != Chair && destination != Sink)) throw new ArgumentException("倾倒需要杯/壶和接茶容器");
                if (cmd.Verb == TeaVerb.Carry && (destination == target || destination is null || !Sites.Contains(destination) && destination != Cloth && destination != Saucer)) throw new ArgumentException("搬运目标应为不同的地点或承托物");
                Add(TeaIntentKind.Action, target, destination, verb: cmd.Verb);
            }
            catch (ArgumentException e) { errors.Add($"{p.Name} 第{i + 1}行：{e.Message}"); }
        }
        return string.Join("\n", legacy);
    }

    private string KnowledgeSignature(TeaPerson p) => _notice + "|" + string.Join(";", p.Known.Select(k =>
        $"{k.Key.TrueName}:{k.Value.Parent?.Name}:{string.Join(',', k.Value.Tags.OrderBy(t => t.Key.Name).Select(t => t.Value))}:{string.Join(',', k.Value.Relations.Select(r => r.Target.Name))}"));

    private void BeginNotice(int notice)
    {
        _notice = notice; _noticeTicks = 0; _attempted.Clear();
        foreach (var p in People)
        {
            Observation.Observe(p.Soul, new(p.Name, ProjectionKind.Body)
            {
                Target = p.Self, Source = ObservationSource.Perception, Time = Clock.Now,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(HeardNotice, notice), Details = "听到公共通知：" + Notices[notice]
            });
            _noticeTasks[p] = new(_intents.GetValueOrDefault(p, []).Where(t => t.Notice == notice));
            _intentSteps[p] = new();
            p.Reason = "听到了公开通知：" + Notices[notice];
            Wake(p);
        }
        Capture("公开事件 · " + Notices[notice] + "；人物按同一份假设行动，忙完才进入下一次通知。");
    }

    private void CheckNoticeProgress()
    {
        if (Finished) return;
        if (++_noticeTicks > 1500)
        {
            Finished = true; Differences.Add("过程未结束：人物在重复改变状态，尚未完成当前通知下的事情。");
            CheckFinal(); Capture("重演停止 · 当前任务没有收束"); return;
        }
        if (People.All(p => !p.Busy && !_scheduled.Contains(p) && _noticeTasks[p].Count == 0 && _intentSteps[p].Count == 0 && !_activeIntent.Contains(p)))
        {
            if (_notice is 0 or 2 or 4) Ring();
            if (_notice == 4) { Finished = true; CheckFinal(); Capture("重演结束 · 已检查全部时刻；请对照所有已取得的材料。"); return; }
            BeginNotice(_notice + 1);
        }
        Clock.Schedule(1, CheckNoticeProgress);
    }

    private Essence? RememberedSite(TeaPerson p, Essence e) => Sites.Contains(e) ? e :
        p.Known[e].Parent is { } memory ? Sites.FirstOrDefault(s => p.Known[s] == memory) : null;

    private Essence? RememberedSupport(TeaPerson p, Essence e) =>
        _all.FirstOrDefault(candidate => p.Known[e].Relations.Any(r => r.Relation == On && r.Target == p.Known[candidate]));

    private bool RunIntent(TeaPerson p)
    {
        if (!_noticeTasks.ContainsKey(p)) return false;
        if (_intentSteps[p].Count > 0)
        {
            p.Busy = true; Begin(p, _intentSteps[p].Dequeue()); return true;
        }
        if (_afterInspection.Remove(p, out var finishPlan))
        {
            finishPlan();
            if (_intentSteps[p].Count > 0) return RunIntent(p);
        }
        _activeIntent.Remove(p);
        if (!_noticeTasks[p].TryDequeue(out var task)) return false;
        _activeIntent.Add(p);
        p.Reason = $"{Notices[task.Notice]}收到任务 · 第{task.Line}行；取放和观察由人物完成。";
        void Queue(TeaVerb verb, Essence target, Essence? dest = null) =>
            _intentSteps[p].Enqueue(new(new Condition.Constant(Truth.True), verb, target, dest, 0, task.Line));
        void Plan()
        {
            var support = RememberedSupport(p, task.Target);
            switch (task.Kind)
            {
                case TeaIntentKind.Borrow:
                    if (task.Other is null && support is null) { p.Activity = "不知道借来的杯子原先放在哪里"; Capture(p.Name + "：" + p.Activity); break; }
                    Queue(TeaVerb.Pour, task.Target, task.Destination);
                    Queue(TeaVerb.Carry, task.Target, task.Other ?? support); break;
                case TeaIntentKind.Replace:
                    if (support is null) { Capture(p.Name + "：不知道旧杯的承托位置，无法决定替换到哪里"); break; }
                    Queue(TeaVerb.Carry, task.Target, task.Destination);
                    Queue(TeaVerb.Carry, task.Other!, support); break;
                case TeaIntentKind.Store:
                    var platePlace = RememberedSite(p, Saucer);
                    if (platePlace is null) { Capture(p.Name + "：不知道茶碟在哪里"); break; }
                    foreach (var cup in Cups.Where(c => c != task.Target && RememberedSupport(p, c) == Saucer))
                        Queue(TeaVerb.Carry, cup, platePlace); // Store only the selected pair; leave other people's cups where they were.
                    Queue(TeaVerb.Carry, Saucer, Cabinet);
                    Queue(TeaVerb.Carry, task.Target, task.Style == TeaIntentStyle.Stack ? Saucer : Cabinet); break;
                case TeaIntentKind.Cloth:
                    var onCloth = Cups.Where(c => RememberedSupport(p, c) == Cloth).ToArray();
                    if (task.Style == TeaIntentStyle.Remember && onCloth.Length > 0)
                    { Capture(p.Name + "：记得椅布上还有杯子，决定不抽布。"); break; }
                    if (task.Style == TeaIntentStyle.Inspect) foreach (var cup in onCloth) Queue(TeaVerb.Carry, cup, Supply);
                    Queue(TeaVerb.Pull, Cloth); break;
                default: Queue(task.Verb, task.Target, task.Destination); break;
            }
        }
        // A memory-based decision is made before approaching; looking again is a different policy.
        if (task.Kind == TeaIntentKind.Cloth && task.Style == TeaIntentStyle.Remember) Plan();
        else
        {
            var targets = new[] { task.Target, task.Other, task.Destination }.Where(e => e is not null).Select(e => RememberedSite(p, e!)).OfType<Essence>().Distinct();
            foreach (var site in targets) Queue(TeaVerb.Inspect, site);
            _afterInspection[p] = Plan;
        }
        return RunIntent(p);
    }
}

