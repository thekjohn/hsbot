namespace HsBot.Logic
{
    using System.Threading.Tasks;
    using Discord.WebSocket;

    public enum WsTeamCommitmentLevel { Unknown, Competitive, Casual, Inactive }

    public static class WsLogic
    {
        internal static async Task SetWsTeamCommitmentLevel(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, WsTeamCommitmentLevel commitmentLevel)
        {
            var alliance = AllianceLogic.GetAlliance(guild.Id);
            if (alliance == null)
                return;

            if (channel is not SocketThreadChannel thread || !GetWsTeamByAdmiralChannel(guild, channel, out var team, out _))
            {
                await channel.BotResponse("You have to use this command in the team's admin thread!", ResponseType.error);
                return;
            }

            team.CommitmentLevel = commitmentLevel;
            SaveWsTeam(guild.Id, team);

            await thread.BotResponse("Team's commitment level is successfully changed to " + commitmentLevel.ToString(), ResponseType.successStay);
        }

        public static bool GetWsTeamByAdmiralChannel(SocketGuild guild, ISocketMessageChannel channel, out WsTeam team, out SocketRole role)
        {
            foreach (var stateId in StateService.ListIds(guild.Id, "ws-team-"))
            {
                var t = StateService.Get<WsTeam>(guild.Id, stateId);
                if (t.AdmiralChannelId == channel.Id)
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

            if (channel is not SocketThreadChannel thread || !GetWsTeamByAdmiralChannel(guild, channel, out var team, out var teamRole))
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

            await WsDraftLogic.AnnounceWS(guild, channel, team.Name + " started scanning in " + corp.FullName + "!");
        }

        public static async Task WsTeamMatched(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string opponentName, string endsIn)
        {
            var alliance = AllianceLogic.GetAlliance(guild.Id);
            if (alliance == null)
                return;

            if (channel is not SocketThreadChannel thread || !GetWsTeamByAdmiralChannel(guild, channel, out var team, out var teamRole))
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

            await (thread.ParentChannel as SocketTextChannel).SendMessageAsync(teamRole.Mention + " scan finished, our opponent is `" + team.Opponent + "`. Good luck!");

            await thread.SendMessageAsync(":information_source: Use the " + (thread.ParentChannel as SocketTextChannel).Threads.FirstOrDefault(x => x.Name == "orders").Mention + " channel to give orders to the team!");
            await thread.SendMessageAsync(":information_source: Type `" + DiscordBot.CommandPrefix + "wssetcommitmentlevel <commitmentLevel>` to change the commitment level of the team during the WS.");

            await WsDraftLogic.AnnounceWS(guild, channel, team.Name + " matched against `" + team.Opponent + "`. Good luck!");
        }

        public static async Task SetWsTeamEnd(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string endsIn)
        {
            var alliance = AllianceLogic.GetAlliance(guild.Id);
            if (alliance == null)
                return;

            if (channel is not SocketThreadChannel thread || !GetWsTeamByAdmiralChannel(guild, channel, out var team, out var teamRole))
            {
                await channel.BotResponse("You have to use this command in the team's admin thread!", ResponseType.error);
                return;
            }

            if (team.EndsOn == null)
            {
                await channel.BotResponse(team.Name + " is not matched yet!", ResponseType.error);
                return;
            }

            team.EndsOn = endsIn.AddToDateTime(DateTime.UtcNow);

            SaveWsTeam(guild.Id, team);

            await (thread.ParentChannel as SocketTextChannel).SendMessageAsync(teamRole.Mention + " WS ends in " + team.EndsOn.Value.Subtract(DateTime.UtcNow).ToIntervalStr() + ".");
            await thread.SendMessageAsync(teamRole.Mention + " WS ends in " + team.EndsOn.Value.Subtract(DateTime.UtcNow).ToIntervalStr() + ".");
        }

        public static bool WsTeamExists(ulong guildId, string teamName)
        {
            return StateService.Exists(guildId, "ws-team-" + teamName);
        }

        public static void SaveWsTeam(ulong guildId, WsTeam team)
        {
            StateService.Set(guildId, "ws-team-" + team.Name, team);
        }

        internal static async void NotifyThreadWorker(object obj)
        {
            while (true)
            {
                var now = DateTime.UtcNow;
                foreach (var guild in DiscordBot.Discord.Guilds)
                {
                    var ids = StateService.ListIds(guild.Id, "ws-team-");
                    foreach (var signupStateId in ids)
                    {
                        var team = StateService.Get<WsTeam>(guild.Id, signupStateId);
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
                                    + DateTime.UtcNow.Subtract(team.EndsOn.Value.AddDays(-4).AddHours(-12)).ToIntervalStr(true, false)
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
                                    + DateTime.UtcNow.Subtract(team.EndsOn.Value.AddDays(-3).AddHours(-12)).ToIntervalStr(true, false)
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
        public WsTeamCommitmentLevel CommitmentLevel { get; set; } = WsTeamCommitmentLevel.Unknown;

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
}