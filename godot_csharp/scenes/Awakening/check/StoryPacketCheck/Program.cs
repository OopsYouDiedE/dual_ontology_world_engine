using System.Text.Json;
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
var world = chapter.CreateWorld();
var content = chapter.CreateContent();
var dialogue = chapter.CreateDialogue(content, world);
var people = Enum.GetValues<Person>();
Check(people.All(person => world.DesireStrength(person) == 1), "opening starts with original desire strength");
var reward = dialogue.Start("s4_rewards");
var rewardText = new List<string>();
var expectedStrength = people.ToDictionary(person => person, _ => 1);
// The effect belongs after the actual spoken cue; inspecting a packet is not an ack.
foreach (var title in new[] { "s4_rewards", "s4_rewards_shen", "s4_rewards_huo", "s4_rewards_su", "s4_rewards_question", "s4_rewards_empty" })
{
    if (title != "s4_rewards") reward = dialogue.Start(title);
    for (var i = 0; !reward.IsComplete && i < 300; i++)
    {
        Check(reward.Choices.Count == 0 && reward.WorldCommands.Count == 0, "reward opening emits dialogue packets");
        rewardText.AddRange(reward.Lines.Select(line => line.Text));
        Check(people.All(person => world.DesireStrength(person) == expectedStrength[person]), "amplify executes at acknowledged script position");
        var text = string.Join("\n", reward.Lines.Select(line => line.Text));
        if (text.Contains("这一半只是开头，沈师傅")) expectedStrength[Person.Shen]++;
        if (text.Contains("一件试件，不过是一张图纸的一角")) expectedStrength[Person.Huo]++;
        if (text.Contains("那是旧声，苏小姐")) expectedStrength[Person.Su]++;
        if (text == "与你立约的，不是我。") expectedStrength[Person.Detective]++;
        reward = dialogue.Next();
    }
    Check(reward.IsComplete, "reward segment completes: " + title);
}
Check(reward.IsComplete && people.All(person => expectedStrength[person] == 2 && world.DesireStrength(person) == 2), "real reward script amplifies all four people exactly once");
Check(rewardText.Any(text => text.Contains("超越卡诺循环")) && rewardText.Any(text => text.Contains("揣进风衣口袋")), "real opening retains rewards and detective thought");
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
    if (branch == "replayed") world.Witness(Person.Huo, "侦探在重演昨夜");
    if (branch == "matched") world.Witness(Person.Huo, "侦探重演吻合");
    if (branch == "first") content.Session.Remove("flags", "met_huo");
    else content.Session.Set("flags", "met_huo", true);
    var talk = dialogue.Start("talk_huo");
    var expected = branch switch
    {
        "first" => "……什么事。", "met" => "嗯。",
        "replayed" => "你在推昨晚。推出什么，跟我说。",
        _ => "你推完了。……我在这儿守了很久，是不是。",
    };
    Check(talk.Lines.Single().Text == expected, "real nested dialogue condition branch: " + branch);
    var talkMenu = Menu(dialogue, talk);
    Finish(dialogue, dialogue.Choose(talkMenu.Choices.Single(choice => choice.Label == "没事了。").Id));
}
content.Session.Set("state", "full_match_seen", false);
content.Session.Set("state", "replay_count", 0);

var offer = Menu(dialogue, dialogue.Start("recall_offer_su"));
Check(offer.PresentationMode == PresentationMode.Menu && offer.Lines.Count > 0, "choice packet retains presented line");
Check(offer.Choices.Count == 2 && offer.Choices.All(choice => choice.IsAvailable), "real recall offer presents both available story options");
var sayNothing = offer.Choices.Single(choice => choice.Label == "什么都不说。");
Finish(dialogue, dialogue.Choose(sayNothing.Id));
Check(!content.Session.Has("flags", "show_rule"), "silent story choice leaves show-rule flag absent");
offer = Menu(dialogue, dialogue.Start("recall_offer_su"));
var showRule = offer.Choices.Single(choice => choice.Label.Contains("先问问铜口"));
Finish(dialogue, dialogue.Choose(showRule.Id));
Check(content.Session.Has("flags", "show_rule"), "alternative story choice executes compiled effect");

var menu = Menu(dialogue, dialogue.Start("talk_huo_menu"));
var invite = menu.Choices.Single(choice => choice.Label.Contains("单独比一下手掌轮廓"));
Check(!invite.IsAvailable, "unexamined evidence locks hand invitation");
Reject(() => dialogue.Choose(invite.Id), "runtime rejects locked choice");
Reject(() => dialogue.Choose("absent-choice"), "runtime rejects unknown choice");
var unchanged = dialogue.Next();
Check(unchanged.Choices.SequenceEqual(menu.Choices) && !content.Session.Has("flags", "invite_hand_huo"), "rejected choices preserve pending menu and state");
var pipe = unchanged.Choices.Single(choice => choice.Label == "这个铜口是什么？");
var answer = dialogue.Choose(pipe.Id);
Check(answer.Lines.Any(line => line.Text.Contains("传声口")), "available option runs actual response branch");
menu = Menu(dialogue, answer);
Check(menu.Choices.Any(choice => choice.Id == invite.Id && !choice.IsAvailable), "actual jump returns to persistent question menu");
content.Session.Set("examined", "E11", true);
menu = dialogue.Next();
invite = menu.Choices.Single(choice => choice.Id == invite.Id);
Check(invite.IsAvailable, "pending menu reevaluates newly opened evidence gate");
Finish(dialogue, dialogue.Choose(invite.Id));
Check(content.Session.Has("flags", "invite_hand_huo"), "unlocked invitation executes actual branch");
Reject(() => dialogue.Choose(invite.Id), "choice cannot execute twice after completion");

// Next is the acknowledgement API: command delivery pauses before following completion.
var beforeWhisper = world.DesireStrength(Person.Huo);
var nudge = dialogue.Start("nudge_E7");
Check(nudge.Lines.Any(line => line.Text.Contains("卡诺循环")) && world.DesireStrength(Person.Huo) == beforeWhisper, "nudge line precedes effect until ack");
Reject(() => dialogue.Start("s1_wake"), "active session rejects overlapping start");
var command = dialogue.Next();
Check(command.PresentationMode == PresentationMode.World && command.WorldCommands.Count == 1 && !command.IsComplete, "whisper yields command and awaits next ack");
Check(command.WorldCommands[0].Name == "consider" && command.WorldCommands[0].Arguments.SequenceEqual(new object?[] { "huo" }), "real command retains typed actor argument");
Check(world.DesireStrength(Person.Huo) == beforeWhisper + 1 && world.Whispered(Person.Huo) && content.Session.Has("nudged", "E7"), "command packet follows real world and chapter effects");
Check(!world.Whispered(Person.Shen) && !world.Whispered(Person.Su), "private whisper does not leak into other actor cognition");
var ack = dialogue.Next();
Check(ack.IsComplete && ack.WorldCommands.Count == 0, "command ack resumes to completion without redelivery");
Check(dialogue.Next().WorldCommands.Count == 0 && world.DesireStrength(Person.Huo) == beforeWhisper + 1, "repeated completion ack cannot replay whisper");

world.ShowRule1(Person.Su);
var suRule = Finish(dialogue, dialogue.Start("recall_shown_su"));
Check(world.KnowsRule1(Person.Su) && !world.KnowsRule1(Person.Huo) && !world.KnowsRule1(Person.Shen), "actor rule cognition stays isolated");
Check(suRule.Any(text => text.Contains("连着的此地")), "real rule-known dialogue branch presents text");
var existingWorld = chapter.CreateDialogue(content, world);
var repeatedTalk = Finish(existingWorld, existingWorld.Start("s1_wake"));
Check(repeatedTalk.Count > 0 && world.DesireStrength(Person.Huo) == beforeWhisper + 1, "new dialogue session retains supplied world without active frames");
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
    Check(hand.Choices.Single(x => x.Id == "card:E11:compare:huo").IsAvailable, "local geometric hand comparison opens");
    var gone = field.Choose("card:E11:compare:huo", new[] { "detective" });
    Check(gone.WorldCommands.Select(x => x.Name).SequenceEqual(new[] { "aside", "card" }) && !c.Session.Has("hand_comparisons", "huo"), "geometry-expired choice performs no comparison");
    var valid = field.Choose("card:E11:compare:detective", new[] { "detective" });
    Check(valid.WorldCommands.Single().Name == "compare" && valid.WorldCommands.Single().Arguments.Single()?.ToString() == "detective", "available card emits actual compare command");
}
