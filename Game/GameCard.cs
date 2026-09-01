#nullable enable

using DualOntologyEngine.Core;

namespace DualOntologyEngine.Game;

public sealed class GameCard : Entity
{
    public string Subtitle { get; }
    public string Description { get; }
    public bool Studied { get; set; }

    public GameCard(
        string name,
        Entity parent,
        string subtitle,
        string description,
        Dictionary<string, int> tags
    ) : base(name, parent, tags)
    {
        Subtitle = subtitle;
        Description = description;
    }

    public bool Has(string tag) => getTag(tag) > 0;
}

public sealed class ScheduledAction : Entity
{
    private Action? callback;

    public ScheduledAction(string name, Entity parent, Action callback)
        : base(name, parent, []) => this.callback = callback;

    public override SortedDictionary<float, Entity> update_motion_queue()
    {
        callback?.Invoke();
        callback = null;
        set_parent(null);
        return [];
    }
}

public sealed class RecurringAction : Entity
{
	private readonly float delay;
	private readonly Func<bool> callback;

	public RecurringAction(string name, Entity parent, float delay, Func<bool> callback)
		: base(name, parent, [])
	{
		this.delay = delay;
		this.callback = callback;
	}

	public override SortedDictionary<float, Entity> update_motion_queue()
	{
		if (!callback())
		{
			set_parent(null);
			return [];
		}

		return new() { [delay] = this };
	}
}
