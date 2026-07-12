using System;
using System.Collections.Generic;
using System.Linq;

namespace PropHunt.Core;

public sealed class RandomSeekerSelector
{
    private readonly HashSet<ulong> protectedNextRound = new HashSet<ulong>();

    public IReadOnlyCollection<ulong> ProtectedNextRound => protectedNextRound;

    public void RecordKill(ulong victimId, ulong? attackerId, bool victimWasSeeker, bool attackerWasSeeker)
    {
        if (!victimWasSeeker || attackerWasSeeker || !attackerId.HasValue ||
            attackerId.Value == 0 || attackerId.Value == victimId)
        {
            return;
        }

        protectedNextRound.Add(attackerId.Value);
    }

    public IReadOnlyList<ulong> ChooseNextSeekers(
        IEnumerable<ulong> eligiblePlayerIds,
        int seekerCount,
        Func<int, int> nextIndex)
    {
        if (eligiblePlayerIds == null)
        {
            throw new ArgumentNullException(nameof(eligiblePlayerIds));
        }

        if (nextIndex == null)
        {
            throw new ArgumentNullException(nameof(nextIndex));
        }

        if (seekerCount <= 0)
        {
            return Array.Empty<ulong>();
        }

        var candidates = eligiblePlayerIds
            .Where(id => id != 0 && !protectedNextRound.Contains(id))
            .Distinct()
            .ToList();

        if (candidates.Count < seekerCount)
        {
            return Array.Empty<ulong>();
        }

        for (var i = candidates.Count - 1; i > 0; i--)
        {
            var selected = nextIndex(i + 1);
            if (selected < 0 || selected > i)
            {
                throw new ArgumentOutOfRangeException(nameof(nextIndex), "Random index was outside the requested range.");
            }

            var value = candidates[i];
            candidates[i] = candidates[selected];
            candidates[selected] = value;
        }

        protectedNextRound.Clear();
        return candidates.Take(seekerCount).ToArray();
    }

    public void OverrideProtection()
    {
        protectedNextRound.Clear();
    }

    public void Reset()
    {
        protectedNextRound.Clear();
    }
}
