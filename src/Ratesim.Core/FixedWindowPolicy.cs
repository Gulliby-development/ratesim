namespace Ratesim.Core;

public sealed class FixedWindowPolicy : IPolicy
{
    private readonly long _limit;
    private readonly long _windowMs;
    private readonly Dictionary<string, (long Window, long Used)> _state = new();

    public FixedWindowPolicy(long limit, long windowMs)
    {
        _limit = limit;
        _windowMs = windowMs;
    }

    public Decision Decide(string key, long ms, long cost)
    {
        long window = ms / _windowMs;
        long used = 0;
        if (_state.TryGetValue(key, out var entry) && entry.Window == window)
        {
            used = entry.Used;
        }

        if (used + cost <= _limit)
        {
            used += cost;
            _state[key] = (window, used);
            return new Decision(true, used + " of " + _limit + " used");
        }

        return new Decision(false, used + " of " + _limit + " used");
    }
}
