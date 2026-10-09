namespace DualOntologyWorldEngine.Core;

/// <summary>Ordered, engine-independent reconstruction of the hall door evidence.</summary>
// STORY-HARDCODE[CSH-001][schema][production]: 具体隔离门、木楔、灰烟、纸转印、双锁与擦地证据因果模型。
public static class HallCausalityRules
{
    public enum Hypothesis
    {
        InnerWedgeStopsOutsidePush,
        OutsideActorSetBothLocks,
        PassageThroughWedgeGap,
        ClosedDoorContinuesAdmittingSmoke,
        OutsideActorDidAllCleanup
    }
    public enum Action
    {
        BraceDoorWithWedge, SmokeArrives, RemoveWedge, CloseDoor,
        SlideInnerBolt, WrapWedge, MoveStop, WipeFloor, OuterBar,
        PushDoor, PullDoor, UnwrapWedge, WithdrawInnerBolt, LiftOuterBar, ReturnStop, ReleaseDoor
    }

    public enum ConflictKind
    {
        OutwardStopAgainstOutsidePush,
        CounterweightRequiresBrace,
        SootMaskRequiresOpenDoorAndBrace,
        PaperTransferRequiresDirtyWedgeBeforeWrapping,
        TwoLocksRequireSeparateOperations,
        WipeBandRequiresStopMovedBeforeWiping,
        PassageRequiresWiderOpening,
        ClosureStopsOpenDoorSmokePath,
        CleanupRequiresInsideAccess
    }

    public sealed record Trace(string Id, string Description);
    public sealed record Conflict(ConflictKind Kind, bool Contradiction, string Explanation);
    public sealed record Result(IReadOnlyList<Trace> Traces, IReadOnlyList<Conflict> Conflicts,
        bool DoorWasBraced, bool SmokeReachedDoor, bool WedgeWasRemoved,
        bool DoorClosed, bool InnerBoltSlid, bool WedgeWrapped,
        bool StopMoved, bool FloorWiped, bool OuterBarDropped,
        bool DoorIsBraced, IReadOnlyList<string> Failures, bool DoorAllowsPassage);

    /// <summary>Evaluate a proposed explanation against door geometry and simulated lock ownership.</summary>
    // STORY-HARDCODE[CSH-002][text][production]: 针对隔离门案件假设输出结构结论与排除边界。
    public static Conflict TestHypothesis(Hypothesis hypothesis, Result state) => hypothesis switch
    {
        Hypothesis.InnerWedgeStopsOutsidePush => new(ConflictKind.OutwardStopAgainstOutsidePush,
            true, "门向主楼侧外开，外推力使门扇压紧石砌止口；生活翼内的木楔不能抵抗该方向推力，只会妨碍合门。"),
        Hypothesis.OutsideActorSetBothLocks => new(ConflictKind.TwoLocksRequireSeparateOperations,
            state.InnerBoltSlid && state.OuterBarDropped,
            "内销位于生活翼侧，外闩位于主楼侧；单独站在外侧的操作者不能同时完成内销插入与外闩落座。"),
        Hypothesis.PassageThroughWedgeGap => new(ConflictKind.PassageRequiresWiderOpening,
            !state.DoorClosed && !state.DoorAllowsPassage,
            "木楔撑出的约十厘米开口不能直接让成人穿行；必须另有把门开大并保持通路的行动。这只否定直接穿缝，不排除先探路、随后撤离。"),
        Hypothesis.ClosedDoorContinuesAdmittingSmoke => new(ConflictKind.ClosureStopsOpenDoorSmokePath,
            state.SmokeReachedDoor && state.DoorClosed,
            "撤楔后配重使门合拢，先前敞开的进烟通路随之关闭；不能一面重演合门，一面称烟气仍从同一开口持续涌入。此前留下的灰迹仍保留；这不证明操作者善意，也不排除其他加害办法。"),
        Hypothesis.OutsideActorDidAllCleanup => new(ConflictKind.CleanupRequiresInsideAccess, true,
            "一直留在门外的人够不到生活翼内的包楔、擦地与冲洗对象；门合拢后也不能从内侧落下外闩。须补出开大门、跨侧及实际处置行动。洗布和包楔不能排除外来者，内外锁也不证明必须有两个不同身份的人。"),
        _ => throw new ArgumentOutOfRangeException(nameof(hypothesis), hypothesis, "Unknown door hypothesis.")
    };

    /// <summary>Actions mutate simulated state in order; no evidence is injected as an answer.</summary>
    // STORY-HARDCODE[CSH-003][logic][production]: 模拟隔离门案件动作顺序并生成具体证据痕迹和反馈。
    public static Result Simulate(IEnumerable<Action> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        var traces = new List<Trace>();
        var seen = new HashSet<Action>();
        var failures = new List<string>();
        bool everBraced = false;
        bool braced = false, smoke = false, wedgeRemoved = false, closed = true;
        bool allowsPassage = false;
        bool bolt = false, wrapped = false, moved = false, wiped = false, outerBar = false;
        bool sootOnWedge = false, sootMaskedAtDoor = false, sootOnPaperInside = false, sootOnPaperOutside = false;
        bool wipeAtStopFoot = false;

        void MarkWedgeExposure()
        {
            if (!smoke || closed || !braced || allowsPassage || sootMaskedAtDoor) return;
            sootMaskedAtDoor = true;
            sootOnWedge = true;
            traces.Add(new("door-mask", "门底形成木楔遮蔽的斜边洁净区。"));
            traces.Add(new("wedge-soot", "迎灰接触面附着灰黑沉积，背面保留挤压凹痕。"));
        }

        foreach (var action in actions)
        {
            seen.Add(action);
            switch (action)
            {
                case Action.BraceDoorWithWedge:
                    if (!braced && !wrapped && !bolt && !outerBar)
                    {
                        closed = false;
                        allowsPassage = false;
                        wedgeRemoved = false;
                        braced = true; everBraced = true;
                        traces.Add(new("wedge-brace", "木楔支撑门扇，抵住配重的合门拉力。"));
                        MarkWedgeExposure();
                    }
                    else failures.Add("撑门未完成：木楔已经撑在门底、已经包起，或门已上锁。");
                    break;
                case Action.PushDoor:
                case Action.PullDoor:
                    if (bolt || outerBar) failures.Add("开门未完成：内销或外闩仍在锁止门扇。");
                    else if (!closed && (!braced || allowsPassage)) failures.Add("门扇已经开启。");
                    else
                    {
                        closed = false;
                        allowsPassage = true;
                        traces.Add(new("door-held-open", braced
                            ? "将木楔留出的窄缝开大至可通行，需要继续扶住；松手后门扇退回木楔支撑的位置。"
                            : "门扇向主楼侧打开至可通行，需要继续扶住以抵住配重。"));
                    }
                    break;
                case Action.ReleaseDoor:
                    if (closed) { failures.Add("门扇已经合拢，无需松手。"); break; }
                    allowsPassage = false;
                    if (braced)
                    {
                        traces.Add(new("door-wedge-gap", "松开门扇，配重使开口缩回木楔支撑的约十厘米窄缝。"));
                        MarkWedgeExposure();
                    }
                    else { closed = true; traces.Add(new("door-closed", "松开门扇后，配重将门扇拉合。")); }
                    break;
                case Action.SmokeArrives:
                    smoke = true;
                    MarkWedgeExposure();
                    if (!closed && (!braced || allowsPassage)) traces.Add(new("door-soot", "门处于开启状态，灰气可到达门底与楔子。"));
                    if (wrapped)
                    {
                        sootOnPaperOutside = true;
                        traces.Add(new("paper-outside-soot", "包纸先于灰气暴露，灰迹落在纸外侧，纸内没有先存的木楔转印。"));
                    }
                    break;
                case Action.RemoveWedge:
                    if (braced && !closed)
                    {
                        wedgeRemoved = true; braced = false; closed = true; allowsPassage = false;
                        traces.Add(new("wedge-removed", "撤下门底木楔。"));
                        traces.Add(new("door-closed", "失去木楔支撑，配重将门扇拉合。"));
                    }
                    else failures.Add("撤楔未完成：门底没有正在撑门的木楔。");
                    break;
                case Action.CloseDoor:
                    if (!braced && !closed) { closed = true; allowsPassage = false; traces.Add(new("door-closed", "门扇在配重作用下合拢并抵住外开止口。")); }
                    else failures.Add(braced ? "合门未完成：木楔仍在支撑门扇。" : "门扇已经合拢，无需再次合门。");
                    break;
                case Action.SlideInnerBolt:
                    if (closed && !bolt) { bolt = true; traces.Add(new("inner-bolt", "门合拢后，生活翼侧手动内销插入。")); }
                    else failures.Add(bolt ? "内销已经插入。" : "插销未完成：门扇尚未合拢。");
                    break;
                case Action.WithdrawInnerBolt:
                    if (bolt) { bolt = false; traces.Add(new("inner-bolt-withdrawn", "生活翼内销从门框中拔出。")); }
                    else failures.Add("内销尚未插入，无需拔出。");
                    break;
                case Action.WrapWedge:
                    if (wrapped) { failures.Add("木楔已经包起。"); break; }
                    if (braced) { failures.Add("包楔未完成：木楔仍在门底支撑门扇。"); break; }
                    if (sootOnWedge && wedgeRemoved)
                    {
                        wrapped = true; sootOnPaperInside = true;
                        traces.Add(new("paper-inner-transfer", "已沾灰木楔折入纸中，接触处在纸内侧留下转印。"));
                        traces.Add(new("paper-outside-clean", "包裹后纸外侧未接触灰气，保持相对洁净。"));
                    }
                    else { wrapped = true; traces.Add(new("paper-wrapped", "木楔被包入纸中；尚无灰迹可转印。")); }
                    break;
                case Action.UnwrapWedge:
                    if (wrapped) { wrapped = false; traces.Add(new("paper-unwrapped", "展开包纸，木楔重新露出；纸上的旧痕迹仍然保留。")); }
                    else failures.Add("木楔尚未包起，无需拆开。");
                    break;
                case Action.MoveStop:
                    if (!moved) { moved = true; traces.Add(new("stop-moved", "重止挡座移离原位，底部保留旧积灰轮廓。")); }
                    else failures.Add("止挡座已经移开。");
                    break;
                case Action.ReturnStop:
                    if (moved) { moved = false; traces.Add(new("stop-returned", "将铁止挡座搬回原位，地面已有的擦拭痕并不会消失。")); }
                    else failures.Add("止挡座仍在原位。");
                    break;
                case Action.WipeFloor:
                    wiped = true;
                    if (moved && smoke) { wipeAtStopFoot = true; traces.Add(new("wipe-after-stop", "烟尘到达、止挡座移开后擦地，原座底轮廓上出现后发擦拭带。")); }
                    else traces.Add(new("wipe-before-stop", "灰气或移座尚未发生，不能生成原座底后发擦拭带。"));
                    break;
                case Action.OuterBar:
                    if (closed && !outerBar) { outerBar = true; traces.Add(new("outer-bar", "主楼侧横闩落入外侧卡座，形成外部通行限制。")); }
                    else failures.Add(outerBar ? "外闩已经落座。" : "落闩未完成：门扇尚未合拢。");
                    break;
                case Action.LiftOuterBar:
                    if (outerBar) { outerBar = false; traces.Add(new("outer-bar-lifted", "主楼侧横闩从铁座上抬起。")); }
                    else failures.Add("主楼侧横闩尚未落座。");
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(actions), action, "Unknown hall action.");
            }
        }

        var conflicts = new[]
        {
            new Conflict(ConflictKind.OutwardStopAgainstOutsidePush, false, "外开门受外侧推力会压紧主楼侧止口；生活翼内的楔子不能抵抗该方向推力。当前动作集不模拟外侧推门，因此此反例作为结构规则说明，不判定本次动作序列矛盾。"),
            new Conflict(ConflictKind.CounterweightRequiresBrace, smoke && !everBraced, "配重持续拉合门扇；没有撑门动作，不能重建开门探视期间的平衡。"),
            new Conflict(ConflictKind.SootMaskRequiresOpenDoorAndBrace, smoke && !sootMaskedAtDoor, "灰气到达时门未处于木楔支撑的开启状态，无法生成斜边门底遮蔽。"),
            new Conflict(ConflictKind.PaperTransferRequiresDirtyWedgeBeforeWrapping, (wrapped || seen.Contains(Action.WrapWedge)) && (!sootOnPaperInside || sootOnPaperOutside), "包纸转印方向不符：需先让木楔沾灰、从门底撤下，再将接触面包在纸内；纸外应相对洁净。"),
            new Conflict(ConflictKind.TwoLocksRequireSeparateOperations, false, "内销与外闩位于门的两侧，推演分别处理两项动作。当前动作集不包含‘单侧一次操作完成双锁’的动作，因此不把正确的双锁共存标记为矛盾。"),
            new Conflict(ConflictKind.WipeBandRequiresStopMovedBeforeWiping, wiped && !wipeAtStopFoot, "先擦地再移止挡座，不能在原座底遮蔽区留下后发连续擦拭带。")
        };
        return new(traces.AsReadOnly(), Array.AsReadOnly(conflicts), everBraced, smoke, wedgeRemoved, closed,
            bolt, wrapped, moved, wiped, outerBar, braced, failures.AsReadOnly(), allowsPassage);
    }
}
