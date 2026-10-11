using DualOntology.Language;
using System.Globalization;
using System.Text.Json;

// C0 is disposable. Generated compilers reference Runtime.cs only, never this reader.
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
                else if (xs.Length == 2 && Value.SymbolOf(xs[0]) == "main" && !main) { main = true; output += "public static int Main(string[] args) { R.Arguments=args; try { _ = " + Expr(xs[1]) + "; return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.Message); return 1; } }\n"; }
                else throw new Exception("Invalid top-level declaration");
            }
            if (!main) throw new Exception("Missing main");
            File.WriteAllText(args[1], output + "}\n", new System.Text.UTF8Encoding(false));
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
    }
}
