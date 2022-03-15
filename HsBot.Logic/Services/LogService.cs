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

        public static async Task LogToChannel(SocketGuild guild, string message, Embed embed)
        {
            // todo: move into AllianceInfo
            var channel = guild.GetTextChannel(StateService.Get<ulong>(guild.Id, "bot-log-channel"));
            if (channel == null)
                return;

            await channel.SendMessageAsync(message, embed: embed);
        }
    }
}