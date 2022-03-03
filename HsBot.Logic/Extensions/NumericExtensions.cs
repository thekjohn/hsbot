namespace HsBot.Logic
{
    using System.Globalization;

    internal static class NumericExtensions
    {
        public static string ToStr(this int value)
        {
            return value.ToString("D", CultureInfo.InvariantCulture);
        }

        public static string ToStr(this long value)
        {
            return value.ToString("D", CultureInfo.InvariantCulture);
        }

        public static string ToStr(this ulong value)
        {
            return value.ToString("D", CultureInfo.InvariantCulture);
        }
    }
}