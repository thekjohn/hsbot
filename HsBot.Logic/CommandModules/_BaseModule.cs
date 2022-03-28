namespace HsBot.Logic;

using Discord.Commands;
using Discord.WebSocket;

public class BaseModule : ModuleBase<SocketCommandContext>
{
    protected SocketGuildUser CurrentUser => Context.User as SocketGuildUser;
}
