namespace HsBot.Logic
{
    using System.Globalization;

    internal static class DateTimeExtensions
    {
        public static string GetAgoString(this DateTime value)
        {
            var result = "";
            var diff = DateTime.UtcNow.Subtract(value);
            if (diff.Days > 0)
                result += (result != "" ? " " : "") + diff.Days.ToString("D", CultureInfo.InvariantCulture) + "d";

            if (diff.Hours > 0)
                result += (result != "" ? " " : "") + diff.Hours.ToString("D", CultureInfo.InvariantCulture) + "h";

            if (diff.Minutes > 0)
                result += (result != "" ? " " : "") + diff.Minutes.ToString("D", CultureInfo.InvariantCulture) + "m";

            if (diff.Seconds > 0)
                result += (result != "" ? " " : "") + diff.Seconds.ToString("D", CultureInfo.InvariantCulture) + "s";

            if (result == "")
                result = "0s";

            return result;
        }

        public static DateTime AddToNow(this string hourMinuteNotation)
        {
            var days = GetNotationPart(hourMinuteNotation, 'd');
            var hours = GetNotationPart(hourMinuteNotation, 'h');
            var minutes = GetNotationPart(hourMinuteNotation, 'm');
            var seconds = GetNotationPart(hourMinuteNotation, 's');

            return DateTime.UtcNow
                .AddDays(days)
                .AddHours(hours)
                .AddMinutes(minutes)
                .AddSeconds(seconds);
        }

        private static int GetNotationPart(string input, char kind)
        {
            var idx = input.IndexOf(kind);
            if (idx == -1)
                return 0;

            idx--;
            var value = 0;
            var pos = 1;
            while (idx >= 0)
            {
                var c = input.Substring(idx, 1);
                if (!int.TryParse(c, out var v))
                    break;

                value += pos * v;
                idx--;
                pos *= 10;
            }

            return value;
        }
    }
}