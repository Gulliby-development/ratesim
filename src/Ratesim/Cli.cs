using System.Text;
using Ratesim.Core;

namespace Ratesim;

public static class Cli
{
    private const string Usage =
        "usage: ratesim run --policy SPEC [--policy SPEC]... [--max-denied PERCENT] [--scope key|all] [--format text|csv] LOG\n"
        + "       ratesim explain --policy SPEC [--only allowed|denied] [--scope key|all] LOG\n"
        + "       ratesim suggest --kind fixed|sliding --window DURATION [--max-denied PERCENT] [--scope key|all] LOG\n";

    public static int Run(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        try
        {
            Options options = Options.Parse(args);
            if (options.Help)
            {
                stdout.Write(Usage);
                return 0;
            }

            RequestLog log = ReadLog(options.Log!, stdin);
            return Execute(options, log.Requests, stdout);
        }
        catch (RatesimException ex)
        {
            stderr.Write("ratesim: " + ex.Message + "\n");
            return 2;
        }
    }

    private static int Execute(Options options, IReadOnlyList<Request> requests, TextWriter stdout)
    {
        string text;
        int code = 0;
        if (options.Command == "run")
        {
            var results = new List<PolicyResult>();
            foreach (string spec in options.Policies)
            {
                (int allowed, int denied) = Simulator.Run(requests, Build(spec), options.SharedScope);
                results.Add(new PolicyResult(spec, allowed, denied));
                if (options.MaxDeniedGiven && denied * 1000L > options.MaxDeniedTenths * (long)requests.Count)
                {
                    code = 1;
                }
            }

            text = options.Csv
                ? Report.RunCsv(results)
                : Report.RunText(requests.Count, Simulator.CountKeys(requests), results);
        }
        else if (options.Command == "explain")
        {
            text = Report.ExplainText(Simulator.Explain(requests, Build(options.Policies[0]), options.SharedScope), options.Only);
        }
        else
        {
            Suggestion suggestion = Simulator.Suggest(requests, options.Kind!, options.WindowText!, options.MaxDeniedTenths, options.SharedScope);
            text = Report.SuggestText(suggestion);
        }

        stdout.Write(text);
        return code;
    }

    private static IPolicy Build(string spec)
    {
        if (!PolicySpec.TryParse(spec, out IPolicy policy))
        {
            throw new RatesimException($"invalid policy \"{spec}\"");
        }

        return policy;
    }

    private static RequestLog ReadLog(string path, TextReader stdin)
    {
        if (path == "-")
        {
            return RequestLog.Parse("<stdin>", stdin.ReadToEnd());
        }

        string display = path.Replace('\\', '/');
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new RatesimException(display + ": cannot read the file");
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new RatesimException(display + ": input is not valid UTF-8");
        }

        return RequestLog.Parse(display, text);
    }
}
