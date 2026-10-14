namespace Ratesim.Core;

public interface IPolicy
{
    Decision Decide(string key, long ms, long cost);
}

public readonly record struct Decision(bool Allowed, string Detail);

public static class PolicySpec
{
    public static bool TryParse(string? spec, out IPolicy policy)
    {
        policy = null!;
        if (spec is null)
        {
            return false;
        }

        if (spec.StartsWith("fixed:", StringComparison.Ordinal) || spec.StartsWith("sliding:", StringComparison.Ordinal))
        {
            bool isFixed = spec[0] == 'f';
            string rest = spec.Substring(spec.IndexOf(':') + 1);
            if (!TrySplit(rest, '/', out string limitText, out string windowText)
                || !TimeText.TryParsePositive(limitText, out long limit)
                || !TimeText.TryParseDuration(windowText, out long window))
            {
                return false;
            }

            policy = isFixed ? new FixedWindowPolicy(limit, window) : new SlidingWindowPolicy(limit, window);
            return true;
        }

        if (spec.StartsWith("bucket:", StringComparison.Ordinal))
        {
            string rest = spec.Substring(7);
            if (!TrySplit(rest, '/', out string rateText, out string tail)
                || !TrySplit(tail, ',', out string windowText, out string burstText)
                || !TimeText.TryParsePositive(rateText, out long rate)
                || !TimeText.TryParseDuration(windowText, out long window)
                || !TimeText.TryParsePositive(burstText, out long burst))
            {
                return false;
            }

            policy = new TokenBucketPolicy(rate, window, burst);
            return true;
        }

        return false;
    }

    private static bool TrySplit(string text, char separator, out string left, out string right)
    {
        int index = text.IndexOf(separator);
        if (index < 0 || text.IndexOf(separator, index + 1) >= 0)
        {
            left = string.Empty;
            right = string.Empty;
            return false;
        }

        left = text.Substring(0, index);
        right = text.Substring(index + 1);
        return true;
    }
}
