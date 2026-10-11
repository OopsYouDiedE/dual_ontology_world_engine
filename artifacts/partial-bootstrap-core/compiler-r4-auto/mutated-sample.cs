using DuelView.Compiled;
public static class DVChanged {
static object F_sum(object v_n) => (R.Bool(R.lt(v_n,R.number("1"))) ? (object)(R.number("0")) : (object)(R.add(v_n,F_sum(R.sub(v_n,R.number("1"))))));
public static int Main(string[] args) { R.Arguments=args; try { _ = R.Block(() => {object v_i = R.number("0");object v_total = R.number("0");return (object)(R.Block(() => {_ = R.Block(() => {while(R.Bool(R.lt(v_i,R.number("5")))) { _ = R.Block(() => {_ = (v_total = R.add(v_total,v_i));return (object)((v_i = R.add(v_i,R.number("1"))));});} return false;});return (object)(R.print(R.add(v_total,F_sum(R.number("10")))));}));}); return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.Message); return 1; } }
}
