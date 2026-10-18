using DuelView.Compiled;
public static partial class DVProgram {
static object F_probe() {
return R.Block(() => { object comp_source_13 = new System.Collections.Generic.List<object> {R.number("1")};var comp_result_13 = new System.Collections.Generic.List<object>();for (int comp_i_13 = 0; comp_i_13 < R.Items(comp_source_13).Count; comp_i_13++) {object v_x__comp_13 = R.at(comp_source_13, (double)comp_i_13);if (R.Bool(R.number("1"))) comp_result_13.Add(v_x__comp_13);}return (object)comp_result_13;});
}
public static int Main(string[] args) { R.Arguments=args; try { _ = F_probe(); return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.ToString()); return 1; } }
}
