namespace HsBot.Logic
{
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;

    [Summary("greeter")]
    [RequireContext(ContextType.Guild)]
    [RequireMinimumAllianceRole(AllianceRole.Greeter)]
    public class Greeter : BaseModule
    {
        [Command("greeter")]
        [Summary("greeter|show greeter commands")]
        [RequireMinimumAllianceRole(AllianceRole.Greeter)]
        public async Task ShowGreeterCommandList()
        {
            await CleanupService.DeleteCommand(Context.Message);
            var eb = new EmbedBuilder()
                .WithTitle("GREETER COMMANDS")
                .AddField("Recruit to a corporation", "`!recruit <userName> <corpName> <rsLevel>`")
                .AddField("Promote to WS guest (WS signup access)", "`!promote-wsguest <userName>`")
                .AddField("Promote to Ally (RS queue access)", "`!promote-ally <userName> <rsLevel>`")
                .AddField("Demote to guest, remove all roles", "`!demote <userName>`");
            await Context.Channel.SendMessageAsync(null, embed: eb.Build());
        }

        [Command("recruit")]
        [Summary("recruit <userName> <corpName> <rsLevel>|recruit user to a corp and RS level")]
        [RequireMinimumAllianceRole(AllianceRole.Greeter)]
        public async Task Recruit(string userName, string corpName, int rsLevel)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            if (alliance == null)
                return;

            var user = Context.Guild.FindUser(CurrentUser, userName);
            if (user == null)
            {
                await Context.Channel.BotResponse("Can't find user: " + userName, ResponseType.error);
                return;
            }

            if (!user.Roles.Any(x => x.Id == alliance.GuestRoleId || x.Id == alliance.AllyRoleId))
            {
                await Context.Channel.BotResponse("Only guests or allies can be recruited!", ResponseType.error);
                return;
            }

            if (user.GuildPermissions.Administrator)
            {
                await Context.Channel.BotResponse("Administrators can't be recruited!", ResponseType.error);
                return;
            }

            var corp = Context.Guild.FindCorp(alliance, corpName);
            if (corp == null)
            {
                await Context.Channel.BotResponse("Can't find corp: " + corpName, ResponseType.error);
                return;
            }

            await RoleLogic.Recruit(Context.Guild, Context.Channel, user, alliance, corp, rsLevel);
        }

        [Command("demote")]
        [Summary("demote <userName>|remove all roles and add guest role")]
        [RequireMinimumAllianceRole(AllianceRole.Greeter)]
        public async Task DemoteToGuest(string userName)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            if (alliance == null)
                return;

            var user = Context.Guild.FindUser(CurrentUser, userName);
            if (user == null)
            {
                await Context.Channel.BotResponse("Can't find user: " + userName, ResponseType.error);
                return;
            }

            if (user.GuildPermissions.Administrator)
            {
                await Context.Channel.BotResponse("Administrators can't be guestified!", ResponseType.error);
                return;
            }

            await RoleLogic.DemoteToGuest(Context.Guild, Context.Channel, user, alliance);
        }

        [Command("promote-wsguest")]
        [Summary("promote-wsguest <userName>|add ws guest and compendium roles")]
        [RequireMinimumAllianceRole(AllianceRole.Greeter)]
        public async Task PromoteToWsGuest(string userName)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            if (alliance == null)
                return;

            var user = Context.Guild.FindUser(CurrentUser, userName);
            if (user == null)
            {
                await Context.Channel.BotResponse("Can't find user: " + userName, ResponseType.error);
                return;
            }

            if (user.GuildPermissions.Administrator)
            {
                await Context.Channel.BotResponse("Administrators can't be guestified!", ResponseType.error);
                return;
            }

            await RoleLogic.PromoteToWsGuest(Context.Guild, Context.Channel, user, alliance);
        }

        [Command("promote-ally")]
        [Summary("promote-ally <userName> <rsLevel>|add RS queue access")]
        [RequireMinimumAllianceRole(AllianceRole.Greeter)]
        public async Task PromoteToAlly(string userName, int rsLevel)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            if (alliance == null)
                return;

            var user = Context.Guild.FindUser(CurrentUser, userName);
            if (user == null)
            {
                await Context.Channel.BotResponse("Can't find user: " + userName, ResponseType.error);
                return;
            }

            if (user.GuildPermissions.Administrator)
            {
                await Context.Channel.BotResponse("Administrators can't be guestified!", ResponseType.error);
                return;
            }

            await RoleLogic.PromoteToAlly(Context.Guild, Context.Channel, user, alliance, rsLevel);
        }

        [Command("give")]
        [Summary("give <userName> <roleName>|add role to a user")]
        [RequireMinimumAllianceRole(AllianceRole.Greeter)]
        public async Task GiveRole(string userName, string roleName)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var user = Context.Guild.FindUser(CurrentUser, userName);
            if (user == null)
            {
                await Context.Channel.BotResponse("Can't find user: " + userName, ResponseType.error);
                return;
            }

            if (user.GuildPermissions.Administrator)
            {
                await Context.Channel.BotResponse("Administrators can't be changed this way!", ResponseType.error);
                return;
            }

            var role = Context.Guild.FindRole(roleName);
            if (role == null)
            {
                await Context.Channel.BotResponse("Can't find role: " + roleName, ResponseType.error);
                return;
            }

            if (role.Position >= CurrentUser.Roles.Max(x => x.Position))
            {
                await Context.Channel.BotResponse("You can't give a role equal or higher than your highest role!", ResponseType.error);
                return;
            }

            await RoleLogic.GiveRole(Context.Guild, Context.Channel, user, role);
        }

        [Command("take")]
        [Summary("take <userName> <roleName>|take away a role from a user")]
        [RequireMinimumAllianceRole(AllianceRole.Greeter)]
        public async Task TakeRole(string userName, string roleName)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var user = Context.Guild.FindUser(CurrentUser, userName);
            if (user == null)
            {
                await Context.Channel.BotResponse("Can't find user: " + userName, ResponseType.error);
                return;
            }

            if (user.GuildPermissions.Administrator)
            {
                await Context.Channel.BotResponse("Administrators can't be changed this way!", ResponseType.error);
                return;
            }

            var role = Context.Guild.FindRole(roleName);
            if (role == null)
            {
                await Context.Channel.BotResponse("Can't find role: " + roleName, ResponseType.error);
                return;
            }

            if (role.Position >= CurrentUser.Roles.Max(x => x.Position))
            {
                await Context.Channel.BotResponse("You can't take away a role equal or higher than your highest role!", ResponseType.error);
                return;
            }

            await RoleLogic.TakeRole(Context.Guild, Context.Channel, user, role);
        }
    }
}