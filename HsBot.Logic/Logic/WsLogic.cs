namespace HsBot.Logic
{
    using System;
    using System.Globalization;
    using System.Text;
    using System.Threading.Tasks;
    using Discord;
    using Discord.WebSocket;

    public static class WsLogic
    {
        public static async Task ShowWsWesults(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string teamName)
        {
            var results = Services.State.GetList<WsResult>(guild.Id, "ws-result-" + teamName);
            var batchSize = 20;
            var batchCount = (results.Count / batchSize) + (results.Count % batchSize == 0 ? 0 : 1);
            for (var batch = 0; batch < batchCount; batch++)
            {
                var maxOpponentNameLength = results.Skip(batch * batchSize).Take(batchSize).Max(x => x.Opponent.Length);
                var sb = new StringBuilder();
                sb
                    .Append("```")
                    .Append("Match end".PadRight(12))
                    .Append("Opponent".PadRight(maxOpponentNameLength + 2))
                    .Append("Tier".PadRight(6))
                    .AppendLine("Result".PadLeft(10));

                foreach (var result in results.Skip(batch * batchSize).Take(batchSize))
                {
                    sb
                        .Append(result.Date.ToString("yyyy.MM.dd.", CultureInfo.InvariantCulture).PadRight(12))
                        .Append(result.Opponent.PadRight(maxOpponentNameLength + 2))
                        .Append(result.PlayerCount.ToStr().PadRight(6))
                        .Append(result.Score.ToStr().PadLeft(4))
                        .Append(" - ")
                        .AppendLine(result.OpponentScore.ToStr().PadLeft(3));
                }

                sb.Append("```");
                await channel.SendMessageAsync(sb.ToString());
            }
        }

        internal static async Task AddDraftTeam(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, SocketRole role, AllianceLogic.Corp corp)
        {
            var draft = GetWsDraft(guild.Id);
            if (draft == null)
            {
                await channel.BotResponse("There is no active WS draft.", ResponseType.error);
                return;
            }

            var team = draft.Teams.Find(x => x.RoleId == role.Id);
            if (team == null)
            {
                team = new WsTeam()
                {
                    RoleId = role.Id,
                };

                draft.Teams.Add(team);
            }

            team.Name = role.Name;
            team.Members = new WsTeamMembers()
            {
                CorpAbbreviation = corp.Abbreviation,
                Alts = new List<AllianceLogic.Alt>(),
                Mains = new List<ulong>(),
            };

            var currentUsersWithRole = guild.Users.Where(x => x.Roles.Any(r => r.Id == role.Id)).ToList();
            foreach (var user in currentUsersWithRole)
            {
                await user.RemoveRoleAsync(role);
            }

            SaveWsDraft(guild.Id, draft);
            await RepostDraft(guild);
        }

        internal static async Task RemoveDraftTeam(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, SocketRole role)
        {
            var draft = GetWsDraft(guild.Id);
            if (draft == null)
            {
                await channel.BotResponse("There is no active WS draft.", ResponseType.error);
                return;
            }

            var team = draft.Teams.Find(x => x.RoleId == role.Id);
            if (team == null)
            {
                await channel.BotResponse("Specified team doesn't exist in the WS draft.", ResponseType.error);
                return;
            }

            draft.Teams.Remove(team);

            SaveWsDraft(guild.Id, draft);
            await RepostDraft(guild);
        }

        internal static async Task CloseDraft(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser)
        {
            var draft = GetWsDraft(guild.Id);
            if (draft == null)
            {
                await channel.BotResponse("There is no active WS draft.", ResponseType.error);
                return;
            }

            foreach (var team in draft.Teams)
            {
                var cnt = team.Members.Mains.Count + team.Members.Alts.Count;
                if (cnt != 5 && cnt != 10 && cnt != 15)
                {
                    await channel.BotResponse("Cannot close draft, teams must have 5, 10 or 15 members.", ResponseType.error);
                    return;
                }
            }

            //Services.State.Rename(guild.Id, "ws-draft", "ws-draft-archive-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssffff", CultureInfo.InvariantCulture));

            var alliance = AllianceLogic.GetAlliance(guild.Id);

            var admiralRole = alliance.AdmiralRoleId != 0 ? guild.GetRole(alliance.AdmiralRoleId) : null;

            var ch = channel;
            if (alliance.WsAnnounceChannelId != 0)
            {
                var ac = guild.GetTextChannel(alliance.WsAnnounceChannelId);
                if (ac != null)
                    ch = ac;
            }

            var teams = draft.Teams
                .Where(x => guild.GetRole(x.RoleId) != null)
                .OrderBy(x => guild.GetRole(x.RoleId).Name)
                .ToList();

            var channelIndex = 0;

            foreach (var team in teams)
            {
                var role = guild.GetRole(team.RoleId);
                var corp = guild.FindCorp(alliance, team.Members.CorpAbbreviation);
                if (role != null && corp != null)
                {
                    var usersWithRole = team.Members.Mains
                        .Concat(team.Members.Alts.Select(x => x.OwnerUserId))
                        .Concat(team.Members.Alts.Where(x => x.AltUserId != null).Select(x => x.AltUserId.Value))
                        .Distinct();

                    foreach (var userId in usersWithRole)
                    {
                        var user = guild.GetUser(userId);
                        if (user?.Roles.Any(x => x.Id == role.Id) == false)
                            await user.AddRoleAsync(role);
                    }

                    var usersToMention = team.Members.Mains
                        .Concat(team.Members.Alts.Select(x => x.OwnerUserId))
                        .Distinct();

                    var eb = new EmbedBuilder()
                        .WithTitle(role.Name);

                    if (team.Members.Mains.Count > 0)
                    {
                        eb.AddField("Main", string.Join("\n",
                             team.Members.Mains.Select(x => guild.GetUser(x)).Where(x => x != null).Select(x => alliance.GetUserCorpIcon(x) + x.DisplayName)));
                    }

                    if (team.Members.Alts.Count > 0)
                    {
                        eb.AddField("Alt", string.Join("\n",
                            team.Members.Alts.Select(x => x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name)));
                    }

                    await AnnounceWS(guild, channel, role.Name + " is ready, please head to " + corp.FullName + " (" + corp.Abbreviation + ") for scan!", embed: eb.Build());

                    var battleRoom = await guild.CreateTextChannelAsync(role.Name.ToLower(), f =>
                    {
                        f.Position = channelIndex;
                    });

                    team.BattleRoomChannelId = battleRoom.Id;

                    await battleRoom.AddPermissionOverwriteAsync(guild.EveryoneRole, new OverwritePermissions(viewChannel: PermValue.Deny));
                    await battleRoom.AddPermissionOverwriteAsync(role, new OverwritePermissions(viewChannel: PermValue.Allow));

                    var compendiumRole = guild.Roles.FirstOrDefault(x => x.Tags?.BotId == 548952307384188949);
                    if (compendiumRole != null)
                    {
                        await battleRoom.AddPermissionOverwriteAsync(compendiumRole, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow));
                    }

                    //await battleRoom.AddPermissionOverwriteAsync(admiralRole, new OverwritePermissions(viewChannel: PermValue.Allow));

                    await battleRoom.SendMessageAsync(role.Name + " battleroom is ready, please head to " + corp.FullName + " (" + corp.Abbreviation + ") for scan!", embed: eb.Build());

                    var ordersMsg = await battleRoom.SendMessageAsync("Orders will be posted here: " +
                        string.Join(" ", usersToMention
                            .Select(x => guild.GetUser(x))
                            .Where(x => x != null)
                            .OrderBy(x => x.DisplayName)
                            .Select(x => alliance.GetUserCorpIcon(x) + x.Mention)));

                    var ordersThread = await battleRoom.CreateThreadAsync("orders", ThreadType.PublicThread, ThreadArchiveDuration.OneDay, ordersMsg);
                    team.OrdersChannelId = ordersThread.Id;

                    var admiralMsg = await battleRoom.SendMessageAsync("A quiet place for admirals: " +
                        string.Join(" ", usersToMention.Select(x => guild.GetUser(x)).Where(x => x.Roles.Any(y => y.Id == admiralRole.Id)).Select(x => alliance.GetUserCorpIcon(x) + x.Mention)));

                    if (admiralRole != null)
                    {
                        var admiralThread = await battleRoom.CreateThreadAsync("admiral", ThreadType.PublicThread, ThreadArchiveDuration.OneDay, admiralMsg);
                        await admiralThread.SendMessageAsync(":information_source: Type " + DiscordBot.CommandPrefix + "`wsscan` here when scan is started in " + corp.FullName + ". Make sure the corp is closed before the scan to prevent a high influence pilot joining to the corp and ruining the scan.");
                        team.AdmiralChannelId = admiralThread.Id;
                    }

                    channelIndex++;
                }

                SaveWsTeam(guild.Id, team);
            }

            await HelpLogic.ShowAllianceInfo(guild, ch, alliance);
        }

        public static bool GetWsTeamByChannel(SocketGuild guild, ISocketMessageChannel channel, out WsTeam team, out SocketRole role)
        {
            foreach (var stateId in Services.State.ListIds(guild.Id, "ws-team-"))
            {
                var t = Services.State.Get<WsTeam>(guild.Id, stateId);
                if (t.BattleRoomChannelId == channel.Id
                    || t.AdmiralChannelId == channel.Id
                    || t.OrdersChannelId == channel.Id)
                {
                    var r = guild.GetRole(t.RoleId);
                    if (r != null)
                    {
                        team = t;
                        role = r;
                        return true;
                    }
                }
            }

            team = null;
            role = null;
            return false;
        }

        public static async Task WsTeamScanning(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser)
        {
            var alliance = AllianceLogic.GetAlliance(guild.Id);
            if (alliance == null)
                return;

            var adminRole = alliance.AdmiralRoleId != 0 ? guild.GetRole(alliance.AdmiralRoleId) : null;
            if (adminRole != null && !currentUser.Roles.Any(x => x.Id == adminRole.Id))
            {
                await channel.BotResponse("Only members of the " + adminRole.Name + " role can use this command!", ResponseType.error);
                return;
            }

            if (channel is not SocketThreadChannel thread || !GetWsTeamByChannel(guild, channel, out var team, out var teamRole))
            {
                await channel.BotResponse("You have to use this command in the team's admin thread!", ResponseType.error);
                return;
            }

            if (thread.GetPermissionOverwrite(teamRole) == null)
            {
                await channel.BotResponse("You have to use this command in the team's admin thread!", ResponseType.error);
                return;
            }

            if (team.Scanning)
            {
                await channel.BotResponse(team.Name + " is already scanning!", ResponseType.error);
                return;
            }

            if (team.Opponent != null)
            {
                await channel.BotResponse(team.Name + " is already matched against " + team.Opponent + "!", ResponseType.error);
                return;
            }

            team.Scanning = true;
            SaveWsTeam(guild.Id, team);

            var corp = guild.FindCorp(alliance, team.Members.CorpAbbreviation);
            if (corp == null)
            {
                await channel.BotResponse("Can't find team's corp: " + team.Members.CorpAbbreviation, ResponseType.error);
                return;
            }

            await (thread.ParentChannel as SocketTextChannel).SendMessageAsync(teamRole.Mention + " scan started, do not leave " + corp.FullName + "!");

            await thread.SendMessageAsync(":information_source: Type `" + DiscordBot.CommandPrefix + "wsmatched <ends_in> <opponent_name>` when scan is finished. Example: `" + DiscordBot.CommandPrefix + "wsmatched 4d23h12m Blue Star Order`");

            await AnnounceWS(guild, channel, team.Name + " started scanning in " + corp.FullName + "!");
        }

        public static async Task WsTeamMatched(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string opponentName, string endsIn)
        {
            var alliance = AllianceLogic.GetAlliance(guild.Id);
            if (alliance == null)
                return;

            var adminRole = alliance.AdmiralRoleId != 0 ? guild.GetRole(alliance.AdmiralRoleId) : null;
            if (adminRole != null && !currentUser.Roles.Any(x => x.Id == adminRole.Id))
            {
                await channel.BotResponse("Only members of the " + adminRole.Name + " role can use this command!", ResponseType.error);
                return;
            }

            if (channel is not SocketThreadChannel thread || !GetWsTeamByChannel(guild, channel, out var team, out var teamRole))
            {
                await channel.BotResponse("You have to use this command in the team's admin thread!", ResponseType.error);
                return;
            }

            if (!team.Scanning)
            {
                await channel.BotResponse(team.Name + " is not scanning yet! You must start scanning with `" + DiscordBot.CommandPrefix + "wsscan` first.", ResponseType.error);
                return;
            }

            if (team.Opponent != null)
            {
                await channel.BotResponse(team.Name + " is already matched against " + team.Opponent + "!", ResponseType.error);
                return;
            }

            team.Scanning = false;
            team.Opponent = opponentName.Trim();
            team.EndsOn = endsIn.AddToDateTime(DateTime.UtcNow);

            SaveWsTeam(guild.Id, team);

            var corp = guild.FindCorp(alliance, team.Members.CorpAbbreviation);
            if (corp == null)
            {
                await channel.BotResponse("Can't find team's corp: " + team.Members.CorpAbbreviation, ResponseType.error);
                return;
            }

            await (thread.ParentChannel as SocketTextChannel).SendMessageAsync(teamRole.Mention + " scan finished, our opponent is `" + team.Opponent + "`. Good luck!");

            await thread.SendMessageAsync(":information_source: Use the " + (thread.ParentChannel as SocketTextChannel).Threads.FirstOrDefault(x => x.Name == "orders").Mention + " channel to give orders to the team!");

            await AnnounceWS(guild, channel, team.Name + " matched against `" + team.Opponent + "`. Good luck!");
        }

        internal static async Task ManageDraft(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, SocketRole role, bool add, List<SocketGuildUser> mains, List<AllianceLogic.Alt> alts, List<string> unknownNames)
        {
            var draft = GetWsDraft(guild.Id);
            if (draft == null)
            {
                await channel.BotResponse("There is no active WS draft.", ResponseType.error);
                return;
            }

            var team = draft.Teams.Find(x => x.RoleId == role.Id);
            if (team == null)
            {
                await channel.BotResponse("WS team `" + role.Name + "` is not initialized yet! Use `" + DiscordBot.CommandPrefix + "draft-add-team` first!", ResponseType.error);
                return;
            }

            if (add)
            {
                foreach (var main in mains)
                {
                    if (!team.Members.Mains.Contains(main.Id))
                        team.Members.Mains.Add(main.Id);
                }

                foreach (var alt in alts)
                {
                    if (!team.Members.Alts.Any(x => x.Equals(alt)))
                        team.Members.Alts.Add(alt);
                }
            }

            foreach (var t in add ? draft.Teams.Where(x => x != team) : draft.Teams.Where(x => x == team))
            {
                t.Members.Mains.RemoveAll(x => mains.Any(y => y.Id == x));
                t.Members.Alts.RemoveAll(x => alts.Any(y => y.Equals(x)));
            }

            SaveWsDraft(guild.Id, draft);

            await RepostDraft(guild);
        }

        public static async Task RepostDraft(SocketGuild guild)
        {
            var draft = GetWsDraft(guild.Id);
            if (draft == null)
                return;

            var channel = guild.GetTextChannel(draft.ChannelId);
            if (draft.MessageId != 0)
            {
                try
                {
                    await channel.DeleteMessageAsync(draft.MessageId);
                }
                catch (Exception)
                {
                }
            }

            var eb = new EmbedBuilder()
                .WithTitle("DRAFT")
                .WithDescription(":point_right: add a new team: `" + DiscordBot.CommandPrefix + "draft-add-team <roleName> <corpName>`"
                    + "\n:point_right: remove a team: `" + DiscordBot.CommandPrefix + "draft-remove-team <roleName>`"
                    + "\n:point_right: add users to a team: `" + DiscordBot.CommandPrefix + "draft add <roleName> <userNames>`"
                    + "\n:point_right: remove users from a team: `" + DiscordBot.CommandPrefix + "draft remove <roleName> <userNames>`"
                    + "\n:point_right: close draft and create teams, ready to scan: `" + DiscordBot.CommandPrefix + "close-draft`"
                    + "\nAdding users to a team will remove them from all other teams automatically.");

            var userList = draft.OriginalSignup.CompetitiveUsers.Where(x => !draft.Contains(x)).ToList();
            if (userList.Count > 0)
            {
                eb.AddField("💪 Competitive Main", string.Join(" ", userList
                      .Select(x => guild.GetUser(x))
                      .OrderBy(x => x?.DisplayName ?? "<unknown discord user>")
                      .Select(x => "`" + x.DisplayName + "`")));
            }

            var altList = draft.OriginalSignup.CompetitiveAlts.Where(x => !draft.Contains(x)).ToList();
            if (altList.Count > 0)
            {
                eb
                  .AddField("💪 Competitive Alts", string.Join(" ", altList
                      .Select(x => x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name)
                      .OrderBy(x => x)
                      .Select(x => "`" + x + "`")));
            }

            userList = draft.OriginalSignup.CasualUsers.Where(x => !draft.Contains(x)).ToList();
            if (userList.Count > 0)
            {
                eb.AddField("👍 Casual Main", string.Join(" ", userList
                      .Select(x => guild.GetUser(x))
                      .Where(x => x != null)
                      .OrderBy(x => x.DisplayName)
                      .Select(x => "`" + x.DisplayName + "`")));
            }

            altList = draft.OriginalSignup.CasualAlts.Where(x => !draft.Contains(x)).ToList();
            if (altList.Count > 0)
            {
                eb.AddField("👍 Casual Alt", string.Join(" ", altList
                    .Select(x => x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name)
                    .OrderBy(x => x)
                    .Select(x => "`" + x + "`")));
            }

            userList = draft.OriginalSignup.InactiveUsers.Where(x => !draft.Contains(x)).ToList();
            if (userList.Count > 0)
            {
                eb.AddField("😴 Inactive Main", string.Join(" ", userList
                  .Select(x => guild.GetUser(x))
                  .Where(x => x != null)
                  .OrderBy(x => x.DisplayName)
                  .Select(x => "`" + x.DisplayName + "`")));
            }

            altList = draft.OriginalSignup.InactiveAlts.Where(x => !draft.Contains(x)).ToList();
            if (altList.Count > 0)
            {
                eb.AddField("😴 Inactive Alt", string.Join(" ", altList
                    .Select(x => x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name)
                    .OrderBy(x => x)
                    .Select(x => "`" + x + "`")));
            }

            foreach (var team in draft.Teams)
            {
                var role = guild.GetRole(team.RoleId);
                if (role == null)
                    continue;

                eb.AddField(role.Name + " (" + team.Members.CorpAbbreviation + ")",
                    "mains: " + string.Join(" ", team.Members.Mains.Select(x => "`" + (guild.GetUser(x)?.DisplayName ?? "<unknown discord user>") + "`")) +
                    "\nalts: " + string.Join(" ", team.Members.Alts.Select(x => "`" + (x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name) + "`"))
                    );
            }

            var sent = await channel.SendMessageAsync(null, embed: eb.Build());
            draft.MessageId = sent.Id;
            SaveWsDraft(guild.Id, draft);
        }

        private static async Task AnnounceWS(SocketGuild guild, ISocketMessageChannel channel, string message, Embed embed = null)
        {
            var alliance = AllianceLogic.GetAlliance(guild.Id);

            if (alliance.WsAnnounceChannelId != 0)
            {
                var ac = guild.GetTextChannel(alliance.WsAnnounceChannelId);
                if (ac != null)
                    channel = ac;
            }

            await channel.SendMessageAsync(message, embed: embed);
        }

        public static WsTeam GetWsTeam(ulong guildId, string teamName)
        {
            var res = Services.State.Get<WsTeam>(guildId, "ws-team-" + teamName);
            if (res != null)
            {
                if (res.Members.Mains == null)
                    res.Members.Mains = new List<ulong>();
                if (res.Members.Alts == null)
                    res.Members.Alts = new List<AllianceLogic.Alt>();
            }
            return res;
        }

        public static bool WsTeamExists(ulong guildId, string teamName)
        {
            return Services.State.Exists(guildId, "ws-team-" + teamName);
        }

        private static void SaveWsTeam(ulong guildId, WsTeam team)
        {
            Services.State.Set(guildId, "ws-team-" + team.Name, team);
        }

        public static void RecordWsResult(ulong guildId, string wsTeam, WsResult result)
        {
            Services.State.AppendToList(guildId, "ws-result-" + wsTeam, result);
        }

        public static WsDraft GetWsDraft(ulong guildId)
        {
            var res = Services.State.Get<WsDraft>(guildId, "ws-draft");
            if (res != null)
            {
                foreach (var team in res.Teams)
                {
                    if (team.Members.Mains == null)
                        team.Members.Mains = new List<ulong>();

                    if (team.Members.Alts == null)
                        team.Members.Alts = new List<AllianceLogic.Alt>();
                }
            }
            return res;
        }

        public static void SaveWsDraft(ulong guildId, WsDraft draft)
        {
            Services.State.Set(guildId, "ws-draft", draft);
        }

        internal static async void NotifyThreadWorker(object obj)
        {
            while (true)
            {
                var now = DateTime.UtcNow;
                foreach (var guild in DiscordBot.Discord.Guilds)
                {
                    var ids = Services.State.ListIds(guild.Id, "ws-team-");
                    foreach (var signupStateId in ids)
                    {
                        var team = Services.State.Get<WsTeam>(guild.Id, signupStateId);
                        if (team == null || team.EndsOn == null)
                            continue;

                        var teamRole = guild.GetRole(team.RoleId);
                        if (teamRole == null)
                            continue;

                        if (team.Opponent == null)
                            continue;

                        if (team.NotifyPreparationEndsMessageId == null
                            && team.EndsOn.Value.AddDays(-4).AddHours(-16) <= now)
                        {
                            var channel = guild.GetTextChannel(team.BattleRoomChannelId);
                            if (channel != null)
                            {
                                var sent = await channel.SendMessageAsync(teamRole.Mention + " preparation ends in "
                                    + DateTime.Now.Subtract(team.EndsOn.Value.AddDays(-4).AddHours(-12)).ToIntervalStr(true, false)
                                    + ". Make sure you read and scheduled all orders in " + guild.GetThreadChannel(team.OrdersChannelId).Mention + "!");
                                team.NotifyPreparationEndsMessageId = sent.Id;

                                SaveWsTeam(guild.Id, team);
                            }
                        }

                        if (team.NotifySecondDayMessageId == null
                            && team.EndsOn.Value.AddDays(-3).AddHours(-16) <= now)
                        {
                            var channel = guild.GetTextChannel(team.BattleRoomChannelId);
                            if (channel != null)
                            {
                                var sent = await channel.SendMessageAsync(teamRole.Mention + " second day starts in "
                                    + DateTime.Now.Subtract(team.EndsOn.Value.AddDays(-3).AddHours(-12)).ToIntervalStr(true, false)
                                    + ". Make sure you read and scheduled all orders in " + guild.GetThreadChannel(team.OrdersChannelId).Mention + "!");
                                team.NotifySecondDayMessageId = sent.Id;

                                SaveWsTeam(guild.Id, team);
                            }
                        }

                        if (team.NotifyThirdDayMessageId == null
                            && team.EndsOn.Value.AddDays(-2).AddHours(-16) <= now)
                        {
                            var channel = guild.GetTextChannel(team.BattleRoomChannelId);
                            if (channel != null)
                            {
                                var sent = await channel.SendMessageAsync(teamRole.Mention + " third day starts in "
                                    + now.Subtract(team.EndsOn.Value.AddDays(-2).AddHours(-12)).ToIntervalStr(true, false)
                                    + ". Make sure you read and scheduled all orders in " + guild.GetThreadChannel(team.OrdersChannelId).Mention + "!");
                                team.NotifyThirdDayMessageId = sent.Id;

                                SaveWsTeam(guild.Id, team);
                            }
                        }

                        if (team.NotifyFourthDayMessageId == null
                            && team.EndsOn.Value.AddDays(-1).AddHours(-16) <= now)
                        {
                            var channel = guild.GetTextChannel(team.BattleRoomChannelId);
                            if (channel != null)
                            {
                                var sent = await channel.SendMessageAsync(teamRole.Mention + " fourth day starts in "
                                    + now.Subtract(team.EndsOn.Value.AddDays(-1).AddHours(-12)).ToIntervalStr(true, false)
                                    + ". Make sure you read and scheduled all orders in " + guild.GetThreadChannel(team.OrdersChannelId).Mention + "!");
                                team.NotifyFourthDayMessageId = sent.Id;

                                SaveWsTeam(guild.Id, team);
                            }
                        }

                        if (team.NotifyLastHalfDayMessageId == null
                            && team.EndsOn.Value.AddDays(-1).AddHours(-16) <= now)
                        {
                            var channel = guild.GetTextChannel(team.BattleRoomChannelId);
                            if (channel != null)
                            {
                                var sent = await channel.SendMessageAsync(teamRole.Mention + " WS ends in "
                                    + team.EndsOn.Value.Subtract(now).ToIntervalStr(true, false) + ".");
                                team.NotifyLastHalfDayMessageId = sent.Id;

                                SaveWsTeam(guild.Id, team);
                            }
                        }

                        if (team.Notify2hMessageId == null
                            && team.EndsOn.Value.AddHours(-2) <= now)
                        {
                            var channel = guild.GetTextChannel(team.AdmiralChannelId);
                            if (channel != null)
                            {
                                var sent = await channel.SendMessageAsync(teamRole.Mention + " WS ends in "
                                    + team.EndsOn.Value.Subtract(now).ToIntervalStr(true, false) + ". Type `!wsclose <score> <opponentScore>` to close this WS and delete all channels!");
                                team.Notify2hMessageId = sent.Id;

                                SaveWsTeam(guild.Id, team);
                            }
                        }
                    }
                }

                Thread.Sleep(10000);
            }
        }

        public enum WsKind { Unknown, Competitive, Casual, Inactive }

        public class WsResult
        {
            public string TeamName { get; set; }
            public DateTime Date { get; set; }
            public string Opponent { get; set; }
            public int PlayerCount { get; set; }
            public int Score { get; set; }
            public int OpponentScore { get; set; }
            public WsTeamMembers TeamMembers { get; set; }
            public WsKind Kind { get; set; } = WsKind.Unknown;
        }

        public class WsTeamMembers
        {
            public string CorpAbbreviation { get; set; }
            public List<ulong> Mains { get; set; } = new();
            public List<AllianceLogic.Alt> Alts { get; set; } = new();
        }

        public class WsTeam
        {
            public ulong RoleId { get; set; }
            public string Name { get; set; }
            public string Opponent { get; set; }
            public WsTeamMembers Members { get; set; } = new WsTeamMembers();
            public bool Scanning { get; set; }
            public DateTime? EndsOn { get; set; }

            public ulong BattleRoomChannelId { get; set; }
            public ulong AdmiralChannelId { get; set; }
            public ulong OrdersChannelId { get; set; }

            public ulong? NotifyPreparationEndsMessageId { get; set; }
            public ulong? NotifySecondDayMessageId { get; set; }
            public ulong? NotifyThirdDayMessageId { get; set; }
            public ulong? NotifyFourthDayMessageId { get; set; }
            public ulong? NotifyLastHalfDayMessageId { get; set; }
            public ulong? Notify2hMessageId { get; set; }
        }

        public class WsDraft
        {
            public ulong ChannelId { get; set; }
            public ulong MessageId { get; set; }
            public WsSignupLogic.WsSignup OriginalSignup { get; set; }
            public List<WsTeam> Teams { get; set; } = new List<WsTeam>();

            public bool Contains(ulong userId)
            {
                return Teams.Any(x => x.Members.Mains.Contains(userId));
            }

            public bool Contains(AllianceLogic.Alt alt)
            {
                return Teams.Any(x => x.Members.Alts.Any(y => y.Equals(alt)));
            }
        }
    }
}