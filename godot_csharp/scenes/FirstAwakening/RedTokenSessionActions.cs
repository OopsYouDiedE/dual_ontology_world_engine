using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Game.FirstAwakening;

public sealed partial class RedTokenSession
{
    private sealed record PendingAction(RedTokenCooperation Agreement, string Operation, RedTokenOperation Action, string Site);
    private PendingAction? _pendingAction;
    private RedTokenCooperation? _following;
    private readonly Dictionary<string, Projection> _trialViews = [];

    public RedTokenResult IntroduceCompanions()
    {
        if (Ended) return new(false, "本次调查已经结束。");
        if (CompanionsMet) return new(true, "当下介绍已经记录，旧日关系仍待调查。");
        foreach (var actor in RedTokenSimulation.Companions)
        {
            var move = Physical.MoveTo(actor, "A");
            if (!move.Ok) return move;
        }
        Physical.IntroducePeople();
        if (Knows("E14")) Physical.ObserveSite("侦探", "E14");
        return new(true, "四人已交换姓名、职业与核验交付的共同打算。");
    }

    public RedTokenResult ReceiveCopperIntroduction()
    {
        if (Ended) return new(false, "本次调查已经结束。");
        return Physical.ReceiveCopperIntroduction();
    }

    public RedTokenSiteKnowledge SiteKnowledge(string id)
    {
        if (!Physical.EvidenceObjects.TryGetValue(id, out var objects)) return new("物件", false, false, false);
        var record = Physical.KnownRecord("侦探", id);
        var primary = objects.FirstOrDefault();
        var known = primary is null ? null : Physical.KnownObject("侦探", primary.TrueName);
        var name = known?.Name ?? primary?.Get<RedTokenObjectProperty>()?.UnrecognizedName ?? "物件";
        var noticed = record is not null;
        var identified = noticed && objects.Any(item => Physical.KnownObject("侦探", item.TrueName)?.Tags.GetValueOrDefault(RedTokenSimulation.Identified) == 1);
        var understood = noticed && objects.Any(item => Physical.KnownObject("侦探", item.TrueName)?.Tags.GetValueOrDefault(RedTokenSimulation.Understood) == 1);
        return new(name, noticed, identified, understood);
    }

    private RedTokenResult RecallStatements()
    {
        if (!CompanionsMet) return new(false, "先与同伴交换姓名，让转述有明确的说话人。");
        if (!Knows("E10") || !Knows("E11") || !Knows("E07"))
            return new(false, "先查看接掌存根、具名签收与纸包中的楔，再请两人触物回忆。");
        if (Knows("E12")) return new(true, "两人的具名转述已经记录；回忆仍不代替现场物证。");
        var engineer = Physical.KnownRecord("侦探", "statement:E11");
        var musician = Physical.KnownRecord("侦探", "statement:E07");
        if (engineer is null || musician is null) return new(false, "请两人分别靠近本人触物材料并具名转述，再汇集这份记录。");
        Projection[] statements = [engineer, musician];
        Physical.RecordEvidence("侦探", "E12", ObservationSource.Testimony,
            "工程师与音乐家分别转述本人触物片段；保留说话人和来源，不提升为直接历史实证。", statements);
        return new(true, "两人的具名转述已进入侦探认知，回忆与现场观察分别保留。");
    }

    private static bool ParseSafeOperation(string operation, out RedTokenOperation action)
    {
        var key = operation switch
        {
            "reward" => "verify_reward", "press" => "safe_press", "release" => "release",
            "gap" => "knock_gap", "pipe" => "knock_pipe", "close" => "close_door",
            "red" => "deposit_red", "handle" => "deposit_other", "push_red" => "push_red",
            "push_handle" => "push_other", "white" => "take_white",
            "recall_engineer" or "recall_music" => "recall_material", _ => ""
        };
        return RedTokenSimulation.TryParseOperation(key, out action);
    }

    private static string SafeSite(RedTokenOperation action) => action switch
    {
        RedTokenOperation.VerifyReward => "E01", RedTokenOperation.SafePress or RedTokenOperation.Release => "E03",
        RedTokenOperation.KnockGap or RedTokenOperation.CloseDoor => "E06", RedTokenOperation.KnockPipe => "E04", _ => "E13"
    };

    public RedTokenActionPlan PrepareSafeAction(string operation)
    {
        RedTokenActionPlan Refuse(string message) => new(false, message, "", "", operation);
        if (Ended) return Refuse("本次调查已经结束。");
        if (_pendingAction is not null) return Refuse("已有一项协作等待抵达或取消。");
        var parts = operation.Split(':', 2);
        if (!ParseSafeOperation(parts[0], out var action)) return Refuse("没有该安全操作。");
        var specified = parts.Length == 2 ? parts[1] : null;
        if (action == RedTokenOperation.RecallMaterial)
        {
            specified = parts[0] == "recall_engineer" ? "工程师霍铎" : "音乐家苏泠";
            if (!Knows("E10") || !Knows("E11") || !Knows("E07")) return Refuse("先查看接掌存根、具名签收与纸包中的楔，再请两人触物回忆。");
        }
        if (specified is not null && !RedTokenSimulation.Companions.Contains(specified)) return Refuse("没有对应的行动者。");
        if (action == RedTokenOperation.Release)
        {
            if (_safe.Holder is not { } holder) return Refuse("当前没有人持续持柄。");
            if (specified is not null && specified != holder) return Refuse("须由实际持柄者原位放手。");
            specified = holder;
        }
        if (action == RedTokenOperation.VerifyReward && specified is null) specified = "侦探";
        IEnumerable<string> candidates = specified is null ? RedTokenSimulation.Companions : [specified];
        var proposals = new List<RedTokenCooperation>();
        foreach (var actor in candidates)
        {
            if (action != RedTokenOperation.Release && actor == _safe.Holder) continue;
            if (action == RedTokenOperation.SafePress && _safe.Objects["handle"].Get<SpatialProperty>()?.Holder is { } owner && owner != _safe.Objects[actor]) continue;
            proposals.Add(Physical.RequestCooperation(actor, "侦探", action,
                action == RedTokenOperation.VerifyReward ? "共同调查：本人核验约定报偿与可信凭证" : action == RedTokenOperation.RecallMaterial
                ? "共同调查：请求本人触物并具名转述自己记得的片段" : "共同调查：操作独立空载试件，核对当前现象"));
        }
        var selected = proposals.Where(proposal => proposal.Decision is not null)
            .OrderByDescending(proposal => proposal.Decision!.Score).FirstOrDefault();
        foreach (var proposal in proposals.Where(proposal => proposal != selected)) Physical.FinishCooperation(proposal, false);
        if (selected is null)
            return Refuse(proposals.Any(proposal => proposal.Attitude.Stance == SocialStance.Reject)
                ? "同伴暂不接受这项请求；请先回应他的疑虑。" : proposals.Any(proposal => !Physical.HasKnowledge(proposal.Actor, RedTokenSimulation.RewardVerified))
                ? "同伴优先核验约定报偿与可信凭证，请先完成本人的交付核验。" : "暂时没有人能接手；请先交换姓名，或解除占用的岗位。");
        var site = action == RedTokenOperation.RecallMaterial ? selected.Actor == "工程师霍铎" ? "E11" : "E07" : SafeSite(action);
        _pendingAction = new(selected, parts[0], action, site);
        return new(true, selected.Actor + "愿意接手，抵达物件后再操作。", selected.Actor, site, operation);
    }

    public RedTokenResult CommitSafeAction()
    {
        if (_pendingAction is not { } pending) return new(false, "没有待执行的协作。");
        _pendingAction = null;
        var willingness = Physical.CommitCooperation(pending.Agreement);
        if (!willingness.Ok) { Physical.FinishCooperation(pending.Agreement, false); return willingness; }
        var space = pending.Action is RedTokenOperation.KnockGap or RedTokenOperation.CloseDoor ? "D" : RedTokenSimulation.EvidenceSpace(pending.Site);
        if (pending.Action != RedTokenOperation.Release)
        {
            var arrived = Physical.MoveTo(pending.Agreement.Actor, space);
            if (!arrived.Ok) { Physical.FinishCooperation(pending.Agreement, false); return arrived; }
        }
        var result = pending.Action == RedTokenOperation.RecallMaterial
            ? Physical.RecallMaterial(pending.Agreement.Actor, pending.Site)
            : ExecuteSafe(pending.Operation, pending.Agreement.Actor);
        Physical.FinishCooperation(pending.Agreement, result.Ok);
        if (result.Ok)
        {
            if (pending.Action == RedTokenOperation.VerifyReward)
                Physical.RecordRewardInspection(pending.Agreement.Actor);
            else if (pending.Action == RedTokenOperation.RecallMaterial)
                Physical.ReceiveStatement("侦探", pending.Agreement.Actor, "statement:" + pending.Site, result.Message);
            else RecordTrialObservation(pending.Agreement.Actor, pending.Action, result.Message);
            Physical.RememberCooperation("侦探", pending.Agreement.Actor, pending.Agreement.Request.Name, result.Message);
        }
        return result;
    }

    public RedTokenResult CancelPreparedAction()
    {
        if (_pendingAction is { } pending) Physical.FinishCooperation(pending.Agreement, false);
        _pendingAction = null;
        return new(true, "尚未提交的协作已取消，已经发生的物件状态保留。");
    }

    public RedTokenResult FollowCompanion(string actor)
    {
        if (Ended || !CompanionsMet || actor == "侦探" || !RedTokenSimulation.Companions.Contains(actor))
            return new(false, "先交换姓名和来意，再提出同行请求。");
        if (actor == SafeHolder) return new(false, "本人仍在持续持柄，暂时不能同行。");
        if (_following?.Actor == actor) return new(true, "本人已经同意同行。");
        var agreement = Physical.RequestCooperation(actor, "侦探", RedTokenOperation.Accompany, "一起核实负责人、交付与现场材料，保留各自判断");
        var result = Physical.CommitCooperation(agreement);
        if (!result.Ok) { Physical.FinishCooperation(agreement, false); return result; }
        StopFollowing();
        _following = agreement;
        return new(true, actor + "接受同行请求，各自的观察仍分别保存。");
    }

    public RedTokenResult StopFollowing()
    {
        if (_following is { } agreement) Physical.FinishCooperation(agreement, false);
        _following = null;
        return new(true, "本次同行已经结束。");
    }

    // 既有剧情命令在已经完成走位后调用此兼容入口。
    public RedTokenResult SafeTest(string operation)
    {
        var plan = PrepareSafeAction(operation);
        return plan.Ok ? CommitSafeAction() : new(false, plan.Message);
    }

    private void RecordTrialObservation(string actor, RedTokenOperation action, string details)
    {
        // 试件在两个运行时注册的是同一引用，现场认知只读取被操作的试件表面。
        // 它们与昨夜原物分开；试验结果不能污染原物的事实。
        if (_trialViews.Count == 0)
            foreach (var (key, item) in _safe.Objects.Where(pair => pair.Value.Kind != ProjectionKind.Body))
                _trialViews[key] = World.Project(item, "trial:" + key);
        World.Sync();
        string[] keys = action switch
        {
            RedTokenOperation.SafePress or RedTokenOperation.Release => new[] { "handle" },
            RedTokenOperation.KnockGap or RedTokenOperation.CloseDoor => ["door"],
            RedTokenOperation.KnockPipe => ["pipe"],
            RedTokenOperation.DepositRed or RedTokenOperation.PushRed => ["receipt_red", "red_slot"],
            RedTokenOperation.DepositOther or RedTokenOperation.PushOther => ["receipt_handle", "handle_slot"],
            RedTokenOperation.TakeWhite => ["white", "receiver"], _ => Array.Empty<string>()
        };
        foreach (var observer in new[] { actor, "侦探" }.Distinct())
        {
            if (observer != actor && Physical.ObjectLocation(observer) != Physical.ObjectLocation(actor)) continue;
            var observed = World.Observe(Physical.Souls[observer], keys.Select(key => _trialViews[key]), Physical.Runtime.CTB.Now, new TrialPerception(_safe));
            Physical.RecordEvidence(observer, "trial:" + RedTokenSimulation.OperationKey(action), ObservationSource.Perception, details, observed);
        }
    }

    private sealed class TrialPerception(RedTokenSimulation trial) : IProjectionTransform
    {
        public ObservationInput? Transform(ObservationInput input)
        {
            var key = input.Name["trial:".Length..];
            var description = trial.Objects[key].Get<RedTokenObjectProperty>();
            return input with { Name = "空载试件·" + (description?.DisplayName ?? key),
                Tags = input.Tags.SetItem(RedTokenSimulation.Identified, 1), Rules = null, Procedure = null };
        }
    }
}
