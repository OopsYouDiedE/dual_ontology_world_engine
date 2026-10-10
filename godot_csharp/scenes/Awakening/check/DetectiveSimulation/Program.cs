using static DualOntologyWorldEngine.TestSupport.FixtureData;
using System.Text.Json;
using DualOntologyWorldEngine.Awakening;

// Logical host: arrival is reported through the same public boundary as Godot.
// This generates a simulation trajectory, not a puzzle solution or a geometric playtest.
if (args.Length > 0 && args[0] == "--render-json")
{
    if (args.Length is < 2 or > 3)
        throw new ArgumentException(SampleText("detective-simulation", "text-001"));
    var source = Path.GetFullPath(args[1]);
    var target = args.Length == 3 ? Path.GetFullPath(args[2]) : Path.ChangeExtension(source, ".txt");
    if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException(SampleText("detective-simulation", "text-002"));
    using var document = JsonDocument.Parse(File.ReadAllText(source));
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.WriteAllText(target, TrajectoryText.Render(document.RootElement));
    Console.WriteLine($"已转写轨迹：{target}");
    return;
}
if (!args.Contains("--enabled", StringComparer.Ordinal))
    throw new ArgumentException(SampleText("detective-simulation", "text-003"));
var decisionProfile = "default";
string? durationArgument = null;
Random? chaos = null;
string? playAs = null;
int mindSeed = 0;
var persona = new Dictionary<string, double>(StringComparer.Ordinal);
var outputArguments = new List<string>();
for (var index = 0; index < args.Length; index++)
{
    if (args[index] == "--enabled") continue;
    if (args[index] == "--decision-profile")
    {
        if (++index >= args.Length || args[index].StartsWith("--",StringComparison.Ordinal))
            throw new ArgumentException(SampleText("detective-simulation", "text-004"));
        decisionProfile = args[index];
    }
    else if (args[index] == "--persona")
    {
        // 自动验证用：微调某人的默认设置，如 --persona 苏泠.初醒信任.侦探=-2（总纲 §六 第二期）。
        if (++index >= args.Length || !args[index].Contains('=')) throw new ArgumentException(SampleText("detective-simulation", "text-005"));
        var (key, value) = (args[index][..args[index].IndexOf('=')], args[index][(args[index].IndexOf('=') + 1)..]);
        persona[key] = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }
    else if (args[index] == "--play-as")
    {
        // 开局任选一人（总纲 §六 第五期）：记录控制者、播其开场独白，结束时给出此人的认知与心愿；不改他人的行为。
        if (++index >= args.Length) throw new ArgumentException(SampleText("detective-simulation", "text-006"));
        playAs = args[index];
    }
    else if (args[index] == "--mind-seed")
    {
        if (++index >= args.Length || !int.TryParse(args[index], out mindSeed)) throw new ArgumentException(SampleText("detective-simulation", "text-007"));
    }
    else if (args[index] == "--case-seed")
    {
        if (++index >= args.Length || !int.TryParse(args[index], out var caseSeed)) throw new ArgumentException(SampleText("detective-simulation", "text-008"));
        CaseVariants.Seed = caseSeed;
    }
    else if (args[index] == "--chaos")
    {
        // 混沌测试：侦探的每一步改由随机代理在公开协议上乱做（含毁物、拧接头、乱重演、乱作答），检查不卡死（总纲 §六 第三期）。
        if (++index >= args.Length || !int.TryParse(args[index], out var chaosSeed)) throw new ArgumentException(SampleText("detective-simulation", "text-009"));
        chaos = new Random(chaosSeed);
    }
    else if (args[index] == "--duration")
    {
        if (++index >= args.Length || args[index].StartsWith("--",StringComparison.Ordinal))
            throw new ArgumentException(SampleText("detective-simulation", "text-010"));
        durationArgument = args[index];
    }
    else if (args[index].StartsWith("--",StringComparison.Ordinal))
        throw new ArgumentException(SampleText("detective-simulation", "text-011") + args[index]);
    else outputArguments.Add(args[index]);
}
if (outputArguments.Count > 1) throw new ArgumentException(SampleText("detective-simulation", "text-012"));
var outputArgument = outputArguments.FirstOrDefault();
var output = outputArgument is not null ? Path.GetFullPath(outputArgument) : Path.GetFullPath("artifacts/chapter-one-ecology/unified-game-trajectory.txt");
if (string.Equals(Path.GetExtension(output), ".json", StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException(SampleText("detective-simulation", "text-013"));
var sampled = Persona.Sample(mindSeed);
Persona.Overrides = sampled.Concat(persona).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Last().Value);
var world = new MorningWorld();
world.InitializePlayerPerception();
var content = new AwakeningContentSession();
if (!content.Content.RootElement.GetProperty("DETECTIVE_DIALOGUE_CHOICES").EnumerateObject()
    .Any(rule => rule.Value.GetProperty("profile").GetString() == decisionProfile))
    throw new ArgumentException(SampleText("detective-simulation", "text-014") + decisionProfile);
using var layoutStream = typeof(MorningWorld).Assembly.GetManifestResourceStream("Awakening.Layout.json")
    ?? throw new InvalidOperationException("Missing public host layout.");
using var hostLayout = JsonDocument.Parse(layoutStream);
// 运行时长：外部参数优先，否则读入口文件末尾的“运行”；两者都没有时不启动，避免默认跑出无意的长度。
var root = content.Content.RootElement;
var startMinutes = root.GetProperty("DAY_CLOCK").GetProperty("start_minutes").GetInt32();
double? stopMinutes;
string runSource;
if (durationArgument is not null) { stopMinutes = startMinutes + RunDuration.Parse(durationArgument); runSource = SampleText("detective-simulation", "text-015") + durationArgument; }
else if (root.TryGetProperty("RUN", out var run))
{
    var declared = run.GetProperty("duration_minutes");
    stopMinutes = declared.ValueKind == JsonValueKind.Null ? null : startMinutes + declared.GetInt32();
    runSource = SampleText("detective-simulation", "text-016");
}
else throw new ArgumentException(SampleText("detective-simulation", "text-017"));
if (stopMinutes is not null && double.IsNaN(stopMinutes.Value)) stopMinutes = null;
bool Expired() => stopMinutes is { } stop && content.GameMinutes >= stop;
var dialogue = new Chapter1Dialogue(content, world);
var mind = world.WorldOf(Person.Detective);
var planner = new DetectivePlanner(content.Content.RootElement);
var searches = new List<object>();
var timing = new List<object>();
var randomChoices = new Random(content.Content.RootElement.GetProperty("DETECTIVE_SEARCH").GetProperty("seed").GetInt32());
var controls = new List<object>();
var arrivals = new List<Arrival>();
var arrivedPlans = new HashSet<int>();
var knownPlans = new HashSet<int>();
var plans = new List<DailyPlan>();
var storyEvents = new List<StoryEvent>();
var decisions = new List<object>();
var events = new List<object>();
int capturedDecisions = 0, capturedEvents = 0;
double time = 0;
string? previousStage = null;
string currentRoom = "R05";
bool awaitingInput = false;
NightVariables? answerPrediction = null;
bool answerSubmitted = false;
int? exitGameMinutes = null;

void LocateStaged(string who, string spot)
    => LocatePublicPoint(who,hostLayout.RootElement.GetProperty("staging").GetProperty(spot),spot);
void LocatePublicPoint(string who, JsonElement position, string spot)
{
    var x = position[0].GetDouble(); var y = position[1].GetDouble();
    var region = content.Content.RootElement.GetProperty("ROOM_REGIONS").EnumerateArray().FirstOrDefault(r =>
    {
        var rect = r.GetProperty("rect");
        return (rect[0].ValueKind == JsonValueKind.Null || x >= rect[0].GetDouble())
            && (rect[1].ValueKind == JsonValueKind.Null || y >= rect[1].GetDouble())
            && (rect[2].ValueKind == JsonValueKind.Null || x < rect[2].GetDouble())
            && (rect[3].ValueKind == JsonValueKind.Null || y < rect[3].GetDouble());
    });
    if (region.ValueKind == JsonValueKind.Undefined) throw new InvalidDataException("Public staging point has no room: " + spot);
    var name = region.GetProperty("name").GetString();
    var roomKey = content.Content.RootElement.GetProperty("ROOM_NAMES").EnumerateObject().FirstOrDefault(r => r.Value.GetString() == name).Name;
    var room = Array.IndexOf(Names.RoomKeys,roomKey); var actor = Array.IndexOf(Names.PersonKeys,who);
    if (room < 0 || actor < 0) throw new InvalidDataException("Public staging point has no logical binding: " + spot);
    world.DailySuspend(who); world.Locate((Person)actor,(Room)room); content.Session.Locate(who,roomKey);
    if (who == "detective") { currentRoom = roomKey; content.RoomTrigger(roomKey); content.RoomEntry(roomKey); }
    Story(SampleText("detective-simulation", "text-018"), $"公开剧情站位到场代报：{who}；{spot}（{x},{y}）；{roomKey}；未验几何");
}

string Stage() => (string)content.Session.GetProgress("state")["stage"];
void Story(string kind, string text)
{
    storyEvents.Add(new(time, content.GameMinutes, kind, text));
    if (SampleStrings("detective-simulation", "visible-record-kinds").Contains(kind))
        planner.RecordVisibleText(text,$"host:{kind}; game_minutes={content.GameMinutes}; logical_seconds={time}");
}
void SyncStage()
{
    var stage = Stage();
    world.SetDailyStage(stage);
    world.SetMorningGameMinutes(content.GameMinutes);
    if (previousStage == stage) return;
    Story(SampleText("detective-simulation", "text-024"), stage);
    previousStage = stage;
}
void Tick(bool active)
{
    time += 0.25;
    foreach (var entry in content.TakeTiming()) timing.Add(new { Time = time, Timing = entry });
    SyncStage();
    var pending = world.DailyTick(0.25, active, Array.Empty<string>());
    // The ecology clock pauses during presentation. Rebase new records onto the
    // session clock so story and routine events retain their actual ordering.
    foreach (var item in world.DailyDecisions.Skip(capturedDecisions))
        decisions.Add(new { Time = time, item.GameMinutes, item.Person, item.Control, item.Serial, item.Room, item.Action, item.Score, item.Reasons, item.Resumed });
    foreach (var item in world.DailyTrajectory.Skip(capturedEvents))
        events.Add(new { Time = time, item.GameMinutes, item.Person, item.Room, item.Action, item.Text });
    capturedDecisions = world.DailyDecisions.Count;
    capturedEvents = world.DailyTrajectory.Count;
    if (!active) return;
    foreach (var plan in pending)
    {
        if (knownPlans.Add(plan.Serial)) plans.Add(plan);
        var who = Array.IndexOf(Names.PersonKeys, plan.Who);
        var room = Array.IndexOf(Names.RoomKeys, plan.Room);
        if (room < 0) throw new InvalidOperationException("Hosted plan has no room binding.");
        // The public DailyArrive boundary locates the twelve unhosted residents in
        // their own world. Only the four hosted bodies use the Person enum binding.
        if (who >= 0) world.Locate((Person)who, (Room)room);
        content.Session.Locate(plan.Who, plan.Room);
        if (arrivedPlans.Add(plan.Serial))
            arrivals.Add(new Arrival(time, plan.Who, plan.Serial, plan.Room,
                world.DailyArrive(plan.Who, plan.Serial, plan.Room)));
        if (who != (int)Person.Detective) continue;
        var trigger = content.RoomTrigger(plan.Room);
        SyncStage();
        if (trigger == "meeting")
        {
            // The full host stages the three companions in this same room.
            foreach (var companion in new[] { Person.Huo, Person.Shen, Person.Su })
            {
                world.Locate(companion, (Room)room);
                content.Session.Locate(Names.PersonKeys[(int)companion], plan.Room);
            }
            Story(SampleText("detective-simulation", "text-018"), SampleText("detective-simulation", "text-025"));
            PlayFlow(() => content.BeginMeeting(world));
            break; // Presentation suspends pending daily plans.
        }
        if (trigger == "room" && currentRoom != plan.Room)
        {
            currentRoom = plan.Room;
            PlayFlow(() => content.BeginRoomChanged(world, plan.Room, (long)(time * 1000)));
            content.RoomEntry(plan.Room);
            break;
        }
    }
}
void Command(string name, IReadOnlyList<object?> arguments)
{
    Story(SampleText("detective-simulation", "text-026"), name + (arguments.Count == 0 ? "" : "：" + string.Join("；", arguments)));
    if (name == "name")
        Story(SampleText("detective-simulation", "text-027"), SampleText("detective-simulation", "text-028") + content.Session.GetProgress("state")["detective_name"]);
    else if (name is "board" or "field_board")
    {
        // Same host-side read acknowledgment and public clauses as open_board.
        content.Session.Set("flags", "board_read", true);
        Story(SampleText("detective-simulation", "text-021"), Chapter1Data.Text(SampleText("detective-simulation", "text-029")));
        Story(SampleText("detective-simulation", "text-021"), Chapter1Data.Text(SampleText("detective-simulation", "text-030")));
        foreach (var clause in MorningWorld.PostedRules) Story(SampleText("detective-simulation", "text-021"), clause);
    }
    else if (name == "aside") Story(SampleText("detective-simulation", "text-020"), string.Join("：", arguments));
    else if (name == "stage_actor" && arguments.Count > 1)
        LocateStaged(Convert.ToString(arguments[0])!,Convert.ToString(arguments[1])!);
    else if (name == "finale_prepare") LocateStaged("detective",SampleText("detective-simulation", "text-031"));
    else if (name == "posts")
    {
        foreach (var post in hostLayout.RootElement.GetProperty("posts").EnumerateObject())
            LocatePublicPoint(post.Name,post.Value,SampleText("detective-simulation", "text-032")+post.Name);
    }
    else if (name == "summon" && arguments.Count > 0)
    {
        var who = Convert.ToString(arguments[0])!;
        var person = Array.IndexOf(Names.PersonKeys, who);
        var room = Array.IndexOf(Names.RoomKeys,currentRoom);
        if (person >= 0 && room >= 0) { world.DailySuspend(who); world.Locate((Person)person,(Room)room); content.Session.Locate(who,currentRoom); }
        Story(SampleText("detective-simulation", "text-018"), SampleText("detective-simulation", "text-033") + who);
    }
    else if (name == "card" && arguments.Count > 0)
    {
        var id = Convert.ToString(arguments[0])!;
        Story(SampleText("detective-simulation", "text-022"), content.CardFacts(id));
        foreach (var note in content.CardNotes(world,id)) Story(SampleText("detective-simulation", "text-022"),note);
    }
    else if (name == "sound")
    {
        var sentences = TestSentences.All;
        var sentence = randomChoices.Next(sentences.Length);
        var spots = content.Content.RootElement.GetProperty("SOUND_SPOTS").EnumerateObject().Select(s => s.Name).ToArray();
        var spot = spots[randomChoices.Next(spots.Length)];
        Story(SampleText("detective-simulation", "text-027"), SampleText("detective-simulation", "text-034") + sentences[sentence].Full + SampleText("detective-simulation", "text-035") + spot);
        var positions = content.SoundPositions(spot).ToDictionary(p => (Person)Array.IndexOf(Names.PersonKeys,p.Key), p => (Room)Array.IndexOf(Names.RoomKeys,p.Value));
        var heard = world.SoundTest(sentences[sentence],positions);
        content.RecordSoundTest();
        content.Session.Set("state","last_sound",heard.Select(h => (object)new Dictionary<string,object>
            { ["who"] = Names.PersonKeys[(int)h.Who], ["room"] = Names.RoomKeys[(int)h.Where], ["text"] = h.Text, ["direction"] = h.Direction }).ToArray());
        content.Session.Set("state","sound_sentence",sentence);
        var rounds = Convert.ToInt32(content.Session.GetProgress("state")["sound_rounds"])+1;
        content.Session.Set("state","sound_rounds",rounds);
        content.Session.Set("experiments","E8",true);
        if (rounds == 1) PlayDialogue("sound_test");
        foreach (var perception in heard) Story(SampleText("detective-simulation", "text-022"),Names.Of(perception.Who)+"："+perception.Text+"；"+perception.Direction);
    }
    else if (name == "answerpick")
    {
        var step = Convert.ToInt32(content.Session.GetProgress("answer_state")["step"]);
        var argument = answerPrediction is { } prediction ? planner.Argument(step,content,prediction) : null;
        if (chaos is not null)
        {
            // 乱作答：有时什么也不交（暂且不答），有时随便配一句陈述与一件证物。
            var statements = content.Content.RootElement.GetProperty("STATEMENTS").EnumerateObject().Select(s => s.Name).ToArray();
            var exhibits = content.Content.RootElement.GetProperty("EVIDENCE").EnumerateObject().Select(e => e.Name).ToArray();
            argument = Expired() || chaos.NextDouble() < 0.3 ? null : (statements[chaos.Next(statements.Length)], exhibits[chaos.Next(exhibits.Length)]);
            if (argument is null) { content.Session.GetProgress("answer_pick").Clear(); Story(SampleText("detective-simulation", "text-036"), SampleText("detective-simulation", "text-037")); return; }
        }
        if (argument is not { } claim)
        { awaitingInput = true; Story(SampleText("detective-simulation", "text-038"), SampleText("detective-simulation", "text-039")); }
        else
        {
            content.Session.Set("answer_pick","say",claim.Said);
            content.Session.Set("answer_pick","show",claim.Shown);
            answerSubmitted = true;
            var statement = content.Content.RootElement.GetProperty("STATEMENTS").GetProperty(claim.Said).GetString();
            var exhibit = content.Content.RootElement.GetProperty("EVIDENCE").GetProperty(claim.Shown).GetProperty("name").GetString();
            Story(SampleText("detective-simulation", "text-027"), $"提交主观论证：{statement}；举证：{exhibit}（{claim.Shown}，已检视，尚待正式判定）");
        }
    }
    else if (name == "finale") PlayFlow(() => content.BeginFinale(world));
    else if (name == "door_open") { exitGameMinutes = content.GameMinutes; Story(SampleText("detective-simulation", "text-040"), SampleText("detective-simulation", "text-041")); }
    else if (name == "tube")
    {
        // No answer, evidence inspection or replay input is invented here.
        awaitingInput = true;
        Story(SampleText("detective-simulation", "text-038"), SampleText("detective-simulation", "text-042"));
    }
}
void PlayDialogue(string title)
{
    var packet = dialogue.Start(title);
    var selectedChoices = new HashSet<string>(StringComparer.Ordinal);
    for (var budget = 0; budget < 10000; budget++)
    {
        SyncStage();
        foreach (var line in packet.Lines)
        {
            planner.RecordVisibleSource(line.LineId, line.Text, line.SourceId);
            Story(SampleText("detective-simulation", "text-019"), (line.Speaker.Length == 0 ? "" : line.Speaker + "：") + line.Text);
        }
        foreach (var command in packet.WorldCommands) Command(command.Name, command.Arguments);
        foreach (var choice in packet.Choices)
            Story(SampleText("detective-simulation", "text-043"), choice.Label + (choice.IsAvailable ? "" : SampleText("detective-simulation", "text-044") + choice.LockHint + "）"));
        Tick(false);
        if (packet.IsComplete || awaitingInput)
        {
            if (chaos is null && (title.StartsWith("answer_wrong",StringComparison.Ordinal) || title == "answer_need_test"))
            { awaitingInput = true; Story(SampleText("detective-simulation", "text-045"), SampleText("detective-simulation", "text-046")); }
            return;
        }
        if (packet.Choices.Count > 0)
        {
            // Repeated question menus keep their public return option, but each question
            // is asked at most once during this actual conversation.
            var available = packet.Choices.Where(c => c.IsAvailable && !selectedChoices.Contains(c.Id)).ToArray();
            var rules = Sample("detective-simulation", "dialogue-weights").EnumerateArray()
                .Where(r => r.GetProperty("profile").GetString() == decisionProfile).ToArray();
            var weighted = available.Select(choice =>
            {
                var matching = rules.Where(r => (r.GetProperty("scene").GetString() == title || r.GetProperty("scene").GetString() == "*")
                    && r.GetProperty("choice_ids").EnumerateArray().Any(id => id.GetString() == choice.Id)).ToArray();
                return new { Choice = choice, Weight = matching.Length == 0 ? 1.0 : matching.Sum(r => r.GetProperty("weight").GetDouble()) };
            }).Where(c => c.Weight > 0).ToArray();
            var draw = randomChoices.NextDouble() * weighted.Sum(c => c.Weight);
            var choice = weighted.FirstOrDefault(c => (draw -= c.Weight) < 0)?.Choice;
            if (choice is null) { awaitingInput = true; Story(SampleText("detective-simulation", "text-038"), SampleText("detective-simulation", "text-047")); return; }
            Story(SampleText("detective-simulation", "text-027"), SampleText("detective-simulation", "text-048") + choice.Label);
            selectedChoices.Add(choice.Id);
            packet = dialogue.Choose(choice.Id);
        }
        else packet = dialogue.Next();
    }
    throw new InvalidDataException("Dialogue exceeded host presentation budget.");
}
void PlayFlow(Action begin)
{
    var plan = content.StartPlan(begin);
    for (var budget = 0; budget < 10000; budget++)
    {
        var presentation = content.NextMeeting(plan);
        SyncStage();
        if (plan.Finished) return;
        if (presentation.Length == 0) throw new InvalidOperationException("Logical story flow awaits an unreported host action.");
        if (presentation.StartsWith('@'))
        {
            var parts = presentation[1..].Split(':');
            Command(parts[0], parts.Skip(1).Cast<object?>().ToArray());
            Tick(false);
        }
        else PlayDialogue(presentation);
        if (awaitingInput) return;
        content.Presented(plan);
    }
    throw new InvalidDataException("Story flow exceeded host presentation budget.");
}
world.SetDetectiveControl("simulation");
controls.Add(new { Time = time, Mode = world.DetectiveControl, Reason = SampleText("detective-simulation", "text-049"), SameMind = ReferenceEquals(mind, world.WorldOf(Person.Detective)) });
if (persona.Count > 0) Story(SampleText("detective-simulation", "text-050"), SampleText("detective-simulation", "text-051") + string.Join("，", persona.Select(p => $"{p.Key}={p.Value}")));
if (mindSeed != 0) Story(SampleText("detective-simulation", "text-050"), $"隐藏心智抽样：种子 {mindSeed}（{sampled.Count} 项，取值不向玩家显示）");
if (CaseVariants.Seed != 0) Story(SampleText("detective-simulation", "text-050"), $"寝区案件变体：种子 {CaseVariants.Seed}（真相与答案不变）");
if (playAs is not null)
{
    var opening = content.Content.RootElement.GetProperty("OPENINGS").EnumerateArray().FirstOrDefault(o => o.GetProperty("person").GetString() == playAs);
    if (opening.ValueKind == JsonValueKind.Undefined) throw new ArgumentException(SampleText("detective-simulation", "text-052") + playAs);
    Story(SampleText("detective-simulation", "text-053"), $"玩家选择 {playAs}（{(opening.GetProperty("selectable").GetBoolean() ? SampleText("detective-simulation", "text-054") : SampleText("detective-simulation", "text-055"))}）；侦探与其余人照常自主行动");
    if (playAs != SampleText("detective-simulation", "text-056")) PlayDialogue(opening.GetProperty("scene").GetString()!);
}
Story(SampleText("detective-simulation", "text-050"), $"来源：{runSource}；{(stopMinutes is { } limit ? string.Format(System.Globalization.CultureInfo.CurrentCulture, SampleText("detective-simulation", "text-057"), limit, startMinutes, limit - startMinutes) : SampleText("detective-simulation", "text-058"))}");
content.Session.Locate("detective", currentRoom);
SyncStage();
PlayFlow(() => content.StartFlow(world, SampleText("detective-simulation", "text-059")));
// Roaming and meeting are advanced through the original public daily/story flow.
for (var tick = 0; tick < 180 * 4 && !awaitingInput && !Expired() && Stage() != "investigate"; tick++) Tick(true);
int chaosActions = 0;
NightVariables RandomVariables() => NightVariables.All().ElementAt(chaos!.Next(NightVariables.All().Count()));
DetectiveAction ChaosAction()
{
    // 合法动作与非常规动作混在一起，均匀抽取；不看真相、不看判定。
    var rooms = new[] { "R05", "R08", "hall", "pantry", "foyer" };
    var evidence = content.Content.RootElement.GetProperty("EVIDENCE").EnumerateObject().Select(e => e.Name).ToArray();
    var joints = world.Joints.Keys.ToArray();
    var pool = planner.Actions.Where(a => a.Kind != "answer" || chaos!.NextDouble() < 0.3).ToList();
    pool.Add(new("chaos_destroy", "destroy", evidence[chaos!.Next(evidence.Length)], "", SampleText("detective-simulation", "text-060"), 2, 0, "", "", "", "", true));
    pool.Add(new("chaos_joint", "joint", joints[chaos.Next(joints.Length)], "", SampleText("detective-simulation", "text-061"), 3, 0, "", "", "", "", true));
    pool.Add(new("chaos_wander", "wander", "", rooms[chaos.Next(rooms.Length)], SampleText("detective-simulation", "text-062"), 1, 0, "", "", "", "", true));
    pool.Add(new("chaos_unknown", "destroy", SampleText("detective-simulation", "text-063"), "", SampleText("detective-simulation", "text-064"), 1, 0, "", "", "", "", true));
    return pool[chaos.Next(pool.Count)];
}
for (var actionIndex = 0; actionIndex < (chaos is null ? planner.ActionBudget : 400) && !awaitingInput && !Expired() && Stage() == "investigate"; actionIndex++)
{
    planner.Refresh(mind,content);
    var search = planner.Plan();
    if (chaos is not null)
    {
        var picked = ChaosAction();
        chaosActions++;
        search = search with { Action = picked, Prediction = RandomVariables() };
    }
    searches.Add(new { Time = time, GameMinutes = content.GameMinutes, Decision = search });
    if (search.Action is not { } action)
    { Story(SampleText("detective-simulation", "text-045"), SampleText("detective-simulation", "text-065")); break; }
    Story(SampleText("detective-simulation", "text-066"), $"{action.Label}；树节点 {search.Nodes}，预算 {search.Iterations}，深度 {search.MaxDepth}，候选 {search.RemainingHypotheses}；未来反馈只是采样假设");
    if (action.Room.Length > 0)
    {
        var room = Array.IndexOf(Names.RoomKeys,action.Room);
        if (room < 0) throw new InvalidDataException("Unknown detective action room: " + action.Room);
        world.DailySuspend("detective");
        world.Locate(Person.Detective,(Room)room); content.Session.Locate("detective",action.Room);
        currentRoom = action.Room;
        var signal = content.RoomTrigger(currentRoom);
        SyncStage();
        content.RoomEntry(currentRoom);
        if (signal == "room") PlayFlow(() => content.BeginRoomChanged(world,currentRoom,(long)(time*1000)));
        Story(SampleText("detective-simulation", "text-018"), SampleText("detective-simulation", "text-067") + currentRoom);
    }
    if (awaitingInput) break;
    if (action.Kind is "destroy" or "joint" or "wander")
    {
        if (action.Kind == "destroy")
            Story(SampleText("detective-simulation", "text-036"), (content.DestroyEvidence(world, action.Target) ? SampleText("detective-simulation", "text-068") : SampleText("detective-simulation", "text-069")) + action.Target);
        else if (action.Kind == "joint")
        {
            var loose = !world.Joints[action.Target];
            content.SetJoint(world, action.Target, loose);
            Story(SampleText("detective-simulation", "text-036"), $"把{action.Target}{(loose ? SampleText("detective-simulation", "text-070") : SampleText("detective-simulation", "text-071"))}了");
        }
        else { content.ConsumeAction(SampleText("detective-simulation", "text-072")); Story(SampleText("detective-simulation", "text-036"), SampleText("detective-simulation", "text-073") + action.Room + SampleText("detective-simulation", "text-074")); }
        planner.Completed(action);
        Tick(true);
        continue;
    }
    switch (action.Kind)
    {
        case "inspect":
            foreach (var signal in content.Examine(world,action.Target))
            {
                if (signal == "unlock") { content.UnlockReplay(); PlayDialogue("replay_unlock"); }
                else if (signal == "v6") PlayDialogue("v6_doubt");
            }
            Story(SampleText("detective-simulation", "text-022"),content.CardFacts(action.Target));
            foreach (var note in content.CardNotes(world,action.Target)) Story(SampleText("detective-simulation", "text-022"),note);
            break;
        case "experiment":
            var experimentTarget = action.Target.Split(':');
            PlayFlow(() => content.BeginExperiment(world,experimentTarget[0],experimentTarget.Length > 1 ? experimentTarget[1] : "detective"));
            break;
        case "help": PlayFlow(() => content.BeginHelp(world,action.Target)); break;
        case "talk": PlayFlow(() => content.BeginTalk(world,action.Target)); break;
        case "recall": PlayFlow(() => content.BeginRecall(world,action.Target)); break;
        case "replay":
            if (search.Prediction is not { } variables) { awaitingInput = true; break; }
            var values = new[] { (int)variables.V1,(int)variables.V2,(int)variables.V3,(int)variables.V4,(int)variables.V5,(int)variables.V6 };
            var parameters = content.Content.RootElement.GetProperty("VARIABLES").EnumerateArray().Select((entry,index) =>
                entry.GetProperty("question").GetString() + "：" + entry.GetProperty("options")[values[index]].GetString());
            Story(SampleText("detective-simulation", "text-075"),string.Join("；",parameters) + SampleText("detective-simulation", "text-076"));
            var findings = content.RunVisibleReplay(world,variables);
            var replayFrames = (object[])content.Session.GetProgress("replay_result")["frames"];
            foreach (var item in replayFrames)
            {
                var frame = (Dictionary<string,object>)item;
                var thoughts = (Dictionary<string,object>)frame["thoughts"];
                if (frame.TryGetValue("event", out var eventData) && eventData is Dictionary<string, object> visibleEvent
                    && visibleEvent.TryGetValue("kind", out var eventKind))
                    planner.RecordVisibleSource("replay/" + eventKind, Convert.ToString(frame["label"]) ?? "", "visible replay");
                var voices = thoughts.Select(pair => content.Content.RootElement.GetProperty("PERSON_NAMES").GetProperty(pair.Key).GetString() + SampleText("detective-simulation", "text-077") + pair.Value);
                Story(SampleText("detective-simulation", "text-078"), $"重演内时刻 {frame["t"]}；{frame["actor"]}：{frame["label"]}；" + string.Join("；",voices) + SampleText("detective-simulation", "text-079"));
            }
            planner.RecordReplay(variables,findings);
            foreach (var finding in findings) Story(SampleText("detective-simulation", "text-023"),finding.Text);
            PlayFlow(() => content.BeginAfterReplay(world));
            break;
        case "answer":
            answerPrediction = search.Prediction;
            PlayFlow(() => content.BeginAnswer(world));
            break;
        default: throw new InvalidDataException("Unsupported visible detective action: " + action.Kind);
    }
    if (awaitingInput) break;
    planner.Completed(action);
    // Residents keep the same public tick/arrival stream between investigations.
    Tick(true);
}
SyncStage();
foreach (var entry in content.TakeTiming()) timing.Add(new { Time = time, Timing = entry });
if (chaos is not null)
    Story(SampleText("detective-simulation", "text-036"), $"随机动作 {chaosActions} 次；毁物 {content.Session.GetProgress("destroyed").Count} 件；补救 {content.Session.GetProgress("rescue").Count} 次；未建模 {content.Session.GetProgress("unmodeled").Count} 次；终止阶段 {Stage()}；"
        + (Expired() ? SampleText("detective-simulation", "text-080") : Stage() is "finale" or "ended" ? SampleText("detective-simulation", "text-081") : SampleText("detective-simulation", "text-082")));
if (playAs is not null)
{
    foreach (var line in Wishes.For(playAs)) Story(SampleText("detective-simulation", "text-083"), line);
    foreach (var dorm in world.DormCaseResults)
        foreach (var belief in dorm.Beliefs.Where(b => b.StartsWith(playAs + "：", StringComparison.Ordinal)))
            Story(SampleText("detective-simulation", "text-084"), $"{dorm.Case}：答“{dorm.Answer}”时，{belief}");
}
Story(SampleText("detective-simulation", "text-045"), $"当前阶段 {Stage()}；实际自主答辩提交 {answerSubmitted}；正式终局开门 {exitGameMinutes is not null}。预测、动机与搜索值均为主观假设；结果以正式流程反馈为准。");
ContinuationSimulation.Outcome? continuation = null;
if (Expired()) Story(SampleText("detective-simulation", "text-085"), $"达到运行时长：游戏第 {content.GameMinutes} 分钟；当前阶段 {Stage()}。之后的剧情不在本次运行范围内。");
else if (exitGameMinutes is { } handoffMinutes && !awaitingInput)
{
    var handoffSeconds = time;
    var sequence = 0;
    var finalTimeLabel = SampleText("detective-simulation", "text-086");
    double finalGameMinutes = content.GameMinutes;
    try
    {
        continuation = ContinuationSimulation.Run(world, content.Session,
            content.Content.RootElement.GetProperty("DETECTIVE_SEARCH").GetProperty("seed").GetInt32(), content.GameMinutes, stopMinutes,
            (elapsed, gameMinutes, day, slot, kind, text) =>
            {
                time = Math.Max(time, handoffSeconds + elapsed) + 0.000001;
                sequence++;
                finalGameMinutes = gameMinutes;
                finalTimeLabel = DualOntologyWorldEngine.Chapter2.Prototype.Chapter2Content.TimeName(day, slot);
                storyEvents.Add(new(time, gameMinutes, kind, text, finalTimeLabel));
            });
        storyEvents.Add(new(time, finalGameMinutes, SampleText("detective-simulation", "text-087"), $"续篇结束 {continuation.Ended}；结局 {continuation.Result}；续篇人物行动 {continuation.CharacterActions} 条，其中失败 {continuation.FailedActions} 条；限制：{continuation.Limitation}", finalTimeLabel));
    }
    catch (Exception error)
    {
        awaitingInput = true;
        storyEvents.Add(new(time, finalGameMinutes, SampleText("detective-simulation", "text-088"), error.GetType().Name + "：" + error.Message + SampleText("detective-simulation", "text-089"), finalTimeLabel));
    }
}
else Story(SampleText("detective-simulation", "text-090"), SampleText("detective-simulation", "text-091"));
var result = new
{
    Source = "AwakeningContentSession and Chapter1Dialogue from wake; MorningWorld autonomous residents and detective; logical host reports immediate arrival",
    HostGeometryExercised = false, RealTimePlaytest = false, SimulationSeconds = time,
    Controls = controls, StoryEvents = storyEvents, Plans = plans, Arrivals = arrivals, Searches = searches,
    GameMinutes = content.GameMinutes, Timing = timing,
    Decisions = decisions, Events = events,
    Participants = world.DailyTrajectory.Select(e => e.Person).Distinct().Order().ToArray(),
    SameDetectiveMind = ReferenceEquals(mind, world.WorldOf(Person.Detective)),
    FinalControl = world.DetectiveControl, FinalStage = Stage(), AwaitingInput = awaitingInput,
    AutonomousAnswerSubmitted = answerSubmitted, ExitGameMinutes = exitGameMinutes,
    DecisionProfile = decisionProfile, Continuation = continuation
};
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var recorded = JsonSerializer.SerializeToElement(result);
File.WriteAllText(output, TrajectoryText.Render(recorded));
Console.WriteLine($"已保存 {world.DailyTrajectory.Count} 条完成事件、{result.Participants.Length} 名参与者的轨迹：{output}");

record Arrival(double Time, string Who, int Serial, string Room, bool Accepted);

record StoryEvent(double Time, double GameMinutes, string Kind, string Text, string? TimeLabel = null);
