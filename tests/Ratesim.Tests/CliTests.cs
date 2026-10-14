using System.Text;
using Xunit;

namespace Ratesim.Tests;

public class CliTests
{
    private const string PolicyHelp = "(expected fixed:LIMIT/WINDOW, sliding:LIMIT/WINDOW or bucket:RATE/WINDOW,BURST)";

    private static readonly string ApiLog = string.Join(
        "\n",
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
        "2024-05-01T10:00:12.000Z bob 3") + "\n";

    private static string[] Split(string args)
    {
        return args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static (int Code, string Out, string Err) Run(string args, string stdin = "")
    {
        return RunArray(Split(args), stdin);
    }

    private static (int Code, string Out, string Err) RunArray(string[] args, string stdin = "")
    {
        var stdout = new StringWriter { NewLine = "\n" };
        var stderr = new StringWriter { NewLine = "\n" };
        int code = Cli.Run(args, new StringReader(stdin), stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    private static void AssertError(string args, string stdin, string message)
    {
        (int code, string output, string error) = Run(args, stdin);
        Assert.Equal(2, code);
        Assert.Equal(string.Empty, output);
        Assert.Equal("ratesim: " + message + "\n", error);
        Assert.DoesNotContain('\r', error);
    }

    private static void WithTempDir(string testName, Action<string> body)
    {
        string dir = Path.Combine(Path.GetTempPath(), testName + "-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            body(dir);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void HelpAlonePrintsUsage()
    {
        (int code, string output, string error) = Run("--help");
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(
            "usage: ratesim run --policy SPEC [--policy SPEC]... [--max-denied PERCENT] [--scope key|all] LOG\n"
            + "       ratesim explain --policy SPEC [--scope key|all] LOG\n"
            + "       ratesim suggest --kind fixed|sliding --window DURATION [--max-denied PERCENT] [--scope key|all] LOG\n",
            output);
    }

    [Theory]
    [InlineData("run --help a.log")]
    [InlineData("--help run")]
    [InlineData("--help --help")]
    [InlineData("explain --policy fixed:1/1s --help a.log")]
    public void HelpElsewhereIsUnknownOption(string args)
    {
        AssertError(args, string.Empty, "unknown option \"--help\"");
    }

    [Theory]
    [InlineData("", "missing command (run, explain or suggest)")]
    [InlineData("foo", "unknown command \"foo\"")]
    [InlineData("run --policy", "--policy needs a value")]
    [InlineData("suggest --kind fixed --window", "--window needs a value")]
    [InlineData("run a.log --scope", "--scope needs a value")]
    [InlineData("run --max-denied", "--max-denied needs a value")]
    [InlineData("suggest --policy fixed:1/1s a.log", "unknown option \"--policy\"")]
    [InlineData("run --kind fixed a.log", "unknown option \"--kind\"")]
    [InlineData("explain --window 1s a.log", "unknown option \"--window\"")]
    [InlineData("explain --max-denied 5 a.log", "unknown option \"--max-denied\"")]
    [InlineData("run --verbose a.log", "unknown option \"--verbose\"")]
    [InlineData("run --verbose=1 a.log", "unknown option \"--verbose=1\"")]
    [InlineData("run --policy fixed:1/1s a.log b.log", "too many arguments")]
    [InlineData("run - -", "too many arguments")]
    [InlineData("run --scope x --verbose", "invalid --scope \"x\" (expected key or all)")]
    [InlineData("run --policy -x a.log", "invalid policy \"-x\" " + PolicyHelp)]
    public void ArgumentLoopErrors(string args, string message)
    {
        AssertError(args, string.Empty, message);
    }

    [Theory]
    [InlineData("run --policy fixed:0/1s a.log", "invalid policy \"fixed:0/1s\" " + PolicyHelp)]
    [InlineData("run --policy=fixed:0/1s a.log", "invalid policy \"fixed:0/1s\" " + PolicyHelp)]
    [InlineData("run --scope everything a.log", "invalid --scope \"everything\" (expected key or all)")]
    [InlineData("run --scope=everything a.log", "invalid --scope \"everything\" (expected key or all)")]
    [InlineData("suggest --kind bucket a.log", "invalid --kind \"bucket\" (expected fixed or sliding)")]
    [InlineData("suggest --kind=bucket a.log", "invalid --kind \"bucket\" (expected fixed or sliding)")]
    [InlineData("suggest --window 2d a.log", "invalid --window \"2d\" (expected a duration such as 500ms, 10s, 5m or 1h)")]
    [InlineData("suggest --window=2d a.log", "invalid --window \"2d\" (expected a duration such as 500ms, 10s, 5m or 1h)")]
    [InlineData("suggest --window 25h a.log", "invalid --window \"25h\" (expected a duration such as 500ms, 10s, 5m or 1h)")]
    [InlineData("suggest --window=25h a.log", "invalid --window \"25h\" (expected a duration such as 500ms, 10s, 5m or 1h)")]
    [InlineData("run --max-denied 100.5 a.log", "invalid --max-denied \"100.5\" (expected a percentage from 0 to 100 with at most one decimal)")]
    [InlineData("run --max-denied=100.5 a.log", "invalid --max-denied \"100.5\" (expected a percentage from 0 to 100 with at most one decimal)")]
    [InlineData("run --max-denied -1 a.log", "invalid --max-denied \"-1\" (expected a percentage from 0 to 100 with at most one decimal)")]
    [InlineData("run --max-denied=-1 a.log", "invalid --max-denied \"-1\" (expected a percentage from 0 to 100 with at most one decimal)")]
    [InlineData("suggest --max-denied 2.55 a.log", "invalid --max-denied \"2.55\" (expected a percentage from 0 to 100 with at most one decimal)")]
    [InlineData("suggest --max-denied=2.55 a.log", "invalid --max-denied \"2.55\" (expected a percentage from 0 to 100 with at most one decimal)")]
    [InlineData("run --max-denied 02 a.log", "invalid --max-denied \"02\" (expected a percentage from 0 to 100 with at most one decimal)")]
    [InlineData("run --max-denied=02 a.log", "invalid --max-denied \"02\" (expected a percentage from 0 to 100 with at most one decimal)")]
    public void InvalidOptionValues(string args, string message)
    {
        AssertError(args, string.Empty, message);
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("2", 1)]
    [InlineData("2.5", 1)]
    [InlineData("100", 0)]
    public void MaxDeniedValuesAccepted(string value, int expectedCode)
    {
        (int code, _, string error) = Run("run --policy fixed:3/10s --max-denied " + value + " -", ApiLog);
        Assert.Equal(expectedCode, code);
        Assert.Equal(string.Empty, error);
        (int code2, _, _) = Run("run --policy fixed:3/10s --max-denied=" + value + " -", ApiLog);
        Assert.Equal(expectedCode, code2);
    }

    [Fact]
    public void LastScopeWins()
    {
        (int code, string output, string error) = Run("run --policy fixed:3/10s --scope all --scope key -", ApiLog);
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(
            "10 requests from 2 keys\n"
            + "policy       allowed  denied  denied%\n"
            + "fixed:3/10s        8       2    20.0%\n",
            output);
    }

    [Theory]
    [InlineData("run --policy fixed:1/1s --policy fixed:1/1s", "missing LOG")]
    [InlineData("run missing.log", "run needs --policy")]
    [InlineData("explain missing.log", "explain needs exactly one --policy")]
    [InlineData("explain --policy fixed:1/1s --policy fixed:2/1s missing.log", "explain needs exactly one --policy")]
    [InlineData("suggest missing.log", "suggest needs --kind")]
    [InlineData("suggest --kind fixed missing.log", "suggest needs --window")]
    [InlineData("run --policy fixed:1/1s --policy fixed:1/1s missing.log", "duplicate policy \"fixed:1/1s\"")]
    [InlineData("run --policy fixed:1/1s missing.log", "missing.log: cannot read the file")]
    public void PostLoopErrors(string args, string message)
    {
        AssertError(args, string.Empty, message);
    }

    [Fact]
    public void RunWorkedExample()
    {
        (int code, string output, string error) = Run("run --policy fixed:3/10s --policy sliding:3/10s --policy bucket:1/5s,2 -", ApiLog);
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(
            "10 requests from 2 keys\n"
            + "policy         allowed  denied  denied%\n"
            + "fixed:3/10s          8       2    20.0%\n"
            + "sliding:3/10s        7       3    30.0%\n"
            + "bucket:1/5s,2        6       4    40.0%\n",
            output);
    }

    [Fact]
    public void ExplainBucketWorkedExample()
    {
        (int code, string output, string error) = Run("explain --policy bucket:1/5s,2 -", ApiLog);
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(
            "2 2024-05-01T10:00:00.000Z alice allowed (tokens left: 1)\n"
            + "3 2024-05-01T10:00:01.000Z alice allowed (tokens left: 0)\n"
            + "4 2024-05-01T10:00:01.500Z bob allowed (tokens left: 1)\n"
            + "5 2024-05-01T10:00:02.000Z alice denied (tokens left: 0)\n"
            + "6 2024-05-01T10:00:09.900Z alice allowed (tokens left: 0)\n"
            + "7 2024-05-01T10:00:10.000Z alice allowed (tokens left: 0)\n"
            + "8 2024-05-01T10:00:10.200Z alice denied (tokens left: 0)\n"
            + "9 2024-05-01T10:00:10.300Z bob allowed (tokens left: 1)\n"
            + "10 2024-05-01T10:00:11.000Z alice denied (tokens left: 0)\n"
            + "11 2024-05-01T10:00:12.000Z bob denied (cost 3 exceeds burst 2)\n"
            + "6 allowed, 4 denied\n",
            output);
    }

    [Fact]
    public void ExplainSlidingEndsWithSummary()
    {
        (int code, string output, _) = Run("explain --policy sliding:3/10s -", ApiLog);
        Assert.Equal(0, code);
        string[] lines = output.Split('\n');
        Assert.Equal("7 allowed, 3 denied", lines[^2]);
        Assert.Equal(string.Empty, lines[^1]);
    }

    [Fact]
    public void ExplainFixedLines()
    {
        (int code, string output, _) = Run("explain --policy fixed:3/10s -", ApiLog);
        Assert.Equal(0, code);
        string[] lines = output.Split('\n');
        Assert.Equal("2 2024-05-01T10:00:00.000Z alice allowed (1 of 3 used)", lines[0]);
        Assert.Equal("5 2024-05-01T10:00:02.000Z alice denied (2 of 3 used)", lines[3]);
        Assert.Equal("8 allowed, 2 denied", lines[^2]);
    }

    [Theory]
    [InlineData("--kind fixed --window 10s", "suggested: fixed:5/10s\ndenied 0 of 10 requests (0.0%)\n")]
    [InlineData("--kind fixed --window 10s --max-denied 20", "suggested: fixed:3/10s\ndenied 2 of 10 requests (20.0%)\n")]
    [InlineData("--kind=fixed --window=10s --max-denied=20", "suggested: fixed:3/10s\ndenied 2 of 10 requests (20.0%)\n")]
    public void SuggestOutputs(string args, string expected)
    {
        (int code, string output, string error) = Run("suggest " + args + " -", ApiLog);
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData("10", 1)]
    [InlineData("19.9", 1)]
    [InlineData("20", 0)]
    [InlineData("20.0", 0)]
    [InlineData("100", 0)]
    public void MaxDeniedExitTable(string value, int expectedCode)
    {
        (int code, string output, string error) = Run("run --policy fixed:3/10s --max-denied " + value + " -", ApiLog);
        Assert.Equal(expectedCode, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(
            "10 requests from 2 keys\n"
            + "policy       allowed  denied  denied%\n"
            + "fixed:3/10s        8       2    20.0%\n",
            output);
    }

    [Fact]
    public void MaxDeniedWithTwoPoliciesExitsOneWhenAnyExceeds()
    {
        (int code, _, _) = Run("run --policy fixed:3/10s --policy bucket:1/5s,2 --max-denied 30 -", ApiLog);
        Assert.Equal(1, code);
        (int code2, _, _) = Run("run --policy fixed:3/10s --policy sliding:3/10s --max-denied 30 -", ApiLog);
        Assert.Equal(0, code2);
    }

    [Fact]
    public void ScopeAllSharesState()
    {
        (int code, string output, _) = Run("run --policy fixed:3/10s --scope all -", ApiLog);
        Assert.Equal(0, code);
        Assert.Equal(
            "10 requests from 2 keys\n"
            + "policy       allowed  denied  denied%\n"
            + "fixed:3/10s        6       4    40.0%\n",
            output);
    }

    [Fact]
    public void OneRequestSingularForms()
    {
        (int code, string output, string error) = Run("run --policy fixed:1/1s -", "2024-05-01T10:00:00Z a\n");
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(
            "1 request from 1 key\n"
            + "policy      allowed  denied  denied%\n"
            + "fixed:1/1s        1       0     0.0%\n",
            output);
    }

    [Fact]
    public void SuggestOneRequestLog()
    {
        (int code, string output, _) = Run("suggest --kind sliding --window 1s -", "2024-05-01T10:00:00Z a\n");
        Assert.Equal(0, code);
        Assert.Equal("suggested: sliding:1/1s\ndenied 0 of 1 request (0.0%)\n", output);
    }

    [Theory]
    [InlineData("")]
    [InlineData("# only a comment\n\n")]
    public void EmptyLogRun(string log)
    {
        (int code, string output, string error) = Run("run --policy fixed:1/1s -", log);
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(
            "0 requests from 0 keys\n"
            + "policy      allowed  denied  denied%\n"
            + "fixed:1/1s        0       0     0.0%\n",
            output);
    }

    [Fact]
    public void EmptyLogExplainAndSuggest()
    {
        (int code, string output, _) = Run("explain --policy fixed:1/1s -", string.Empty);
        Assert.Equal(0, code);
        Assert.Equal("0 allowed, 0 denied\n", output);
        (int code2, string output2, _) = Run("suggest --kind fixed --window 10s -", string.Empty);
        Assert.Equal(0, code2);
        Assert.Equal("suggested: fixed:1/10s\ndenied 0 of 0 requests (0.0%)\n", output2);
    }

    [Fact]
    public void BomAndCrlfStdin()
    {
        string log = "\uFEFF2024-05-01T10:00:00Z a\r\n2024-05-01T10:00:01Z a\r\n";
        (int code, string output, string error) = Run("explain --policy fixed:1/10s -", log);
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(
            "1 2024-05-01T10:00:00Z a allowed (1 of 1 used)\n"
            + "2 2024-05-01T10:00:01Z a denied (1 of 1 used)\n"
            + "1 allowed, 1 denied\n",
            output);
    }

    [Fact]
    public void BlankCommentAndTabLinesKeepLineNumbers()
    {
        (int code, string output, _) = Run("explain --policy fixed:1/10s -", "\n   \n # c\n\t2024-05-01T10:00:00Z\ta\t\n");
        Assert.Equal(0, code);
        Assert.Equal("4 2024-05-01T10:00:00Z a allowed (1 of 1 used)\n1 allowed, 0 denied\n", output);
    }

    [Theory]
    [InlineData("2024-05-01T10:00:00Z", "<stdin>:1: expected TIME KEY [COST]")]
    [InlineData("# c\n2024-05-01T10:00:00Z a 1 x", "<stdin>:2: expected TIME KEY [COST]")]
    [InlineData("2024-02-30T10:00:00Z alice", "<stdin>:1: invalid time \"2024-02-30T10:00:00Z\"")]
    [InlineData("bad alice 0", "<stdin>:1: invalid time \"bad\"")]
    [InlineData("2024-05-01T10:00:00Z a 0", "<stdin>:1: invalid cost \"0\"")]
    [InlineData("2024-05-01T10:00:00Z a 01", "<stdin>:1: invalid cost \"01\"")]
    [InlineData("2024-05-01T10:00:00Z a 1000000", "<stdin>:1: invalid cost \"1000000\"")]
    [InlineData("2024-05-01T10:00:05Z a\n2024-05-01T10:00:06Z a\n2024-05-01T10:00:01Z a", "<stdin>:3: time goes backwards")]
    [InlineData("2024-05-01T10:00:00Z a\nbad b\nworse c", "<stdin>:2: invalid time \"bad\"")]
    public void StdinLogErrors(string log, string message)
    {
        AssertError("run --policy fixed:1/1s -", log, message);
    }

    [Fact]
    public void EqualTimesAreAccepted()
    {
        (int code, _, _) = Run("run --policy fixed:1/1s -", "2024-05-01T10:00:00Z a\n2024-05-01T10:00:00Z a\n");
        Assert.Equal(0, code);
    }

    [Fact]
    public void FileGivesSameOutputAsStdin()
    {
        WithTempDir(nameof(FileGivesSameOutputAsStdin), dir =>
        {
            string path = Path.Combine(dir, "api.log");
            File.WriteAllText(path, ApiLog, new UTF8Encoding(false));
            (int code, string fileOut, string fileErr) = RunArray(new[] { "run", "--policy", "fixed:3/10s", path });
            (int code2, string stdinOut, _) = Run("run --policy fixed:3/10s -", ApiLog);
            Assert.Equal(0, code);
            Assert.Equal(code2, code);
            Assert.Equal(string.Empty, fileErr);
            Assert.Equal(stdinOut, fileOut);
        });
    }

    [Fact]
    public void MissingFileShowsSlashes()
    {
        WithTempDir(nameof(MissingFileShowsSlashes), dir =>
        {
            string path = dir + "\\nothere\\x.log";
            (int code, string output, string error) = RunArray(new[] { "run", "--policy", "fixed:1/1s", path });
            Assert.Equal(2, code);
            Assert.Equal(string.Empty, output);
            Assert.Equal("ratesim: " + path.Replace('\\', '/') + ": cannot read the file\n", error);
        });
    }

    [Fact]
    public void DirectoryCannotBeRead()
    {
        WithTempDir(nameof(DirectoryCannotBeRead), dir =>
        {
            (int code, string output, string error) = RunArray(new[] { "run", "--policy", "fixed:1/1s", dir });
            Assert.Equal(2, code);
            Assert.Equal(string.Empty, output);
            Assert.Equal("ratesim: " + dir.Replace('\\', '/') + ": cannot read the file\n", error);
        });
    }

    [Fact]
    public void InvalidUtf8File()
    {
        WithTempDir(nameof(InvalidUtf8File), dir =>
        {
            string path = Path.Combine(dir, "bad.log");
            byte[] bytes = { 0xFF, 0xFE, 0x41 };
            File.WriteAllBytes(path, bytes);
            (int code, string output, string error) = RunArray(new[] { "run", "--policy", "fixed:1/1s", path });
            Assert.Equal(2, code);
            Assert.Equal(string.Empty, output);
            Assert.Equal("ratesim: " + path.Replace('\\', '/') + ": input is not valid UTF-8\n", error);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        });
    }

    [Fact]
    public void FileSyntaxErrorUsesPathPrefixAndKeepsBytes()
    {
        WithTempDir(nameof(FileSyntaxErrorUsesPathPrefixAndKeepsBytes), dir =>
        {
            string path = Path.Combine(dir, "syntax.log");
            byte[] bytes = Encoding.UTF8.GetBytes("2024-05-01T10:00:00Z a\nbad b\n");
            File.WriteAllBytes(path, bytes);
            (int code, string output, string error) = RunArray(new[] { "explain", "--policy", "fixed:1/1s", path });
            Assert.Equal(2, code);
            Assert.Equal(string.Empty, output);
            Assert.Equal("ratesim: " + path.Replace('\\', '/') + ":2: invalid time \"bad\"\n", error);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        });
    }

    [Fact]
    public void SuccessfulRunLeavesFileUnchanged()
    {
        WithTempDir(nameof(SuccessfulRunLeavesFileUnchanged), dir =>
        {
            string path = Path.Combine(dir, "api.log");
            byte[] bytes = Encoding.UTF8.GetBytes(ApiLog);
            File.WriteAllBytes(path, bytes);
            (int code, _, _) = RunArray(new[] { "suggest", "--kind", "fixed", "--window", "10s", path });
            Assert.Equal(0, code);
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Single(Directory.GetFiles(dir));
        });
    }
}
