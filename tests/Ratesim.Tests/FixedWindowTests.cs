using Ratesim.Core;
using Xunit;

namespace Ratesim.Tests;

public class FixedWindowTests
{
    private const long Base = 1714557600000; // 2024-05-01T10:00:00Z, a multiple of 10000

    private static FixedWindowPolicy Policy(long limit, long windowMs) => new(limit, windowMs);

    [Fact]
    public void WindowEdge_LastMillisecondDenied()
    {
        var p = Policy(1, 10000);
        Assert.True(p.Decide("a", Base, 1).Allowed);
        var d = p.Decide("a", Base + 9999, 1);
        Assert.False(d.Allowed);
        Assert.Equal("1 of 1 used", d.Detail);
    }

    [Fact]
    public void WindowEdge_NextWindowAllowed()
    {
        var p = Policy(1, 10000);
        Assert.True(p.Decide("a", Base + 9999, 1).Allowed);
        var d = p.Decide("a", Base + 10000, 1);
        Assert.True(d.Allowed);
        Assert.Equal("1 of 1 used", d.Detail);
    }

    [Fact]
    public void WindowEdge_FromEpochZero()
    {
        var p = Policy(1, 10000);
        Assert.True(p.Decide("a", 0, 1).Allowed);
        Assert.False(p.Decide("a", 9999, 1).Allowed);
        Assert.True(p.Decide("a", 10000, 1).Allowed);
    }

    [Fact]
    public void WindowsAreAlignedToMultiplesSinceEpoch()
    {
        var p = Policy(1, 10000);
        Assert.True(p.Decide("a", Base + 5000, 1).Allowed);
        Assert.False(p.Decide("a", Base + 9999, 1).Allowed);
        Assert.True(p.Decide("a", Base + 10000, 1).Allowed);
        Assert.False(p.Decide("a", Base + 19999, 1).Allowed);
        Assert.True(p.Decide("a", Base + 20000, 1).Allowed);
    }

    [Fact]
    public void CostTwoThenTwo_SecondDenied()
    {
        var p = Policy(3, 10000);
        var first = p.Decide("a", Base, 2);
        var second = p.Decide("a", Base + 1000, 2);
        Assert.True(first.Allowed);
        Assert.Equal("2 of 3 used", first.Detail);
        Assert.False(second.Allowed);
        Assert.Equal("2 of 3 used", second.Detail);
    }

    [Fact]
    public void CostTwoThenOne_FillsLimit()
    {
        var p = Policy(3, 10000);
        Assert.Equal("2 of 3 used", p.Decide("a", Base, 2).Detail);
        var second = p.Decide("a", Base + 1, 1);
        Assert.True(second.Allowed);
        Assert.Equal("3 of 3 used", second.Detail);
    }

    [Fact]
    public void DeniedRequestConsumesNothing()
    {
        var p = Policy(3, 10000);
        Assert.True(p.Decide("a", Base, 2).Allowed);
        Assert.False(p.Decide("a", Base + 1, 2).Allowed);
        Assert.False(p.Decide("a", Base + 2, 2).Allowed);
        var d = p.Decide("a", Base + 3, 1);
        Assert.True(d.Allowed);
        Assert.Equal("3 of 3 used", d.Detail);
    }

    [Fact]
    public void CostEqualToLimitIsAllowed()
    {
        var p = Policy(5, 1000);
        var d = p.Decide("a", Base, 5);
        Assert.True(d.Allowed);
        Assert.Equal("5 of 5 used", d.Detail);
    }

    [Fact]
    public void CostAboveLimitIsDeniedWithZeroUsed()
    {
        var p = Policy(5, 1000);
        var d = p.Decide("a", Base, 6);
        Assert.False(d.Allowed);
        Assert.Equal("0 of 5 used", d.Detail);
    }

    [Fact]
    public void KeysHaveSeparateCounts()
    {
        var p = Policy(1, 10000);
        Assert.True(p.Decide("a", Base, 1).Allowed);
        Assert.False(p.Decide("a", Base + 1, 1).Allowed);
        var d = p.Decide("b", Base + 2, 1);
        Assert.True(d.Allowed);
        Assert.Equal("1 of 1 used", d.Detail);
    }

    [Fact]
    public void CountResetsInNewWindow()
    {
        var p = Policy(2, 1000);
        Assert.Equal("2 of 2 used", p.Decide("a", Base, 2).Detail);
        var d = p.Decide("a", Base + 1000, 1);
        Assert.True(d.Allowed);
        Assert.Equal("1 of 2 used", d.Detail);
    }

    [Fact]
    public void LargeLimitAndCostAtDomainEdge()
    {
        var p = Policy(999999, 86400000);
        var d = p.Decide("a", Base, 999999);
        Assert.True(d.Allowed);
        Assert.Equal("999999 of 999999 used", d.Detail);
        Assert.False(p.Decide("a", Base + 1, 1).Allowed);
    }

    [Fact]
    public void SmallWindowOfOneMillisecond()
    {
        var p = Policy(1, 1);
        Assert.True(p.Decide("a", 5, 1).Allowed);
        Assert.True(p.Decide("a", 6, 1).Allowed);
        var p2 = Policy(1, 2);
        Assert.True(p2.Decide("a", 4, 1).Allowed);
        Assert.False(p2.Decide("a", 5, 1).Allowed);
    }

    [Fact]
    public void TryParse_ReturnsFreshInstances()
    {
        Assert.True(PolicySpec.TryParse("fixed:1/10s", out var one));
        Assert.True(PolicySpec.TryParse("fixed:1/10s", out var two));
        Assert.NotSame(one, two);
        Assert.True(one.Decide("a", Base, 1).Allowed);
        Assert.True(two.Decide("a", Base, 1).Allowed);
    }

    [Fact]
    public void TryParse_Null_IsInvalid()
    {
        Assert.False(PolicySpec.TryParse(null, out _));
    }

    [Fact]
    public void TryParse_FixedUsesGivenNumbers()
    {
        Assert.True(PolicySpec.TryParse("fixed:2/1s", out var p));
        Assert.Equal("1 of 2 used", p.Decide("a", 0, 1).Detail);
        Assert.Equal("2 of 2 used", p.Decide("a", 999, 1).Detail);
        Assert.False(p.Decide("a", 999, 1).Allowed);
        Assert.True(p.Decide("a", 1000, 1).Allowed);
    }

    [Theory]
    [InlineData("fixed:5/10s", typeof(FixedWindowPolicy))]
    [InlineData("sliding:3/10s", typeof(SlidingWindowPolicy))]
    [InlineData("bucket:2/1s,10", typeof(TokenBucketPolicy))]
    [InlineData("fixed:999999/24h", typeof(FixedWindowPolicy))]
    [InlineData("sliding:1/1ms", typeof(SlidingWindowPolicy))]
    [InlineData("fixed:1/500ms", typeof(FixedWindowPolicy))]
    [InlineData("bucket:999999/1440m,999999", typeof(TokenBucketPolicy))]
    public void TryParse_ValidSpecs(string spec, Type expected)
    {
        Assert.True(PolicySpec.TryParse(spec, out var policy));
        Assert.IsType(expected, policy);
    }

    [Theory]
    [InlineData("fixed:0/10s")]
    [InlineData("fixed:05/10s")]
    [InlineData("fixed:5/010s")]
    [InlineData("fixed:5/25h")]
    [InlineData("fixed:5/86401s")]
    [InlineData("fixed:1000000/1s")]
    [InlineData("bucket:2/1s")]
    [InlineData("bucket:2/1s,0")]
    [InlineData("bucket:2/1s,02")]
    [InlineData("fixed:5/10s,2")]
    [InlineData("Fixed:5/10s")]
    [InlineData("fixed: 5/10s")]
    [InlineData("fixed:5/10s ")]
    [InlineData("token:1/1s")]
    [InlineData("fixed:5")]
    [InlineData("")]
    [InlineData("fixed:/10s")]
    [InlineData("fixed:5/")]
    [InlineData("sliding:3/10")]
    [InlineData("sliding:3/10S")]
    [InlineData("bucket:0/1s,1")]
    [InlineData("bucket:1/25h,1")]
    [InlineData("bucket:1/1s,1000000")]
    [InlineData("fixed:-1/10s")]
    public void TryParse_InvalidSpecs(string spec)
    {
        Assert.False(PolicySpec.TryParse(spec, out _));
    }
}
