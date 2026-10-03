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

var truth = new NightVariables(SuPlace.OwnRoom, HuoPlace.ByTube, VoiceOrigin.Tube, VoiceWords.BoltKeepShut, DetectiveHeeds.Huo, Acquaintance.Familiar);
string Short(NightVariables v) => string.Join(" ",
    v.V1 switch { SuPlace.OwnRoom => "苏泠在房", SuPlace.Hall => "苏泠在厅", _ => "苏泠在备餐间" },
    v.V2 switch { HuoPlace.ByTube => "霍铎在铜口", HuoPlace.OwnRoom => "霍铎在房", _ => "霍铎在门厅" },
    v.V3 == VoiceOrigin.Tube ? "经铜口" : "在门外",
    v.V4 == VoiceWords.BoltKeepShut ? "闩门别开" : "开门救我",
    v.V5 switch { DetectiveHeeds.Huo => "听霍铎", DetectiveHeeds.Su => "听苏泠", _ => "听自己" },
    v.V6 == Acquaintance.Familiar ? "相熟" : "初识");

// ---------------------------------------------------------------- 1. 解空间穷举
var morning = new MorningWorld();
var realityBefore = morning.RealityFingerprint();

var all = NightVariables.All().ToArray();
Check(all.Length == 216, $"变量组合共 {all.Length} 种");
var results = all.Select(NightReplay.Run).ToArray();
var matches = results.Where(r => r.FullMatch).ToArray();
Check(matches.Length == 1, $"全部证物对照下完全吻合的组合数 = {matches.Length}");
Check(matches.Length == 1 && matches[0].Variables == truth, "唯一吻合组合 = 设计 §4.4（她的房间 / 活动厅铜口旁 / 从主楼对着铜口说 / 闩门，别开 / 霍铎 / 早已相熟）");
Check(results.All(r => r.Log.All(l => !l.Contains("失败"))), "216 次重演中没有行动因客观不可行而失败");
Check(results.All(r => r.Frames.Length >= 3), "每次重演都产生可回放的帧（至少警铃与两次人声）");

Line("");
Line("== 每个错误组合被哪些证物否定（全部证物已检视时）");
foreach (var r in results.Where(r => !r.FullMatch))
    Line($"{Short(r.Variables),-40} -> {string.Join(" ", r.Conflicts)}");

Line("");
Line("== 单个错误值（其余变量取真值）与设计 §4.4 否定表核对");
var expected = new (string Name, NightVariables Variables, string[] Evidence)[]
{
    ("V1 活动厅", truth with { V1 = SuPlace.Hall }, ["E2", "E5"]),
    ("V1 备餐间", truth with { V1 = SuPlace.Pantry }, ["E2", "E5"]),
    ("V2 他的房间", truth with { V2 = HuoPlace.OwnRoom }, ["E7"]),
    ("V2 门厅", truth with { V2 = HuoPlace.Foyer }, ["E7", "E3"]),
    ("V3 门外走廊", truth with { V3 = VoiceOrigin.OutsideDoor }, ["E7", "E2"]),
    ("V4 开门，救我", truth with { V4 = VoiceWords.OpenSaveMe }, ["E7", "E1"]),
    ("V5 苏泠", truth with { V5 = DetectiveHeeds.Su }, ["E1", "E3"]),
    ("V5 自己判断", truth with { V5 = DetectiveHeeds.Himself }, ["E1", "E7"]),
    ("V6 素不相识", truth with { V6 = Acquaintance.Strangers }, ["E1", "E7", "E9"]),
};
foreach (var (name, variables, evidence) in expected)
{
    var conflicts = results.First(r => r.Variables == variables).Conflicts;
    Check(evidence.All(conflicts.Contains), $"{name}：设计列出 {string.Join("、", evidence)}；重演实际冲突 {string.Join("、", conflicts)}");
}

foreach (var (name, wrong) in new (string, Func<NightVariables, bool>)[]
{
    ("V1≠她的房间", v => v.V1 != truth.V1), ("V2≠铜口旁", v => v.V2 != truth.V2), ("V3≠铜口", v => v.V3 != truth.V3),
    ("V4≠闩门别开", v => v.V4 != truth.V4), ("V5≠霍铎", v => v.V5 != truth.V5), ("V6≠早已相熟", v => v.V6 != truth.V6),
})
{
    var subset = results.Where(r => wrong(r.Variables)).ToArray();
    Check(subset.All(r => r.Conflicts.Length > 0), $"{name}：{subset.Length} 个组合全部至少有一件冲突证物");
}

Line("");
Line("== 删去单件证物后吻合的组合（衡量每件证物在解空间中的必要性）");
foreach (var (id, _, name) in Comparison.Items)
{
    var survivors = results.Where(r => r.Conflicts.All(c => c == id)).Select(r => Short(r.Variables)).ToArray();
    Line($"不看 {id} {name}：{survivors.Length} 个组合吻合  {string.Join(" | ", survivors)}");
}

// 唯一解不依赖行动值调参：在一组速度、延迟参数上复测（逐项改一个参数，再加随机组合）。
Line("");
Line("== 行动值参数扫描（唯一解是否依赖先后调参）");
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
    Line($"{(wrong.Length > 0 ? "错配" : winners.Contains(truth) ? "唯一" : "真值不再重现")}  {name}  {timing}");
}
Check(falseMatches.Count == 0, $"{timings.Count} 组行动值参数下，没有任何错误组合吻合现场" + (falseMatches.Count == 0 ? "" : "；错配：" + string.Join("；", falseMatches)));
Check(results.First(r => r.Variables == truth).FullMatch, "默认行动值下真值组合必须重现现场（苏泠先于侦探到门边是故事前提，主代理裁定接受）");
Line($"仍依赖先后的地方：{timings.Count - truthLost.Count}/{timings.Count} 组参数下真值组合仍全部吻合；其余 {truthLost.Count} 组里真值本身重现不出现场（苏泠须先于侦探到门边开门）：{string.Join("；", truthLost)}");

// 盲目改值能多快试出答案：每次只改一个变量、选冲突最少的那一改（贪心爬山），从每个起点出发要重演几次。
// 这一项只是度量，不判通过与否：次数越少，越可能退化成“试到全绿”的填空。
Line("");
Line("== 贪心试错度量（每次改一个变量，保留冲突最少者）");
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
Line("== 推演来源");
var strangers = results.First(r => r.Variables == truth with { V6 = Acquaintance.Strangers });
Check(strangers.Log.Any(l => l.Contains("侦探 对 霍铎 的请求：Negotiate")), "V6=素不相识时，侦探没有接受霍铎的请求：SocialSystem 判为 Negotiate（信任不足）");
Check(strangers.Frames.Any(f => f.Thoughts[Person.Detective].Contains("我凭什么信一个昨天才认识的人？")), "回放中侦探的心声气泡出现“我凭什么信一个昨天才认识的人？”");
var truthRun = results.First(r => r.Variables == truth);
var voiceFrame = truthRun.Frames.First(f => f.Label.Contains("铜口说话"));
Check(voiceFrame.Thoughts.Values.Distinct().Count() == 4, "铜口说话的同一刻，四人的心声互不相同：" + string.Join(" / ", voiceFrame.Thoughts.Select(t => $"{Names.Of(t.Key)}：{t.Value}")));
var again = NightReplay.Run(truth);
Check(again.Traces.ToString() == truthRun.Traces.ToString() && again.Log.SequenceEqual(truthRun.Log), "同一组变量重演两次，结果与过程完全相同（每次新建独立世界）");

// ---------------------------------------------------------------- 3. 感知隔离
Line("");
Line("== 感知隔离（清晨的声路试验）");
var positions = new Dictionary<Person, Room> { [Person.Detective] = Room.Foyer, [Person.Huo] = Room.Hall, [Person.Shen] = Room.Pantry, [Person.Su] = Room.R05 };
var heard = morning.SoundTest(TestSentences.All[0], positions);
foreach (var h in heard) Line($"  {Names.Of(h.Who)} 在 {Names.Of(h.Where)}：{h.Text}{(h.SeemsFromOutside ? "（像在门外）" : "")}");
Check(heard.Select(h => h.Text).Distinct().Count() == 4, "同一句话在四个位置得到四种不同的感知");
Check(heard.First(h => h.Where == Room.Foyer) is { Hearing: Hearing.Tail, SeemsFromOutside: true, Text: "……开门" }, "门边只听到句尾重读的“……开门”，且像从门外来");
Check(heard.First(h => h.Where == Room.Hall).Hearing == Hearing.Full, "铜口旁听到整句");
Check(morning.WorldOf(Person.Detective).History.Last().Details == "……开门" && morning.WorldOf(Person.Huo).History.Last().Details == "闩上门，不要开门。",
    "各人的感知只写入各自的内部世界");
Check(morning.RealityFingerprint() == realityBefore, "声路试验不改动客观世界");

// ---------------------------------------------------------------- 4. 重演不改现实
Line("");
Line("== 重演不改动现实世界");
var mindsBeforeReplay = Enum.GetValues<Person>().ToDictionary(p => p, morning.MindFingerprint);
foreach (var v in all.Take(40)) NightReplay.Run(v);
Check(morning.RealityFingerprint() == realityBefore, "跑完 216+40 次重演后，清晨现实世界的证物与人物状态不变");
Check(Enum.GetValues<Person>().All(p => morning.MindFingerprint(p) == mindsBeforeReplay[p]), "跑完重演后，四人清晨的内部世界不变");

// ---------------------------------------------------------------- 5. 回忆只影响本人
Line("");
Line("== 回忆只影响本人");
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
Check(morning.RealityFingerprint() == realityBefore, "回忆不改动客观世界");

// ---------------------------------------------------------------- 6. 欲望放大与同伴是否触物回忆（引擎决定）
Line("");
Line("== 欲望放大（Corruption）与同伴触物回忆的决定");
RecallDecision Ask(MorningWorld world, string evidence, Person who, bool showClause8)
{
    if (showClause8) world.ShowClause8(who);
    world.Amplify(who, "（走近旧物时的一句真话）");
    return world.DecideRecall(evidence)!;
}
var told = new MorningWorld();
foreach (var who in Enum.GetValues<Person>()) told.Amplify(who, "（S4 核验报偿时的一句真话）");
Check(told.WorldOf(Person.Huo).History.Any(h => h.Source == ObservationSource.Corruption) && told.DesireStrength(Person.Huo) == 2,
    "S4 恶魔的话以 Corruption 来源写入霍铎认知，欲望加强");
// 三人倾向各不相同（设计 §5）：(物件, 人, 未告知时, 已告知时)
foreach (var (evidence, who, plainExpected, toldExpected) in new[]
{
    ("E7", Person.Huo, RecallChoice.Touch, RecallChoice.Touch),
    ("E5", Person.Su, RecallChoice.Touch, RecallChoice.Refuse),
    ("E10", Person.Shen, RecallChoice.Hesitate, RecallChoice.Refuse),
})
{
    var plain = new MorningWorld();
    plain.Amplify(who, "S4");
    var yes = Ask(plain, evidence, who, false);
    var shown = new MorningWorld();
    shown.Amplify(who, "S4");
    var othersBefore = Enum.GetValues<Person>().Where(p => p != who).ToDictionary(p => p, shown.MindFingerprint);
    var no = Ask(shown, evidence, who, true);
    Check(yes.Choice == plainExpected, $"{Names.Of(who)}：未被告知第 8 条时，DecisionSystem 选择 {yes.Choice}（{yes.Thought}）");
    Check(no.Choice == toldExpected, $"{Names.Of(who)}：被告知第 8 条后，选择 {no.Choice}（{no.Thought}）");
    Check(othersBefore.All(p => shown.MindFingerprint(p.Key) == p.Value), $"{Names.Of(who)}：指看第 8 条与恶魔的话只改变他本人的认知");
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
Check(MorningWorld.PostedClauses.Length == 8 && MorningWorld.PostedClauses.All(c => !c.Contains("归乡") && !c.Contains("放逐")),
    "公告框只抄录第 1—8 条，没有第 9、10 条");

Line("");
Line("== 真值组合的推演日志");
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
