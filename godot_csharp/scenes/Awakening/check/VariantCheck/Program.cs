using System.Text.Json;
using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Story;

// 第五期检查（总纲 §六）：开局人选与开场独白、隐藏心智抽样、心愿、寝区案件变体与求解器。
static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }

var root = Chapter1Data.Root;
var document = Chapter1CompiledDialogue.Create();

// ---- 开局人选：十六人各有开场，侦探可选，其余待验收 ----
var openings = root.GetProperty("OPENINGS").EnumerateArray().ToArray();
Check(openings.Length == 16, "sixteen people can be chosen at the start (pending acceptance except the detective)");
Check(openings.All(o => document.Entries.ContainsKey(o.GetProperty("scene").GetString()!)), "every opening monologue exists as a compiled scene");
Check(openings.Count(o => o.GetProperty("selectable").GetBoolean()) == 1 && openings.Single(o => o.GetProperty("selectable").GetBoolean()).GetProperty("person").GetString() == "侦探",
    "only the detective is selectable until the others pass acceptance");
Check(openings.Where(o => o.GetProperty("person").GetString() != "侦探").All(o =>
    document.Entries[o.GetProperty("scene").GetString()!].Any(i => i.Op == "line" && i.Text.Contains("只记得上船"))), "every opening keeps the shared skeleton: only the boarding is remembered");

// ---- 隐藏心智抽样 ----
Check(Persona.Sample(0).Count == 0, "seed 0 is the first playthrough: the author's defaults");
var ranges = root.GetProperty("MIND_RANGES");
var samples = Enumerable.Range(1, 50).Select(Persona.Sample).ToArray();
Check(samples.All(s => s.All(p =>
{
    var dot = p.Key.IndexOf('.');
    var range = ranges.GetProperty(p.Key[..dot]).GetProperty(p.Key[(dot + 1)..]);
    return p.Value >= range[0].GetDouble() && p.Value <= range[1].GetDouble();
})), "every sampled value stays inside its declared range");
Check(Persona.Sample(7).OrderBy(p => p.Key).SequenceEqual(Persona.Sample(7).OrderBy(p => p.Key)), "sampling is deterministic per seed");
Check(samples.Select(s => string.Join(",", s.OrderBy(p => p.Key).Select(p => p.Value))).Distinct().Count() > 10, "different seeds give different minds");
Persona.Overrides = Persona.Sample(3);
var sampledWorld = new MorningWorld();
var suTrust = sampledWorld.TrustIn(Person.Su, Person.Detective);
Check(suTrust is >= -1 and <= 1 && suTrust == (int)Persona.Sample(3)["苏泠.初醒信任.侦探"], "a sampled mind reaches the person's own cognition");
Persona.Overrides = new Dictionary<string, double>();

// ---- 心愿：欲望经方法库分解，按本人的本事标出须找人的一步 ----
var huo = Wishes.For("霍铎");
foreach (var line in huo) Console.WriteLine("  " + line);
Check(huo.Any(l => l.Contains("取回封存匣")) && huo.Any(l => l.Contains("打开名录库")), "Huo's wish decomposes into opening the archive");
Check(huo.Any(l => l.Contains("信号：须找会的人")), "Huo cannot relay the signal himself and must find someone");
var shen = Wishes.For("沈鼎");
Check(shen.Any(l => l.Contains("统一煮粥") && l.Contains("厨艺：自己够")), "Shen can cook for everyone himself");
Check(Wishes.For("不存在的人").Count == 0, "no wishes for an unknown person");

// ---- 寝区案件：作者版本的指标与变体 ----
foreach (var item in root.GetProperty("DORM_CASES").EnumerateObject())
{
    var authored = CaseVariants.Solve(item.Value);
    Console.WriteLine($"  {item.Name} 作者版本：唯一 {authored.UniqueAnswer}，另一问在区内无解 {authored.OtherOpen}，每条都有用 {authored.EveryTraceUsed}，单件定案 {authored.SingleDecisive}，最短推理 {authored.MinPath}");
    Check(authored.UniqueAnswer && authored.OtherOpen && authored.EveryTraceUsed, $"{item.Name}: the authored case is solvable inside the dorm with its inference left open");
    var variants = Enumerable.Range(1, 200).Select(seed => CaseVariants.Variant(item.Name, item.Value, seed)).ToArray();
    Check(variants.All(v => CaseVariants.Solve(v).Acceptable), $"{item.Name}: every generated variant passes the solver");
    Check(variants.All(v => v.GetProperty("truth").EnumerateArray().Select(t => t.GetInt32()).SequenceEqual(item.Value.GetProperty("truth").EnumerateArray().Select(t => t.GetInt32()))), $"{item.Name}: variants keep the truth later chapters depend on");
    var orders = variants.Select(v => string.Join("|", v.GetProperty("traces").EnumerateArray().Select(t => t.GetProperty("key").GetString() + t.GetProperty("needs").GetInt32()))).Distinct().Count();
    Check(orders > 20, $"{item.Name}: variants differ in which traces appear and when ({orders} distinct)");
}
Console.WriteLine("AUTO VALIDATION ONLY; NOT BLIND PLAYTEST");
