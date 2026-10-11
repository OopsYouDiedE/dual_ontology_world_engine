using DuelView.Compiled;
public static class DVProgram {
public static int Main(string[] args) { R.Arguments=args; try { _ = R.print(R.add(R.number("17"),R.number("26"))); return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.Message); return 1; } }
}
