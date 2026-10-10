using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Game
{
    private PlayerAction? _preparedAction;
    private Truth? _preparedRuleVerdict;
    private sealed class SocialAttempt(PlayerAction action, Person recipient, JsonElement definition)
    {
        public PlayerAction Action { get; } = action;
        public Person Recipient { get; } = recipient;
        public JsonElement Definition { get; } = definition;
        public Projection? Request;
        public ActionExecution? Execution;
        public Essence? Target;
        public bool Accepted;
        public bool Succeeded => Execution?.Status == ExecutionStatus.Completed;
    }
    private readonly List<SocialAttempt> _socialAttempts = [];
    private static string SocialId(PlayerAction a) => a.ActionId ?? (a.Cmd == Cmd.Ask ? a.Job?.ToString() ?? throw new InvalidDataException("Missing social action ID.") : a.Cmd.ToString());
    private bool DeclaredSocial(PlayerAction a) => a.Cmd == Cmd.Ask || ExecutionDesign.GetProperty("actions").TryGetProperty(SocialId(a), out _);
    private JsonElement SocialDefinition(PlayerAction a) => ExecutionDesign.GetProperty("actions").TryGetProperty(SocialId(a), out var d) ? d : throw new InvalidDataException("Unknown social action: " + SocialId(a));
    private static RF SocialFlags(JsonElement d) => d.TryGetProperty("flags",out var f) && !string.IsNullOrWhiteSpace(f.GetString()) ? Enum.Parse<RF>(f.GetString()!) : RF.None;
    private int Compatibility(Person p, PlayerAction a)
    {
        var flags = a.Cmd == Cmd.TellRule ? a.Spec?.Flags ?? a.Card?.Flags(this) ?? RF.None : SocialFlags(SocialDefinition(a));
        var job = a.Cmd == Cmd.TellRule ? null : SocialDefinition(a).TryGetProperty("job",out var j) ? Enum.Parse<Job>(j.GetString()!) : (Job?)null;
        return FlagCompatibility(p, flags) + p.Pro.Habits.Where(h => h.Job == job && h.Slots.Contains(Slot)).Sum(h => h.Weight + (h.Desire ? 2 : 0));
    }
    private static int FlagCompatibility(Person p, RF f) => p.Pro.Values.Where(v => f.HasFlag(v.Key)).Sum(v => v.Value)
        + ((p.Pro.DesireFlags & f) != RF.None ? Math.Max(1,p.Desire) : 0);

    private (Condition Condition, Condition Goal, Effect Effect, Person Actor, Essence? Target)? SocialAst(Person recipient, PlayerAction action, Person reader, Essence? fixedTarget = null, bool physical = false)
    {
        var d=SocialDefinition(action); var actor=d.GetProperty("actor").GetString()=="sender" ? Det : recipient; var fx=physical?RealFx:FxOf(reader);
        var bindings=new Dictionary<string,Noun>(StringComparer.Ordinal)
        { ["$sender"]=fx.R(Det.Body), ["$recipient"]=fx.R(recipient.Body),
            ["$work_route"]=fx.R(Way[WorkPlace(recipient.Id)]), ["$work_room"]=fx.R(Space[WorkPlace(recipient.Id)]) };
        if(action.Place is {} at) bindings.Add("$destination",fx.R(Way[at]));
        if(action.About is {} about) bindings.Add("$about",fx.R(this[about].Body));
        if(action.Thing is {} thing) bindings.Add("$thing",fx.R(Things_[thing].Thing));
        var ast=BindAst(fx,bindings); Projection? target=null; Essence? targetEntity=null; var targetNode=d.GetProperty("target");
        if(targetNode.ValueKind!=JsonValueKind.String)
        {
            var targetAst=BindAst(FxOf(recipient),new Dictionary<string,Noun> { ["$sender"]=recipient.M[Det.Body], ["$recipient"]=recipient.Self });
            var criterion=targetAst.Condition(targetNode);
            var selected=fixedTarget is not null ? recipient.M.GetValueOrDefault(fixedTarget) : recipient.World.Projections
                .Where(p=>(!d.TryGetProperty("target_self",out var self)||self.GetBoolean()||!ReferenceEquals(p,recipient.M[actor.Body]))
                    &&Ast.Evaluate(criterion,new(recipient.M[actor.Body],p,Scene:recipient.Self.Parent))==Truth.True)
                .OrderBy(p=>p.Name,StringComparer.Ordinal).FirstOrDefault(p=>EssenceOf(recipient,p) is {} e && Det.M.ContainsKey(e));
            if(selected is null)return null;
            targetEntity=EssenceOf(recipient,selected)??throw new InvalidDataException("Selected AST target has no runtime binding.");
            target=fx.R(targetEntity);
        }
        var context=new ActionContext(fx.R(actor.Body),target,Scene:fx.R(Space[Where(actor)]));
        Noun Bind(Noun n)=>new Noun.Reference(context.Resolve(n)??throw new InvalidDataException("Unbound social AST slot."));
        var condition=ast.Condition(d.GetProperty("condition"));
        if(target is not null)condition=new Condition.All([condition,ast.Condition(targetNode),
            new Condition.Constant(!d.TryGetProperty("target_self",out var self)||self.GetBoolean()||!ReferenceEquals(targetEntity,actor.Body)?Truth.True:Truth.False)]);
        return (Ast.Map(condition,Bind),Ast.Map(ast.Condition(d.GetProperty("goal")),Bind),Ast.Bind(ast.Effect(d.GetProperty("effect")),context),actor,targetEntity);
    }
    private bool SocialAvailable(Person recipient,PlayerAction action,Essence? target=null)
    {
        var definition=SocialDefinition(action);
        var physical=definition.TryGetProperty("condition_world",out var source)&&source.GetString()=="physical";
        var ast=SocialAst(recipient,action,recipient,target,physical);
        if(ast is null)return false;
        var reader=physical?View[ast.Value.Actor.Body]:recipient.Self;
        return Ast.Evaluate(new Condition.All([ast.Value.Condition,ast.Value.Goal]),new(reader))==Truth.True;
    }
    public int HostSocialScore(P who,PlayerAction a)
    {
        var p=this[who];
        if(!HostDormReleased(p)||p.Asleep||Where(p)!=Where(Det))return int.MinValue;
        if(a.Cmd!=Cmd.TellRule)
        {
            if(!SocialAvailable(p,a))return int.MinValue;
        }
        return SocialSystem.EvaluateSender(p.World,p.Self,p.M[Det.Body],Compatibility(p,a)).Score;
    }
    public bool HostRuleRelevant(RuleSpec spec)=>spec.PostedAt.Contains(Where(Det))
        ||spec.Flags.HasFlag(RF.Rations)&&Where(Det) is Place.Logistics or Place.Clinic
        ||(spec.Flags&(RF.OpenArchive|RF.CommitteeArchive|RF.SealArchive|RF.PactArchive))!=0&&Where(Det) is Place.ArchiveFront or Place.Hall
        ||spec.Flags.HasFlag(RF.EvidenceFirst)&&Day>=CaseDay;
    public bool HostPrepareSocialAction(PlayerAction a)
    {
        if(a.Cmd!=Cmd.TellRule&&!DeclaredSocial(a))return true;
        if (a.Who is null)
        {
            var accepted = false;
            foreach (var who in a.Crew) accepted |= HostPrepareSocialAction(a with { Who=who, Crew=[] });
            if (a.Crew.Length == 0) return true;
            if (accepted) _preparedAction=a;
            return accepted;
        }
        var p=this[a.Who.Value];
        if(HostSocialScore(p.Id,a)<=0)
        { ObserveSocialResult(p,SocialId(a),false,policy:a.Cmd==Cmd.TellRule?"rules":"requests"); Feed.Add(a.Cmd==Cmd.TellRule?Voice(p,a.Spec??OwnRulePreview(a.Card),Truth.False):Answer(p,SocialStance.Reject)); return false; }
        if(a.Cmd==Cmd.TellRule)
        {
            var spec=OwnSpec(a.Spec)??CardSpec(a.Card!,P.Det); _preparedRuleVerdict=Truth.True;
            var before=p.Acc.GetValueOrDefault(spec); p.Acc[spec]=Truth.True;p.Why[spec]=X("social.accepted");spec.Endorsers.Add(p.Id);
            WriteTold(p,spec,Truth.True);if(before!=Truth.True)OnAccepted(p,spec);ObserveSocialResult(p,"tell:"+spec.Id,true,policy:"rules");
        }
        else if(!PrepareDeclaredSocial(p,a))return false;
        _preparedAction=a;return true;
    }
    private bool PrepareDeclaredSocial(Person p,PlayerAction a)
    {
        var ast=SocialAst(p,a,Det);if(ast is null)return false;
        var d=SocialDefinition(a);
        var text=AstText(d.GetProperty("text").GetString()!, ("name",p.Name),
            ("job",d.TryGetProperty("job",out var j)?Names.Of(Enum.Parse<Job>(j.GetString()!)):SocialId(a)),
            ("place",a.Place is {} at?Names.Of(at):""),("thing",a.Thing),("about",a.About is {} about?Names.Of(about):""));
        var procedure=ast.Value.Actor==p?ast.Value.Effect:new Effect.Think(new Condition.Exists(FxOf(Det).R(p.Body)));
        _asked.Remove(p.Id);
        var request=Request(p.Id,text,procedure,Compatibility(p,a),d.TryGetProperty("send_cost",out var sendCost)?sendCost.GetDouble():0.01);
        var accepted=request is not null && _asked.GetValueOrDefault(p.Id)==SocialStance.Accept;
        _socialAttempts.Add(new(a,p,d){Request=request,Accepted=accepted,Target=ast.Value.Target});
        if(accepted && d.TryGetProperty("promise",out var promise) && promise.GetBoolean())Promise(p);
        if(request is not null&&ast.Value.Actor==Det)Observation.Observe(p.Soul,new(request.Name,request.Kind)
            {Target=request,Source=ObservationSource.Introspection,Time=Now,Tags=ImmutableDictionary<Tag,int?>.Empty.Add(Tags.Responded,1)});
        return accepted;
    }
    private RuleSpec OwnRulePreview(Card? c)=>c is null?RuanLedger:new()
        {Id=0,Text=WordingOf(c)??CardText(c),Author=P.Det,Flags=c.Flags(this),Card=c,Target=CardTarget(c),Build=fx=>CompileCard(fx,c)};
    private void ObserveSocialResult(Person p,string identity,bool accepted,Projection? request=null,string policy="requests")
    {
        var receipt=new TestimonyEventV1<Essence?>(new()
        {Time=Now,TimeUnit=ObservationTimeUnitV1.ActionValue,Subject=p.Body,Object=Det.Body,Source=p.Body,Location=Space[Where(p)],
            KnowledgeSource=ObservationSource.Testimony,Phase=EventPhaseV1.Accepted,Outcome=accepted?EventOutcomeV1.Succeeded:EventOutcomeV1.Failed,
            Perceivers=[p.Soul,Det.Soul],Causes=request is not null && p.World.History.LastOrDefault(h=>ReferenceEquals(h.Target,request)&&h.Event is not null)?.Event is {} cause?[cause.Fields.Id]:[]});
        Rt.RecordEvent(receipt);
        foreach(var reader in new[]{p,Det})Observation.Observe(reader.Soul,new(identity,ProjectionKind.Event)
            {Source=ObservationSource.Testimony,Existence=Truth.True,Time=Now,Event=Rt.ProjectEvent(reader.Soul,receipt),
                Tags=ImmutableDictionary<Tag,int?>.Empty.Add(Tags.Responded,1),AddRelations=[(Relations.Subject,reader.M[p.Body])]});
        if(!ExecutionDesign.TryGetProperty("receipt_attitudes",out var policies)||!policies.TryGetProperty(policy,out var outcomes)
            ||!outcomes.TryGetProperty(accepted?"accepted":"rejected",out var row))return;
        var tags=row.EnumerateObject().ToImmutableDictionary(t=>new Tag(t.Name),t=>(int?)t.Value.GetInt32());
        if(tags.Count==0)return;
        var attitude=new TestimonyEventV1<Essence?>(new()
        {Time=Now,TimeUnit=ObservationTimeUnitV1.ActionValue,Subject=Det.Body,Object=p.Body,Source=p.Body,Location=Space[Where(p)],
            KnowledgeSource=ObservationSource.Testimony,Phase=EventPhaseV1.Delivered,Outcome=EventOutcomeV1.Succeeded,
            Perceivers=[p.Soul],Causes=[receipt.Fields.Id]});
        Rt.RecordEvent(attitude);
        Observation.Observe(p.Soul,new(identity+".attitude",ProjectionKind.Event)
            {Source=ObservationSource.Testimony,Existence=Truth.True,Time=Now,Event=Rt.ProjectEvent(p.Soul,attitude),
                Tags=tags,AddRelations=[(Relations.Subject,p.M[Det.Body])]});
    }
    public bool CancelSocialAction(PlayerAction action)
    {
        var attempt=_socialAttempts.LastOrDefault(a=>ReferenceEquals(a.Action,action));
        if(attempt is null || !attempt.Accepted || attempt.Succeeded)return false;
        if(attempt.Execution is {} execution) { S.Cancel(execution); if(ReferenceEquals(attempt.Recipient.Exec,execution))attempt.Recipient.Exec=null; if(ReferenceEquals(Det.Exec,execution))Det.Exec=null; }
        if(attempt.Request is {} request && attempt.Recipient.World.Contains(request))Observation.Observe(attempt.Recipient.Soul,new(request.Name,request.Kind)
            {Target=request,Source=ObservationSource.Introspection,Time=Now,Tags=ImmutableDictionary<Tag,int?>.Empty.Add(Tags.Responded,1)});
        attempt.Accepted=false;
        if(ReferenceEquals(_preparedAction,action))_preparedAction=null;
        return true;
    }
    private bool SocialSucceeded(PlayerAction a)=>_socialAttempts.LastOrDefault(x=>ReferenceEquals(x.Action,a))?.Succeeded==true;
    private Condition? SocialRequirements(Person p,Decision d)
    {
        var attempt=_socialAttempts.LastOrDefault(a=>a.Accepted&&ReferenceEquals(a.Request,d.Basis));
        if(attempt is null)return null;
        var ast=SocialAst(attempt.Recipient,attempt.Action,p,attempt.Target);
        return ast is null?new Condition.Constant(Truth.False):new Condition.All([ast.Value.Condition,ast.Value.Goal]);
    }
    private void TrackSocialExecution(Person p,ActionExecution exec)
    {
        var attempt=_socialAttempts.LastOrDefault(a=>a.Accepted&&ReferenceEquals(a.Request,exec.Intent.Basis));
        if(attempt is not null)attempt.Execution=exec;
    }
    private double SocialCost(Person actor,Effect effect,JsonElement definition)
    {
        var destination=Destination(actor,effect);
        return definition.GetProperty("cost").GetDouble()
            +(destination is {} to && to!=Where(actor) && definition.TryGetProperty("travel_cost",out var travelCost)?travelCost.GetDouble()/actor.Pro.Speed:0)
            +(actor.Weak?0.5:0);
    }
    private bool SubmitSenderSocial(PlayerAction a)
    {
        var attempt=_socialAttempts.LastOrDefault(x=>ReferenceEquals(x.Action,a));if(attempt is null||!attempt.Accepted)return false;
        var ast=SocialAst(attempt.Recipient,a,Det,attempt.Target);
        if(ast is null||ast.Value.Actor!=Det||Det.Exec is not null)return false;
        var requirements=new Condition.All([ast.Value.Condition,ast.Value.Goal]);
        if(!SocialAvailable(attempt.Recipient,a,attempt.Target))return false;
        var exec=S.Submit(Det.Soul,new(Det.Self,ast.Value.Effect,SocialCost(Det,ast.Value.Effect,attempt.Definition),Requirements:requirements));
        Det.Exec=exec;attempt.Execution=exec;_running[exec]=(null,"",0);_startPlace[Det]=Where(Det);return true;
    }
    public IReadOnlyList<(P Who,bool Accepted)> HostMediate(string wording)
    {
        var mediation=ExecutionDesign.GetProperty("mediation");
        if(!mediation.GetProperty("wordings").TryGetProperty(wording,out var row))throw new ArgumentException("Unknown mediation wording.");
        FreeEvents.Add(new FreeEvent.Mediation(SlotIndex,wording));var flags=SocialFlags(row);var opposed=row.GetProperty("opposed").GetString();
        var results=new List<(P,bool)>();
        foreach(var who in mediation.GetProperty("participants").EnumerateArray().Select(v=>Enum.Parse<P>(v.GetString()!)))
        {var p=this[who];var compatibility=FlagCompatibility(p,flags);if(!string.IsNullOrEmpty(opposed))compatibility-=p.Pro.Values.GetValueOrDefault(Enum.Parse<RF>(opposed));
            var accepted=SocialSystem.EvaluateSender(p.World,p.Self,p.M[Det.Body],compatibility).Stance==SocialStance.Accept;
            ObserveSocialResult(p,"mediate:"+wording,accepted,policy:"mediation");results.Add((who,accepted));}
        Mediated=results.All(r=>r.Item2);return results;
    }
}
