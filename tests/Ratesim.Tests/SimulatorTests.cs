using Ratesim.Core;

namespace Ratesim.Tests;

public class SimulatorTests
{
    private const string ApiLog =
        "# api gateway sample\n"
        + "2024-05-01T10:00:00.000Z alice\n"
        + "2024-05-01T10:00:01.000Z alice\n"
        + "2024-05-01T10:00:01.500Z bob\n"
        + "2024-05-01T10:00:02.000Z alice 2\n"
        + "2024-05-01T10:00:09.900Z alice\n"
        + "2024-05-01T10:00:10.000Z alice\n"
        + "2024-05-01T10:00:10.200Z alice\n"
        + "2024-05-01T10:00:10.300Z bob\n"
        + "2024-05-01T10:00:11.000Z alice\n"
        + "2024-05-01T10:00:12.000Z bob 3\n";

    private static IReadOnlyList<Request> Api() => RequestLog.Parse("api.log", ApiLog).Requests;

    private static IPolicy Policy(string spec)
    {
        Assert.True(PolicySpec.TryParse(spec, out IPolicy policy));
        return policy;
    }

    [Theory]
    [InlineData("fixed:3/10s", 8, 2)]
    [InlineData("sliding:3/10s", 7, 3)]
    [InlineData("bucket:1/5s,2", 6, 4)]
    public void RunCountsPerKey(string spec, int allowed, int denied)
    {
        var result = Simulator.Run(Api(), Policy(spec), false);
        Assert.Equal(allowed, result.Allowed);
        Assert.Equal(denied, result.Denied);
    }

    [Fact]
    public void SharedScopeCountsOneState()
    {
        var result = Simulator.Run(Api(), Policy("fixed:3/10s"), true);
        Assert.Equal(6, result.Allowed);
        Assert.Equal(4, result.Denied);
        Assert.Equal(2, Simulator.CountKeys(Api()));
    }

    [Fact]
    public void CountKeysOfEmptyLogIsZero()
    {
        Assert.Equal(0, Simulator.CountKeys(RequestLog.Parse("x", string.Empty).Requests));
    }

    [Fact]
    public void ExplainReturnsOneEntryPerRequest()
    {
        var entries = Simulator.Explain(Api(), Policy("sliding:3/10s"), false);
        Assert.Equal(10, entries.Count);
        Assert.Equal(7, entries.Count(e => e.Decision.Allowed));
    }

    [Fact]
    public void SuggestZeroDenialsFixed()
    {
        var s = Simulator.Suggest(Api(), "fixed", "10s", 0, false);
        Assert.Equal("fixed:5/10s", s.Spec);
        Assert.Equal(0, s.Denied);
        Assert.Equal(10, s.Total);
    }

    [Fact]
    public void SuggestWithTwentyPercent()
    {
        var s = Simulator.Suggest(Api(), "fixed", "10s", 200, false);
        Assert.Equal("fixed:3/10s", s.Spec);
        Assert.Equal(2, s.Denied);
    }

    [Fact]
    public void SuggestWithHundredPercentGivesLimitOne()
    {
        var s = Simulator.Suggest(Api(), "fixed", "10s", 1000, false);
        Assert.Equal("fixed:1/10s", s.Spec);
    }

    [Fact]
    public void SuggestOnEmptyLogGivesLimitOne()
    {
        var s = Simulator.Suggest(RequestLog.Parse("x", string.Empty).Requests, "fixed", "10s", 0, false);
        Assert.Equal("fixed:1/10s", s.Spec);
        Assert.Equal(0, s.Denied);
        Assert.Equal(0, s.Total);
    }

    [Fact]
    public void SuggestOnSingleRequestSliding()
    {
        var requests = RequestLog.Parse("x", "2024-05-01T10:00:00Z a\n").Requests;
        var s = Simulator.Suggest(requests, "sliding", "1s", 0, false);
        Assert.Equal("sliding:1/1s", s.Spec);
        Assert.Equal(1, s.Total);
    }

    [Fact]
    public void SuggestSharedScopeUsesAllRequests()
    {
        var s = Simulator.Suggest(Api(), "fixed", "10s", 0, true);
        Assert.Equal("fixed:7/10s", s.Spec);
        Assert.Equal(0, s.Denied);
    }

    [Fact]
    public void SuggestKeepsWindowTextAsGiven()
    {
        var s = Simulator.Suggest(Api(), "fixed", "10000ms", 0, false);
        Assert.Equal("fixed:5/10000ms", s.Spec);
    }

    [Theory]
    [InlineData(0, 0, "0.0%")]
    [InlineData(1, 16, "6.3%")]
    [InlineData(1, 3, "33.3%")]
    [InlineData(2, 3, "66.7%")]
    [InlineData(1, 8, "12.5%")]
    [InlineData(1, 2000, "0.1%")]
    [InlineData(2, 10, "20.0%")]
    [InlineData(16, 16, "100.0%")]
    [InlineData(0, 5, "0.0%")]
    public void PercentRoundsHalfUp(int denied, int total, string expected)
    {
        Assert.Equal(expected, Report.Percent(denied, total));
    }

    [Fact]
    public void RunTextAlignsColumns()
    {
        var results = new List<PolicyResult>
        {
            new("fixed:3/10s", 8, 2),
            new("sliding:3/10s", 7, 3),
            new("bucket:1/5s,2", 6, 4),
        };
        string expected = "10 requests from 2 keys\n"
            + "policy         allowed  denied  denied%\n"
            + "fixed:3/10s          8       2    20.0%\n"
            + "sliding:3/10s        7       3    30.0%\n"
            + "bucket:1/5s,2        6       4    40.0%\n";
        Assert.Equal(expected, Report.RunText(10, 2, results));
    }

    [Fact]
    public void RunTextUsesSingularForms()
    {
        var results = new List<PolicyResult> { new("fixed:1/1s", 1, 0) };
        string expected = "1 request from 1 key\n"
            + "policy      allowed  denied  denied%\n"
            + "fixed:1/1s        1       0     0.0%\n";
        Assert.Equal(expected, Report.RunText(1, 1, results));
    }

    [Fact]
    public void SuggestTextSingularAndPlural()
    {
        Assert.Equal(
            "suggested: sliding:1/1s\ndenied 0 of 1 request (0.0%)\n",
            Report.SuggestText(new Suggestion("sliding:1/1s", 0, 1)));
        Assert.Equal(
            "suggested: fixed:3/10s\ndenied 2 of 10 requests (20.0%)\n",
            Report.SuggestText(new Suggestion("fixed:3/10s", 2, 10)));
    }
}
