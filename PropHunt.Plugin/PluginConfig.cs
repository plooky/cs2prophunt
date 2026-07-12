using PropHunt.Core;

namespace PropHunt.Plugin;

public sealed class PluginConfig
{
    public string[] MapNamePatterns { get; set; } = { "ph_", "prophunt", "prop_hunt", "prop-hunt" };

    public string SeekerTeam { get; set; } = "CT";

    public int TwoSeekerHiderThreshold { get; set; } = PropHuntRules.DefaultTwoSeekerHiderThreshold;

    public bool AutoStart { get; set; } = true;
}
