using System;
using System.Collections.Generic;
using System.Linq;

namespace PropHunt.Core;

public static class PropHuntRules
{
    public const int DefaultTwoSeekerHiderThreshold = 10;

    public static int GetSeekerCountForTotalPlayers(int playerCount, int twoSeekerHiderThreshold)
    {
        if (playerCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount));
        }

        if (playerCount == 0)
        {
            return 0;
        }

        var hidersWithOneSeeker = Math.Max(0, playerCount - 1);
        return hidersWithOneSeeker > Math.Max(0, twoSeekerHiderThreshold) ? 2 : 1;
    }

    public static bool HasValidBalance(int hiderCount, int seekerCount)
    {
        return hiderCount > seekerCount && seekerCount > 0;
    }

    public static bool IsPropHuntMap(string? mapName, IEnumerable<string> mapNamePatterns)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return false;
        }

        if (mapNamePatterns == null)
        {
            throw new ArgumentNullException(nameof(mapNamePatterns));
        }

        return mapNamePatterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Any(pattern => mapName!.IndexOf(pattern.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
