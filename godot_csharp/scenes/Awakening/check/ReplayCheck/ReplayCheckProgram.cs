using System.Text.Json;
using static DualOntologyWorldEngine.TestSupport.FixtureData;
using System.Text;
using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Core;

// 《门的这一侧》重演检查：用游戏内同一份 C# 重演（NightReplay，跑在 Core 上）穷举 216 种组合，
// 并检查感知隔离、重演不改现实、回忆只影响本人。
// 输出写到 stdout 与 artifacts/remake-belief-replay/solution-space.txt。
var output = new StringBuilder();
var failures = 0;
var checks = 0;
void Line(string text) { Console.WriteLine(text); output.AppendLine(text); }
void Check(bool ok, string name)
{
    checks++;
    if (!ok) failures++;
    Line((ok ? "PASS " : "FAIL ") + name);
}

var truth = SampleAs<NightVariables>("replay-check", "golden-variables");
var variableDisplays = Chapter1Data.Root.GetProperty("VARIABLES").EnumerateArray()
    .ToDictionary(variable => variable.GetProperty("key").GetString()!, variable => variable.GetProperty("options"));
string Short(NightVariables v) => string.Join(" ", new[]
    { ("V1", (int)v.V1), ("V2", (int)v.V2), ("V3", (int)v.V3), ("V4", (int)v.V4), ("V5", (int)v.V5), ("V6", (int)v.V6) }
    .Select(value => variableDisplays[value.Item1][value.Item2].GetString()));

// ---------------------------------------------------------------- 1. 解空间穷举
var morning = new MorningWorld();
var realityBefore = morning.RealityFingerprint();

var all = NightVariables.All().ToArray();
Check(all.Length == 216, $"变量组合共 {all.Length} 种");
var results = all.Select(NightReplay.Run).ToArray();
var matches = results.Where(r => r.FullMatch).ToArray();
Check(matches.Length == 1, $"全部证物对照下完全吻合的组合数 = {matches.Length}");
Check(matches.Length == 1 && matches[0].Variables == truth, "唯一吻合组合 = 设计 §4.4（她的房间 / 活动厅铜口旁 / 从主楼对着铜口说 / 闩门，别开 / 霍铎 / 早已相熟）");
Check(results.All(r => r.Log.All(l => !l.Contains(SampleText("replay-check", "text-001")))), SampleText("replay-check", "text-002"));
Check(results.All(r => r.Frames.Length >= 3), SampleText("replay-check", "text-003"));

Line("");
Line(SampleText("replay-check", "text-004"));
foreach (var r in results.Where(r => !r.FullMatch))
    Line($"{Short(r.Variables),-40} -> {string.Join(" ", r.Conflicts)}");

Line("");
Line(SampleText("replay-check", "text-005"));
var expected = Sample("replay-check", "single-wrong-values").EnumerateArray().Select(row => (
    Name: row.GetProperty("name").GetString()!, Variables: row.GetProperty("variables").Deserialize<NightVariables>()!,
    Evidence: row.GetProperty("evidence").EnumerateArray().Select(id => id.GetString()!).ToArray()));
foreach (var (name, variables, evidence) in expected)
{
    var conflicts = results.First(r => r.Variables == variables).Conflicts;
    Check(evidence.All(conflicts.Contains), $"{name}：设计列出 {string.Join("、", evidence)}；重演实际冲突 {string.Join("、", conflicts)}");
}

foreach (var (name, wrong) in new (string, Func<NightVariables, bool>)[]
{
    (SampleText("replay-check", "text-015"), v => v.V1 != truth.V1), (SampleText("replay-check", "text-016"), v => v.V2 != truth.V2), (SampleText("replay-check", "text-017"), v => v.V3 != truth.V3),
    (SampleText("replay-check", "text-018"), v => v.V4 != truth.V4), (SampleText("replay-check", "text-019"), v => v.V5 != truth.V5), (SampleText("replay-check", "text-020"), v => v.V6 != truth.V6),
})
{
    var subset = results.Where(r => wrong(r.Variables)).ToArray();
    Check(subset.All(r => r.Conflicts.Length > 0), $"{name}：{subset.Length} 个组合全部至少有一件冲突证物");
}

Line("");
Line(SampleText("replay-check", "text-021"));
foreach (var (id, _, name) in Comparison.Items)
{
    var survivors = results.Where(r => r.Conflicts.All(c => c == id)).Select(r => Short(r.Variables)).ToArray();
    Line($"不看 {id} {name}：{survivors.Length} 个组合吻合  {string.Join(" | ", survivors)}");
}

// 唯一解不依赖行动值调参：在一组速度、延迟参数上复测（逐项改一个参数，再加随机组合）。
Line("");
Line(SampleText("replay-check", "text-022"));
var timings = new List<(string Name, NightTiming Timing)>();
var baseTiming = NightTiming.Default;
foreach (var v in new[] { 0.4, 0.6, 1.2, 2.0 }) timings.Add(($"侦探速度 {v}", baseTiming with { DetectiveSpeed = v }));
foreach (var v in new[] { 0.5, 0.8, 2.0, 3.0 }) timings.Add(($"苏泠速度 {v}", baseTiming with { SuSpeed = v }));
foreach (var v in new[] { 0.5, 2.0 }) timings.Add(($"霍铎速度 {v}", baseTiming with { HuoSpeed = v }));
foreach (var v in new[] { 0.4, 2.0 }) timings.Add(($"沈鼎速度 {v}", baseTiming with { ShenSpeed = v }));
foreach (var v in new[] { 0.5, 3.0, 40.0 }) timings.Add(($"烟传到备餐间 {v}", baseTiming with { SmokeReachesPantry = v }));
foreach (var v in new[] { 0.2, 3.0 }) timings.Add(($"到场先看一眼 {v}", baseTiming with { LookDelay = v }));
foreach (var v in new[] { 6.0, 20.0 }) timings.Add(($"侦探惊醒后延迟 {v}", baseTiming with { DetectiveWakes = v }));
var random = new Random(20261003);
double Pick(params double[] values) => values[random.Next(values.Length)];
for (var i = 0; i < 20; i++)
    timings.Add(($"随机组合 {i + 1}", new NightTiming(Pick(0.5, 0.85, 1.5), Pick(0.6, 1.0, 1.6), Pick(0.6, 0.9, 1.6), Pick(0.6, 1.3, 2.5),
        Pick(1, 8, 13, 30), Pick(0, 4, 12), Pick(0.3, 1, 2.5))));
var falseMatches = new List<string>();
var truthLost = new List<string>();
foreach (var (name, timing) in timings)
{
    var sweep = all.Select(v => NightReplay.Run(v, timing)).ToArray();
    var winners = sweep.Where(r => r.FullMatch).Select(r => r.Variables).ToArray();
    var wrong = winners.Where(w => w != truth).ToArray();
    if (wrong.Length > 0) falseMatches.Add($"{name}：{string.Join(" | ", wrong.Select(Short))}");
    if (!winners.Contains(truth))
    {
        var truthRunHere = sweep.First(r => r.Variables == truth);
        truthLost.Add($"{name}（真值组合冲突 {string.Join(" ", truthRunHere.Conflicts)}）");
    }
    Line($"{(wrong.Length > 0 ? SampleText("replay-check", "text-023") : winners.Contains(truth) ? SampleText("replay-check", "text-024") : SampleText("replay-check", "text-025"))}  {name}  {timing}");
}
Check(falseMatches.Count == 0, $"{timings.Count} 组行动值参数下，没有任何错误组合吻合现场" + (falseMatches.Count == 0 ? "" : SampleText("replay-check", "text-026") + string.Join("；", falseMatches)));
Check(results.First(r => r.Variables == truth).FullMatch, SampleText("replay-check", "text-027"));
Line($"仍依赖先后的地方：{timings.Count - truthLost.Count}/{timings.Count} 组参数下真值组合仍全部吻合；其余 {truthLost.Count} 组里真值本身重现不出现场（苏泠须先于侦探到门边开门）：{string.Join("；", truthLost)}");

// 盲目改值能多快试出答案：每次只改一个变量、选冲突最少的那一改（贪心爬山），从每个起点出发要重演几次。
// 这一项只是度量，不判通过与否：次数越少，越可能退化成“试到全绿”的填空。
Line("");
Line(SampleText("replay-check", "text-028"));
var byVars = results.ToDictionary(r => r.Variables, r => r.Conflicts.Length);
int[] Sizes = [3, 3, 2, 2, 3, 2];
NightVariables With(NightVariables v, int index, int value) => index switch
{
    0 => v with { V1 = (SuPlace)value }, 1 => v with { V2 = (HuoPlace)value }, 2 => v with { V3 = (VoiceOrigin)value },
    3 => v with { V4 = (VoiceWords)value }, 4 => v with { V5 = (DetectiveHeeds)value }, _ => v with { V6 = (Acquaintance)value }
};
var stuck = 0;
var counts = new List<int>();
foreach (var start in all)
{
    var current = start;
    var replays = 1;
    while (byVars[current] > 0)
    {
        var best = current;
        for (var i = 0; i < 6; i++)
            for (var value = 0; value < Sizes[i]; value++)
            {
                var next = With(current, i, value);
                if (next == current) continue;
                replays++;
                if (byVars[next] < byVars[best]) best = next;
            }
        if (best == current) { stuck++; break; }
        current = best;
    }
    if (byVars[current] == 0) counts.Add(replays);
}
Line($"从 216 个起点出发：{counts.Count} 个靠逐项试改到达全部吻合，{stuck} 个卡在局部最优；到达者平均重演 {counts.DefaultIfEmpty().Average():0.0} 次，最多 {counts.DefaultIfEmpty().Max()} 次。");
var minimal = results.Where(r => !r.FullMatch).Min(r => r.Conflicts.Length);
Line($"错误组合中冲突最少的有 {results.Count(r => r.Conflicts.Length == minimal)} 个，各有 {minimal} 件冲突证物：" +
     string.Join(" | ", results.Where(r => r.Conflicts.Length == minimal).Select(r => Short(r.Variables) + " -> " + string.Join(" ", r.Conflicts))));

// ---------------------------------------------------------------- 2. 引擎推演，而非查表
Line("");
Line(SampleText("replay-check", "text-029"));
var strangers = results.First(r => r.Variables == truth with { V6 = Acquaintance.Strangers });
Check(strangers.Log.Any(l => l.Contains(SampleText("replay-check", "text-030"))), SampleText("replay-check", "text-031"));
Check(strangers.Frames.Any(f => f.Thoughts[Person.Detective].Contains(SampleText("replay-check", "text-032"))), SampleText("replay-check", "text-033"));
var truthRun = results.First(r => r.Variables == truth);
var voiceFrame = truthRun.Frames.First(f => f.Event.Kind == "voice_tube");
Check(voiceFrame.Thoughts.Values.Distinct().Count() == 4, SampleText("replay-check", "text-034") + string.Join(" / ", voiceFrame.Thoughts.Select(t => $"{Names.Of(t.Key)}：{t.Value}")));
var again = NightReplay.Run(truth);
Check(again.Traces.ToString() == truthRun.Traces.ToString() && again.Log.SequenceEqual(truthRun.Log), SampleText("replay-check", "text-035"));

// ---------------------------------------------------------------- 3. 感知隔离
Line("");
Line(SampleText("replay-check", "text-036"));
var positions = new Dictionary<Person, Room> { [Person.Detective] = Room.Foyer, [Person.Huo] = Room.Hall, [Person.Shen] = Room.Pantry, [Person.Su] = Room.R05 };
var heard = morning.SoundTest(TestSentences.All[0], positions);
foreach (var h in heard) Line($"  {Names.Of(h.Who)} 在 {Names.Of(h.Where)}：{h.Text}{(h.SeemsFromOutside ? SampleText("replay-check", "text-037") : "")}");
Check(heard.Select(h => h.Text).Distinct().Count() == 4, SampleText("replay-check", "text-038"));
Check(heard.First(h => h.Where == Room.Foyer) is { Hearing: Hearing.Tail, SeemsFromOutside: true } foyerHeard && foyerHeard.Text == SampleText("replay-check", "text-039"), SampleText("replay-check", "text-040"));
Check(heard.First(h => h.Where == Room.Hall).Hearing == Hearing.Full, SampleText("replay-check", "text-041"));
Check(morning.WorldOf(Person.Detective).History.Last().Details == SampleText("replay-check", "text-039") && morning.WorldOf(Person.Huo).History.Last().Details == SampleText("replay-check", "text-042"),
    SampleText("replay-check", "text-043"));
Check(morning.RealityFingerprint() == realityBefore, SampleText("replay-check", "text-044"));

// ---------------------------------------------------------------- 4. 重演不改现实
Line("");
Line(SampleText("replay-check", "text-045"));
var mindsBeforeReplay = Enum.GetValues<Person>().ToDictionary(p => p, morning.MindFingerprint);
foreach (var v in all.Take(40)) NightReplay.Run(v);
Check(morning.RealityFingerprint() == realityBefore, SampleText("replay-check", "text-046"));
Check(Enum.GetValues<Person>().All(p => morning.MindFingerprint(p) == mindsBeforeReplay[p]), SampleText("replay-check", "text-047"));

// ---------------------------------------------------------------- 5. 回忆只影响本人
Line("");
Line(SampleText("replay-check", "text-048"));
foreach (var memory in MorningWorld.Memories)
{
    var before = Enum.GetValues<Person>().ToDictionary(p => p, morning.MindFingerprint);
    var hooks = morning.Hooks(memory.Who);
    morning.Recall(memory.Evidence);
    var changed = Enum.GetValues<Person>().Where(p => morning.MindFingerprint(p) != before[p]).ToArray();
    Check(changed.SequenceEqual([memory.Who]) && morning.Hooks(memory.Who) == hooks + 1,
        $"{Names.Of(memory.Who)}触碰 {memory.Evidence}：只有{Names.Of(memory.Who)}的认知改变，钩 {hooks}→{morning.Hooks(memory.Who)}");
    Check(morning.WorldOf(memory.Who).History.Last().Source == ObservationSource.Recall, $"{Names.Of(memory.Who)}的片段来源为 Recall");
    morning.Recall(memory.Evidence);
    Check(morning.Hooks(memory.Who) == hooks + 1, $"{Names.Of(memory.Who)}重复回忆不再加钩");
}
Check(morning.RealityFingerprint() == realityBefore, SampleText("replay-check", "text-049"));

// ---------------------------------------------------------------- 6. 欲望放大与同伴是否触物回忆（引擎决定）
Line("");
Line(SampleText("replay-check", "text-050"));
RecallDecision Ask(MorningWorld world, string evidence, Person who, bool showRule1)
{
    if (showRule1) world.ShowRule1(who);
    world.Amplify(who, SampleText("replay-check", "text-051"));
    return world.DecideRecall(evidence)!;
}
var told = new MorningWorld();
foreach (var who in Enum.GetValues<Person>()) told.Amplify(who, SampleText("replay-check", "text-052"));
Check(told.WorldOf(Person.Huo).History.Any(h => h.Source == ObservationSource.Corruption) && told.DesireStrength(Person.Huo) == 2,
    SampleText("replay-check", "text-053"));
// 三人倾向各不相同（设计 §5）：(物件, 人, 未告知时, 已告知时)
foreach (var (evidence, who, plainExpected, toldExpected) in Sample("replay-check", "recall-choices").EnumerateArray().Select(row => (row.GetProperty("evidence").GetString()!, Enum.Parse<Person>(row.GetProperty("who").GetString()!), Enum.Parse<RecallChoice>(row.GetProperty("plain").GetString()!), Enum.Parse<RecallChoice>(row.GetProperty("told").GetString()!))))
{
    var plain = new MorningWorld();
    plain.Amplify(who, "S4");
    var yes = Ask(plain, evidence, who, false);
    var shown = new MorningWorld();
    shown.Amplify(who, "S4");
    var othersBefore = Enum.GetValues<Person>().Where(p => p != who).ToDictionary(p => p, shown.MindFingerprint);
    var no = Ask(shown, evidence, who, true);
    Check(yes.Choice == plainExpected, $"{Names.Of(who)}：未被告知守则第 1 条时，DecisionSystem 选择 {yes.Choice}（{yes.Thought}）");
    Check(no.Choice == toldExpected, $"{Names.Of(who)}：被告知守则第 1 条后，选择 {no.Choice}（{no.Thought}）");
    Check(othersBefore.All(p => shown.MindFingerprint(p.Key) == p.Value), $"{Names.Of(who)}：指看守则第 1 条与恶魔的话只改变他本人的认知");
}
// 沈鼎犹豫时反问侦探：三种回答经 SocialSystem 判定后写进他的认知，再由 DecisionSystem 取舍。
foreach (var (advice, adviceExpected) in new[] { ("touch", RecallChoice.Touch), ("decline", RecallChoice.Refuse), ("own", RecallChoice.Refuse) })
{
    var world = new MorningWorld();
    world.Amplify(Person.Shen, "S4");
    Ask(world, "E10", Person.Shen, false);
    var stance = world.Advise(Person.Shen, advice);
    var after = world.DecideRecall("E10")!;
    Check(stance == DualOntologyWorldEngine.Core.SocialStance.Accept && after.Choice == adviceExpected,
        $"沈鼎“你说呢？”——侦探回答 {advice}：SocialSystem 判 {stance}，沈鼎选择 {after.Choice}（{after.Thought}）");
}
Check(MorningWorld.PostedRules.Length == 4 && MorningWorld.PostedRules.All(c => !c.Contains(SampleText("replay-check", "text-054")) && !c.Contains(SampleText("replay-check", "text-055")) && !c.Contains(SampleText("replay-check", "text-056")) && !c.Contains(SampleText("replay-check", "text-057"))),
    SampleText("replay-check", "text-058"));

// ---------------------------------------------------------------- 6b. 冲突只描述痕迹之差
Line("");
Line(SampleText("replay-check", "text-059"));
var conflictTexts = results.SelectMany(r => Comparison.Compare(r.Traces, MorningTraces.Evidence, _ => true))
    .Where(f => f.Status == FindingStatus.Conflict).Select(f => f.Text).Distinct().ToArray();
var actionWords = new[] { SampleText("replay-check", "text-060"), SampleText("replay-check", "text-061"), SampleText("replay-check", "text-062"), SampleText("replay-check", "text-063"), SampleText("replay-check", "text-064"), SampleText("replay-check", "text-065"), SampleText("replay-check", "text-066"), SampleText("replay-check", "text-067"), SampleText("replay-check", "text-068"), SampleText("replay-check", "text-069") };
var offending = conflictTexts.Where(t => actionWords.Any(t.Contains)).ToArray();
Check(conflictTexts.Length > 0 && offending.Length == 0 && conflictTexts.All(t => t.StartsWith(SampleText("replay-check", "text-070")) && t.Contains(SampleText("replay-check", "text-071"))),
    $"216 次重演的 {conflictTexts.Length} 种冲突文字都写成“重演中……；现场……”，不出现谁做了什么" + (offending.Length > 0 ? "：" + string.Join(" / ", offending) : ""));

// ---------------------------------------------------------------- 7. 同伴自己会去碰（涌现）
Line("");
Line(SampleText("replay-check", "text-072"));
foreach (var (evidence, who, room, wrong) in Sample("replay-check", "reach-cases").EnumerateArray().Select(row => (row.GetProperty("evidence").GetString()!, Enum.Parse<Person>(row.GetProperty("who").GetString()!), Enum.Parse<Room>(row.GetProperty("room").GetString()!), Enum.Parse<Room>(row.GetProperty("wrong").GetString()!))))
{
    var world = new MorningWorld();
    world.Amplify(who, "S4");
    var before = world.Intend(who)!;
    world.Whisper(who, SampleText("replay-check", "text-073"));
    var after = world.Intend(who)!;
    Check(!before.Reach && after.Reach && world.WorldOf(who).History.Last().Source == ObservationSource.Corruption,
        $"{Names.Of(who)}：低语之前守着手头的事（{before.Thought}）；低语（Corruption）之后自己要去拿（{after.Thought}）");
    var reality = world.RealityFingerprint();
    world.Locate(who, wrong);
    var far = world.Reach(who);
    Check(!far && !world.Holding(who), $"{Names.Of(who)}：人不在{Names.Of(room)}时，ActionRuntime 判定拿不到");
    world.Locate(who, room);
    Check(world.Reach(who) && world.Holding(who), $"{Names.Of(who)}：走到{Names.Of(room)}后伸手，拿起了{evidence}");
    world.PutBack(who);
    Check(!world.Holding(who) && world.IsDisturbed(evidence) && world.RealityFingerprint() != reality,
        $"{Names.Of(who)}：放回后现实里留下“被动过”的痕迹");
    Check(!world.Intend(who)!.Reach, $"{Names.Of(who)}：拿定主意之后不再自己去拿");
    var toldFirst = new MorningWorld();
    toldFirst.Amplify(who, "S4");
    toldFirst.ShowRule1(who);
    toldFirst.Whisper(who, SampleText("replay-check", "text-073"));
    var expectGo = who == Person.Huo;
    Check(toldFirst.Intend(who)!.Reach == expectGo, $"{Names.Of(who)}：事先读过守则第 1 条，低语之后{(expectGo ? SampleText("replay-check", "text-074") : SampleText("replay-check", "text-075"))}");
}
// 侦探在场时的三种回应：劝阻（Testimony，经 SocialSystem）、说明守则第 1 条（感知）、任其自便（什么也不说）；不在场时他独自取舍。
foreach (var (evidence, who, declineExpected, clauseExpected, noneExpected, aloneExpected) in Sample("replay-check", "recall-responses").EnumerateArray().Select(row => (row.GetProperty("evidence").GetString()!, Enum.Parse<Person>(row.GetProperty("who").GetString()!), Enum.Parse<RecallChoice>(row.GetProperty("decline").GetString()!), Enum.Parse<RecallChoice>(row.GetProperty("clause").GetString()!), Enum.Parse<RecallChoice>(row.GetProperty("none").GetString()!), Enum.Parse<RecallChoice>(row.GetProperty("alone").GetString()!))))
{
    MorningWorld Fresh()
    {
        var w = new MorningWorld();
        w.Amplify(who, "S4");
        w.Whisper(who, SampleText("replay-check", "text-073"));
        return w;
    }
    var dissuaded = Fresh();
    var stance = dissuaded.Advise(who, "decline");
    var a = dissuaded.DecideRecall(evidence)!;
    var shown = Fresh();
    shown.ShowRule1(who);
    var b = shown.DecideRecall(evidence)!;
    var c = Fresh().DecideRecall(evidence)!;
    var d = Fresh().DecideAlone(evidence)!;
    Check(a.Choice == declineExpected, $"{Names.Of(who)}：侦探劝阻（SocialSystem 判 {stance}）→ {a.Choice}（{a.Thought}）");
    Check(b.Choice == clauseExpected, $"{Names.Of(who)}：侦探说明守则第 1 条 → {b.Choice}（{b.Thought}）");
    Check(c.Choice == noneExpected, $"{Names.Of(who)}：侦探任其自便 → {c.Choice}（{c.Thought}）");
    Check(d.Choice == aloneExpected, $"{Names.Of(who)}：侦探不在场 → {d.Choice}（{d.Thought}）");
}

Line("");
Line(SampleText("replay-check", "text-076"));
foreach (var l in truthRun.Log) Line("  " + l);
Line("");
Line($"TOTAL {checks} checks, {failures} failed");

for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    if (Directory.Exists(Path.Combine(dir.FullName, "artifacts")) && Directory.Exists(Path.Combine(dir.FullName, "csharp")))
    {
        var target = Path.Combine(dir.FullName, "artifacts", "remake-belief-replay");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "solution-space.txt"), output.ToString());
        break;
    }
return failures == 0 ? 0 : 1;
