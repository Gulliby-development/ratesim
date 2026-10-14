using Ratesim.Core;
using Xunit;

namespace Ratesim.Tests;

public class RequestLogTests
{
    private const string T0 = "2024-05-01T10:00:00Z";

    private static string Fail(string text)
    {
        var ex = Assert.Throws<RatesimException>(() => RequestLog.Parse("f.log", text));
        return ex.Message;
    }

    [Fact]
    public void BomAndCrlfParseLikePlainLf()
    {
        string lf = "# c\n" + T0 + " alice\n" + T0 + " bob 3\n";
        string crlf = "\uFEFF" + lf.Replace("\n", "\r\n");

        var expected = RequestLog.Parse("f.log", lf).Requests;
        var actual = RequestLog.Parse("f.log", crlf).Requests;

        Assert.Equal(expected, actual);
        Assert.Equal(2, actual.Count);
        Assert.Equal("alice", actual[0].Key);
        Assert.Equal(3, actual[1].Cost);
    }

    [Fact]
    public void SkippedLinesAreCountedInLineNumbers()
    {
        string text = "\n  \n# comment\n   # indented\n\t# tab comment\n" + T0 + " a\n\n" + T0 + " b\n";

        var requests = RequestLog.Parse("f.log", text).Requests;

        Assert.Equal(2, requests.Count);
        Assert.Equal(6, requests[0].Line);
        Assert.Equal(8, requests[1].Line);
    }

    [Theory]
    [InlineData("2024-05-01T10:00:00Z a")]
    [InlineData("2024-05-01T10:00:00Z\ta")]
    [InlineData("  2024-05-01T10:00:00Z   a  ")]
    [InlineData("\t2024-05-01T10:00:00Z \t a\t")]
    public void SpacesAndTabsSeparateAndSurroundFields(string line)
    {
        var requests = RequestLog.Parse("f.log", line).Requests;

        var request = Assert.Single(requests);
        Assert.Equal(1, request.Line);
        Assert.Equal(T0, request.TimeText);
        Assert.Equal(1714557600000L, request.Ms);
        Assert.Equal("a", request.Key);
        Assert.Equal(1, request.Cost);
    }

    [Theory]
    [InlineData(T0 + " a", 1)]
    [InlineData(T0 + " a 1", 1)]
    [InlineData(T0 + " a 3", 3)]
    [InlineData(T0 + " a 999999", 999999)]
    public void CostDefaultsToOneAndAcceptsExplicitValue(string line, long cost)
    {
        Assert.Equal(cost, Assert.Single(RequestLog.Parse("f.log", line).Requests).Cost);
    }

    [Fact]
    public void EqualConsecutiveTimesAreAccepted()
    {
        var requests = RequestLog.Parse("f.log", T0 + " a\n" + T0 + " b\n").Requests;

        Assert.Equal(2, requests.Count);
        Assert.Equal(requests[0].Ms, requests[1].Ms);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\uFEFF")]
    [InlineData("# only\n  # comments\n")]
    [InlineData("   \r\n\t\r\n")]
    public void EmptyOrCommentOnlyTextHasNoRequests(string text)
    {
        Assert.Empty(RequestLog.Parse("f.log", text).Requests);
    }

    [Theory]
    [InlineData("2024-05-01T10:00:00Z", 1714557600000L)]
    [InlineData("2024-05-01T10:00:00.2Z", 1714557600200L)]
    [InlineData("2024-05-01T10:00:00.25Z", 1714557600250L)]
    [InlineData("2024-05-01T10:00:00.250Z", 1714557600250L)]
    public void TimeTextIsKeptVerbatim(string time, long ms)
    {
        var request = Assert.Single(RequestLog.Parse("f.log", time + " a").Requests);

        Assert.Equal(time, request.TimeText);
        Assert.Equal(ms, request.Ms);
    }

    [Theory]
    [InlineData("2024-05-01T10:00:00Z", "f.log:1: expected TIME KEY [COST]")]
    [InlineData("2024-05-01T10:00:00Z a 1 x", "f.log:1: expected TIME KEY [COST]")]
    [InlineData("# c\n2024-05-01T10:00:00Z a 1 x", "f.log:2: expected TIME KEY [COST]")]
    [InlineData("2024-02-30T10:00:00Z alice", "f.log:1: invalid time \"2024-02-30T10:00:00Z\"")]
    [InlineData("bad alice 0", "f.log:1: invalid time \"bad\"")]
    [InlineData("2024-05-01T10:00:00Z a 0", "f.log:1: invalid cost \"0\"")]
    [InlineData("2024-05-01T10:00:00Z a 01", "f.log:1: invalid cost \"01\"")]
    [InlineData("2024-05-01T10:00:00Z a 1000000", "f.log:1: invalid cost \"1000000\"")]
    [InlineData("2024-05-01T10:00:00Z a x", "f.log:1: invalid cost \"x\"")]
    public void SyntaxErrorsHaveExactMessages(string text, string message)
    {
        Assert.Equal(message, Fail(text));
    }

    [Fact]
    public void TimeGoingBackwardsIsReportedOnItsLine()
    {
        string text = "2024-05-01T10:00:05Z a\n# c\n2024-05-01T10:00:04.999Z a\n";

        Assert.Equal("f.log:3: time goes backwards", Fail(text));
    }

    [Fact]
    public void FirstBadLineWins()
    {
        string text = T0 + " a\n" + T0 + "\n" + "bad a\n";

        Assert.Equal("f.log:2: expected TIME KEY [COST]", Fail(text));
    }

    [Fact]
    public void DisplayNameIsUsedInMessages()
    {
        var ex = Assert.Throws<RatesimException>(() => RequestLog.Parse("<stdin>", "x"));

        Assert.Equal("<stdin>:1: expected TIME KEY [COST]", ex.Message);
    }
}
