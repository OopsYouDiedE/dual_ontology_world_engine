using DuelView.Compiled;
public static class DVProgram {
public static int Main(string[] args) { R.Arguments=args; try { _ = R.Block(() => {object v_x = R.number("7");object v_y = R.add(v_x,R.number("2"));return (object)(R.print(v_y));}); return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.Message); return 1; } }
}
