namespace HsBot.Logic;

internal static class SocketGuildChannelExtensions
{
    public static bool IsPubliclyAccessible(this SocketGuildChannel channel)
    {
        return channel.GetPermissionOverwrite(channel.Guild.EveryoneRole)?.ViewChannel == PermValue.Allow;
    }
}
