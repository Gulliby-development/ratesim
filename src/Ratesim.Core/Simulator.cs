namespace Ratesim.Core;

public sealed record PolicyResult(string Spec, int Allowed, int Denied);

public sealed record ExplainEntry(Request Request, Decision Decision);

public sealed record Suggestion(string Spec, int Denied, int Total);

public static class Simulator
{
    private const string SharedKey = "\u0000all";

    public static (int Allowed, int Denied) Run(IReadOnlyList<Request> requests, IPolicy policy, bool shared)
    {
        int allowed = 0;
        int denied = 0;
        foreach (Request request in requests)
        {
            Decision decision = policy.Decide(shared ? SharedKey : request.Key, request.Ms, request.Cost);
            if (decision.Allowed)
            {
                allowed++;
            }
            else
            {
                denied++;
            }
        }

        return (allowed, denied);
    }

    public static IReadOnlyList<ExplainEntry> Explain(IReadOnlyList<Request> requests, IPolicy policy, bool shared)
    {
        var entries = new List<ExplainEntry>(requests.Count);
        foreach (Request request in requests)
        {
            entries.Add(new ExplainEntry(request, policy.Decide(shared ? SharedKey : request.Key, request.Ms, request.Cost)));
        }

        return entries;
    }

    public static int CountKeys(IReadOnlyList<Request> requests)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (Request request in requests)
        {
            keys.Add(request.Key);
        }

        return keys.Count;
    }

    public static Suggestion Suggest(IReadOnlyList<Request> requests, string kind, string windowText, int maxDeniedTenths, bool shared)
    {
        var totals = new Dictionary<string, long>(StringComparer.Ordinal);
        long largest = 1;
        foreach (Request request in requests)
        {
            string key = shared ? SharedKey : request.Key;
            totals.TryGetValue(key, out long sum);
            sum += request.Cost;
            totals[key] = sum;
            if (sum > largest)
            {
                largest = sum;
            }
        }

        long total = requests.Count;
        for (long limit = 1; limit <= largest; limit++)
        {
            string spec = $"{kind}:{limit}/{windowText}";
            if (!PolicySpec.TryParse(spec, out IPolicy policy))
            {
                throw new RatesimException($"cannot build policy \"{spec}\"");
            }

            int denied = 0;
            bool within = true;
            foreach (Request request in requests)
            {
                if (!policy.Decide(shared ? SharedKey : request.Key, request.Ms, request.Cost).Allowed)
                {
                    denied++;
                    if (denied * 1000L > maxDeniedTenths * total)
                    {
                        within = false;
                        break;
                    }
                }
            }

            if (within)
            {
                return new Suggestion(spec, denied, requests.Count);
            }
        }

        throw new RatesimException("no limit found");
    }
}
