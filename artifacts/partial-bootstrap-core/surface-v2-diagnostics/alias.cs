using DuelView.Compiled;
public static class DVProgram {
static object F_平方(object v_x) {
return R.mul(v_x,v_x);
}
static object F_main() {
_ = R.print(F_平方(R.number("3")));
return false;
}
public static int Main(string[] args) { R.Arguments=args; try { _ = F_main(); return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.ToString()); return 1; } }
}
