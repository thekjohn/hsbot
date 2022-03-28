namespace HsBot.Logic;

public class BaseModule : ModuleBase<SocketCommandContext>
{
    protected SocketGuildUser CurrentUser => Context.User as SocketGuildUser;
}
