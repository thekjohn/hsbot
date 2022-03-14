namespace HsBot.Logic
{
    using System;
    using System.Globalization;
    using System.Text;
    using Discord.WebSocket;

    public static class WsResultsLogic
    {
        public static void RecordWsResult(ulong guildId, string teamName, WsResult result)
        {
            StateService.AppendToList(guildId, "ws-result-" + teamName, result);
        }

        public static async Task ShowWsResultsOfTeam(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string teamName)
        {
            var results = StateService.GetList<WsResult>(guild.Id, "ws-result-" + teamName);
            var batchSize = 20;
            var batchCount = (results.Count / batchSize) + (results.Count % batchSize == 0 ? 0 : 1);
            for (var batch = 0; batch < batchCount; batch++)
            {
                var maxOpponentNameLength = results.Skip(batch * batchSize).Take(batchSize).Max(x => x.Opponent.Length);
                var sb = new StringBuilder();
                sb
                    .Append("```")
                    .Append("Match end".PadRight(12))
                    .Append("Opponent".PadRight(maxOpponentNameLength + 1))
                    .Append("Tier".PadRight(5))
                    .AppendLine("Result".PadLeft(10));

                foreach (var result in results.Skip(batch * batchSize).Take(batchSize))
                {
                    sb
                        .Append(result.Date.ToString("yyyy.MM.dd.", CultureInfo.InvariantCulture).PadRight(12))
                        .Append(result.Opponent.PadRight(maxOpponentNameLength + 1))
                        .Append(result.PlayerCount.ToStr().PadRight(5))
                        .Append(result.Score.ToStr().PadLeft(4))
                        .Append(" - ")
                        .AppendLine(result.OpponentScore.ToStr().PadLeft(3));
                }

                sb.Append("```");
                await channel.SendMessageAsync(sb.ToString());
            }
        }

        public static async Task ShowWsResultsOfOpponent(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string opponentName)
        {
            var results = new List<WsResult>();

            foreach (var teamStateId in StateService.ListIds(guild.Id, "ws-result-"))
            {
                var teamResults = StateService.GetList<WsResult>(guild.Id, teamStateId);
                results.AddRange(teamResults.Where(x => x.Opponent.StartsWith(opponentName, StringComparison.InvariantCultureIgnoreCase)));
            }

            var batchSize = 20;
            var batchCount = (results.Count / batchSize) + (results.Count % batchSize == 0 ? 0 : 1);
            for (var batch = 0; batch < batchCount; batch++)
            {
                var maxOpponentNameLength = Math.Max(8, results.Skip(batch * batchSize).Take(batchSize).Max(x => x.Opponent.Length));
                var maxTeamNameLength = Math.Max(5, results.Skip(batch * batchSize).Take(batchSize).Max(x => x.TeamName.Length));

                var sb = new StringBuilder();
                sb
                    .Append("```")
                    .Append("Match end".PadRight(12))
                    .Append("Team".PadRight(maxTeamNameLength + 1))
                    .Append("Opponent".PadRight(maxOpponentNameLength + 1))
                    .Append("Tier".PadRight(5))
                    .Append("Result".PadLeft(10))
                    .AppendLine("Commitment".PadLeft(12));

                foreach (var result in results.Skip(batch * batchSize).Take(batchSize))
                {
                    sb
                        .Append(result.Date.ToString("yyyy.MM.dd.", CultureInfo.InvariantCulture).PadRight(12))
                        .Append(result.TeamName.PadRight(maxTeamNameLength + 1))
                        .Append(result.Opponent.PadRight(maxOpponentNameLength + 1))
                        .Append(result.PlayerCount.ToStr().PadRight(5))
                        .Append(result.Score.ToStr().PadLeft(4))
                        .Append(" - ")
                        .Append(result.OpponentScore.ToStr().PadLeft(3))
                        .AppendLine(result.FinalCommitmentLevel.ToString().PadLeft(12));
                }

                sb.Append("```");
                await channel.SendMessageAsync(sb.ToString());
            }
        }
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
        public WsTeamCommitmentLevel FinalCommitmentLevel { get; set; } = WsTeamCommitmentLevel.Unknown;
    }
}