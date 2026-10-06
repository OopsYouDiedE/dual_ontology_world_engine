using System.Text.Json;
using DualOntologyWorldEngine.Awakening;

// Logical host: arrival is reported through the same public boundary as Godot.
// This generates a simulation trajectory, not a puzzle solution or a geometric playtest.
if (args.Length > 0 && args[0] == "--render-json")
{
    if (args.Length is < 2 or > 3)
        throw new ArgumentException("用法：--render-json <已有JSON> [输出txt路径]");
    var source = Path.GetFullPath(args[1]);
    var target = args.Length == 3 ? Path.GetFullPath(args[2]) : Path.ChangeExtension(source, ".txt");
    if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("文本输出路径不能覆盖源JSON。");
    using var document = JsonDocument.Parse(File.ReadAllText(source));
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.WriteAllText(target, TrajectoryText.Render(document.RootElement));
    Console.WriteLine($"已转写轨迹：{target}");
    return;
}
if (!args.Contains("--enabled", StringComparer.Ordinal))
    throw new ArgumentException("自主逻辑模拟必须显式传入 --enabled；正常游戏保持玩家控制。");
var decisionProfile = "default";
string? durationArgument = null;
var outputArguments = new List<string>();
for (var index = 0; index < args.Length; index++)
{
    if (args[index] == "--enabled") continue;
    if (args[index] == "--decision-profile")
    {
        if (++index >= args.Length || args[index].StartsWith("--",StringComparison.Ordinal))
            throw new ArgumentException("--decision-profile 需要一个剧情选择意图名称。");
        decisionProfile = args[index];
    }
    else if (args[index] == "--duration")
    {
        if (++index >= args.Length || args[index].StartsWith("--",StringComparison.Ordinal))
            throw new ArgumentException("--duration 需要时长，如 240、90分钟、1天22小时或到结局。");
        durationArgument = args[index];
    }
    else if (args[index].StartsWith("--",StringComparison.Ordinal))
        throw new ArgumentException("未知模拟参数：" + args[index]);
    else outputArguments.Add(args[index]);
}
if (outputArguments.Count > 1) throw new ArgumentException("模拟只接受一个输出路径。");
var outputArgument = outputArguments.FirstOrDefault();
var output = outputArgument is not null ? Path.GetFullPath(outputArgument) : Path.GetFullPath("artifacts/chapter-one-ecology/unified-game-trajectory.txt");
if (string.Equals(Path.GetExtension(output), ".json", StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("完整动作脚本只接受文本输出路径，不生成 JSON 文件。");
var world = new MorningWorld();
world.InitializePlayerPerception();
var content = new AwakeningContentSession();
if (!content.Content.RootElement.GetProperty("DETECTIVE_DIALOGUE_CHOICES").EnumerateObject()
    .Any(rule => rule.Value.GetProperty("profile").GetString() == decisionProfile))
    throw new ArgumentException("剧情中没有该模拟对白选择意图：" + decisionProfile);
using var layoutStream = typeof(MorningWorld).Assembly.GetManifestResourceStream("Awakening.Layout.json")
    ?? throw new InvalidOperationException("Missing public host layout.");
using var hostLayout = JsonDocument.Parse(layoutStream);
// 运行时长：外部参数优先，否则读入口文件末尾的“运行”；两者都没有时不启动，避免默认跑出无意的长度。
var root = content.Content.RootElement;
var startMinutes = root.GetProperty("DAY_CLOCK").GetProperty("start_minutes").GetInt32();
double? stopMinutes;
string runSource;
if (durationArgument is not null) { stopMinutes = startMinutes + RunDuration.Parse(durationArgument); runSource = "外部参数 --duration " + durationArgument; }
else if (root.TryGetProperty("RUN", out var run))
{
    var declared = run.GetProperty("duration_minutes");
    stopMinutes = declared.ValueKind == JsonValueKind.Null ? null : startMinutes + declared.GetInt32();
    runSource = "剧本的运行语句";
}
else throw new ArgumentException("剧本没有“运行”语句时，须用 --duration 指定时长（可写“到结局”）。");
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
int personalRestCount = 0;
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
    Story("宿主", $"公开剧情站位到场代报：{who}；{spot}（{x},{y}）；{roomKey}；未验几何");
}

string Stage() => (string)content.Session.GetProgress("state")["stage"];
void Story(string kind, string text)
{
    storyEvents.Add(new(time, content.GameMinutes, kind, text));
    if (kind is "对白" or "旁白" or "公告" or "证物" or "对照")
        planner.RecordVisibleText(text,$"host:{kind}; game_minutes={content.GameMinutes}; logical_seconds={time}");
}
void SyncStage()
{
    var stage = Stage();
    world.SetDailyStage(stage);
    world.SetMorningGameMinutes(content.GameMinutes);
    if (previousStage == stage) return;
    Story("阶段", stage);
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
            Story("宿主", "相遇站位到场代报；未验房内几何");
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
    Story("命令", name + (arguments.Count == 0 ? "" : "：" + string.Join("；", arguments)));
    if (name == "name")
        Story("模拟输入", "采用报名栏的默认称呼：" + content.Session.GetProgress("state")["detective_name"]);
    else if (name is "board" or "field_board")
    {
        // Same host-side read acknowledgment and public clauses as open_board.
        content.Session.Set("flags", "board_read", true);
        Story("公告", Chapter1Data.Text("公告框·标题"));
        Story("公告", Chapter1Data.Text("公告框·引言"));
        foreach (var clause in MorningWorld.PostedRules) Story("公告", clause);
    }
    else if (name == "aside") Story("旁白", string.Join("：", arguments));
    else if (name == "stage_actor" && arguments.Count > 1)
        LocateStaged(Convert.ToString(arguments[0])!,Convert.ToString(arguments[1])!);
    else if (name == "finale_prepare") LocateStaged("detective","终局站位·侦探");
    else if (name == "posts")
    {
        foreach (var post in hostLayout.RootElement.GetProperty("posts").EnumerateObject())
            LocatePublicPoint(post.Name,post.Value,"岗位·"+post.Name);
    }
    else if (name == "summon" && arguments.Count > 0)
    {
        var who = Convert.ToString(arguments[0])!;
        var person = Array.IndexOf(Names.PersonKeys, who);
        var room = Array.IndexOf(Names.RoomKeys,currentRoom);
        if (person >= 0 && room >= 0) { world.DailySuspend(who); world.Locate((Person)person,(Room)room); content.Session.Locate(who,currentRoom); }
        Story("宿主", "公开召集到场代报：" + who);
    }
    else if (name == "card" && arguments.Count > 0)
    {
        var id = Convert.ToString(arguments[0])!;
        Story("证物", content.CardFacts(id));
        foreach (var note in content.CardNotes(world,id)) Story("证物",note);
    }
    else if (name == "sound")
    {
        var sentences = TestSentences.All;
        var sentence = randomChoices.Next(sentences.Length);
        var spots = content.Content.RootElement.GetProperty("SOUND_SPOTS").EnumerateObject().Select(s => s.Name).ToArray();
        var spot = spots[randomChoices.Next(spots.Length)];
        Story("模拟输入", "公开声路菜单采样：" + sentences[sentence].Full + "；站位 " + spot);
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
        foreach (var perception in heard) Story("证物",Names.Of(perception.Who)+"："+perception.Text+"；"+perception.Direction);
    }
    else if (name == "answerpick")
    {
        var step = Convert.ToInt32(content.Session.GetProgress("answer_state")["step"]);
        var argument = answerPrediction is { } prediction ? planner.Argument(step,content,prediction) : null;
        if (argument is not { } claim)
        { awaitingInput = true; Story("待输入", "没有足够公开依据提交本步陈述与举证，停止答辩"); }
        else
        {
            content.Session.Set("answer_pick","say",claim.Said);
            content.Session.Set("answer_pick","show",claim.Shown);
            answerSubmitted = true;
            var statement = content.Content.RootElement.GetProperty("STATEMENTS").GetProperty(claim.Said).GetString();
            var exhibit = content.Content.RootElement.GetProperty("EVIDENCE").GetProperty(claim.Shown).GetProperty("name").GetString();
            Story("模拟输入", $"提交主观论证：{statement}；举证：{exhibit}（{claim.Shown}，已检视，尚待正式判定）");
        }
    }
    else if (name == "finale") PlayFlow(() => content.BeginFinale(world));
    else if (name == "rest")
    {
        PlayFlow(() => content.BeginRest(world));
        personalRestCount++;
        Story("个人休息", "侦探短暂独自休息结束，三名同伴仍按各自目标行动，继续查看生活翼");
    }
    else if (name == "door_open") { exitGameMinutes = content.GameMinutes; Story("出门", "正式终局流程开启隔离门"); }
    else if (name == "tube")
    {
        // No answer, evidence inspection or replay input is invented here.
        awaitingInput = true;
        Story("待输入", "此宿主演示未提供该交互输入，停止推进流程");
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
            Story("对白", (line.Speaker.Length == 0 ? "" : line.Speaker + "：") + line.Text);
        foreach (var command in packet.WorldCommands) Command(command.Name, command.Arguments);
        foreach (var choice in packet.Choices)
            Story("可见选项", choice.Label + (choice.IsAvailable ? "" : "（不可选：" + choice.LockHint + "）"));
        Tick(false);
        if (packet.IsComplete || awaitingInput)
        {
            if (title.StartsWith("answer_wrong",StringComparison.Ordinal) || title == "answer_need_test")
            { awaitingInput = true; Story("停点", "正式答辩反馈未接受论证；保留失败，不遍历判定猜答案"); }
            return;
        }
        if (packet.Choices.Count > 0)
        {
            // Repeated question menus keep their public return option, but each question
            // is asked at most once during this actual conversation.
            var available = packet.Choices.Where(c => c.IsAvailable && !selectedChoices.Contains(c.Id)).ToArray();
            var rules = content.Content.RootElement.GetProperty("DETECTIVE_DIALOGUE_CHOICES").EnumerateObject().Select(r => r.Value)
                .Where(r => r.GetProperty("profile").GetString() == decisionProfile).ToArray();
            var weighted = available.Select(choice =>
            {
                var matching = rules.Where(r => (r.GetProperty("scene").GetString() == title || r.GetProperty("scene").GetString() == "*")
                    && choice.Label.Contains(r.GetProperty("label_contains").GetString()!,StringComparison.Ordinal)).ToArray();
                return new { Choice = choice, Weight = matching.Length == 0 ? 1.0 : matching.Sum(r => r.GetProperty("weight").GetDouble()) };
            }).Where(c => c.Weight > 0).ToArray();
            var draw = randomChoices.NextDouble() * weighted.Sum(c => c.Weight);
            var choice = weighted.FirstOrDefault(c => (draw -= c.Weight) < 0)?.Choice;
            if (choice is null) { awaitingInput = true; Story("待输入", "当前没有可用选项"); return; }
            Story("模拟输入", "按剧情中侦探选择意图对当前可见选项采样：" + choice.Label);
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
controls.Add(new { Time = time, Mode = world.DetectiveControl, Reason = "新会话开始即启用侦探与住民相同的自主行动机制", SameMind = ReferenceEquals(mind, world.WorldOf(Person.Detective)) });
Story("运行", $"来源：{runSource}；{(stopMinutes is { } limit ? $"运行到游戏第 {limit} 分钟（自起始 {startMinutes} 分钟起 {limit - startMinutes} 分钟）" : "运行到剧情自然结束")}");
content.Session.Locate("detective", currentRoom);
SyncStage();
PlayFlow(() => content.StartFlow(world, "醒来"));
// Roaming and meeting are advanced through the original public daily/story flow.
for (var tick = 0; tick < 180 * 4 && !awaitingInput && !Expired() && Stage() != "investigate"; tick++) Tick(true);
for (var actionIndex = 0; actionIndex < planner.ActionBudget && !awaitingInput && !Expired() && Stage() == "investigate"; actionIndex++)
{
    planner.Refresh(mind,content);
    var search = planner.Plan();
    searches.Add(new { Time = time, GameMinutes = content.GameMinutes, Decision = search });
    if (search.Action is not { } action)
    { Story("停点", "主观模型无可执行调查，或观测与假设不相容；未提交猜测答辩。"); break; }
    Story("多步推演", $"{action.Label}；树节点 {search.Nodes}，预算 {search.Iterations}，深度 {search.MaxDepth}，候选 {search.RemainingHypotheses}；未来反馈只是采样假设");
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
        Story("宿主", "调查目的地到场代报；未验几何：" + currentRoom);
    }
    if (awaitingInput) break;
    switch (action.Kind)
    {
        case "inspect":
            foreach (var signal in content.Examine(world,action.Target))
            {
                if (signal == "unlock") { content.UnlockReplay(); PlayDialogue("replay_unlock"); }
                else if (signal == "v6") PlayDialogue("v6_doubt");
            }
            Story("证物",content.CardFacts(action.Target));
            foreach (var note in content.CardNotes(world,action.Target)) Story("证物",note);
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
            Story("重演设想",string.Join("；",parameters) + "。这些是待检验假设，随后与已检视的现场逐项对照。");
            var findings = content.RunVisibleReplay(world,variables);
            var replayFrames = (object[])content.Session.GetProgress("replay_result")["frames"];
            foreach (var item in replayFrames)
            {
                var frame = (Dictionary<string,object>)item;
                var thoughts = (Dictionary<string,object>)frame["thoughts"];
                var voices = thoughts.Select(pair => content.Content.RootElement.GetProperty("PERSON_NAMES").GetProperty(pair.Key).GetString() + "（墨影心声）：" + pair.Value);
                Story("昨夜墨影", $"重演内时刻 {frame["t"]}；{frame["actor"]}：{frame["label"]}；" + string.Join("；",voices) + "（由本次设想生成，非晨间现场的新事实）");
            }
            planner.RecordReplay(variables,findings);
            foreach (var finding in findings) Story("对照",finding.Text);
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
Story("停点", $"当前阶段 {Stage()}；实际自主答辩提交 {answerSubmitted}；正式终局开门 {exitGameMinutes is not null}。预测、动机与搜索值均为主观假设；结果以正式流程反馈为准。");
ContinuationSimulation.Outcome? continuation = null;
if (Expired()) Story("运行停点", $"达到运行时长：游戏第 {content.GameMinutes} 分钟；当前阶段 {Stage()}。之后的剧情不在本次运行范围内。");
else if (exitGameMinutes is { } handoffMinutes && !awaitingInput)
{
    var handoffSeconds = time;
    var sequence = 0;
    var finalTimeLabel = "生活翼正式交接后";
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
        storyEvents.Add(new(time, finalGameMinutes, "整场结论", $"续篇结束 {continuation.Ended}；结局 {continuation.Result}；续篇人物行动 {continuation.CharacterActions} 条，其中失败 {continuation.FailedActions} 条；限制：{continuation.Limitation}", finalTimeLabel));
    }
    catch (Exception error)
    {
        awaitingInput = true;
        storyEvents.Add(new(time, finalGameMinutes, "交接或续篇失败", error.GetType().Name + "：" + error.Message + "；保留本轮轨迹，不构造完成会话代替。", finalTimeLabel));
    }
}
else Story("整场停点", "本轮生活翼未正式开门，不能构造另一会话跳过原剧情进入续篇。");
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
    DecisionProfile = decisionProfile, PersonalRestCount = personalRestCount, Continuation = continuation
};
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var recorded = JsonSerializer.SerializeToElement(result);
File.WriteAllText(output, TrajectoryText.Render(recorded));
Console.WriteLine($"已保存 {world.DailyTrajectory.Count} 条完成事件、{result.Participants.Length} 名参与者的轨迹：{output}");

record Arrival(double Time, string Who, int Serial, string Room, bool Accepted);

record StoryEvent(double Time, double GameMinutes, string Kind, string Text, string? TimeLabel = null);

