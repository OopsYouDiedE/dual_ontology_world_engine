using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Story;

/// <summary>章节只绑定已有世界与输入输出，剧情推进和组合语义由两章共用。</summary>
public sealed class StoryBindings<TWorld, TActor>
{
    public required Action<TWorld, string> Text { get; init; }
    public required Action<TWorld, StoryPresentation<TActor>> Present { get; init; }
    public required Func<TWorld, TActor, bool> Busy { get; init; }
    public required Func<TWorld, TActor, Effect, ActionExecution> Execute { get; init; }
    public required Func<TWorld, CTBSystem> Timeline { get; init; }
}

public enum StoryPresentationKind { Image, Audio, Animation }
public sealed record StoryPresentation<TActor>(StoryPresentationKind Kind, string Content, TActor Actor);
public sealed record StorySequence<TWorld, TActor>(string Id, Func<TWorld, bool> Trigger, IReadOnlyList<StoryStep<TWorld, TActor>> Steps);
public sealed record StoryOption<TWorld, TActor>(string Id, string Text, IReadOnlyList<StoryStep<TWorld, TActor>> Steps, Func<TWorld, bool>? Available = null);

public abstract record StoryStep<TWorld, TActor>
{
    public sealed record Text(string Content) : StoryStep<TWorld, TActor>;
    public sealed record Presentation(StoryPresentation<TActor> Content) : StoryStep<TWorld, TActor>;
    public sealed record Action(TActor Actor, Func<TWorld, Effect> Build) : StoryStep<TWorld, TActor>;
    public sealed record CoreAction(Func<TWorld, ScriptCharacter> Actor, Func<TWorld, Effect> Build) : StoryStep<TWorld, TActor>
    {
        public CoreAction(ScriptCharacter actor, Func<TWorld, Effect> build) : this(_ => actor, build) { }
    }
    public sealed record Wait(Func<TWorld, bool> Until) : StoryStep<TWorld, TActor>;
    public sealed record Update(System.Action<TWorld> Apply) : StoryStep<TWorld, TActor>;
    public sealed record Branch(Func<TWorld, bool> When, IReadOnlyList<StoryStep<TWorld, TActor>> Then, IReadOnlyList<StoryStep<TWorld, TActor>> Otherwise) : StoryStep<TWorld, TActor>;
    public sealed record Choice(string Id, string Prompt, IReadOnlyList<StoryOption<TWorld, TActor>> Options) : StoryStep<TWorld, TActor>;
    public sealed record Parallel(IReadOnlyList<IReadOnlyList<StoryStep<TWorld, TActor>>> Branches) : StoryStep<TWorld, TActor>;
    public sealed record Repeat(int Count, IReadOnlyList<StoryStep<TWorld, TActor>> Steps) : StoryStep<TWorld, TActor>;
}

/// <summary>同一执行内核处理触发、顺序、并行、重复、等待与选择。物理动作交给原 Core 会话。</summary>
public sealed class StoryRuntime<TWorld, TActor>
{
    private sealed class Fiber(IReadOnlyList<StoryStep<TWorld, TActor>> steps)
    {
        public readonly LinkedList<StoryStep<TWorld, TActor>> Steps = new(steps);
        public ActionExecution? Execution;
        public List<Fiber>? Children;
        public StoryStep<TWorld, TActor>.Choice? Choice;
        public bool Finished => Steps.Count == 0 && Execution is null && Children is null && Choice is null;
    }

    private readonly TWorld _world;
    private readonly StoryBindings<TWorld, TActor> _bindings;
    private readonly IReadOnlyList<StorySequence<TWorld, TActor>> _sequences;
    private readonly HashSet<string> _started = [];
    private readonly List<Fiber> _fibers = [];
    private Fiber? _choosing;
    private bool _polling, _again;
    public StoryStep<TWorld, TActor>.Choice? PendingChoice => _choosing?.Choice;
    public bool IsRunning => _fibers.Any(f => !f.Finished);
    public event System.Action<string, string>? Chosen;

    public StoryRuntime(TWorld world, StoryBindings<TWorld, TActor> bindings, IReadOnlyList<StorySequence<TWorld, TActor>> sequences)
    {
        if (sequences.Select(s => s.Id).Distinct().Count() != sequences.Count)
            throw new ArgumentException("剧情序列 Id 必须唯一。", nameof(sequences));
        _world = world;
        _bindings = bindings;
        _sequences = sequences;
    }

    public bool Controls(TActor actor) => _actors.Any(pair => pair.Key.Status == ExecutionStatus.Pending &&
        EqualityComparer<TActor>.Default.Equals(pair.Value, actor));
    public bool Controls(ScriptCharacter actor) => _coreActors.Any(pair => pair.Key.Status == ExecutionStatus.Pending && ReferenceEquals(pair.Value, actor));
    public bool Owns(ActionExecution execution) => _actors.ContainsKey(execution) || _coreActors.ContainsKey(execution);

    private readonly Dictionary<ActionExecution, TActor> _actors = [];
    private readonly Dictionary<ActionExecution, ScriptCharacter> _coreActors = [];

    /// <summary>只推进到本剧情下一个已调度阶段；遇到选择或条件等待即返回。</summary>
    public void Pump()
    {
        Poll();
        while (PendingChoice is null)
        {
            var next = _actors.Keys.Concat(_coreActors.Keys).Where(e => e.Status == ExecutionStatus.Pending && e.Scheduled is { Pending: true })
                .Select(e => e.Scheduled!).OrderBy(s => s.Due).FirstOrDefault();
            if (next is null) return;
            // 时间轴仍按 Core 的先后次序处理已到期动作，不推演到下一整时段。
            _bindings.Timeline(_world).RunUntil(next.Due);
            Poll();
        }
    }

    private static void Prepend(Fiber fiber, IReadOnlyList<StoryStep<TWorld, TActor>> steps)
    {
        for (var i = steps.Count - 1; i >= 0; i--) fiber.Steps.AddFirst(steps[i]);
    }

    public void Choose(string optionId)
    {
        var fiber = _choosing ?? throw new InvalidOperationException("当前没有剧情选项。");
        var choice = fiber.Choice!;
        var option = choice.Options.Single(o => o.Id == optionId);
        if (option.Available?.Invoke(_world) == false) throw new InvalidOperationException("该剧情选项当前不可用。");
        fiber.Choice = null;
        _choosing = null;
        Prepend(fiber, option.Steps);
        Chosen?.Invoke(choice.Id, optionId);
        Poll();
    }

    public void Poll()
    {
        if (_polling) { _again = true; return; }
        _polling = true;
        try
        {
            do
            {
                _again = false;
                foreach (var sequence in _sequences.Where(s => !_started.Contains(s.Id) && s.Trigger(_world)))
                {
                    _started.Add(sequence.Id);
                    _fibers.Add(new(sequence.Steps));
                }
                foreach (var fiber in _fibers.ToArray()) Advance(fiber);
                _fibers.RemoveAll(f => f.Finished);
            } while (_again);
        }
        finally { _polling = false; }
    }

    private void Advance(Fiber fiber)
    {
        if (fiber.Choice is not null || fiber.Execution is { Status: ExecutionStatus.Pending }) return;
        if (fiber.Execution is { Status: not ExecutionStatus.Completed })
            throw new InvalidOperationException($"剧情动作失败：{fiber.Execution.Status}；{fiber.Execution.Result}");
        if (fiber.Execution is { } finished) { _actors.Remove(finished); _coreActors.Remove(finished); }
        fiber.Execution = null;
        if (fiber.Children is { } children)
        {
            if (children.Any(c => !c.Finished)) return;
            fiber.Children = null;
        }
        while (fiber.Steps.First is { } node)
        {
            if (node.Value is StoryStep<TWorld, TActor>.Wait wait && !wait.Until(_world)) return;
            if (node.Value is StoryStep<TWorld, TActor>.Action busy && _bindings.Busy(_world, busy.Actor)) return;
            if (node.Value is StoryStep<TWorld, TActor>.CoreAction coreBusy && coreBusy.Actor(_world) is { } coreActor && coreActor.World.IsBusy(coreActor)) return;
            if (node.Value is StoryStep<TWorld, TActor>.Choice && _choosing is not null) return;
            fiber.Steps.RemoveFirst();
            switch (node.Value)
            {
                case StoryStep<TWorld, TActor>.Text text:
                    _bindings.Text(_world, text.Content);
                    break;
                case StoryStep<TWorld, TActor>.Presentation presentation:
                    _bindings.Present(_world, presentation.Content);
                    break;
                case StoryStep<TWorld, TActor>.Update update:
                    update.Apply(_world);
                    break;
                case StoryStep<TWorld, TActor>.Branch branch:
                    Prepend(fiber, branch.When(_world) ? branch.Then : branch.Otherwise);
                    break;
                case StoryStep<TWorld, TActor>.Choice choice:
                    fiber.Choice = choice with { Options = choice.Options.Where(o => o.Available?.Invoke(_world) != false).ToArray() };
                    _choosing = fiber;
                    return;
                case StoryStep<TWorld, TActor>.Action action:
                    fiber.Execution = _bindings.Execute(_world, action.Actor, action.Build(_world));
                    _actors[fiber.Execution] = action.Actor;
                    return;
                case StoryStep<TWorld, TActor>.CoreAction coreAction:
                    var scriptActor = coreAction.Actor(_world);
                    fiber.Execution = scriptActor.World.Submit(scriptActor, coreAction.Build(_world));
                    _coreActors[fiber.Execution] = scriptActor;
                    return;
                case StoryStep<TWorld, TActor>.Parallel parallel:
                    fiber.Children = parallel.Branches.Select(b => new Fiber(b)).ToList();
                    _fibers.AddRange(fiber.Children);
                    _again = true;
                    return;
                case StoryStep<TWorld, TActor>.Repeat repeat:
                    for (var i = 0; i < repeat.Count; i++) Prepend(fiber, repeat.Steps);
                    break;
            }
        }
        _again = true;
    }
}
