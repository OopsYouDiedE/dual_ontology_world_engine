using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Story;

/// <summary>脚本人物持有原 Core 的身体、灵魂与自我投影引用，不依赖章节枚举。</summary>
public sealed class ScriptCharacter
{
    internal ScriptCharacter(CoreScriptWorld world, string key, SoulEssence soul, Essence body, Projection self)
        => (World, Key, Soul, Body, Self) = (world, key, soul, body, self);
    public CoreScriptWorld World { get; }
    public string Key { get; }
    public SoulEssence Soul { get; }
    public Essence Body { get; }
    public Projection Self { get; }
    /// <summary>宿主复用的原 SpriteFrames 路径；宿主仍调用原 StoryActor.gd。</summary>
    public string? SpriteFramesPath { get; set; }
    internal Func<bool>? Busy;
    internal Action<ActionExecution>? Submitted;
}

/// <summary>两章原世界的脚本声明入口。这里只保存名称索引，权威状态仍是原 Essence/WorldRuntime/SimulationSession。</summary>
public sealed class CoreScriptWorld(WorldRuntime runtime, SimulationSession session)
{
    public WorldRuntime Runtime { get; } = runtime;
    public SimulationSession Session { get; } = session;
    public Dictionary<string, Essence> Objects { get; } = [];
    public Dictionary<string, ScriptCharacter> Characters { get; } = [];
    private readonly HashSet<ScriptCharacter> _created = [];
    public Essence this[string key] => Objects[key];

    public Essence Register(string key, Essence essence)
    {
        Objects.Add(key, essence);
        if (Runtime.Representations(essence).Count == 0) Runtime.Project(essence, essence.TrueName);
        return essence;
    }

    public ScriptCharacter RegisterCharacter(string key, SoulEssence soul, Essence body, Projection self,
        Func<bool>? busy = null, Action<ActionExecution>? submitted = null)
    {
        Register(key, body);
        if (Runtime.Representations(soul).Count == 0) Runtime.Project(soul, soul.TrueName);
        var character = new ScriptCharacter(this, key, soul, body, self) { Busy = busy, Submitted = submitted };
        Characters.Add(key, character);
        return character;
    }

    public Essence DefineSpace(string key, string name)
    {
        var space = new Essence(name, ProjectionKind.Space);
        space.Add(new StateProperty());
        Register(key, space);
        return space;
    }

    public Essence DefineObject(string key, string name, Essence space, Action<Essence>? configure = null,
        ProjectionKind kind = ProjectionKind.Object)
    {
        var entity = new Essence(name, kind);
        entity.Add(new SpatialProperty { Space = space });
        entity.Add(new StateProperty());
        configure?.Invoke(entity);
        return Register(key, entity);
    }

    public ScriptCharacter DefineCharacter(string key, string name, Essence space, string? spriteFramesPath = null,
        Action<Essence>? configureBody = null)
    {
        var body = new Essence(name, ProjectionKind.Body);
        body.Add(new SpatialProperty { Space = space });
        body.Add(new StateProperty());
        configureBody?.Invoke(body);
        var soul = new SoulEssence(name + "之魂");
        soul.Relations.Add((Relations.Controls, body));
        Runtime.Project(body, name);
        Runtime.Project(soul, soul.TrueName);
        var seen = Runtime.Observe(soul, Runtime.Representations(space).Concat(Runtime.Representations(body)), Session.CTB.Now);
        var character = RegisterCharacter(key, soul, body, seen.Single(p => p.Kind == ProjectionKind.Body));
        character.SpriteFramesPath = spriteFramesPath;
        _created.Add(character);
        return character;
    }

    public ImmutableArray<Projection> Observe(ScriptCharacter character, params Essence[] visible)
        => Runtime.Observe(character.Soul, visible.SelectMany(Runtime.Representations), Session.CTB.Now);

    public ImmutableArray<Projection> ObserveRoom(ScriptCharacter character)
    {
        var room = character.Body.Get<SpatialProperty>()!.Space;
        var local = Objects.Values.Where(e => ReferenceEquals(e, room) || ReferenceEquals(e, character.Body) ||
            ReferenceEquals(e.Get<SpatialProperty>()?.Holder?.Get<SpatialProperty>()?.Space ?? e.Get<SpatialProperty>()?.Space, room));
        return Runtime.Observe(character.Soul, local.Distinct().SelectMany(Runtime.Representations), Session.CTB.Now);
    }

    public bool IsBusy(ScriptCharacter actor) => actor.Busy?.Invoke() == true || Session.IsActing(actor.Self);
    public ActionExecution Submit(ScriptCharacter actor, Effect effect)
    {
        var execution = Session.Submit(actor.Soul, new(actor.Self, effect, 0));
        actor.Submitted?.Invoke(execution);
        return execution;
    }

    /// <summary>沿用 Core 自主决策、观察唤醒和调度；并非预写人物动作列表。</summary>
    public void Activate(ScriptCharacter actor, double delay = 1) => Session.Activate(actor.Soul, actor.Self, delay);
    public void Deactivate(ScriptCharacter actor) => Session.Deactivate(actor.Self);
    public void RefreshAfter(ActionExecution execution)
    {
        foreach (var actor in _created.Where(c => ReferenceEquals(c.Self, execution.Intent.Actor))) ObserveRoom(actor);
    }
}
