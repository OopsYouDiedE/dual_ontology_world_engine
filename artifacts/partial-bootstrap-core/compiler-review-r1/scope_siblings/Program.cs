using DuelView.Compiled;
public static class DVProgram {
public static int Main(string[] args) { R.Arguments=args; try { _ = R.Block(() => {_ = R.Block(() => {object v_x = R.number("7");return (object)(R.print(v_x));});return (object)(R.Block(() => {object v_x = R.number("9");return (object)(R.print(v_x));}));}); return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.Message); return 1; } }
}
