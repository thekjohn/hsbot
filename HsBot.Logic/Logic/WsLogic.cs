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

        public static async Task WsScanStarted(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string teamName)
        {
            var team = GetWsTeam(guild.Id, teamName);
            if (team == null)
            {
                await channel.BotResponse("WS team `" + teamName + "` is not assembled yet!", ResponseType.error);
                return;
            }

            if ((team.Members.Mains.Count + team.Members.Alts.Count != 15)
                && (team.Members.Mains.Count + team.Members.Alts.Count != 10)
                && (team.Members.Mains.Count + team.Members.Alts.Count != 5))
            {
                await channel.BotResponse("WS team `" + teamName + "` is not complete yet!", ResponseType.error);
                return;
            }

            if (team.Scanning)
            {
                await channel.BotResponse("WS team `" + teamName + "` is already scanning!", ResponseType.error);
                return;
            }

            if (team.Opponent != null)
            {
                await channel.BotResponse("WS team `" + teamName + "` is already matched against " + team.Opponent + "!", ResponseType.error);
                return;
            }

            team.Scanning = true;
            SaveWsTeam(guild.Id, team);

            var alliance = AllianceLogic.GetAlliance(guild.Id);
            var corp = guild.FindCorp(alliance, team.Members.CorpAbbreviation);
            if (corp != null)
            {
                await channel.BotResponse("Can't find team's corp: " + team.Members.CorpAbbreviation, ResponseType.error);
                return;
            }

            await AnnounceWS(guild, channel, teamName + " started scanning in " + corp.FullName + "!");
        }

        public static async Task WsMatched(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string teamName, string opponentName, string endsIn)
        {
            var team = GetWsTeam(guild.Id, teamName);
            if (team == null)
            {
                await channel.BotResponse("WS team `" + teamName + "` is not assembled yet!", ResponseType.error);
                return;
            }

            if (!team.Scanning)
            {
                await channel.BotResponse("WS team `" + teamName + "` is not scanning yet! You must start scanning with `" + DiscordBot.CommandPrefix + ".wsscan` first.", ResponseType.error);
                return;
            }

            if (team.Opponent != null)
            {
                await channel.BotResponse("WS team `" + teamName + "` is already matched against " + team.Opponent + "!", ResponseType.error);
                return;
            }

            team.Scanning = false;
            team.Opponent = opponentName.Trim();
            team.EndsOn = endsIn.AddToDateTime(DateTime.UtcNow);

            SaveWsTeam(guild.Id, team);

            var alliance = AllianceLogic.GetAlliance(guild.Id);

            var corp = guild.FindCorp(alliance, team.Members.CorpAbbreviation);
            if (corp != null)
            {
                await channel.BotResponse("Can't find team's corp: " + team.Members.CorpAbbreviation, ResponseType.error);
                return;
            }

            await AnnounceWS(guild, channel, teamName + " is matched against " + team.Opponent + ". Good luck!");
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
            return Services.State.Get<WsTeam>(guildId, "ws-team-" + teamName);
        }

        private static void SaveWsTeam(ulong guildId, WsTeam team)
        {
            Services.State.Set(guildId, "ws-team-" + team.Name, team);
        }

        public static void RecordWsResult(ulong guildId, string wsTeam, WsResult result)
        {
            Services.State.AppendToList(guildId, "ws-result-" + wsTeam, result);
        }

        public class WsResult
        {
            public string TeamName { get; set; }
            public DateTime Date { get; set; }
            public string Opponent { get; set; }
            public int PlayerCount { get; set; }
            public int Score { get; set; }
            public int OpponentScore { get; set; }
            public WsTeamMembers TeamMembers { get; set; }
        }

        public class WsTeamMembers
        {
            public string CorpAbbreviation { get; set; }
            public List<ulong> Mains { get; set; }
            public List<AllianceLogic.Alt> Alts { get; set; }
        }

        public class WsTeam
        {
            public string Name { get; set; }
            public string Opponent { get; set; }
            public WsTeamMembers Members { get; set; } = new WsTeamMembers();
            public bool Scanning { get; set; }
            public DateTime? EndsOn { get; set; }
        }
    }
}