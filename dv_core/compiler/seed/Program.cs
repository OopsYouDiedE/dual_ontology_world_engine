using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using DualOntology.Language;

// C0 is disposable. Generated compilers reference Runtime.cs only, never this reader.
namespace DualOntology.Language
{
    public sealed class LanguageException(string message) : Exception(message);

    public abstract record Value
    {
        public sealed record Symbol(string Name) : Value;
        public sealed record Text(string Data) : Value;
        public sealed record Number : Value
        {
            public Number(double data)
            {
                if (!double.IsFinite(data)) throw new LanguageException("Non-finite number.");
                Data = data;
            }
            public double Data { get; }
        }
        public sealed record Boolean(bool Data) : Value;
        public sealed record Nil : Value;
        public sealed record List(ImmutableArray<Value> Items) : Value;

        public static readonly Value Null = new Nil();
        public static readonly Value True = new Boolean(true);
        public static readonly Value False = new Boolean(false);
        public static List Seq(params Value[] items) => new([.. items]);
        public static bool IsTruthy(Value value) => value is not Nil and not Boolean { Data: false };
        public static ImmutableArray<Value> ItemsOf(Value value) => value is List list ? list.Items : throw new LanguageException("Expected list.");
        public static double NumberOf(Value value) => value is Number number ? number.Data : throw new LanguageException("Expected number.");
        public static string SymbolOf(Value value) => value is Symbol symbol ? symbol.Name : throw new LanguageException("Expected symbol.");
        public static bool Same(Value left, Value right) => (left, right) switch
        {
            (List a, List b) => a.Items.Length == b.Items.Length && a.Items.Zip(b.Items).All(p => Same(p.First, p.Second)),
            _ => left == right
        };
        public static string Print(Value value) => value switch
        {
            Symbol symbol => symbol.Name,
            Text text => JsonSerializer.Serialize(text.Data),
            Number number => number.Data.ToString("R", CultureInfo.InvariantCulture),
            Boolean boolean => boolean.Data ? "true" : "false",
            Nil => "nil",
            List list => "(" + string.Join(" ", list.Items.Select(Print)) + ")",
            _ => throw new LanguageException("Unsupported value.")
        };
    }

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
}

static class Seed
{
    static string Name(Value v)
    {
        var s = Value.SymbolOf(v);
        if (s.Length == 0 || !(char.IsAsciiLetter(s[0]) || s[0] == '_') || s.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_'))) throw new Exception("Invalid identifier: " + s);
        return s;
    }
    static string Str(string s) => JsonSerializer.Serialize(s);
    static string Expr(Value v)
    {
        if (v is Value.Text t) return Str(t.Data);
        if (v is Value.Number n) return "R.number(" + Str(n.Data.ToString("R", CultureInfo.InvariantCulture)) + ")";
        if (v is Value.Boolean b) return b.Data ? "true" : "false";
        if (v is Value.Symbol) return "v_" + Name(v);
        var xs = Value.ItemsOf(v);
        if (xs.Length == 0) throw new Exception("Empty expression");
        var h = Name(xs[0]);
        switch (h)
        {
            case "if":
                if (xs.Length != 4) throw new Exception("if arity");
                return "(R.Bool(" + Expr(xs[1]) + ") ? (object)(" + Expr(xs[2]) + ") : (object)(" + Expr(xs[3]) + "))";
            case "do":
                if (xs.Length < 2) throw new Exception("do arity");
                return "R.Block(() => {" + string.Concat(xs.Skip(1).SkipLast(1).Select(x => "_ = " + Expr(x) + ";")) + "return (object)(" + Expr(xs[^1]) + ");})";
            case "let":
                if (xs.Length != 3) throw new Exception("let arity");
                var bs = Value.ItemsOf(xs[1]);
                if (bs.Length % 2 != 0) throw new Exception("let bindings");
                var body = "R.Block(() => {";
                for (int i = 0; i < bs.Length; i += 2) body += "object v_" + Name(bs[i]) + " = " + Expr(bs[i + 1]) + ";";
                return body + "return (object)(" + Expr(xs[2]) + ");})";
            case "set":
                if (xs.Length != 3) throw new Exception("set arity");
                return "(v_" + Name(xs[1]) + " = " + Expr(xs[2]) + ")";
            case "while":
                if (xs.Length != 3) throw new Exception("while arity");
                return "R.Block(() => {while(R.Bool(" + Expr(xs[1]) + ")) { _ = " + Expr(xs[2]) + ";} return false;})";
            default: return (h.StartsWith("rt_", StringComparison.Ordinal) ? "R." + h[3..] : "F_" + h) + "(" + string.Join(",", xs.Skip(1).Select(Expr)) + ")";
        }
    }
    static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2) throw new Exception("Usage: Seed input.dv output.cs");
            var forms = Reader.ReadAll(File.ReadAllText(args[0]));
            var output = "using DuelView.Compiled;\npublic static class DVProgram {\n";
            bool main = false;
            foreach (var f in forms)
            {
                var xs = Value.ItemsOf(f);
                if (xs.Length == 4 && Value.SymbolOf(xs[0]) == "defn") output += "static object F_" + Name(xs[1]) + "(" + string.Join(",", Value.ItemsOf(xs[2]).Select(p => "object v_" + Name(p))) + ") => " + Expr(xs[3]) + ";\n";
                else if (xs.Length == 2 && Value.SymbolOf(xs[0]) == "main" && !main) { main = true; output += "public static int Main(string[] args) { R.Arguments=args; try { _ = " + Expr(xs[1]) + "; return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.ToString()); return 1; } }\n"; }
                else throw new Exception("Invalid top-level declaration");
            }
            if (!main) throw new Exception("Missing main");
            File.WriteAllText(args[1], output + "}\n", new System.Text.UTF8Encoding(false));
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e.ToString()); return 1; }
    }
}
