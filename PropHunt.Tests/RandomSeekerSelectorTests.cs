using PropHunt.Core;
using Xunit;

namespace PropHunt.Tests;

public sealed class RandomSeekerSelectorTests
{
    [Fact]
    public void RandomDrawCanSelectPreviousSeekerBecauseAllPlayersAreEligible()
    {
        var selector = new RandomSeekerSelector();

        Assert.Equal(new[] { 1UL }, selector.ChooseNextSeekers(new[] { 1UL, 2UL, 3UL }, 1, maximum => maximum - 1));
    }

    [Fact]
    public void PropWhoKillsSeekerIsProtectedForExactlyOneSuccessfulDraw()
    {
        var selector = new RandomSeekerSelector();
        selector.RecordKill(1, 2, victimWasSeeker: true, attackerWasSeeker: false);

        var protectedRound = selector.ChooseNextSeekers(new[] { 1UL, 2UL, 3UL }, 1, _ => 0);
        var followingRound = selector.ChooseNextSeekers(new[] { 2UL }, 1, _ => 0);

        Assert.DoesNotContain(2UL, protectedRound);
        Assert.Contains(2UL, followingRound);
    }

    [Fact]
    public void MultiplePropKillersAreProtectedTogether()
    {
        var selector = new RandomSeekerSelector();
        selector.RecordKill(1, 3, true, false);
        selector.RecordKill(2, 4, true, false);

        var seekers = selector.ChooseNextSeekers(new[] { 1UL, 2UL, 3UL, 4UL, 5UL }, 2, _ => 0);

        Assert.DoesNotContain(3UL, seekers);
        Assert.DoesNotContain(4UL, seekers);
    }

    [Fact]
    public void DisconnectedProtectedPlayerDoesNotAffectDraw()
    {
        var selector = new RandomSeekerSelector();
        selector.RecordKill(1, 9, true, false);

        Assert.Single(selector.ChooseNextSeekers(new[] { 1UL, 2UL, 3UL }, 1, _ => 0));
    }

    [Fact]
    public void InsufficientUnprotectedPlayersReturnsNoSelectionAndKeepsProtection()
    {
        var selector = new RandomSeekerSelector();
        selector.RecordKill(1, 2, true, false);

        Assert.Empty(selector.ChooseNextSeekers(new[] { 1UL, 2UL }, 2, _ => 0));
        Assert.Contains(2UL, selector.ProtectedNextRound);
    }

    [Fact]
    public void ResetAndAdminOverrideClearProtection()
    {
        var selector = new RandomSeekerSelector();
        selector.RecordKill(1, 2, true, false);
        selector.OverrideProtection();
        Assert.Empty(selector.ProtectedNextRound);

        selector.RecordKill(1, 3, true, false);
        selector.Reset();
        Assert.Empty(selector.ProtectedNextRound);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(11, 10, 1)]
    [InlineData(12, 10, 2)]
    public void SeekerCountUsesHideAndSeekThreshold(int players, int threshold, int expected)
    {
        Assert.Equal(expected, PropHuntRules.GetSeekerCountForTotalPlayers(players, threshold));
    }

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(1, 1, false)]
    [InlineData(2, 2, false)]
    public void BalanceRequiresMoreHidersThanSeekers(int hiders, int seekers, bool expected)
    {
        Assert.Equal(expected, PropHuntRules.HasValidBalance(hiders, seekers));
    }
}
