using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
var runtime=new WorldRuntime();
using var simulation=new SimulationSession(runtime);
var world=new CoreScriptWorld(runtime,simulation);
var actor=world.DefineCharacter("neutral.actor","A",world.DefineSpace("neutral.room","R"));
var snapshot=((ContractResult<CognitionSnapshotV1>.Success)actor.Soul.World.CaptureSnapshot()).Value;
var cognition=new PlayerCognition(snapshot,actor.Self.SubjectId!.Value);
var failures=0; var passes=0;
void Check(string label,bool condition){Console.WriteLine((condition?"PASS ":"FAIL ")+label); if(condition)passes++;else failures++;}
SubjectivePlayerController Controller(string? prefer=null)=>new(new(){Actor="neutral.actor",Name="A",Mode="agent",Rules=prefer==null?[new(){Mode="prefer",Kind="menu",LabelContains="preferred",Weight=100}]:[new(){Mode="prefer",Kind="menu",LabelContains=prefer,Weight=1000}],Reasoning=new(){Depth=8,Expansions=4096,Hypotheses=128,ArgumentWeight=16,InformationWeight=8}},17);
PublicPlayerCandidate Place(string token)=>new(token,"source","interact",false){Location=new(100,100)};
PublicPlayerCandidate Menu(string token,string label,string purpose="")=>new(token,label,"menu",false,Purpose:purpose);
PublicPlayerObservation At(int turn,params PublicPlayerCandidate[] c)=>new(turn,[],[],[..c],false){Field=new("field",new(100,100))};
bool Inquiry(PlayerAlternative a)=>a.Basis.Any(b=>b.StartsWith("seek_")||b.StartsWith("examine_unexamined")||b.StartsWith("return_for_untried")||b.StartsWith("expected_information"));
void Contact(SubjectivePlayerController controller){var d=controller.Decide(At(1,Place("contact")),cognition);Check("contact",controller.CompleteInteraction(d.Token!,true));}
var c=Controller();Contact(c);
c.Decide(At(2,Menu("pick","preferred"),Menu("stale","stale","inspect")),cognition);
c.Decide(At(3,Menu("close","close","close")),cognition);
var back=c.Decide(At(4,Place("back"),new("wait","wait","wait",false)),cognition);
Check("withdrawn_method_no_return_bonus",!Inquiry(back.Alternatives.Single(a=>a.Token=="back")));
foreach(var purpose in new[]{"hide","destroy"}){
 var neutral=Controller();Contact(neutral);
 neutral.Decide(At(2,Menu("pick","preferred"),Menu("remaining",purpose,purpose)),cognition);
 var revisit=neutral.Decide(At(3,Place("back"),new("wait","wait","wait",false)),cognition);
 Check(purpose+"_no_unfinished_inquiry",!Inquiry(revisit.Alternatives.Single(a=>a.Token=="back")));
 var chosen=Controller(purpose).Decide(At(1,Menu("wanted",purpose,purpose),Menu("close","close","close")),cognition);
 Check(purpose+"_character_preference_still_selects",chosen.Token=="wanted");
 var goal=new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(cognition.Actor),new("neutral.goal"),1);
 var material=new FrozenProjectionV1(new(snapshot.SubjectWorld,Guid.NewGuid()),purpose,ProjectionKind.Concept,Truth.True,null,ImmutableDictionary<Tag,int>.Empty.Add(AcquiredPropositions.Opportunity,1),[],[],[new(new FrozenConditionV1.Constant(Truth.True),RuleMode.Infer,new FrozenEffectV1.Think(goal),1,Truth.Unknown)],null,[new(new("acquired.neutral."+purpose),purpose,purpose)],null);
 var informed=cognition with{Snapshot=snapshot with{Projections=snapshot.Projections.Add(material)}};
 var rated=Controller().Decide(At(1,Menu("operation",purpose,purpose),Menu("close","close","close")),informed);
 Console.WriteLine(JsonSerializer.Serialize(rated));
 Check(purpose+"_no_information_reward",!Inquiry(rated.Alternatives.Single(a=>a.Token=="operation")));
}
Console.WriteLine($"COMPLETE passes={passes} failures={failures}");
return failures==0?0:1;
