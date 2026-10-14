using Ratesim.Core;

namespace Ratesim.Tests;

public class TokenBucketTests
{
    private static IPolicy Make(string spec)
    {
        Assert.True(PolicySpec.TryParse(spec, out IPolicy policy));
        return policy;
    }

    private static long Ts(string text)
    {
        Assert.True(TimeText.TryParseTimestamp(text, out long ms));
        return ms;
    }

    private static void Expect(Decision decision, bool allowed, string detail)
    {
        Assert.Equal(allowed, decision.Allowed);
        Assert.Equal(detail, decision.Detail);
    }

    [Fact]
    public void BurstIsConsumedThenDenied()
    {
        IPolicy p = Make("bucket:1/1s,2");
        Expect(p.Decide("a", 0, 1), true, "tokens left: 1");
        Expect(p.Decide("a", 0, 1), true, "tokens left: 0");
        Expect(p.Decide("a", 0, 1), false, "tokens left: 0");
    }

    [Fact]
    public void FractionalRefillCountsFromLastAllowedState()
    {
        IPolicy p = Make("bucket:1/5s,1");
        Expect(p.Decide("a", 0, 1), true, "tokens left: 0");
        Expect(p.Decide("a", 2500, 1), false, "tokens left: 0");
        Expect(p.Decide("a", 5000, 1), true, "tokens left: 0");
    }

    [Fact]
    public void OneMillisecondBeforeFullRefillIsDenied()
    {
        IPolicy p = Make("bucket:1/5s,1");
        Expect(p.Decide("a", 0, 1), true, "tokens left: 0");
        Expect(p.Decide("a", 4999, 1), false, "tokens left: 0");
    }

    [Fact]
    public void RefillIsCappedAtBurst()
    {
        IPolicy p = Make("bucket:1/1s,2");
        Expect(p.Decide("a", 0, 1), true, "tokens left: 1");
        Expect(p.Decide("a", 100000, 1), true, "tokens left: 1");
    }

    [Fact]
    public void CostAboveBurstIsDeniedAndBucketStaysFull()
    {
        IPolicy p = Make("bucket:2/1s,10");
        Expect(p.Decide("a", 0, 11), false, "cost 11 exceeds burst 10");
        Expect(p.Decide("a", 0, 10), true, "tokens left: 0");
    }

    [Fact]
    public void CostEqualToBurstIsAllowed()
    {
        IPolicy p = Make("bucket:1/1s,3");
        Expect(p.Decide("a", 0, 3), true, "tokens left: 0");
    }

    [Fact]
    public void DeniedRequestDoesNotChangeState()
    {
        IPolicy p = Make("bucket:2/1s,10");
        Expect(p.Decide("a", 0, 10), true, "tokens left: 0");
        Expect(p.Decide("a", 0, 11), false, "cost 11 exceeds burst 10");
        Expect(p.Decide("a", 500, 1), true, "tokens left: 0");
        Expect(p.Decide("a", 500, 1), false, "tokens left: 0");
        Expect(p.Decide("a", 1000, 1), true, "tokens left: 0");
    }

    [Fact]
    public void NewKeyStartsFull()
    {
        IPolicy p = Make("bucket:1/1s,2");
        Expect(p.Decide("a", 0, 2), true, "tokens left: 0");
        Expect(p.Decide("b", 0, 1), true, "tokens left: 1");
        Expect(p.Decide("a", 0, 1), false, "tokens left: 0");
    }

    [Fact]
    public void ExtremeTimesDoNotOverflow()
    {
        IPolicy p = Make("bucket:999999/24h,999999");
        Expect(p.Decide("a", Ts("1970-01-01T00:00:00Z"), 1), true, "tokens left: 999998");
        Expect(p.Decide("a", Ts("9999-12-31T23:59:59Z"), 999999), true, "tokens left: 0");
    }

    [Theory]
    [InlineData("bucket:1/1s,1", 2)]
    [InlineData("bucket:5/1s,5", 6)]
    [InlineData("bucket:1/1ms,999999", 1000000)]
    public void CostAboveBurstMessage(string spec, long cost)
    {
        IPolicy p = Make(spec);
        Decision d = p.Decide("a", 0, cost);
        Assert.False(d.Allowed);
        Assert.StartsWith("cost " + cost + " exceeds burst ", d.Detail);
    }
}
