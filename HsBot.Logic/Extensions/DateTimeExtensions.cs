namespace HsBot.Logic;

using System.Globalization;

internal static class DateTimeExtensions
{
    public static string ToIntervalStr(this TimeSpan value, bool includeMinutes = true, bool includeSeconds = true)
    {
        var result = "";
        if (value.Days > 0)
            result += (result != "" ? " " : "") + value.Days.ToString("D", CultureInfo.InvariantCulture) + "d";

        if (value.Hours > 0)
            result += (result != "" ? " " : "") + value.Hours.ToString("D", CultureInfo.InvariantCulture) + "h";

        if (value.Minutes > 0 && includeMinutes)
            result += (result != "" ? " " : "") + value.Minutes.ToString("D", CultureInfo.InvariantCulture) + "m";

        if (value.Seconds > 0 && includeSeconds)
            result += (result != "" ? " " : "") + value.Seconds.ToString("D", CultureInfo.InvariantCulture) + "s";

        if (result == "")
            result = "0s";

        return result;
    }

    public static DateTime AddToDateTime(this string hourMinuteNotation, DateTime dateTime)
    {
        var days = GetNotationPart(hourMinuteNotation, 'd');
        var hours = GetNotationPart(hourMinuteNotation, 'h');
        var minutes = GetNotationPart(hourMinuteNotation, 'm');
        var seconds = GetNotationPart(hourMinuteNotation, 's');

        return dateTime
            .AddDays(days)
            .AddHours(hours)
            .AddMinutes(minutes)
            .AddSeconds(seconds);
    }

    private static double GetNotationPart(string input, char kind)
    {
        var idx = input.IndexOf(kind, StringComparison.InvariantCultureIgnoreCase);
        if (idx == -1)
            return 0;

        idx--;
        var value = 0.0d;
        var pos = 1;
        while (idx >= 0)
        {
            var c = input.Substring(idx, 1);
            if (c == ".")
            {
                value = double.Parse("0." + value.ToString("G", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                idx--;
                pos = 1;
                continue;
            }

            if (!int.TryParse(c, out var v))
                break;

            value += pos * v;
            idx--;
            pos *= 10;
        }

        return value;
    }
}
