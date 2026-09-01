#nullable enable

namespace DualOntologyEngine.Core;

public sealed class WordSpiritSystem(RuleEnvironment environment, RuleSystem rules)
{
	private readonly Dictionary<string, (Func<Effect, bool> Validate, Action<Effect> Apply)> handlers =
		new(StringComparer.OrdinalIgnoreCase);
	private readonly List<WordSpiritBinding> bindings = [];

	public IReadOnlyList<WordSpiritBinding> Bindings => bindings;
	public int ConstraintChecks { get; private set; }
	public int BlockedIntents { get; private set; }

	public void register(string operation, Func<Effect, bool> validate, Action<Effect> apply) =>
		handlers[operation] = (validate, apply);

	public WordSpiritBinding? sign(string name, WordSpirit definition, Soul signer, WordSpiritOffer offer)
	{
		if (definition.Cost.Operator == "none" || !SemanticMatcher.cost_matches(offer.Payment, offer.RequiredCost) ||
			SemanticMatcher.binding_strength(signer, offer.Target, offer.Anchors, environment) < offer.RequiredBinding ||
			!validate_cost(definition.Cost))
			return null;
		apply(definition.Cost);
		var binding = new WordSpiritBinding(name, definition, signer, offer.Target);
		bindings.Add(binding);
		return binding;
	}

	public bool allows(Effect intent)
	{
		ConstraintChecks++;
		var allowed = bindings.Where(binding => binding.Active)
			.All(binding => rules.objective_allows(intent, binding.Definition.Constraints, environment));
		if (!allowed)
			BlockedIntents++;
		return allowed;
	}

	public IReadOnlyList<WordSpiritBinding> trigger()
	{
		List<WordSpiritBinding> fired = [];
		foreach (var binding in bindings.Where(binding => binding.Active && !binding.Fired).ToArray())
		{
			if (!binding.Definition.Rules.All(rule => rules.activation_satisfied(rule, environment)))
				continue;
			if (!binding.Definition.Effects.All(validate))
				continue;
			foreach (var effect in binding.Definition.Effects)
				apply(effect);
			binding.Fired = true;
			binding.Active = binding.Definition.Constraints.Length > 0;
			fired.Add(binding);
		}
		return fired;
	}

	private bool validate(Effect effect)
	{
		if (effect.Operator == "sequence")
			return effect.Arguments.OfType<Effect>().All(validate);
		if (effect.Operator == "when" && effect.Arguments is [Expression condition, Effect nested])
			return !environment.evaluate(condition) || validate(nested);
		return !handlers.TryGetValue(effect.Operator, out var handler) || handler.Validate(effect);
	}

	private bool validate_cost(Effect effect)
	{
		if (effect.Operator == "sequence")
			return effect.Arguments.OfType<Effect>().All(validate_cost);
		if (!handlers.TryGetValue(effect.Operator, out var handler))
			return false;
		return handler.Validate(effect);
	}

	private void apply(Effect effect)
	{
		if (effect.Operator == "sequence")
		{
			foreach (var item in effect.Arguments.OfType<Effect>())
				apply(item);
			return;
		}
		if (effect.Operator == "when" && effect.Arguments is [Expression condition, Effect nested])
		{
			if (environment.evaluate(condition))
				apply(nested);
			return;
		}
		if (handlers.TryGetValue(effect.Operator, out var handler))
			handler.Apply(effect);
		else
			environment.assert_effect(effect);
	}
}

public sealed class WordSpiritBinding(string name, WordSpirit definition, Soul signer, Entity target)
{
	public string Name { get; } = name;
	public WordSpirit Definition { get; } = definition;
	public Soul Signer { get; } = signer;
	public Entity Target { get; } = target;
	public bool Active { get; internal set; } = true;
	public bool Fired { get; internal set; }
}

public sealed record WordSpiritOffer(
	Entity Target,
	Entity Payment,
	IReadOnlyList<Entity> Anchors,
	int RequiredBinding,
	IReadOnlyDictionary<string, int> RequiredCost
);

public static class SemanticMatcher
{
	public static int binding_strength(Soul signer, Entity target, IEnumerable<Entity> anchors, RuleEnvironment environment)
	{
		var score = signer.Characters.Any(character => character == target) ? 3 : 0;
		foreach (var anchor in anchors.Distinct())
		{
			score += anchor.getTag("TrueName") * 3 + anchor.getTag("Name") + anchor.getTag("Bond") +
				anchor.getTag("Authority") + anchor.getTag("Seal");
			if (anchor.parent == target || target.parent == anchor)
				score++;
			if (environment.Facts.Any(fact => fact.Arguments.Any(argument => ReferenceEquals(argument, target)) &&
				fact.Arguments.Any(argument => ReferenceEquals(argument, anchor))))
				score++;
		}
		return score;
	}

	public static bool cost_matches(Entity payment, IReadOnlyDictionary<string, int> required) =>
		payment.parent is not null && required.All(item => payment.getTag(item.Key) >= item.Value);
}
