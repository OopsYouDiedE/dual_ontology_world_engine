using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace DualOntology.Language;

public static class Reader
{
    public static ImmutableArray<Value> ReadAll(string source)
    {
        if (source.Length > 1_048_576) throw new LanguageException("Source size limit exceeded.");
        return new Parser(source).All();
    }
    public static Value Read(string source)
    {
        var values = ReadAll(source);
        return values.Length == 1 ? values[0] : throw new LanguageException("Expected exactly one expression.");
    }
    private sealed class Parser(string source)
    {
        private int offset, nodes;
        private LanguageException Error(string message)
        {
            var before = source[..offset];
            return new($"{before.Count(c => c == '\n') + 1}:{offset - before.LastIndexOf('\n')}: {message}");
        }
        private void Skip()
        {
            while (offset < source.Length)
            {
                if (char.IsWhiteSpace(source[offset])) offset++;
                else if (source[offset] == ';') { while (offset < source.Length && source[offset] != '\n') offset++; }
                else break;
            }
        }
        public ImmutableArray<Value> All()
        {
            var result = ImmutableArray.CreateBuilder<Value>();
            Skip();
            while (offset < source.Length) { result.Add(Form(0)); Skip(); }
            return result.ToImmutable();
        }
        private Value Form(int depth)
        {
            Skip();
            if (depth > 128 || ++nodes > 100_000) throw Error("Reader budget exhausted.");
            if (offset == source.Length) throw Error("Unexpected end of source.");
            var ch = source[offset++];
            if (ch == '\'') return Value.Seq(new Value.Symbol("quote"), Form(depth + 1));
            if (ch == '(')
            {
                var items = ImmutableArray.CreateBuilder<Value>();
                Skip();
                while (offset < source.Length && source[offset] != ')') { items.Add(Form(depth + 1)); Skip(); }
                if (offset == source.Length) throw Error("Missing closing parenthesis.");
                offset++;
                return new Value.List(items.ToImmutable());
            }
            if (ch == ')') throw Error("Unexpected closing parenthesis.");
            var start = offset - 1;
            if (ch == '"')
            {
                while (offset < source.Length)
                {
                    var next = source[offset++];
                    if (next == '\\') { if (offset < source.Length) offset++; }
                    else if (next == '"')
                    {
                        try { return new Value.Text(JsonSerializer.Deserialize<string>(source[start..offset])!); }
                        catch (JsonException) { throw Error("Invalid string escape."); }
                    }
                }
                throw Error("Unterminated string.");
            }
            while (offset < source.Length && !char.IsWhiteSpace(source[offset]) && source[offset] is not '(' and not ')' and not ';' and not '\'') offset++;
            var token = source[start..offset];
            return token switch
            {
                "nil" => Value.Null,
                "true" => Value.True,
                "false" => Value.False,
                _ when double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => new Value.Number(number),
                _ => new Value.Symbol(token)
            };
        }
    }
}
