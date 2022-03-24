namespace HsBot.Logic
{
    using System.Text;
    using System.Threading.Tasks;
    using Discord.WebSocket;

    public static class WsModLogic
    {
        public static int[] MinerCapacity { get; } = new[] { 0, 50, 250, 600, 1200, 2000, 2500 };
        public static int[] HydroBayCapacity { get; } = new[] { 0, 50, 75, 110, 170, 250, 370, 550, 850, 1275, 2000 };

        internal static async Task Classify(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser)
        {
            var alliance = AllianceLogic.GetAlliance(guild.Id);
            if (alliance == null)
                return;

            if (!WsLogic.GetWsTeamByChannel(guild, channel, out var team, out _))
            {
                await channel.BotResponse("You have to use this command in a WS team battleroom!", ResponseType.error);
                return;
            }

            await DeleteTeamsOpsPanel(guild, team);

            var entries = GetTeamEntries(guild, team, null);
            var longestName = entries.Max(x => x.Name.Length);

            var sb = new StringBuilder();
            sb
                .Append("```")
                .Append("NAME".PadRight(longestName))
                .Append(' ')
                .AppendLine("MATCHES");

            var filters = ModuleFilterLogic.GetAllModuleFilters(guild.Id).OrderBy(x => x.Name).ToList();
            foreach (var entry in entries)
            {
                sb
                    .Append(entry.Name.PadRight(longestName))
                    .Append(' ')
                    .AppendJoin(", ", filters
                        .Where(x => FilterMatches(entry, x))
                        .Select(x =>
                        {
                            var needShortNames = x.Modules.Count > 1;
                            var twoCharShortNames = false;
                            if (needShortNames)
                            {
                                var shortNames = x.Modules.Select(m => CompendiumResponseMap.GetShortName(m.Name)[0]).ToArray();
                                twoCharShortNames = shortNames.Distinct().Count() != shortNames.Length;
                            }

                            var modLevels = string.Join('/', x.Modules.Select(m =>
                            {
                                var property = CompendiumResponseMap.GetByName(m.Name);
                                var level = (property?.GetValue(entry.Response.map) as CompendiumResponseModule)?.level ?? 0;

                                var shortName = "";
                                if (needShortNames)
                                {
                                    shortName = CompendiumResponseMap.GetShortName(m.Name);
                                    if (shortName.Length > 0)
                                        shortName = shortName[..(twoCharShortNames ? 2 : 1)];
                                }

                                return shortName + level.ToEmptyStr();
                            }));

                            return x.Name + " (" + modLevels + ")";
                        }))
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

        internal static async Task WsModMining(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string filterName)
        {
            var alliance = AllianceLogic.GetAlliance(guild.Id);
            if (alliance == null)
                return;

            if (!WsLogic.GetWsTeamByChannel(guild, channel, out var team, out var _))
            {
                await channel.BotResponse("You have to use this command in a WS team battleroom!", ResponseType.error);
                return;
            }

            await DeleteTeamsOpsPanel(guild, team);

            var filter = filterName != null ? ModuleFilterLogic.GetModuleFilter(guild.Id, filterName) : null;
            var entries = GetTeamEntries(guild, team, filter);

            var sb = new StringBuilder();
            sb
                .Append("```")
                .Append(GetModulesTable(guild, team,
                    new[] { "miner", "miningboost", "remote", "miningunity", "genesis", "enrich", "crunch", "teleport", "leap", "warp", "relicdrone", "mscap", "mscaphbe" }
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
            var entries = GetTeamEntries(guild, team, filter);
            var longestName = entries.Max(x => x.Name.Length);

            var sb = new StringBuilder();
            sb
                .Append("name".PadRight(longestName))
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
                sb.Append(entry.Name.PadRight(longestName + 1));
                if (entry.Response == null || entry.Response.array.Length < 5)
                {
                    sb.AppendLine();
                    continue;
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

        internal static async Task WsModDefense(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string filterName)
        {
            var alliance = AllianceLogic.GetAlliance(guild.Id);
            if (alliance == null)
                return;

            if (!WsLogic.GetWsTeamByChannel(guild, channel, out var team, out var teamRole))
            {
                await channel.BotResponse("You have to use this command in a WS team battleroom!", ResponseType.error);
                return;
            }

            await DeleteTeamsOpsPanel(guild, team);

            var filter = filterName != null ? ModuleFilterLogic.GetModuleFilter(guild.Id, filterName) : null;
            var entries = GetTeamEntries(guild, team, filter);
            var longestName = entries.Max(x => x.Name.Length);

            var sb = new StringBuilder();
            sb
                .Append("```")
                .Append("name".PadRight(longestName + 1))
                .Append("BS".PadLeft(2))
                .Append("laser".PadLeft(6))
                .Append("barr".PadLeft(5))
                .Append("blast".PadLeft(6))
                .Append("omega".PadLeft(6))
                .Append("tw".PadLeft(5))
                .Append("tele".PadLeft(5))
                .Append("leap".PadLeft(5))
                .Append("BARR".PadLeft(5))
                .Append("SUP".PadLeft(4))
                .Append("BOND".PadLeft(5))
                .Append("fort".PadLeft(5))
                .Append("emp".PadLeft(4))
                .Append(' ')
                .AppendLine("group")
                ;

            foreach (var entry in entries)
            {
                sb.Append(entry.Name.PadRight(longestName + 1));
                if (entry.Response == null || entry.Response.array.Length < 5)
                {
                    sb.AppendLine();
                    continue;
                }

                sb
                    .Append((entry.Response.map?.bs?.level ?? 0).ToEmptyStr().PadLeft(2))
                    .Append((entry.Response.map?.laser?.level ?? 0).ToEmptyStr().PadLeft(6))
                    .Append((entry.Response.map?.barrage?.level ?? 0).ToEmptyStr().PadLeft(5))
                    .Append((entry.Response.map?.blast?.level ?? 0).ToEmptyStr().PadLeft(6))
                    .Append((entry.Response.map?.omega?.level ?? 0).ToEmptyStr().PadLeft(6))
                    .Append((entry.Response.map?.warp?.level ?? 0).ToEmptyStr().PadLeft(5))
                    .Append((entry.Response.map?.teleport?.level ?? 0).ToEmptyStr().PadLeft(5))
                    .Append((entry.Response.map?.leap?.level ?? 0).ToEmptyStr().PadLeft(5))
                    .Append((entry.Response.map?.barrier?.level ?? 0).ToEmptyStr().PadLeft(5))
                    .Append((entry.Response.map?.suppress?.level ?? 0).ToEmptyStr().PadLeft(4))
                    .Append((entry.Response.map?.bond?.level ?? 0).ToEmptyStr().PadLeft(4))
                    .Append((entry.Response.map?.fortify?.level ?? 0).ToEmptyStr().PadLeft(5))
                    .Append((entry.Response.map?.emp?.level ?? 0).ToEmptyStr().PadLeft(4))
                    .Append(' ')
                    .AppendLine(entry.Assignment?.MinerGroupNameBS)
                    ;
            }

            sb.Append("```");

            var sent = await channel.SendMessageAsync(sb.ToString());
            WsLogic.ChangeWsTeam(guild.Id, ref team, t =>
            {
                t.OpsPanelChannelId = channel.Id;
                t.OpsPanelMessageId = sent.Id;
            });
        }

        internal static async Task WsModRocket(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string filterName)
        {
            var alliance = AllianceLogic.GetAlliance(guild.Id);
            if (alliance == null)
                return;

            if (!WsLogic.GetWsTeamByChannel(guild, channel, out var team, out var teamRole))
            {
                await channel.BotResponse("You have to use this command in a WS team battleroom!", ResponseType.error);
                return;
            }

            await DeleteTeamsOpsPanel(guild, team);

            var filter = filterName != null ? ModuleFilterLogic.GetModuleFilter(guild.Id, filterName) : null;
            var entries = GetTeamEntries(guild, team, filter);
            var longestName = entries.Max(x => x.Name.Length);

            var sb = new StringBuilder();
            sb
                .Append("```")
                .Append("name".PadRight(longestName + 1))
                .Append("BS".PadLeft(2))
                .Append("alpha".PadLeft(6))
                .Append("delta".PadLeft(6))
                .Append("omega".PadLeft(6))
                .Append(' ')
                .AppendLine("group")
                ;

            foreach (var entry in entries)
            {
                sb.Append(entry.Name.PadRight(longestName + 1));
                if (entry.Response == null || entry.Response.array.Length < 5)
                {
                    sb.AppendLine();
                    continue;
                }

                sb
                    .Append((entry.Response.map?.bs?.level ?? 0).ToEmptyStr().PadLeft(2))
                    .Append((entry.Response.map?.rocket?.level ?? 0).ToEmptyStr().PadLeft(6))
                    .Append((entry.Response.map?.deltarocket?.level ?? 0).ToEmptyStr().PadLeft(6))
                    .Append((entry.Response.map?.omegarocket?.level ?? 0).ToEmptyStr().PadLeft(6))
                    .Append(' ')
                    .AppendLine(entry.Assignment?.MinerGroupNameBS)
                    ;
            }

            sb.Append("```");

            var sent = await channel.SendMessageAsync(sb.ToString());
            WsLogic.ChangeWsTeam(guild.Id, ref team, t =>
            {
                t.OpsPanelChannelId = channel.Id;
                t.OpsPanelMessageId = sent.Id;
            });
        }

        private static List<Entry> GetTeamEntries(SocketGuild guild, WsTeam team, ModuleFilter filter)
        {
            var mains = team.Members.Mains.Select(x => new Entry()
            {
                Name = guild.GetUser(x)?.GetShortDisplayName(),
                Response = CompendiumLogic.GetUserData(guild.Id, x),
                Assignment = team.OpsAssignments.Find(y => y.UserId == x),
            });

            var alts = team.Members.Alts.Select(x => x.AltUserId != null
                ? new Entry()
                {
                    Name = guild.GetUser(x.AltUserId.Value)?.GetShortDisplayName() ?? "<unknown discord user>",
                    Response = CompendiumLogic.GetUserData(guild.Id, x.AltUserId.Value),
                    Assignment = team.OpsAssignments.Find(y => y.Alt?.Equals(x) == true),
                }
                : new Entry()
                {
                    Name = x.Name,
                    Response = null,// todo: support storing module data for discordless alts
                    Assignment = team.OpsAssignments.Find(y => y.Alt?.Equals(x) == true),
                });

            var allEntry = mains
                .Concat(alts)
                .Where(entry => entry?.Name != null && FilterMatches(entry, filter))
                .OrderBy(x =>
                {
                    if (filter != null)
                        return -x.Score;

                    return (x.Response?.array?.Length ?? 0) >= 5 ? 0 : 1;
                })
                .ThenBy(x => x.Name)
                .ToList();

            return allEntry;
        }

        private static bool FilterMatches(Entry entry, ModuleFilter filter)
        {
            if (filter == null)
                return true;

            if ((entry.Response?.array?.Length ?? 0) < 5)
                return false;

            var mapTypeProperties = typeof(CompendiumResponseMap).GetProperties();
            foreach (var module in filter.Modules)
            {
                var property = Array.Find(mapTypeProperties, p => string.Equals(p.Name, module.Name, StringComparison.InvariantCultureIgnoreCase));
                if (property == null)
                    continue;

                var value = (CompendiumResponseModule)property.GetValue(entry.Response.map);
                if (value == null)
                    return false;

                if (value.level < module.Level)
                    return false;

                entry.Score += value.ws;
            }

            return true;
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
}