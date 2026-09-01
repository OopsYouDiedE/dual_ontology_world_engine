#nullable enable

using System.Collections;
using System.Text;
using static DualOntologyEngine.Core.RuleAst;

namespace DualOntologyEngine.Core;

public sealed class RuleParser
{
	private static readonly Dictionary<string, (DirectiveMode Mode, bool Ordered)> Headers =
		Enum.GetValues<DirectiveMode>()
			.SelectMany(mode => new[]
			{
				(Key: $"{mode.ToString().ToUpperInvariant()}:", Value: (mode, false)),
				(Key: $"{mode.ToString().ToUpperInvariant()} IN ORDER:", Value: (mode, true)),
			})
			.ToDictionary(item => item.Key, item => item.Value);

	private static readonly Dictionary<string, string> Verbs = new(StringComparer.OrdinalIgnoreCase)
	{
		["add_in_order"] = "add_in_order", ["arises_from"] = "arises_from",
		["attacked_by"] = "attacked_by", ["bind"] = "bind", ["binds_to"] = "bind",
		["break"] = "break", ["cool_with"] = "cool_with", ["ends_life"] = "end_life",
		["guides_to"] = "guide_to", ["hides_behind"] = "hide_behind",
		["declare_hostility"] = "declare_hostility", ["perceives"] = "perceives",
		["remain_forbidden"] = "remain_forbidden", ["remove"] = "remove",
		["repeat_after_each"] = "repeat_after_each", ["requests"] = "request",
		["takes_inexact_quantity"] = "take_inexact_quantity", ["abandons"] = "abandon",
		["accepts"] = "accept", ["approaches"] = "approach", ["calls"] = "call",
		["chooses"] = "choose", ["feeds"] = "feed", ["holds"] = "hold",
		["keeps_holding"] = "keep_holding", ["moves_away_from"] = "move_away_from",
		["purchases"] = "purchase", ["reports_to"] = "report_to", ["stays"] = "stay",
		["discards"] = "discard", ["has_separated_from"] = "separated_from",
		["encounters"] = "encounter", ["follows"] = "follow", ["urges"] = "urge",
		["exhibits"] = "exhibit", ["repeatedly_urges"] = "repeatedly_urge",
		["sells"] = "sell", ["tears"] = "tear", ["touches"] = "touch",
		["trusts"] = "trust", ["verifies"] = "verify", ["asks"] = "ask",
		["answers"] = "answer", ["remains_silent"] = "remain_silent",
		["sacrifices"] = "sacrifice", ["seals"] = "seal",
		["becomes"] = "become", ["binds"] = "bind", ["breaks"] = "break",
		["collapses"] = "collapse", ["declares"] = "declare", ["ends"] = "end",
		["enters"] = "enter", ["falls_from"] = "fall_from", ["goes_to"] = "go_to",
		["has_violated"] = "violate", ["ignores"] = "ignore", ["inserts"] = "insert",
		["leaves"] = "leave", ["performs"] = "perform", ["persists"] = "persist",
		["refuses"] = "refuse", ["removes"] = "remove", ["repeats"] = "repeat",
		["sheds"] = "shed", ["summons"] = "summon", ["takes"] = "take",
		["tells"] = "tell", ["wears"] = "wear",
	};

	public Rule Parse(string text)
	{
		var lines = Lines(text);
		var opening = Pop(lines).ToUpperInvariant();
		if (opening is not ("WHEN ALL:" or "WHEN ALWAYS:"))
			throw new RuleSyntaxException("rule must start with WHEN ALL: or WHEN ALWAYS:");

		List<Expression> conditions = [];
		while (lines.Count > 0 && !Headers.ContainsKey(lines[0].ToUpperInvariant()))
			conditions.Add(ParseExpression(Item(Pop(lines))));

		if (opening == "WHEN ALL:" && conditions.Count == 0)
			throw new RuleSyntaxException("WHEN ALL requires a condition");

		var condition = opening == "WHEN ALWAYS:"
			? Expression("true")
			: conditions.Count == 1
				? conditions[0]
				: Expression("all", [.. conditions]);

		List<Directive> directives = [];
		while (lines.Count > 0)
		{
			if (!Headers.TryGetValue(Pop(lines).ToUpperInvariant(), out var header))
				throw new RuleSyntaxException("expected directive header");

			List<Effect> actions = [];
			while (lines.Count > 0 && !Headers.ContainsKey(lines[0].ToUpperInvariant()))
				actions.Add(ParseEffect(Item(Pop(lines))));
			if (actions.Count == 0)
				throw new RuleSyntaxException($"{header.Mode.ToString().ToUpperInvariant()} requires an effect");

			var body = header.Ordered || actions.Count > 1
				? Effect("sequence", [.. actions])
				: actions[0];
			directives.Add(new(header.Mode, body));
		}

		if (directives.Count == 0)
			throw new RuleSyntaxException("rule requires a directive");
		return new(condition, directives, text);
	}

	public WordSpirit ParseWordSpirit(string text)
	{
		var lines = Lines(text);
		if (Pop(lines).ToUpperInvariant() != "WORD SPIRIT:")
			throw new RuleSyntaxException("word spirit must start with WORD SPIRIT:");

		List<string> blocks = [];
		List<string> constraints = [];
		List<string> actual = [];
		Effect cost = Effect("none");
		while (lines.Count > 0)
		{
			switch (Pop(lines).ToUpperInvariant())
			{
				case "CONTRACT COST:":
					List<Effect> costs = [];
					while (lines.Count > 0 &&
						!lines[0].Equals("ACTIVATION RULE:", StringComparison.OrdinalIgnoreCase) &&
						!lines[0].Equals("CONSTRAINT RULE:", StringComparison.OrdinalIgnoreCase) &&
						!lines[0].Equals("ACTUAL EFFECTS IN ORDER:", StringComparison.OrdinalIgnoreCase))
						costs.Add(ParseEffect(Item(Pop(lines))));
					if (costs.Count == 0)
						throw new RuleSyntaxException("CONTRACT COST requires an effect");
					cost = costs.Count == 1 ? costs[0] : Effect("sequence", [.. costs]);
					break;
				case "ACTIVATION RULE:":
					List<string> block = [];
					while (lines.Count > 0 && !lines[0].Equals("END ACTIVATION RULE", StringComparison.OrdinalIgnoreCase))
						block.Add(Pop(lines));
					if (lines.Count == 0)
						throw new RuleSyntaxException("missing END ACTIVATION RULE");
					Pop(lines);
					blocks.Add(string.Join('\n', block));
					break;
				case "CONSTRAINT RULE:":
					List<string> constraint = [];
					while (lines.Count > 0 && !lines[0].Equals("END CONSTRAINT RULE", StringComparison.OrdinalIgnoreCase))
						constraint.Add(Pop(lines));
					if (lines.Count == 0)
						throw new RuleSyntaxException("missing END CONSTRAINT RULE");
					Pop(lines);
					constraints.Add(string.Join('\n', constraint));
					break;
				case "ACTUAL EFFECTS IN ORDER:":
					actual = lines;
					lines = [];
					break;
				default:
					throw new RuleSyntaxException("unexpected word spirit section");
			}
		}

		if (actual.Count == 0)
			throw new RuleSyntaxException("word spirit requires actual effects");
		var effects = actual.Select(line => ParseEffect(Item(line))).ToArray();
		return new(blocks.Select(Parse), constraints.Select(Parse), cost, [Effect("sequence", effects)]);
	}

	public IReadOnlyList<Rule> ParseRules(string text)
	{
		var lines = Lines(text);
		if (Pop(lines).ToUpperInvariant() != "RULES:")
			throw new RuleSyntaxException("rules document must start with RULES:");

		List<Rule> rules = [];
		while (lines.Count > 0)
		{
			if (Pop(lines).ToUpperInvariant() != "RULE:")
				throw new RuleSyntaxException("expected RULE:");
			List<string> block = [];
			while (lines.Count > 0 && !lines[0].Equals("END RULE", StringComparison.OrdinalIgnoreCase))
				block.Add(Pop(lines));
			if (lines.Count == 0)
				throw new RuleSyntaxException("missing END RULE");
			Pop(lines);
			rules.Add(Parse(string.Join('\n', block)));
		}
		return rules;
	}

	public static string FormatAst(object value)
	{
		StringBuilder output = new();
		Visit(value, "", "", true, output);
		return output.ToString().TrimEnd();
	}

	private Expression ParseExpression(string sentence)
	{
		var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (words.Length == 3 && words[1] == "is")
			return Expression("state", words[0], Atom(words[2]));
		if (words.Length == 3 && words[1] == "is_not")
			return Expression("not", Expression("state", words[0], words[2]));
		if (words.Length >= 4 && words[1] == "is")
			return Expression(words[2], [words[0], .. words[3..].Select(Atom)]);
		var before = Array.IndexOf(words, "before");
		if (words.Length >= 4 && words[1] == "has_not" && before >= 0)
			return Expression("not", Expression("completed_before", [.. words[2..before], .. words[(before + 1)..]]));
		if (words.Length >= 3 && words[1] == "count_is_greater_than")
			return Expression("count_gt", [words[0], Atom(words[2]), .. words[3..]]);
		if (words.Length >= 3)
			return Expression(Verb(words[1]), [words[0], .. words[2..].Select(Atom)]);
		throw new RuleSyntaxException($"cannot parse condition {sentence}");
	}

	private Effect ParseEffect(string sentence)
	{
		if (sentence.StartsWith("within ", StringComparison.OrdinalIgnoreCase) && sentence.Contains(", "))
		{
			var parts = sentence.Split(", ", 2);
			var limit = parts[0].Split(' ');
			return Effect("within", $"{limit[2]}_{limit[1]}", ParseEffect(parts[1]));
		}
		var thenAt = sentence.IndexOf(", then ", StringComparison.OrdinalIgnoreCase);
		if (sentence.StartsWith("if ", StringComparison.OrdinalIgnoreCase) && thenAt >= 0)
			return Effect("when", ParseExpression(sentence[3..thenAt]), ParseEffect(sentence[(thenAt + 7)..]));
		foreach (var (prefix, op) in new[] { ("wait until ", "wait_until"), ("verify that ", "verify") })
			if (sentence.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				return Effect(op, ParseExpression(sentence[prefix.Length..]));
		foreach (var (separator, op) in new[] { (" until ", "until"), (" before ", "before") })
		{
			var at = sentence.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
			if (at >= 0)
				return Effect(op, ParseEffect(sentence[..at]), ParseExpression(sentence[(at + separator.Length)..]));
		}

		var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (words.Length < 2)
			throw new RuleSyntaxException($"cannot parse effect {sentence}");
		var arguments = words[2..].ToList();
		var except = arguments.IndexOf("except");
		if (except >= 0)
			return Effect($"{Verb(words[1])}_except", [words[0], .. arguments[..except], .. arguments[(except + 1)..]]);
		return Effect(Verb(words[1]), [words[0], .. arguments.Select(Atom)]);
	}

	private static string Verb(string word) => Verbs.TryGetValue(word, out var verb)
		? verb
		: throw new RuleSyntaxException($"unknown verb {word}");

	private static object Atom(string word) => int.TryParse(word, out var number) ? number : word;

	private static List<string> Lines(string text) =>
		text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
			.Select(line => line.Trim()).Where(line => line.Length > 0).ToList();

	private static string Pop(List<string> lines)
	{
		if (lines.Count == 0)
			return "";
		var first = lines[0];
		lines.RemoveAt(0);
		return first;
	}

	private static string Item(string line)
	{
		line = line.StartsWith("- ") ? line[2..] : line;
		var dot = line.IndexOf(". ", StringComparison.Ordinal);
		if (dot > 0 && int.TryParse(line[..dot], out _))
			line = line[(dot + 2)..];
		return line.TrimEnd('.', ';');
	}

	private static void Visit(object? node, string prefix, string label, bool last, StringBuilder output)
	{
		var branch = last ? "└─ " : "├─ ";
		var childPrefix = prefix + (last ? "   " : "│  ");
		switch (node)
		{
			case WordSpirit spirit:
				output.AppendLine($"{prefix}{branch}{label}WordSpirit");
				Visit(spirit.Rules, childPrefix, "rules: ", false, output);
				Visit(spirit.Constraints, childPrefix, "constraints: ", false, output);
				Visit(spirit.Cost, childPrefix, "cost: ", false, output);
				Visit(spirit.Effects, childPrefix, "effects: ", true, output);
				break;
			case Rule rule:
				output.AppendLine($"{prefix}{branch}{label}Rule");
				Visit(rule.Condition, childPrefix, "condition: ", false, output);
				Visit(rule.Directives, childPrefix, "directives: ", true, output);
				break;
			case Directive directive:
				output.AppendLine($"{prefix}{branch}{label}{directive.Mode.ToString().ToUpperInvariant()}");
				Visit(directive.Effect, childPrefix, "effect: ", true, output);
				break;
			case Expression expression:
				output.AppendLine($"{prefix}{branch}{label}Expression({expression.Operator})");
				Visit(expression.Arguments, childPrefix, "", true, output);
				break;
			case Effect effect:
				output.AppendLine($"{prefix}{branch}{label}Effect({effect.Operator})");
				Visit(effect.Arguments, childPrefix, "", true, output);
				break;
			case IEnumerable items when node is not string:
				var values = items.Cast<object?>().ToArray();
				output.AppendLine($"{prefix}{branch}{label}list");
				for (var index = 0; index < values.Length; index++)
					Visit(values[index], childPrefix, $"[{index}] ", index == values.Length - 1, output);
				break;
			default:
				output.AppendLine($"{prefix}{branch}{label}{node}");
				break;
		}
	}
}
