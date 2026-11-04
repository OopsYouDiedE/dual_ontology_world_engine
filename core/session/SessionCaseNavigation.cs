using DuelView.Core;

public sealed partial class DvSession
{
    /// <summary>Estimates the supplied host navigation polyline; the caller is responsible for its provenance.</summary>
    public CaseTravel.Result EstimateNavigationPath(IReadOnlyList<CaseTravel.Point3> points,
        double minimumSpeed, double maximumSpeed, string source)
        => CaseTravel.FromNavigationPath(points, minimumSpeed, maximumSpeed, source);
}
