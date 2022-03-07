namespace HsBot.Logic
{
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;
    using TimeZoneNames;

    [Summary("admin")]
    public class Admin : BaseModule
    {
        [Command("alts")]
        [Summary("alts [user]|display alts")]
        [RequireUserPermission(GuildPermission.ChangeNickname)]
        public async Task Alts(string user = null)
        {
            await CleanupService.DeleteCommand(Context.Message);

            SocketGuildUser otherUser = null;
            if (user != null)
            {
                otherUser = Context.Guild.FindUser(CurrentUser, user);
                if (otherUser == null)
                {
                    await Context.Channel.BotResponse("Can't find user: " + user + ".", ResponseType.error);
                    return;
                }
            }

            await AltsLogic.ShowAlts(Context.Guild, Context.Channel, CurrentUser, otherUser ?? CurrentUser);
        }

        [Command("addalt")]
        [Summary("addalt name|add a new alt for yourself")]
        [RequireUserPermission(GuildPermission.ChangeNickname)]
        public async Task AddAlt(string name)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await AltsLogic.AddAlt(Context.Guild, Context.Channel, CurrentUser, name);
        }

        [Command("setalliance")]
        [Summary("setalliance <name> <abbrev>|set the main parameters of the alliance")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetAlliance(SocketRole role, string name, string abbrev)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            alliance.RoleId = role.Id;
            alliance.Name = name;
            alliance.Abbreviation = abbrev;
            AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

            await Context.Channel.BotResponse("Alliance updated: " + role.Name, ResponseType.success);
        }

        [Command("setcorp")]
        [Summary("setcorp <nameToFind> <newName> <icon> <abbrev>|set the main parameters of a corp")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task SetCorp(string nameToFind, string newName, string icon, string abbrev)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            var corp = Context.Guild.FindCorp(alliance, nameToFind);
            if (corp != null)
            {
                corp.FullName = newName;
                corp.IconMention = icon;
                corp.Abbreviation = abbrev;
                AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

                await Context.Channel.BotResponse("Corp updated: " + nameToFind, ResponseType.success);
            }
            else
            {
                await Context.Channel.BotResponse("Unknown corp: " + nameToFind, ResponseType.error);
            }
        }

        [Command("addcorp")]
        [Summary("addcorp <role> <abbreviation>|add new corp to the alliance")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task AddCorp(SocketRole role, string abbreviation)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            var corp = alliance.Corporations.Find(x => x.RoleId == role.Id);
            if (corp == null)
            {
                corp = new AllianceLogic.Corp
                {
                    RoleId = role.Id,
                    Abbreviation = abbreviation,
                };

                alliance.Corporations.Add(corp);
                AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

                await Context.Channel.BotResponse("Corp created: " + role.Name, ResponseType.success);
            }
            else
            {
                await Context.Channel.BotResponse("Corp already created: " + role.Name, ResponseType.error);
            }
        }

        [Command("setrelics")]
        [Summary("setrelics <corp> <amount>|change the relic count of a corp")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task SetRelics(string corp, int relicCount)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            if (alliance == null)
                return;

            var corporation = Context.Guild.FindCorp(alliance, corp);
            if (corporation == null)
            {
                await Context.Channel.BotResponse("Unknown corp: " + corp, ResponseType.error);
                return;
            }

            if (!CurrentUser.GuildPermissions.Administrator && !CurrentUser.Roles.Any(x => x.Id == corporation.RoleId))
            {
                await Context.Channel.BotResponse("Only members of the specified corp can use this command!", ResponseType.error);
                return;
            }

            corporation.CurrentRelicCount = relicCount;
            AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

            await Context.Channel.BotResponse("Corp updated: " + corporation.FullName, ResponseType.success);
        }

        [Command("setrstimeout")]
        [Summary("setrstimeout <level> <minutes>|change the activity timeout of a specific RS queue")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task SetRsTimeout(int level, int minutes)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var stateId = Services.State.GetId("rs-queue-timeout", (ulong)level);
            var currentValue = Services.State.Get<int>(Context.Guild.Id, stateId);

            Services.State.Set(Context.Guild.Id, stateId, minutes);
            await Context.Channel.BotResponse("Timeout for RS" + level.ToStr() + " has been changed to " + minutes.ToStr() + "."
                    + (currentValue != 0 ? " Previous value was " + currentValue.ToStr() + "." : ""), ResponseType.success);
        }

        [Command("setrstimeout")]
        [Summary("setrstimeout <minutes>|change the activity timeout of a all RS queues")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task SetRsTimeout(int minutes)
        {
            await CleanupService.DeleteCommand(Context.Message);

            for (var level = 1; level <= 12; level++)
            {
                var stateId = Services.State.GetId("rs-queue-timeout", (ulong)level);
                var currentValue = Services.State.Get<int>(Context.Guild.Id, stateId);
                if (currentValue != minutes)
                {
                    Services.State.Set(Context.Guild.Id, stateId, minutes);
                }

                await Context.Channel.BotResponse("Timeout for RS" + level.ToStr() + " has been changed to " + minutes.ToStr() + "."
                    + (currentValue != 0 ? " Previous value was " + currentValue.ToStr() + "." : ""), ResponseType.success);
            }
        }

        [Command("setallyrole")]
        [Summary("setallyrole <role> <icon>|set the ally role")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetAllyRole(string roleName, string icon)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var role = Context.Guild.FindRole(roleName);
            if (role == null)
            {
                await Context.Channel.BotResponse("Unknown role: " + roleName, ResponseType.error);
                return;
            }

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            alliance.AllyRoleId = role.Id;
            alliance.AllyIcon = icon;
            AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

            await Context.Channel.BotResponse("Ally role changed: " + role.Name, ResponseType.success);
        }

        [Command("set-greeter-role")]
        [Summary("set-greeter-role <role>|set the greeter role")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetGreeterRole(string roleName)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var role = Context.Guild.FindRole(roleName);
            if (role == null)
            {
                await Context.Channel.BotResponse("Unknown role: " + roleName, ResponseType.error);
                return;
            }

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            alliance.GreeterRoleId = role.Id;
            AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

            await Context.Channel.BotResponse("Greeter role changed: " + role.Name, ResponseType.success);
        }

        [Command("set-guest-role")]
        [Summary("set-guest-role <role>|set the greeter role")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetGuestRole(string roleName)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var role = Context.Guild.FindRole(roleName);
            if (role == null)
            {
                await Context.Channel.BotResponse("Unknown role: " + roleName, ResponseType.error);
                return;
            }

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            alliance.GuestRoleId = role.Id;
            AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

            await Context.Channel.BotResponse("Guest role changed: " + role.Name, ResponseType.success);
        }

        [Command("set-compendium-role")]
        [Summary("set-compendium-role <role>|set the compendium role")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetCompendiumRole(string roleName)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var role = Context.Guild.FindRole(roleName);
            if (role == null)
            {
                await Context.Channel.BotResponse("Unknown role: " + roleName, ResponseType.error);
                return;
            }

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            alliance.CompendiumRoleId = role.Id;
            AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

            await Context.Channel.BotResponse("Compendium role changed: " + role.Name, ResponseType.success);
        }

        [Command("set-public-channel")]
        [Summary("set-public-channel <channel>|set the public chat channel")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetGreeterRole(SocketTextChannel channel)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            alliance.PublicChannelId = channel.Id;
            AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

            await Context.Channel.BotResponse("Public chat channel changed: " + channel.Name, ResponseType.success);
        }

        [Command("falsestart")]
        [Summary("falsestart <runNumber>|invalidate an RS run")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task FalseStartRun(int runNumber)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await FalseStartRun(Context.Guild, Context.Channel, runNumber);
        }

        private static async Task FalseStartRun(SocketGuild guild, ISocketMessageChannel channel, int runNumber)
        {
            var queueStateId = Services.State.GetId("rs-log", (ulong)runNumber);
            var queue = Services.State.Get<Rs.RsQueueEntry>(guild.Id, queueStateId);
            if (queue == null)
            {
                await channel.BotResponse("Can't find run #" + runNumber.ToStr(), ResponseType.error);
                return;
            }

            foreach (var userId in queue.Users)
            {
                var runCountStateId = Services.State.GetId("rs-run-count", userId, (ulong)queue.Level);
                var runCount = Services.State.Get<int>(guild.Id, runCountStateId);
                runCount--;
                Services.State.Set(guild.Id, runCountStateId, runCount);
            }

            queue.FalseStart = DateTime.UtcNow;
            Services.State.Set(guild.Id, queueStateId, queue);

            await channel.BotResponse("Run #" + runNumber.ToStr() + " is successfuly reset.", ResponseType.success);
        }

        [Command("startwssignup")]
        [Summary("startwssignup 5d3h|start a new WS signup which ends in 5d3h from now")]
        public async Task StartWsSignup(string endsFromNow)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsSignupLogic.StartNew(Context.Guild, Context.Channel, CurrentUser, endsFromNow.AddToDateTime(DateTime.UtcNow));
        }

        [Command("timezone-list")]
        [Summary("timezone-list|list all time zones")]
        [RequireUserPermission(GuildPermission.ChangeNickname)]
        public async Task TimezoneList()
        {
            await CleanupService.DeleteCommand(Context.Message);
            var timezones = TimeZoneInfo.GetSystemTimeZones();
            var index = 0;

            var eb = new EmbedBuilder()
                .WithTitle("time zones");

            var now = DateTime.UtcNow;

            foreach (var tz in timezones)
            {
                var name = TZNames.GetDisplayNameForTimeZone(tz.Id, "en-US");
                index++;
                //var desc = "#" + index.ToStr() + " : UTC" + (tz.BaseUtcOffset.TotalMilliseconds >= 0 ? "+" : "-") + tz.BaseUtcOffset.ToString(@"hh\:mm");
                var desc = "#" + index.ToStr() + " [" + TimeZoneInfo.ConvertTimeFromUtc(now, tz).ToString("HH:mm") + "] [UTC" + (tz.BaseUtcOffset.TotalMilliseconds >= 0 ? "+" : "")
                    + (tz.BaseUtcOffset.Minutes == 0
                        ? tz.BaseUtcOffset.Hours.ToStr()
                        : tz.BaseUtcOffset.ToString(@"h\:mm")
                        ) + "]";
                eb.AddField(tz.StandardName, desc, true);

                if (eb.Fields.Count == 25)
                {
                    await ReplyAsync(embed: eb.Build());
                    eb = new EmbedBuilder()
                        .WithTitle("time zones");
                }
            }

            if (eb.Fields.Count > 0)
                await ReplyAsync(embed: eb.Build());
        }

        [Command("timezone-set")]
        [Summary("timezone-set <identifier>|set your own timezone. Get a list of identifiers with the `{cmdPrefix}timezone-list` command")]
        [RequireUserPermission(GuildPermission.ChangeNickname)]
        public async Task TimezoneSet(int identifier/*, string otherUser = null*/)
        {
            /*if (otherUser != null && !CurrentUser.GuildPermissions.Administrator)
            {
                await Context.Channel.BotResponse("Only Administrators can set the timezone for other users.", ResponseType.error);
                return;
            }
            */
            await CleanupService.DeleteCommand(Context.Message);
            var timeZones = TimeZoneInfo.GetSystemTimeZones();
            if (identifier < 1 || identifier > timeZones.Count)
            {
                await Context.Channel.BotResponse("Wrong timezone index", ResponseType.error);
                return;
            }

            var user = CurrentUser;
            /*if (otherUser != null)
            {
                user = Context.Guild.FindUser(CurrentUser, otherUser);
                if (user == null)
                {
                    await Context.Channel.BotResponse("Unknown user: " + otherUser, ResponseType.error);
                    return;
                }
            }*/

            var tz = timeZones[identifier - 1];
            await TimeZoneLogic.SetTimeZone(Context.Guild, Context.Channel, user, tz);
        }

        [Command("set-rs-queue-role")]
        [Summary("set-rs-queue-role <role>|set the role for RS queue access (used by afk command)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetRsQueueRole(string role)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var rsRole = Context.Guild.FindRole(role);
            if (rsRole == null)
            {
                await Context.Channel.BotResponse("Unknown role: " + role, ResponseType.error);
                return;
            }

            Services.State.Set(Context.Guild.Id, "rs-queue-role", rsRole.Id);
            await Context.Channel.BotResponse("RS Queue role is set to: " + rsRole.Name, ResponseType.success);
        }

        [Command("set-bot-log-channel")]
        [Summary("set-bot-log-channel <channel>|log channel for the bot")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetBotLogChannel(SocketTextChannel channel)
        {
            await CleanupService.DeleteCommand(Context.Message);

            Services.State.Set(Context.Guild.Id, "bot-log-channel", channel.Id);
            await Context.Channel.BotResponse("Bot log channel is set to: " + channel.Name, ResponseType.success);
        }

        [Command("recruit")]
        [Summary("recruit <userName> <corpName> <rsLevel>|recruit user to a corp and RS level")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task SetBotLogChannel(string userName, string corpName, int rsLevel)
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

        [Command("guestify")]
        [Summary("guestify <userName>|remove all roles and add guest role")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task Guestify(string userName)
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

            await RoleLogic.Guestify(Context.Guild, Context.Channel, user, alliance);
        }
    }
}