namespace HsBot.ConsoleHost;

internal static class Program
{
    private static void Main(string[] args)
    {
        var bot = new DiscordBot();
        bot.MainAsync().GetAwaiter().GetResult();
    }
}
