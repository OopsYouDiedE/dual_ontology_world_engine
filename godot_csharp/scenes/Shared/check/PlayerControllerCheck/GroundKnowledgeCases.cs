using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
internal static class GroundKnowledgeCases
{
    static PlayerCognition Cognition()
    {
        var runtime = new WorldRuntime(); using var simulation = new SimulationSession(runtime);
        var world = new CoreScriptWorld(runtime, simulation);
        var actor = world.DefineCharacter("neutral.ground.actor", "A", world.DefineSpace("neutral.ground.room", "R"));
        return new(((ContractResult<CognitionSnapshotV1>.Success)actor.Soul.World.CaptureSnapshot()).Value, actor.Self.SubjectId!.Value);
    }
    static SubjectivePlayerController Controller() => new(new() { Actor="neutral.ground.actor", Name="A", Mode="agent", Rules=[new(){Mode="prefer",Kind="untried",Weight=6}], Reasoning=new(){Depth=8,Expansions=4096,Hypotheses=128,ArgumentWeight=16,InformationWeight=8}},17);
    static PublicPlayerCandidate Move(string token, bool known, string purpose="") => new(token,token,"move",false,Purpose:purpose){Location=new(144,16),KnownGround=known};
    static PublicPlayerObservation At(int turn,string space,params PublicPlayerCandidate[] candidates) => new(turn,[],[],[..candidates],false){Field=new(space,new(16,16))};
    static bool Novel(PlayerAlternative a) => a.Basis.Contains("authored_rule:prefer:untried:6");
    public static void Run(Action<string,bool> check)
    {
        var held=Cognition();
        foreach(var reverse in new[]{false,true}) {
            var known=Move("known",true); var unknown=Move("unknown",false) with {Location=new(16,144)};
            var d=Controller().Decide(At(1,"a",reverse?unknown:known,reverse?known:unknown),held);
            Console.WriteLine("GROUND "+JsonSerializer.Serialize(d));
            check("ground_unknown_beats_seen_unstepped_"+reverse,d.Token=="unknown" && !Novel(d.Alternatives.Single(a=>a.Token=="known")) && !d.Alternatives.Single(a=>a.Token=="known").Basis.Any(b=>b.StartsWith("unwalked_ground:")));
        }
        var frontier=Controller().Decide(At(1,"a",Move("known",true),Move("frontier",true,"frontier")),held);
        check("ground_seen_frontier_keeps_exploration",frontier.Token=="frontier" && frontier.Basis.Contains("edge_of_unseen") && Novel(frontier.Alternatives.Single(a=>a.Token=="frontier")));
        var controller=Controller(); controller.Decide(At(1,"a",Move("blocked",false)),held);
        var once=controller.Decide(At(2,"a",Move("blocked",false)),held);
        var twice=controller.Decide(At(3,"a",Move("blocked",false)),held);
        check("ground_unknown_wall_accumulates_failures",once.Basis.Contains("observed_movement_stalled:1") && twice.Basis.Contains("observed_movement_stalled:2") && twice.Score<once.Score);
        var elsewhere=controller.Decide(At(4,"b",Move("blocked",false)),held);
        check("ground_space_isolation",!elsewhere.Basis.Any(b=>b.StartsWith("observed_movement_stalled:")));
        var other=Controller().Decide(At(1,"a",Move("blocked",false)),Cognition());
        check("ground_subject_isolation",!other.Basis.Any(b=>b.StartsWith("observed_movement_stalled:")));
        var port=new PlayerCandidateSession(_=>[],"A",10,100);
        var offer=new PlayerFieldOffer("step","move",false,new Dictionary<string,object?>{{"kind","field_action"}}){Location=new(144,16)};
        var before=port.Observe([],[offer]).Candidates.Single();
        var same=port.Observe([],[offer]).Candidates.Single();
        var after=port.Observe([],[offer with{KnownGround=true}]).Candidates.Single();
        check("ground_metadata_changes_token",before.Token==same.Token && after.Token!=before.Token && !before.KnownGround && after.KnownGround);
        bool rejected=false;try{port.Submit(new(before.Token));}catch(PlayerBoundaryException e){rejected=e.Code=="unknown_consumed_or_expired_token";}
        check("ground_old_metadata_token_rejected",rejected);
        FrozenProjectionV1 Material(string name,Tag tag)=>new(new(held.Snapshot.SubjectWorld,Guid.NewGuid()),name,ProjectionKind.Concept,Truth.True,null,ImmutableDictionary<Tag,int>.Empty.Add(tag,1),[],[],[new(new FrozenConditionV1.Constant(Truth.True),RuleMode.Infer,new FrozenEffectV1.Think(new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(held.Actor),new("neutral-goal"),1)),1,Truth.Unknown)],null,[new(new("acquired.neutral."+name),name,name)],null);
        held=held with{Snapshot=held.Snapshot with{Projections=held.Snapshot.Projections.Add(Material("question",AcquiredPropositions.Question)).Add(Material("target",AcquiredPropositions.Opportunity))}};
        var route=Controller();
        route.Decide(At(1,"a",new("target","target","interact",false){Location=new(400,16)},new("continue","continue","continue",false)),held);
        var approach=route.Decide(At(2,"a",Move("plain",true),Move("approach",true,"approach") with{Value="target"}),held);
        Console.WriteLine("GROUND_APPROACH "+JsonSerializer.Serialize(approach));
        check("ground_reasoned_approach_keeps_information",approach.Token=="approach" && approach.Basis.Contains("follow_observed_route_to_needed_place"));
    }
}
