using DuelView.Core;
using DuelView.Compiled;
using static DuelView.Core.DVKernel;
public static partial class CampaignRuntime {
    public static object F_island_topology_rooms() {
        return DvValueTemplate.Instantiate("template.island_topology_rooms");
    }
    public static object F_island_topology_edges() {
        return DvValueTemplate.Instantiate("template.island_topology_edges");
    }
}
