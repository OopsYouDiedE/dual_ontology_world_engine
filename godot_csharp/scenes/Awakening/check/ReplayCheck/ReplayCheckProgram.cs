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
Check(results.All(r => r.Frames.Length > 3), "每次重演都产生可回放的帧");

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
