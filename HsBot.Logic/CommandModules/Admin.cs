namespace HsBot.Logic
{
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;

    [Summary("admin")]
    public class Admin : BaseModule
    {
        [Command("setalliance")]
        [Summary("setalliance <name> <abbrev>|set the main parameters of the alliance")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetAlliance(SocketRole role, string name, string abbrev)
        {
            await Context.Message.DeleteAsync();

            var alliance = Alliance.GetAlliance(Context.Guild.Id);
            alliance.RoleId = role.Id;
            alliance.Name = name;
            alliance.Abbreviation = abbrev;
            Alliance.SaveAlliance(Context.Guild.Id, alliance);

            await ReplyAsync("alliance successfully changed: " + role.Name);
        }

        [Command("setcorp")]
        [Summary("setcorplevel <name> <icon> <abbrev>|set the main parameters of a corp")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetCorp(SocketRole role, string fullName, string icon, string abbrev)
        {
            var alliance = Alliance.GetAlliance(Context.Guild.Id);
            var corp = alliance.Corporations.Find(x => x.RoleId == role.Id);
            if (corp != null)
            {
                corp.FullName = fullName;
                corp.IconMention = icon;
                corp.Abbreviation = abbrev;
                Alliance.SaveAlliance(Context.Guild.Id, alliance);

                await ReplyAsync("corp successfully changed: " + role.Name);
            }
            else
            {
                await ReplyAsync("unknown corp: " + role.Name);
            }
        }

        [Command("addcorp")]
        [Summary("addcorp <role>|add new corp to the alliance")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task AddCorp(SocketRole role)
        {
            var alliance = Alliance.GetAlliance(Context.Guild.Id);
            var corp = alliance.Corporations.Find(x => x.RoleId == role.Id);
            if (corp == null)
            {
                corp = new Alliance.Corp
                {
                    RoleId = role.Id
                };

                alliance.Corporations.Add(corp);
                Alliance.SaveAlliance(Context.Guild.Id, alliance);

                await ReplyAsync("corp created: " + role.Name);
            }
            else
            {
                await ReplyAsync("corp already added: " + role.Name);
            }
        }

        [Command("setcorplevel")]
        [Summary("setcorplevel <level> <relics>|change the level and relic count of a corp")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task SetCorpLevel(SocketRole role, int level, int relics)
        {
            /*if (!CurrentUser.Roles.Any(x => x.Id == role.Id))
            {
                await ReplyAsync("Only members within the specified corp can use this command!");
                return;
            }*/

            var alliance = Alliance.GetAlliance(Context.Guild.Id);

            var corp = alliance.Corporations.Find(x => x.RoleId == role.Id);
            if (corp != null)
            {
                corp.CurrentLevel = level;
                corp.CurrentRelicCount = relics;
                Alliance.SaveAlliance(Context.Guild.Id, alliance);

                await ReplyAsync("corp successfully changed: " + role.Name);
            }
            else
            {
                await ReplyAsync("unknown corp: " + role.Name);
            }
        }

        [Command("setrstimeout")]
        [Summary("setrstimeout <level> <minutes>|change the activity timout of a specific RS queue")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task SetRsTimeout(int level, int minutes)
        {
            await Context.Message.DeleteAsync();

            var stateId = Services.State.GetId("rs-queue-timout", (ulong)level);
            var currentValue = Services.State.Get<int>(Context.Guild.Id, stateId);

            Services.State.Set(Context.Guild.Id, stateId, minutes);
            await ReplyAsync("Timeout for RS" + level.ToStr() + " has been changed to " + minutes.ToStr() + "."
                + (currentValue != 0 ? " Previous value was " + currentValue.ToStr() + "." : ""));
        }

        [Command("setrstimeout")]
        [Summary("setrstimeout <minutes>|change the activity timout of a all RS queues")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task SetRsTimeout(int minutes)
        {
            await Context.Message.DeleteAsync();

            for (var level = 1; level <= 12; level++)
            {
                var stateId = Services.State.GetId("rs-queue-timout", (ulong)level);
                var currentValue = Services.State.Get<int>(Context.Guild.Id, stateId);
                if (currentValue != minutes)
                {
                    Services.State.Set(Context.Guild.Id, stateId, minutes);
                }

                await ReplyAsync("Timeout for RS" + level.ToStr() + " has been changed to " + minutes.ToStr() + "."
                    + (currentValue != 0 ? " Previous value was " + currentValue.ToStr() + "." : ""));
            }
        }

        [Command("setallyrole")]
        [Summary("setallyrole <role> <abbreviation>|set the main parameters of the alliance")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetAllyRole(SocketRole role, string icon)
        {
            await Context.Message.DeleteAsync();

            var alliance = Alliance.GetAlliance(Context.Guild.Id);
            alliance.AllyRoleId = role.Id;
            alliance.AllyIcon = icon;
            Alliance.SaveAlliance(Context.Guild.Id, alliance);

            await ReplyAsync("ally data successfully changed: " + role.Name);
        }
    }
}