using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Game.FirstAwakening;

public sealed record RedTokenSiteKnowledge(string Name, bool Noticed, bool Identified, bool Understood);
public sealed record RedTokenActionPlan(bool Ok, string Message, string Actor, string Site, string Operation);
public sealed record RedTokenCooperation(string Actor, Projection Request, Decision? Decision, SocialAttitude Attitude);

/// <summary>章节认知入口。实体仍在现实中，命名、规则与人际判断只写入接收者的内部世界。</summary>
public sealed partial class RedTokenSimulation
{
    public static readonly string[] Companions = ["侦探", "工程师霍铎", "主厨沈鼎", "音乐家苏泠"];
    public static readonly Tag Inspected = new("Inspected"), Identified = new("Identified"), Understood = new("Understood"),
        RewardVerified = new("RewardVerified"), ReceptionKnown = new("ReceptionKnown"), ExitKnown = new("ExitKnown"), Introduced = new("Introduced"),
        MechanicalSkill = new("MechanicalSkill"), AcousticSkill = new("AcousticSkill"), SupplySkill = new("SupplySkill"),
        InquirySkill = new("InquirySkill"), RequestAccepted = new("RequestAccepted");
    public static readonly RelationTag AcquaintedWith = new("AcquaintedWith"), CooperatesWith = new("CooperatesWith"),
        Intends = new("Intends"), HasGoal = new("HasGoal");
    private readonly Dictionary<(string Actor, string Id), Projection> _records = [];
    private readonly Dictionary<(string Actor, string Id), Projection> _noticedSites = [];
    private readonly Dictionary<(string Actor, RedTokenOperation Operation), Projection> _procedures = [];

    private void InitializePeople()
    {
        foreach (var actor in Souls.Keys)
        {
            var self = _mental[(actor, actor)];
            var skill = actor switch
            {
                "工程师霍铎" => MechanicalSkill, "主厨沈鼎" => SupplySkill,
                "音乐家苏泠" => AcousticSkill, _ => InquirySkill
            };
            Observation.Observe(Souls[actor], new(actor, ProjectionKind.Body)
            {
                Target = self, Source = ObservationSource.Introspection, Time = Runtime.CTB.Now,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Cooperation, 1).Add(skill, 1)
                    .Add(RewardVerified, 0).Add(ReceptionKnown, 0).Add(ExitKnown, 0).Add(Introduced, 0),
                Details = "保留姓名与职业技能；在校经历及人际关系没有随之恢复。"
            });
            var goal = Observation.Observe(Souls[actor], new(actor == "侦探" ? "核实身份与负责人；入约来意待查" :
                actor == "工程师霍铎" ? "核验负责人与动力工艺交付" : actor == "主厨沈鼎" ? "核验负责人与酬劳、供给安全" :
                actor == "音乐家苏泠" ? "核验负责人与声学知识交付" : "完成本人已接受的工作", ProjectionKind.Concept)
            { Source = ObservationSource.Introspection, Existence = Truth.True });
            Observation.Observe(Souls[actor], new(actor, ProjectionKind.Body)
            { Target = self, Source = ObservationSource.Introspection, AddRelations = [(HasGoal, goal)] });
        }
    }

    public Projection? KnownObject(string actor, string key) => _mental.GetValueOrDefault((actor, key));
    public Projection? KnownRecord(string actor, string id) => _records.GetValueOrDefault((actor, id));
    public IEnumerable<string> NoticedSites(string actor) => _noticedSites.Keys.Where(key => key.Actor == actor).Select(key => key.Id);
    public IEnumerable<string> KnownEvidence(string actor) => _records.Where(pair => pair.Key.Actor == actor &&
        pair.Value.Tags.GetValueOrDefault(Inspected) == 1).Select(pair => pair.Key.Id);
    public bool HasKnowledge(string actor, Tag tag) => KnownObject(actor, actor)?.Tags.GetValueOrDefault(tag) == 1;

    public void RememberSelf(string actor, Tag tag, int value, string details)
    {
        var self = KnownObject(actor, actor)!;
        Observation.Observe(Souls[actor], new(self.Name, self.Kind)
        {
            Target = self, Source = ObservationSource.Introspection, Time = Runtime.CTB.Now,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(tag, value), Details = details
        });
    }

    /// <summary>仅由已通过空间/交互筛选的宿主调用；父链仅建立位置名词，不递归观察其它内容。</summary>
    private Projection ObserveObject(string actor, string key, bool identify = true, bool attentionOnly = false)
    {
        var chain = new List<Essence>();
        var visited = new HashSet<Essence>();
        for (Essence? item = Objects[key]; item is not null && visited.Add(item);
             item = item.Get<SpatialProperty>()?.Holder ?? item.Get<SpatialProperty>()?.Space)
            chain.Add(item);
        chain.Reverse();
        World.Sync();
        var sources = chain.Select(item => _views[item.TrueName]).ToArray();
        var received = World.Observe(Souls[actor], sources, Runtime.CTB.Now,
            new ScenePerception(this, actor, key, identify, attentionOnly));
        for (var i = 0; i < received.Length; i++) _mental[(actor, chain[i].TrueName)] = received[i];
        return _mental[(actor, key)];
    }

    private sealed class ScenePerception(RedTokenSimulation scene, string actor, string focus, bool identify, bool attentionOnly) : IProjectionTransform
    {
        public ObservationInput? Transform(ObservationInput input)
        {
            var item = scene.Objects[input.Name];
            var description = item.Get<RedTokenObjectProperty>();
            var previous = scene.KnownObject(actor, item.TrueName);
            var recognized = input.Name == focus && identify;
            if (scene.Kind == RedTokenWorldKind.Scene && input.Name == "red")
                recognized &= scene.KnownRecord(actor, "E02") is not null;
            // 支撑链只帮助指称位置，不因看见里面一件物品而取得全部抽屉/纸面信息。
            var tags = input.Name == focus && !attentionOnly ? input.Tags : ImmutableDictionary<Tag, int?>.Empty;
            if (input.Name == actor && input.Name == focus && scene.Kind != RedTokenWorldKind.Scene)
                foreach (var ownState in new[] { Read, Accepted, Released })
                    tags = tags.SetItem(ownState, scene.Value(actor, ownState));
            if (!attentionOnly && input.Name == "confirmation" && scene.ObjectLocation(actor) != "C")
            {
                tags = tags.Remove(Clear).Remove(Confirmed).Remove(FilterReady).Remove(Released);
                foreach (var signal in new[] { Clear, Confirmed, FilterReady, Released })
                    if (scene.KnownObject(actor, "confirmation")?.Tags.ContainsKey(signal) != true)
                        tags = tags.Add(signal, 0);
            }
            return input with
            {
                Name = description is null ? input.Name : recognized ? description.DisplayName : previous?.Tags.GetValueOrDefault(Identified) == 1 ? previous.Name : description.UnrecognizedName,
                Source = input.Name == actor && input.Name == focus ? ObservationSource.Introspection : input.Source,
                Tags = tags.SetItem(Identified, recognized ? 1 : previous?.Tags.GetValueOrDefault(Identified) ?? 0),
                AddRelations = attentionOnly ? [] : input.AddRelations,
                Rules = input.Name == focus && !attentionOnly ? input.Rules : null,
                Procedure = input.Name == focus && !attentionOnly ? input.Procedure : null,
                Details = attentionOnly ? "只注意到物件轮廓与所在位置，未近身检视。" : input.Details
            };
        }
    }

    /// <summary>显式搜索只注意本房间入口的首件可见实体；不移动、不检视、不读纸面或机制。</summary>
    public bool NoticeSite(string actor, string id)
    {
        if (!Souls.ContainsKey(actor) || !EvidenceObjects.ContainsKey(id) || ObjectLocation(actor) != EvidenceSpace(id)) return false;
        var item = VisibleAtEvidence(id).FirstOrDefault();
        if (item is null) return false;
        _noticedSites[(actor, id)] = ObserveObject(actor, item.TrueName, identify: false, attentionOnly: true);
        return true;
    }

    public RedTokenResult ObserveSite(string actor, string id)
    {
        if (!Souls.ContainsKey(actor) || !EvidenceObjects.ContainsKey(id)) return new(false, "没有对应的观察对象。");
        var targetSpace = EvidenceSpace(id);
        var move = MoveTo(actor, targetSpace);
        if (!move.Ok) return move;
        if (id == "E16" && Value("pipe", Taken) == 1)
        {
            World.Sync();
            var heard = World.Observe(Souls[actor], [_views["接待者"]], Runtime.CTB.Now, new CopperVoice())[0];
            _mental[(actor, "接待者")] = heard;
            RecordEvidence(actor, "statement:铜口", ObservationSource.Testimony,
                "铜口回应者自称接待者，提供一次移物的说明；没有据此取得其所在位置或全部历史。", speaker: heard);
        }
        var observed = VisibleAtEvidence(id).Select(item => ObserveObject(actor, item.TrueName)).ToArray();
        if (observed.Length == 0) return new(false, "此处材料尚未进入可观察范围。");
        RecordEvidence(actor, id, ObservationSource.Perception, "本人近身查看原物及当前可见表面。", observed);
        if (id == "ROOM_COAT") RememberSelf(actor, ReceptionKnown, 1, "票据记有接待位置与本人终止权。");
        if (id == "ROOM_DOOR") RememberSelf(actor, ExitKnown, 1, "本人看到内栓未落下，可以出门寻找别人。");
        if (id is "E02" or "E11")
        {
            foreach (var item in observed)
                Observation.Observe(Souls[actor], new(item.Name, item.Kind)
                { Target = item, Source = ObservationSource.Perception, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Understood, 1),
                    Details = "已读到规程文字；阅读不等于已接受昨夜岗位。" });
        }
        return new(true, "已记录本人当前观察到的材料。");
    }

    public bool CopperIntroductionReceived => Companions.All(actor => KnownRecord(actor, "statement:铜口欢迎") is not null);

    public RedTokenResult ReceiveCopperIntroduction()
    {
        if (CopperIntroductionReceived) return new(true, "铜口的当下自述已记录，负责人身份与交付仍待亲手核验。");
        if (Kind != RedTokenWorldKind.Scene || Companions.Any(actor => !HasKnowledge(actor, Introduced)))
            return new(false, "先完成在场四人的当下介绍。");
        if (Companions.Any(actor => ObjectLocation(actor) != EvidenceSpace("companions")))
            return new(false, "四人须在介绍所在的公共空间共同听取铜口自述。");

        World.Sync();
        foreach (var actor in Companions)
        {
            if (KnownRecord(actor, "statement:铜口欢迎") is not null) continue;
            var speaker = World.Observe(Souls[actor], [_views["接待者"]], Runtime.CTB.Now,
                new CopperVoice("院务维系者", "经领取柜上方铜口听到其当下自述；身份未核验，也未直接看见回应者身体。"))[0];
            _mental[(actor, "接待者")] = speaker;
            RecordEvidence(actor, "statement:铜口欢迎", ObservationSource.Testimony,
                "经领取柜上方铜口听到回应者自称院务维系者，称柜内约定交付可由本人亲手核验；这份自述尚不证明负责人身份或报偿已兑现。",
                speaker: speaker);
            // 自述指向公共空间里的领取柜，仅注意外形；不开柜、不读取交付、不发现其它物件。
            NoticeSite(actor, "E01");
        }
        return new(true, "四人已听取有铜口来源的自述，并注意到待亲手核验的领取柜。");
    }

    private sealed class CopperVoice(string role = "接待者",
        string details = "经铜口听到具名自述，不是直接看见远处身体。") : IProjectionTransform
    {
        public ObservationInput? Transform(ObservationInput input) => input with
        {
            Name = "铜口回应者（自称" + role + "）", Source = ObservationSource.Testimony,
            Move = false, Parent = null, Tags = ImmutableDictionary<Tag, int?>.Empty,
            AddRelations = [], Rules = null, Procedure = null,
            Details = details
        };
    }

    public static string EvidenceSpace(string id) => id switch
    {
        "E01" or "E14" or "companions" => "A",
        "E06" => "D", "E07" or "ROOM_COAT" or "ROOM_DESK" or "ROOM_DOOR" or "ROOM_WINDOW" => "R05",
        "E08" or "E09" or "E09.deep" or "E09.completion" => "C", _ => "B"
    };

    public Projection RecordEvidence(string actor, string id, ObservationSource source, string details,
        IEnumerable<Projection>? objects = null, Projection? speaker = null)
    {
        var relations = (objects ?? []).Select(p => (Relations.Object, p)).ToImmutableHashSet();
        if (speaker is not null) relations = relations.Add((Relations.Subject, speaker));
        var record = Observation.Observe(Souls[actor], new(id, ProjectionKind.Event)
        {
            Target = KnownRecord(actor, id), Source = source, Time = Runtime.CTB.Now, Existence = Truth.True,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Inspected, 1), AddRelations = relations, Details = details
        });
        _records[(actor, id)] = record;
        return record;
    }

    public void IntroducePeople()
    {
        foreach (var actor in Companions)
        {
            foreach (var other in Companions.Where(name => name != actor))
            {
                var person = ObserveObject(actor, other);
                Observation.Observe(Souls[actor], new(person.Name, person.Kind)
                {
                    Target = person, Source = ObservationSource.Testimony, Time = Runtime.CTB.Now,
                    Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, person.Tags.GetValueOrDefault(Tags.Trust)),
                    Details = "当下面对面介绍的姓名和职业，不是恢复在校人际记忆。"
                });
                var self = KnownObject(actor, actor)!;
                Observation.Observe(Souls[actor], new(self.Name, self.Kind)
                { Target = self, Source = ObservationSource.Testimony, AddRelations = [(AcquaintedWith, person)] });
            }
            RememberSelf(actor, Introduced, 1, "已完成当下介绍。");
            RememberSelf(actor, ReceptionKnown, 1, "同伴提出到领取柜共同核实负责人和交付。");
        }
    }

    public Projection ReceiveStatement(string receiver, string sender, string id, string details)
    {
        var speaker = KnownObject(receiver, sender) ?? ObserveObject(receiver, sender);
        return RecordEvidence(receiver, id, ObservationSource.Testimony, details, speaker: speaker);
    }

    public RedTokenResult IdentifyInvestigator(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return new(false, "姓名不能为空。");
        var arrived = MoveTo("侦探", "R05");
        if (!arrived.Ok) return arrived;
        var ticket = ObserveObject("侦探", "ticket");
        var description = Objects["侦探"].Get<RedTokenObjectProperty>()!;
        Objects["侦探"].Add(new RedTokenObjectProperty
        { DisplayName = name.Trim(), UnrecognizedName = description.UnrecognizedName, Portable = description.Portable, Operations = description.Operations });
        var self = KnownObject("侦探", "侦探")!;
        Observation.Observe(Souls["侦探"], new(name.Trim(), ProjectionKind.Body)
        {
            Target = self, Source = ObservationSource.Perception, Time = Runtime.CTB.Now,
            AddRelations = [(Relations.HasIdentity, ticket)], Details = "衣袋票据上的本人姓名；没有恢复入约动机与在校经历。"
        });
        return new(true, "本人姓名已按票据进入认知。");
    }

    public RedTokenResult RecallMaterial(string actor, string site)
    {
        var key = actor == "工程师霍铎" && site == "E11" ? "signed_exception" :
            actor == "音乐家苏泠" && site == "E07" ? "wedge" : null;
        if (key is null) return new(false, "本人没有这份触物片段。");
        var contact = ObserveSite(actor, site);
        if (!contact.Ok) return contact;
        var touch = Apply(actor, RedTokenOperation.RecallMaterial, _ => new Effect.Act(ActionOp.Inspect, See(actor, key)), "本人近身触物");
        if (!touch.Ok) return touch;
        var detail = actor == "工程师霍铎"
            ? "触本人签收折页：合门后台侧仍响。只恢复本人听见的片段，不证明全场顺序。"
            : "触本人沾灰楔：合门后门侧静。只恢复本人触物与听声片段，不证明内销者身份。";
        RecordEvidence(actor, "recall:" + site, ObservationSource.Recall, detail, [KnownObject(actor, key)!]);
        return new(true, detail);
    }

    public void RecordRewardInspection(string actor)
    {
        var reward = ObserveObject(actor, "reward:" + actor);
        RecordEvidence(actor, "reward:" + actor, ObservationSource.Perception,
            "本人检视约定实物、凭据与交付内容；没有替别人代签。", [reward]);
        var self = KnownObject(actor, actor)!;
        var stay = Observation.Observe(Souls[actor], new("本人凭可信交付自愿继续调查", ProjectionKind.Concept)
        { Source = ObservationSource.Introspection, Existence = Truth.True });
        Observation.Observe(Souls[actor], new(self.Name, self.Kind)
        { Target = self, Source = ObservationSource.Introspection, AddRelations = [(HasGoal, stay)] });
    }

    public void RememberCooperation(string receiver, string actor, string request, string details)
    {
        if (receiver == actor) return;
        var speaker = KnownObject(receiver, actor);
        if (speaker is null) return;
        // 远处完成只构成具名回报；近处才是本人看到的协作成果。
        var source = ObjectLocation(receiver) == ObjectLocation(actor) ? ObservationSource.Perception : ObservationSource.Testimony;
        var record = RecordEvidence(receiver, "cooperation:" + actor + ":" + Runtime.CTB.Now, source,
            request + "；" + details, speaker: speaker);
        Observation.Observe(Souls[receiver], new(record.Name, record.Kind)
        { Target = record, Source = source, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, source == ObservationSource.Perception ? 1 : 0) });
        var self = KnownObject(receiver, receiver)!;
        Observation.Observe(Souls[receiver], new(self.Name, self.Kind)
        { Target = self, Source = source, AddRelations = [(CooperatesWith, speaker)] });
    }

    private void DeliverSignal(string sender, string receiver, Tag tag, string details)
    {
        var from = ObjectLocation(sender); var to = ObjectLocation(receiver);
        if (!(from == to || from is "B" or "C" && to is "B" or "C")) return;
        if (KnownObject(sender, "confirmation")?.Tags.GetValueOrDefault(tag) != 1) return;
        var signal = KnownObject(receiver, "confirmation") ?? ObserveObject(receiver, "confirmation", false);
        var speaker = KnownObject(receiver, sender) ?? ObserveObject(receiver, sender);
        var message = Observation.Observe(Souls[receiver], new(details, ProjectionKind.Event)
        {
            Source = ObservationSource.Testimony, Time = Runtime.CTB.Now, Existence = Truth.True,
            AddRelations = [(Relations.Subject, speaker), (Relations.Object, signal)], Details = details
        });
        var attitude = SocialSystem.Evaluate(Souls[receiver].World, KnownObject(receiver, receiver)!, message);
        if (attitude.Stance != SocialStance.Accept)
        {
            Events.Add(receiver + (attitude.Stance == SocialStance.Reject ? "：不接受这次具名确认" : "：要求补充确认依据"));
            return;
        }
        Observation.Observe(Souls[receiver], new(signal.Name, signal.Kind)
        {
            Target = signal, Source = ObservationSource.Testimony, Time = Runtime.CTB.Now,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(tag, 1), AddRelations = [(Relations.Subject, speaker)], Details = details
        });
        Runtime.CTB.RunUntil(Runtime.CTB.Now);
    }

    private Projection RememberProcedure(string actor, RedTokenOperation operation, Effect effect, string description)
    {
        var previous = _procedures.GetValueOrDefault((actor, operation));
        if (previous?.Procedure is { } procedure && Ast.Equivalent(procedure, effect)) return previous;
        var learned = Observation.Observe(Souls[actor], new(description, ProjectionKind.Procedure)
        {
            Target = previous, Source = ObservationSource.Introspection, Time = Runtime.CTB.Now,
            Existence = Truth.True, Procedure = effect, Details = "对当前已知器具使用本人掌握的操作流程。"
        });
        _procedures[(actor, operation)] = learned;
        return learned;
    }

    public RedTokenCooperation RequestCooperation(string actor, string sender, RedTokenOperation operation, string description)
    {
        var self = KnownObject(actor, actor)!;
        var speaker = KnownObject(actor, sender);
        var request = Observation.Observe(Souls[actor], new(description, ProjectionKind.Event)
        {
            Source = actor == sender ? ObservationSource.Introspection : ObservationSource.Testimony,
            Time = Runtime.CTB.Now, Existence = Truth.True,
            AddRelations = speaker is null ? [] : [(Relations.Subject, speaker)], Details = description
        });
        var attitude = speaker is null ? new SocialAttitude(SocialStance.Negotiate, 0) : SocialSystem.Evaluate(Souls[actor].World, self, request);
        Effect intention = new Effect.Think(new Condition.Related(self, Intends, request));
        var skill = operation is RedTokenOperation.KnockGap or RedTokenOperation.KnockPipe ? AcousticSkill :
            operation == RedTokenOperation.VerifyReward ? actor switch
            { "工程师霍铎" => MechanicalSkill, "主厨沈鼎" => SupplySkill, "音乐家苏泠" => AcousticSkill, _ => InquirySkill } : MechanicalSkill;
        var accepted = attitude.Stance == SocialStance.Accept &&
            (Kind != RedTokenWorldKind.Scene || operation is RedTokenOperation.VerifyReward or RedTokenOperation.Accompany || HasKnowledge(actor, RewardVerified));
        var goal = self.Relations.FirstOrDefault(relation => relation.Relation == HasGoal).Target;
        var rules = new List<Rule>();
        if (accepted)
        {
            rules.Add(new Rule(new Condition.All([new Condition.Tagged(request, RequestAccepted, 1),
                new Condition.Tagged(self, skill, 1)]), RuleMode.Prefer, intention, 4, Truth.True));
            if (goal is not null)
                rules.Add(new Rule(new Condition.All([new Condition.Tagged(request, RequestAccepted, 1),
                    new Condition.Related(self, HasGoal, goal)]), RuleMode.Prefer, intention, 2, Truth.True));
        }
        Observation.Observe(Souls[actor], new(request.Name, request.Kind)
        {
            Target = request, Source = ObservationSource.Introspection,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(RequestAccepted, accepted ? 1 : 0),
            Procedure = accepted ? intention : null,
            Rules = rules.ToImmutableArray()
        });
        var decision = accepted ? DecisionSystem.Choose(Souls[actor].World, self) : null;
        if (decision?.Basis != request) decision = null;
        return new(actor, request, decision, attitude);
    }

    public RedTokenResult CommitCooperation(RedTokenCooperation agreement)
    {
        if (agreement.Decision is not { } decision) return new(false, "尚未接受这项协作请求。");
        try
        {
            var execution = Runtime.Submit(Souls[agreement.Actor], new(KnownObject(agreement.Actor, agreement.Actor)!,
                decision.Effect, 0, Basis: decision.Basis));
            Runtime.CTB.RunUntil(Runtime.CTB.Now);
            if (execution.Status == ExecutionStatus.Completed)
                Observation.Observe(Souls[agreement.Actor], new(agreement.Request.Name, agreement.Request.Kind)
                {
                    Target = agreement.Request, Source = ObservationSource.Introspection,
                    Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Responded, 1).Add(RequestAccepted, 0),
                    Rules = [], ClearProcedure = true, Details = "已接受并形成意图，等实际操作结果后记录履约。"
                });
            return new(execution.Status == ExecutionStatus.Completed, execution.Status == ExecutionStatus.Completed ? "本人接受请求。" : "本人当前无法接手。");
        }
        catch (InvalidOperationException exception) { return new(false, exception.Message); }
    }

    public void FinishCooperation(RedTokenCooperation agreement, bool fulfilled)
    {
        var self = KnownObject(agreement.Actor, agreement.Actor)!;
        Observation.Observe(Souls[agreement.Actor], new(self.Name, self.Kind)
        { Target = self, Source = ObservationSource.Introspection, RemoveRelations = [(Intends, agreement.Request)] });
        Observation.Observe(Souls[agreement.Actor], new(agreement.Request.Name, agreement.Request.Kind)
        {
            Target = agreement.Request, Source = ObservationSource.Introspection, Time = Runtime.CTB.Now,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Responded, 1).Add(RequestAccepted, 0).Add(Tags.Success, fulfilled ? 1 : 0),
            Rules = [], ClearProcedure = true, Details = fulfilled ? "已完成此次请求。" : "本次请求暂缓或取消，未宣告完成。"
        });
    }
}
