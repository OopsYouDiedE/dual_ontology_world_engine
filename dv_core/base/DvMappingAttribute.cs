namespace DuelView.Core;

/// <summary>Marks the script-facing mapping convention of a native API.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DvMappingAttribute : Attribute { }
