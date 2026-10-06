using System.Reflection;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
using A = DualOntologyWorldEngine.Awakening;
using B = DualOntologyWorldEngine.Chapter2.Prototype;
static void Check(bool b,string n){if(!b)throw new Exception(n);Console.WriteLine("PASS "+n);}
var chapter=new A.Chapter1Script(); var w=chapter.CreateWorld(); var content=chapter.CreateContent();
Check(typeof(A.MorningWorld).Assembly.GetName().Name=="Chapter1Script" && typeof(B.Game).Assembly.GetName().Name=="Chapter2Script","formal assembly names");
Check(typeof(A.MorningWorld).Assembly.GetReferencedAssemblies().Any(x=>x.Name=="ChapterRuntime") && typeof(B.Game).Assembly.GetReferencedAssemblies().Any(x=>x.Name=="ChapterRuntime"),"both chapters reference shared runtime");
foreach(var asm in new[]{typeof(A.MorningWorld).Assembly,typeof(B.Game).Assembly}) Check(!asm.GetReferencedAssemblies().Any(x=>x.Name!.Contains("Godot")),"pure CSharp "+asm.GetName().Name);
using(var stream=typeof(A.Chapter1Script).Assembly.GetManifestResourceStream("Awakening.Content.json") ?? throw new Exception("missing compiled content resource"))
using(var bytes=new MemoryStream()) { stream.CopyTo(bytes); Check(bytes.ToArray().SequenceEqual(File.ReadAllBytes("godot_csharp/scenes/Awakening/Script/generated_awakening_content.json")),"embedded bytes Awakening.Content.json"); }
Check(content.Content.RootElement.GetRawText()==File.ReadAllText("godot_csharp/scenes/Awakening/Script/generated_awakening_content.json").Trim(),"runtime JSON raw exact");
var document=A.Chapter1CompiledDialogue.Create();
var generatedDialogue=CompiledDialogueDocument.Parse(File.ReadAllText("godot_csharp/scenes/Awakening/Script/generated_awakening_dialogue.json"));
Check(System.Text.Json.JsonSerializer.Serialize(document)==System.Text.Json.JsonSerializer.Serialize(generatedDialogue),"runtime dialogue exact complete generated document");
Check(document.Entries.ContainsKey("s1_wake") && document.Entries.ContainsKey("s2_meet"),"compiled runtime dialogue opening entries");
var dialogue=chapter.CreateDialogue(content,w);
var wakePacket=dialogue.Start("s1_wake");
Check(wakePacket.Lines.Single().Text==document.Entries["s1_wake"].First(x=>x.Op=="line").Text,"runtime dialogue exact first compiled line");
for(var packetCount=0; !wakePacket.IsComplete && packetCount<100; packetCount++) { Check(wakePacket.Choices.Count==0,"wake has no unexpected choices"); wakePacket=dialogue.Next(); }
Check(wakePacket.IsComplete,"compiled wake finishes within packet budget");
var fields=BindingFlags.NonPublic|BindingFlags.Instance;
int Count(string n)=>((System.Collections.IDictionary)typeof(A.MorningWorld).GetField(n,fields)!.GetValue(w)!).Count;
Check(Count("_souls")==4 && Count("_space")==9 && Count("_evidence")==12,"chapter1 original 4 people 9 rooms 12 evidence");
Check(w.TotalHooks==0 && Enum.GetValues<A.Person>().All(p=>!w.HasRecalled(p) && w.WorldOf(p).Projections.Any(x=>x.Name=="登船")),"chapter1 initial memories and hooks");
var sw=w.ScriptWorld;Check(ReferenceEquals(sw["room:R05"],sw.Characters["person:Detective"].Body.Get<SpatialProperty>()!.Space),"chapter1 original initial detective room");
Check(Enum.GetValues<A.Person>().Where(p=>p!=A.Person.Detective).All(p=>ReferenceEquals(sw["room:Hall"],sw.Characters["person:"+p].Body.Get<SpatialProperty>()!.Space)),"chapter1 initial companions Hall");
content.BeginWake(w);foreach(var expected in new[]{"@lock","s1_wake","@unlock","@hud"}){Check(content.NextMeeting()==expected && content.NextMeeting()==expected,"wake ack "+expected);content.Presented();}Check((string)content.Session.GetProgress("state")["stage"]=="roam","wake transitions roam");
content.BeginMeeting(w);Check(content.NextMeeting()=="s2_meet" && content.NextMeeting()=="s2_meet","meeting waits ack");content.Presented();Check(content.NextMeeting()=="@name","meeting original next name");
var g=B.Chapter2Script.CreateWorld();Check(g.People.Length==16 && g.Space.Count==11 && g.Day==2 && g.Slot==B.Slot.Morning,"chapter2 16 people 11 spaces original start");
var writer=new StringWriter();new B.ConsoleUI(g,new StringReader("状态\n查 中央基座\n"),writer,false).Run();var output=writer.ToString();Check(output.Contains(B.Chapter2Content.Intro) && output.Contains("输入结束"),"CLI original opening and finite input");Check(g.Script.Count==1 && g.Script[0]?.Cmd==B.Cmd.Inspect,"original legal inspect invokes action");Check(output.Contains("口粮") && output.Contains(B.Chapter2Content.TimeName(2,B.Slot.Morning)),"CLI original status");
var copy=B.Game.Rebuild(g.Script,g.FreeScript,g.Script.Count,g.Options,g.PlaceScript,g.StoryChoices,g.FreeEvents);Check(copy.Day==g.Day && copy.Slot==g.Slot && copy.Script.Count==g.Script.Count,"Rebuild original record parity");var originalRoom=g.Where(g.Det);var originalRations=B.Game.Get(g.Granary,B.T.Ration);copy.SetPlayerPlace(B.Place.Clinic);copy.Set(copy.Granary,B.T.Ration,0);Check(g.Where(g.Det)==originalRoom && B.Game.Get(g.Granary,B.T.Ration)==originalRations && !ReferenceEquals(copy.Det.Body,g.Det.Body),"Rebuild isolated original references");
var sharedType=typeof(StoryRuntime<,>); var ch1Type=typeof(A.AwakeningContentSession).GetProperty("_story",fields)!.PropertyType.GetGenericTypeDefinition(); var ch2Type=typeof(B.StoryRuntime).GetField("_runtime",fields)!.FieldType.GetGenericTypeDefinition(); Check(ReferenceEquals(sharedType,ch1Type) && ReferenceEquals(sharedType,ch2Type),"same actual shared generic StoryRuntime type reference"); Console.WriteLine("AUTO VALIDATION ONLY; NOT BLIND PLAYTEST");
