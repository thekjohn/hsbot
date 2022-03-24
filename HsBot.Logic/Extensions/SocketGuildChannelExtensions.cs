namespace HsBot.Logic
{
    using Discord;
    using Discord.WebSocket;

    internal static class SocketGuildChannelExtensions
    {
        public static bool IsPubliclyAccessible(this SocketGuildChannel channel)
        {
            return channel.GetPermissionOverwrite(channel.Guild.EveryoneRole)?.ViewChannel == PermValue.Allow;
        }
    }
}