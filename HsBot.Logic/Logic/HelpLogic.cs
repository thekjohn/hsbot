namespace HsBot.Logic;

public static class HelpLogic
{
    public static async Task ShowMostUsedCommands(SocketGuild guild, ISocketMessageChannel channel)
    {
        var eb = new EmbedBuilder()
            .WithTitle("JARVIS ONBOARDING")
            .WithColor(Color.Green)
            .AddField("get the list of available timezones", "`" + DiscordBot.CommandPrefix + "timezone-list`")
            .AddField("set your own timezone", "`" + DiscordBot.CommandPrefix + "timezone-set 55` where 55 is the # number of the timezone you looked up previously")
            .AddField("flag when you are AFK (mainly during WS)", "`" + DiscordBot.CommandPrefix + "afk 5h10m` During AFK, you lose access to the RS queue channel.")
            .AddField("flag when you are no longer AFK", "`" + DiscordBot.CommandPrefix + "back` You get back your RS queue access.")
            .AddField("set your own RS roles", "`" + DiscordBot.CommandPrefix + "rsrole`")
            .AddField("enter your highest RS queue", "`" + DiscordBot.CommandPrefix + "in`. Short form is `" + DiscordBot.CommandPrefix + "i`")
            .AddField("enter an RS queue", "`" + DiscordBot.CommandPrefix + "in 10` Where 10 is the RS level. Short form is `" + DiscordBot.CommandPrefix + "i 10`")
            .AddField("leave an RS queue", "`" + DiscordBot.CommandPrefix + "out 10` Where 10 is the RS level. Short form is `" + DiscordBot.CommandPrefix + "o 10`")
            .AddField("leave all RS queues", "`" + DiscordBot.CommandPrefix + "out`. Short form is `" + DiscordBot.CommandPrefix + "o`")
            .AddField("get the list of commands", "`" + DiscordBot.CommandPrefix + "help`")
            .AddField("get the defails of a commands", "`" + DiscordBot.CommandPrefix + "help sga`")
            .AddField("get the overview of the alliance (corps)", "`" + DiscordBot.CommandPrefix + "sga`")
            .AddField("get the overview of all alts in alliance", "`" + DiscordBot.CommandPrefix + "sga alts`")
            .AddField("get the list of the members of a corp", "`" + DiscordBot.CommandPrefix + "sga ge`")
            .AddField("get the list of the members of a role", "`" + DiscordBot.CommandPrefix + "sga ally`")
            .AddField("list your alts", "`" + DiscordBot.CommandPrefix + "alts`")
            .WithFooter("This message will self-destruct in 60 seconds.");

        CleanupService.RegisterForDeletion(60,
            await channel.SendMessageAsync(null, embed: eb.Build()));
    }

    public static async Task ShowMostUsedWsCommands(SocketGuild guild, ISocketMessageChannel channel)
    {
        var eb = new EmbedBuilder()
            .WithTitle("JARVIS ONBOARDING - WS COMMANDS")
            .WithColor(Color.Green)
            .AddField("alert the entire WS team", "`" + DiscordBot.CommandPrefix + "alert incoming enemies!!!`")
            .AddField("register a reminder for yourself", "`" + DiscordBot.CommandPrefix + "remind me 2h15m send drone`")
            .AddField("register a reminder for somebody", "`" + DiscordBot.CommandPrefix + "remind {name} 18h send your BS back`")
            .AddField("flag when you are AFK", "`" + DiscordBot.CommandPrefix + "afk 5h10m` During AFK, you lose access to the RS queue channel.")
            .AddField("flag when you are no longer AFK", "`" + DiscordBot.CommandPrefix + "back` You get back your RS queue access.")
            .AddField("display of the classification table of the WS team", "`" + DiscordBot.CommandPrefix + "classify`")
            .AddField("display of the classification table of the members of a role", "`" + DiscordBot.CommandPrefix + "classify rs10`")
            .AddField("show the list of the registered module filters", "`" + DiscordBot.CommandPrefix + "mflist`")
            .WithFooter("This message will self-destruct in 60 seconds.");

        CleanupService.RegisterForDeletion(60,
            await channel.SendMessageAsync(null, embed: eb.Build()));
    }

    public static async Task ShowMostUsedRsCommands(SocketGuild guild, ISocketMessageChannel channel)
    {
        var eb = new EmbedBuilder()
            .WithTitle("JARVIS ONBOARDING - RS COMMANDS")
            .WithColor(Color.Green)
            .AddField("set your own RS roles", "`" + DiscordBot.CommandPrefix + "rsrole`")
            .AddField("enter your highest RS queue", "`" + DiscordBot.CommandPrefix + "in`. Short form is `" + DiscordBot.CommandPrefix + "i`")
            .AddField("enter an RS queue", "`" + DiscordBot.CommandPrefix + "in 10` Where 10 is the RS level. Short form is `" + DiscordBot.CommandPrefix + "i 10`")
            .AddField("leave an RS queue", "`" + DiscordBot.CommandPrefix + "out 10` Where 10 is the RS level. Short form is `" + DiscordBot.CommandPrefix + "o 10`")
            .AddField("leave all RS queues", "`" + DiscordBot.CommandPrefix + "out`. Short form is `" + DiscordBot.CommandPrefix + "o`")
            .WithFooter("This message will self-destruct in 60 seconds.");

        CleanupService.RegisterForDeletion(60,
            await channel.SendMessageAsync(null, embed: eb.Build()));
    }

    public static async Task ShowMostUsedGreeterCommands(SocketGuild guild, ISocketMessageChannel channel)
    {
        var eb = new EmbedBuilder()
            .WithTitle("GREETER COMMANDS")
            .AddField("Recruit to a corporation", "`" + DiscordBot.CommandPrefix + "recruit <userName> <corpName> <rsLevel>`")
            .AddField("Promote to WS guest (WS signup access)", "`" + DiscordBot.CommandPrefix + "wsguest <userName>`")
            .AddField("Promote to Ally (RS queue access)", "`" + DiscordBot.CommandPrefix + "ally <userName> <rsLevel>`")
            .AddField("Demote to guest, remove all roles", "`" + DiscordBot.CommandPrefix + "demote <userName>`")
            .AddField("Set name for a guest/ally/WS guest", "`" + DiscordBot.CommandPrefix + "setname <userName> <ingameName> [corpName]`\nex: `!setname \"He Was Called Special\" \"BraveTempest81\" \"Blue Cat Order\"`")
            .WithFooter("This message will self-destruct in 60 seconds.");

        CleanupService.RegisterForDeletion(60,
            await channel.SendMessageAsync(null, embed: eb.Build()));
    }

    public static async Task ShowAllianceInfo(SocketGuild guild, ISocketMessageChannel channel, AllianceLogic.AllianceInfo alliance)
    {
        var allianceRole = guild.GetRole(alliance.RoleId);

        var eb = new EmbedBuilder()
            .WithTitle(alliance.Name ?? allianceRole.Name)
            .WithColor(Color.Green);

        foreach (var corp in alliance.Corporations.OrderByDescending(x => x.CurrentRelicCount))
        {
            var role = guild.GetRole(corp.RoleId);
            if (role == null)
            {
                await channel.BotResponse("Corp has no member role!", ResponseType.error);
                return;
            }

            var level = GetCorpLevel(corp.CurrentRelicCount);
            var bonus = GetCorpArtifactBonus(level);
            eb.AddField(corp.IconMention + " " + (corp.FullName ?? role.Name) + " [" + corp.Abbreviation + "]", corp.CurrentRelicCount.ToStr() + " relics, " + bonus.ToStr() + "% bonus", true);
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

    public static int GetCorpArtifactBonus(int level)
    {
        return level switch
        {
            2 => 1,
            3 => 2,
            4 => 3,
            5 => 4,
            6 => 6,
            7 => 8,
            8 => 10,
            9 => 12,
            10 => 14,
            11 => 16,
            12 => 18,
            >= 13 => 20,
            _ => 0,
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
                .WithDescription(sb.ToString())
                .WithColor(Color.Green);

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

            var afk = await AfkLogic.GetUserAfk(guild, user);
            if (afk != null)
            {
                sb
                    .Append(' ')
                    .Append(guild.GetEmoteReference("afk"))
                    .Append(' ')
                    .Append(afk.EndsOn.Subtract(now).ToIntervalStr());
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
            .WithColor(Color.Green)
            .WithDescription(sb.ToString())
            .WithFooter("This message will self-destruct in 30 seconds.");

        CleanupService.RegisterForDeletion(30,
            await channel.SendMessageAsync(embed: eb.Build()));
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

                var afk = await AfkLogic.GetUserAfk(guild, user);
                if (afk != null)
                {
                    sb
                        .Append(' ')
                        .Append(guild.GetEmoteReference("afk"))
                        .Append(' ')
                        .Append(afk.EndsOn.Subtract(now).ToIntervalStr());
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
                .WithColor(Color.Green)
                .WithDescription(sb.ToString())
                .WithFooter("This message will self-destruct in 30 seconds.");

            CleanupService.RegisterForDeletion(30,
                await channel.SendMessageAsync(embed: eb.Build()));
        }
    }

    internal static async Task ShowUser(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user)
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

                var relevantOwnerRoles = owner.Roles
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
                afk = await AfkLogic.GetUserAfk(guild, owner);
            }
        }
        else
        {
            timeZone = TimeZoneLogic.GetUserTimeZone(guild.Id, user.Id);
            afk = await AfkLogic.GetUserAfk(guild, user);
        }

        if (afk != null)
            eb.AddField("afk", guild.GetEmoteReference("afk") + " " + afk.EndsOn.Subtract(now).ToIntervalStr());

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
                        altsText += altUser.GetPermissions(channel as IGuildChannel).ViewChannel
                            ? altUser.Mention
                            : altUser.DisplayName;

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

        var tech = CompendiumLogic.GetUserData(guild.Id, user.Id);
        if (tech?.map != null)
        {
            var ml = BuildModuleList(tech, nameof(tech.map.dispatch), nameof(tech.map.relicdrone));
            if (!string.IsNullOrEmpty(ml))
                eb.AddField("trade modules", ml);
            ml = BuildModuleList(tech, nameof(tech.map.miningboost), nameof(tech.map.hydrobay), nameof(tech.map.enrich), nameof(tech.map.remote), nameof(tech.map.miningunity), nameof(tech.map.crunch), nameof(tech.map.genesis));
            if (!string.IsNullOrEmpty(ml))
                eb.AddField("mining modules", ml);
            ml = BuildModuleList(tech, nameof(tech.map.battery), nameof(tech.map.laser), nameof(tech.map.mass), nameof(tech.map.barrage), nameof(tech.map.dart));
            if (!string.IsNullOrEmpty(ml))
                eb.AddField("weapon modules", ml);
            ml = BuildModuleList(tech, nameof(tech.map.delta), nameof(tech.map.omega), nameof(tech.map.blast), nameof(tech.map.area));
            if (!string.IsNullOrEmpty(ml))
                eb.AddField("shield modules", ml);
            ml = BuildModuleList(tech, nameof(tech.map.emp), nameof(tech.map.teleport), nameof(tech.map.warp), nameof(tech.map.unity), nameof(tech.map.stealth), nameof(tech.map.fortify), nameof(tech.map.impulse), nameof(tech.map.rocket), nameof(tech.map.suppress), nameof(tech.map.destiny), nameof(tech.map.barrier), nameof(tech.map.vengeance), nameof(tech.map.deltarocket), nameof(tech.map.leap), nameof(tech.map.bond), nameof(tech.map.omegarocket));
            if (!string.IsNullOrEmpty(ml))
                eb.AddField("support modules", ml);

            var matchingFilters = ModuleFilterLogic.GetAllModuleFilters(guild.Id)
                .Where(filter => tech.TestFilter(filter, out _))
                .OrderBy(filter => filter.Name)
                .ToList();

            if (matchingFilters.Count > 0)
                eb.AddField("classification", string.Join(' ', matchingFilters.Select(filter => tech.GetClassification(filter))));
        }

        eb.WithFooter("This message will self-destruct in 30 seconds.");

        CleanupService.RegisterForDeletion(30,
            await channel.SendMessageAsync(null, embed: eb.Build()));
    }

    private static string BuildModuleList(CompendiumResponse response, params string[] modules)
    {
        var sb = new StringBuilder();
        foreach (var moduleName in modules)
        {
            var property = CompendiumResponseMap.GetByName(moduleName);
            var level = (property?.GetValue(response.map) as CompendiumResponseModule)?.level;
            if (level == null)
                continue;

            if (sb.Length > 0)
                sb.Append(' ');

            var name = CompendiumResponseMap.GetShortName(moduleName);
            sb.Append(name);
            sb.Append(" (");
            sb.Append(level.ToString());
            sb.Append(')');
        }

        return sb.ToString();
    }
}
