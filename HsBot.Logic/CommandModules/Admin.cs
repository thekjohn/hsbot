namespace HsBot.Logic
{
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;

    [Summary("admin")]
    public class Admin : BaseModule
    {
        [Command("alts")]
        [Summary("alts [user]|display alts")]
        [RequireUserPermission(GuildPermission.ChangeNickname)]
        public async Task Alts(string user = null)
        {
            await Context.Message.DeleteAsync();

            SocketGuildUser otherUser = null;
            if (user != null)
            {
                otherUser = Context.Guild.FindUser(CurrentUser, user);
                if (otherUser == null)
                {
                    Services.Cleanup.RegisterForDeletion(10,
                        await Context.Channel.SendMessageAsync(":x: Can't find user: " + user + "."));
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
            await Context.Message.DeleteAsync();
            await AltsLogic.AddAlt(Context.Guild, Context.Channel, CurrentUser, name);
        }

        [Command("setalliance")]
        [Summary("setalliance <name> <abbrev>|set the main parameters of the alliance")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetAlliance(SocketRole role, string name, string abbrev)
        {
            await Context.Message.DeleteAsync();

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            alliance.RoleId = role.Id;
            alliance.Name = name;
            alliance.Abbreviation = abbrev;
            AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

            await ReplyAsync("alliance changed: " + role.Name);
        }

        [Command("setcorp")]
        [Summary("setcorplevel <name> <icon> <abbrev>|set the main parameters of a corp")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetCorp(SocketRole role, string fullName, string icon, string abbrev)
        {
            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            var corp = alliance.Corporations.Find(x => x.RoleId == role.Id);
            if (corp != null)
            {
                corp.FullName = fullName;
                corp.IconMention = icon;
                corp.Abbreviation = abbrev;
                AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

                await ReplyAsync("corp changed: " + role.Name);
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
            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            var corp = alliance.Corporations.Find(x => x.RoleId == role.Id);
            if (corp == null)
            {
                corp = new AllianceLogic.Corp
                {
                    RoleId = role.Id
                };

                alliance.Corporations.Add(corp);
                AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

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

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);

            var corp = alliance.Corporations.Find(x => x.RoleId == role.Id);
            if (corp != null)
            {
                corp.CurrentLevel = level;
                corp.CurrentRelicCount = relics;
                AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

                await ReplyAsync("corp changed: " + role.Name);
            }
            else
            {
                await ReplyAsync("unknown corp: " + role.Name);
            }
        }

        [Command("setrstimeout")]
        [Summary("setrstimeout <level> <minutes>|change the activity timeout of a specific RS queue")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task SetRsTimeout(int level, int minutes)
        {
            await Context.Message.DeleteAsync();

            var stateId = Services.State.GetId("rs-queue-timeout", (ulong)level);
            var currentValue = Services.State.Get<int>(Context.Guild.Id, stateId);

            Services.State.Set(Context.Guild.Id, stateId, minutes);
            await ReplyAsync("Timeout for RS" + level.ToStr() + " has been changed to " + minutes.ToStr() + "."
                + (currentValue != 0 ? " Previous value was " + currentValue.ToStr() + "." : ""));
        }

        [Command("setrstimeout")]
        [Summary("setrstimeout <minutes>|change the activity timeout of a all RS queues")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task SetRsTimeout(int minutes)
        {
            await Context.Message.DeleteAsync();

            for (var level = 1; level <= 12; level++)
            {
                var stateId = Services.State.GetId("rs-queue-timeout", (ulong)level);
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

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            alliance.AllyRoleId = role.Id;
            alliance.AllyIcon = icon;
            AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

            await ReplyAsync("ally role changed: " + role.Name);
        }

        [Command("falsestart")]
        [Summary("falsestart <runNumber>|invalidate an RS run")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task FalseStartRun(int runNumber)
        {
            await Context.Message.DeleteAsync();
            await FalseStartRun(Context.Guild, Context.Channel, runNumber);
        }

        private static async Task FalseStartRun(SocketGuild guild, ISocketMessageChannel channel, int runNumber)
        {
            var queueStateId = Services.State.GetId("rs-log", (ulong)runNumber);
            var queue = Services.State.Get<Rs.RsQueueEntry>(guild.Id, queueStateId);
            if (queue == null)
            {
                Services.Cleanup.RegisterForDeletion(10,
                    await channel.SendMessageAsync(":x: Can't find run #" + runNumber.ToStr()));
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

            Services.Cleanup.RegisterForDeletion(10,
                await channel.SendMessageAsync("Run #" + runNumber.ToStr() + " is successfuly reset."));
        }

        [Command("startwssignup")]
        [Summary("startwssignup 5d3h|start a new WS signup which ends in 5d3h from now")]
        public async Task StartWsSignup(string endsFromNow)
        {
            await Context.Message.DeleteAsync();

            await WsSignupLogic.StartNew(Context.Guild, Context.Channel, CurrentUser, endsFromNow.AddToUtcNow());
        }
    }
}