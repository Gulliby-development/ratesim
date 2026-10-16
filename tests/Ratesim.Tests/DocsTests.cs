namespace Ratesim.Tests;

public class DocsTests
{
    private static string ReadReadme()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ratesim.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var text = File.ReadAllText(Path.Combine(dir.FullName, "README.md"));
        return text.Replace("\r\n", "\n");
    }

    private static string Lines(params string[] lines) => string.Join("\n", lines) + "\n";

    [Fact]
    public void ContainsUsageLines()
    {
        var expected = Lines(
            "usage: ratesim run --policy SPEC [--policy SPEC]... [--max-denied PERCENT] [--scope key|all] LOG",
            "       ratesim explain --policy SPEC [--only allowed|denied] [--scope key|all] LOG",
            "       ratesim suggest --kind fixed|sliding --window DURATION [--max-denied PERCENT] [--scope key|all] LOG");
        Assert.Contains(expected, ReadReadme());
    }

    [Fact]
    public void ContainsApiLogBlock()
    {
        var expected = Lines(
            "# api gateway sample",
            "2024-05-01T10:00:00.000Z alice",
            "2024-05-01T10:00:01.000Z alice",
            "2024-05-01T10:00:01.500Z bob",
            "2024-05-01T10:00:02.000Z alice 2",
            "2024-05-01T10:00:09.900Z alice",
            "2024-05-01T10:00:10.000Z alice",
            "2024-05-01T10:00:10.200Z alice",
            "2024-05-01T10:00:10.300Z bob",
            "2024-05-01T10:00:11.000Z alice",
            "2024-05-01T10:00:12.000Z bob 3");
        Assert.Contains(expected, ReadReadme());
    }

    [Fact]
    public void ContainsRunTable()
    {
        var expected = Lines(
            "10 requests from 2 keys",
            "policy         allowed  denied  denied%",
            "fixed:3/10s          8       2    20.0%",
            "sliding:3/10s        7       3    30.0%",
            "bucket:1/5s,2        6       4    40.0%");
        Assert.Contains(expected, ReadReadme());
    }

    [Fact]
    public void ContainsExplainBucketOutput()
    {
        var expected = Lines(
            "2 2024-05-01T10:00:00.000Z alice allowed (tokens left: 1)",
            "3 2024-05-01T10:00:01.000Z alice allowed (tokens left: 0)",
            "4 2024-05-01T10:00:01.500Z bob allowed (tokens left: 1)",
            "5 2024-05-01T10:00:02.000Z alice denied (tokens left: 0)",
            "6 2024-05-01T10:00:09.900Z alice allowed (tokens left: 0)",
            "7 2024-05-01T10:00:10.000Z alice allowed (tokens left: 0)",
            "8 2024-05-01T10:00:10.200Z alice denied (tokens left: 0)",
            "9 2024-05-01T10:00:10.300Z bob allowed (tokens left: 1)",
            "10 2024-05-01T10:00:11.000Z alice denied (tokens left: 0)",
            "11 2024-05-01T10:00:12.000Z bob denied (cost 3 exceeds burst 2)",
            "6 allowed, 4 denied");
        Assert.Contains(expected, ReadReadme());
    }

    [Fact]
    public void ContainsSuggestOutput()
    {
        var expected = Lines(
            "suggested: fixed:5/10s",
            "denied 0 of 10 requests (0.0%)");
        Assert.Contains(expected, ReadReadme());
    }

    [Fact]
    public void ContainsSuggestMaxDeniedOutput()
    {
        var expected = Lines(
            "suggested: fixed:3/10s",
            "denied 2 of 10 requests (20.0%)");
        Assert.Contains(expected, ReadReadme());
    }

    [Theory]
    [InlineData("## Install")]
    [InlineData("## Usage")]
    [InlineData("## Log format")]
    [InlineData("## Policies")]
    [InlineData("## Exit codes")]
    [InlineData("## Development")]
    public void ContainsHeading(string heading)
    {
        Assert.Contains("\n" + heading + "\n", "\n" + ReadReadme());
    }
}
