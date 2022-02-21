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
    }
}