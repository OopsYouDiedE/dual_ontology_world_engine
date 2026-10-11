using DuelView.Compiled;
public static class DVProgram {
public static int Main(string[] args) { R.Arguments=args; try { _ = (R.Bool(false) ? (object)(R.add("text",R.number("2"))) : (object)(R.print("ok"))); return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.Message); return 1; } }
}
