using DuelView.Compiled;
using static DuelView.Core.DVKernel;

public static partial class CampaignRuntime
{
    private static object Authored(string id)
    {
        var definition = R.content("data", id);
        if (definition is false) throw new InvalidDataException("Missing campaign data: " + id);
        return F_dv_get(definition, "value");
    }

    private static object AuthoredCopy(string id) => R.copy_value(Authored(id));
}
