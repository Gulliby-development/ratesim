namespace Ratesim.Core;

public sealed class TokenBucketPolicy : IPolicy
{
    private readonly long _rate;
    private readonly long _windowMs;
    private readonly long _burst;
    private readonly long _cap;
    private readonly Dictionary<string, (long Scaled, long Last)> _state = new();

    public TokenBucketPolicy(long rate, long windowMs, long burst)
    {
        _rate = rate;
        _windowMs = windowMs;
        _burst = burst;
        _cap = burst * windowMs;
    }

    public Decision Decide(string key, long ms, long cost)
    {
        if (cost > _burst)
        {
            return new Decision(false, "cost " + cost + " exceeds burst " + _burst);
        }

        long scaled = _cap;
        if (_state.TryGetValue(key, out var entry))
        {
            long elapsed = ms - entry.Last;
            if (elapsed >= (_cap / _rate) + 1)
            {
                scaled = _cap;
            }
            else
            {
                scaled = Math.Min(_cap, entry.Scaled + (elapsed * _rate));
            }
        }

        long needed = cost * _windowMs;
        if (scaled >= needed)
        {
            scaled -= needed;
            _state[key] = (scaled, ms);
            return new Decision(true, "tokens left: " + (scaled / _windowMs));
        }

        return new Decision(false, "tokens left: " + (scaled / _windowMs));
    }
}
