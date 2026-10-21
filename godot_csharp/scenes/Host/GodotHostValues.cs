using Godot;
using System.Collections;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

/// <summary>Convert only changed immutable Core values into Godot's value graph.</summary>
internal static class GodotHostValues
{
    public static GDict Dictionary(IReadOnlyDictionary<string, object?> values)
    {
        GDict result = new();
        foreach (var pair in values) result[pair.Key] = Value(pair.Value);
        return result;
    }
    public static Variant Value(object? value) => value switch
    {
        null => default,
        bool boolean => boolean,
        string text => text,
        byte number => (long)number,
        short number => (long)number,
        int number => (long)number,
        long number => number,
        float number => (double)number,
        double number => number,
        decimal number => (double)number,
        IReadOnlyDictionary<string, object?> record => Dictionary(record),
        IEnumerable sequence => Sequence(sequence),
        _ => throw new ArgumentException($"Unsupported host value {value.GetType().Name}")
    };
    private static GArray Sequence(IEnumerable values)
    {
        GArray result = new();
        foreach (var item in values) result.Add(Value(item));
        return result;
    }
}
