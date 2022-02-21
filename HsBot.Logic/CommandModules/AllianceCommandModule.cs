namespace HsBot.Logic
{
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;

    [Summary("management")]
    public class AllianceCommandModule : BaseModule
    {
        [Command("setalliance")]
        [Summary("setalliance|set the main parameters of the alliance -  requires admin role")]
        public async Task SetAlliance(SocketRole role, string name, string abbreviation)
        {
            await Context.Message.DeleteAsync();

            if (!CurrentUser.Roles.Any(x => x.Permissions.Administrator))
            {
                await ReplyAsync("only administrators can use this command");
                return;
            }

            var alliance = GetAlliance(Context.Guild.Id);

            alliance.RoleId = role.Id;
            alliance.Name = name;
            alliance.Abbreviation = abbreviation;
            SaveAlliance(alliance);

            await ReplyAsync("alliance successfully changed: " + role.Name);
        }

        [Command("setcorp")]
        [Summary("setcorplevel|set the main parameters of a corp - requires admin role")]
        public async Task SetCorp(SocketRole role, string fullName, string iconMention, string abbreviation)
        {
            if (!CurrentUser.Roles.Any(x => x.Permissions.Administrator))
            {
                await ReplyAsync("only administrators can use this command");
                return;
            }

            var alliance = GetAlliance(Context.Guild.Id);

            var corp = alliance.Corporations.Find(x => x.RoleId == role.Id);
            if (corp != null)
            {
                corp.FullName = fullName;
                corp.IconMention = iconMention;
                corp.Abbreviation = abbreviation;
                SaveAlliance(alliance);

                await ReplyAsync("corp successfully changed: " + role.Name);
            }
            else
            {
                await ReplyAsync("unknown corp: " + role.Name);
            }
        }

        [Command("setcorplevel")]
        [Summary("setcorplevel|change the level and relic count of a corp - requires 'manage channels' role")]
        public async Task SetCorpLevel(SocketRole role, int level, int relicCount)
        {
            if (!CurrentUser.Roles.Any(x => x.Permissions.ManageChannels))
            {
                await ReplyAsync("only members with 'manage channels' role can use this command");
                return;
            }

            if (!CurrentUser.Roles.Any(x => x.Id == role.Id))
            {
                await ReplyAsync("only members within the specified corp can use this command");
                return;
            }

            var alliance = GetAlliance(Context.Guild.Id);

            var corp = alliance.Corporations.Find(x => x.RoleId == role.Id);
            if (corp != null)
            {
                corp.CurrentLevel = level;
                corp.CurrentRelicCount = relicCount;
                SaveAlliance(alliance);

                await ReplyAsync("corp successfully changed: " + role.Name);
            }
            else
            {
                await ReplyAsync("unknown corp: " + role.Name);
            }
        }

        [Command("addcorp")]
        [Summary("addcorp|add new corp to the alliance - requires admin role")]
        public async Task AddCorp(SocketRole role)
        {
            var currentUser = CurrentUser;
            if (!CurrentUser.Roles.Any(x => x.Permissions.Administrator))
            {
                await ReplyAsync("only administrators can use this command");
                return;
            }

            var alliance = GetAlliance(Context.Guild.Id);

            var corp = alliance.Corporations.Find(x => x.RoleId == role.Id);
            if (corp == null)
            {
                corp = new Corp
                {
                    RoleId = role.Id
                };

                alliance.Corporations.Add(corp);
                SaveAlliance(alliance);

                await ReplyAsync("corp created: " + role.Name);
            }
            else
            {
                await ReplyAsync("corp already added: " + role.Name);
            }
        }

        [Command("alliance")]
        [Summary("alliance|display alliance info, corps, levels")]
        public async Task ListCorp()
        {
            var alliance = GetAlliance(Context.Guild.Id);

            var allianceRole = Context.Guild.GetRole(alliance.RoleId);

            var msg = new EmbedBuilder()
                .WithTitle(alliance.Name ?? allianceRole.Name)
            ;

            foreach (var corp in alliance.Corporations.OrderByDescending(x => x.CurrentRelicCount))
            {
                var role = Context.Guild.GetRole(corp.RoleId);
                if (role != null)
                {
                    msg.AddField(corp.IconMention + " " + (corp.FullName ?? role.Name) + " [" + corp.Abbreviation + "]", "level: " + corp.CurrentLevel + ", relics: " + corp.CurrentRelicCount);
                }
            }

            await ReplyAsync(embed: msg.Build());
        }

        public static Alliance GetAlliance(ulong guildId)
        {
            return Services.State.Get<Alliance>(guildId, "alliance")
                ?? new Alliance();
        }

        private void SaveAlliance(Alliance alliance)
        {
            Services.State.Set(Context.Guild.Id, "alliance", alliance);
        }

        public class Alliance
        {
            public ulong RoleId { get; set; }
            public string Abbreviation { get; set; }
            public string Name { get; set; }

            public List<Corp> Corporations { get; set; } = new List<Corp>();

            public string GetUserCorpIcon(SocketGuildUser user)
            {
                var corp = Corporations.Find(c => user.Roles.Any(r => r.Id == c.RoleId));
                if (corp != null && !string.IsNullOrEmpty(corp.IconMention))
                    return corp.IconMention;

                return null;
            }
        }

        public class Corp
        {
            public ulong RoleId { get; set; }
            public string IconMention { get; set; }
            public string FullName { get; set; }
            public string Abbreviation { get; set; }
            public int CurrentLevel { get; set; }
            public int CurrentRelicCount { get; set; }
        }
    }
}