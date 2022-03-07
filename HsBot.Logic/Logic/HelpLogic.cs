namespace HsBot.Logic
{
    using System.Globalization;
    using System.Text;
    using Discord;
    using Discord.WebSocket;

    public static class HelpLogic
    {
        public static async Task ShowAllianceInfo(SocketGuild guild, ISocketMessageChannel channel, AllianceLogic.AllianceInfo alliance)
        {
            var allianceRole = guild.GetRole(alliance.RoleId);

            var eb = new EmbedBuilder()
                .WithTitle(alliance.Name ?? allianceRole.Name);

            foreach (var corp in alliance.Corporations.OrderByDescending(x => x.CurrentRelicCount))
            {
                var role = guild.GetRole(corp.RoleId);
                if (role == null)
                {
                    await channel.BotResponse("Corp has no member role!", ResponseType.error);
                    return;
                }

                var level = GetCorpLevel(corp.CurrentRelicCount);
                eb.AddField(corp.IconMention + " " + (corp.FullName ?? role.Name) + " [" + corp.Abbreviation + "]", "level: " + level.ToStr() + ", relics: " + corp.CurrentRelicCount.ToStr(), true);
            }

            await channel.SendMessageAsync(embed: eb.Build());
        }

        public static int GetCorpLevel(int relicCount)
        {
            return relicCount switch
            {
                >= 20000 => 16,
                >= 16000 => 15,
                >= 13000 => 14,
                >= 11000 => 13,
                >= 9000 => 12,
                >= 7000 => 11,
                >= 5000 => 10,
                >= 3000 => 9,
                >= 2000 => 8,
                >= 1000 => 7,
                >= 500 => 6,
                >= 250 => 5,
                >= 100 => 4,
                >= 30 => 3,
                >= 1 => 2,
                _ => 1,
            };
        }

        public static async Task ShowAllianceAlts(SocketGuild guild, ISocketMessageChannel channel, AllianceLogic.AllianceInfo alliance)
        {
            var eb = new EmbedBuilder()
                .WithTitle("ALTS");

            var usersWithAlts = alliance.Alts
                .Select(x => x.OwnerUserId)
                .Distinct()
                .Select(x => guild.GetUser(x))
                .Where(x => x != null)
                .OrderByDescending(x => alliance.Alts.Count(y => y.OwnerUserId == x.Id))
                .ThenBy(x => x.DisplayName);

            var sb = new StringBuilder();
            foreach (var user in usersWithAlts)
            {
                sb.Clear();
                foreach (var alt in alliance.Alts.Where(x => x.OwnerUserId == user.Id))
                {
                    var name = alt.Name;
                    if (alt.AltUserId != null)
                    {
                        var altUser = guild.GetUser(alt.AltUserId.Value);
                        name = altUser?.DisplayName ?? "<unknown discord user>";
                    }

                    sb.AppendLine(name);
                }

                eb.AddField(alliance.GetUserCorpIcon(user) + user.DisplayName, sb.ToString(), true);
            }

            await channel.SendMessageAsync(embed: eb.Build());
        }

        public static async Task ShowCorpMembers(SocketGuild guild, ISocketMessageChannel channel, AllianceLogic.AllianceInfo alliance, AllianceLogic.Corp corp)
        {
            var corpRole = guild.GetRole(corp.RoleId);
            if (corpRole == null)
            {
                await channel.BotResponse("Corp has no member role!", ResponseType.error);
                return;
            }

            var now = DateTime.UtcNow;
            var sb = new StringBuilder();

            var realAccounts = guild.Users
                .Where(x => x.Roles.Any(y => y.Id == corpRole.Id) && !alliance.Alts.Any(y => y.AltUserId == x.Id))
                .OrderBy(x => x.DisplayName);

            foreach (var user in realAccounts)
            {
                sb
                    .Append(alliance.GetUserCorpIcon(user))
                    .Append(user.DisplayName);

                var tz = TimeZoneLogic.GetUserTimeZone(guild.Id, user.Id);
                if (tz != null)
                {
                    sb
                        .Append(" (**")
                        .Append(TimeZoneInfo.ConvertTimeFromUtc(now, tz).ToString("HH:mm", CultureInfo.InvariantCulture))
                        .Append("**)");
                }

                var afk = AfkLogic.GetUserAfk(guild.Id, user.Id);
                if (afk != null)
                {
                    sb
                        .Append(" (AFK for **")
                        .Append(afk.EndsOn.Subtract(now).ToIntervalStr())
                        .Append("**)");
                }

                var alts = alliance.Alts.Where(x => x.OwnerUserId == user.Id).ToList();
                if (alts.Count > 0)
                {
                    sb
                        .Append(" (alt: ")
                        .AppendJoin(", ", alts.Select(x => x.AltUserId != null
                            ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>"
                            : x.Name))
                        .Append(')');
                }

                sb.AppendLine();
            }

            var eb = new EmbedBuilder()
                .WithTitle("Members of " + corp.FullName)
                .WithDescription(sb.ToString());

            await channel.SendMessageAsync(embed: eb.Build());
        }

        public static async Task ShowRoleMembers(SocketGuild guild, ISocketMessageChannel channel, AllianceLogic.AllianceInfo alliance, SocketRole role)
        {
            var now = DateTime.UtcNow;
            var sb = new StringBuilder();
            var realAccounts = guild.Users
                .Where(x => x.Roles.Any(y => y.Id == role.Id) && !alliance.Alts.Any(y => y.AltUserId == x.Id))
                .OrderBy(x => x.DisplayName)
                .ToList();

            var batchSize = 50;
            var batchCount = (realAccounts.Count / batchSize) + (realAccounts.Count % batchSize == 0 ? 0 : 1);
            for (var batch = 0; batch < batchCount; batch++)
            {
                sb.Clear();
                foreach (var user in realAccounts.Skip(batch * batchSize).Take(batchSize))
                {
                    sb
                        .Append(alliance.GetUserCorpIcon(user))
                        .Append(user.DisplayName);

                    var tz = TimeZoneLogic.GetUserTimeZone(guild.Id, user.Id);
                    if (tz != null)
                    {
                        sb
                            .Append(" (**")
                            .Append(TimeZoneInfo.ConvertTimeFromUtc(now, tz).ToString("HH:mm", CultureInfo.InvariantCulture))
                            .Append("**)");
                    }

                    var afk = AfkLogic.GetUserAfk(guild.Id, user.Id);
                    if (afk != null)
                    {
                        sb
                            .Append(" (AFK for **")
                            .Append(afk.EndsOn.Subtract(now).ToIntervalStr())
                            .Append("**)");
                    }

                    var alts = alliance.Alts.Where(x => x.OwnerUserId == user.Id).ToList();
                    if (alts.Count > 0)
                    {
                        sb
                            .Append(" (alt: ")
                            .AppendJoin(", ", alts.Select(x => x.AltUserId != null
                                ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>"
                                : x.Name))
                            .Append(')');
                    }

                    sb.AppendLine();
                }

                var eb = new EmbedBuilder()
                    .WithTitle("Members of " + role.Name)
                    .WithDescription(sb.ToString());

                await channel.SendMessageAsync(embed: eb.Build());
            }
        }

        internal static async Task ShowMember(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user)
        {
            var alliance = AllianceLogic.GetAlliance(guild.Id);
            if (alliance == null)
                return;

            var afk = AfkLogic.GetUserAfk(guild.Id, user.Id);
            var timeZone = TimeZoneLogic.GetUserTimeZone(guild.Id, user.Id);

            var now = DateTime.UtcNow;

            var eb = new EmbedBuilder()
                .WithTitle("Who is " + user.DisplayName + "?");

            var corpIcon = alliance.GetUserCorpIcon(user, false, true);
            if (!string.IsNullOrEmpty(corpIcon))
            {
                eb.AddField("corp", corpIcon);
            }

            eb
                .AddField("roles", string.Join(", ", user.Roles
                    .Where(x => !x.IsEveryone)
                    .OrderByDescending(x => x.Position)
                    .Select(x => x.Name)))
                .AddField("afk", afk != null
                    ? "AFK for " + afk.EndsOn.Subtract(now).ToIntervalStr()
                    : "-")
                .AddField("timezone", timeZone != null
                    ? timeZone.StandardName
                        + "\nUTC" + (timeZone.BaseUtcOffset.TotalMilliseconds >= 0 ? "+" : "")
                            + (timeZone.BaseUtcOffset.Minutes == 0
                                ? timeZone.BaseUtcOffset.Hours.ToStr()
                                : timeZone.BaseUtcOffset.ToString(@"h\:mm")
                        + "\nlocal time: " + TimeZoneInfo.ConvertTimeFromUtc(now, timeZone).ToString("yyyy.MM.dd HH:mm", CultureInfo.InvariantCulture))
                    : "-");

            await channel.SendMessageAsync(null, embed: eb.Build());
        }
    }
}