using System.Reflection;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
using DualOntologyWorldEngine.Chapter2.Prototype;
static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
var s = new ChapterSession(); s.Locate("a", "r"); s.Place("x", "r");
Check(s.IsLocalTarget("a", "x") && !s.IsLocalTarget("absent", "x"), "shared locality");
s.Observe("a", "x"); Check(s.HasObservation("a", "x") && !s.HasObservation("b", "x"), "shared observation isolation");
s.Set("chapter1", "k", 7); Check((int)s.GetProgress("chapter1")["k"] == 7 && !s.Has("chapter2", "k"), "shared progress isolation");
s.Remove("chapter1", "k"); Check(!s.Has("chapter1", "k"), "shared progress remove");
var g = new Game();
Check(!g.HostCanEnter(Place.Hall) && !g.HostCanEnter(Place.Clinic) && !g.HostCanEnter(Place.ArchiveFront), "chapter2 corridors begin closed");
Check(g.Npcs.Where(p => p.Pro.Dorm is Place.DormS or Place.DormL).All(p => !g.HostDormReleased(p) && g.Where(p) == p.Pro.Dorm), "unreleased residents remain in their own dorms");
g.HostCorridor("north", true); g.HostCorridor("east", true);
g.SetPlayerPlace(Place.Clinic); g.SetPlayerPlace(Place.Hall);
Check(g.Chapter.CharacterRooms[P.Det.ToString()] == "Hall" && g.Where(g.Det) == Place.Hall, "chapter2 room sync");
Check(g.Chapter.HasObservation(P.Det.ToString(), "中央基座"), "chapter2 local observations");
Check(RoomPresentation.For(g, null).Actions.Length == 0 && RoomPresentation.PersonActions(g, P.Huo, null).Length == 0, "unknown room empty action lists");
object? Parse(ConsoleUI ui, string text) { try { return typeof(ConsoleUI).GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(ui, new object?[] { text, false }); } catch(TargetInvocationException e) { throw e.InnerException!; } }
var scoped = new ConsoleUI(g, new StringReader(""), TextWriter.Null, false, roomScoped:true);
bool Rejected(string text) { try { Parse(scoped,text); return false; } catch(PlayerError) { return true; } }
Check(Rejected("查 粮仓"), "cross-room inspect rejected");
var remote = g.Npcs.First(); remote.Body.Get<SpatialProperty>()!.Space = g.Space[Place.Clinic]; g.Rt.Sync();
Check(Rejected("问 " + remote.Name), "cross-room conversation rejected");
g.SetPlayerPlace(g.Where(remote));
var escort = (PlayerAction)Parse(scoped, "同去 " + remote.Name + " 中央基座")!;
Check(escort.Cmd == Cmd.Escort && escort.Place == Place.Hall, "local escort permits cross-room destination");
scoped.HasRoom = false; Check(Rejected("查 中央基座"), "unknown map rejects stale inspect");
var plain = new ConsoleUI(g, new StringReader(""), TextWriter.Null, false);
Check(((PlayerAction)Parse(plain,"查 中央基座")!).Cmd == Cmd.Inspect, "ordinary CLI preserves default unrestricted route");
var definition = WorldScript.Load(Path.GetFullPath(args[0]));
var scripted = definition.CreateGame();
scripted.Story!.Pump();
Check(scripted.Story!.PendingChoice!.Id == "first-thought", "compiled CSharp world script loads and choice suspends");
scripted.ChooseStory("observe");
Check(scripted.Story.PendingChoice is null && Game.Get(scripted.Det.Body,T.Insight)==1 && scripted.Feed.Select(t => t.Replace("\u001estory-text:", "")).Contains("你记下了此刻亲自确认的事。"), "typed effect executes before following text");
Check(!scripted.Feed.Select(t => t.Replace("\u001estory-text:", "")).Contains("你整理了笔记。"), "condition wait suspends");
scripted.SetPlayerPlace(Place.Hall); scripted.RunSlot(null); scripted.RunSlot(null); scripted.Story.Pump();
Check(scripted.Feed.Select(t => t.Replace("\u001estory-text:", "")).Contains("你整理了笔记。") && scripted.Feed.Count(t=>t.Replace("\u001estory-text:", "")=="铜口的声音从远处传来。")==2, "wait releases and parallel repeat executes");
var rebuilt=Game.Rebuild(scripted.Script,scripted.FreeScript,scripted.Script.Count,scripted.Options,scripted.PlaceScript,scripted.StoryChoices);
Check(rebuilt.StoryChoices.SequenceEqual(scripted.StoryChoices) && rebuilt.PlaceScript.SequenceEqual(scripted.PlaceScript) && Game.Get(rebuilt.Det.Body,T.Insight)==1, "rebuild restores choice and visit records");
var choiceCount=scripted.StoryChoices.Count; var placeCount=scripted.PlaceScript.Count; var insight=Game.Get(scripted.Det.Body,T.Insight);
rebuilt.SetPlayerPlace(Place.Clinic); Check(scripted.StoryChoices.Count==choiceCount && scripted.PlaceScript.Count==placeCount && Game.Get(scripted.Det.Body,T.Insight)==insight, "rebuild world mutation does not pollute real world");
var chapter1 = new DualOntologyWorldEngine.Awakening.Chapter1Script();
var awakening = chapter1.CreateContent();
Check(awakening.Content.RootElement.GetRawText()==File.ReadAllText("godot_csharp/scenes/Awakening/Script/generated_awakening_content.json").Trim(), "chapter1 generated content exact");
var compiledDialogue = DualOntologyWorldEngine.Awakening.Chapter1CompiledDialogue.Create();
Check(compiledDialogue.Entries.ContainsKey("s1_wake"), "chapter1 real generated dialogue document");
var packetDialogue = chapter1.CreateDialogue(awakening, chapter1.CreateWorld());
Check(packetDialogue.Start("s1_wake").Lines.Count==1, "chapter1 dialogue factory emits opening packet");
Check(awakening.Content.RootElement.GetProperty("EVIDENCE").EnumerateObject().Count()==12, "chapter1 content loaded by CSharp session");
awakening.Session.Locate("detective","room1"); awakening.Session.Place("E12","room1"); awakening.Session.Place("E3","room2");
Check(awakening.SiteVisible("roam","E12") && !awakening.SiteVisible("investigate","E3"), "chapter1 legal local evidence visibility");
Check(awakening.Judge(2,"V3_0","E8")=="answer_need_test", "chapter1 evidence experiment prerequisite");
awakening.Session.Set("experiments","E8",true);
Check(awakening.Judge(2,"V3_0","E8")=="answer_ok2" && awakening.CanExperiment("E8"), "chapter1 repeated E8 and statement judgment");
awakening.Session.Set("experiments","E1",true); awakening.Session.Set("helped","E1",true); awakening.Session.Set("recalls","huo","known");
Check(!awakening.CanExperiment("E1") && !awakening.CanHelp("E1") && !awakening.CanRecall("huo",false) && !awakening.CanRecall("su",true), "chapter1 completed action and movement gates");
Check(awakening.Judge(1,"me","E7")=="answer_wrong_show", "chapter1 paired evidence requires both examined");
awakening.Session.Set("examined","E12",true); awakening.Session.Set("examined","E7",true);
Check(awakening.Judge(1,"me","E7")=="answer_ok1" && awakening.Judge(3,"V4_0","E6")=="answer_ok3" && awakening.Judge(4,"trust","E9")=="answer_ok4", "chapter1 legal statement judgments");
Check(awakening.Statements([0,0,0,0,0,1],0,false).Length==0, "chapter1 statements require replay");
awakening.Session.Set("examined","E9",true); awakening.Session.Set("examined","E10",true);
Check(awakening.V6Open && awakening.Statements([0,0,0,0,0,1],1,false).Contains("V6_1"), "chapter1 acquaintance statement gate");
var meetingWorld = new DualOntologyWorldEngine.Awakening.MorningWorld(); awakening.BeginMeeting(meetingWorld);
Check(awakening.NextMeeting()=="s2_meet" && awakening.NextMeeting()=="s2_meet", "chapter1 shared runtime waits for UI acknowledgement");
awakening.Presented(); Check(awakening.NextMeeting()=="@name", "chapter1 shared runtime advances on acknowledgment");
var orderedDef = new WorldScriptDefinition { Sequences = [new("ordered",2,Slot.Morning,[new StoryChoice("location-choice","choose",[new("record","record",[new StoryEffect(P.Det,w=>new Effect.Set(w.Det.Self,T.Insight,(int)w.Where(w.Det)))])])])] };
var ordered=orderedDef.CreateGame(); ordered.SetPlayerPlace(Place.Clinic); ordered.ChooseStory("record"); ordered.SetPlayerPlace(Place.Hall); ordered.RunSlot(null);
var orderedCopy=Game.Rebuild(ordered.Script,ordered.FreeScript,ordered.Script.Count,ordered.Options,ordered.PlaceScript,ordered.StoryChoices,ordered.FreeEvents);
Check(Game.Get(ordered.Det.Body,T.Insight)==(int)Place.Clinic && Game.Get(orderedCopy.Det.Body,T.Insight)==(int)Place.Clinic && orderedCopy.FreeEvents.SequenceEqual(ordered.FreeEvents), "ordered visit choice visit preserved by actual Rebuild");
var hidden=new Game(new GameOptions { Hide=(person,projection)=>person.Id==P.Det }); hidden.SetPlayerPlace(Place.Clinic); hidden.SetPlayerPlace(Place.Hall);
Check(!hidden.Chapter.HasObservation(P.Det.ToString(),"中央基座") && !hidden.Chapter.HasObservation(P.Det.ToString(),"病床"), "Hide prevents shared observation ledger leak");
Console.WriteLine("AUTO VALIDATION ONLY; NOT BLIND PLAYTEST");

var mc = meetingWorld.ScriptWorld;
Check(ReferenceEquals(mc.Session.CTB, meetingWorld.StoryTimeline) &&
    ReferenceEquals(mc.Characters["person:Detective"].Body.Get<SpatialProperty>()!.Space, mc["room:R05"]),
    "chapter1 actual original Core session and room reference binding");
var gc = scripted.ScriptWorld;
Check(ReferenceEquals(gc.Runtime, scripted.Rt) && ReferenceEquals(gc.Session, scripted.S) &&
    ReferenceEquals(gc.Characters["person:Det"].Body, scripted.Det.Body) &&
    ReferenceEquals(gc["room:Hall"], scripted.Space[Place.Hall]), "chapter2 actual original Core references binding");
var extra = gc.DefineSpace("validation-room", "验证房间");
var testPerson = gc.DefineCharacter("validation-person", "验证人物", extra);
var testObject = gc.DefineObject("validation-object", "验证物件", extra);
var remoteSpace = gc.DefineSpace("validation-remote", "远处房间");
gc.DefineObject("validation-remote-object", "远处物件", remoteSpace);
var sensed = gc.ObserveRoom(testPerson);
Check(sensed.Any(p => p.Name == "验证物件") && !sensed.Any(p => p.Name == "远处物件") &&
    ReferenceEquals(testPerson.Body.Get<SpatialProperty>()!.Space, extra), "arbitrary dynamic character room object observed locally by original Core");
var property = new Tag("validation-value");
var execution = gc.Submit(testPerson, new Effect.Set(testPerson.Self, property, 17));
gc.Session.CTB.RunUntil(gc.Session.CTB.Now + 5);
Check(execution.Status == ExecutionStatus.Completed && Game.Get(testPerson.Body, property) == 17,
    "dynamic actor action commits original body state through original session");
scripted.S.CTB.RunUntil(scripted.S.CTB.Now + 8);
Check(ReferenceEquals(scripted.ScriptWorld["note"].Get<SpatialProperty>()!.Holder, scripted.ScriptWorld.Characters["guest"].Body),
    "chapter2 externally defined actor autonomously takes original Core object");
