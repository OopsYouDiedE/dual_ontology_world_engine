using DuelView.Compiled;
public static partial class DVProgram {
static object F_source(object v_counter) {
_ = R.replace(v_counter,R.number("0"),R.add(R.at(v_counter,R.number("0")),R.number("1")));
return new System.Collections.Generic.List<object> {R.number("1"),R.number("2"),R.number("3")};
}
static object F_classify(object v_x) {
if (R.Bool(R.lt(v_x,R.number("0")))) {
return "negative";
} else {
if (R.Bool(R.eq(v_x,R.number("0")))) {
return "zero";
} else {
if (R.Bool(R.eq(v_x,R.number("1")))) {
return "one";
} else {
return "many";
}
}
}
}
static object F_probe() {
object v_counter__330 = new System.Collections.Generic.List<object> {R.number("0")};
object v_x__353 = R.number("10");
object v_mapped__369 = R.Block(() => { object comp_source_382 = F_source(v_counter__330);var comp_result_382 = new System.Collections.Generic.List<object>();for (int comp_i_382 = 0; comp_i_382 < R.Items(comp_source_382).Count; comp_i_382++) {object v_x__comp_382 = R.at(comp_source_382, (double)comp_i_382);if (R.Bool(R.lt(R.number("1"),v_x__comp_382))) comp_result_382.Add(R.add(v_x__comp_382,R.number("10")));}return (object)comp_result_382;});
object v_nested__431 = R.Block(() => { object comp_source_444 = new System.Collections.Generic.List<object> {R.number("1"),R.number("2")};var comp_result_444 = new System.Collections.Generic.List<object>();for (int comp_i_444 = 0; comp_i_444 < R.Items(comp_source_444).Count; comp_i_444++) {object v_x__comp_444 = R.at(comp_source_444, (double)comp_i_444);if (R.Bool(true)) comp_result_444.Add(R.Block(() => { object comp_source_462 = new System.Collections.Generic.List<object> {v_x__comp_444,R.add(v_x__comp_444,R.number("1"))};var comp_result_462 = new System.Collections.Generic.List<object>();for (int comp_i_462 = 0; comp_i_462 < R.Items(comp_source_462).Count; comp_i_462++) {object v_x__comp_462 = R.at(comp_source_462, (double)comp_i_462);if (R.Bool(true)) comp_result_462.Add(R.mul(v_x__comp_462,R.number("2")));}return (object)comp_result_462;}));}return (object)comp_result_444;});
object v_captured__497 = R.Block(() => { object comp_source_512 = new System.Collections.Generic.List<object> {R.number("1"),R.number("2")};var comp_result_512 = new System.Collections.Generic.List<object>();for (int comp_i_512 = 0; comp_i_512 < R.Items(comp_source_512).Count; comp_i_512++) {object v_y__comp_512 = R.at(comp_source_512, (double)comp_i_512);if (R.Bool(true)) comp_result_512.Add(R.add(v_x__353,v_y__comp_512));}return (object)comp_result_512;});
object v_empty__542 = R.Block(() => { object comp_source_554 = new System.Collections.Generic.List<object> {};var comp_result_554 = new System.Collections.Generic.List<object>();for (int comp_i_554 = 0; comp_i_554 < R.Items(comp_source_554).Count; comp_i_554++) {object v_z__comp_554 = R.at(comp_source_554, (double)comp_i_554);if (R.Bool(true)) comp_result_554.Add(v_z__comp_554);}return (object)comp_result_554;});
object v_excluded__576 = R.Block(() => { object comp_source_591 = new System.Collections.Generic.List<object> {R.number("1"),R.number("2")};var comp_result_591 = new System.Collections.Generic.List<object>();for (int comp_i_591 = 0; comp_i_591 < R.Items(comp_source_591).Count; comp_i_591++) {object v_z__comp_591 = R.at(comp_source_591, (double)comp_i_591);if (R.Bool(false)) comp_result_591.Add(R.fail("projection must not run"));}return (object)comp_result_591;});
return new System.Collections.Generic.List<object> {R.at(v_counter__330,R.number("0")),v_x__353,v_mapped__369,v_nested__431,v_captured__497,v_empty__542,v_excluded__576,F_classify(R.sub(R.number("0"),R.number("1"))),F_classify(R.number("0")),F_classify(R.number("1")),F_classify(R.number("2"))};
}
public static int Main(string[] args) { R.Arguments=args; try { _ = R.print(R.json_stringify(F_probe())); return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.ToString()); return 1; } }
}
