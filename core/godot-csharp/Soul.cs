#nullable enable

namespace DualOntologyEngine.Core;

public sealed class Character : Entity
{
	private readonly HashSet<Soul> souls = [];
	public IReadOnlyCollection<Soul> Souls => souls;

	public Character(string name, Entity parent, Dictionary<string, int> tags)
		: base(name, parent, tags) { }

	public void bind(Soul soul)
	{
		if (!souls.Add(soul))
			return;
		soul.bind(this);
	}

	internal void bind_from_soul(Soul soul) => souls.Add(soul);
	internal void unbind_from_soul(Soul soul) => souls.Remove(soul);
}

public sealed class Soul : Entity
{
	private readonly HashSet<Character> characters = [];
	private readonly List<Expression> observations = [];
	private readonly List<RuleBelief> beliefs = [];

	public IReadOnlyCollection<Character> Characters => characters;
	public IReadOnlyList<Expression> Observations => observations;
	public IReadOnlyList<RuleBelief> Beliefs => beliefs;
	public int Stability => getTag("Stability");
	public int Identity => getTag("Identity");

	public Soul(string name, Entity parent, int stability, int identity)
		: base(name, parent, new() { ["Soul"] = 1, ["Stability"] = stability, ["Identity"] = identity }) { }

	public void bind(Character character)
	{
		if (!characters.Add(character))
			return;
		character.bind_from_soul(this);
	}

	public void unbind(Character character)
	{
		if (!characters.Remove(character))
			return;
		character.unbind_from_soul(this);
	}

	public void observe(Expression fact)
	{
		if (!observations.Any(existing => RuleEnvironment.structurally_equal(existing, fact)))
			observations.Add(fact);
	}

	public RuleBelief receive(RulePublication publication, int confidence = 1)
	{
		var existing = beliefs.FirstOrDefault(item => ReferenceEquals(item.Rule, publication.Rule));
		if (existing is not null)
			return existing;
		var belief = new RuleBelief(publication, confidence, CognitiveStage.Received);
		beliefs.Add(belief);
		return belief;
	}

	public RuleBelief understand(Rule rule)
	{
		var belief = find(rule);
		if (belief.Stage == CognitiveStage.Received)
			belief.Stage = CognitiveStage.Understood;
		return belief;
	}

	public RuleBelief consider(Rule rule, Entity source, int confidence = 1, string guaranteeTag = "Guarantee")
	{
		var belief = receive(new RulePublication(rule, source, guaranteeTag), confidence);
		understand(rule);
		var authority = source.getTag(guaranteeTag) + source.getTag("Authority");
		belief.Stage = authority + confidence >= Math.Max(2, 5 - Stability / 2)
			? CognitiveStage.Accepted : CognitiveStage.Rejected;
		return belief;
	}

	public void accept(Rule rule) => find(rule).Stage = CognitiveStage.Accepted;
	public void reject(Rule rule) => find(rule).Stage = CognitiveStage.Rejected;
	public void revise(Rule rule, int confidence)
	{
		var belief = find(rule);
		belief.Confidence = confidence;
	}

	public RuleDecision decide(IEnumerable<Effect> candidates, RuleSystem rules, RuleEnvironment environment,
		Func<Effect, int>? utility = null) => rules.decide(this, candidates, environment, utility);

	private RuleBelief find(Rule rule) => beliefs.First(item => ReferenceEquals(item.Rule, rule));
}

public enum CognitiveStage
{
	Received,
	Understood,
	Accepted,
	Rejected,
}

public sealed record RulePublication(Rule Rule, Entity Issuer, string GuaranteeTag)
{
	public bool HasGuarantee => Issuer.getTag(GuaranteeTag) > 0;
}

public sealed class RuleBelief(RulePublication publication, int confidence, CognitiveStage stage)
{
	public RulePublication Publication { get; } = publication;
	public Rule Rule => Publication.Rule;
	public Entity Source => Publication.Issuer;
	public int Confidence { get; set; } = confidence;
	public CognitiveStage Stage { get; internal set; } = stage;
	public bool Accepted => Stage == CognitiveStage.Accepted;
	public int Priority => Confidence + Source.getTag(Publication.GuaranteeTag) * 2 + Source.getTag("Authority");
}
