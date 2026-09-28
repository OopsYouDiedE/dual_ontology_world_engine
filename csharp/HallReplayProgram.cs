namespace DualOntologyWorldEngine.Core;

/// <summary>Ordered reconstruction; waiting on smoke advances the environment to that observable event.</summary>
public static class HallReplayProgram
{
    public sealed record Result(IReadOnlyList<HallCausalityRules.Action> Actions,
        IReadOnlyList<string> Events, IReadOnlyList<string> EventActions, string? Error);

    public static bool TryAction(string code, out HallCausalityRules.Action action) =>
        Enum.TryParse(code, out action) && Enum.IsDefined(action) && action.ToString() == code;

    public static Result Compile(IReadOnlyList<string> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var actions = new List<HallCausalityRules.Action>();
        var events = new List<string>();
        var eventActions = new List<string>();
        var visits = new int[steps.Count];
        int pc = 0, tick = 0;
        bool smoke = false, musicianAtDoor = false;
        // Old saved control-flow programs retain their original clock. New manuscripts use WAIT_SMOKE.
        bool legacyClock = steps.Any(code => code.StartsWith("IF_SMOKE:", StringComparison.Ordinal));
        Result Error(string message) => new(Array.Empty<HallCausalityRules.Action>(),
            Array.Empty<string>(), Array.Empty<string>(), message);
        bool Target(string text, out int target) =>
            int.TryParse(text, out target) && target >= 1 && target <= steps.Count;
        void Emit(string caption, string code = "") { events.Add(caption); eventActions.Add(code); }
        void Smoke()
        {
            if (smoke) return;
            smoke = true;
            actions.Add(HallCausalityRules.Action.SmokeArrives);
            Emit("环境 · 门缝涌入灰黑热烟", "SmokeArrives");
        }
        // Validate the entire manuscript, including unreachable lines, before executing any action.
        for (int i = 0; i < steps.Count; i++)
        {
            string code = steps[i];
            if (code.StartsWith("IF_SMOKE:", StringComparison.Ordinal) || code.StartsWith("GOTO:", StringComparison.Ordinal))
            {
                if (!Target(code[(code.IndexOf(':') + 1)..], out _)) return Error($"第{i + 1}步指向不存在的步号。");
            }
            else if (code is not ("WAIT" or "WAIT_SMOKE" or "GoToDoor") &&
                     (!TryAction(code, out var action) || action == HallCausalityRules.Action.SmokeArrives))
                return Error($"第{i + 1}步的行动无效：{code}");
        }
        while (pc < steps.Count && tick < 48)
        {
            if (legacyClock && tick == 3) Smoke();
            if (++visits[pc] > 8) return Error($"第{pc + 1}步反复执行，尚未出现能让循环退出的可观察条件。");
            string code = steps[pc];
            if (code.StartsWith("IF_SMOKE:", StringComparison.Ordinal))
            {
                Target(code["IF_SMOKE:".Length..], out int target);
                bool observed = smoke && musicianAtDoor;
                Emit(observed ? "音乐家在门厅观察到热烟，按条件行动。" : "音乐家尚未观察到热烟，继续等待。");
                pc = observed ? target - 1 : pc + 1;
            }
            else if (code.StartsWith("GOTO:", StringComparison.Ordinal))
            {
                Target(code["GOTO:".Length..], out int target);
                Emit("继续观察先前的条件。");
                pc = target - 1;
            }
            else if (code == "WAIT_SMOKE")
            {
                if (!musicianAtDoor) return Error($"第{pc + 1}步无法观察热烟：音乐家须先到门边。");
                if (!smoke) { Emit("音乐家留在门边，等待热烟出现。", code); Smoke(); }
                else Emit("音乐家已看到热烟，继续行动。", code);
                pc++;
            }
            else if (code == "WAIT") { Emit("驻足观察一拍。", code); pc++; }
            else if (code == "GoToDoor")
            {
                musicianAtDoor = true;
                Emit("音乐家走向隔离门。", code);
                pc++;
            }
            else
            {
                TryAction(code, out var action);
                int previousFailures = HallCausalityRules.Simulate(actions).Failures.Count;
                actions.Add(action);
                if (action is HallCausalityRules.Action.BraceDoorWithWedge or HallCausalityRules.Action.MoveStop
                    && HallCausalityRules.Simulate(actions).Failures.Count == previousFailures)
                    musicianAtDoor = true;
                Emit(code, code);
                pc++;
            }
            tick++;
        }
        if (pc < steps.Count) return Error("动作流超过48拍。请设置退出循环的可观察条件。");
        return new(actions.AsReadOnly(), events.AsReadOnly(), eventActions.AsReadOnly(), null);
    }
}
