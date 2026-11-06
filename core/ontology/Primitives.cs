// @mechanism: dual_ontology_state
// @layer: core/ontology
// @contracts: Primitives

using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using System.Buffers;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System.Threading;
using System;

// --- Source: Primitives.cs ---
namespace DuelView.Compiled
{


// Shared runtime implementation; state remains owned by the execution context.
public static partial class R
{
    public static object read_view(object value)
    {
        var caches = Caches;
        if (!caches.ReadViewEnabled || value is FrozenList) return value;
        return UnchangedSnapshot(value, caches) ?? value;
    }
    // The view must not turn reference-valued subjects or lookup keys into
    // different identities. This non-throwing guard never thaws input branches.
    // Invalid/unusual refs retain every original read and its error ordering.
    public static object read_ref_view_safe(object reference)
    {
        if (!Caches.ReadViewEnabled || !TryItems(reference, out var pairs)) return BoolFalse;
        object? objectId = null, facet = null, field = null, subject = null;
        var hasSubject = false;
        for (var i = 0; i < pairs.Count; i++)
        {
            if (!TryItems(pairs[i], out var pair) || pair.Count < 2 || pair[0] is not string key) return BoolFalse;
            switch (key)
            {
                case "object": objectId = pair[1]; break;
                case "facet": facet = pair[1]; break;
                case "field": field = pair[1]; break;
                case "subject": subject = pair[1]; hasSubject = true; break;
            }
        }
        return objectId is string && facet is string && field is string && (!hasSubject || subject is string)
            ? BoolTrue : BoolFalse;
    }
    public static string Text(object x) => x as string ?? throw new Exception("Expected Text");
    public static double Num(object x) => x is double n && double.IsFinite(n) ? n : throw new Exception("Expected finite Number");
    public static bool Bool(object x) => x is bool b ? b : throw new Exception("Expected Bool");
    public static IReadOnlyList<object> Items(object x) => TryItems(x, out var items)
        ? items : throw new Exception("Expected List");
    // Read interface is not a value-domain predicate: external arrays and other
    // IReadOnlyList implementations are deliberately not accepted as DV lists.
    public static bool IsDvList(object value) => value is List<object> or FrozenList;
    public static bool TryItems(object value, out IReadOnlyList<object> items)
    {
        switch (value)
        {
            case List<object> mutable: items = mutable; return true;
            case FrozenList frozen: items = frozen; return true;
            default: items = null!; return false;
        }
    }
    private static readonly object NumberZero = 0d;
    private static readonly object NumberOne = 1d;
    private static readonly object NumberMinusOne = -1d;
    // Numeric atoms are immutable. Reuse small exact integers without sharing
    // mutable value state; preserve the distinct binary representation of -0.
    private static readonly object[] SmallNumbers = CreateSmallNumbers();
    private static object[] CreateSmallNumbers()
    {
        var values = new object[1025];
        values[0] = NumberZero; values[1] = NumberOne;
        for (var i = 2; i < values.Length; i++) values[i] = (double)i;
        return values;
    }
    private static object NumberValue(double number)
    {
        if (!double.IsFinite(number)) throw new Exception("Expected finite Number");
        if (number >= 0 && number < SmallNumbers.Length && number == Math.Truncate(number))
        {
            if (number != 0 || BitConverter.DoubleToInt64Bits(number) == 0) return SmallNumbers[(int)number];
        }
        if (number == -1) return NumberMinusOne;
        return number;
    }
    public static object number(object s)
    {
        // Validate Text before selecting exact finite spellings. All other
        // input, including negative zero, keeps the original parse/error path.
        var text = Text(s);
        return text switch
        {
            "0" => NumberZero,
            "1" => NumberOne,
            "-1" => NumberMinusOne,
            _ => NumberValue(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture))
        };
    }
    public static object is_number(object s) => double.TryParse(Text(s), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n);
    // Runtime value predicate; unlike is_number, this never parses or requires Text.
    public static object is_text(object value) => value is string;
    public static object is_numeric_value(object value) => value is double n && double.IsFinite(n) || value is Rational q && q.Denominator.Sign > 0;
    public static object add(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Add(Rational.From(b)) : NumberValue(Num(a) + Num(b));
    public static object sub(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Subtract(Rational.From(b)) : NumberValue(Num(a) - Num(b));
    public static object mul(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Multiply(Rational.From(b)) : NumberValue(Num(a) * Num(b));
    public static object div(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Divide(Rational.From(b)) : NumberValue(Num(a) / Num(b));
    public static object lt(object a, object b) => (a is Rational || b is Rational ? Rational.From(a).CompareTo(Rational.From(b)) < 0 : Num(a) < Num(b)) ? BoolTrue : BoolFalse;
    private static bool EqValue(object a, object b) => (a is Rational || b is Rational) && (a is Rational || a is double) && (b is Rational || b is double) ? Rational.From(a).Equals(Rational.From(b)) : Equals(a, b);
    private static readonly object BoolTrue = true;
    private static readonly object BoolFalse = false;
    public static object eq(object a, object b) => EqValue(a, b) ? BoolTrue : BoolFalse;
    public static object exact(object a) => Rational.From(a);
    public static object unit(object a)
    {
        var value = Rational.From(a);
        if (value.CompareTo(Rational.From(0d)) < 0 || value.CompareTo(Rational.From(1d)) > 0) throw new Exception("Numeric refinement requires 0 <= value <= 1");
        return value;
    }
    public static object floor(object a) => NumberValue(Math.Floor(Num(a)));
    public static object not(object a) => Bool(a) ? BoolFalse : BoolTrue;
    public static object cat(object a, object b) => Text(a) + Text(b);
    // Concatenate the text fragments used by session presentation.
    public static object text_join(object values)
    {
        var result = new StringBuilder();
        foreach (var value in Items(values)) result.Append(Text(value));
        return result.ToString();
    }
    public static object length(object a) => NumberValue(Text(a).Length);
    private static int Index(object a) { var n = Num(a); return n >= 0 && n <= int.MaxValue && n == Math.Truncate(n) ? (int)n : throw new Exception("Invalid index"); }
    public static object slice(object a, object start, object count) => Text(a).Substring(Index(start), Index(count));
    public static object chr(object a, object i) => Text(a)[Index(i)].ToString();
    public static object code(object a) => Text(a).Length == 1 ? (double)Text(a)[0] : throw new Exception("Expected one UTF-16 unit");
    public static object show(object a) => a switch { string s => s, Rational q => q.ToString(), double n => n.ToString("R", CultureInfo.InvariantCulture), bool b => b ? "true" : "false", List<object> list => FormatList(list), FrozenList frozen => FormatList(frozen), _ => throw new Exception("Cannot format value") };
    private static string FormatList(IReadOnlyList<object> list)
    {
        var sb = new StringBuilder("[");
        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(show(list[i]));
        }
        sb.Append(']');
        return sb.ToString();
    }
    public static object replace(object list, object index, object value) { Invalidate(list); MutableItems(list)[Index(index)] = value; return list; }
    // Trusted newly created roots keep metadata beside the same List<object>
    // ABI. State remains lazy; external and cross-context lists still follow
    // MutableStateTable's existing weak-table fallback.
    private static List<object> NewMutableList(int capacity)
    {
        var caches = Caches;
        return caches.RuntimeSpeedEnabled
            ? new OwnedList(capacity, caches.MutableStates)
            : new List<object>(capacity);
    }
    // Strongly typed CLR bridge for Core's value constructors, like Items;
    // this is not an additional DV script primitive or a new value domain.
    public static List<object> NewValueList(int capacity) => NewMutableList(capacity);
    public static object list() => NewMutableList(0);
    public static object push(object a, object b)
    {
        var items = MutableItems(a);
        if (!MutableStates.TryGetValue(items, out var state))
        {
            items.Add(b);
            return a;
        }
        // Retain only a certified, unchanged immutable prefix. A later write to
        // any old child invalidates this prefix through the ordinary owner links.
        var prefix = state.Backbone ?? state.AppendBase;
        Invalidate(a);
        items.Add(b);
        state.AppendBase = prefix;
        return a;
    }
    public static object at(object a, object i) => ReadItem(a, Index(i));
    // Persistent spine only: mirror dvs_replace's equality and selected reads.
    public static object list_with_index(object items, object index, object value)
    {
        var source = Items(items);
        var result = NewMutableList(source.Count);
        if (index is double selected)
        {
            // The source position is always a finite non-negative integer.
            // Double equality therefore has the same result as eq here,
            // including negative zero, fractions and non-finite indices.
            for (var i = 0; i < source.Count; i++)
                result.Add(i == selected ? value : ReadItem(items, i));
            return result;
        }
        for (var i = 0; i < source.Count; i++)
            result.Add(Bool(eq(NumberValue(i), index)) ? value : ReadItem(items, i));
        return result;
    }
    public static object count(object a) => a switch
    {
        FrozenList frozen => NumberValue(frozen.Count),
        List<object> mutable => NumberValue(mutable.Count),
        _ => NumberValue(Items(a).Count)
    };
    private static object RawItem(object owner, int index) => owner is FrozenList frozen
        ? frozen[index] : (owner as List<object> ?? throw new Exception("Expected List"))[index];
    // Tiny records stay linear until repeated lookup amortizes an index.
}
}

// --- Source: Rational.cs ---
namespace DuelView.Compiled
{


// Exact arithmetic is a value ABI, independent of cognitive or plot semantics.
[JsonConverter(typeof(RationalJsonConverter))]
public sealed record Rational
{
    private static readonly Rational Zero = new(BigInteger.Zero, BigInteger.One);
    private static readonly Rational One = new(BigInteger.One, BigInteger.One);
    private static readonly Rational MinusOne = new(BigInteger.MinusOne, BigInteger.One);
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }
    public Rational(BigInteger n, BigInteger d)
    {
        if (d.IsZero) throw new DivideByZeroException();
        if (d.Sign < 0) { n = -n; d = -d; }
        if (d.IsOne) { Numerator = n; Denominator = BigInteger.One; return; }
        if (n.IsZero) { Numerator = BigInteger.Zero; Denominator = BigInteger.One; return; }
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d);
        Numerator = n / gcd; Denominator = d / gcd;
    }
    public static Rational From(object value)
    {
        if (value is Rational q) return q;
        string text;
        if (value is double d)
        {
            d = R.Num(d);
            if (d == 0) return Zero;
            if (d == 1) return One;
            if (d == -1) return MinusOne;
            // Within the safe-integer range, the round-trip decimal spelling
            // denotes this exact integer. Larger values and fractions must keep
            // the original decimal conversion, not their binary significand.
            if (d >= -9007199254740991d && d <= 9007199254740991d && d == Math.Truncate(d))
                return new Rational(new BigInteger((long)d), BigInteger.One);
            text = d.ToString("R", CultureInfo.InvariantCulture);
        }
        else text = R.Text(value);
        var slash = text.IndexOf('/');
        if (slash >= 0) return new(BigInteger.Parse(text[..slash], CultureInfo.InvariantCulture), BigInteger.Parse(text[(slash+1)..], CultureInfo.InvariantCulture));
        var exponent = text.IndexOfAny(['e', 'E']);
        int power = 0;
        if (exponent >= 0) { power = int.Parse(text[(exponent+1)..], CultureInfo.InvariantCulture); text = text[..exponent]; }
        var point = text.IndexOf('.');
        if (point >= 0) { power -= text.Length - point - 1; text = text.Remove(point, 1); }
        var n = BigInteger.Parse(text, CultureInfo.InvariantCulture);
        return power >= 0 ? new(n * BigInteger.Pow(10, power), 1) : new(n, BigInteger.Pow(10, -power));
    }
    public Rational Add(Rational b)
    {
        if (b.Numerator.IsZero) return this;
        if (Numerator.IsZero) return b;
        return Denominator == b.Denominator
            ? new Rational(Numerator + b.Numerator, Denominator)
            : new Rational(Numerator*b.Denominator+b.Numerator*Denominator, Denominator*b.Denominator);
    }
    public Rational Subtract(Rational b)
    {
        if (b.Numerator.IsZero) return this;
        return Denominator == b.Denominator
            ? new Rational(Numerator - b.Numerator, Denominator)
            : new Rational(Numerator*b.Denominator-b.Numerator*Denominator, Denominator*b.Denominator);
    }
    public Rational Multiply(Rational b)
    {
        if (Numerator.IsZero || b.Numerator.IsZero) return Zero;
        if (b.Numerator.IsOne && b.Denominator.IsOne) return this;
        if (Numerator.IsOne && Denominator.IsOne) return b;
        return new Rational(Numerator*b.Numerator, Denominator*b.Denominator);
    }
    public Rational Divide(Rational b)
    {
        if (b.Numerator.IsZero) throw new DivideByZeroException();
        if (Numerator.IsZero) return Zero;
        if (b.Numerator.IsOne && b.Denominator.IsOne) return this;
        return new Rational(Numerator*b.Denominator, Denominator*b.Numerator);
    }
    public int CompareTo(Rational b) => Denominator == b.Denominator
        ? Numerator.CompareTo(b.Numerator) : (Numerator*b.Denominator).CompareTo(b.Numerator*Denominator);
    public override string ToString() => Denominator.IsOne ? Numerator.ToString(CultureInfo.InvariantCulture) : Numerator.ToString(CultureInfo.InvariantCulture)+"/"+Denominator.ToString(CultureInfo.InvariantCulture);
}
public sealed class RationalJsonConverter : JsonConverter<Rational>
{
    public override Rational Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => Rational.From(reader.GetString()!);
    public override void Write(Utf8JsonWriter writer, Rational value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
}

// --- Source: Models.cs ---
namespace DuelView.Core
{


/// <summary>Planning / Models: native Core mechanisms.</summary>
public static partial class DVKernel
{

        // --- Kernel Decoupling Delegates for Autonomy & Simulation ---
        public static Func<object, object>? F_dvs_schedule_Handler { get; set; }
        public static object F_dvs_schedule(object v_state) => F_dvs_schedule_Handler != null ? F_dvs_schedule_Handler(v_state) : v_state;

        public static Func<object, object>? F_dvs_prepare_actor_Handler { get; set; }
        public static object F_dvs_prepare_actor(object v_state) => F_dvs_prepare_actor_Handler != null ? F_dvs_prepare_actor_Handler(v_state) : v_state;

        public static Func<object, object>? F_dvs_interrupt_waits_Handler { get; set; }
        public static object F_dvs_interrupt_waits(object v_state) => F_dvs_interrupt_waits_Handler != null ? F_dvs_interrupt_waits_Handler(v_state) : v_state;

        public static Func<object, object, object>? F_dvs_dispatch_due_Handler { get; set; }
        public static object F_dvs_dispatch_due(object v_state, object v_index) => F_dvs_dispatch_due_Handler != null ? F_dvs_dispatch_due_Handler(v_state, v_index) : v_state;

        public static Func<object, object, object>? F_dvs_working_explanation_Handler { get; set; }
        public static object F_dvs_working_explanation(object v_actor, object v_hypothesis) => F_dvs_working_explanation_Handler != null ? F_dvs_working_explanation_Handler(v_actor, v_hypothesis) : false;

        public static Func<object, object, object>? F_dvs_idle_guard_matches_Handler { get; set; }
        public static object F_dvs_idle_guard_matches(object v_guard, object v_changes) => F_dvs_idle_guard_matches_Handler != null ? F_dvs_idle_guard_matches_Handler(v_guard, v_changes) : false;

        public static Func<object, object, object>? F_dvs_idle_inputs_changed_Handler { get; set; }
        public static object F_dvs_idle_inputs_changed(object v_inputs, object v_changes) => F_dvs_idle_inputs_changed_Handler != null ? F_dvs_idle_inputs_changed_Handler(v_inputs, v_changes) : false;

        public static Func<object, object, object, object>? F_dvs_simulation_pulse_Handler { get; set; }
        public static object F_dvs_simulation_pulse(object v_state, object v_seconds, object v_visible_rooms) => F_dvs_simulation_pulse_Handler != null ? F_dvs_simulation_pulse_Handler(v_state, v_seconds, v_visible_rooms) : v_state;

        public static Func<object, object, object>? F_dvs_session_complete_Handler { get; set; }
        public static object F_dvs_session_complete(object v_state, object v_receipt) => F_dvs_session_complete_Handler != null ? F_dvs_session_complete_Handler(v_state, v_receipt) : v_state;

        public static Func<object, object, object>? F_dvs_set_player_Handler { get; set; }
        public static object F_dvs_set_player(object v_state, object v_player) => F_dvs_set_player_Handler != null ? F_dvs_set_player_Handler(v_state, v_player) : v_state;

        public static Func<object, object, object>? F_dvs_select_player_Handler { get; set; }
        public static object F_dvs_select_player(object v_state, object v_player) => F_dvs_select_player_Handler != null ? F_dvs_select_player_Handler(v_state, v_player) : v_state;

        public static Func<object, object, object>? F_dvs_navigation_task_completed_Handler { get; set; }
        public static object F_dvs_navigation_task_completed(object v_state, object v_actor) => F_dvs_navigation_task_completed_Handler != null ? F_dvs_navigation_task_completed_Handler(v_state, v_actor) : false;

        public static Func<object, double, string, string, object>? F_dvs_advance_cognitive_time_Handler { get; set; }
        public static object F_dvs_advance_cognitive_time(object state, double minutes, string actor, string attemptId) =>
            F_dvs_advance_cognitive_time_Handler != null ? F_dvs_advance_cognitive_time_Handler(state, minutes, actor, attemptId) : state;

        public static Func<object, object, object>? F_dvs_advance_fixed_step_Handler { get; set; }
        public static object F_dvs_advance_fixed_step(object v_state, object v_step) => F_dvs_advance_fixed_step_Handler != null ? F_dvs_advance_fixed_step_Handler(v_state, v_step) : v_state;

        public static Func<object, object, object, object, object, object>? F_dv_adopt_Handler { get; set; }
        public static object F_dv_adopt(object v_world, object v_law, object v_mode, object v_confidence, object v_basis) => F_dv_adopt_Handler != null ? F_dv_adopt_Handler(v_world, v_law, v_mode, v_confidence, v_basis) : v_world;

        public static Func<object, object, object, object, object, object, object>? F_dv_adopt_into_Handler { get; set; }
        public static object F_dv_adopt_into(object v_domain, object v_owned_adoptions, object v_law, object v_mode, object v_confidence, object v_basis) =>
            F_dv_adopt_into_Handler != null ? F_dv_adopt_into_Handler(v_domain, v_owned_adoptions, v_law, v_mode, v_confidence, v_basis) : v_domain;

        public static Func<object, object, object>? F_dv_finish_owned_adoptions_Handler { get; set; }
        public static object F_dv_finish_owned_adoptions(object v_world, object v_owned_adoptions) =>
            F_dv_finish_owned_adoptions_Handler != null ? F_dv_finish_owned_adoptions_Handler(v_world, v_owned_adoptions) : v_world;

        public static Func<object, object, object, object>? F_dvp_owned_decision_ready_Handler { get; set; }
        public static object F_dvp_owned_decision_ready(object v_actor, object v_action, object v_mind) => F_dvp_owned_decision_ready_Handler != null ? F_dvp_owned_decision_ready_Handler(v_actor, v_action, v_mind) : false;


        

        
    public static object F_dvs_sim_rule(object v_predicted, object v_covered, object v_step, object v_mind, object v_confidence, object v_start, object v_i, object v_event_count, object v_selected_index)
    {
        object v_name__221893 = F_dv_get(v_step, "law");
        object v_at__221917 = F_dv_get(v_step, "at");
        object v_adoptions__221938 = F_dv_get(v_mind, "adoptions");
        object v_complete__221973 = true;
        object v_failures__221997 = new System.Collections.Generic.List<object>(0) { };
        object v_trace__222019 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(F_dv_has_key(v_adoptions__221938, v_name__221893)))
        {
            object v_adoption__222078 = F_dv_get(v_adoptions__221938, v_name__221893);
            if (R.Bool(F_dv_and(F_dv_get(v_adoption__222078, "active"), R.eq(F_dv_get(v_adoption__222078, "mode"), "predict"))))
            {
                object v_law__222195 = F_dv_get(v_adoption__222078, "law");
                object v_premise__222230 = F_dv_truth(v_predicted, F_dv_get(F_dv_get(v_adoption__222078, "law"), "premise"));
                object v_inputs__222298 = F_dvs_sim_inputs(v_predicted, F_dv_get(F_dv_get(v_adoption__222078, "law"), "premise"));
                object v_local_confidence__222371 = F_dv_min(F_dv_get(v_adoption__222078, "confidence"), F_dv_min(v_confidence, F_dv_premise_confidence(v_predicted, F_dv_get(F_dv_get(v_adoption__222078, "law"), "premise"))));
                object v_result__222510 = false;
                object v_withheld__222541 = new System.Collections.Generic.List<object>(0) { };
                v_predicted = F_dv_with(v_predicted, "adoptions", F_dv_with(v_adoptions__221938, v_name__221893, F_dv_with(v_adoption__222078, "confidence", v_local_confidence__222371)));
                object v_event_id__222704 = R.cat(R.cat(R.cat("hypothetical-", R.show(v_event_count)), R.cat("-", R.show(v_i))), R.cat("-", R.show(v_selected_index)));
                v_result__222510 = F_dv_predict(F_dv_with(v_predicted, "domain", "mind"), v_name__221893, v_event_id__222704, R.add(v_start, v_at__221917), F_dv_get(v_predicted, "revision"));
                if (R.Bool(R.eq(v_premise__222230, "unknown")))
                {
                    object v_uncertain__223019 = F_dvs_sim_withhold(v_predicted, F_dv_get(v_law__222195, "effects"));
                    v_predicted = F_dv_get(v_uncertain__223019, "world");
                    v_withheld__222541 = F_dv_get(v_uncertain__223019, "refs");
                }
                object v_local_1__223174 = false;
                if (R.Bool(R.eq(v_premise__222230, "false")))
                {
                    if (R.Bool(F_dv_get(v_step, "optional")))
                    {
                        v_local_1__223174 = "condition-false-not-applicable";
                    }
                    else
                    {
                        v_local_1__223174 = "required-premise-false";
                    }
                }
                else
                {
                    if (R.Bool(R.eq(v_premise__222230, "unknown")))
                    {
                        v_local_1__223174 = "conditional-output-unknown";
                    }
                    else
                    {
                        v_local_1__223174 = F_dv_get(v_result__222510, "status");
                    }
                }
                _ = R.push(v_trace__222019, F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("step", v_i), F_dv_pair("at", v_at__221917), F_dv_pair("law", v_name__221893), F_dv_pair("premise", v_premise__222230) }, new System.Collections.Generic.List<object>(8) { F_dv_pair("inputs", v_inputs__222298), F_dv_pair("status", F_dv_get(v_result__222510, "status")), F_dv_pair("changes", F_dv_get(v_result__222510, "changes")), F_dv_pair("basis", F_dv_get(v_adoption__222078, "basis")), F_dv_pair("stage", F_dv_get(v_step, "stage")), F_dv_pair("optional", F_dv_get(v_step, "optional")), F_dv_pair("withheld", v_withheld__222541), F_dv_pair("reason", v_local_1__223174) }));
                if (R.Bool(R.eq(F_dv_get(v_result__222510, "status"), "applied")))
                {
                    object v_changes__223935 = F_dv_get(v_result__222510, "changes");
                    object v_j__223980 = R.number("0");
                    v_predicted = F_dv_get(v_result__222510, "world");
                    while (R.Bool(R.lt(v_j__223980, R.count(v_changes__223935))))
                    {
                        if (R.Bool(F_dv_has(v_covered, F_dv_get(R.at(v_changes__223935, v_j__223980), "ref"))))
                        {
                            _ = false;
                        }
                        else
                        {
                            _ = R.push(v_covered, F_dv_get(R.at(v_changes__223935, v_j__223980), "ref"));
                        }
                        v_j__223980 = R.add(v_j__223980, R.number("1"));
                    }
                }
                else
                {
                    if (R.Bool(F_dv_and(R.eq(v_premise__222230, "false"), F_dv_get(v_step, "optional"))))
                    {
                        _ = false;
                    }
                    else
                    {
                        v_complete__221973 = false;
                        _ = R.push(v_failures__221997, new System.Collections.Generic.List<object>(3) { F_dv_pair("law", v_name__221893), F_dv_pair("reason", F_dv_get(v_result__222510, "status")), F_dv_pair("premise", v_premise__222230) });
                    }
                }
            }
            else
            {
                v_complete__221973 = false;
                _ = R.push(v_failures__221997, new System.Collections.Generic.List<object>(2) { F_dv_pair("law", v_name__221893), F_dv_pair("reason", "not-adopted-predict") });
            }
        }
        else
        {
            v_complete__221973 = false;
            _ = R.push(v_failures__221997, new System.Collections.Generic.List<object>(2) { F_dv_pair("law", v_name__221893), F_dv_pair("reason", "unknown-law") });
        }
        return new System.Collections.Generic.List<object>(5) { F_dv_pair("world", v_predicted), F_dv_pair("coverage", v_covered), F_dv_pair("complete", v_complete__221973), F_dv_pair("failures", v_failures__221997), F_dv_pair("trace", v_trace__222019) };
    }

    public static object F_dvs_sim_equal_guard(object v_proposition, object v_ref)
    {
        if (R.Bool(R.eq(F_dv_get(v_proposition, "op"), "equals")))
        {
            if (R.Bool(F_dv_same(F_dv_get(v_proposition, "ref"), v_ref)))
            {
                return new System.Collections.Generic.List<object>(2) { F_dv_pair("found", true), F_dv_pair("value", F_dv_get(v_proposition, "value")) };
            }
        }
        if (R.Bool(R.eq(F_dv_get(v_proposition, "op"), "all")))
        {
            object v_i__225106 = R.number("0");
            while (R.Bool(R.lt(v_i__225106, R.count(F_dv_get(v_proposition, "items")))))
            {
                object v_result__225177 = F_dvs_sim_equal_guard(R.at(F_dv_get(v_proposition, "items"), v_i__225106), v_ref);
                if (R.Bool(F_dv_get(v_result__225177, "found")))
                {
                    return v_result__225177;
                }
                v_i__225106 = R.add(v_i__225106, R.number("1"));
            }
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("found", false), F_dv_pair("value", false) };
    }

    public static object F_dvs_sim_dispatch(object v_mind, object v_predicted, object v_step)
    {
        object v_table__225411 = F_dv_get(v_step, "dispatch");
        object v_selector__225441 = F_dv_read(v_predicted, F_dv_get(v_table__225411, "selector"));
        object v_indexed__225495 = F_dv_uncontested(v_selector__225441);
        object v_entries__225538 = new System.Collections.Generic.List<object>(0) { };
        object v_i__225559 = R.number("0");
        while (R.Bool(R.lt(v_i__225559, R.count(F_dv_get(v_table__225411, "laws")))))
        {
            object v_name__225615 = R.at(F_dv_get(v_table__225411, "laws"), v_i__225559);
            object v_adoption__225648 = F_dv_find(F_dv_get(v_mind, "adoptions"), v_name__225615, false);
            object v_guard__225708 = new System.Collections.Generic.List<object>(2) { F_dv_pair("found", false), F_dv_pair("value", false) };
            if (R.Bool(R.eq(v_adoption__225648, false)))
            {
                v_indexed__225495 = false;
            }
            else
            {
                if (R.Bool((R.Bool(R.not(F_dv_get(v_adoption__225648, "active"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_adoption__225648, "mode"), "predict"))))))
                {
                    v_indexed__225495 = false;
                }
                v_guard__225708 = F_dvs_sim_equal_guard(F_dv_get(F_dv_get(v_adoption__225648, "law"), "premise"), F_dv_get(v_table__225411, "selector"));
                if (R.Bool(R.not(F_dv_get(v_guard__225708, "found"))))
                {
                    v_indexed__225495 = false;
                }
                object v_j__226083 = R.number("0");
                while (R.Bool(R.lt(v_j__226083, R.count(F_dv_get(F_dv_get(v_adoption__225648, "law"), "effects")))))
                {
                    if (R.Bool(F_dv_same(F_dv_get(R.at(F_dv_get(F_dv_get(v_adoption__225648, "law"), "effects"), v_j__226083), "ref"), F_dv_get(v_table__225411, "selector"))))
                    {
                        v_indexed__225495 = false;
                    }
                    v_j__226083 = R.add(v_j__226083, R.number("1"));
                }
            }
            _ = R.push(v_entries__225538, new System.Collections.Generic.List<object>(2) { F_dv_pair("law", v_name__225615), F_dv_pair("guard", v_guard__225708) });
            v_i__225559 = R.add(v_i__225559, R.number("1"));
        }
        object v_selected__226362 = new System.Collections.Generic.List<object>(0) { };
        object v_excluded__226384 = new System.Collections.Generic.List<object>(0) { };
        v_i__225559 = R.number("0");
        while (R.Bool(R.lt(v_i__225559, R.count(v_entries__225538))))
        {
            object v_entry__226455 = R.at(v_entries__225538, v_i__225559);
            if (R.Bool((R.Bool(v_indexed__225495) ? (object)(R.not(F_dv_same(F_dv_get(F_dv_get(v_entry__226455, "guard"), "value"), F_dv_get(v_selector__225441, "value")))) : (object)(false))))
            {
                _ = R.push(v_excluded__226384, v_entry__226455);
            }
            else
            {
                _ = R.push(v_selected__226362, new System.Collections.Generic.List<object>(4) { F_dv_pair("law", F_dv_get(v_entry__226455, "law")), F_dv_pair("at", F_dv_get(v_step, "at")), F_dv_pair("stage", F_dv_get(v_step, "stage")), F_dv_pair("optional", F_dv_get(v_step, "optional")) });
            }
            v_i__225559 = R.add(v_i__225559, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("steps", v_selected__226362), F_dv_pair("indexed", v_indexed__225495), F_dv_pair("excluded", v_excluded__226384), F_dv_pair("inputs", new System.Collections.Generic.List<object>(1) { new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", F_dv_get(v_table__225411, "selector")), F_dv_pair("cell", v_selector__225441) } }) };
    }

    public static object F_dvs_slice_intersects(object v_left, object v_right)
    {
        object v_i__227124 = R.number("0");
        while (R.Bool(R.lt(v_i__227124, R.count(v_left))))
        {
            if (R.Bool(F_dv_has(v_right, R.at(v_left, v_i__227124))))
            {
                return true;
            }
            v_i__227124 = R.add(v_i__227124, R.number("1"));
        }
        return false;
    }

    public static object F_dvs_slice_law_io(object v_mind, object v_name)
    {
        object v_adoption__227298 = F_dv_find(F_dv_get(v_mind, "adoptions"), v_name, false);
        if (R.Bool(R.eq(v_adoption__227298, false)))
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("known", false), F_dv_pair("reads", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("writes", new System.Collections.Generic.List<object>(0) { }) };
        }
        object v_law__227441 = F_dv_get(v_adoption__227298, "law");
        object v_reads__227468 = F_dvs_sim_refs(F_dv_get(v_law__227441, "premise"));
        object v_writes__227510 = new System.Collections.Generic.List<object>(0) { };
        object v_i__227530 = R.number("0");
        while (R.Bool(R.lt(v_i__227530, R.count(F_dv_get(v_law__227441, "effects")))))
        {
            object v_effect__227587 = R.at(F_dv_get(v_law__227441, "effects"), v_i__227530);
            v_writes__227510 = F_dv_explanation_merge(v_writes__227510, new System.Collections.Generic.List<object>(1) { F_dv_get(v_effect__227587, "ref") });
            if (R.Bool(R.eq(F_dv_get(v_effect__227587, "op"), "increment")))
            {
                v_reads__227468 = F_dv_explanation_merge(v_reads__227468, new System.Collections.Generic.List<object>(1) { F_dv_get(v_effect__227587, "ref") });
            }
            v_i__227530 = R.add(v_i__227530, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("known", (R.Bool(F_dv_get(v_adoption__227298, "active")) ? (object)(R.eq(F_dv_get(v_adoption__227298, "mode"), "predict")) : (object)(false))), F_dv_pair("reads", v_reads__227468), F_dv_pair("writes", v_writes__227510) };
    }

    public static object F_dvs_slice_step_io(object v_actor, object v_step)
    {
        if (R.Bool(F_dv_has_key(v_step, "response")))
        {
            object v_model__227978 = F_dv_find(F_dv_find(v_actor, "response_models", new System.Collections.Generic.List<object>(0) { }), F_dv_get(F_dv_get(v_step, "response"), "model"), false);
            if (R.Bool(R.eq(v_model__227978, false)))
            {
                return new System.Collections.Generic.List<object>(3) { F_dv_pair("known", false), F_dv_pair("reads", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("writes", new System.Collections.Generic.List<object>(0) { }) };
            }
            object v_response_reads__228165 = F_dvs_sim_refs(F_dv_find(v_step, "available_when", F_dv_true()));
            object v_response_writes__228251 = new System.Collections.Generic.List<object>(0) { };
            object v_context__228284 = F_dv_find(v_model__227978, "context", new System.Collections.Generic.List<object>(0) { });
            object v_k__228336 = R.number("0");
            while (R.Bool(R.lt(v_k__228336, R.count(v_context__228284))))
            {
                v_response_reads__228165 = F_dv_explanation_merge(v_response_reads__228165, new System.Collections.Generic.List<object>(1) { F_dv_get(R.at(v_context__228284, v_k__228336), "from") });
                v_k__228336 = R.add(v_k__228336, R.number("1"));
            }
            if (R.Bool(F_dv_has_key(v_model__227978, "choice_ref")))
            {
                v_response_writes__228251 = new System.Collections.Generic.List<object>(1) { F_dv_get(v_model__227978, "choice_ref") };
            }
            v_k__228336 = R.number("0");
            object v_response_known__228607 = true;
            while (R.Bool(R.lt(v_k__228336, R.count(F_dv_get(F_dv_get(v_step, "response"), "options")))))
            {
                object v_option__228698 = F_dv_find(F_dv_get(v_model__227978, "options"), R.at(F_dv_get(F_dv_get(v_step, "response"), "options"), v_k__228336), new System.Collections.Generic.List<object>(0) { });
                object v_bridge__228776 = F_dvs_slice_law_io(F_dv_get(v_actor, "mind"), F_dv_find(v_option__228698, "bridge_law", ""));
                v_response_known__228607 = (R.Bool(v_response_known__228607) ? (object)(F_dv_get(v_bridge__228776, "known")) : (object)(false));
                v_response_reads__228165 = F_dv_explanation_merge(v_response_reads__228165, F_dv_get(v_bridge__228776, "reads"));
                v_response_writes__228251 = F_dv_explanation_merge(v_response_writes__228251, F_dv_get(v_bridge__228776, "writes"));
                v_k__228336 = R.add(v_k__228336, R.number("1"));
            }
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("known", v_response_known__228607), F_dv_pair("reads", v_response_reads__228165), F_dv_pair("writes", v_response_writes__228251) };
        }
        object v_names__229196 = new System.Collections.Generic.List<object>(1) { F_dv_get(v_step, "law") };
        object v_reads__229223 = new System.Collections.Generic.List<object>(0) { };
        object v_writes__229242 = new System.Collections.Generic.List<object>(0) { };
        object v_known__229262 = true;
        if (R.Bool(F_dv_has_key(v_step, "dispatch")))
        {
            v_names__229196 = F_dv_get(F_dv_get(v_step, "dispatch"), "laws");
            v_reads__229223 = new System.Collections.Generic.List<object>(1) { F_dv_get(F_dv_get(v_step, "dispatch"), "selector") };
        }
        object v_i__229396 = R.number("0");
        while (R.Bool(R.lt(v_i__229396, R.count(v_names__229196))))
        {
            object v_io__229447 = F_dvs_slice_law_io(F_dv_get(v_actor, "mind"), R.at(v_names__229196, v_i__229396));
            v_known__229262 = (R.Bool(v_known__229262) ? (object)(F_dv_get(v_io__229447, "known")) : (object)(false));
            v_reads__229223 = F_dv_explanation_merge(v_reads__229223, F_dv_get(v_io__229447, "reads"));
            v_writes__229242 = F_dv_explanation_merge(v_writes__229242, F_dv_get(v_io__229447, "writes"));
            v_i__229396 = R.add(v_i__229396, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("known", v_known__229262), F_dv_pair("reads", v_reads__229223), F_dv_pair("writes", v_writes__229242) };
    }

    public static object F_dvs_slice_feature(object v_hypothesis, object v_feature, object v_plan, object v_plan_io)
    {
        object v_needed__229784 = F_dvs_sim_refs(F_dvs_sim_feature(v_feature));
        object v_selected__229840 = new System.Collections.Generic.List<object>(0) { };
        object v_keep_prefix__229862 = false;
        object v_i__229890 = R.sub(R.count(v_plan), R.number("1"));
        while (R.Bool(R.not(R.lt(v_i__229890, R.number("0")))))
        {
            object v_step__229945 = R.at(v_plan, v_i__229890);
            if (R.Bool((R.Bool(R.not(F_dv_has_key(v_feature, "at"))) ? (object)(true) : (object)(R.not(R.lt(F_dv_get(v_feature, "at"), F_dv_get(v_step__229945, "at")))))))
            {
                object v_io__230045 = R.at(v_plan_io, v_i__229890);
                if (R.Bool((R.Bool((R.Bool(v_keep_prefix__229862) ? (object)(true) : (object)(R.not(F_dv_get(v_io__230045, "known"))))) ? (object)(true) : (object)(F_dvs_slice_intersects(F_dv_get(v_io__230045, "writes"), v_needed__229784)))))
                {
                    v_selected__229840 = F_dv_explanation_merge(v_selected__229840, new System.Collections.Generic.List<object>(1) { v_i__229890 });
                    v_needed__229784 = F_dv_explanation_merge(v_needed__229784, F_dv_get(v_io__230045, "reads"));
                    if (R.Bool(R.not(F_dv_get(v_io__230045, "known"))))
                    {
                        v_keep_prefix__229862 = true;
                    }
                }
            }
            v_i__229890 = R.sub(v_i__229890, R.number("1"));
        }
        object v_timeline__230377 = new System.Collections.Generic.List<object>(0) { };
        v_i__229890 = R.number("0");
        while (R.Bool(R.lt(v_i__229890, R.count(v_plan))))
        {
            if (R.Bool(F_dv_has(v_selected__229840, v_i__229890)))
            {
                _ = R.push(v_timeline__230377, R.at(v_plan, v_i__229890));
            }
            v_i__229890 = R.add(v_i__229890, R.number("1"));
        }
        object v_assumptions__230530 = new System.Collections.Generic.List<object>(0) { };
        object v_original__230555 = F_dv_find(v_hypothesis, "assumptions", new System.Collections.Generic.List<object>(0) { });
        v_i__229890 = R.number("0");
        while (R.Bool(R.lt(v_i__229890, R.count(v_original__230555))))
        {
            if (R.Bool((R.Bool(v_keep_prefix__229862) ? (object)(true) : (object)(F_dvs_slice_intersects(F_dvs_sim_refs(R.at(v_original__230555, v_i__229890)), v_needed__229784)))))
            {
                _ = R.push(v_assumptions__230530, R.at(v_original__230555, v_i__229890));
            }
            v_i__229890 = R.add(v_i__229890, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(6) { F_dv_pair("timeline", v_timeline__230377), F_dv_pair("assumptions", v_assumptions__230530), F_dv_pair("feature", F_dv_without(v_feature, "at")), F_dv_pair("original_feature", v_feature), F_dv_pair("size", R.add(R.count(v_timeline__230377), R.count(v_assumptions__230530))), F_dv_pair("mask_plan", v_plan) };
    }

    public static object F_dvs_model_probe_key(object v_actor, object v_hypothesis, object v_time)
    {
        object v_plan__231286 = F_dvs_sim_plan(v_hypothesis);
        object v_start__231326 = F_dv_find(v_hypothesis, "start_time", v_time);
        object v_mask__231382 = F_dv_find(v_hypothesis, "mask_plan", v_plan__231286);
        object v_initial__231436 = F_dvs_sim_mask_response(v_actor, F_dvs_sim_mask(F_dv_get(v_actor, "mind"), v_mask__231382, v_start__231326), v_mask__231382);
        return new System.Collections.Generic.List<object>(10) { F_dv_get(v_actor, "id"), v_initial__231436, F_dv_find(v_actor, "response_models", new System.Collections.Generic.List<object>(0) { }), F_dv_find(F_dv_get(v_actor, "policy"), "Replay_Depth", R.count(v_plan__231286)), v_plan__231286, F_dv_find(v_hypothesis, "assumptions", new System.Collections.Generic.List<object>(0) { }), F_dv_find(v_hypothesis, "features", new System.Collections.Generic.List<object>(0) { }), F_dv_find(v_hypothesis, "prior", R.number("0.5")), F_dv_get(v_hypothesis, "basis"), v_start__231326 };
    }

    public static object F_dvs_model_probe(object v_actor, object v_hypothesis, object v_events, object v_time, object v_parent)
    {
        if (R.Bool(R.not(F_dv_find(F_dv_get(v_actor, "policy"), "opt_replaycache", true))))
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("result", F_dvs_model_execute(v_actor, v_hypothesis, v_events, v_time, v_parent)), F_dv_pair("cache", new System.Collections.Generic.List<object>(0) { }) };
        }
        object v_key__232016 = F_dvs_model_probe_key(v_actor, v_hypothesis, v_time);
        object v_cache__232075 = F_dv_find(v_actor, "model_probe_cache", new System.Collections.Generic.List<object>(0) { });
        object v_i__232131 = R.number("0");
        while (R.Bool(R.lt(v_i__232131, R.count(v_cache__232075))))
        {
            object v_saved__232180 = R.at(v_cache__232075, v_i__232131);
            if (R.Bool((R.Bool(F_dv_same(F_dv_get(v_saved__232180, "key"), v_key__232016)) ? (object)(R.not(R.lt(F_dvs_sim_work_budget(v_actor), F_dv_get(F_dv_get(v_saved__232180, "result"), "expanded")))) : (object)(false))))
            {
                object v_result__232305 = F_dv_with_many(F_dv_get(v_saved__232180, "result"), new System.Collections.Generic.List<object>(6) { F_dv_pair("expanded", R.number("0")), F_dv_pair("total_expanded", R.number("0")), F_dv_pair("remaining", F_dv_find(F_dv_get(v_actor, "policy"), "model_budget", R.number("0"))), F_dv_pair("work_remaining", F_dvs_sim_work_budget(v_actor)), F_dv_pair("reused_work", F_dv_get(F_dv_get(v_saved__232180, "result"), "expanded")), F_dv_pair("reused_from_event", F_dv_get(v_saved__232180, "event")) });
                _ = F_dvs_event(v_events, "model_probe_reuse", F_dv_get(v_actor, "id"), v_time, F_dvs_cognitive_parents(v_actor, new System.Collections.Generic.List<object>(2) { v_parent, F_dv_get(v_saved__232180, "event") }), new System.Collections.Generic.List<object>(4) { F_dv_pair("hypothesis", v_hypothesis), F_dv_pair("source_event", F_dv_get(v_saved__232180, "event")), F_dv_pair("result", v_result__232305), F_dv_pair("scope", "identical-completed-causal-probe-not-new-prediction") });
                return new System.Collections.Generic.List<object>(2) { F_dv_pair("result", v_result__232305), F_dv_pair("cache", v_cache__232075) };
            }
            v_i__232131 = R.add(v_i__232131, R.number("1"));
        }
        object v_result__232891 = F_dvs_model_execute(v_actor, v_hypothesis, v_events, v_time, v_parent);
        object v_next__232967 = v_cache__232075;
        if (R.Bool((R.Bool((R.Bool(F_dv_get(v_result__232891, "plan_complete")) ? (object)(R.eq(R.count(F_dv_get(v_result__232891, "failures")), R.number("0"))) : (object)(false))) ? (object)(R.not(R.eq(F_dv_get(v_result__232891, "stop_reason"), "budget-exhausted"))) : (object)(false))))
        {
            v_next__232967 = F_dv_append(v_cache__232075, new System.Collections.Generic.List<object>(3) { F_dv_pair("key", v_key__232016), F_dv_pair("result", v_result__232891), F_dv_pair("event", F_dv_get(R.at(v_events, R.sub(R.count(v_events), R.number("1"))), "id")) });
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("result", v_result__232891), F_dv_pair("cache", v_next__232967) };
    }

    public static object F_dvs_model(object v_actor, object v_hypothesis, object v_events, object v_time, object v_parent)
    {
        object v_features__233304 = F_dv_find(v_hypothesis, "features", new System.Collections.Generic.List<object>(0) { });
        object v_plan__233359 = F_dvs_sim_plan(v_hypothesis);
        object v_plan_io__233399 = new System.Collections.Generic.List<object>(0) { };
        object v_p__233420 = R.number("0");
        while (R.Bool(R.lt(v_p__233420, R.count(v_plan__233359))))
        {
            _ = R.push(v_plan_io__233399, F_dvs_slice_step_io(v_actor, R.at(v_plan__233359, v_p__233420)));
            v_p__233420 = R.add(v_p__233420, R.number("1"));
        }
        object v_smallest__233544 = false;
        object v_i__233569 = R.number("0");
        while (R.Bool(R.lt(v_i__233569, R.count(v_features__233304))))
        {
            object v_feature__233623 = R.at(v_features__233304, v_i__233569);
            object v_observed__233657 = F_dv_find(v_feature__233623, "observation", F_dvs_sim_feature(v_feature__233623));
            if (R.Bool(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), v_observed__233657), "unknown"))))
            {
                object v_slice__233800 = F_dvs_slice_feature(v_hypothesis, v_feature__233623, v_plan__233359, v_plan_io__233399);
                if (R.Bool(R.eq(v_smallest__233544, false)))
                {
                    v_smallest__233544 = v_slice__233800;
                }
                else
                {
                    if (R.Bool(R.lt(F_dv_get(v_slice__233800, "size"), F_dv_get(v_smallest__233544, "size"))))
                    {
                        v_smallest__233544 = v_slice__233800;
                    }
                }
            }
            v_i__233569 = R.add(v_i__233569, R.number("1"));
        }
        object v_full_size__234061 = R.add(R.count(v_plan__233359), R.count(F_dv_find(v_hypothesis, "assumptions", new System.Collections.Generic.List<object>(0) { })));
        if (R.Bool(R.eq(v_smallest__233544, false)))
        {
            return F_dvs_model_execute(v_actor, v_hypothesis, v_events, v_time, v_parent);
        }
        if (R.Bool(R.not(R.lt(R.mul(F_dv_get(v_smallest__233544, "size"), R.number("2")), v_full_size__234061))))
        {
            return F_dvs_model_execute(v_actor, v_hypothesis, v_events, v_time, v_parent);
        }
        object v_trial__234369 = F_dv_with_many(v_hypothesis, new System.Collections.Generic.List<object>(5) { F_dv_pair("timeline", F_dv_get(v_smallest__233544, "timeline")), F_dv_pair("assumptions", F_dv_get(v_smallest__233544, "assumptions")), F_dv_pair("features", new System.Collections.Generic.List<object>(1) { F_dv_get(v_smallest__233544, "feature") }), F_dv_pair("mask_plan", F_dv_get(v_smallest__233544, "mask_plan")), F_dv_pair("evaluation_scope", new System.Collections.Generic.List<object>(2) { F_dv_pair("kind", "causal-refutation"), F_dv_pair("feature", F_dv_get(v_smallest__233544, "original_feature")) }) });
        object v_probed__234634 = F_dvs_model_probe(v_actor, v_trial__234369, v_events, v_time, v_parent);
        object v_probe__234703 = F_dv_get(v_probed__234634, "result");
        if (R.Bool(R.eq(F_dv_get(v_probe__234703, "status"), "inconsistent")))
        {
            return F_dv_with_many(v_probe__234703, new System.Collections.Generic.List<object>(2) { F_dv_pair("evaluation_scope", F_dv_get(v_trial__234369, "evaluation_scope")), F_dv_pair("model_probe_cache", F_dv_get(v_probed__234634, "cache")) });
        }
        object v_next__234890 = F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(2) { F_dv_pair("model_previous_event", F_dv_get(R.at(v_events, R.sub(R.count(v_events), R.number("1"))), "id")), F_dv_pair("policy", F_dv_with_many(F_dv_get(v_actor, "policy"), new System.Collections.Generic.List<object>(2) { F_dv_pair("model_budget", F_dv_get(v_probe__234703, "remaining")), F_dv_pair("model_work_remaining", F_dv_get(v_probe__234703, "work_remaining")) })) });
        object v_result__235103 = F_dvs_model_execute(v_next__234890, v_hypothesis, v_events, v_time, v_parent);
        return F_dv_with_many(v_result__235103, new System.Collections.Generic.List<object>(5) { F_dv_pair("probe_expanded", F_dv_get(v_probe__234703, "expanded")), F_dv_pair("probe_reused_work", F_dv_find(v_probe__234703, "reused_work", R.number("0"))), F_dv_pair("model_probe_cache", F_dv_get(v_probed__234634, "cache")), F_dv_pair("expanded", R.add(F_dv_get(v_probe__234703, "expanded"), F_dv_get(v_result__235103, "expanded"))), F_dv_pair("total_expanded", R.add(F_dv_get(v_probe__234703, "expanded"), F_dv_get(v_result__235103, "total_expanded"))) });
    }
}
}

// --- Source: Exchange.cs ---
namespace DuelView.Compiled
{


// Shared runtime implementation; state remains owned by the execution context.
public static partial class R
{
    public static object fail(object a) => throw new Exception(Text(a));
    public static object json_parse(object text)
    {
        using var document = JsonDocument.Parse(Text(text), new JsonDocumentOptions { MaxDepth = 256 });
        return ReadJson(document.RootElement);
    }
    private static object ReadJson(JsonElement item) => item.ValueKind switch
    {
        JsonValueKind.Array => item.EnumerateArray().Select(ReadJson).ToList(),
        JsonValueKind.String => item.GetString()!,
        JsonValueKind.Number => Num(item.GetDouble()),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw new Exception("DV persistence supports arrays, text, finite numbers and booleans")
    };
    // Serialization preserves the supplied DV value. Evidence projection belongs
    // to explicit DV reporting APIs, never to implicit field-name filtering.
    private static void WriteJsonFast(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case string s:
                writer.WriteStringValue(s);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case Rational q:
                Span<char> qBuf = stackalloc char[128];
                if (q.Denominator.IsOne)
                {
                    if (q.Numerator.TryFormat(qBuf, out int written, provider: CultureInfo.InvariantCulture))
                    {
                        writer.WriteStringValue(qBuf[..written]);
                        break;
                    }
                }
                else
                {
                    if (q.Numerator.TryFormat(qBuf, out int nWritten, provider: CultureInfo.InvariantCulture))
                    {
                        qBuf[nWritten] = '/';
                        if (q.Denominator.TryFormat(qBuf[(nWritten + 1)..], out int dWritten, provider: CultureInfo.InvariantCulture))
                        {
                            writer.WriteStringValue(qBuf[..(nWritten + 1 + dWritten)]);
                            break;
                        }
                    }
                }
                writer.WriteStringValue(q.ToString());
                break;
            case object dvList when TryItems(dvList, out var list):
                writer.WriteStartArray();
                for (var i = 0; i < list.Count; i++)
                {
                    WriteJsonFast(writer, list[i]);
                    if (writer.BytesPending >= 16 * 1024 * 1024) writer.Flush();
                }
                writer.WriteEndArray();
                break;
            default:
                throw new Exception("DV persistence supports arrays, text, finite numbers and booleans");
        }
    }
    private sealed class JsonEncoding(string text)
    {
        internal readonly string Text = text;
    }
    private static ConditionalWeakTable<FrozenList, JsonEncoding> JsonEncodings => Caches.JsonEncodings;
    public static object json_stringify(object value)
    {
        // Only frozen identities are cacheable. Mutable values retain the exact
        // pre-existing serialization path and never reuse a stale encoding.
        if (value is FrozenList frozen)
            return JsonEncodings.GetValue(frozen, key => new JsonEncoding(EncodeJson(key))).Text;
        return EncodeJson(value);
    }
    private static string EncodeJson(object value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { SkipValidation = true }))
        {
            WriteJsonFast(writer, value);
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }
    public static object print(object a)
    {
        lock (OutputLock)
        {
            if (ConsoleOutputContext.Value is { } output)
            {
                output.WriteLine(Text(show(a)));
                output.Flush();
                return a;
            }
            if (binaryRecording || Environment.GetEnvironmentVariable("DV_BINARY_STDOUT") == "1")
            {
                Console.Error.WriteLine(Text(show(a)));
                Console.Error.Flush();
            }
            else
            {
                Console.WriteLine(Text(show(a)));
                Console.Out.Flush();
            }
        }
        return a;
    }
    public static object Block(Func<object> f) => f();
}
}


namespace DuelView.Runtime
{
    public static class Values
    {
        public static readonly JsonElement Empty = JsonSerializer.SerializeToElement(new { });
        public static JsonElement Get(this JsonElement value, string key) =>
            value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var item) ? item : default;
        public static string Text(this JsonElement value, string fallback = "") =>
            value.ValueKind == JsonValueKind.String ? value.GetString()! : fallback;
        public static bool Flag(this JsonElement value, bool fallback = false) => value.ValueKind switch
        { JsonValueKind.True => true, JsonValueKind.False => false, _ => fallback };
        public static IEnumerable<JsonElement> Items(this JsonElement value) =>
            value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : Enumerable.Empty<JsonElement>();
        public static IEnumerable<JsonProperty> Fields(this JsonElement value) =>
            value.ValueKind == JsonValueKind.Object ? value.EnumerateObject() : Enumerable.Empty<JsonProperty>();
        public static double Number(this JsonElement value, double fallback = 0) =>
            value.ValueKind == JsonValueKind.Number ? value.GetDouble() : ExactNumber.TryRead(value, out var number)
                ? (double)number.Numerator / (double)number.Denominator : fallback;
        public static bool Same(JsonElement left, JsonElement right)
        {
            if (ExactNumber.TryRead(left, out var a) && ExactNumber.TryRead(right, out var b)) return a == b;
            if (left.ValueKind != right.ValueKind) return false;
            return left.ValueKind switch
            {
                JsonValueKind.String => left.GetString() == right.GetString(),
                JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined => true,
                JsonValueKind.Array => left.GetArrayLength() == right.GetArrayLength() && left.EnumerateArray().Zip(right.EnumerateArray()).All(p => Same(p.First, p.Second)),
                JsonValueKind.Object => left.EnumerateObject().Count() == right.EnumerateObject().Count() && left.EnumerateObject().All(p => right.TryGetProperty(p.Name, out var v) && Same(p.Value, v)),
                _ => false
            };
        }
    }

    public readonly record struct ExactNumber
    {
        public BigInteger Numerator { get; }
        public BigInteger Denominator { get; }
        public ExactNumber(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero) throw new InvalidDataException("Zero denominator in DV value.");
            var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator) * denominator.Sign;
            Numerator = numerator / gcd; Denominator = denominator / gcd;
        }
        public static bool TryRead(JsonElement value, out ExactNumber number)
        {
            string text;
            if (value.ValueKind == JsonValueKind.Number) text = value.GetRawText();
            else if (value.Get("$dv_rational").ValueKind == JsonValueKind.String) text = value.Get("$dv_rational").Text();
            else { number = default; return false; }
            var slash = text.IndexOf('/');
            if (slash >= 0)
            {
                number = new(BigInteger.Parse(text[..slash], CultureInfo.InvariantCulture), BigInteger.Parse(text[(slash + 1)..], CultureInfo.InvariantCulture));
                return true;
            }
            var exponentIndex = text.IndexOfAny(['e', 'E']);
            var exponent = exponentIndex < 0 ? 0 : int.Parse(text[(exponentIndex + 1)..], CultureInfo.InvariantCulture);
            if (Math.Abs((long)exponent) > 1000) throw new InvalidDataException("DV number exponent exceeds runtime budget.");
            if (exponentIndex >= 0) text = text[..exponentIndex];
            var dot = text.IndexOf('.');
            if (dot >= 0) { exponent -= text.Length - dot - 1; text = text.Remove(dot, 1); }
            var numerator = BigInteger.Parse(text, CultureInfo.InvariantCulture);
            number = exponent >= 0 ? new(numerator * BigInteger.Pow(10, exponent), BigInteger.One)
                : new(numerator, BigInteger.Pow(10, -exponent));
            return true;
        }
        public int CompareTo(ExactNumber other) => (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
        public ExactNumber Add(ExactNumber other) => new(Numerator * other.Denominator + other.Numerator * Denominator, Denominator * other.Denominator);
        public JsonElement Json() => Denominator.IsOne && Numerator >= long.MinValue && Numerator <= long.MaxValue
            ? JsonSerializer.SerializeToElement((long)Numerator)
            : JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["$dv_rational"] = $"{Numerator}/{Denominator}" });
    }

    public readonly record struct FieldKey(string Object, string Facet, string Field)
    {
        public static FieldKey Read(JsonElement reference, string subject)
        {
            var scope = reference.Get("subject").Text();
            if (scope.Length > 0 && scope != subject) throw new InvalidDataException("Cross-subject reference access.");
            if (reference.Get("type").Text() != "ref") throw new InvalidDataException("Expected DV field reference.");
            var key = new FieldKey(reference.Get("object").Text(), reference.Get("facet").Text(), reference.Get("field").Text());
            if (key.Object.Length == 0 || key.Facet.Length == 0 || key.Field.Length == 0)
                throw new InvalidDataException("Incomplete DV field reference.");
            return key;
        }
    }
}



namespace DuelView.Core
{
    public static partial class DVKernel
    {

        public static object F_dv_assign(object v_ref, object v_value)
            => new System.Collections.Generic.List<object>(3) { F_dv_pair("op", "assign"), F_dv_pair("ref", v_ref), F_dv_pair("value", v_value) };

        public static object F_dv_increment(object v_ref, object v_amount)
            => new System.Collections.Generic.List<object>(3) { F_dv_pair("op", "increment"), F_dv_pair("ref", v_ref), F_dv_pair("value", v_amount) };

        public static object F_dv_held_for(object v_ref, object v_value, object v_clock, object v_duration)
            => new System.Collections.Generic.List<object>(5) { F_dv_pair("op", "held_for"), F_dv_pair("ref", v_ref), F_dv_pair("value", v_value), F_dv_pair("clock", v_clock), F_dv_pair("duration", v_duration) };

        public static object F_dv_axiom(object v_name, object v_version, object v_premise, object v_effects)
            => new System.Collections.Generic.List<object>(4) { F_dv_pair("name", v_name), F_dv_pair("version", v_version), F_dv_pair("premise", F_dv_clone(v_premise)), F_dv_pair("effects", F_dv_clone(v_effects)) };

        public static object F_dv_contract(object v_name, object v_version, object v_premise, object v_effects, object v_postcondition)
            => new System.Collections.Generic.List<object>(5) { F_dv_pair("name", v_name), F_dv_pair("version", v_version), F_dv_pair("premise", F_dv_clone(v_premise)), F_dv_pair("effects", F_dv_clone(v_effects)), F_dv_pair("postcondition", F_dv_clone(v_postcondition)) };

        public static object F_dvp_add_ref(object v_refs, object v_ref)
        {
            if (R.Bool(F_dv_has(v_refs, v_ref))) return v_refs;
            return F_dv_append(v_refs, v_ref);
        }

        public static object F_dvp_merge_refs(object v_refs, object v_extra)
        {
            object v_result = v_refs;
            object v_i = R.number("0");
            while (R.Bool(R.lt(v_i, R.count(v_extra))))
            {
                v_result = F_dvp_add_ref(v_result, R.at(v_extra, v_i));
                v_i = R.add(v_i, R.number("1"));
            }
            return v_result;
        }

        public static object F_dv_cognition_hidden(object v_mind, object v_object)
            => F_dv_has(F_dv_find(F_dv_find(v_mind, "CognitionFilter", new System.Collections.Generic.List<object>(0) { }), "hidden_objects", new System.Collections.Generic.List<object>(0) { }), v_object);

        internal static string ExecutionOutcomeCategory(object execution)
        {
            if (R.Text(F_dv_get(execution, "status")) == "applied") return "completed";
            var failureClass = R.Text(F_dv_find(execution, "failure_class", ""));
            var reason = R.Text(F_dv_find(execution, "reason", ""));
            if (failureClass.Length == 0 && reason.Length > 0) failureClass = reason;
            return failureClass switch
            {
                "activity-interrupted" or "interrupted" => "interrupted",
                "native-continuous-source-cancel" or "native-spatial-cancelled" or "cancelled" or "action-cancelled"
                    or "user-cancelled" or "task-cancelled" => "cancelled",
                "native-spatial-completion-invalid" or "native-spatial-execution-fault" or "execution-fault"
                    or "argument-execution-state-changed" or "execution-state-mismatch" => "execution-fault",
                "native-motion-blocked" or "contact-out-of-reach" or "native-spatial-path-blocked" or "path-blocked" => "path-blocked",
                "condition-invalidated" or "actor-left-required-location" or "required-participant-absent"
                    or "required-absence-not-established" or "request-context-invalidated"
                    or "native-spatial-condition-invalidated" or "target-left-location" => "condition-invalidated",
                "character-refused" or "actor-refused" or "request-refused" or "declined" or "peer-refused" => "refused",
                "no-eligible-arguments" or "missing-required-argument" or "invalid-action-argument" => "argument-invalid",
                _ => failureClass.Length > 0 ? failureClass : "failed"
            };
        }

        public static Func<object, object, object, object, object, object, object, object, object, object>? F_dv_accept_explanation_Handler { get; set; }
        public static object F_dv_accept_explanation(object v_mind, object v_model, object v_competitors, object v_slots, object v_sources, object v_policy, object v_budget, object v_time, object v_id) => F_dv_accept_explanation_Handler != null ? F_dv_accept_explanation_Handler(v_mind, v_model, v_competitors, v_slots, v_sources, v_policy, v_budget, v_time, v_id) : false;
        public static Func<object, object, object>? F_dv_action_cost_bounds_Handler { get; set; }
        public static object F_dv_action_cost_bounds(object v_minimum, object v_maximum) => F_dv_action_cost_bounds_Handler != null ? F_dv_action_cost_bounds_Handler(v_minimum, v_maximum) : false;
        public static Func<object, object, object, object, object, object>? F_dv_action_cost_finish_Handler { get; set; }
        public static object F_dv_action_cost_finish(object v_ledger, object v_physical, object v_quote, object v_actual, object v_outcome) => F_dv_action_cost_finish_Handler != null ? F_dv_action_cost_finish_Handler(v_ledger, v_physical, v_quote, v_actual, v_outcome) : false;
        public static Func<object, object, object>? F_dv_action_cost_le_Handler { get; set; }
        public static object F_dv_action_cost_le(object v_a, object v_b) => F_dv_action_cost_le_Handler != null ? F_dv_action_cost_le_Handler(v_a, v_b) : false;
        public static Func<object, object, object>? F_dv_action_cost_ledger_Handler { get; set; }
        public static object F_dv_action_cost_ledger(object v_id, object v_balances) => F_dv_action_cost_ledger_Handler != null ? F_dv_action_cost_ledger_Handler(v_id, v_balances) : false;
        public static Func<object, object, object, object, object, object>? F_dv_action_cost_quote_Handler { get; set; }
        public static object F_dv_action_cost_quote(object v_ledger, object v_world, object v_action, object v_event, object v_bounds) => F_dv_action_cost_quote_Handler != null ? F_dv_action_cost_quote_Handler(v_ledger, v_world, v_action, v_event, v_bounds) : false;
        public static Func<object, object, object, object>? F_dv_action_cost_vector_Handler { get; set; }
        public static object F_dv_action_cost_vector(object v_time, object v_computation, object v_stamina) => F_dv_action_cost_vector_Handler != null ? F_dv_action_cost_vector_Handler(v_time, v_computation, v_stamina) : false;
        public static Func<object, object, object>? F_dv_appraise_request_Handler { get; set; }
        public static object F_dv_appraise_request(object v_mind, object v_request) => F_dv_appraise_request_Handler != null ? F_dv_appraise_request_Handler(v_mind, v_request) : false;
        public static Func<object, object, object, object>? F_dv_binding_Handler { get; set; }
        public static object F_dv_binding(object v_physical, object v_local, object v_subject) => F_dv_binding_Handler != null ? F_dv_binding_Handler(v_physical, v_local, v_subject) : false;
        public static Func<object, object>? F_dv_clamp_signed_Handler { get; set; }
        public static object F_dv_clamp_signed(object v_value) => F_dv_clamp_signed_Handler != null ? F_dv_clamp_signed_Handler(v_value) : false;
        public static Func<object, object, object, object, object, object>? F_dv_criterion_Handler { get; set; }
        public static object F_dv_criterion(object v_id, object v_ref, object v_mode, object v_target, object v_weight) => F_dv_criterion_Handler != null ? F_dv_criterion_Handler(v_id, v_ref, v_mode, v_target, v_weight) : false;
        public static Func<object, object, object>? F_dv_dot_Handler { get; set; }
        public static object F_dv_dot(object v_features, object v_weights) => F_dv_dot_Handler != null ? F_dv_dot_Handler(v_features, v_weights) : false;
        public static Func<object, object, object, object, object, object>? F_dv_execute_Handler { get; set; }
        public static object F_dv_execute(object v_physical, object v_name, object v_event_id, object v_time, object v_expected_revision) => F_dv_execute_Handler != null ? F_dv_execute_Handler(v_physical, v_name, v_event_id, v_time, v_expected_revision) : false;
        public static Func<object, object, object>? F_dv_explanation_merge_Handler { get; set; }
        public static object F_dv_explanation_merge(object v_a, object v_b) => F_dv_explanation_merge_Handler != null ? F_dv_explanation_merge_Handler(v_a, v_b) : false;
        public static Func<object, object, object, object>? F_dv_explanation_model_complete_Handler { get; set; }
        public static object F_dv_explanation_model_complete(object v_model, object v_status, object v_revision) => F_dv_explanation_model_complete_Handler != null ? F_dv_explanation_model_complete_Handler(v_model, v_status, v_revision) : false;
        public static Func<object, object, object>? F_dv_instance_Handler { get; set; }
        public static object F_dv_instance(object v_id, object v_law) => F_dv_instance_Handler != null ? F_dv_instance_Handler(v_id, v_law) : false;
        public static Func<object, object, object, object>? F_dv_local_Handler { get; set; }
        public static object F_dv_local(object v_bindings, object v_physical, object v_subject) => F_dv_local_Handler != null ? F_dv_local_Handler(v_bindings, v_physical, v_subject) : false;
        public static Func<object, object, object, object, object, object, object, object, object, object>? F_dv_observation_Handler { get; set; }
        public static object F_dv_observation(object v_id, object v_subject, object v_ref, object v_value, object v_source, object v_origin, object v_time, object v_trust, object v_dependency) => F_dv_observation_Handler != null ? F_dv_observation_Handler(v_id, v_subject, v_ref, v_value, v_source, v_origin, v_time, v_trust, v_dependency) : false;
        public static Func<object, object, object>? F_dv_observation_profile_Handler { get; set; }
        public static object F_dv_observation_profile(object v_source_weights, object v_threshold) => F_dv_observation_profile_Handler != null ? F_dv_observation_profile_Handler(v_source_weights, v_threshold) : false;
        public static Func<object, object, object, object, object, object>? F_dv_person_policy_Handler { get; set; }
        public static object F_dv_person_policy(object v_subject, object v_values, object v_effort_cost, object v_uncertainty_cost, object v_expression_policy) => F_dv_person_policy_Handler != null ? F_dv_person_policy_Handler(v_subject, v_values, v_effort_cost, v_uncertainty_cost, v_expression_policy) : false;
        public static Func<object, object, object, object, object, object>? F_dv_predict_Handler { get; set; }
        public static object F_dv_predict(object v_mind, object v_name, object v_event_id, object v_time, object v_expected_revision) => F_dv_predict_Handler != null ? F_dv_predict_Handler(v_mind, v_name, v_event_id, v_time, v_expected_revision) : false;
        public static Func<object, object, object>? F_dv_premise_confidence_Handler { get; set; }
        public static object F_dv_premise_confidence(object v_world, object v_proposition) => F_dv_premise_confidence_Handler != null ? F_dv_premise_confidence_Handler(v_world, v_proposition) : false;
        public static Func<object, object, object, object>? F_dv_request_Handler { get; set; }
        public static object F_dv_request(object v_id, object v_requester, object v_self) => F_dv_request_Handler != null ? F_dv_request_Handler(v_id, v_requester, v_self) : false;
        public static Func<object, object, object, object, object, object>? F_dv_stage_Handler { get; set; }
        public static object F_dv_stage(object v_world, object v_effects, object v_time, object v_basis, object v_confidence) => F_dv_stage_Handler != null ? F_dv_stage_Handler(v_world, v_effects, v_time, v_basis, v_confidence) : false;
        public static Func<object, object, object>? F_dv_suspend_Handler { get; set; }
        public static object F_dv_suspend(object v_world, object v_name) => F_dv_suspend_Handler != null ? F_dv_suspend_Handler(v_world, v_name) : false;
        public static Func<object, object, object, object, object>? F_dv_update_mind_Handler { get; set; }
        public static object F_dv_update_mind(object v_mind, object v_observations, object v_expected_revision, object v_profile) => F_dv_update_mind_Handler != null ? F_dv_update_mind_Handler(v_mind, v_observations, v_expected_revision, v_profile) : false;
        public static Func<object, object, object, object, object>? F_dvp_eligible_Handler { get; set; }
        public static object F_dvp_eligible(object v_actor, object v_action, object v_mind, object v_relevant) => F_dvp_eligible_Handler != null ? F_dvp_eligible_Handler(v_actor, v_action, v_mind, v_relevant) : false;
        public static Func<object, object, object>? F_dvp_find_action_Handler { get; set; }
        public static object F_dvp_find_action(object v_actions, object v_id) => F_dvp_find_action_Handler != null ? F_dvp_find_action_Handler(v_actions, v_id) : false;
        public static Func<object, object>? F_dvp_goal_refs_Handler { get; set; }
        public static object F_dvp_goal_refs(object v_actor) => F_dvp_goal_refs_Handler != null ? F_dvp_goal_refs_Handler(v_actor) : false;
        public static Func<object, object, object, object>? F_dvp_normative_ready_Handler { get; set; }
        public static object F_dvp_normative_ready(object v_actor, object v_action, object v_mind) => F_dvp_normative_ready_Handler != null ? F_dvp_normative_ready_Handler(v_actor, v_action, v_mind) : false;
        public static Func<object, object, object, object>? F_dvp_owned_quantity_Handler { get; set; }
        public static object F_dvp_owned_quantity(object v_actor, object v_mind, object v_declaration) => F_dvp_owned_quantity_Handler != null ? F_dvp_owned_quantity_Handler(v_actor, v_mind, v_declaration) : false;
        public static Func<object, object>? F_dvp_owner_requirement_refs_Handler { get; set; }
        public static object F_dvp_owner_requirement_refs(object v_action) => F_dvp_owner_requirement_refs_Handler != null ? F_dvp_owner_requirement_refs_Handler(v_action) : false;
        public static Func<object, object, object>? F_dvp_personal_impact_Handler { get; set; }
        public static object F_dvp_personal_impact(object v_choice, object v_value) => F_dvp_personal_impact_Handler != null ? F_dvp_personal_impact_Handler(v_choice, v_value) : false;
        public static Func<object, object, object>? F_dvp_personal_ready_Handler { get; set; }
        public static object F_dvp_personal_ready(object v_mind, object v_choice) => F_dvp_personal_ready_Handler != null ? F_dvp_personal_ready_Handler(v_mind, v_choice) : false;
        public static Func<object, object, object, object>? F_dvp_resonance_utility_Handler { get; set; }
        public static object F_dvp_resonance_utility(object v_actor, object v_action, object v_mind) => F_dvp_resonance_utility_Handler != null ? F_dvp_resonance_utility_Handler(v_actor, v_action, v_mind) : false;
        public static Func<object, object, object>? F_dvp_topic_Handler { get; set; }
        public static object F_dvp_topic(object v_actor, object v_item) => F_dvp_topic_Handler != null ? F_dvp_topic_Handler(v_actor, v_item) : false;
        public static Func<object, object>? F_dvs_actor_can_act_Handler { get; set; }
        public static object F_dvs_actor_can_act(object v_actor) => F_dvs_actor_can_act_Handler != null ? F_dvs_actor_can_act_Handler(v_actor) : false;
        public static Func<object, object, object>? F_dvs_autonomy_update_Handler { get; set; }
        public static object F_dvs_autonomy_update(object actor, object time) => F_dvs_autonomy_update_Handler != null ? F_dvs_autonomy_update_Handler(actor, time) : false;
        public static Func<object, object, object>? F_dvs_cached_model_Handler { get; set; }
        public static object F_dvs_cached_model(object v_actor, object v_hypothesis) => F_dvs_cached_model_Handler != null ? F_dvs_cached_model_Handler(v_actor, v_hypothesis) : false;
        public static Func<object, object, object>? F_dvs_conversation_execution_Handler { get; set; }
        public static object F_dvs_conversation_execution(object pending, object state) => F_dvs_conversation_execution_Handler != null ? F_dvs_conversation_execution_Handler(pending, state) : false;
        public static Func<object, object, object>? F_dvs_current_local_contact_Handler { get; set; }
        public static object F_dvs_current_local_contact(object actor, object target) => F_dvs_current_local_contact_Handler != null ? F_dvs_current_local_contact_Handler(actor, target) : false;
        public static Func<object, object>? F_dvs_dirty_refs_Handler { get; set; }
        public static object F_dvs_dirty_refs(object v_condition) => F_dvs_dirty_refs_Handler != null ? F_dvs_dirty_refs_Handler(v_condition) : false;
        public static Func<object, object, object, object, object>? F_dvs_dirty_truth_Handler { get; set; }
        public static object F_dvs_dirty_truth(object v_world, object v_condition, object v_config, object v_name) => F_dvs_dirty_truth_Handler != null ? F_dvs_dirty_truth_Handler(v_world, v_condition, v_config, v_name) : false;
        public static Func<object, object, object>? F_dvs_factor_catalog_complete_Handler { get; set; }
        public static object F_dvs_factor_catalog_complete(object v_domains, object v_hypotheses) => F_dvs_factor_catalog_complete_Handler != null ? F_dvs_factor_catalog_complete_Handler(v_domains, v_hypotheses) : false;
        public static Func<object, object, object, object>? F_dvs_factor_certificate_current_Handler { get; set; }
        public static object F_dvs_factor_certificate_current(object v_actor, object v_certificate, object v_events) => F_dvs_factor_certificate_current_Handler != null ? F_dvs_factor_certificate_current_Handler(v_actor, v_certificate, v_events) : false;
        public static Func<object, object, object>? F_dvs_factor_covers_Handler { get; set; }
        public static object F_dvs_factor_covers(object v_certificate, object v_assignments) => F_dvs_factor_covers_Handler != null ? F_dvs_factor_covers_Handler(v_certificate, v_assignments) : false;
        public static Func<object, object, object, object, object, object, object>? F_dvs_heard_before_selection_Handler { get; set; }
        public static object F_dvs_heard_before_selection(object v_events, object v_actor, object v_action, object v_execution_event, object v_parse_event, object v_source_event) => F_dvs_heard_before_selection_Handler != null ? F_dvs_heard_before_selection_Handler(v_events, v_actor, v_action, v_execution_event, v_parse_event, v_source_event) : false;
        public static Func<object, object, object, object>? F_dvs_initialize_cognition_Handler { get; set; }
        public static object F_dvs_initialize_cognition(object v_actor, object v_seed, object v_ordinal) => F_dvs_initialize_cognition_Handler != null ? F_dvs_initialize_cognition_Handler(v_actor, v_seed, v_ordinal) : false;
        public static Func<object, object, object>? F_dvs_local_candidate_admissible_Handler { get; set; }
        public static object F_dvs_local_candidate_admissible(object v_actor, object v_action) => F_dvs_local_candidate_admissible_Handler != null ? F_dvs_local_candidate_admissible_Handler(v_actor, v_action) : false;
        public static Func<object, object, object>? F_dvs_manual_candidate_available_Handler { get; set; }
        public static object F_dvs_manual_candidate_available(object v_actor, object v_candidate) => F_dvs_manual_candidate_available_Handler != null ? F_dvs_manual_candidate_available_Handler(v_actor, v_candidate) : false;
        public static Func<object, object, object, object, object, object>? F_dvs_model_execute_Handler { get; set; }
        public static object F_dvs_model_execute(object v_actor, object v_hypothesis, object v_events, object v_time, object v_parent) => F_dvs_model_execute_Handler != null ? F_dvs_model_execute_Handler(v_actor, v_hypothesis, v_events, v_time, v_parent) : false;
        public static Func<object, object, object, object, object>? F_dvs_owned_decision_completion_selection_Handler { get; set; }
        public static object F_dvs_owned_decision_completion_selection(object v_actor, object v_action, object v_events, object v_parent) => F_dvs_owned_decision_completion_selection_Handler != null ? F_dvs_owned_decision_completion_selection_Handler(v_actor, v_action, v_events, v_parent) : false;
        public static Func<object, object, object, object>? F_dvs_owned_decision_inputs_Handler { get; set; }
        public static object F_dvs_owned_decision_inputs(object v_actor, object v_decision, object v_mind) => F_dvs_owned_decision_inputs_Handler != null ? F_dvs_owned_decision_inputs_Handler(v_actor, v_decision, v_mind) : false;
        public static Func<object, object>? F_dvs_player_options_Handler { get; set; }
        public static object F_dvs_player_options(object v_actor) => F_dvs_player_options_Handler != null ? F_dvs_player_options_Handler(v_actor) : false;
        public static Func<object>? F_dvs_presentation_timing_Handler { get; set; }
        public static object F_dvs_presentation_timing() => F_dvs_presentation_timing_Handler != null ? F_dvs_presentation_timing_Handler() : false;
        public static Func<object, object>? F_dvs_reason_cell_Handler { get; set; }
        public static object F_dvs_reason_cell(object v_cell) => F_dvs_reason_cell_Handler != null ? F_dvs_reason_cell_Handler(v_cell) : false;
        public static Func<object, object>? F_dvs_reason_changes_Handler { get; set; }
        public static object F_dvs_reason_changes(object v_changes) => F_dvs_reason_changes_Handler != null ? F_dvs_reason_changes_Handler(v_changes) : false;
        public static Func<object, object>? F_dvs_reason_value_Handler { get; set; }
        public static object F_dvs_reason_value(object v_cell) => F_dvs_reason_value_Handler != null ? F_dvs_reason_value_Handler(v_cell) : false;
        public static Func<object, object, object, object, object, object>? F_dvs_receive_public_claim_Handler { get; set; }
        public static object F_dvs_receive_public_claim(object v_listener, object v_speech, object v_speaker, object v_source, object v_parse_event) => F_dvs_receive_public_claim_Handler != null ? F_dvs_receive_public_claim_Handler(v_listener, v_speech, v_speaker, v_source, v_parse_event) : false;
        public static Func<object, object, object>? F_dvs_recorded_event_position_Handler { get; set; }
        public static object F_dvs_recorded_event_position(object v_events, object v_id) => F_dvs_recorded_event_position_Handler != null ? F_dvs_recorded_event_position_Handler(v_events, v_id) : false;
        public static Func<object, object, object>? F_dvs_replay_candidate_available_Handler { get; set; }
        public static object F_dvs_replay_candidate_available(object v_actor, object v_candidate) => F_dvs_replay_candidate_available_Handler != null ? F_dvs_replay_candidate_available_Handler(v_actor, v_candidate) : false;
        public static Func<object, object>? F_dvs_scheduled_count_Handler { get; set; }
        public static object F_dvs_scheduled_count(object v_actors) => F_dvs_scheduled_count_Handler != null ? F_dvs_scheduled_count_Handler(v_actors) : false;
        public static Func<object, object>? F_dvs_sim_feature_Handler { get; set; }
        public static object F_dvs_sim_feature(object v_feature) => F_dvs_sim_feature_Handler != null ? F_dvs_sim_feature_Handler(v_feature) : false;
        public static Func<object, object, object>? F_dvs_sim_inputs_Handler { get; set; }
        public static object F_dvs_sim_inputs(object v_world, object v_proposition) => F_dvs_sim_inputs_Handler != null ? F_dvs_sim_inputs_Handler(v_world, v_proposition) : false;
        public static Func<object, object, object, object>? F_dvs_sim_mask_Handler { get; set; }
        public static object F_dvs_sim_mask(object v_mind, object v_source_plan, object v_start) => F_dvs_sim_mask_Handler != null ? F_dvs_sim_mask_Handler(v_mind, v_source_plan, v_start) : false;
        public static Func<object, object, object, object>? F_dvs_sim_mask_response_Handler { get; set; }
        public static object F_dvs_sim_mask_response(object v_actor, object v_world, object v_plan) => F_dvs_sim_mask_response_Handler != null ? F_dvs_sim_mask_response_Handler(v_actor, v_world, v_plan) : false;
        public static Func<object, object>? F_dvs_sim_plan_Handler { get; set; }
        public static object F_dvs_sim_plan(object v_hypothesis) => F_dvs_sim_plan_Handler != null ? F_dvs_sim_plan_Handler(v_hypothesis) : false;
        public static Func<object, object>? F_dvs_sim_refs_Handler { get; set; }
        public static object F_dvs_sim_refs(object v_proposition) => F_dvs_sim_refs_Handler != null ? F_dvs_sim_refs_Handler(v_proposition) : false;
        public static Func<object, object, object>? F_dvs_sim_withhold_Handler { get; set; }
        public static object F_dvs_sim_withhold(object v_world, object v_effects) => F_dvs_sim_withhold_Handler != null ? F_dvs_sim_withhold_Handler(v_world, v_effects) : false;
        public static Func<object, object>? F_dvs_sim_work_budget_Handler { get; set; }
        public static object F_dvs_sim_work_budget(object v_actor) => F_dvs_sim_work_budget_Handler != null ? F_dvs_sim_work_budget_Handler(v_actor) : false;
        public static Func<object, object, object, object, object>? F_dvs_simulation_environment_Handler { get; set; }
        public static object F_dvs_simulation_environment(object v_world, object v_story, object v_events, object v_parent) => F_dvs_simulation_environment_Handler != null ? F_dvs_simulation_environment_Handler(v_world, v_story, v_events, v_parent) : false;
        public static Func<object, object, object>? F_dvs_simulation_next_time_Handler { get; set; }
        public static object F_dvs_simulation_next_time(object v_world, object v_due) => F_dvs_simulation_next_time_Handler != null ? F_dvs_simulation_next_time_Handler(v_world, v_due) : false;
        public static Func<object, object, object>? F_dvs_simulation_open_Handler { get; set; }
        public static object F_dvs_simulation_open(object v_story, object v_world) => F_dvs_simulation_open_Handler != null ? F_dvs_simulation_open_Handler(v_story, v_world) : false;
        public static Func<object, object, object>? F_dvs_simulation_unmanaged_processes_Handler { get; set; }
        public static object F_dvs_simulation_unmanaged_processes(object v_world, object v_processes) => F_dvs_simulation_unmanaged_processes_Handler != null ? F_dvs_simulation_unmanaged_processes_Handler(v_world, v_processes) : false;
        public static Func<object, object, object, object, object>? F_dvs_reason_Handler { get; set; }
        public static object F_dvs_reason(object v_actor, object v_events, object v_time, object v_parent) => F_dvs_reason_Handler != null ? F_dvs_reason_Handler(v_actor, v_events, v_time, v_parent) : false;
        public static Func<object, object, object, object, object>? F_dvs_current_appraisals_Handler { get; set; }
        public static object F_dvs_current_appraisals(object v_actor, object v_events, object v_time, object v_parent) => F_dvs_current_appraisals_Handler != null ? F_dvs_current_appraisals_Handler(v_actor, v_events, v_time, v_parent) : false;
        public static Func<object, object, object, object, object, object>? F_dv_infer_Handler { get; set; }
        public static object F_dv_infer(object v_mind, object v_name, object v_event_id, object v_time, object v_expected_revision) => F_dv_infer_Handler != null ? F_dv_infer_Handler(v_mind, v_name, v_event_id, v_time, v_expected_revision) : false;
        public static Func<object, object>? F_dvs_autonomy_needs_Handler { get; set; }
        public static object F_dvs_autonomy_needs(object actor) => F_dvs_autonomy_needs_Handler != null ? F_dvs_autonomy_needs_Handler(actor) : false;

        public static Func<object, object, object>? F_dvs_declaration_cache_get_Handler { get; set; }
        public static object F_dvs_declaration_cache_get(object owner, object key) => F_dvs_declaration_cache_get_Handler != null ? F_dvs_declaration_cache_get_Handler(owner, key) : false;

        public static Func<object, object, object, object>? F_dvs_declaration_cache_put_Handler { get; set; }
        public static object F_dvs_declaration_cache_put(object owner, object key, object value) => F_dvs_declaration_cache_put_Handler != null ? F_dvs_declaration_cache_put_Handler(owner, key, value) : value;

        public static Func<object, object, object, object, object, object, object>? F_dvs_autonomy_complete_Handler { get; set; }
        public static object F_dvs_autonomy_complete(object actor, object action, object success, object events, object eventId, object time) => F_dvs_autonomy_complete_Handler != null ? F_dvs_autonomy_complete_Handler(actor, action, success, events, eventId, time) : actor;

        public static Func<object, object, object>? F_dvs_navigation_request_status_Handler { get; set; }
        public static object F_dvs_navigation_request_status(object actor, object task) => F_dvs_navigation_request_status_Handler != null ? F_dvs_navigation_request_status_Handler(actor, task) : false;

        public static Func<object, object, object, object, object>? F_dvp_eligible_base_Handler { get; set; }
        public static object F_dvp_eligible_base(object v_actor, object v_action, object v_mind, object v_relevant) => F_dvp_eligible_base_Handler != null ? F_dvp_eligible_base_Handler(v_actor, v_action, v_mind, v_relevant) : false;

        public static Func<object, object, object, object>? F_dvp_input_signature_Handler { get; set; }
        public static object F_dvp_input_signature(object v_actor, object v_action, object v_refs) => F_dvp_input_signature_Handler != null ? F_dvp_input_signature_Handler(v_actor, v_action, v_refs) : "";


        public static Func<object, object>? F_dvs_simulation_sync_Handler { get; set; }
        public static object F_dvs_simulation_sync(object v_state) => F_dvs_simulation_sync_Handler != null ? F_dvs_simulation_sync_Handler(v_state) : v_state;

        public static Func<object, object, object>? F_dvs_simulation_actor_ready_Handler { get; set; }
        public static object F_dvs_simulation_actor_ready(object v_actor, object v_state) => F_dvs_simulation_actor_ready_Handler != null ? F_dvs_simulation_actor_ready_Handler(v_actor, v_state) : false;

        public static Func<object, object>? ReadGoalRefsHandler { get; set; }
        public static object ReadGoalRefs(object actor) => ReadGoalRefsHandler != null ? ReadGoalRefsHandler(actor) : new List<object>();


        public static Func<object, object, object, object>? F_dvs_erosion_update_Handler { get; set; }
        public static object F_dvs_erosion_update(object v_actor, object v_physical, object v_time) => F_dvs_erosion_update_Handler != null ? F_dvs_erosion_update_Handler(v_actor, v_physical, v_time) : v_actor;

        public static Func<object, object, object, object, object, object, object, object>? F_dvs_notice_peers_Handler { get; set; }
        public static object F_dvs_notice_peers(object v_actor, object v_physical, object v_actors, object v_bindings, object v_events, object v_time, object v_parent) => F_dvs_notice_peers_Handler != null ? F_dvs_notice_peers_Handler(v_actor, v_physical, v_actors, v_bindings, v_events, v_time, v_parent) : v_actor;

        public static Func<object, object, object, object, object, object, object>? F_dvs_notice_visible_objects_Handler { get; set; }
        public static object F_dvs_notice_visible_objects(object v_actor, object v_physical, object v_bindings, object v_events, object v_time, object v_parent) => F_dvs_notice_visible_objects_Handler != null ? F_dvs_notice_visible_objects_Handler(v_actor, v_physical, v_bindings, v_events, v_time, v_parent) : v_actor;

        public static Func<object, object, object>? F_dv_explanation_model_current_Handler { get; set; }
        public static object F_dv_explanation_model_current(object v_mind, object v_model) => F_dv_explanation_model_current_Handler != null ? F_dv_explanation_model_current_Handler(v_mind, v_model) : false;

        public static Func<object, object, object, object, object, object>? F_dvs_current_action_score_Handler { get; set; }
        public static object F_dvs_current_action_score(object v_actor, object v_action, object v_bindings, object v_relevant, object v_actual) => F_dvs_current_action_score_Handler != null ? F_dvs_current_action_score_Handler(v_actor, v_action, v_bindings, v_relevant, v_actual) : false;

        public static Func<object, object>? F_dvs_navigation_task_candidates_Handler { get; set; }
        public static object F_dvs_navigation_task_candidates(object actor) => F_dvs_navigation_task_candidates_Handler != null ? F_dvs_navigation_task_candidates_Handler(actor) : new List<object>();

        public static Func<object, object, object, object>? F_dvs_navigation_task_dispatch_Handler { get; set; }
        public static object F_dvs_navigation_task_dispatch(object actor, object action, object actionEvent) => F_dvs_navigation_task_dispatch_Handler != null ? F_dvs_navigation_task_dispatch_Handler(actor, action, actionEvent) : false;

        public static Func<object, object, object, object, object>? F_dvs_navigation_task_sync_Handler { get; set; }
        public static object F_dvs_navigation_task_sync(object actor, object events, object time, object physical) => F_dvs_navigation_task_sync_Handler != null ? F_dvs_navigation_task_sync_Handler(actor, events, time, physical) : actor;

        public static Func<object, object, object>? F_dvs_navigation_task_diagnostics_Handler { get; set; }
        public static object F_dvs_navigation_task_diagnostics(object actor, object choice) => F_dvs_navigation_task_diagnostics_Handler != null ? F_dvs_navigation_task_diagnostics_Handler(actor, choice) : false;

        public static Func<object, object, object, object, object, object>? F_dvs_navigation_task_commit_Handler { get; set; }
        public static object F_dvs_navigation_task_commit(object actor, object action, object events, object time, object parent) => F_dvs_navigation_task_commit_Handler != null ? F_dvs_navigation_task_commit_Handler(actor, action, events, time, parent) : actor;

        public static Func<object, object, object, object, object, object, object, object>? F_dvs_control_state_Handler { get; set; }
        public static object F_dvs_control_state(object v_state, object v_actors, object v_events, object v_cursor, object v_player, object v_selection, object v_waiting) => F_dvs_control_state_Handler != null ? F_dvs_control_state_Handler(v_state, v_actors, v_events, v_cursor, v_player, v_selection, v_waiting) : v_state;

        public static Func<object, object, object>? F_dvs_player_choice_Handler { get; set; }
        public static object F_dvs_player_choice(object v_actor, object v_id) => F_dvs_player_choice_Handler != null ? F_dvs_player_choice_Handler(v_actor, v_id) : false;

        public static Func<object, object, object, object, object>? F_dvs_reason_run_Handler { get; set; }
        public static object F_dvs_reason_run(object v_actor, object v_events, object v_time, object v_parent) => F_dvs_reason_run_Handler != null ? F_dvs_reason_run_Handler(v_actor, v_events, v_time, v_parent) : v_actor;

        public static Func<object, object>? F_dvs_has_active_communication_goal_Handler { get; set; }
        public static object F_dvs_has_active_communication_goal(object v_actor) => F_dvs_has_active_communication_goal_Handler != null ? F_dvs_has_active_communication_goal_Handler(v_actor) : false;

        public static Func<object, object, object, object, object>? F_dvs_prepare_cognition_Handler { get; set; }
        public static object F_dvs_prepare_cognition(object v_actor, object v_events, object v_time, object v_parent) => F_dvs_prepare_cognition_Handler != null ? F_dvs_prepare_cognition_Handler(v_actor, v_events, v_time, v_parent) : v_actor;


        public static Func<object, object, object>? F_dvs_owned_decision_selection_Handler { get; set; }
        public static object F_dvs_owned_decision_selection(object v_actor, object v_action) => F_dvs_owned_decision_selection_Handler != null ? F_dvs_owned_decision_selection_Handler(v_actor, v_action) : false;

        public static Func<object, object, object, object>? F_dvs_visible_context_Handler { get; set; }
        public static object F_dvs_visible_context(object v_actor, object v_physical, object v_actors) => F_dvs_visible_context_Handler != null ? F_dvs_visible_context_Handler(v_actor, v_physical, v_actors) : new List<object>();

        public static Func<object, object, object>? F_dvs_visible_objects_Handler { get; set; }
        public static object F_dvs_visible_objects(object v_actor, object v_physical) => F_dvs_visible_objects_Handler != null ? F_dvs_visible_objects_Handler(v_actor, v_physical) : new List<object>();

        public static Func<object, object, object>? F_dvs_action_label_Handler { get; set; }
        public static object F_dvs_action_label(object v_actor, object v_action) => F_dvs_action_label_Handler != null ? F_dvs_action_label_Handler(v_actor, v_action) : "";


        public static Func<object, object, object, object, object, object, object>? F_dvs_complete_cognitive_effects_Handler { get; set; }
        public static object F_dvs_complete_cognitive_effects(object v_actor, object v_action, object v_success, object v_events, object v_event, object v_time) => F_dvs_complete_cognitive_effects_Handler != null ? F_dvs_complete_cognitive_effects_Handler(v_actor, v_action, v_success, v_events, v_event, v_time) : v_actor;

        public static Func<object, object, object, object, object, object, object, object, object>? F_dvs_notice_movement_witnesses_Handler { get; set; }
        public static object F_dvs_notice_movement_witnesses(object v_actors, object v_moving_index, object v_before, object v_physical, object v_bindings, object v_events, object v_time, object v_parent) => F_dvs_notice_movement_witnesses_Handler != null ? F_dvs_notice_movement_witnesses_Handler(v_actors, v_moving_index, v_before, v_physical, v_bindings, v_events, v_time, v_parent) : v_actors;

        public static Func<object, object>? F_dvs_observation_profile_Handler { get; set; }
        public static object F_dvs_observation_profile(object v_actor) => F_dvs_observation_profile_Handler != null ? F_dvs_observation_profile_Handler(v_actor) : false;

        public static Func<object, object, object>? F_dvs_observation_source_Handler { get; set; }
        public static object F_dvs_observation_source(object v_observations, object v_action) => F_dvs_observation_source_Handler != null ? F_dvs_observation_source_Handler(v_observations, v_action) : false;

        public static Func<object, object, object, object, object, object, object, object, object, object>? F_dvs_public_grants_Handler { get; set; }
        public static object F_dvs_public_grants(object v_actors, object v_speaker_index, object v_action, object v_physical, object v_bindings, object v_events, object v_time, object v_parent, object v_execution_id) => F_dvs_public_grants_Handler != null ? F_dvs_public_grants_Handler(v_actors, v_speaker_index, v_action, v_physical, v_bindings, v_events, v_time, v_parent, v_execution_id) : false;

        public static Func<object, object, object, object, object, object, object, object>? F_dvs_receipt_clock_observations_Handler { get; set; }
        public static object F_dvs_receipt_clock_observations(object v_story, object v_mind, object v_bindings, object v_observations, object v_action_event, object v_execution_event, object v_time) => F_dvs_receipt_clock_observations_Handler != null ? F_dvs_receipt_clock_observations_Handler(v_story, v_mind, v_bindings, v_observations, v_action_event, v_execution_event, v_time) : new List<object>();

        public static Func<object, object, object, object, object, object, object, object, object, object, object>? F_dvs_visible_action_grants_Handler { get; set; }
        public static object F_dvs_visible_action_grants(object v_actors, object v_speaker_index, object v_action, object v_before, object v_physical, object v_bindings, object v_events, object v_time, object v_parent, object v_execution_id) => F_dvs_visible_action_grants_Handler != null ? F_dvs_visible_action_grants_Handler(v_actors, v_speaker_index, v_action, v_before, v_physical, v_bindings, v_events, v_time, v_parent, v_execution_id) : false;

        public static Func<object, object, object>? F_dv_explanation_model_inputs_Handler { get; set; }
        public static object F_dv_explanation_model_inputs(object v_mind, object v_model) => F_dv_explanation_model_inputs_Handler != null ? F_dv_explanation_model_inputs_Handler(v_mind, v_model) : new List<object>();

        public static Func<object, object, object, object, object>? F_dvs_factor_prune_Handler { get; set; }
        public static object F_dvs_factor_prune(object v_actor, object v_events, object v_time, object v_parent) => F_dvs_factor_prune_Handler != null ? F_dvs_factor_prune_Handler(v_actor, v_events, v_time, v_parent) : v_actor;

        public static Func<object, object, object>? F_dvs_navigation_evidence_pending_Handler { get; set; }
        public static object F_dvs_navigation_evidence_pending(object actor, object goal) => F_dvs_navigation_evidence_pending_Handler != null ? F_dvs_navigation_evidence_pending_Handler(actor, goal) : false;

        public static Func<object, object, object, object>? F_dv_aggregate_Handler { get; set; }
        public static object F_dv_aggregate(object v_claims, object v_profile, object v_time) => F_dv_aggregate_Handler != null ? F_dv_aggregate_Handler(v_claims, v_profile, v_time) : new List<object>();

        public static Func<object, object, object>? F_dv_cognition_observations_Handler { get; set; }
        public static object F_dv_cognition_observations(object v_mind, object v_observations) => F_dv_cognition_observations_Handler != null ? F_dv_cognition_observations_Handler(v_mind, v_observations) : v_mind;

        public static Func<object, object, object, object, object, object, object>? F_dv_perceive_Handler { get; set; }
        public static object F_dv_perceive(object v_physical, object v_mind, object v_bindings, object v_grants, object v_event_id, object v_time) => F_dv_perceive_Handler != null ? F_dv_perceive_Handler(v_physical, v_mind, v_bindings, v_grants, v_event_id, v_time) : new List<object>();

        public static Func<object, object, object, object>? F_dv_project_value_Handler { get; set; }
        public static object F_dv_project_value(object v_value, object v_bindings, object v_subject) => F_dv_project_value_Handler != null ? F_dv_project_value_Handler(v_value, v_bindings, v_subject) : v_value;

        public static Func<object, object, object>? F_dv_replace_claim_Handler { get; set; }
        public static object F_dv_replace_claim(object v_claims, object v_observation) => F_dv_replace_claim_Handler != null ? F_dv_replace_claim_Handler(v_claims, v_observation) : v_claims;

        public static Func<object, object, object>? F_dvs_manual_candidate_Handler { get; set; }
        public static object F_dvs_manual_candidate(object v_actor, object v_id) => F_dvs_manual_candidate_Handler != null ? F_dvs_manual_candidate_Handler(v_actor, v_id) : false;

        public static Func<object, object>? F_dvs_replay_attempt_key_Handler { get; set; }
        public static object F_dvs_replay_attempt_key(object v_action) => F_dvs_replay_attempt_key_Handler != null ? F_dvs_replay_attempt_key_Handler(v_action) : "";

        public static Func<object, object>? F_dvs_replay_has_available_candidate_Handler { get; set; }
        public static object F_dvs_replay_has_available_candidate(object v_actor) => F_dvs_replay_has_available_candidate_Handler != null ? F_dvs_replay_has_available_candidate_Handler(v_actor) : false;

        public static Func<object, object, object, object>? F_dvs_replay_input_signature_Handler { get; set; }
        public static object F_dvs_replay_input_signature(object v_actor, object v_action, object v_refs) => F_dvs_replay_input_signature_Handler != null ? F_dvs_replay_input_signature_Handler(v_actor, v_action, v_refs) : "";

        public static Func<object, object, object>? F_dvs_model_recheck_Handler { get; set; }
        public static object F_dvs_model_recheck(object v_actor, object v_model) => F_dvs_model_recheck_Handler != null ? F_dvs_model_recheck_Handler(v_actor, v_model) : false;

        public static Func<object, object, object, object, object>? F_dvs_model_recheck_eligible_Handler { get; set; }
        public static object F_dvs_model_recheck_eligible(object v_actor, object v_hypothesis, object v_model, object v_time) => F_dvs_model_recheck_eligible_Handler != null ? F_dvs_model_recheck_eligible_Handler(v_actor, v_hypothesis, v_model, v_time) : false;

        public static Func<object, object>? F_dvs_interruptible_wait_Handler { get; set; }
        public static object F_dvs_interruptible_wait(object v_action) => F_dvs_interruptible_wait_Handler != null ? F_dvs_interruptible_wait_Handler(v_action) : false;

        public static Func<object, object, object>? F_dvs_plan_cache_get_Handler { get; set; }
        public static object F_dvs_plan_cache_get(object owner, object key) => F_dvs_plan_cache_get_Handler != null ? F_dvs_plan_cache_get_Handler(owner, key) : false;
        public static Func<object, object, object, object>? F_dvs_plan_cache_put_Handler { get; set; }
        public static object F_dvs_plan_cache_put(object owner, object key, object value) => F_dvs_plan_cache_put_Handler != null ? F_dvs_plan_cache_put_Handler(owner, key, value) : false;
        public static Func<object, object, object, object>? F_dvp_achievement_pending_work_Handler { get; set; }
        public static object F_dvp_achievement_pending_work(object v_actor, object v_action, object v_pending) => F_dvp_achievement_pending_work_Handler != null ? F_dvp_achievement_pending_work_Handler(v_actor, v_action, v_pending) : false;
        public static Func<object, object, object, object>? F_dvp_answer_reward_Handler { get; set; }
        public static object F_dvp_answer_reward(object v_actor, object v_action, object v_mind) => F_dvp_answer_reward_Handler != null ? F_dvp_answer_reward_Handler(v_actor, v_action, v_mind) : false;
        public static Func<object, object, object>? F_dvp_communication_progress_Handler { get; set; }
        public static object F_dvp_communication_progress(object v_actor, object v_action) => F_dvp_communication_progress_Handler != null ? F_dvp_communication_progress_Handler(v_actor, v_action) : false;
        public static Func<object, object, object, object, object>? F_dvp_evidence_action_progress_Handler { get; set; }
        public static object F_dvp_evidence_action_progress(object actor, object action, object reference, object bindings) => F_dvp_evidence_action_progress_Handler != null ? F_dvp_evidence_action_progress_Handler(actor, action, reference, bindings) : false;
        public static Func<object, object, object>? F_dvp_evidence_due_Handler { get; set; }
        public static object F_dvp_evidence_due(object v_actor, object v_goal) => F_dvp_evidence_due_Handler != null ? F_dvp_evidence_due_Handler(v_actor, v_goal) : false;
        public static Func<object, object, object>? F_dvp_extract_required_room_Handler { get; set; }
        public static object F_dvp_extract_required_room(object v_prop, object v_loc_ref) => F_dvp_extract_required_room_Handler != null ? F_dvp_extract_required_room_Handler(v_prop, v_loc_ref) : false;
        public static Func<object, object, object>? F_dvp_personal_motivation_Handler { get; set; }
        public static object F_dvp_personal_motivation(object v_actor, object v_action) => F_dvp_personal_motivation_Handler != null ? F_dvp_personal_motivation_Handler(v_actor, v_action) : false;
        public static Func<object, object, object>? F_dvp_refresh_current_evidence_Handler { get; set; }
        public static object F_dvp_refresh_current_evidence(object v_actor, object v_goals) => F_dvp_refresh_current_evidence_Handler != null ? F_dvp_refresh_current_evidence_Handler(v_actor, v_goals) : false;
        public static Func<object, object, object, object>? F_dvp_scope_Handler { get; set; }
        public static object F_dvp_scope(object v_actor, object v_action, object v_bindings) => F_dvp_scope_Handler != null ? F_dvp_scope_Handler(v_actor, v_action, v_bindings) : false;
        public static Func<object, object, object, object>? F_dvp_source_supported_since_Handler { get; set; }
        public static object F_dvp_source_supported_since(object v_cell, object v_source, object v_minimum_time) => F_dvp_source_supported_since_Handler != null ? F_dvp_source_supported_since_Handler(v_cell, v_source, v_minimum_time) : false;
        public static Func<object, object, object>? F_dvp_owned_request_ready_Handler { get; set; }
        public static object F_dvp_owned_request_ready(object actor, object action) => F_dvp_owned_request_ready_Handler != null ? F_dvp_owned_request_ready_Handler(actor, action) : false;
        public static Func<object, object, object, object>? F_dvp_owner_utility_Handler { get; set; }
        public static object F_dvp_owner_utility(object v_actor, object v_action, object v_mind) => F_dvp_owner_utility_Handler != null ? F_dvp_owner_utility_Handler(v_actor, v_action, v_mind) : false;
        public static Func<object, object, object, object>? F_dvp_private_execution_utility_Handler { get; set; }
        public static object F_dvp_private_execution_utility(object v_actor, object v_action, object v_mind) => F_dvp_private_execution_utility_Handler != null ? F_dvp_private_execution_utility_Handler(v_actor, v_action, v_mind) : false;
    }
}
