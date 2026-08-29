namespace DualOntologyEngine.Core;

public interface IActionUnit
{
    float ActionDelay { get; }

    IEnumerable<IActionUnit> Resolve();

    void ScheduleNextAction();
}
