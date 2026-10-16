using Ratesim.Core;

namespace Ratesim.Tests;

public class ExplainOnlyTests
{
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

    private const string AlwaysDeniedLog =
        "2024-05-01T10:00:00.000Z alice 2\n"
        + "2024-05-01T10:00:01.000Z alice 2\n"
        + "2024-05-01T10:00:02.000Z alice 2\n"
        + "2024-05-01T10:00:03.000Z alice 2\n"
        + "2024-05-01T10:00:04.000Z alice 2\n"
        + "2024-05-01T10:00:05.000Z alice 2\n"
        + "2024-05-01T10:00:06.000Z alice 2\n"
        + "2024-05-01T10:00:07.000Z alice 2\n"
        + "2024-05-01T10:00:08.000Z alice 2\n"
        + "2024-05-01T10:00:09.000Z alice 2\n";

    private static (int Code, string Out, string Err) Run(string[] args, string stdin)
    {
        var stdout = new StringWriter { NewLine = "\n" };
        var stderr = new StringWriter { NewLine = "\n" };
        int code = Cli.Run(args, new StringReader(stdin), stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    private static string SuccessOutput(string[] args, string stdin)
    {
        (int code, string output, string error) = Run(args, stdin);
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, error);
        Assert.DoesNotContain('\r', output);
        Assert.DoesNotContain('\uFEFF', output);
        Assert.EndsWith("\n", output);
        return output;
    }

    private static void AssertError(string[] args, string message)
    {
        (int code, string output, string error) = Run(args, string.Empty);
        Assert.Equal(2, code);
        Assert.Equal(string.Empty, output);
        Assert.Equal("ratesim: " + message + "\n", error);
        Assert.DoesNotContain('\r', error);
    }

    [Fact]
    public void ExplainOnlyDeniedMatchesWorkedExample()
    {
        string output = SuccessOutput(
            new[] { "explain", "--only", "denied", "--policy", "bucket:1/5s,2", "-" },
            ApiLog);

        Assert.Equal(
            "5 2024-05-01T10:00:02.000Z alice denied (tokens left: 0)\n"
            + "8 2024-05-01T10:00:10.200Z alice denied (tokens left: 0)\n"
            + "10 2024-05-01T10:00:11.000Z alice denied (tokens left: 0)\n"
            + "11 2024-05-01T10:00:12.000Z bob denied (cost 3 exceeds burst 2)\n"
            + "6 allowed, 4 denied\n",
            output);
    }

    [Fact]
    public void ExplainOnlyAllowedMatchesWorkedExample()
    {
        string output = SuccessOutput(
            new[] { "explain", "--only", "allowed", "--policy", "fixed:3/10s", "-" },
            ApiLog);

        Assert.Equal(
            "2 2024-05-01T10:00:00.000Z alice allowed (1 of 3 used)\n"
            + "3 2024-05-01T10:00:01.000Z alice allowed (2 of 3 used)\n"
            + "4 2024-05-01T10:00:01.500Z bob allowed (1 of 3 used)\n"
            + "6 2024-05-01T10:00:09.900Z alice allowed (3 of 3 used)\n"
            + "7 2024-05-01T10:00:10.000Z alice allowed (1 of 3 used)\n"
            + "8 2024-05-01T10:00:10.200Z alice allowed (2 of 3 used)\n"
            + "9 2024-05-01T10:00:10.300Z bob allowed (1 of 3 used)\n"
            + "10 2024-05-01T10:00:11.000Z alice allowed (3 of 3 used)\n"
            + "8 allowed, 2 denied\n",
            output);
    }

    [Fact]
    public void ExplainWithoutOnlyStillPrintsEveryRequest()
    {
        string output = SuccessOutput(
            new[] { "explain", "--policy", "bucket:1/5s,2", "-" },
            ApiLog);

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
    public void ExplainOnlySupportsEqualsPlacementAndLastValueWins()
    {
        string denied = SuccessOutput(
            new[] { "explain", "--only", "denied", "--policy", "fixed:3/10s", "-" },
            ApiLog);
        string allowed = SuccessOutput(
            new[] { "explain", "--only", "allowed", "--policy", "fixed:3/10s", "-" },
            ApiLog);

        Assert.Equal(
            denied,
            SuccessOutput(
                new[] { "explain", "--only=denied", "--policy", "fixed:3/10s", "-" },
                ApiLog));
        Assert.Equal(
            allowed,
            SuccessOutput(
                new[] { "explain", "--policy", "fixed:3/10s", "--only", "allowed", "-" },
                ApiLog));
        Assert.Equal(
            denied,
            SuccessOutput(
                new[] { "explain", "--only", "allowed", "--policy", "fixed:3/10s", "--only=denied", "-" },
                ApiLog));
        Assert.Equal(
            allowed,
            SuccessOutput(
                new[] { "explain", "--only=denied", "--policy", "fixed:3/10s", "-", "--only", "allowed" },
                ApiLog));
        Assert.Equal(
            denied,
            SuccessOutput(
                new[] { "explain", "--only", "denied", "--policy", "fixed:3/10s", "--only=allowed", "--only", "denied", "-" },
                ApiLog));
    }

    [Fact]
    public void ExplainOnlyFilteringPreservesScopeOrderAndTotals()
    {
        foreach (string policy in new[] { "bucket:1/5s,2", "fixed:3/10s" })
        {
            foreach (string scope in new[] { "key", "all" })
            {
                var common = new[] { "explain", "--policy", policy, "--scope", scope, "-" };
                string unfiltered = SuccessOutput(common, ApiLog);
                string[] lines = unfiltered.Split('\n');
                string totals = lines[^2];

                foreach (string decision in new[] { "allowed", "denied" })
                {
                    string[] selectedLines = lines
                        .Take(lines.Length - 2)
                        .Where(line => line.Contains(" " + decision + " (", StringComparison.Ordinal))
                        .ToArray();
                    string expected = selectedLines.Length == 0
                        ? totals + "\n"
                        : string.Join("\n", selectedLines) + "\n" + totals + "\n";
                    string[] filteredArgs = { "explain", "--only", decision, "--policy", policy, "--scope", scope, "-" };

                    Assert.Equal(expected, SuccessOutput(filteredArgs, ApiLog));
                }
            }
        }
    }

    [Fact]
    public void ExplainOnlyWithNoMatchesPrintsOnlyFullLogTotals()
    {
        Assert.Equal(
            "10 allowed, 0 denied\n",
            SuccessOutput(
                new[] { "explain", "--only", "denied", "--policy", "fixed:100/10s", "-" },
                ApiLog));
        Assert.Equal(
            "0 allowed, 10 denied\n",
            SuccessOutput(
                new[] { "explain", "--only", "allowed", "--policy", "bucket:1/5s,1", "-" },
                AlwaysDeniedLog));
    }

    [Theory]
    [InlineData("allowed")]
    [InlineData("denied")]
    public void ExplainOnlyOnEmptyLogPrintsTotals(string decision)
    {
        Assert.Equal(
            "0 allowed, 0 denied\n",
            SuccessOutput(
                new[] { "explain", "--only", decision, "--policy", "fixed:1/1s", "-" },
                string.Empty));
    }

    [Theory]
    [InlineData("x", false)]
    [InlineData("Denied", false)]
    [InlineData("ALLOWED", false)]
    [InlineData("", false)]
    [InlineData("x", true)]
    [InlineData("Denied", true)]
    [InlineData("ALLOWED", true)]
    [InlineData("", true)]
    public void InvalidOnlyValuesAreRejectedAsTyped(string value, bool equalsForm)
    {
        string onlyArgument = equalsForm ? "--only=" + value : "--only";
        string[] args = equalsForm
            ? new[] { "explain", onlyArgument, "--policy", "fixed:3/10s", "-" }
            : new[] { "explain", onlyArgument, value, "--policy", "fixed:3/10s", "-" };

        AssertError(args, $"invalid --only \"{value}\" (expected allowed or denied)");
    }

    [Fact]
    public void ExplainOnlyWithoutAValueIsRejected()
    {
        AssertError(
            new[] { "explain", "--policy", "fixed:3/10s", "-", "--only" },
            "--only needs a value");
    }

    [Theory]
    [InlineData("run", "--only")]
    [InlineData("run", "--only=denied")]
    [InlineData("suggest", "--only")]
    [InlineData("suggest", "--only=denied")]
    public void OtherCommandsRejectOnlyWithOriginalOptionSpelling(string command, string option)
    {
        string[] args = command == "suggest"
            ? new[] { command, option, "denied", "--kind", "fixed", "--window", "10s", "-" }
            : new[] { command, option, "denied", "--policy", "fixed:3/10s", "-" };
        string expectedOption = option == "--only" ? "--only" : "--only=denied";

        AssertError(args, $"unknown option \"{expectedOption}\"");
    }

    [Fact]
    public void ExplainOptionErrorsAreReportedInArgumentOrder()
    {
        AssertError(
            new[] { "explain", "--only", "x", "--policy", "bad", "-" },
            "invalid --only \"x\" (expected allowed or denied)");
        AssertError(
            new[] { "explain", "--policy", "bad", "--only", "x", "-" },
            "invalid policy \"bad\" (expected fixed:LIMIT/WINDOW, sliding:LIMIT/WINDOW or bucket:RATE/WINDOW,BURST)");
    }

    [Fact]
    public void ExplainOnlyInvalidFirstOccurrenceIsNotOverwrittenByALaterValidValue()
    {
        AssertError(
            new[] { "explain", "--only", "x", "--only", "allowed", "--policy", "fixed:3/10s", "-" },
            "invalid --only \"x\" (expected allowed or denied)");
    }

    [Fact]
    public void OneArgumentReportExplainTextStillPrintsAllEntries()
    {
        Assert.True(PolicySpec.TryParse("bucket:1/5s,2", out IPolicy policy));
        var requests = RequestLog.Parse("<stdin>", ApiLog).Requests;
        IReadOnlyList<ExplainEntry> entries = Simulator.Explain(requests, policy, false);

        Assert.Equal(
            SuccessOutput(new[] { "explain", "--policy", "bucket:1/5s,2", "-" }, ApiLog),
            Report.ExplainText(entries));
    }
}
