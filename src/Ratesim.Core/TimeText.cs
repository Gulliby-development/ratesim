namespace Ratesim.Core;

public static class TimeText
{
    private const long MaxDurationMs = 86400000;

    public static bool TryParsePositive(string? text, out long value)
    {
        value = 0;
        if (text is null || text.Length < 1 || text.Length > 6 || text[0] == '0')
        {
            return false;
        }

        long result = 0;
        foreach (char c in text)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }

            result = (result * 10) + (c - '0');
        }

        value = result;
        return true;
    }

    public static bool TryParseDuration(string? text, out long ms)
    {
        ms = 0;
        if (text is null || text.Length < 2)
        {
            return false;
        }

        string number;
        long factor;
        if (text.EndsWith("ms", StringComparison.Ordinal))
        {
            number = text.Substring(0, text.Length - 2);
            factor = 1;
        }
        else
        {
            number = text.Substring(0, text.Length - 1);
            switch (text[text.Length - 1])
            {
                case 's':
                    factor = 1000;
                    break;
                case 'm':
                    factor = 60000;
                    break;
                case 'h':
                    factor = 3600000;
                    break;
                default:
                    return false;
            }
        }

        if (!TryParsePositive(number, out long n))
        {
            return false;
        }

        long total = n * factor;
        if (total > MaxDurationMs)
        {
            return false;
        }

        ms = total;
        return true;
    }

    public static bool TryParseTimestamp(string? text, out long ms)
    {
        ms = 0;
        if (text is null || text.Length < 20 || text.Length == 21 || text.Length > 24)
        {
            return false;
        }

        if (text[4] != '-' || text[7] != '-' || text[10] != 'T' || text[13] != ':' || text[16] != ':' || text[text.Length - 1] != 'Z')
        {
            return false;
        }

        if (!TryDigits(text, 0, 4, out int year) || !TryDigits(text, 5, 2, out int month) || !TryDigits(text, 8, 2, out int day)
            || !TryDigits(text, 11, 2, out int hour) || !TryDigits(text, 14, 2, out int minute) || !TryDigits(text, 17, 2, out int second))
        {
            return false;
        }

        long fraction = 0;
        if (text.Length > 20)
        {
            if (text[19] != '.')
            {
                return false;
            }

            int digits = text.Length - 21;
            if (!TryDigits(text, 20, digits, out int f))
            {
                return false;
            }

            fraction = f;
            for (int i = digits; i < 3; i++)
            {
                fraction *= 10;
            }
        }
        else if (text[19] != 'Z')
        {
            return false;
        }

        if (year < 1970 || month < 1 || month > 12 || day < 1 || day > DaysInMonth(year, month)
            || hour > 23 || minute > 59 || second > 59)
        {
            return false;
        }

        long days = DaysFromCivil(year, month, day);
        ms = ((((days * 24) + hour) * 60 + minute) * 60 + second) * 1000 + fraction;
        return true;
    }

    private static bool TryDigits(string text, int start, int count, out int value)
    {
        value = 0;
        int result = 0;
        for (int i = start; i < start + count; i++)
        {
            char c = text[i];
            if (c < '0' || c > '9')
            {
                return false;
            }

            result = (result * 10) + (c - '0');
        }

        value = result;
        return true;
    }

    private static bool IsLeap(int year) => (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;

    private static int DaysInMonth(int year, int month)
    {
        switch (month)
        {
            case 2:
                return IsLeap(year) ? 29 : 28;
            case 4:
            case 6:
            case 9:
            case 11:
                return 30;
            default:
                return 31;
        }
    }

    private static long DaysFromCivil(int year, int month, int day)
    {
        long y = month <= 2 ? year - 1 : year;
        long era = y / 400;
        long yoe = y - (era * 400);
        long mp = month > 2 ? month - 3 : month + 9;
        long doy = (((153 * mp) + 2) / 5) + day - 1;
        long doe = (yoe * 365) + (yoe / 4) - (yoe / 100) + doy;
        return (era * 146097) + doe - 719468;
    }
}
