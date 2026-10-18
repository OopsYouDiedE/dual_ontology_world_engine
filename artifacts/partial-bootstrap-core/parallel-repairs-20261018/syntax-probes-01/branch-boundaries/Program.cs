using DuelView.Compiled;
public static partial class DVProgram {
static object F_branch(object v_x) {
if (R.Bool(R.eq(v_x,R.number("0")))) {
if (R.Bool(false)) {
return "bad-inner";
} else {
if (R.Bool(true)) {
return "nested";
} else {
}
}
} else {
if (R.Bool(R.eq(v_x,R.number("1")))) {
return "outer";
} else {
}
}
if (R.Bool(R.eq(v_x,R.number("2")))) {
return "independent";
} else {
}
return "fallback";
}
static object F_loop() {
object v_x__258 = R.number("0");
while (R.Bool(R.lt(v_x__258,R.number("2")))) {
v_x__258 = R.add(v_x__258,R.number("1"));
}
if (R.Bool(R.eq(v_x__258,R.number("2")))) {
return v_x__258;
} else {
if (R.Bool(true)) {
return R.number("99");
} else {
}
}
return R.number("98");
}
public static int Main(string[] args) { R.Arguments=args; try { _ = R.print(R.json_stringify(R.Block(() => {object v_r = R.list();return (object)(R.Block(() => {_ = R.push(v_r,F_branch(R.number("0")));_ = R.push(v_r,F_branch(R.number("1")));_ = R.push(v_r,F_branch(R.number("2")));_ = R.push(v_r,F_branch(R.number("3")));_ = R.push(v_r,F_loop());return (object)(v_r);}));}))); return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.ToString()); return 1; } }
}
