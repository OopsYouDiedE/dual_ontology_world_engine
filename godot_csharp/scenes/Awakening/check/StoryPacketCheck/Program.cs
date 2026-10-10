using static DualOntologyWorldEngine.TestSupport.FixtureData;
using System.Text.Json;
using System.Text.Json.Nodes;
using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Story;

static void Check(bool ok, string name)
{
    if (!ok) throw new Exception(name);
    Console.WriteLine("PASS " + name);
}
static void Reject(Action action, string name)
{
    try { action(); }
    catch (InvalidOperationException) { Check(true, name); return; }
    throw new Exception(name + ": expected InvalidOperationException");
}
static InteractionPacket Menu(Chapter1Dialogue dialogue, InteractionPacket packet)
{
    for (var i = 0; i < 200; i++)
    {
        if (packet.Choices.Count > 0) return packet;
        Check(!packet.IsComplete, "interaction reaches a real story menu");
        Check(packet.WorldCommands.Count == 0, "menu traversal has no unexpected host command");
        packet = dialogue.Next();
    }
    throw new Exception("menu traversal exceeded budget");
}
static List<string> Finish(Chapter1Dialogue dialogue, InteractionPacket packet)
{
    var lines = new List<string>();
    for (var i = 0; i < 300; i++)
    {
        lines.AddRange(packet.Lines.Select(line => line.Text));
        if (packet.IsComplete) return lines;
        Check(packet.Choices.Count == 0, "finite entry has no unhandled choice");
        Check(packet.WorldCommands.Count == 0, "finite entry has no unacknowledged command");
        packet = dialogue.Next();
    }
    throw new Exception("entry exceeded budget");
}

var document = Chapter1CompiledDialogue.Create();
Check(document.Entries.Count > 0, "real generated dialogue is present");
var fileDocument = CompiledDialogueDocument.Parse(File.ReadAllText("godot_csharp/scenes/Awakening/Script/generated_awakening_dialogue.json"));
Check(JsonSerializer.Serialize(document) == JsonSerializer.Serialize(fileDocument), "compiled dialogue matches complete generated production document");

var chapter = new Chapter1Script();
// Changing every translated label must leave the selected compiled branch intact.
{
    var renamed = JsonNode.Parse(File.ReadAllText("godot_csharp/scenes/Awakening/Script/generated_awakening_dialogue.json"))!;
    void RenameLabels(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj.ContainsKey("label")) obj["label"] = "translated option";
            foreach (var value in obj.Select(pair => pair.Value).OfType<JsonNode>().ToArray()) RenameLabels(value);
        }
        else if (node is JsonArray array)
            foreach (var value in array.OfType<JsonNode>()) RenameLabels(value);
    }
    RenameLabels(renamed);
    var runtime = new CompiledDialogueRuntime(CompiledDialogueDocument.Parse(renamed.ToJsonString()), new()
    {
        EvaluateCall = (_, _) => false, ExecuteCall = (_, _) => { }, Interpolate = text => text,
    });
    runtime.Start("talk_huo_menu");
    var packet = runtime.Next();
    for (var i = 0; packet.Choices.Count == 0 && i < 200; i++) packet = runtime.Next();
    Check(packet.Choices.Single(choice => choice.Id == "talk_huo_menu:choice:1").Label == "translated option",
        "translation mutation reaches the same stable choice ID");
    runtime.Choose("talk_huo_menu:choice:1");
    Check(runtime.Next().Lines.Single().LineId == "talk_huo_menu.004",
        "translation mutation preserves the chosen response branch");
}
// Derived flow parameters must be refreshed when a continuation carries earlier values.
{
    var flowWorld = chapter.CreateWorld();
    var flowContent = chapter.CreateContent();
    var progress = flowContent.Session;
    progress.Set("answer_state", "step", 3);
    flowContent.StartFlow(flowWorld, "答辩·提问", (SampleText("story-packet-check", "text-001"), "1"));
    Check(flowContent.NextMeeting() == "answer_q3", "question provider replaces the carried step");

    progress.Set("answer_pick", "say", "me");
    progress.Set("answer_pick", "show", "E3");
    flowContent.StartFlow(flowWorld, "答辩·裁决", (SampleText("story-packet-check", "text-002"), "wrong_show"), (SampleText("story-packet-check", "text-001"), "1"), (SampleText("story-packet-check", "text-003"), "2"));
    Check(flowContent.NextMeeting() == "@sfx:answer_wrong:-3", "current wrong statement emits failure cue");
    flowContent.Presented();
    Check(flowContent.NextMeeting() == "answer_wrong_say", "verdict provider replaces the carried verdict");

    progress.Set("answer_pick", "say", "V4_0");
    progress.Set("answer_pick", "show", "E6");
    flowContent.StartFlow(flowWorld, "答辩·裁决", (SampleText("story-packet-check", "text-002"), "wrong_say"), (SampleText("story-packet-check", "text-001"), "1"), (SampleText("story-packet-check", "text-003"), "2"));
    Check(flowContent.NextMeeting() == "@sfx:answer_ok:-4", "corrected answer replaces the previous failure");
    flowContent.Presented();
    Check(flowContent.NextMeeting() == "answer_ok3", "correct answer uses the current question");
    flowContent.Presented();
    Check(flowContent.NextMeeting() == "answer_q4" && (int)progress.GetProgress("answer_state")["step"] == 4,
        "correct answer advances once to the next authored question");
}
var world = chapter.CreateWorld();
var content = chapter.CreateContent();
var dialogue = chapter.CreateDialogue(content, world);
var people = Enum.GetValues<Person>();
Check(people.All(person => world.DesireStrength(person) == 1), "opening starts with original desire strength");
var reward = dialogue.Start("s3_broadcast");
var rewardText = new List<string>();
var expectedStrength = people.ToDictionary(person => person, _ => 1);
// The effect belongs after the actual spoken cue; inspecting a packet is not an ack.
for (var i = 0; !reward.IsComplete && i < 300; i++)
{
    Check(reward.Choices.Count == 0 && reward.WorldCommands.Count == 0, "reward opening emits dialogue packets");
    rewardText.AddRange(reward.Lines.Select(line => line.Text));
    Check(people.All(person => world.DesireStrength(person) == expectedStrength[person]), "amplify executes at acknowledged script position");
    if (reward.Lines.Any(line => line.LineId == "s3_broadcast.021"))
    {
        foreach (var person in new[] { Person.Shen, Person.Huo, Person.Su }) expectedStrength[person]++;
    }
    if (reward.Lines.Any(line => line.LineId == "s4_rewards.017")) expectedStrength[Person.Detective]++;
    reward = dialogue.Next();
}
Check(reward.IsComplete && people.All(person => expectedStrength[person] == 2 && world.DesireStrength(person) == 2), "real reward script amplifies all four people exactly once");
Check(new[] { "s4_rewards.004", "s4_rewards.016", "s4_rewards.017", "s4_rewards.020", "s4_rewards.023" }
    .All(id => rewardText.Contains(document.Entries["s3_broadcast"].Single(step => step.LineId == id).Text)), "broadcast retains stable reward line IDs");
Check(dialogue.Next().IsComplete && people.All(person => world.DesireStrength(person) == 2), "completed interaction cannot repeat effects");

// The opening proceeds directly into investigation.
{
    var routeWorld = chapter.CreateWorld();
    var routeContent = chapter.CreateContent();
    var routeDialogue = chapter.CreateDialogue(routeContent, routeWorld);
    Finish(routeDialogue, routeDialogue.Start("s5_question"));
    Check(routeContent.Session.Has("flags", "accepted"), "opening accepts investigation without a rest choice");
}

// Nested if/else priorities come from the actual talk entry.
foreach (var branch in new[] { "first", "met", "replayed", "matched" })
{
    // 霍铎的招呼按他自己的认知分支：看见侦探重演、看见重演吻合（总纲 §六 第二期），而不是全局进度。
    if (branch == "replayed") world.Witness(Person.Huo, SampleText("story-packet-check", "text-004"));
    if (branch == "matched") world.Witness(Person.Huo, SampleText("story-packet-check", "text-005"));
    if (branch == "first") content.Session.Remove("flags", "met_huo");
    else content.Session.Set("flags", "met_huo", true);
    var talk = dialogue.Start("talk_huo");
    var expected = branch switch
    {
        "first" => SampleText("story-packet-check", "text-006"), "met" => SampleText("story-packet-check", "text-007"),
        "replayed" => SampleText("story-packet-check", "text-008"),
        _ => SampleText("story-packet-check", "text-009"),
    };
    Check(talk.Lines.Single().Text == expected, "real nested dialogue condition branch: " + branch);
    var talkMenu = Menu(dialogue, talk);
    Finish(dialogue, dialogue.Choose(talkMenu.Choices.Single(choice => choice.Id == "talk_huo_menu:choice:9").Id));
}
content.Session.Set("state", "full_match_seen", false);
content.Session.Set("state", "replay_count", 0);

content.Session.Set("flags", "price_heard", true);
var offer = Menu(dialogue, dialogue.Start("recall_offer_su"));
Check(offer.PresentationMode == PresentationMode.Menu && offer.Lines.Count > 0, "choice packet retains presented line");
Check(offer.Choices.Count == 2 && offer.Choices.All(choice => choice.IsAvailable), "real recall offer presents both available story options");
var sayNothing = offer.Choices.Single(choice => choice.Id == "recall_offer_su:choice:1");
Finish(dialogue, dialogue.Choose(sayNothing.Id));
Check(!content.Session.Has("flags", "show_rule"), "silent story choice leaves show-rule flag absent");
offer = Menu(dialogue, dialogue.Start("recall_offer_su"));
var showRule = offer.Choices.Single(choice => choice.Id != sayNothing.Id);
Finish(dialogue, dialogue.Choose(showRule.Id));
Check(content.Session.Has("flags", "show_rule"), "alternative story choice executes compiled effect");

var menu = Menu(dialogue, dialogue.Start("talk_huo_menu"));
var invite = menu.Choices.Single(choice => choice.Id == "talk_huo_menu:choice:0");
Check(!invite.IsAvailable, "unexamined evidence locks hand invitation");
Reject(() => dialogue.Choose(invite.Id), "runtime rejects locked choice");
Reject(() => dialogue.Choose("absent-choice"), "runtime rejects unknown choice");
var unchanged = dialogue.Next();
Check(unchanged.Choices.SequenceEqual(menu.Choices) && !content.Session.Has("flags", "invite_hand_huo"), "rejected choices preserve pending menu and state");
var pipe = unchanged.Choices.Single(choice => choice.Id == "talk_huo_menu:choice:1");
var answer = dialogue.Choose(pipe.Id);
Check(answer.Lines.Any(line => line.Text.Contains(SampleText("story-packet-check", "text-010"))), "available option runs actual response branch");
menu = Menu(dialogue, answer);
Check(menu.Choices.Any(choice => choice.Id == invite.Id && !choice.IsAvailable), "actual jump returns to persistent question menu");
content.Session.Set("hand_comparisons", "detective", true);
menu = dialogue.Next();
invite = menu.Choices.Single(choice => choice.Id == invite.Id);
Check(invite.IsAvailable, "pending menu reevaluates newly opened evidence gate");
Finish(dialogue, dialogue.Choose(invite.Id));
Check(content.Session.Has("flags", "invite_hand_huo"), "unlocked invitation executes actual branch");
Reject(() => dialogue.Choose(invite.Id), "choice cannot execute twice after completion");

var beforeWhisper = world.DesireStrength(Person.Huo);
Check(!document.Entries.Keys.Any(key => key.StartsWith("nudge_")), "manual nudge entries are absent");
content.Session.Set("examined", "E7", true);
menu = Menu(dialogue, dialogue.Start("talk_huo_menu"));
Finish(dialogue, dialogue.Choose("talk_huo_menu:choice:4"));
Check(content.Session.Has("flags", "invite_help_E7"), "authored person menu requests evidence assistance");
Check(dialogue.Next().IsComplete && world.DesireStrength(Person.Huo) == beforeWhisper, "completed invitation cannot replay effects");

world.ShowRule1(Person.Su);
var suRule = Finish(dialogue, dialogue.Start("recall_shown_su"));
Check(world.KnowsRule1(Person.Su) && !world.KnowsRule1(Person.Huo) && !world.KnowsRule1(Person.Shen), "actor rule cognition stays isolated");
Check(suRule.Any(text => text.Contains(SampleText("story-packet-check", "text-011"))), "real rule-known dialogue branch presents text");
var existingWorld = chapter.CreateDialogue(content, world);
var repeatedTalk = Finish(existingWorld, existingWorld.Start("s1_wake"));
Check(repeatedTalk.Count > 0 && world.DesireStrength(Person.Huo) == beforeWhisper, "new dialogue session retains supplied world without active frames");
var freshWorld = chapter.CreateWorld();
var freshContent = chapter.CreateContent();
var freshDialogue = chapter.CreateDialogue(freshContent, freshWorld);
var wake = freshDialogue.Start("s1_wake");
Check(wake.Lines.Single().Text == document.Entries["s1_wake"].First(step => step.Op == "line").Text, "new game session restarts actual first line");
Check(people.All(person => freshWorld.DesireStrength(person) == 1) && !freshWorld.Whispered(Person.Huo) && !freshWorld.KnowsRule1(Person.Su), "new game resets actor cognition and effects");
Check(!freshContent.Session.Has("flags", "show_rule") && !freshContent.Session.Has("flags", "invite_hand_huo") && !freshContent.Session.Has("nudged", "E7"), "new game resets chapter progress independently");
Finish(freshDialogue, wake);
Console.WriteLine("AUTO VALIDATION ONLY; NOT BLIND PLAYTEST");
// Independent production interaction routing/card/stale-choice regression.
{
    var real = new Chapter1Script();
    var c = real.CreateContent(); var w = real.CreateWorld();
    var field = new Chapter1Interaction(c, w);
    foreach (var route in c.Content.RootElement.GetProperty("SITE_INTERACTIONS").EnumerateObject())
    {
        var packet = field.Interact(route.Name);
        var kind = route.Value.GetProperty("kind").GetString();
        var expected = kind switch { "evidence" => "card", "person" => "talk", "board" => "field_board", "trace" => "trace", "tube" => "tube", _ => throw new Exception("unknown route") };
        Check(packet.WorldCommands.Count == 1 && packet.WorldCommands[0].Name == expected, "real field route " + route.Name);
        if (kind is "evidence" or "person" or "trace") Check(packet.WorldCommands[0].Arguments.Single()?.ToString() == route.Value.GetProperty("target").GetString(), "real field target " + route.Name);
    }
    Check(field.Interact("E8").WorldCommands.Single().Name == "tube", "main E8 opens tube menu");
    foreach (var joint in new[] { "E8j", "E8r" }) Check(field.Interact(joint).WorldCommands.Single().Name == "card", joint + " opens E8 evidence card");
    var card = field.CardPacket("E1", []);
    Check(card.PresentationMode == PresentationMode.EvidenceCard && card.Card?.Id == "E1" && card.Choices.Any(x => x.Id == "card:E1:close"), "real evidence card opens");
    Check(!card.Choices.Any(x => x.Id.EndsWith(":help")), "evidence cards do not offer assistance");
    var experiment = card.Choices.Single(x => x.Id == "card:E1:experiment");
    Check(experiment.IsAvailable, "initial real experiment available");
    c.Session.Set("experiments", "E1", true);
    var expired = field.Choose(experiment.Id);
    Check(expired.WorldCommands.Select(x => x.Name).SequenceEqual(new[] { "aside", "card" }), "expired choice returns lock hint and card without operation");
    try { field.Choose("card:E2:experiment"); throw new Exception("foreign choice accepted"); } catch (ArgumentException) { Check(true, "foreign card choice rejected without operation"); }
    var close = field.Choose("card:E1:close");
    Check(close.WorldCommands.Single().Name == "close", "invalid choices preserve active card");
    Reject(() => field.Choose(experiment.Id), "closed card rejects stale choice");
    field.CardPacket("E1", []); field.Interact("E8");
    Reject(() => field.Choose("card:E1:close"), "new field interaction invalidates old card");
    c.Session.Locate("detective", "R05"); c.Session.Locate("huo", "R05");
    var hand = field.CardPacket("E11", new[] { "detective", "huo" });
    Check(!hand.Choices.Any(x => x.Id == "card:E11:compare:huo"), "companion comparison follows the detective comparison");
    var valid = field.Choose("card:E11:compare:detective", new[] { "detective" });
    Check(valid.WorldCommands.Single().Name == "compare" && valid.WorldCommands.Single().Arguments.Single()?.ToString() == "detective", "available card emits actual compare command");
    c.Session.Set("hand_comparisons", "detective", true);
    hand = field.CardPacket("E11", new[] { "detective", "huo" });
    Check(hand.Choices.Single(x => x.Id == "card:E11:compare:huo").IsAvailable, "local geometric hand comparison opens");
    try { field.Choose("card:E11:compare:huo", new[] { "detective" }); throw new Exception("absent companion choice accepted"); }
    catch (ArgumentException) { Check(true, "geometry-expired comparison is no longer an offered choice"); }
    Check(!c.Session.Has("hand_comparisons", "huo"), "geometry-expired choice performs no comparison");
}
