using Ratesim.Core;

namespace Ratesim;

public sealed class Options
{
    private Options(string command)
    {
        Command = command;
    }

    public string Command { get; }

    public bool Help { get; private set; }

    public List<string> Policies { get; } = new();

    public bool SharedScope { get; private set; }

    public string? Kind { get; private set; }

    public string? WindowText { get; private set; }

    public long WindowMs { get; private set; }

    public int MaxDeniedTenths { get; private set; }

    public bool MaxDeniedGiven { get; private set; }

    public string? Log { get; private set; }

    public static Options Parse(string[] args)
    {
        if (args.Length == 0)
        {
            throw new RatesimException("missing command (run, explain or suggest)");
        }

        if (args.Length == 1 && args[0] == "--help")
        {
            return new Options(string.Empty) { Help = true };
        }

        string command = args[0];
        if (command == "--help")
        {
            throw new RatesimException("unknown option \"--help\"");
        }

        if (command != "run" && command != "explain" && command != "suggest")
        {
            throw new RatesimException($"unknown command \"{command}\"");
        }

        var options = new Options(command);
        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            string name = arg;
            string? value = null;
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                int eq = arg.IndexOf('=');
                if (eq >= 0)
                {
                    name = arg.Substring(0, eq);
                    value = arg.Substring(eq + 1);
                }
            }

            if (TakesValue(command, name))
            {
                if (value is null)
                {
                    if (i + 1 >= args.Length)
                    {
                        throw new RatesimException($"{name} needs a value");
                    }

                    value = args[++i];
                }

                options.Apply(name, value);
            }
            else if (arg.StartsWith('-') && arg != "-")
            {
                throw new RatesimException($"unknown option \"{arg}\"");
            }
            else if (options.Log is not null)
            {
                throw new RatesimException("too many arguments");
            }
            else
            {
                options.Log = arg;
            }
        }

        options.Check();
        return options;
    }

    private static bool TakesValue(string command, string name)
    {
        switch (name)
        {
            case "--scope":
                return true;
            case "--policy":
                return command != "suggest";
            case "--max-denied":
                return command != "explain";
            case "--kind":
            case "--window":
                return command == "suggest";
            default:
                return false;
        }
    }

    private static bool TryParseTenths(string text, out int tenths)
    {
        tenths = 0;
        int dot = text.IndexOf('.');
        string whole = dot < 0 ? text : text.Substring(0, dot);
        if (whole.Length < 1 || whole.Length > 3 || (whole.Length > 1 && whole[0] == '0'))
        {
            return false;
        }

        int result = 0;
        foreach (char c in whole)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }

            result = (result * 10) + (c - '0');
        }

        result *= 10;
        if (dot >= 0)
        {
            if (text.Length != dot + 2 || text[dot + 1] < '0' || text[dot + 1] > '9')
            {
                return false;
            }

            result += text[dot + 1] - '0';
        }

        if (result > 1000)
        {
            return false;
        }

        tenths = result;
        return true;
    }

    private void Apply(string name, string value)
    {
        switch (name)
        {
            case "--policy":
                if (!PolicySpec.TryParse(value, out _))
                {
                    throw new RatesimException($"invalid policy \"{value}\" (expected fixed:LIMIT/WINDOW, sliding:LIMIT/WINDOW or bucket:RATE/WINDOW,BURST)");
                }

                Policies.Add(value);
                break;
            case "--scope":
                if (value != "key" && value != "all")
                {
                    throw new RatesimException($"invalid --scope \"{value}\" (expected key or all)");
                }

                SharedScope = value == "all";
                break;
            case "--kind":
                if (value != "fixed" && value != "sliding")
                {
                    throw new RatesimException($"invalid --kind \"{value}\" (expected fixed or sliding)");
                }

                Kind = value;
                break;
            case "--window":
                if (!TimeText.TryParseDuration(value, out long ms))
                {
                    throw new RatesimException($"invalid --window \"{value}\" (expected a duration such as 500ms, 10s, 5m or 1h)");
                }

                WindowText = value;
                WindowMs = ms;
                break;
            default:
                if (!TryParseTenths(value, out int tenths))
                {
                    throw new RatesimException($"invalid --max-denied \"{value}\" (expected a percentage from 0 to 100 with at most one decimal)");
                }

                MaxDeniedTenths = tenths;
                MaxDeniedGiven = true;
                break;
        }
    }

    private void Check()
    {
        if (Log is null)
        {
            throw new RatesimException("missing LOG");
        }

        if (Command == "run" && Policies.Count == 0)
        {
            throw new RatesimException("run needs --policy");
        }

        if (Command == "explain" && Policies.Count != 1)
        {
            throw new RatesimException("explain needs exactly one --policy");
        }

        if (Command == "suggest" && Kind is null)
        {
            throw new RatesimException("suggest needs --kind");
        }

        if (Command == "suggest" && WindowText is null)
        {
            throw new RatesimException("suggest needs --window");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string spec in Policies)
        {
            if (!seen.Add(spec))
            {
                throw new RatesimException($"duplicate policy \"{spec}\"");
            }
        }
    }
}
