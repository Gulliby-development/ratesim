using Ratesim.Core;
using Xunit;

namespace Ratesim.Tests;

public class RunSortTests
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

    private static string[] Policies(string output)
    {
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(2).Select(line => line.Split(' ')[0]).ToArray();
    }

    [Fact]
    public void InputKeepsTheCommandLineOrder()
    {
        (int code, string output, _) = Run("run", "--policy", "bucket:1/5s,2", "--policy", "fixed:3/10s", "--policy", "sliding:3/10s", "-");
        Assert.Equal(0, code);
        Assert.Equal(new[] { "bucket:1/5s,2", "fixed:3/10s", "sliding:3/10s" }, Policies(output));
        (_, string explicitInput, _) = Run("run", "--policy", "bucket:1/5s,2", "--policy", "fixed:3/10s", "--policy", "sliding:3/10s", "--sort", "input", "-");
        Assert.Equal(output, explicitInput);
    }

    [Fact]
    public void DeniedPutsTheFewestDeniedFirst()
    {
        (_, string output, _) = Run("run", "--policy", "bucket:1/5s,2", "--policy", "sliding:3/10s", "--policy", "fixed:3/10s", "--sort", "denied", "-");
        Assert.Equal(new[] { "fixed:3/10s", "sliding:3/10s", "bucket:1/5s,2" }, Policies(output));
    }

    [Fact]
    public void AllowedPutsTheMostAllowedFirst()
    {
        (_, string output, _) = Run("run", "--policy", "bucket:1/5s,2", "--policy", "sliding:3/10s", "--policy", "fixed:3/10s", "--sort=allowed", "-");
        Assert.Equal(new[] { "fixed:3/10s", "sliding:3/10s", "bucket:1/5s,2" }, Policies(output));
    }

    [Fact]
    public void PolicySortsByTheOrdinalPolicyText()
    {
        (_, string output, _) = Run("run", "--policy", "sliding:3/10s", "--policy", "fixed:3/10s", "--policy", "bucket:1/5s,2", "--policy", "fixed:10/10s", "--sort", "policy", "-");
        Assert.Equal(new[] { "bucket:1/5s,2", "fixed:10/10s", "fixed:3/10s", "sliding:3/10s" }, Policies(output));
    }

    [Fact]
    public void TiesKeepTheCommandLineOrder()
    {
        (_, string output, _) = Run("run", "--policy", "fixed:100/10s", "--policy", "sliding:100/10s", "--policy", "fixed:50/1h", "--sort", "denied", "-");
        Assert.Equal(new[] { "fixed:100/10s", "sliding:100/10s", "fixed:50/1h" }, Policies(output));
    }

    [Fact]
    public void SortsTheCsvRowsToo()
    {
        (_, string output, _) = Run("run", "--policy", "bucket:1/5s,2", "--policy", "fixed:3/10s", "--format", "csv", "--sort", "denied", "-");
        Assert.Equal("policy,allowed,denied,denied_percent\nfixed:3/10s,8,2,20.0\n\"bucket:1/5s,2\",6,4,40.0\n", output);
    }

    [Fact]
    public void TheLastSortWinsAndTheExitCodeIgnoresIt()
    {
        (int code, string output, _) = Run("run", "--policy", "bucket:1/5s,2", "--policy", "fixed:3/10s", "--sort", "policy", "--sort", "denied", "--max-denied", "30", "-");
        Assert.Equal(1, code);
        Assert.Equal(new[] { "fixed:3/10s", "bucket:1/5s,2" }, Policies(output));
    }

    [Fact]
    public void RejectsAnUnknownSortKey()
    {
        (int code, string output, string error) = Run("run", "--policy", "fixed:3/10s", "--sort", "name", "-");
        Assert.Equal(2, code);
        Assert.Equal(string.Empty, output);
        Assert.Equal("ratesim: invalid --sort \"name\" (expected input, policy, allowed or denied)\n", error);
    }

    [Fact]
    public void OnlyRunAcceptsSort()
    {
        (int code, _, string error) = Run("explain", "--policy", "fixed:3/10s", "--sort", "denied", "-");
        Assert.Equal(2, code);
        Assert.Equal("ratesim: unknown option \"--sort\"\n", error);
    }
}
