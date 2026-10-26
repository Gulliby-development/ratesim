using System.Text;
using Ratesim.Core;
using Xunit;

namespace Ratesim.Tests;

public class RunCsvTests
{
    private static readonly string ApiLog = string.Join(
        "\n",
        "2024-05-01T10:00:00.000Z alice",
        "2024-05-01T10:00:01.000Z alice",
        "2024-05-01T10:00:01.500Z bob",
        "2024-05-01T10:00:02.000Z alice 2",
        "2024-05-01T10:00:09.900Z alice",
        "2024-05-01T10:00:10.000Z alice",
        "2024-05-01T10:00:10.200Z alice",
        "2024-05-01T10:00:10.300Z bob",
        "2024-05-01T10:00:11.000Z alice",
        "2024-05-01T10:00:12.000Z bob 3") + "\n";

    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        var stdout = new StringWriter { NewLine = "\n" };
        var stderr = new StringWriter { NewLine = "\n" };
        int code = Cli.Run(args, new StringReader(ApiLog), stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void PrintsOneRowPerPolicy()
    {
        (int code, string output, string error) = Run("run", "--policy", "fixed:3/10s", "--policy", "sliding:3/10s", "--format", "csv", "-");
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal("policy,allowed,denied,denied_percent\nfixed:3/10s,8,2,20.0\nsliding:3/10s,7,3,30.0\n", output);
    }

    [Fact]
    public void QuotesAPolicyThatHoldsAComma()
    {
        (int code, string output, _) = Run("run", "--policy", "bucket:1/5s,2", "--format=csv", "-");
        Assert.Equal(0, code);
        Assert.Equal("policy,allowed,denied,denied_percent\n\"bucket:1/5s,2\",6,4,40.0\n", output);
    }

    [Fact]
    public void TextIsTheDefaultFormat()
    {
        (_, string explicitText, _) = Run("run", "--policy", "fixed:3/10s", "--format", "text", "-");
        (_, string implicitText, _) = Run("run", "--policy", "fixed:3/10s", "-");
        Assert.Equal("10 requests from 2 keys\npolicy       allowed  denied  denied%\nfixed:3/10s        8       2    20.0%\n", implicitText);
        Assert.Equal(implicitText, explicitText);
    }

    [Fact]
    public void KeepsTheMaxDeniedExitCode()
    {
        (int failing, string output, _) = Run("run", "--policy", "fixed:3/10s", "--max-denied", "10", "--format", "csv", "-");
        Assert.Equal(1, failing);
        Assert.Equal("policy,allowed,denied,denied_percent\nfixed:3/10s,8,2,20.0\n", output);
        (int passing, _, _) = Run("run", "--policy", "fixed:3/10s", "--max-denied", "20", "--format", "csv", "-");
        Assert.Equal(0, passing);
    }

    [Fact]
    public void RejectsUnknownFormats()
    {
        Assert.Equal((2, string.Empty, "ratesim: invalid --format \"xml\" (expected text or csv)\n"), Run("run", "--policy", "fixed:3/10s", "--format", "xml", "-"));
        Assert.Equal((2, string.Empty, "ratesim: invalid --format \"\" (expected text or csv)\n"), Run("run", "--policy", "fixed:3/10s", "--format=", "-"));
        Assert.Equal((2, string.Empty, "ratesim: --format needs a value\n"), Run("run", "--policy", "fixed:3/10s", "--format"));
    }

    [Fact]
    public void OnlyRunAcceptsTheFormat()
    {
        Assert.Equal((2, string.Empty, "ratesim: unknown option \"--format\"\n"), Run("explain", "--policy", "fixed:3/10s", "--format", "csv", "-"));
        Assert.Equal((2, string.Empty, "ratesim: unknown option \"--format\"\n"), Run("suggest", "--kind", "fixed", "--window", "10s", "--format", "csv", "-"));
    }

    [Fact]
    public void WritesCsvFieldsWithQuotesDoubled()
    {
        var results = new List<PolicyResult> { new("a\"b,c", 1, 2), new("plain", 0, 0) };
        Assert.Equal("policy,allowed,denied,denied_percent\n\"a\"\"b,c\",1,2,66.7\nplain,0,0,0.0\n", Report.RunCsv(results));
    }
}
