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
            var sb = new StringBuilder();

            var alts = alliance.Alts.OrderBy(x => x.AltUserId != null
                ? guild.GetUser(x.OwnerUserId)?.DisplayName ?? "<unknown discord user>"
                : x.Name).ToList();

            var batchSize = 50;
            var batchCount = (alts.Count / batchSize) + (alts.Count % batchSize == 0 ? 0 : 1);
            for (var batch = 0; batch < batchCount; batch++)
            {
                sb.Clear();

                foreach (var alt in alts.Skip(batch * batchSize).Take(batchSize))
                {
                    var owner = guild.GetUser(alt.OwnerUserId);
                    if (owner == null)
                        continue;

                    sb.Append(alliance.GetUserCorpIcon(owner));

                    if (alt.AltUserId != null)
                    {
                        var altUser = guild.GetUser(alt.AltUserId.Value);
                        if (altUser != null)
                        {
                            sb.Append('`').Append(altUser.DisplayName).Append('`');
                            var relevantAltRoles = GetRelevantRsWsRoles(altUser);
                            if (relevantAltRoles.Count > 0)
                            {
                                sb
                                    .Append(' ')
                                    .AppendJoin(" ", relevantAltRoles.Select(x => x.Mention));
                            }
                        }
                        else
                        {
                            sb.Append("<unknown discord user>");
                        }
                    }
                    else
                    {
                        sb.Append('`').Append(alt.Name).Append('`');
                    }

                    sb
                        .Append(" owned by ")
                        .AppendLine(owner.Mention);
                }

                var eb = new EmbedBuilder()
                    .WithTitle("ALTS")
                    .WithDescription(sb.ToString());

                await channel.SendMessageAsync(embed: eb.Build());
            }
        }

        public static List<SocketRole> GetHighestRsRole(SocketGuildUser user)
        {
            return user.Roles
                .Where(x => x.Name.StartsWith("rs", StringComparison.InvariantCultureIgnoreCase))
                .OrderByDescending(x => x.Position)
                .Take(1)
                .ToList();
        }

        public static List<SocketRole> GetRelevantRsWsRoles(SocketGuildUser user)
        {
            return user.Roles
                .Where(x => x.Name.StartsWith("ws", StringComparison.InvariantCultureIgnoreCase)
                         || x.Name.StartsWith("rs", StringComparison.InvariantCultureIgnoreCase))
                .OrderByDescending(x => x.Position)
                .ToList();
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

            var now = DateTime.UtcNow;

            var eb = new EmbedBuilder()
                .WithTitle("Who is " + user.DisplayName + "?");

            var corpIcon = alliance.GetUserCorpIcon(user, false, true);
            if (!string.IsNullOrEmpty(corpIcon))
                eb.AddField("corporation", corpIcon);

            eb.AddField("roles", string.Join(", ", user.Roles
                .Where(x => !x.IsEveryone)
                .OrderByDescending(x => x.Position)
                .Select(x => x.Mention)));

            AfkLogic.AfkEntry afk = null;
            TimeZoneInfo timeZone = null;

            var isAlt = alliance.Alts.Find(x => x.AltUserId == user.Id);
            if (isAlt != null)
            {
                var owner = guild.GetUser(isAlt.OwnerUserId);
                if (owner != null)
                {
                    var ownerText = owner.Mention;

                    var relevantOwnerRoles = owner
                        .Roles
                        .Where(x => x.Name.Contains("ws", StringComparison.InvariantCultureIgnoreCase)
                                 || x.Name.Contains("rs", StringComparison.InvariantCultureIgnoreCase))
                        .ToList();

                    if (relevantOwnerRoles.Count > 0)
                    {
                        ownerText += " " + string.Join(" ", relevantOwnerRoles
                            .Where(x => !x.IsEveryone)
                            .OrderByDescending(x => x.Position)
                            .Select(x => x.Mention));
                    }

                    eb.AddField("owner of this alt", ownerText);

                    timeZone = TimeZoneLogic.GetUserTimeZone(guild.Id, owner.Id);
                    afk = AfkLogic.GetUserAfk(guild.Id, owner.Id);
                }
            }
            else
            {
                timeZone = TimeZoneLogic.GetUserTimeZone(guild.Id, user.Id);
                afk = AfkLogic.GetUserAfk(guild.Id, user.Id);
            }

            if (afk != null)
                eb.AddField("afk", "AFK for " + afk.EndsOn.Subtract(now).ToIntervalStr());

            if (timeZone != null)
            {
                eb.AddField("time zone", timeZone.StandardName
                    + ", UTC" + (timeZone.BaseUtcOffset.TotalMilliseconds >= 0 ? "+" : "")
                    + (timeZone.BaseUtcOffset.Minutes == 0
                        ? timeZone.BaseUtcOffset.Hours.ToStr()
                        : timeZone.BaseUtcOffset.ToString(@"h\:mm"))
                    + "\nlocal time: **" + TimeZoneInfo.ConvertTimeFromUtc(now, timeZone).ToString("yyyy.MM.dd HH:mm", CultureInfo.InvariantCulture) + "**");
            }

            var alts = alliance.Alts.Where(x => x.OwnerUserId == user.Id).ToList();
            if (alts.Count > 0)
            {
                var altsText = "";
                foreach (var alt in alts)
                {
                    if (alt.AltUserId != null)
                    {
                        var altUser = guild.GetUser(alt.AltUserId.Value);
                        if (altUser != null)
                        {
                            altsText += altUser.Mention;
                            var relevantAltRoles = GetRelevantRsWsRoles(altUser);
                            if (relevantAltRoles.Count > 0)
                            {
                                altsText += " " + string.Join(" ", relevantAltRoles.Select(x => x.Mention));
                            }

                            altsText += "\n";
                        }
                        else
                        {
                            altsText += "<unknown discord user>\n";
                        }
                    }
                    else
                    {
                        altsText += "`" + alt.Name + "`\n";
                    }
                }

                eb.AddField("alts", altsText);
            }

            await channel.SendMessageAsync(null, embed: eb.Build());
        }
    }
}