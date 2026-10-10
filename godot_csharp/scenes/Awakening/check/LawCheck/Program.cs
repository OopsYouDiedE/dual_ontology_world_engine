using static DualOntologyWorldEngine.TestSupport.FixtureData;
using System.Text.Json;
using DualOntologyWorldEngine.Awakening;

// 法则化检查（总纲 §六 第三期）：声路由法则推出、随现实接头改变而昨夜不变；证物毁去后由代管者补救；未建模给保守结果。
static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }

var truth = SampleAs<NightVariables>("law-check", "golden-variables");
var acoustics = Chapter1Data.Root.GetProperty("ACOUSTICS").GetProperty("hearing");

// ---- 声学法则与编译出的声路表一致 ----
foreach (var origin in Enum.GetValues<VoiceOrigin>())
    foreach (var room in Enum.GetValues<Room>())
    {
        var rows = acoustics.GetProperty(origin.ToString());
        var expected = rows.TryGetProperty(Names.RoomKeys[(int)room], out var row) ? row.GetString() : rows.GetProperty("*").GetString();
        Check(Acoustics.Hear(origin, room).ToString() == expected, $"law equals table: {origin} at {room}");
    }

// ---- 拧紧接头：此后的声路试验随之改变，昨夜的痕迹不变 ----
{
    var world = new MorningWorld();
    world.InitializePlayerPerception();
    var before = JsonSerializer.Serialize(NightReplay.Run(truth).Traces);
    var positions = new Dictionary<Person, Room> { [Person.Detective] = Room.R05, [Person.Huo] = Room.Hall, [Person.Shen] = Room.Pantry, [Person.Su] = Room.R08 };
    Check(world.SoundTest(TestSentences.All[0], positions).Single(h => h.Who == Person.Su).Hearing == Hearing.Tail, "a loose wall joint lets Su hear the stressed tail");
    Check(world.SetJoint(SampleText("law-check", "text-001"), false), "the wall joint can be tightened");
    Check(world.SoundTest(TestSentences.All[0], positions).Single(h => h.Who == Person.Su).Hearing == Hearing.AlarmOnly, "after tightening, R-08 hears nothing through the pipe");
    Check(Acoustics.Hear(VoiceOrigin.OutsideDoor, Room.R08, world.Joints) == Hearing.Nothing, "an outside voice no longer leaks into R-08");
    Check(JsonSerializer.Serialize(NightReplay.Run(truth).Traces) == before, "last night's traces keep last night's joints");
    world.SetJoint(SampleText("law-check", "text-002"), false);
    var content = new AwakeningContentSession();
    content.Session.Locate("detective", "hall");
    content.Examine(world, "E8");
    var tube = world.WorldOf(Person.Detective).Projections.First(p => p.Tags.Keys.Any(t => t.Name == SampleText("law-check", "text-003")));
    Check(tube.Tags.First(t => t.Key.Name == SampleText("law-check", "text-003")).Value == 0, "with both joints tight the tube card shows no loose joint");
}

// ---- 证物毁去：补救与保守结果 ----
{
    var world = new MorningWorld();
    var content = new AwakeningContentSession();
    Check(content.Judge(1, "me", "") == "answer_wrong_show", "a right statement without support is still wrong while evidence exists");
    Check(content.DestroyEvidence(world, "E3") && content.DestroyEvidence(world, "E7"), "evidence can be destroyed");
    Check(content.Judge(1, "me", "") == "answer_wrong_show", "while one supporting item (E12) remains, no rescue");
    content.DestroyEvidence(world, "E12");
    Check(content.Judge(1, "me", "") == "answer_rescue", "with every supporting item gone the steward rescues a right statement");
    Check(content.Session.GetProgress("rescue").ContainsKey("1"), "the rescue records its reason");
    Check(content.Judge(1, "V3_0", "") == "answer_wrong_say", "rescue never accepts a wrong statement");
    var before = world.MindFingerprint(Person.Detective);
    content.Session.Locate("detective", "foyer");
    content.Examine(world, "E3");
    Check(world.MindFingerprint(Person.Detective) == before, "a destroyed item cannot be examined");
    Check(!content.DestroyEvidence(world, SampleText("law-check", "text-004")) && content.Session.GetProgress("unmodeled").Count == 1, "an unmodelled target is reported and nothing changes");
    Check(!content.SetJoint(world, SampleText("law-check", "text-005"), true) && content.Session.GetProgress("unmodeled").Count == 2, "an unknown joint is reported and nothing changes");
}
Console.WriteLine("AUTO VALIDATION ONLY; NOT BLIND PLAYTEST");
