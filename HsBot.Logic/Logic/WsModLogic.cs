namespace HsBot.Logic;

public static class WsModLogic
{
    public static int[] MinerCapacity { get; } = new[] { 0, 50, 250, 600, 1200, 2000, 2500 };
    public static int[] HydroBayCapacity { get; } = new[] { 0, 50, 75, 110, 170, 250, 370, 550, 850, 1275, 2000 };

    internal static async Task ClassifyTeam(SocketGuild guild, IMessageChannel channel)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        if (!WsLogic.GetWsTeamByChannel(guild, channel.Id, out var team, out var teamRole))
        {
            await channel.BotResponse("You have to use this command in a WS battleroom!", ResponseType.error);
            return;
        }

        await DeleteTeamsOpsPanel(guild, team);

        var entries = GetRoleEntries(guild, teamRole, null);
        if (entries.Count == 0)
            return;

        var longestName = entries.Max(x => x.Name.Length);

        var sb = new StringBuilder()
            .Append("classification of ").Append(teamRole.Name).Append(" ```")
            .Append("NAME".PadRight(longestName))
            .Append(' ')
            .AppendLine("FILTER MATCHES");

        var filters = ModuleFilterLogic.GetAllModuleFilters(guild.Id).OrderBy(x => x.Name).ToList();
        foreach (var entry in entries)
        {
            sb
                .Append(entry.Name.PadRight(longestName))
                .Append(' ')
                .AppendJoin(", ", filters
                    .Where(filter => filter.Modules.Any(x => x.Level > 0) && entry.Response.TestFilter(filter, out _))
                    .Select(filter => entry.Response.GetClassification(filter)))
                .AppendLine();
        }

        sb.Append("```");

        var sent = await channel.SendMessageAsync(sb.ToString());
        WsLogic.ChangeWsTeam(guild.Id, ref team, t =>
        {
            t.OpsPanelChannelId = channel.Id;
            t.OpsPanelMessageId = sent.Id;
        });
    }

    internal static async Task ClassifyRole(SocketGuild guild, IMessageChannel channel, SocketRole role)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var entries = GetRoleEntries(guild, role, null);
        var longestName = entries.Max(x => x.Name.Length);

        var filters = ModuleFilterLogic.GetAllModuleFilters(guild.Id).OrderBy(x => x.Name).ToList();
        var batchSize = 20;
        var batchCount = (entries.Count / batchSize) + (entries.Count % batchSize == 0 ? 0 : 1);

        for (var batch = 0; batch < batchCount; batch++)
        {
            var sb = new StringBuilder()
            .Append("classification of ")
            .Append(role.Name)
            .Append(" ```")
            .Append("NAME".PadRight(longestName))
            .Append(' ')
            .AppendLine("FILTER MATCHES");

            foreach (var entry in entries.Skip(batch * batchSize).Take(batchSize))
            {
                sb
                    .Append(entry.Name.PadRight(longestName))
                    .Append(' ')
                    .AppendJoin(", ", filters
                        .Where(filter => filter.Modules.Any(x => x.Level > 0) && entry.Response.TestFilter(filter, out _))
                        .Select(filter => entry.Response.GetClassification(filter)))
                    .AppendLine();
            }

            sb.Append("```");

            await channel.SendMessageAsync(sb.ToString());
        }
    }

    internal static async Task WsModMining(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string filterName)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        if (!WsLogic.GetWsTeamByChannel(guild, channel.Id, out var team, out var teamRole))
        {
            await channel.BotResponse("You have to use this command in a WS battleroom!", ResponseType.error);
            return;
        }

        await DeleteTeamsOpsPanel(guild, team);

        var filter = filterName != null ? ModuleFilterLogic.GetModuleFilter(guild.Id, filterName) : null;
        var entries = GetRoleEntries(guild, teamRole, filter);
        var sb = new StringBuilder()
            .Append("mining").Append(filterName != null ? " + " + filterName : "")
            .Append("```")
            .Append(GetModulesTable(guild, team,
                new[] { "miner", "miningboost", "remote", "miningunity", "genesis", "enrich", "crunch", "teleport", "barrier", "suppress", "leap", "warp", "relicdrone", "mscap", "mscaphbe" }
                , filterName))
            .Append("```");

        var sent = await channel.SendMessageAsync(sb.ToString());
        WsLogic.ChangeWsTeam(guild.Id, ref team, t =>
        {
            t.OpsPanelChannelId = channel.Id;
            t.OpsPanelMessageId = sent.Id;
        });
    }

    internal static async Task WsModDefense(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string filterName)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        if (!WsLogic.GetWsTeamByChannel(guild, channel.Id, out var team, out var teamRole))
        {
            await channel.BotResponse("You have to use this command in a WS battleroom!", ResponseType.error);
            return;
        }

        await DeleteTeamsOpsPanel(guild, team);

        var filter = filterName != null ? ModuleFilterLogic.GetModuleFilter(guild.Id, filterName) : null;
        var sb = new StringBuilder()
            .Append("defense").Append(filterName != null ? " + " + filterName : "")
            .Append("```")
            .Append(GetModulesTable(guild, team,
                new[] { "bs", "laser", "barrage", "blast", "omega", "warp", "teleport", "leap", "barrier", "suppress", "bond", "fortify", "emp" }
                , filterName))
            .Append("```");

        var sent = await channel.SendMessageAsync(sb.ToString());
        WsLogic.ChangeWsTeam(guild.Id, ref team, t =>
        {
            t.OpsPanelChannelId = channel.Id;
            t.OpsPanelMessageId = sent.Id;
        });
    }

    internal static string GetModulesTable(SocketGuild guild, WsTeam team, string[] moduleNames, string filterName)
    {
        var filter = filterName != null ? ModuleFilterLogic.GetModuleFilter(guild.Id, filterName) : null;
        var teamRole = guild.GetRole(team.RoleId);
        var entries = GetRoleEntries(guild, teamRole, filter);
        var longestName = Math.Max("MODULES".Length, entries.Max(x => x.Name.Length));

        var sb = new StringBuilder();
        sb
            .Append("MODULE".PadRight(longestName))
            .Append(' ');

        if (filter != null)
        {
            moduleNames =
                filter.Modules.Select(x => x.Name)
                .Concat(
                moduleNames.Where(x => !filter.Modules.Any(m => string.Equals(m.Name, x, StringComparison.InvariantCultureIgnoreCase)))
                )
                .ToArray();
        }

        if (filter != null)
        {
            sb
                .Append("score")
                .Append(' ');
        }

        foreach (var moduleName in moduleNames)
        {
            var shortName = CompendiumResponseMap.GetShortName(moduleName) ?? moduleName;
            sb
                .Append(shortName)
                .Append(' ');
        }

        sb.AppendLine("group");

        foreach (var entry in entries)
        {
            sb
                .Append(entry.Name.PadRight(longestName))
                .Append(' ');

            if (entry.Response == null || entry.Response.array.Length < 5)
            {
                sb.AppendLine();
                continue;
            }

            if (filter != null)
            {
                sb
                    .Append(entry.Score.ToStr().PadLeft(5))
                    .Append(' ');
            }

            foreach (var moduleName in moduleNames)
            {
                var shortName = CompendiumResponseMap.GetShortName(moduleName);

                var value = moduleName switch
                {
                    "mscap" => MinerCapacity[entry.Response.map?.miner?.level ?? 0].ToStr(),
                    "mscaphbe" => (MinerCapacity[entry.Response.map?.miner?.level ?? 0]
                        + HydroBayCapacity[entry.Response.map?.hydrobay?.level ?? 0]).ToStr(),
                    _ => entry.Response.map != null
                        ? (CompendiumResponseMap.GetByName(moduleName).GetValue(entry.Response.map) as CompendiumResponseModule)?.level.ToStr()
                        : null
                };

                sb
                    .Append((value ?? "-").PadLeft(shortName.Length))
                    .Append(' ');
            }

            sb.AppendLine(entry.Assignment?.MinerGroupNameMS);
        }

        return sb.ToString();
    }

    private static List<Entry> GetRoleEntries(SocketGuild guild, SocketRole role, ModuleFilter filter)
    {
        var team = WsLogic.GetWsTeamByRole(guild.Id, role);

        var mains = guild.Users
            .Where(x => x.Roles.Any(y => y.Id == role.Id))
            .Select(x => new Entry()
            {
                Name = x?.GetShortDisplayName(),
                Response = CompendiumLogic.GetUserData(guild.Id, x.Id),
                Assignment = team?.OpsAssignments.Find(y => y.UserId == x.Id),
            });

        var alts = team?.Members.Alts
            .Where(x => x.AltUserId == null)
            .Select(x => new Entry()
            {
                Name = x.Name,
                Response = null,// todo: support storing module data for discordless alts
                Assignment = team.OpsAssignments.Find(y => y.Alt?.Equals(x) == true),
            });

        var allEntry = mains;
        if (alts != null)
            allEntry = allEntry.Concat(alts);

        return allEntry
            .Where(entry =>
            {
                if (entry?.Name != null && entry.Response != null && entry.Response.TestFilter(filter, out var score))
                {
                    entry.Score = score;
                    return true;
                }
                return false;
            })
            .OrderBy(x => (x.Response?.array?.Length ?? 0) >= 5 ? 0 : 1)
            .ThenBy(x => -x.Score)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static async Task DeleteTeamsOpsPanel(SocketGuild guild, WsTeam team)
    {
        if (team.OpsPanelMessageId != 0)
        {
            try
            {
                await guild.GetTextChannel(team.OpsPanelChannelId).DeleteMessageAsync(team.OpsPanelMessageId);
            }
            catch (Exception)
            {
            }
        }
    }

    private class Entry
    {
        public string Name { get; set; }
        public CompendiumResponse Response { get; set; }
        public WsTeamOpsAssignment Assignment { get; set; }
        public int Score { get; set; }
    }
}
