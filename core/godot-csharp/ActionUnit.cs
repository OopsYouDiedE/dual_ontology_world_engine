namespace DualOntologyEngine.Core;

public abstract class ActionUnit(float actionDelay = 0)
{
	public float ActionDelay { get; protected set; } = actionDelay;

	public virtual IEnumerable<ActionUnit> Resolve()
	{
		yield return this;
	}

	public abstract void ScheduleNextAction();
}
