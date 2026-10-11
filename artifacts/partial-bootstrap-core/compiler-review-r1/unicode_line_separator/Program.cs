using DuelView.Compiled;
public static class DVProgram {
public static int Main(string[] args) { R.Arguments=args; try { _ = R.print("alpha beta"); return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.Message); return 1; } }
}
