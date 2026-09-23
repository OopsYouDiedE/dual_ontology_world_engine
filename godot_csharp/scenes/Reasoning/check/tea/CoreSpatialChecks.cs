using DualOntologyWorldEngine.Core;

static class CoreSpatialChecks
{
    private sealed class Fixture
    {
        public readonly WorldRuntime World = new();
        public readonly CTBSystem Clock = new();
        public readonly SimulationSession Session;
        public readonly Essence Room = new("房间", ProjectionKind.Space);
        public readonly Essence Table = new("桌子");
        public readonly Essence Saucer = new("茶碟");
        public readonly Essence Cup = new("茶杯");
        public readonly Essence Box = new("盒子");
        public readonly Essence OtherCup = new("盒中杯");
        public readonly Essence Cabinet = new("柜子");
        public readonly SoulEssence Soul = new("甲");
        public readonly SoulEssence OtherSoul = new("乙");
        public readonly Essence Body = new("甲的身体", ProjectionKind.Body);
        public readonly Essence OtherBody = new("乙的身体", ProjectionKind.Body);
        public Projection Actor = null!;
        public Projection OtherActor = null!;
        public Projection OtherCupTarget = null!;
        public Projection CupView = null!;
        public Projection SaucerView = null!;
        public Projection BoxView = null!;
        public Projection OtherCupView = null!;
        public Projection CabinetView = null!;

        public Fixture(bool nested = true)
        {
            Session = new(World, Clock);
            Room.Add(new SpatialProperty());
            Table.Add(new SpatialProperty { Space = Room });
            Saucer.Add(new SpatialProperty { Holder = Table });
            Cup.Add(new SpatialProperty { Holder = Saucer });
            Box.Add(new SpatialProperty { Space = Room });
            OtherCup.Add(new SpatialProperty { Holder = Box });
            Cabinet.Add(new SpatialProperty { Space = Room });
            Body.Add(new SpatialProperty { Space = Room });
            OtherBody.Add(new SpatialProperty { Space = Room });
            Soul.Relations.Add((Relations.Controls, Body));
            OtherSoul.Relations.Add((Relations.Controls, OtherBody));
            var room = World.Project(Room, Room.TrueName);
            World.Project(Soul, Soul.TrueName);
            World.Project(OtherSoul, OtherSoul.TrueName);
            var table = World.Project(Table, Table.TrueName, room);
            var saucer = World.Project(Saucer, Saucer.TrueName, table);
            var cup = World.Project(Cup, Cup.TrueName, saucer);
            var box = World.Project(Box, Box.TrueName, room);
            var otherCup = World.Project(OtherCup, OtherCup.TrueName, box);
            var cabinet = World.Project(Cabinet, Cabinet.TrueName, room);
            var actor = World.Project(Body, Body.TrueName, room);
            var otherActor = World.Project(OtherBody, OtherBody.TrueName, room);
            var reality = new[] { room, table, saucer, cup, box, otherCup, cabinet, actor, otherActor };
            var seen = World.Observe(Soul, reality);
            var otherSeen = World.Observe(OtherSoul, reality);
            Actor = seen[7]; CupView = seen[3]; SaucerView = seen[2]; BoxView = seen[4];
            OtherCupView = seen[5]; CabinetView = seen[6]; OtherActor = otherSeen[8]; OtherCupTarget = otherSeen[3];
        }

        public ActionExecution Run(SoulEssence soul, Projection actor, Effect effect)
        {
            var execution = Session.Submit(soul, new(actor, effect));
            while (execution.Status == ExecutionStatus.Pending)
                if (!Clock.Step()) throw new InvalidOperationException("Core spatial action ran out of scheduled events.");
            return execution;
        }
    }

    private static Effect Act(ActionOp op, Projection target, Projection? tool = null)
        => new Effect.Act(op, target, tool is null ? null : new Noun.Reference(tool));

    public static void Run(Action<bool, string> require)
    {
        var nested = new Fixture();
        var take = nested.Run(nested.Soul, nested.Actor, Act(ActionOp.Take, nested.CupView));
        require(take.Status == ExecutionStatus.Completed && take.Result?.Succeeded == true, $"Core应能从房间内嵌套的桌→碟→杯取杯（{take.Status}/{take.Result?.Failure}）");
        require(nested.World.Representations(nested.Cup).Single().Parent == nested.World.Representations(nested.Body).Single(), "取起后现实投影应归身体持有");
        require(nested.Soul.World.Projections.Single(p => p.Name == "茶杯").Parent?.Name == "茶碟", "客观Take/Sync不得自动改写主体记忆");
        var inspect = nested.Run(nested.Soul, nested.Actor, Act(ActionOp.Inspect, nested.CupView));
        require(inspect.Status == ExecutionStatus.Completed && inspect.Result?.Succeeded == true, "持有的嵌套器物应可检查");
        var place = nested.Run(nested.Soul, nested.Actor, Act(ActionOp.Place, nested.CupView, nested.SaucerView));
        require(place.Status == ExecutionStatus.Completed && place.Result?.Succeeded == true, "应能将杯放回可达的嵌套茶碟");
        require(nested.World.Representations(nested.Cup).Single().Parent == nested.World.Representations(nested.Saucer).Single(), "放置后现实投影应恢复杯→碟承托链");

        var locked = new Fixture();
        locked.Cabinet.Add(new LockProperty { IsLocked = true });
        locked.Box.Get<SpatialProperty>()!.Space = locked.Cabinet;
        locked.World.Sync();
        var deniedByLock = locked.Run(locked.Soul, locked.Actor, Act(ActionOp.Take, locked.OtherCupView));
        require(deniedByLock.Status == ExecutionStatus.Failed && deniedByLock.Result?.Failure == ActionFailure.OutOfReach, "锁柜中的嵌套杯应不可取用");

        var held = new Fixture();
        held.Cup.Get<SpatialProperty>()!.Holder = held.OtherBody;
        held.Cup.Get<SpatialProperty>()!.Space = null;
        held.World.Sync();
        var deniedHeld = held.Run(held.Soul, held.Actor, Act(ActionOp.Take, held.CupView));
        require(deniedHeld.Status == ExecutionStatus.Failed && deniedHeld.Result?.Failure == ActionFailure.OutOfReach, "不得从另一身体手中抢取器物");

        var cycle = new Fixture();
        var count = new Tag("阶段标记");
        cycle.Box.Add(new StateProperty());
        var boxProjection = cycle.Soul.World.Projections.Single(p => p.Name == "盒子");
        var childProjection = cycle.Soul.World.Projections.Single(p => p.Name == "盒中杯");
        var failCycle = cycle.Run(cycle.Soul, cycle.Actor, new Effect.Sequence([
            new Effect.Set(boxProjection, count, 7), Act(ActionOp.Take, boxProjection), Act(ActionOp.Place, boxProjection, childProjection)
        ]));
        require(failCycle.Status == ExecutionStatus.Failed && failCycle.Result?.Failure == ActionFailure.ConditionFailed, "Place进入自身后代必须拒绝");
        require(cycle.Box.Get<StateProperty>()!.Values.GetValueOrDefault(count) == 0, "同阶段前置Set在承托环失败时必须回滚");
        require(ReferenceEquals(cycle.Box.Get<SpatialProperty>()!.Space, cycle.Room), "承托环失败不得提交Take或破坏原归属");

        var stale = new Fixture();
        ActionExecution? otherTake = null;
        stale.Clock.Schedule(1.5, () => otherTake = stale.Session.Submit(stale.OtherSoul,
            new(stale.OtherActor, Act(ActionOp.Take, stale.OtherCupTarget), 0.1)));
        var delayed = stale.Run(stale.Soul, stale.Actor, new Effect.Wait(2, Act(ActionOp.Take, stale.CupView)));
        require(otherTake?.Status == ExecutionStatus.Completed && otherTake.Result?.Succeeded == true, "等待期间另一人应成功取走桌上的杯");
        require(delayed.Status == ExecutionStatus.Failed && delayed.Result?.Failure == ActionFailure.OutOfReach, "等待后必须按最新持有人状态拒绝过期取用");
        require(ReferenceEquals(stale.Cup.Get<SpatialProperty>()!.Holder, stale.OtherBody), "失败后应保留另一人的持有关系");
        stale.Session.Dispose(); nested.Session.Dispose(); locked.Session.Dispose(); held.Session.Dispose(); cycle.Session.Dispose();
    }
}
