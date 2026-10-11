using DuelView.Compiled;
public static class IndependentReviewProgram {
static object F_identity(object v_text) => v_text;
public static int Main(string[] args) { R.Arguments=args; try { _ = R.Block(() => {_ = R.print(F_identity("left\u0085mid\u2028next\u2029right"));_ = R.print("\\u2028");return (object)(R.print("\u0085\u2028\u2029"));}); return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.Message); return 1; } }
}
