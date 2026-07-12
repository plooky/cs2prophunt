using PropHunt.Core;

namespace PropHunt.Plugin;

public sealed class PluginConfig
{
    public string SeekerTeam { get; set; } = "CT";

    public int TwoSeekerHiderThreshold { get; set; } = PropHuntRules.DefaultTwoSeekerHiderThreshold;

}
