using DuelView.Compiled;
public static class DVProgram {
static object F_odd(object v_n) => (R.Bool(R.eq(v_n,R.number("0"))) ? (object)(false) : (object)(F_even(R.sub(v_n,R.number("1")))));
static object F_even(object v_n) => (R.Bool(R.eq(v_n,R.number("0"))) ? (object)(true) : (object)(F_odd(R.sub(v_n,R.number("1")))));
public static int Main(string[] args) { R.Arguments=args; try { _ = R.print(F_odd(R.number("9"))); return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.Message); return 1; } }
}
