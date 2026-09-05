using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using DualOntologyWorldEngine.Core.Timeline;

namespace DualOntologyWorldEngine.Core.Content;

/// <summary>Core 提供的灵异物品原型。具体作品只需放置、命名并关联目标。</summary>
public enum HauntedItem
{
    Talisman,
    Salt,
    Candle,
    Bell,
    Mirror,
    RedThread,
    NameSlip,
    Relic,
    RulePaper,
    Recorder,
    SealBox,
    Effigy
}

/// <summary>
/// 默认灵异物品装配和能力候选工厂。
/// 能力的世界变化全部由声明式 Effect 交给 ActionRuntime 结算。
/// </summary>
public static class HauntedItems
{
    public static Entity Create(HauntedItem kind, string name, Entity parent, Entity? target = null)
    {
        var properties = new Dictionary<string, object>
        {
            ["item_kind"] = kind,
            ["integrity"] = kind switch
            {
                HauntedItem.Mirror => Balance.MirrorIntegrity,
                HauntedItem.SealBox => Balance.SealBoxCapacity,
                HauntedItem.Effigy => 1.0,
                HauntedItem.Recorder => 2.0,
                _ => 1.0
            },
            ["charges"] = kind switch
            {
                HauntedItem.Talisman => Balance.TalismanCharges,
                HauntedItem.Salt => Balance.SaltCharges,
                HauntedItem.Bell => Balance.BellCharges,
                HauntedItem.RedThread => Balance.RedThreadCharges,
                HauntedItem.SealBox => Balance.SealBoxCapacity,
                HauntedItem.Effigy => Balance.EffigyCharges,
                _ => 0.0
            },
            ["fuel"] = kind == HauntedItem.Candle ? Balance.CandleFuel : 0.0,
            ["public_fields"] = PublicFields(kind)
        };

        switch (kind)
        {
            case HauntedItem.Candle:
                properties["lit"] = false;
                properties["lit_target"] = null!;
                properties["light_owner"] = null!;
                break;
            case HauntedItem.Recorder:
                properties["battery"] = Balance.RecorderBattery;
                properties["capacity"] = Balance.RecorderCapacity;
                properties["recording_subject"] = null!;
                properties["recording_text"] = "";
                break;
            case HauntedItem.SealBox:
                properties["sealed_object"] = null!;
                properties["sealed_parent"] = null!;
                break;
            case HauntedItem.RedThread:
                properties["linked"] = false;
                properties["endpoint_a"] = null!;
                properties["endpoint_b"] = null!;
                break;
        }

        var item = new Entity(name, parent, properties: properties);
        item.Set("item_kind", kind);
        if (target != null)
        {
            item.Set("target", target);
            if (kind == HauntedItem.NameSlip) item.Set("referent", target);
            item.Relate("target", target);
        }
        return item;
    }

    /// <summary>
    /// 从角色随身物品和当前场景发现默认能力。
    /// 候选只声明效果和资源条件，不在发现阶段写入世界。
    /// </summary>
    public static IEnumerable<Intent> Offers(
        Soul mind,
        Entity actor,
        RuleEnvironment env,
        IReadOnlyDictionary<string, object>? context = null)
    {
        var items = actor.Items.OfType<Entity>().Concat(SceneEntities(actor))
            .Where(IsItem).Distinct().ToArray();
        var targets = Targets(actor, context).ToArray();

        foreach (var item in items)
        {
            var kind = item.Get<HauntedItem>("item_kind");
            var associated = item.Get<Entity>("target");

            if (kind is HauntedItem.Talisman or HauntedItem.Salt)
            {
                var field = kind == HauntedItem.Talisman ? "protected" : "warded";
                var label = kind == HauntedItem.Talisman ? "以符纸护住目标" : "撒盐守住边界";
                foreach (var target in targets)
                    yield return IntentFor(actor, mind, item, target, "protect",
                        [F("spend", item, "charges", 1.0), F("set", target, field, true), F("set", target, field + "_by", item)],
                        [Ne(target, field, true)], label);
            }

            if (kind == HauntedItem.Candle)
            {
                foreach (var target in targets)
                    yield return IntentFor(actor, mind, item, target, "light",
                        [F("spend", item, "fuel", 1.0), F("set", actor, "candle", item),
                         F("set", item, "lit", true), F("set", item, "lit_target", target),
                         F("set", item, "light_owner", actor), F("relate", target, "IlluminatedBy", item),
                         F("set", target, "illuminated", true)],
                        [Ne(item, "lit", true)], "点燃烛火");
            }

            if (kind == HauntedItem.Bell)
            {
                foreach (var target in targets)
                    yield return IntentFor(actor, mind, item, target, "ring",
                        [F("spend", item, "charges", 1.0), F("set", target, "startled", true),
                         F("set", target, "revealed", true)],
                        [], "摇铃惊动在场者");
            }

            if (kind == HauntedItem.Mirror)
            {
                yield return IntentFor(actor, mind, item, actor, "activate_mirror",
                    [F("set", actor, "mirror", item)],
                    [Gte(item, "integrity", 1.0), Eq(actor, "mirror", null!)], "举镜对准自身");
            }

            if (kind == HauntedItem.RedThread)
            {
                foreach (var target in targets)
                    yield return IntentFor(actor, mind, item, target, "relink",
                        [F("spend", item, "charges", 1.0), F("set", item, "endpoint_a", actor),
                         F("set", item, "endpoint_b", target), F("set", item, "linked", true),
                         F("relate", item, "endpoint", actor), F("relate", item, "endpoint", target)],
                        [Eq(item, "linked", false)], "用红线系住关系");
                if (item.Get<Entity>("endpoint_a") != null && item.Get<Entity>("endpoint_b") != null)
                    yield return IntentFor(actor, mind, item, item, "cut",
                        [F("set", item, "linked", false), F("set", item, "endpoint_a", null!),
                         F("set", item, "endpoint_b", null!), F("unrelate", item, "endpoint", item.Get<Entity>("endpoint_a")!),
                         F("unrelate", item, "endpoint", item.Get<Entity>("endpoint_b")!)],
                        [Eq(item, "linked", true)], "剪断红线");
            }

            if (kind == HauntedItem.NameSlip || kind == HauntedItem.Relic || kind == HauntedItem.RulePaper)
            {
                var target = kind == HauntedItem.NameSlip ? item.Get<Entity>("referent") : associated;
                if (target != null && targets.Contains(target) && HasInvestigableState(target))
                    yield return IntentFor(actor, mind, item, target, "reveal",
                        [F("observe", mind, target, "evidence"), F("set", actor, "last_clue", item),
                         F("relate", actor, "Examined", item)],
                        [HasValue(target, "evidence"), Ne(target, "evidence", ""),
                         AST.E("any", Ne(target, "occult_evidence", true), Eq(target, "illuminated", true), Eq(target, "revealed", true)),
                         Not(Contains(Value(actor, "rel:Examined"), item))], "查阅线索");
            }

            if (kind == HauntedItem.Recorder)
            {
                foreach (var target in targets)
                {
                    if (target.Get<object>("statement") == null) continue;
                    yield return IntentFor(actor, mind, item, target, "record",
                        [F("spend", item, "battery", 1.0), F("spend", item, "capacity", 1.0),
                         F("set", item, "recording_subject", target), F("set", item, "recording_text", Value(target, "statement"))],
                        [
                         HasValue(target, "statement"), Ne(target, "statement", "")], "录下这句话");
                }

                yield return IntentFor(actor, mind, item, item, "play",
                        [F("spend", item, "battery", 1.0), F("observe", mind, item, "recording_subject"),
                         F("observe", mind, item, "recording_text"), F("set", actor, "heard_recording", item)],
                        [HasValue(item, "recording_subject"),
                         Ne(item, "recording_text", "")], "重放录音");
            }

            if (kind == HauntedItem.SealBox)
            {
                foreach (var target in targets)
                {
                    if (!IsSmallObject(target)) continue;
                    yield return IntentFor(actor, mind, item, target, "seal",
                        [F("spend", item, "charges", 1.0), F("set", target, "sealed", true),
                         F("set", target, "seal_source", item),
                         F("set", item, "sealed_parent", Value(target, "$parent")),
                         F("set", item, "sealed_object", target), F("move", target, item)],
                        [Eq(item, "sealed_object", null!),
                         Ne(target, "sealed", true)], "把目标封进盒中");
                }
                var sealedObject = item.Get<Entity>("sealed_object");
                if (sealedObject != null)
                    yield return IntentFor(actor, mind, item, sealedObject, "unseal",
                        [F("set", sealedObject, "sealed", false), F("set", sealedObject, "seal_source", null!),
                         F("set", item, "sealed_object", null!), F("move", sealedObject, item.Get<Entity>("sealed_parent")!)],
                        [Eq(item, "sealed_object", sealedObject), Eq(sealedObject, "sealed", true),
                         Eq(sealedObject, "seal_source", item)], "打开自己的封盒");
            }

            if (kind == HauntedItem.Effigy)
            {
                foreach (var target in targets)
                    yield return IntentFor(actor, mind, item, target, "activate_substitute",
                        [F("set", actor, "substitute", item),
                         F("set", item, "substitute_target", target)],
                        [Gte(item, "charges", 1.0), Eq(actor, "substitute", null!)], "让纸人替你承受");
            }
        }
    }

    /// <summary>安装烛火的连续阶段监听。每个阶段都由一个声明式 Intent 支付燃料；耗尽时产生熄灭事件。</summary>
    public static Action Install(SimulationSession session)
    {
        var scheduled = new HashSet<Func<double?>>();
        var installed = true;

        void Schedule(double delay, string label, Func<double?> callback)
        {
            Func<double?>? wrapped = null;
            wrapped = () =>
            {
                var next = callback();
                if (!next.HasValue && wrapped != null) scheduled.Remove(wrapped);
                return next;
            };
            scheduled.Add(wrapped);
            session.Timeline.Schedule(delay, label, wrapped);
        }

        Action<Intent, ActionResolution> handler = (intent, resolution) =>
        {
            if (!resolution.Allowed || resolution.Result is not { Succeeded: true }) return;
            var item = intent.Tool;
            var target = intent.Target;
            if (item == null || target == null || !item.Properties.TryGetValue("item_kind", out var rawKind) || rawKind is not HauntedItem kind) return;

            if (intent.Effect.Op == "light" && kind == HauntedItem.Candle)
                Schedule(Balance.CandleMaintenanceIntervalAv, $"维持烛火：{item.Name}",
                    () => MaintainCandle(session, item, intent.Mind, target));
            else if (intent.Effect.Op == "protect" && kind is HauntedItem.Talisman or HauntedItem.Salt)
                Schedule(Balance.CandleMaintenanceIntervalAv, $"保护失效：{item.Name}",
                    () => ExpireProtection(session, item, intent.Mind, target, kind));
            else if (intent.Effect.Op == "seal" && kind == HauntedItem.SealBox)
                Schedule(Balance.CandleMaintenanceIntervalAv, $"维持封存：{item.Name}",
                    () => MaintainSeal(session, item, intent.Mind, target));
        };

        session.Runtime.Executed += handler;
        return () =>
        {
            if (!installed) return;
            installed = false;
            session.Runtime.Executed -= handler;
            session.Timeline.Cancel(task => scheduled.Contains(task.Action));
            scheduled.Clear();
        };
    }

    private static double? MaintainCandle(SimulationSession session, Entity candle, Soul mind, Entity target)
    {
        if (!candle.Get<bool>("lit") || candle.Get<Entity>("lit_target") == null) return null;
        var placement = candle.Parent as Entity ?? candle;
        var sustain = new Intent(candle, mind, F("maintain_light", candle, target),
            [F("spend", candle, "fuel", 1.0), F("set", target, "illuminated", true)],
            motionCost: Balance.CandleMaintenanceIntervalAv, label: "维持烛火",
            requirements: ItemRequirements(placement, candle,
                Eq(candle, "lit", true), Eq(candle, "lit_target", target),
                Contains(Value(target, "rel:IlluminatedBy"), candle)));
        var result = session.Runtime.ResolveDetailed(sustain, new Dictionary<string, object> { ["cause"] = candle });
        if (result.Allowed && result.Result is { Succeeded: true }) return Balance.CandleMaintenanceIntervalAv;

        var owner = candle.Get<Entity>("light_owner");
        var expiredEffects = new List<Effect>
        {
            F("unrelate", target, "IlluminatedBy", candle),
            F("when", Eq(target, "rel:IlluminatedBy", Array.Empty<object>()), F("set", target, "illuminated", false)),
            F("set", candle, "lit", false), F("set", candle, "lit_target", null!)
        };
        if (owner != null) expiredEffects.Add(F("set", owner, "candle", null!));
        var extinguish = new Intent(candle, mind, F("light_expired", candle, target), expiredEffects,
            motionCost: Balance.CandleExtinguishAv, label: "烛油耗尽，火光熄灭",
            requirements: []);
        session.Runtime.ResolveDetailed(extinguish, new Dictionary<string, object> { ["cause"] = candle });
        return null;
    }

    private static double? ExpireProtection(SimulationSession session, Entity item, Soul mind, Entity target, HauntedItem kind)
    {
        var field = kind == HauntedItem.Talisman ? "protected_by" : "warded_by";
        var active = kind == HauntedItem.Talisman ? "protected" : "warded";
        var expiry = new Intent(item, mind, F("protection_expired", item, target),
            [F("set", target, active, false), F("set", target, field, null!)],
            motionCost: Balance.CandleExtinguishAv, label: "护持时限已到",
            requirements: [Eq(target, field, item)]);
        session.Runtime.ResolveDetailed(expiry, new Dictionary<string, object> { ["cause"] = item });
        return null;
    }

    private static double? MaintainSeal(SimulationSession session, Entity box, Soul mind, Entity target)
    {
        if (box.Get<Entity>("sealed_object") != target) return null;
        var placement = box.Parent as Entity ?? box;
        var sustain = new Intent(box, mind, F("maintain_seal", box, target),
            [F("spend", box, "charges", 1.0), F("set", target, "sealed", true), F("move", target, box)],
            motionCost: Balance.CandleMaintenanceIntervalAv, label: "维持封存",
            requirements: ItemRequirements(placement, box, 
                Eq(box, "sealed_object", target), Eq(target, "sealed", true), Eq(target, "seal_source", box)));
        var result = session.Runtime.ResolveDetailed(sustain, new Dictionary<string, object> { ["cause"] = box });
        if (result.Allowed && result.Result is { Succeeded: true }) return Balance.CandleMaintenanceIntervalAv;

        var originalParent = box.Get<Entity>("sealed_parent");
        var release = new Intent(box, mind, F("seal_expired", box, target),
            [F("set", target, "sealed", false), F("set", target, "seal_source", null!),
             F("set", box, "sealed_object", null!), F("set", box, "sealed_parent", null!),
             F("move", target, originalParent!)],
            motionCost: Balance.CandleExtinguishAv, label: "封存耗尽，目标被释放",
            requirements: [Eq(target, "seal_source", box)]);
        session.Runtime.ResolveDetailed(release, new Dictionary<string, object> { ["cause"] = box });
        return null;
    }

    private static Intent IntentFor(Entity actor, Soul mind, Entity item, Entity target, string op,
        IEnumerable<Effect> changes, IEnumerable<Expr> requirements, string label) =>
        new(actor, mind, F(op, actor, target, item), changes, Balance.BasicActionAv, label,
            ItemRequirements(actor, item, requirements.Concat(new[] {
                AST.E("any", AST.E("eq", target, actor), ItemLocation(actor, target),
                    AST.E("all", Eq(item, "sealed_object", target), Eq(target, "$parent", item)))
            }).ToArray()))
        {
            Definition = item
        };

    private static bool IsItem(Entity entity) => entity.Get<object>("item_kind") is HauntedItem;

    private static IEnumerable<Entity> Targets(Entity actor, IReadOnlyDictionary<string, object>? context)
    {
        yield return actor;
        var explicitTarget = context?.GetValueOrDefault("target") as Entity;
        if (explicitTarget != null && IsVisibleTarget(actor, explicitTarget)) yield return explicitTarget;
        foreach (var inventoryEntity in actor.Items.OfType<Entity>())
        {
            if (IsVisibleTarget(actor, inventoryEntity) && !ReferenceEquals(inventoryEntity, explicitTarget))
                yield return inventoryEntity;
        }
        foreach (var entity in SceneEntities(actor))
        {
            if (IsVisibleTarget(actor, entity) && !ReferenceEquals(entity, explicitTarget)) yield return entity;
        }
    }

    private static IEnumerable<Entity> SceneEntities(Entity actor)
    {
        var root = actor.Parent as Entity;
        if (root == null) yield break;
        foreach (var entity in root.Items.OfType<Entity>())
            if (!ReferenceEquals(entity, actor)) yield return entity;
    }

    private static bool IsVisibleTarget(Entity actor, Entity entity)
    {
        if (ReferenceEquals(entity, actor) || IsItem(entity) || entity.HasTag("Soul") || entity.HasTag("Scene")) return false;
        if (entity.HasTag("Hidden") || entity.HasTag("Clause") || entity.HasProperty("pattern") || entity.HasProperty("clause") || entity.HasProperty("clause_pattern")) return false;
        if (entity.Get<bool>("visible", true) == false) return false;
        return ReferenceEquals(entity.Parent, actor) || ReferenceEquals(entity.Parent, actor.Parent);
    }

    private static bool HasInvestigableState(Entity entity) =>
        entity.HasProperty("evidence") && !string.IsNullOrEmpty(entity.Get<string>("evidence"));

    private static bool IsSmallObject(Entity entity) =>
        entity is not Character && !entity.HasTag("Scene") && !entity.HasTag("Large") && !IsItem(entity);

    private static string[] PublicFields(HauntedItem kind) => kind switch
    {
        HauntedItem.Recorder => ["item_kind", "battery", "capacity", "recording_subject", "recording_text"],
        HauntedItem.Candle => ["item_kind", "fuel", "lit"],
        HauntedItem.Mirror => ["item_kind", "integrity"],
        HauntedItem.SealBox => ["item_kind", "integrity", "charges"],
        _ => ["item_kind", "integrity", "charges"]
    };

    private static Expr Value(Entity entity, string field) => AST.E("value", entity, field);
    private static Expr Eq(Entity entity, string field, object value) => AST.E("eq", Value(entity, field), value);
    private static Expr Ne(Entity entity, string field, object value) => AST.E("ne", Value(entity, field), value);
    private static Expr Gte(Entity entity, string field, double value) => AST.E("gte", Value(entity, field), value);
    private static Expr HasValue(Entity entity, string field) => AST.E("all", AST.E("known", Value(entity, field)), Ne(entity, field, null!));
    private static Expr Contains(Expr source, object value) => AST.E("contains", source, value);
    private static Expr Not(Expr expression) => AST.E("not", expression);

    private static Expr ItemLocation(Entity actor, Entity item)
    {
        var scene = actor.Parent as Entity;
        return scene == null
            ? Eq(item, "$parent", actor)
            : AST.E("any", Eq(item, "$parent", actor), Eq(item, "$parent", scene));
    }

    private static Expr[] ItemRequirements(Entity actor, Entity item, params Expr[] requirements)
    {
        var all = new List<Expr> { ItemLocation(actor, item), Gte(item, "integrity", 0.0001) };
        all.AddRange(requirements);
        return all.ToArray();
    }

    private static Effect F(string op, params object[] args) => AST.F(op, args);
}
