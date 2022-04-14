namespace HsBot.Logic;

public static class WsResultsLogic
{
    public static void RecordWsResult(ulong guildId, string teamName, WsResult result)
    {
        StateService.AppendToList(guildId, "ws-result-" + teamName, result);
    }

    public static async Task ShowWsResults(SocketGuild guild, ISocketMessageChannel channel, string name)
    {
        List<WsResult> results = null;
        string displayName = null;

        if (results == null && StateService.Exists(guild.Id, "ws-result-" + name))
        {
            results = StateService.GetList<WsResult>(guild.Id, "ws-result-" + name);
            displayName = name + " (WS team)";
        }

        if (results == null)
        {
            var user = guild.FindUser(null, name);
            if (user != null)
            {
                results = StateService.ListIds(guild.Id, "ws-result-")
                   .SelectMany(x => StateService
                       .GetList<WsResult>(guild.Id, x)
                       .Where(y => y.TeamMembers?.Mains.Contains(user.Id) == true
                        || y.TeamMembers?.Alts.Any(alt => alt.AltUserId == user.Id || alt.OwnerUserId == user.Id) == true)
                   )
                   .OrderBy(x => x.Date)
                   .ToList();

                displayName = user.DisplayName + " (user)";
            }
        }

        if (results == null)
        {
            results = StateService.ListIds(guild.Id, "ws-result-")
               .SelectMany(x => StateService
                   .GetList<WsResult>(guild.Id, x)
                   .Where(y => y.Opponent.StartsWith(name, StringComparison.InvariantCultureIgnoreCase))
               )
               .OrderBy(x => x.Date)
               .ToList();

            displayName = name + " (opponent)";
        }

        await ShowWsResults(channel, results, displayName);
    }

    private static async Task ShowWsResults(ISocketMessageChannel channel, List<WsResult> results, string displayName)
    {
        var batchSize = 20;
        var batchCount = (results.Count / batchSize) + (results.Count % batchSize == 0 ? 0 : 1);
        for (var batch = 0; batch < batchCount; batch++)
        {
            var maxOpponentNameLength = Math.Max(8, results.Skip(batch * batchSize).Take(batchSize).Max(x => x.Opponent.Length));
            var maxTeamNameLength = Math.Max(5, results.Skip(batch * batchSize).Take(batchSize).Max(x => x.TeamName.Length));

            var sb = new StringBuilder();
            sb
                .Append("WS results for `").Append(displayName).Append('`')
                .Append("\n```")
                .Append("Match end".PadRight(12))
                .Append("Team".PadRight(maxTeamNameLength + 1))
                .Append("Opponent".PadRight(maxOpponentNameLength + 1))
                .Append("Tier".PadRight(5))
                .Append("Result".PadLeft(10))
                .Append(' ')
                .AppendLine("Commitment".PadRight(12))
                //.Append("Pilots".PadRight(10))
                ;

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
                    .Append(' ')
                    .AppendLine(result.CommitmentLevel.ToString().PadRight(12))
                    /*.Append(result.TeamMembers != null
                        ? string.Join(" ", result.TeamMembers.Mains
                            .Select(x => guild.GetUser(x))
                            .Where(x => x != null)
                            .Select(x => x.GetShortDisplayName()))
                        : "n.a.")*/
                    ;
            }

            sb.Append("```This message will self-destruct in 60 seconds.");

            CleanupService.RegisterForDeletion(60,
                await channel.SendMessageAsync(sb.ToString()));
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
    public WsTeamCommitmentLevel CommitmentLevel { get; set; } = WsTeamCommitmentLevel.Unknown;
}
