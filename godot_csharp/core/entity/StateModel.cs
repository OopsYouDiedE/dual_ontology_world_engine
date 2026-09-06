namespace DualOntologyWorldEngine.Core;

/// <summary>实体状态槽的非泛型边界。名称只参与诊断，槽对象本身才是状态键。</summary>
public interface IStateField
{
    string Name { get; }
    Type ValueType { get; }
    object? DefaultValue { get; }
}

/// <summary>可由作品声明的强类型状态槽。每个槽对象代表唯一机制语义。</summary>
public sealed class StateField<T>(string name, T? defaultValue = default) : IStateField
{
    public string Name { get; } = name;
    public Type ValueType => typeof(T);
    public T? Default { get; } = defaultValue;
    object? IStateField.DefaultValue => Default;
    public override string ToString() => Name;
}

/// <summary>核心内稳定使用的实体状态槽。</summary>
public static class CoreFields
{
    public static readonly StateField<bool> Opened = new("开启", false);
    public static readonly StateField<double> Charges = new("充能", 0d);
    public static readonly StateField<double> Fuel = new("燃料", 0d);
    public static readonly StateField<double> Integrity = new("完整度", 0d);
    public static readonly StateField<double> Doses = new("剂量", 0d);
    public static readonly StateField<bool> Active = new("生效", false);
    public static readonly StateField<bool> Completed = new("完成", false);
    public static readonly StateField<bool> Accepted = new("接受", false);
    public static readonly StateField<bool> Fired = new("已触发", false);
    public static readonly StateField<bool> Sealed = new("封存", false);
    public static readonly StateField<bool> Lit = new("点亮", false);
    public static readonly StateField<bool> Revealed = new("揭示", false);
    public static readonly StateField<bool> Used = new("已使用", false);
    public static readonly StateField<int> Confidence = new("置信", 0);
    public static readonly StateField<double> StartedAt = new("开始时刻", 0d);
    public static readonly StateField<double> ExpiresAt = new("到期时刻", 0d);
    public static readonly StateField<double> Duration = new("持续时长", 0d);
    public static readonly StateField<double> Decay = new("衰减", 0d);
    public static readonly StateField<string?> Text = new("文本");
    public static readonly StateField<string?> Description = new("描述");
    public static readonly StateField<Entity?> Target = new("目标");
    public static readonly StateField<Entity?> Source = new("来源");
    public static readonly StateField<Entity?> Subject = new("主体");
    public static readonly StateField<Entity?> Evidence = new("证据");
}

/// <summary>行动语义。它是规则匹配和审计使用的有限集合。</summary>
public enum ActionKind
{
    None, Wait, Rest, Move, Open, Close, Inspect, Operate, Eat, Take, Read, Drink,
    Reassure, Explode, Ping, Work, Respond, Alert, Say, Ask, Challenge, Retract,
    Silence, Conceal, StateClaim, Hurt, Light, Reveal, Protect, Disable, Consume,
    Restock, AcceptClause, Grant, Exchange, BindSelf, Name, PrepareSubstitute,
    InvokeRelease, InvokeSeal, Testify, Maintain, Expire, Record, Play, Ring, Relink,
    Cut, Seal, Unseal
}

public enum ActionPhase
{
    Main,
    BeforeReaction,
    Reaction,
    Scheduled
}
