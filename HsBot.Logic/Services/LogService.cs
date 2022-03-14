namespace HsBot.Logic
{
    using System.Globalization;
    using Discord;
    using Discord.WebSocket;

    internal static class LogService
    {
        public static void Log(string userName, string text, ConsoleColor color = ConsoleColor.Gray)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(DateTime.UtcNow.ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture) + (userName != null ? " [" + userName + "]" : "") + " " + text);
        }

        public static void LogToChannel(SocketGuild guild, string message, Embed embed)
        {
            var channelId = StateService.Get<ulong>(guild.Id, "bot-log-channel");
            if (channelId == 0)
                return;

            var channel = guild.GetTextChannel(channelId);
            if (channel == null)
                return;

            channel.SendMessageAsync(message, embed: embed);
        }
    }
}