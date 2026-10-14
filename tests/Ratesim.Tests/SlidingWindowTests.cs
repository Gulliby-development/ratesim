using Ratesim.Core;
using Xunit;

namespace Ratesim.Tests;

public class SlidingWindowTests
{
    private static SlidingWindowPolicy Make(long limit, long windowMs) => new(limit, windowMs);

    [Theory]
    [InlineData(10000L, true, "1 of 1 used")]
    [InlineData(10001L, true, "1 of 1 used")]
    [InlineData(9999L, false, "1 of 1 used")]
    [InlineData(1L, false, "1 of 1 used")]
    [InlineData(0L, false, "1 of 1 used")]
    public void SecondRequestAtWindowEdge(long secondMs, bool allowed, string detail)
    {
        var policy = Make(1, 10000);

        Assert.Equal(new Decision(true, "1 of 1 used"), policy.Decide("a", 0, 1));
        Assert.Equal(new Decision(allowed, detail), policy.Decide("a", secondMs, 1));
    }

    [Fact]
    public void CostTwoDenialKeepsUsedCost()
    {
        var policy = Make(3, 10000);

        Assert.Equal(new Decision(true, "1 of 3 used"), policy.Decide("a", 0, 1));
        Assert.Equal(new Decision(true, "2 of 3 used"), policy.Decide("a", 1000, 1));
        Assert.Equal(new Decision(false, "2 of 3 used"), policy.Decide("a", 2000, 2));
    }

    [Fact]
    public void OldestRequestDropsOutAtExactlyWindow()
    {
        var policy = Make(3, 10000);

        Assert.Equal(new Decision(true, "1 of 3 used"), policy.Decide("a", 0, 1));
        Assert.Equal(new Decision(true, "2 of 3 used"), policy.Decide("a", 5000, 1));
        Assert.Equal(new Decision(true, "3 of 3 used"), policy.Decide("a", 9000, 1));
        Assert.Equal(new Decision(true, "3 of 3 used"), policy.Decide("a", 10000, 1));
    }

    [Fact]
    public void FullWindowDeniesOneMillisecondBeforeDropOut()
    {
        var policy = Make(3, 10000);

        policy.Decide("a", 0, 1);
        policy.Decide("a", 5000, 1);
        policy.Decide("a", 9000, 1);
        Assert.Equal(new Decision(false, "3 of 3 used"), policy.Decide("a", 9999, 1));
    }

    [Fact]
    public void DeniedRequestsNeverCount()
    {
        var policy = Make(1, 10000);

        Assert.True(policy.Decide("a", 0, 1).Allowed);
        Assert.False(policy.Decide("a", 5000, 1).Allowed);
        Assert.False(policy.Decide("a", 9999, 1).Allowed);
        Assert.Equal(new Decision(true, "1 of 1 used"), policy.Decide("a", 10000, 1));
    }

    [Fact]
    public void KeysHaveSeparateState()
    {
        var policy = Make(1, 10000);

        Assert.True(policy.Decide("a", 0, 1).Allowed);
        Assert.Equal(new Decision(true, "1 of 1 used"), policy.Decide("b", 0, 1));
        Assert.False(policy.Decide("a", 1, 1).Allowed);
        Assert.False(policy.Decide("b", 1, 1).Allowed);
    }

    [Fact]
    public void CostAboveLimitIsDeniedWithNothingUsed()
    {
        var policy = Make(3, 10000);

        Assert.Equal(new Decision(false, "0 of 3 used"), policy.Decide("a", 0, 4));
    }

    [Fact]
    public void CostEqualToLimitIsAllowed()
    {
        var policy = Make(3, 10000);

        Assert.Equal(new Decision(true, "3 of 3 used"), policy.Decide("a", 0, 3));
        Assert.Equal(new Decision(false, "3 of 3 used"), policy.Decide("a", 1, 1));
    }

    [Fact]
    public void SameTimeRequestsAccumulate()
    {
        var policy = Make(2, 1000);

        Assert.Equal(new Decision(true, "1 of 2 used"), policy.Decide("a", 500, 1));
        Assert.Equal(new Decision(true, "2 of 2 used"), policy.Decide("a", 500, 1));
        Assert.Equal(new Decision(false, "2 of 2 used"), policy.Decide("a", 500, 1));
    }

    [Fact]
    public void ParsedSpecBehavesAsSlidingWindow()
    {
        Assert.True(PolicySpec.TryParse("sliding:1/10s", out var policy));

        Assert.True(policy.Decide("a", 0, 1).Allowed);
        Assert.False(policy.Decide("a", 9999, 1).Allowed);
        Assert.True(policy.Decide("a", 10000, 1).Allowed);
    }
}
