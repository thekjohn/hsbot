namespace HsBot.Logic
{
    using System.Globalization;

    internal class LogService
    {
        public void Log(string userName, string text, ConsoleColor color = ConsoleColor.Gray)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture) + (userName != null ? " [" + userName + "]" : "") + " " + text);
        }
    }
}