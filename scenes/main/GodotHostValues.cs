using Godot;
using System.Collections;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

/// <summary>Convert only changed immutable Core values into Godot's value graph.</summary>
internal static class GodotHostValues
{
    // Capture host evidence directly, before a caller can mutate its containers.
    // Do not route typed Godot values through JSON's unrelated parser depth cap.
    public static object? Native(Variant value) => value.VariantType switch
    {
        Variant.Type.Nil => null,
        Variant.Type.Bool => value.AsBool(),
        Variant.Type.Int => value.AsInt64(),
        Variant.Type.Float => value.AsDouble(),
        // GDScript input names and scene paths are textual atoms too.
        Variant.Type.String or Variant.Type.StringName or Variant.Type.NodePath => value.AsString(),
        Variant.Type.PackedInt32Array => value.AsInt32Array().Select(item => (object?)(long)item).ToArray(),
        Variant.Type.PackedInt64Array => value.AsInt64Array().Select(item => (object?)item).ToArray(),
        Variant.Type.PackedFloat32Array => value.AsFloat32Array().Select(item => (object?)(double)item).ToArray(),
        Variant.Type.PackedFloat64Array => value.AsFloat64Array().Select(item => (object?)item).ToArray(),
        Variant.Type.PackedStringArray => value.AsStringArray().Select(item => (object?)item).ToArray(),
        Variant.Type.Dictionary => value.AsGodotDictionary().ToDictionary(
            pair => pair.Key.AsString(), pair => Native(pair.Value), StringComparer.Ordinal),
        Variant.Type.Array => value.AsGodotArray().Select(Native).ToArray(),
        // Match Godot 4.6 core/io/json.cpp JSON::_stringify's default:
        // vectors, integer vectors, colors, rectangles, transforms, packed
        // byte/vector/color arrays and other non-JSON atoms become their
        // Godot String representation. Only the array types listed above
        // become JSON arrays. This preserves existing trace payload shapes
        // while containers still bypass JSON parsing and its depth limit.
        _ => value.AsString()
    };
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
