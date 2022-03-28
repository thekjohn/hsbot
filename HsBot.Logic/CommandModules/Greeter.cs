namespace HsBot.Logic
{
    using System.Threading.Tasks;
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
            await HelpLogic.ShowMostUsedGreeterCommands(Context.Guild, Context.Channel);
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

        [Command("wsguest")]
        [Summary("wsguest <userName>|add ws guest and compendium roles")]
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

        [Command("ally")]
        [Summary("ally <userName> <rsLevel>|add RS queue access")]
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

        [Command("setname")]
        [Summary("setname <userName> <ingameName> [corpName]|Set the ingame name of a guest/ally/WS guest. Corp is optional. Example: `!setname \"He Was Called Special\" \"He.Is.Special\" \"Blue Cat Order\"`")]
        [RequireMinimumAllianceRole(AllianceRole.Greeter)]
        public async Task SetName(string userName, string ingameName, string corpName = null)
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

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            if (alliance == null)
                return;

            if (!user.Roles.Any(x => x.Id == alliance.AllyRoleId || x.Id == alliance.GuestRoleId || x.Id == alliance.WsGuestRoleId)
                || user.Roles.Any(x => x.Id == alliance.RoleId))
            {
                await Context.Channel.BotResponse("Only guests, WS guests, and allies can be renamed with this command!", ResponseType.error);
                return;
            }

            if (user.Roles.Any(x => x.Id == alliance.RoleId))
            {
                await Context.Channel.BotResponse("Alliance members can't be renamed with this command!", ResponseType.error);
                return;
            }

            await RoleLogic.ChangeName(Context.Guild, Context.Channel, user, ingameName, corpName);
        }

        /*[Command("setcorp")]
        [Summary("setcorp <userName> <corpName>|Set the corp name of a guest/ally/WS guest. Example: `!setcorp \"Monster71\" \"Blue Cat Order\"`")]
        [RequireMinimumAllianceRole(AllianceRole.Greeter)]
        public async Task SetCorp(string userName, string corpName)
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

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            if (alliance == null)
                return;

            if (!user.Roles.Any(x => x.Id == alliance.AllyRoleId || x.Id == alliance.GuestRoleId || x.Id == alliance.WsGuestRoleId)
                || user.Roles.Any(x => x.Id == alliance.RoleId))
            {
                await Context.Channel.BotResponse("Only guests, WS guests, and allies can be renamed with this command!", ResponseType.error);
            }

            var ign = user.DisplayName;
            if (ign.IndexOf("[") == 0 && ign.IndexOf("]") > 0)
            {
                ign = user.DisplayName[(ign.IndexOf("]") + 1)..].Trim();
            }
            else if (ign.IndexOf("[") > 0)
            {
                ign = user.DisplayName[..(ign.IndexOf("[") - 1)].Trim();
            }

            await RoleLogic.ChangeName(Context.Guild, Context.Channel, user, ign, corpName);
        }*/

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