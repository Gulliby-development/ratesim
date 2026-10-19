using Ratesim.Core;

namespace Ratesim.Tests;

public class OptionsTests
{
    private const string PolicyHelp = "(expected fixed:LIMIT/WINDOW, sliding:LIMIT/WINDOW or bucket:RATE/WINDOW,BURST)";
    private const string MaxHelp = "(expected a percentage from 0 to 100 with at most one decimal)";

    private static string[] Split(string args) =>
        args.Length == 0 ? Array.Empty<string>() : args.Split(' ');

    private static string Fail(string args) =>
        Assert.Throws<RatesimException>(() => Options.Parse(Split(args))).Message;

    [Theory]
    [InlineData("", "missing command (run, explain or suggest)")]
    [InlineData("foo", "unknown command \"foo\"")]
    [InlineData("--help run", "unknown option \"--help\"")]
    [InlineData("run --help", "unknown option \"--help\"")]
    [InlineData("run --policy", "--policy needs a value")]
    [InlineData("explain --policy", "--policy needs a value")]
    [InlineData("suggest --kind fixed --window", "--window needs a value")]
    [InlineData("suggest --kind", "--kind needs a value")]
    [InlineData("run a.log --scope", "--scope needs a value")]
    [InlineData("run --max-denied", "--max-denied needs a value")]
    [InlineData("suggest --max-denied", "--max-denied needs a value")]
    [InlineData("suggest --policy fixed:1/1s a.log", "unknown option \"--policy\"")]
    [InlineData("run --kind fixed a.log", "unknown option \"--kind\"")]
    [InlineData("run --window 1s a.log", "unknown option \"--window\"")]
    [InlineData("explain --kind fixed a.log", "unknown option \"--kind\"")]
    [InlineData("explain --window 1s a.log", "unknown option \"--window\"")]
    [InlineData("explain --max-denied 5 a.log", "unknown option \"--max-denied\"")]
    [InlineData("explain --max-denied", "unknown option \"--max-denied\"")]
    [InlineData("run --kind", "unknown option \"--kind\"")]
    [InlineData("run --verbose a.log", "unknown option \"--verbose\"")]
    [InlineData("run --verbose=1 a.log", "unknown option \"--verbose=1\"")]
    [InlineData("run -x a.log", "unknown option \"-x\"")]
    [InlineData("run --policy fixed:1/1s a.log b.log", "too many arguments")]
    [InlineData("run - -", "too many arguments")]
    [InlineData("run --scope x --verbose", "invalid --scope \"x\" (expected key or all)")]
    [InlineData("run --policy -x a.log", "invalid policy \"-x\" " + PolicyHelp)]
    public void ParseReportsFirstProblem(string args, string message)
    {
        Assert.Equal(message, Fail(args));
    }

    [Theory]
    [InlineData("--policy", "fixed:0/1s", "invalid policy \"fixed:0/1s\" " + PolicyHelp)]
    [InlineData("--policy", "bucket:2/1s", "invalid policy \"bucket:2/1s\" " + PolicyHelp)]
    [InlineData("--scope", "everything", "invalid --scope \"everything\" (expected key or all)")]
    [InlineData("--max-denied", "100.5", "invalid --max-denied \"100.5\" " + MaxHelp)]
    [InlineData("--max-denied", "-1", "invalid --max-denied \"-1\" " + MaxHelp)]
    [InlineData("--max-denied", "2.55", "invalid --max-denied \"2.55\" " + MaxHelp)]
    [InlineData("--max-denied", "02", "invalid --max-denied \"02\" " + MaxHelp)]
    [InlineData("--max-denied", ".5", "invalid --max-denied \".5\" " + MaxHelp)]
    [InlineData("--max-denied", "2.", "invalid --max-denied \"2.\" " + MaxHelp)]
    [InlineData("--max-denied", "101", "invalid --max-denied \"101\" " + MaxHelp)]
    [InlineData("--max-denied", "", "invalid --max-denied \"\" " + MaxHelp)]
    public void InvalidRunValues(string option, string value, string message)
    {
        Assert.Equal(message, Assert.Throws<RatesimException>(() => Options.Parse(new[] { "run", option, value })).Message);
        Assert.Equal(message, Assert.Throws<RatesimException>(() => Options.Parse(new[] { "run", option + "=" + value })).Message);
    }

    [Theory]
    [InlineData("--kind", "bucket", "invalid --kind \"bucket\" (expected fixed or sliding)")]
    [InlineData("--window", "2d", "invalid --window \"2d\" (expected a duration such as 500ms, 10s, 5m or 1h)")]
    [InlineData("--window", "25h", "invalid --window \"25h\" (expected a duration such as 500ms, 10s, 5m or 1h)")]
    [InlineData("--window", "0s", "invalid --window \"0s\" (expected a duration such as 500ms, 10s, 5m or 1h)")]
    public void InvalidSuggestValues(string option, string value, string message)
    {
        Assert.Equal(message, Assert.Throws<RatesimException>(() => Options.Parse(new[] { "suggest", option, value })).Message);
        Assert.Equal(message, Assert.Throws<RatesimException>(() => Options.Parse(new[] { "suggest", option + "=" + value })).Message);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("0.5", 5)]
    [InlineData("2", 20)]
    [InlineData("2.5", 25)]
    [InlineData("100", 1000)]
    [InlineData("100.0", 1000)]
    public void AcceptedMaxDenied(string value, int tenths)
    {
        var a = Options.Parse(new[] { "run", "--policy", "fixed:1/1s", "--max-denied", value, "a.log" });
        var b = Options.Parse(new[] { "run", "--policy", "fixed:1/1s", "--max-denied=" + value, "a.log" });
        Assert.True(a.MaxDeniedGiven);
        Assert.Equal(tenths, a.MaxDeniedTenths);
        Assert.Equal(tenths, b.MaxDeniedTenths);
    }

    [Fact]
    public void MaxDeniedDefaultsToNotGiven()
    {
        var o = Options.Parse(new[] { "run", "--policy", "fixed:1/1s", "a.log" });
        Assert.False(o.MaxDeniedGiven);
        Assert.Equal(0, o.MaxDeniedTenths);
    }

    [Fact]
    public void HelpOnlyAsSingleArgument()
    {
        Assert.True(Options.Parse(new[] { "--help" }).Help);
    }

    [Fact]
    public void LastValueWins()
    {
        var o = Options.Parse(new[] { "run", "--policy", "fixed:1/1s", "--scope", "all", "--scope=key", "--max-denied", "1", "--max-denied", "3", "a.log" });
        Assert.False(o.SharedScope);
        Assert.Equal(30, o.MaxDeniedTenths);
        var p = Options.Parse(new[] { "run", "--policy", "fixed:1/1s", "--scope", "key", "--scope", "all", "a.log" });
        Assert.True(p.SharedScope);
    }

    [Fact]
    public void SuggestLastKindAndWindowWin()
    {
        var o = Options.Parse(new[] { "suggest", "--kind", "fixed", "--kind=sliding", "--window", "1s", "--window=2s", "a.log" });
        Assert.Equal("sliding", o.Kind);
        Assert.Equal("2s", o.WindowText);
        Assert.Equal(2000, o.WindowMs);
    }

    [Fact]
    public void OptionsAndLogInAnyOrder()
    {
        var o = Options.Parse(new[] { "run", "a.log", "--policy=fixed:1/1s", "--policy", "sliding:2/1s" });
        Assert.Equal("a.log", o.Log);
        Assert.Equal(new[] { "fixed:1/1s", "sliding:2/1s" }, o.Policies);
    }

    [Fact]
    public void DashIsStandardInputLog()
    {
        var o = Options.Parse(new[] { "run", "--policy", "fixed:1/1s", "-" });
        Assert.Equal("-", o.Log);
    }

    [Fact]
    public void ValueAfterOptionMayStartWithDash()
    {
        Assert.Equal("invalid --scope \"-\" (expected key or all)", Fail("run --scope -"));
    }

    [Theory]
    [InlineData("run --policy fixed:1/1s --policy fixed:1/1s", "missing LOG")]
    [InlineData("run missing.log", "run needs --policy")]
    [InlineData("explain missing.log", "explain needs exactly one --policy")]
    [InlineData("explain --policy fixed:1/1s --policy fixed:2/1s missing.log", "explain needs exactly one --policy")]
    [InlineData("suggest missing.log", "suggest needs --kind")]
    [InlineData("suggest --kind fixed missing.log", "suggest needs --window")]
    [InlineData("suggest --window 1s missing.log", "suggest needs --kind")]
    [InlineData("run --policy fixed:1/1s --policy fixed:1/1s missing.log", "duplicate policy \"fixed:1/1s\"")]
    [InlineData("run --policy fixed:1/1s --policy fixed:1/1s --policy sliding:1/1s missing.log", "duplicate policy \"fixed:1/1s\"")]
    public void PostLoopChecksInOrder(string args, string message)
    {
        Assert.Equal(message, Fail(args));
    }

    [Fact]
    public void EquivalentSpecsAreNotDuplicates()
    {
        var o = Options.Parse(new[] { "run", "--policy", "fixed:5/10s", "--policy", "fixed:5/10000ms", "a.log" });
        Assert.Equal(2, o.Policies.Count);
    }

    [Fact]
    public void ArgumentErrorBeatsPostLoopCheck()
    {
        Assert.Equal("unknown option \"--verbose\"", Fail("run --verbose"));
    }
    [Theory]
    [InlineData("run --policy fixed:1/1s -- -a.log", "-a.log")]
    [InlineData("run --policy fixed:1/1s -- --help", "--help")]
    [InlineData("run --policy fixed:1/1s -- --policy=fixed:2/1s", "--policy=fixed:2/1s")]
    public void ParseTreatsArgumentsAfterOptionsEndAsLog(string args, string log)
    {
        var options = Options.Parse(Split(args));
        Assert.Equal(log, options.Log);
        Assert.Equal(new[] { "fixed:1/1s" }, options.Policies);
    }

    [Theory]
    [InlineData("run --policy fixed:1/1s --", "missing LOG")]
    [InlineData("run --policy fixed:1/1s -- a.log b.log", "too many arguments")]
    [InlineData("run --policy fixed:1/1s a.log -- b.log", "too many arguments")]
    [InlineData("run --policy fixed:1/1s -- -- a.log", "too many arguments")]
    [InlineData("run --policy -- a.log", "invalid policy \"--\" " + PolicyHelp)]
    [InlineData("-- run", "unknown command \"--\"")]
    public void ParseReportsOptionsEndErrors(string args, string message)
    {
        Assert.Equal(message, Fail(args));
    }

    [Fact]
    public void SecondOptionsTerminatorIsLog()
    {
        var options = Options.Parse(Split("run --policy fixed:1/1s -- --"));
        Assert.Equal("--", options.Log);
    }
}
