namespace Ratesim.Core;

public sealed class SlidingWindowPolicy : IPolicy
{
    private readonly long _limit;
    private readonly long _windowMs;
    private readonly Dictionary<string, KeyState> _state = new();

    public SlidingWindowPolicy(long limit, long windowMs)
    {
        _limit = limit;
        _windowMs = windowMs;
    }

    public Decision Decide(string key, long ms, long cost)
    {
        if (!_state.TryGetValue(key, out var state))
        {
            state = new KeyState();
            _state[key] = state;
        }

        while (state.Entries.Count > 0 && state.Entries.Peek().Ms <= ms - _windowMs)
        {
            state.Sum -= state.Entries.Dequeue().Cost;
        }

        if (state.Sum + cost <= _limit)
        {
            state.Entries.Enqueue((ms, cost));
            state.Sum += cost;
            return new Decision(true, state.Sum + " of " + _limit + " used");
        }

        return new Decision(false, state.Sum + " of " + _limit + " used");
    }

    private sealed class KeyState
    {
        public Queue<(long Ms, long Cost)> Entries { get; } = new();

        public long Sum { get; set; }
    }
}
