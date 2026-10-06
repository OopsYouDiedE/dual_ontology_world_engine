using DualOntologyWorldEngine.Awakening;

// 认知与言语的自动验证：同一场景下只微调某人的默认设置（初醒信任、说话习惯），
// 检查回应的变化方向与范围是否合乎设计（总纲 §六 第二期）。只用公开接口，不读真相。
static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }

var truth = new NightVariables(SuPlace.OwnRoom, HuoPlace.ByTube, VoiceOrigin.Tube, VoiceWords.BoltKeepShut,
    DetectiveHeeds.Huo, Acquaintance.Familiar);
var companions = new[] { "huo", "shen", "su" };
const string topic = "早已相熟";

(MorningWorld World, AwakeningContentSession Content) Fresh(IReadOnlyDictionary<string, double>? overrides = null)
{
    Persona.Overrides = overrides ?? new Dictionary<string, double>();
    var world = new MorningWorld();
    world.InitializePlayerPerception();
    var content = new AwakeningContentSession();
    foreach (var (who, room) in new[] { ("detective", "hall"), ("huo", "hall"), ("shen", "pantry"), ("su", "foyer") })
        content.Session.Locate(who, room);
    return (world, content);
}

Dictionary<string, (string Act, int Credence)> Answers(IReadOnlyDictionary<string, double>? overrides = null, bool shenSawCups = false, bool replay = true)
{
    var (world, content) = Fresh(overrides);
    if (shenSawCups) world.Examine(Person.Shen, "E9");
    if (replay) content.RunVisibleReplay(world, truth);
    return companions.ToDictionary(who => who, who => (content.SpeechKey(world, who, topic), content.Credence(world, who, topic)));
}

int Forthcoming(string key) => key switch
{
    "如实·有把握" or "如实·否认" or "如实·没把握" => 5, "含糊" => 4, "反问" => 3, "隐瞒" => 2, "拒绝" => 1, _ => 0,
};

// ---- 场景：重演之前与之后 ----
{
    var (world, content) = Fresh();
    Check(companions.All(who => !world.Knows(Names.PersonOf(who), "侦探重演吻合")), "before any replay no companion knows of a match");
    Check(content.Credence(world, "detective", topic) < 75, "before conceiving the sixth question the detective assumes strangers");
    content.RunVisibleReplay(world, truth);
    Check(content.Credence(world, "detective", topic) >= 75, "a matched replay, his own inference, convinces the detective");
    Check(companions.All(who => world.Knows(Names.PersonOf(who), "侦探重演吻合")), "companions summoned to the reversal witness the match");
}
var baseline = Answers();
foreach (var (who, answer) in baseline) Console.WriteLine($"  默认：{who} → {answer.Act}（把握 {answer.Credence}）");
Check(new[] { "shen", "su" }.All(who => baseline[who].Credence is > 50 and < 100), "testimony alone moves Shen and Su towards the claim without certainty");
Check(baseline["huo"].Credence == 100, "Huo, who has his own slate before him, is certain: its second line only fits long acquaintance");

// ---- 证据：亲眼看过杯子的人更有把握 ----
var cups = Answers(shenSawCups: true);
Check(cups["shen"].Credence > baseline["shen"].Credence, "the companion who examined the cups is surer than without");
Check(cups["huo"] == baseline["huo"] && cups["su"] == baseline["su"], "one person's examination does not change others' answers");

// ---- 在场即可见：不在场的人认知不变 ----
{
    var (world, content) = Fresh();
    content.Session.Locate("detective", "pantry");
    var su = world.MindFingerprint(Person.Su);
    var shen = world.MindFingerprint(Person.Shen);
    content.Examine(world, "E9");
    Check(world.MindFingerprint(Person.Su) == su, "an absent companion learns nothing from an examination");
    Check(world.MindFingerprint(Person.Shen) != shen, "a present companion sees what is examined");
}

// ---- 微调：只改苏泠对侦探的初醒信任 ----
var colder = Answers(new Dictionary<string, double> { ["苏泠.初醒信任.侦探"] = -3 });
Check(colder["huo"] == baseline["huo"] && colder["shen"] == baseline["shen"], "changing Su's trust leaves Huo's and Shen's answers unchanged");
Check(Forthcoming(colder["su"].Act) < Forthcoming(baseline["su"].Act), $"Su distrusting the detective is less forthcoming ({baseline["su"].Act} → {colder["su"].Act})");
Check(colder["su"].Credence <= baseline["su"].Credence, "distrust never raises how much Su believes the detective's replay");

// ---- 微调：信任单调；从不撒谎的人不撒谎 ----
foreach (var who in companions)
{
    var name = Names.Of(Names.PersonOf(who));
    var acts = Enumerable.Range(-3, 7).Select(t => Answers(new Dictionary<string, double> { [name + ".初醒信任.侦探"] = t })[who]).ToArray();
    Console.WriteLine($"  {name} 信任 -3…3：" + string.Join(" / ", acts.Select(a => $"{a.Act}({a.Credence})")));
    Check(acts.Zip(acts.Skip(1)).All(p => Forthcoming(p.First.Act) <= Forthcoming(p.Second.Act)), $"{name}: more trust is never less forthcoming");
    Check(acts.Zip(acts.Skip(1)).All(p => p.First.Credence <= p.Second.Credence), $"{name}: more trust never lowers belief in the detective's replay");
    Check(acts.All(a => !a.Act.StartsWith("撒谎")), $"{name}: an honest speaker never lies");
}

// ---- 微调：说话习惯只改本人的行为 ----
var reticent = Answers(new Dictionary<string, double> { ["沈鼎.说话习惯.寡言"] = 3, ["沈鼎.说话习惯.坦率"] = 0 });
Check(reticent["huo"] == baseline["huo"] && reticent["su"] == baseline["su"], "Shen's habits change only Shen");
Check(Forthcoming(reticent["shen"].Act) <= Forthcoming(baseline["shen"].Act), "a more reticent Shen is never more forthcoming");

// ---- 微调：把他们改成会撒谎、又不信侦探时，才可能说反话 ----
var liar = Answers(new Dictionary<string, double> { ["霍铎.说话习惯.会撒谎"] = 1, ["霍铎.初醒信任.侦探"] = -1 }, shenSawCups: true);
Console.WriteLine($"  会撒谎且不信侦探的霍铎 → {liar["huo"].Act}（把握 {liar["huo"].Credence}）");
Check(liar["shen"] == cups["shen"], "Huo's habits do not touch Shen");

Persona.Overrides = new Dictionary<string, double>();
Console.WriteLine("AUTO VALIDATION ONLY; NOT BLIND PLAYTEST");
