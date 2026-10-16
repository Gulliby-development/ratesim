using System.Globalization;
using System.Text;

namespace Ratesim.Core;

public static class Report
{
    public static string Percent(int denied, int total)
    {
        long tenths = total == 0 ? 0 : (denied * 2000L + total) / (2L * total);
        return Num(tenths / 10) + "." + Num(tenths % 10) + "%";
    }

    public static string RunText(int requestCount, int keyCount, IReadOnlyList<PolicyResult> results)
    {
        var rows = new List<string[]>
        {
            new[] { "policy", "allowed", "denied", "denied%" },
        };
        foreach (PolicyResult result in results)
        {
            rows.Add(new[]
            {
                result.Spec,
                Num(result.Allowed),
                Num(result.Denied),
                Percent(result.Denied, result.Allowed + result.Denied),
            });
        }

        var widths = new int[4];
        foreach (string[] row in rows)
        {
            for (int i = 0; i < 4; i++)
            {
                widths[i] = Math.Max(widths[i], row[i].Length);
            }
        }

        var sb = new StringBuilder();
        sb.Append(Plural(requestCount, "request")).Append(" from ").Append(Plural(keyCount, "key")).Append('\n');
        foreach (string[] row in rows)
        {
            sb.Append(row[0].PadRight(widths[0]));
            for (int i = 1; i < 4; i++)
            {
                sb.Append("  ").Append(row[i].PadLeft(widths[i]));
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }

    public static string ExplainText(IReadOnlyList<ExplainEntry> entries)
    {
        return ExplainText(entries, null);
    }

    public static string ExplainText(IReadOnlyList<ExplainEntry> entries, string? only)
    {
        var sb = new StringBuilder();
        int allowed = 0;
        int denied = 0;
        foreach (ExplainEntry entry in entries)
        {
            Request request = entry.Request;
            if (entry.Decision.Allowed)
            {
                allowed++;
            }
            else
            {
                denied++;
            }

            if (only is not null && ((only == "allowed") != entry.Decision.Allowed))
            {
                continue;
            }

            sb.Append(Num(request.Line)).Append(' ').Append(request.TimeText).Append(' ').Append(request.Key)
                .Append(entry.Decision.Allowed ? " allowed (" : " denied (")
                .Append(entry.Decision.Detail).Append(")\n");
        }

        sb.Append(Num(allowed)).Append(" allowed, ").Append(Num(denied)).Append(" denied\n");
        return sb.ToString();
    }

    public static string SuggestText(Suggestion suggestion)
    {
        return "suggested: " + suggestion.Spec + "\n"
            + "denied " + Num(suggestion.Denied) + " of " + Plural(suggestion.Total, "request")
            + " (" + Percent(suggestion.Denied, suggestion.Total) + ")\n";
    }

    private static string Plural(int count, string noun)
    {
        return Num(count) + " " + noun + (count == 1 ? string.Empty : "s");
    }

    private static string Num(long value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
