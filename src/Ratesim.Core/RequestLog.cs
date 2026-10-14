namespace Ratesim.Core;

public sealed record Request(int Line, string TimeText, long Ms, string Key, long Cost);

public sealed class RequestLog
{
    private RequestLog(IReadOnlyList<Request> requests)
    {
        Requests = requests;
    }

    public IReadOnlyList<Request> Requests { get; }

    public static RequestLog Parse(string display, string text)
    {
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text.Substring(1);
        }

        var requests = new List<Request>();
        string[] lines = text.Split('\n');
        long previous = long.MinValue;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.EndsWith('\r'))
            {
                line = line.Substring(0, line.Length - 1);
            }

            int number = i + 1;
            string trimmed = line.Trim(' ', '\t');
            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                continue;
            }

            string[] fields = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2 || fields.Length > 3)
            {
                throw new RatesimException($"{display}:{number}: expected TIME KEY [COST]");
            }

            if (!TimeText.TryParseTimestamp(fields[0], out long ms))
            {
                throw new RatesimException($"{display}:{number}: invalid time \"{fields[0]}\"");
            }

            long cost = 1;
            if (fields.Length == 3 && !TimeText.TryParsePositive(fields[2], out cost))
            {
                throw new RatesimException($"{display}:{number}: invalid cost \"{fields[2]}\"");
            }

            if (ms < previous)
            {
                throw new RatesimException($"{display}:{number}: time goes backwards");
            }

            previous = ms;
            requests.Add(new Request(number, fields[0], ms, fields[1], cost));
        }

        return new RequestLog(requests);
    }
}
