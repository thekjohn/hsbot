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

                eb.AddField(alliance.GetUserCorpIcon(user) + " " + user.DisplayName, sb.ToString(), true);
            }

            await channel.SendMessageAsync(embed: eb.Build());
        }

        public static async Task ShowCorpInfo(SocketGuild guild, ISocketMessageChannel channel, AllianceLogic.AllianceInfo alliance, AllianceLogic.Corp corp)
        {
            var corpRole = guild.GetRole(corp.RoleId);
            if (corpRole == null)
            {
                await channel.BotResponse("Corp has no member role!", ResponseType.error);
                return;
            }

            var eb = new EmbedBuilder()
                .WithTitle(corp.FullName)
                .AddField("icon", corp.IconMention, true)
                .AddField("abbreviation", "[" + corp.Abbreviation + "]", true)
                .AddField("level", GetCorpLevel(corp.CurrentRelicCount).ToStr(), true)
                .AddField("relics", corp.CurrentRelicCount.ToStr(), true);

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
            foreach (var user in guild.Users.Where(x => x.Roles.Any(y => y.Id == corpRole.Id)).OrderBy(x => x.DisplayName))
            {
                sb.Append(user.DisplayName);
                var tz = TimeZoneLogic.GetUserTimeZone(guild.Id, user.Id);
                if (tz != null)
                {
                    sb
                        .Append(" (**")
                        .Append(TimeZoneInfo.ConvertTimeFromUtc(now, tz).ToString("HH:mm", CultureInfo.InvariantCulture))
                        .Append("**)");
                }

                var afk = AfkLogic.GetAfk(guild.Id, user.Id);
                if (afk != null)
                {
                    sb
                        .Append(" (AFK for **")
                        .Append(afk.EndsOn.Subtract(now).ToIntervalStr())
                        .Append("**)");
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
            foreach (var user in guild.Users.Where(x => x.Roles.Any(y => y.Id == role.Id)).OrderBy(x => x.DisplayName))
            {
                sb.Append(user.DisplayName);
                var tz = TimeZoneLogic.GetUserTimeZone(guild.Id, user.Id);
                if (tz != null)
                {
                    sb
                        .Append(" (**")
                        .Append(TimeZoneInfo.ConvertTimeFromUtc(now, tz).ToString("HH:mm", CultureInfo.InvariantCulture))
                        .Append("**)");
                }

                sb.AppendLine();
            }

            var eb = new EmbedBuilder()
                .WithTitle("Members of " + role.Name)
                .WithDescription(sb.ToString());

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }
}