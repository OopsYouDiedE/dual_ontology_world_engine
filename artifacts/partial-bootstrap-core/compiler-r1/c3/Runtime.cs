using System.Globalization;
using System.Text;

namespace DuelView.Compiled;

// Platform/value ABI only. No reader, AST walker, evaluator or code generator.
public static class R
{
    public static string[] Arguments = [];
    public static string Text(object x) => x as string ?? throw new Exception("Expected Text");
    public static double Num(object x) => x is double n && double.IsFinite(n) ? n : throw new Exception("Expected finite Number");
    public static bool Bool(object x) => x is bool b ? b : throw new Exception("Expected Bool");
    public static List<object> Items(object x) => x as List<object> ?? throw new Exception("Expected List");
    public static object number(object s) => Num(double.Parse(Text(s), NumberStyles.Float, CultureInfo.InvariantCulture));
    public static object is_number(object s) => double.TryParse(Text(s), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n);
    public static object add(object a, object b) => Num(Num(a) + Num(b));
    public static object sub(object a, object b) => Num(Num(a) - Num(b));
    public static object mul(object a, object b) => Num(Num(a) * Num(b));
    public static object div(object a, object b) => Num(Num(a) / Num(b));
    public static object lt(object a, object b) => Num(a) < Num(b);
    public static object eq(object a, object b) => Equals(a, b);
    public static object not(object a) => !Bool(a);
    public static object cat(object a, object b) => Text(a) + Text(b);
    public static object length(object a) => (double)Text(a).Length;
    private static int Index(object a) { var n = Num(a); return n >= 0 && n <= int.MaxValue && n == Math.Truncate(n) ? (int)n : throw new Exception("Invalid index"); }
    public static object slice(object a, object start, object count) => Text(a).Substring(Index(start), Index(count));
    public static object chr(object a, object i) => Text(a)[Index(i)].ToString();
    public static object code(object a) => Text(a).Length == 1 ? (double)Text(a)[0] : throw new Exception("Expected one UTF-16 unit");
    public static object show(object a) => a switch { string s => s, double n => n.ToString("R", CultureInfo.InvariantCulture), bool b => b ? "true" : "false", _ => throw new Exception("Cannot format value") };
    public static object list() => new List<object>();
    public static object push(object a, object b) { Items(a).Add(b); return a; }
    public static object at(object a, object i) => Items(a)[Index(i)];
    public static object count(object a) => (double)Items(a).Count;
    public static object is_list(object a) => a is List<object>;
    public static object fail(object a) => throw new Exception(Text(a));
    public static object arg(object i) => Arguments[Index(i)];
    public static object read(object p) => File.ReadAllText(Text(p), new UTF8Encoding(false, true));
    public static object write(object p, object s) { File.WriteAllText(Text(p), Text(s), new UTF8Encoding(false, true)); return s; }
    public static object print(object a) { Console.WriteLine(Text(show(a))); return a; }
    public static object Block(Func<object> f) => f();
}
