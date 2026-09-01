#nullable enable

using System.Collections.Immutable;

namespace DualOntologyEngine.Core;

public sealed class RuleEnvironment
{
	private readonly Dictionary<string, object> aliases = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<Expression> facts = [];
	public IReadOnlyList<Expression> Facts => facts;
	public int Version { get; private set; }

	public void clear()
	{
		aliases.Clear();
		facts.Clear();
		Version = 0;
	}

	public void alias(string name, object value) => aliases[name] = value;
	public object resolve(object value) => value is string name && aliases.TryGetValue(name, out var found) ? found : value;

	public bool assert(Expression fact)
	{
		if (facts.Any(item => matches(item, fact)))
			return false;
		facts.Add(fact);
		Version++;
		return true;
	}

	public bool retract(Expression fact)
	{
		var existing = facts.FirstOrDefault(item => matches(item, fact));
		if (existing is null)
			return false;
		facts.Remove(existing);
		Version++;
		return true;
	}

	public bool evaluate(Expression expression) => expression.Operator switch
	{
		"true" => true,
		"all" => expression.Arguments.OfType<Expression>().All(evaluate),
		"not" => expression.Arguments.Length == 1 && expression.Arguments[0] is Expression inner && !evaluate(inner),
		"state" => evaluate_state(expression) || facts.Any(fact => matches(fact, expression)),
		"count_gt" => evaluate_count(expression) || facts.Any(fact => matches(fact, expression)),
		_ => facts.Any(fact => matches(fact, expression)),
	};

	public bool effect_is_fact(Effect effect) => effect.Operator switch
	{
		"sequence" => effect.Arguments.OfType<Effect>().All(effect_is_fact),
		"when" when effect.Arguments is [Expression condition, Effect nested] => evaluate(condition) && effect_is_fact(nested),
		_ => evaluate(new Expression(effect.Operator, effect.Arguments)),
	};

	public void assert_effect(Effect effect)
	{
		if (effect.Operator == "sequence")
		{
			foreach (var nested in effect.Arguments.OfType<Effect>())
				assert_effect(nested);
			return;
		}
		assert(new Expression(effect.Operator, effect.Arguments));
	}

	public Effect materialize(Effect effect) => new(effect.Operator,
		effect.Arguments.Select(argument => argument switch
		{
			Effect nested => materialize(nested),
			Expression expression => materialize(expression),
			_ => resolve(argument),
		}).ToArray());

	public Expression materialize(Expression expression) => new(expression.Operator,
		expression.Arguments.Select(argument => argument is Expression nested ? materialize(nested) : resolve(argument)).ToArray());

	public bool matches(Expression fact, Expression query)
	{
		if (!fact.Operator.Equals(query.Operator, StringComparison.OrdinalIgnoreCase) || fact.Arguments.Length != query.Arguments.Length)
			return false;
		return fact.Arguments.Zip(query.Arguments).All(pair => values_equal(pair.First, pair.Second));
	}

	public bool effect_matches(Effect pattern, Effect candidate)
	{
		if (pattern.Operator == "sequence")
			return pattern.Arguments.OfType<Effect>().Any(item => effect_matches(item, candidate));
		if (!pattern.Operator.Equals(candidate.Operator, StringComparison.OrdinalIgnoreCase) || pattern.Arguments.Length != candidate.Arguments.Length)
			return false;
		return pattern.Arguments.Zip(candidate.Arguments).All(pair => values_equal(pair.First, pair.Second));
	}

	private bool evaluate_state(Expression expression)
	{
		if (expression.Arguments is not [var subjectValue, string state])
			return false;
		return resolve(subjectValue) is Entity entity && entity.getTag(state) > 0;
	}

	private bool evaluate_count(Expression expression)
	{
		if (expression.Arguments.Length < 2 || resolve(expression.Arguments[0]) is not Entity entity)
			return false;
		return expression.Arguments[1] is int threshold && entity.children.Count > threshold;
	}

	private bool values_equal(object left, object right)
	{
		left = resolve(left);
		right = resolve(right);
		if (left is Expression leftExpression && right is Expression rightExpression)
			return matches(leftExpression, rightExpression);
		if (left is Effect leftEffect && right is Effect rightEffect)
			return effect_matches(leftEffect, rightEffect);
		if (left is string leftText && right is string rightText)
			return leftText.Equals(rightText, StringComparison.OrdinalIgnoreCase);
		return Equals(left, right);
	}

	public static bool structurally_equal(Expression left, Expression right)
	{
		if (!left.Operator.Equals(right.Operator, StringComparison.OrdinalIgnoreCase) || left.Arguments.Length != right.Arguments.Length)
			return false;
		return left.Arguments.Zip(right.Arguments).All(pair => pair switch
		{
			{ First: Expression a, Second: Expression b } => structurally_equal(a, b),
			_ => Equals(pair.First, pair.Second),
		});
	}
}

public sealed class RuleSystem
{
	public RuleDecision evaluate_intent(Soul soul, Effect intent, RuleEnvironment environment)
	{
		var active = soul.Beliefs
			.Where(belief => belief.Accepted && environment.evaluate(belief.Rule.Condition))
			.SelectMany(belief => expand(belief.Rule.Directives, environment).Select(directive => (Belief: belief, Directive: directive)))
			.ToArray();

		if (active.Length == 0)
			return new(intent, true, 0, null, "没有已接受规则评价这个意图");

		var evaluated = active.Select(item =>
		{
			var matches = environment.effect_matches(item.Directive.Effect, intent);
			var contribution = (item.Directive.Mode, matches) switch
			{
				(DirectiveMode.Require, true) => 100 + item.Belief.Priority,
				(DirectiveMode.Require, false) => -80 - item.Belief.Priority,
				(DirectiveMode.Prefer, true) => 50 + item.Belief.Priority,
				(DirectiveMode.Permit, true) => 20 + item.Belief.Priority,
				(DirectiveMode.Forbid, true) => -100 - item.Belief.Priority,
				_ => 0,
			};
			return (item.Belief, item.Directive, Matches: matches, Contribution: contribution);
		}).Where(item => item.Contribution != 0).ToArray();

		if (evaluated.Length == 0)
			return new(intent, true, 0, null, "已接受规则与这个意图无关");

		var score = evaluated.Sum(item => item.Contribution);
		var strongest = evaluated.OrderByDescending(item => Math.Abs(item.Contribution)).First();
		var complies = !evaluated.Any(item => item.Directive.Mode == DirectiveMode.Forbid && item.Matches) &&
			!evaluated.Any(item => item.Directive.Mode == DirectiveMode.Require && !item.Matches);
		var reason = strongest.Directive.Mode == DirectiveMode.Require && !strongest.Matches
			? $"未满足 REQUIRE：{strongest.Belief.Source.name}"
			: $"{strongest.Directive.Mode}：{strongest.Belief.Source.name}";
		return new(intent, complies, score, strongest.Belief, reason);
	}

	public RuleDecision decide(Soul soul, IEnumerable<Effect> candidates, RuleEnvironment environment,
		Func<Effect, int>? utility = null)
	{
		var available = candidates.ToList();
		foreach (var required in required_intents(soul, environment))
			if (!available.Any(candidate => environment.effect_matches(required, candidate)))
				available.Add(required);
		return available
		.Select(intent =>
		{
			var decision = evaluate_intent(soul, intent, environment);
			return decision with { Score = decision.Score + (utility?.Invoke(intent) ?? 0) };
		})
		.OrderByDescending(decision => decision.Score)
		.First();
	}

	public IEnumerable<Effect> required_intents(Soul soul, RuleEnvironment environment) => soul.Beliefs
		.Where(belief => belief.Accepted && environment.evaluate(belief.Rule.Condition))
		.SelectMany(belief => expand(belief.Rule.Directives, environment))
		.Where(directive => directive.Mode == DirectiveMode.Require)
		.Select(directive => environment.materialize(directive.Effect));

	public bool objective_allows(Effect intent, IEnumerable<Rule> constraints, RuleEnvironment environment)
	{
		foreach (var rule in constraints.Where(rule => environment.evaluate(rule.Condition)))
			foreach (var directive in expand(rule.Directives, environment))
				if (directive.Mode == DirectiveMode.Forbid && environment.effect_matches(directive.Effect, intent))
					return false;
		return true;
	}

	public bool activation_satisfied(Rule rule, RuleEnvironment environment)
	{
		if (!environment.evaluate(rule.Condition))
			return false;
		return expand(rule.Directives, environment).All(directive => directive.Mode switch
		{
			DirectiveMode.Require => environment.effect_is_fact(directive.Effect),
			DirectiveMode.Forbid => !environment.effect_is_fact(directive.Effect),
			_ => true,
		});
	}

	private static IEnumerable<Directive> expand(IEnumerable<Directive> directives, RuleEnvironment environment)
	{
		foreach (var directive in directives)
		{
			if (directive.Effect.Operator == "when" && directive.Effect.Arguments is [Expression condition, Effect nested])
			{
				if (environment.evaluate(condition))
					yield return new(directive.Mode, nested);
			}
			else
				yield return directive;
		}
	}

}

public sealed record RuleDecision(Effect Intent, bool Allowed, int Score, RuleBelief? Basis, string Reason);

public sealed record Intent(Character Actor, Soul Mind, Effect Effect, float MotionCost, Action Resolve, string Label);
