using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;

namespace DualOntologyWorldEngine.Core;

/// <summary>引擎核心通用基础动作系统 (Base Actions)。</summary>
/// <remarks>
/// 为世界中的角色提供通用的物理交互与意图构造器。
/// 所有动作意图生成标准的 <see cref="Intent"/> 派生实例，分类归入物理、认知、社交、契约与生理五大基础体系，
/// 无缝接入动作运行时（ActionRuntime）、规则审查与因果事件流。
/// </remarks>
public static class BaseActions
{
    /// <summary>获取实体的关联心智主体，优先使用显式传入或已绑定的 Soul。</summary>
    public static Soul GetActorMind(Entity actor, Soul? mind = null)
    {
        if (mind != null) return mind;
        if (actor is Character character && character.Mind != null)
        {
            return character.Mind;
        }
        var soulEntry = actor.Entries("Soul").FirstOrDefault();
        if (soulEntry?.Value is Soul linkedSoul)
        {
            return linkedSoul;
        }
        throw new InvalidOperationException($"{actor.Name} 未绑定 Soul；请在内容装配阶段显式创建并绑定心智。");
    }

    private static Intent Make(
        Entity actor,
        Soul? mind,
        Effect effect,
        Action? action = null,
        Expr? obs = null,
        string label = "",
        Func<bool>? validate = null,
        double cost = 1.0,
        ResponseMode? mode = null)
    {
        var m = GetActorMind(actor, mind);
        return new Intent(
            actor,
            m,
            effect,
            cost,
            resolve: () =>
            {
                action?.Invoke();
                if (obs != null) m.Observe(obs);
                return true;
            },
            label: label,
            validate: validate,
            responseMode: mode);
    }

    /// <summary>只执行到目标路径的下一场景，重新发现机会后再继续；默认支付该通道的代价。</summary>
    public static Intent CreateMoveIntent(Entity world, Entity actor, Entity destination, double? cost = null, Soul? mind = null)
    {
        var start = TopologySystem.GetEnclosingScene(actor);
        var passages = world.GetTopology()?.Passages ?? TopologySystem.CollectPassages(world);
        var (passage, next) = start == null ? (null, null)
            : TopologySystem.GetNextStep(start, destination, passages, actor);
        return Make(actor, mind, AST.F("move", actor, next ?? destination),
            () => actor.Reparent(next), next == null ? null : AST.E("located_at", actor, next),
            $"移动：{actor.Name}前往{next?.Name ?? destination.Name}",
            () => start != null && passage != null && next != null
                && TopologySystem.GetEnclosingScene(actor) == start
                && passages.Contains(passage) && passage.Allows(start, actor),
            cost ?? (start != null && passage != null ? passage.CostFrom(start) : 1));
    }

    #region 强类型实体状态扩展方法

    /// <summary>判断实体是否为容器（具有 Container 标签或角色）。</summary>
    public static bool IsContainer(this Entity entity) =>
        entity.HasTag(CommonTag.Container) || entity.HasRole(Role.Container);

    /// <summary>判断实体是否为可开合对象（具有 Openable、Door 标签或 AccessControl 角色）。</summary>
    public static bool IsOpenable(this Entity entity) =>
        entity.HasTag(CommonTag.Openable) || entity.HasTag(CommonTag.Door) || entity.HasRole(Role.AccessControl);

    /// <summary>判断实体当前是否处于被持握激活状态。</summary>
    public static bool IsHeld(this Entity entity) =>
        entity.HasTag(CommonTag.Held);

    /// <summary>判断实体是否为可阅读的记录/载体/书籍。</summary>
    public static bool IsReadable(this Entity entity) =>
        entity.HasTag(CommonTag.Readable) ||
        entity.HasTag(CommonTag.Record) ||
        entity.HasRole(Role.RecordMedium) ||
        entity.HasTag("ReadingMaterial") ||
        entity.HasTag("Book") ||
        entity.HasTag("Document");

    #endregion

    public static Intent CreateTakeIntent(Entity actor, Entity item, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("take", actor, item), () => item.PickUp(actor), AST.E("holds", actor, item), $"拾取：{actor.Name}获得{item.Name}", () => item.Parent == actor.Parent, cost);

    public static Intent CreateDropIntent(Entity actor, Entity item, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("drop", actor, item), () => item.Drop(), (actor.Parent as Entity) != null ? AST.E("located_at", item, (Entity)actor.Parent!) : null, $"丢弃：{actor.Name}放下{item.Name}", () => item.Parent == actor, cost);

    public static Intent CreateStoreIntent(Entity actor, Entity item, Entity container, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("store", actor, item, container), () => item.PutInto(container, fromScene: actor.Parent as Entity), AST.E("stored", item, container), $"收纳：{actor.Name}将{item.Name}存入{container.Name}", () => item.Parent == actor && container.Parent == actor.Parent && container.IsContainer() && container.IsAccessible(actor.Parent as Entity), cost);

    public static Intent CreateRetrieveIntent(Entity actor, Entity item, Entity container, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("retrieve", actor, item, container), () => item.TakeFrom(container, actor, fromScene: actor.Parent as Entity), AST.E("holds", actor, item), $"取出：{actor.Name}从{container.Name}拿出{item.Name}", () => item.Parent == container && container.Parent == actor.Parent && container.IsContainer() && container.IsAccessible(actor.Parent as Entity), cost);

    public static Intent CreateOpenIntent(Entity actor, Entity target, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("open", actor, target), () => target.Open(actor.Parent as Entity), AST.E("opened", target), $"打开：{actor.Name}开启{target.Name}", () => target.IsOpenable() && !target.IsOpen && !target.IsLocked(actor.Parent as Entity), cost);

    public static Intent CreateCloseIntent(Entity actor, Entity target, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("close", actor, target), () => target.Close(), AST.E("closed", target), $"关闭：{actor.Name}合上{target.Name}", () => target.IsOpenable() && target.IsOpen, cost);

    public static Intent CreateUnlockIntent(Entity actor, Entity target, Entity key, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("unlock", actor, target), () => target.Unlock(side: null, source: key), AST.E("unlocked", target), $"解锁：{actor.Name}使用{key.Name}开启{target.Name}的锁", () => key.Parent == actor && target.IsLocked(actor.Parent as Entity), cost);

    public static Intent CreateLockIntent(Entity actor, Entity target, Entity key, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("lock", actor, target), () => target.Lock(1, side: null, source: key), AST.E("locked", target), $"上锁：{actor.Name}使用{key.Name}锁上{target.Name}", () => key.Parent == actor && !target.IsLocked(actor.Parent as Entity), cost);

    public static Intent CreateHoldIntent(Entity actor, Entity item, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("hold", actor, item), () => { foreach (var o in actor.Items.OfType<Entity>()) { if (!ReferenceEquals(o, item)) o.Unhold(); } item.Hold(actor); }, AST.E("ready_weapon_or_tool", actor, item), $"持握：{actor.Name}持握{item.Name}", () => item.Parent == actor && !item.IsHeld(), cost);

    public static Intent CreateUnholdIntent(Entity actor, Entity item, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("unhold", actor, item), () => item.Unhold(), null, $"收起：{actor.Name}收起{item.Name}", () => item.Parent == actor && item.IsHeld(), cost);

    public static Intent CreateExamineIntent(Entity actor, Entity target, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("examine", actor, target), () => { var data = target.Examine(actor); if (data.TryGetValue("claims", out var claimsObj) && claimsObj is System.Collections.IEnumerable claims) { foreach (var claim in claims) GetActorMind(actor, mind).Observe(AST.E("fact", claim?.ToString() ?? "")); } }, AST.E("examined", target), $"检视：{actor.Name}仔细观察{target.Name}", () => target.Parent == actor || target.Parent == actor.Parent || target.Parent?.Parent == actor.Parent, cost, ResponseMode.Verify);

    public static Intent CreateReadIntent(Entity actor, Entity target, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("read", actor, target), () => { var content = target.Get<string>("content"); if (!string.IsNullOrEmpty(content)) GetActorMind(actor, mind).Observe(AST.E("text", content)); foreach (var claim in target.Entries("Claim")) GetActorMind(actor, mind).Observe(AST.E("claim", claim.Value?.ToString() ?? "")); }, AST.E("read", target), $"阅读：{actor.Name}翻阅{target.Name}", () => (target.Parent == actor || target.Parent == actor.Parent) && target.IsReadable(), cost, ResponseMode.Verify);

    public static Intent CreateUseIntent(Entity actor, Entity tool, Entity? target = null, double cost = 1.0, Soul? mind = null, Action? useEffect = null) =>
        Make(actor, mind, AST.F("use", actor, tool), useEffect, AST.E("used", tool, target ?? (object)tool), $"使用：{actor.Name}使用{tool.Name}" + (target != null ? $"于{target.Name}" : ""), () => tool.Parent == actor || tool.Parent == actor.Parent, cost);

    public static Intent CreateConsumeIntent(Entity actor, Entity consumable, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("consume", actor, consumable), () => { consumable.Consume(); if (consumable.Has("Food") || consumable.Has("Drink")) { actor.ChangeTag("Panic", -1); GetActorMind(actor, mind).ChangeTag("Stability", 1); } }, AST.E("consumed", consumable), $"消耗：{actor.Name}使用{consumable.Name}", () => (consumable.Parent == actor || consumable.Parent == actor.Parent) && consumable.Has("Consumable") && consumable.Get<int>("doses") > 0, cost);

    public static Intent CreateRestIntent(Entity actor, Entity restingPlace, double cost = 2.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("rest", actor, restingPlace), () => { actor.ChangeTag("Panic", -2); GetActorMind(actor, mind).ChangeTag("Stability", 2); }, AST.E("rested", restingPlace), $"休息：{actor.Name}在{restingPlace.Name}休憩", () => restingPlace.Parent == actor.Parent && (restingPlace.Has("RestingPlace") || restingPlace.Has("Bed") || restingPlace.Has("Resting")), cost);

    public static Intent CreateTransferIntent(Entity giver, Entity receiver, Entity item, double cost = 1.0, Soul? mind = null) =>
        Make(giver, mind, AST.F("transfer", giver, item), () => { item.Reparent(receiver); item.Unhold(); if (receiver is Character c && c.Mind != null) c.Mind.Observe(AST.E("received", item, giver)); }, AST.E("transferred", item, receiver), $"交接：{giver.Name}将{item.Name}递给{receiver.Name}", () => item.Parent == giver && giver.Parent == receiver.Parent, cost);

    public static Intent CreateSurveyDetailIntent(Entity actor, Entity target, Entity tool, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("examine_detail", actor, target), null, AST.E("forensic_clue_verified", target, tool), $"深度勘查：{actor.Name}借助{tool.Name}微观勘查{target.Name}", () => tool.Parent == actor, cost);

    public static Intent CreateSearchContainerIntent(Entity actor, Entity container, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("search_container", actor, container), null, AST.E("container_searched", container), $"搜查容器：{actor.Name}翻找{container.Name}", () => container.Parent == actor.Parent && container.IsContainer(), cost);

    public static Intent CreateMaintainMechanismIntent(Entity actor, Entity facility, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("maintain_mechanism", actor, facility), () => facility.SetTag("MaintenanceDone", 1), AST.E("maintained", facility), $"检修机构：{actor.Name}维护调校{facility.Name}", () => facility.Parent == actor.Parent && !facility.Has("MaintenanceDone"), cost);

    public static Intent CreateChemicalAnalyzeIntent(Entity actor, Entity target, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("analyze_substance", actor, target), null, AST.E("chemical_purity_verified", target), $"化验分析：{actor.Name}化验检测{target.Name}", () => target.Parent == actor || target.Parent == actor.Parent, cost);

    public static Intent CreatePurifyFacilityIntent(Entity actor, Entity facility, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("purify_facility", actor, facility), () => { facility.SetTag("Hazard", 0); facility.SetTag("Tainted", 0); }, AST.E("purified", facility), $"协同净化：{actor.Name}净化受污染的{facility.Name}", () => facility.Parent == actor.Parent && (facility.Has("Hazard") || facility.Has("Tainted")), cost);

    public static Intent CreateSupportItemIntent(Entity giver, Entity receiver, Entity item, double cost = 1.0, Soul? mind = null) =>
        Make(giver, mind, AST.F("support_peer_item", giver, receiver), () => { item.Reparent(receiver); if (receiver is Character c && c.Mind != null) c.Mind.Observe(AST.E("received_support_item", giver, item)); }, AST.E("supported_with_item", receiver, item), $"物资互援：{giver.Name}赠予{receiver.Name}道具{item.Name}", () => item.Parent == giver && giver.Parent == receiver.Parent, cost);

    public static Intent CreatePatrolPactIntent(Entity actor, Entity partner, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("form_patrol_pact", actor, partner), () => { if (partner is Character c && c.Mind != null) c.Mind.Observe(AST.E("allied_patrol", partner, actor)); }, AST.E("allied_patrol", actor, partner), $"同盟守望：{actor.Name}与{partner.Name}结为巡查搭档", () => actor.Parent == partner.Parent && !ReferenceEquals(actor, partner), cost);

    public static Intent CreateConfrontAlibiIntent(Entity interrogator, Entity suspect, double cost = 1.0, Soul? mind = null) =>
        Make(interrogator, mind, AST.F("confront_alibi", interrogator, suspect), null, AST.E("alibi_verified", suspect), $"盘查行踪：{interrogator.Name}核对{suspect.Name}的动向与物证", () => interrogator.Parent == suspect.Parent && !ReferenceEquals(interrogator, suspect), cost);

    public static Intent CreateRefuteArgumentIntent(Entity speaker, Entity target, double cost = 1.0, Soul? mind = null) =>
        Make(speaker, mind, AST.F("refute_argument", speaker, target), null, AST.E("rebutted_bias", target), $"反论斩击：{speaker.Name}当面驳斥{target.Name}的绝望偏见", () => speaker.Parent == target.Parent && !ReferenceEquals(speaker, target), cost);

    public static Intent CreateTrapWithTruthIntent(Entity actor, Entity demon, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("trap_demon_logic", actor, demon), null, AST.E("demon_truth_trapped", demon), $"真言诱捕：{actor.Name}以排中律诱问{demon.Name}", () => actor.Parent == demon.Parent, cost);

    public static Intent CreateVoidClauseIntent(Entity actor, Entity pact, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("nullify_pact_clause", actor, pact), () => pact.SetTag("Pact", 0), AST.E("pact_clause_voided", pact), $"法理废止：{actor.Name}依据上位法理宣告{pact.Name}无效", () => pact.Parent == actor || pact.Parent == actor.Parent, cost);

    public static Intent CreateDisarmHazardIntent(Entity actor, Entity hazard, Entity? tool = null, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("disarm_hazard", actor, hazard), () => hazard.SetTag("Hazard", 0), AST.E("hazard_disarmed", hazard), $"排险解除：{actor.Name}排除{hazard.Name}危险隐患", () => hazard.Parent == actor.Parent && hazard.Has("Hazard"), cost);

    public static Intent CreateStowEvidenceIntent(Entity actor, Entity evidence, Entity container, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("stow_evidence", actor, evidence, container), () => evidence.PutInto(container, fromScene: actor.Parent as Entity), AST.E("evidence_safeguarded", evidence, container), $"物证封存：{actor.Name}将物证{evidence.Name}存入{container.Name}", () => evidence.Parent == actor && container.Parent == actor.Parent && container.IsContainer(), cost);

    public static Intent CreateReinforceDoorIntent(Entity actor, Entity doorOrGate, Entity? material = null, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("reinforce_door", actor, doorOrGate), () => doorOrGate.SetTag("Reinforced", 1), AST.E("door_reinforced", doorOrGate), $"加固门禁：{actor.Name}加固防御{doorOrGate.Name}", () => doorOrGate.Parent == actor.Parent, cost);

    public static Intent CreateTrackTraceIntent(Entity actor, Entity sceneOrTarget, Entity? tool = null, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("track_trace", actor, sceneOrTarget), null, AST.E("traces_tracked", sceneOrTarget), $"痕迹追踪：{actor.Name}细勘{sceneOrTarget.Name}残留动线与扰动", () => sceneOrTarget.Parent == actor.Parent || ReferenceEquals(sceneOrTarget, actor.Parent), cost);

    public static Intent CreateCrossExamineIntent(Entity actor, Entity clueA, Entity clueB, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("cross_examine", actor, clueA, clueB), null, AST.E("clues_correlated", clueA, clueB), $"物证比对：{actor.Name}交叉比对{clueA.Name}与{clueB.Name}", () => (clueA.Parent == actor || clueA.Parent == actor.Parent) && (clueB.Parent == actor || clueB.Parent == actor.Parent), cost);

    public static Intent CreateDecodeCipherIntent(Entity actor, Entity cipherTarget, Entity? tool = null, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("decode_cipher", actor, cipherTarget), () => cipherTarget.SetTag("Decoded", 1), AST.E("cipher_decoded", cipherTarget), $"破译密文：{actor.Name}破译{cipherTarget.Name}隐秘暗号", () => cipherTarget.Parent == actor || cipherTarget.Parent == actor.Parent, cost);

    public static Intent CreateInterrogateMotiveIntent(Entity interrogator, Entity subject, double cost = 1.0, Soul? mind = null) =>
        Make(interrogator, mind, AST.F("interrogate_motive", interrogator, subject), null, AST.E("motive_interrogated", subject), $"动机深探：{interrogator.Name}探查{subject.Name}心理动机", () => interrogator.Parent == subject.Parent && !ReferenceEquals(interrogator, subject), cost, ResponseMode.Verify);

    public static Intent CreateExchangeTestimonyIntent(Entity speaker, Entity listener, Entity clue, double cost = 1.0, Soul? mind = null) =>
        Make(speaker, mind, AST.F("exchange_testimony", speaker, listener), () => { if (listener is Character c && c.Mind != null) c.Mind.Observe(AST.E("testimony_received", speaker, clue)); }, AST.E("testimony_shared", listener, clue), $"交换证言：{speaker.Name}与{listener.Name}共享线索{clue.Name}", () => speaker.Parent == listener.Parent && !ReferenceEquals(speaker, listener), cost, ResponseMode.Cooperate);

    public static Intent CreateExposeContradictionIntent(Entity challenger, Entity contradictoryPeer, Entity evidence, double cost = 1.0, Soul? mind = null) =>
        Make(challenger, mind, AST.F("expose_contradiction", challenger, contradictoryPeer), null, AST.E("contradiction_exposed", contradictoryPeer, evidence), $"揭露矛盾：{challenger.Name}以物证{evidence.Name}指出{contradictoryPeer.Name}言行矛盾", () => challenger.Parent == contradictoryPeer.Parent && !ReferenceEquals(challenger, contradictoryPeer), cost, ResponseMode.Negotiate);

    public static Intent CreateInscribeTalismanIntent(Entity mystic, Entity target, Entity material, double cost = 1.0, Soul? mind = null) =>
        Make(mystic, mind, AST.F("inscribe_talisman", mystic, target), () => target.SetTag("Warded", 1), AST.E("ward_inscribed", target), $"铭刻符印：{mystic.Name}在{target.Name}施加镇魂言灵", () => (target.Parent == mystic || target.Parent == mystic.Parent) && material.Parent == mystic, cost, ResponseMode.Cooperate);

    public static Intent CreateCounterDemonBargainIntent(Entity actor, Entity demon, Entity pact, double cost = 1.0, Soul? mind = null) =>
        Make(actor, mind, AST.F("counter_demon_bargain", actor, demon), () => pact.SetTag("Contested", 1), AST.E("demon_bargain_countered", demon, pact), $"抗辩魔约：{actor.Name}依据上位誓约严正拒绝{demon.Name}的交易", () => actor.Parent == demon.Parent, cost, ResponseMode.Negotiate);

    public static Intent CreateCookFoodIntent(Entity chef, Entity kitchenOrStove, Entity? ingredient = null, double cost = 1.0, Soul? mind = null) =>
        Make(chef, mind, AST.F("cook_food", chef, kitchenOrStove), null, AST.E("nourishment_prepared", kitchenOrStove), $"烹饪热食：{chef.Name}使用{kitchenOrStove.Name}制备热量配给", () => kitchenOrStove.Parent == chef.Parent, cost);

    public static Intent CreateBrewTonicIntent(Entity pharmacist, Entity facilityOrBench, Entity? herb = null, double cost = 1.0, Soul? mind = null) =>
        Make(pharmacist, mind, AST.F("brew_tonic", pharmacist, facilityOrBench), null, AST.E("tonic_brewed", facilityOrBench), $"调制药剂：{pharmacist.Name}使用{facilityOrBench.Name}调制醒神汤剂", () => facilityOrBench.Parent == pharmacist.Parent, cost);

    public static Intent CreateBandageWoundIntent(Entity medic, Entity patient, Entity bandage, double cost = 1.0, Soul? mind = null) =>
        Make(medic, mind, AST.F("bandage_wound", medic, patient), () => { bandage.Consume(); if (patient is Character c && c.Mind != null) c.Mind.AdjustEmotion(Emotion.Serenity, 2, "受到急救救治"); }, AST.E("patient_treated", patient), $"急救包扎：{medic.Name}为{patient.Name}包扎清创", () => medic.Parent == patient.Parent && bandage.Parent == medic, cost);
}
