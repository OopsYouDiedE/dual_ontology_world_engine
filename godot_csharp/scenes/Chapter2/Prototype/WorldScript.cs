using System.Runtime.Loader;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>普通 C# 脚本程序集的唯一入口；两种宿主执行同一份定义。</summary>
public interface IWorldScript
{
    WorldScriptDefinition Define();
}

public sealed class WorldScriptDefinition
{
    public GameOptions Options { get; init; } = new();
    public Action<Game>? Initialize { get; init; }
    public IReadOnlyList<StorySequence> Sequences { get; init; } = [];
    public Game CreateGame() => new(Options with { Definition = this });
}

public static class WorldScript
{
    public static WorldScriptDefinition Load(string assemblyPath)
    {
        // 与宿主的契约程序集共用加载上下文，复用已加载的 Prototype/Core 类型。
        var context = AssemblyLoadContext.GetLoadContext(typeof(IWorldScript).Assembly)!;
        var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
        var entry = assembly.GetExportedTypes().Single(t => !t.IsAbstract && typeof(IWorldScript).IsAssignableFrom(t));
        return ((IWorldScript)Activator.CreateInstance(entry)!).Define();
    }
}

public sealed record StorySequence(string Id, int Day, Slot Slot, IReadOnlyList<StoryStep> Steps, Func<Game, bool>? Trigger = null);
public abstract record StoryStep;
public sealed record StoryText(string Text) : StoryStep;
public enum StoryPresentationKind { Image, Audio, Animation }
public sealed record StoryPresentation(StoryPresentationKind Kind, string Content, P? Actor = null, int Sequence = 0) : StoryStep;
public sealed record StoryEffect(P Actor, Func<Game, Effect> Build) : StoryStep;
public sealed record StoryCoreEffect(Func<Game, DualOntologyWorldEngine.Story.ScriptCharacter> Actor, Func<Game, Effect> Build) : StoryStep
{
    public StoryCoreEffect(DualOntologyWorldEngine.Story.ScriptCharacter actor, Func<Game, Effect> build) : this(_ => actor, build) { }
}
public sealed record StoryWait(Func<Game, bool> Until) : StoryStep;
public sealed record StoryBranch(Func<Game, bool> When, IReadOnlyList<StoryStep> Then, IReadOnlyList<StoryStep> Otherwise) : StoryStep;
public sealed record StoryOption(string Id, string Text, IReadOnlyList<StoryStep> Steps, Func<Game, bool>? Available = null);
public sealed record StoryChoice(string Id, string Prompt, IReadOnlyList<StoryOption> Options) : StoryStep;
public sealed record StoryParallel(IReadOnlyList<IReadOnlyList<StoryStep>> Branches) : StoryStep;
public sealed record StoryRepeat(int Count, IReadOnlyList<StoryStep> Steps) : StoryStep;

/// <summary>第二章只绑定 Game 对象；两章共用 Shared/Story 的执行内核。</summary>
public sealed class StoryRuntime
{
    private readonly Game _game;
    private readonly DualOntologyWorldEngine.Story.StoryRuntime<Game, P?> _runtime;
    public StoryChoice? PendingChoice => _runtime.PendingChoice is { } choice
        ? new(choice.Id, choice.Prompt, choice.Options.Select(o => new StoryOption(o.Id, o.Text, [])).ToArray())
        : null;
    public Queue<StoryPresentation> Presentations { get; } = new();
    public const string MediaMarker = "\u001estory-media:";
    public const string TextMarker = "\u001estory-text:";
    private int _mediaSequence;
    public bool Controls(P actor) => _runtime.Controls(actor) || _game.DefinedScriptWorld is { } world && _runtime.Controls(world.Characters["person:" + actor]);
    public bool Owns(ActionExecution execution) => _runtime.Owns(execution);
    public event Action<string, string>? Chosen
    {
        add => _runtime.Chosen += value;
        remove => _runtime.Chosen -= value;
    }

    public StoryRuntime(Game game, IReadOnlyList<StorySequence> sequences)
    {
        _game = game;
        var bindings = new DualOntologyWorldEngine.Story.StoryBindings<Game, P?>
        {
            Text = (g, text) => g.Feed.Add(TextMarker + text),
            Present = (g, content) =>
            {
                var sequence = _mediaSequence++;
                Presentations.Enqueue(new((StoryPresentationKind)content.Kind, content.Content, content.Actor, sequence));
                g.Feed.Add(MediaMarker + sequence);
            },
            Busy = (g, actor) => g[actor!.Value].Exec is not null,
            Execute = (g, actor, effect) =>
            {
                var person = g[actor!.Value];
                var execution = g.S.Submit(person.Soul, new(person.Self, effect, 0));
                person.Exec = execution;
                return execution;
            },
            Timeline = g => g.S.CTB
        };
        _runtime = new(game, bindings, sequences.Select(s =>
            new DualOntologyWorldEngine.Story.StorySequence<Game, P?>(s.Id,
                g => (s.Day < g.Day || s.Day == g.Day && s.Slot <= g.Slot) && s.Trigger?.Invoke(g) != false,
                Convert(s.Steps))).ToArray());
        game.S.Completed += _ => Poll();
    }

    public void Choose(string optionId) => _runtime.Choose(optionId);
    public void Poll() => _runtime.Poll();
    public void Pump() => _runtime.Pump();

    private static IReadOnlyList<DualOntologyWorldEngine.Story.StoryStep<Game, P?>> Convert(IReadOnlyList<StoryStep> steps)
        => steps.Select(Convert).ToArray();

    private static DualOntologyWorldEngine.Story.StoryStep<Game, P?> Convert(StoryStep step) => step switch
    {
        StoryText text => new DualOntologyWorldEngine.Story.StoryStep<Game, P?>.Text(text.Text),
        StoryPresentation presentation => new DualOntologyWorldEngine.Story.StoryStep<Game, P?>.Presentation(
            new((DualOntologyWorldEngine.Story.StoryPresentationKind)presentation.Kind, presentation.Content, presentation.Actor)),
        StoryEffect effect => new DualOntologyWorldEngine.Story.StoryStep<Game, P?>.Action(effect.Actor, effect.Build),
        StoryCoreEffect effect => new DualOntologyWorldEngine.Story.StoryStep<Game, P?>.CoreAction(effect.Actor, effect.Build),
        StoryWait wait => new DualOntologyWorldEngine.Story.StoryStep<Game, P?>.Wait(wait.Until),
        StoryBranch branch => new DualOntologyWorldEngine.Story.StoryStep<Game, P?>.Branch(branch.When, Convert(branch.Then), Convert(branch.Otherwise)),
        StoryChoice choice => new DualOntologyWorldEngine.Story.StoryStep<Game, P?>.Choice(choice.Id, choice.Prompt,
            choice.Options.Select(o => new DualOntologyWorldEngine.Story.StoryOption<Game, P?>(o.Id, o.Text, Convert(o.Steps), o.Available)).ToArray()),
        StoryParallel parallel => new DualOntologyWorldEngine.Story.StoryStep<Game, P?>.Parallel(parallel.Branches.Select(Convert).ToArray()),
        StoryRepeat repeat => new DualOntologyWorldEngine.Story.StoryStep<Game, P?>.Repeat(repeat.Count, Convert(repeat.Steps)),
        _ => throw new ArgumentException($"未绑定的剧情步骤：{step.GetType().Name}", nameof(step))
    };
}
