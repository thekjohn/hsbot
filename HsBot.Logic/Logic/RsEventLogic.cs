namespace HsBot.Logic;

public static class RsEventLogic
{
    public static RsEventInfo GetRsEventInfo(ulong guildId)
    {
        return StateService.Get<RsEventInfo>(guildId, "rs-event")
            ?? new RsEventInfo();
    }

    public static void SetRsEventInfo(ulong guildId, RsEventInfo rsEvent)
    {
        StateService.Set(guildId, "rs-event", rsEvent);
    }

    public class RsEventInfo
    {
        public bool Active { get; set; }
        public int Season { get; set; }
        public DateTime StartedOn { get; set; }
        public DateTime EndsOn { get; set; }
        public ulong? Day1EndedMessageId { get; set; }
        public ulong? Day2EndedMessageId { get; set; }
        public ulong? Day3EndedMessageId { get; set; }
        public ulong? Day4EndedMessageId { get; set; }
        public ulong? Day5EndedMessageId { get; set; }
        public ulong? Day6EndedMessageId { get; set; }
        public ulong? Day7EndedMessageId { get; set; }
    }

    internal static async Task LogRsScore(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, int runNumber, int score, bool leader)
    {
        var rsEvent = GetRsEventInfo(guild.Id);
        if (!rsEvent.Active)
        {
            await channel.BotResponse("There is no active RS Event.", ResponseType.error);
            return;
        }

        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var runStateId = "rs-log-" + runNumber.ToStr();
        var run = StateService.Get<Rs.RsQueueEntry>(guild.Id, runStateId);
        if (run == null)
        {
            await channel.BotResponse("Unknown run #" + runNumber.ToStr(), ResponseType.error);
            return;
        }

        if (!run.Users.Any(x => x == currentUser.Id))
        {
            await channel.BotResponse("You are not a participant of run #" + runNumber.ToStr() + " so you can't record the score.", ResponseType.error);
            return;
        }

        if (run.RsEventSeason != null && !leader)
        {
            await channel.BotResponse("RS Event score is already recorded for run #" + runNumber.ToStr() + ". Ask an administrator to use `" + DiscordBot.CommandPrefix + "logrsfix` command to overwrite it.", ResponseType.error);
            return;
        }

        var logChannel = guild.GetTextChannel(alliance.RsEventLogChannelId);
        if (logChannel == null)
            return;

        var oldScore = run.RsEventScore;
        run.RsEventSeason = rsEvent.Season;
        run.RsEventScore = score;
        StateService.Set(guild.Id, runStateId, run);

        if (oldScore != null)
        {
            await logChannel.SendMessageAsync("RS" + run.Level.ToStr() + " run #" + runNumber.ToStr() + " score successfully CHANGED from " + oldScore.Value.ToStr() + " to " + score.ToStr() + ". "
                + string.Join(" ", run.Users.Select(x => guild.GetUser(x).Mention)) + " [by " + currentUser.DisplayName + "]");
        }
        else
        {
            await logChannel.SendMessageAsync("RS" + run.Level.ToStr() + " run #" + runNumber.ToStr() + " score successfully recorded: " + score.ToStr() + ". "
                + string.Join(" ", run.Users.Select(x => guild.GetUser(x).Mention)) + " [by " + currentUser.DisplayName + "]");
        }
    }

    internal static async void NotifyThreadWorker(object obj)
    {
        while (true)
        {
            try
            {
                var now = DateTime.UtcNow;
                foreach (var guild in DiscordBot.Discord.Guilds)
                {
                    var rsEvent = GetRsEventInfo(guild.Id);
                    if (rsEvent == null)
                        continue;

                    if (!rsEvent.Active)
                        continue;

                    if (rsEvent.Day1EndedMessageId == null && now >= rsEvent.StartedOn.AddDays(1))
                    {
                        rsEvent.Day1EndedMessageId = await RsEventDayEnded(guild, rsEvent, 1);
                        SetRsEventInfo(guild.Id, rsEvent);
                    }

                    if (rsEvent.Day2EndedMessageId == null && now >= rsEvent.StartedOn.AddDays(2))
                    {
                        rsEvent.Day2EndedMessageId = await RsEventDayEnded(guild, rsEvent, 2);
                        SetRsEventInfo(guild.Id, rsEvent);
                    }

                    if (rsEvent.Day3EndedMessageId == null && now >= rsEvent.StartedOn.AddDays(3))
                    {
                        rsEvent.Day3EndedMessageId = await RsEventDayEnded(guild, rsEvent, 3);
                        SetRsEventInfo(guild.Id, rsEvent);
                    }

                    if (rsEvent.Day4EndedMessageId == null && now >= rsEvent.StartedOn.AddDays(4))
                    {
                        rsEvent.Day4EndedMessageId = await RsEventDayEnded(guild, rsEvent, 4);
                        SetRsEventInfo(guild.Id, rsEvent);
                    }

                    if (rsEvent.Day5EndedMessageId == null && now >= rsEvent.StartedOn.AddDays(5))
                    {
                        rsEvent.Day5EndedMessageId = await RsEventDayEnded(guild, rsEvent, 5);
                        SetRsEventInfo(guild.Id, rsEvent);
                    }

                    if (rsEvent.Day6EndedMessageId == null && now >= rsEvent.StartedOn.AddDays(6))
                    {
                        rsEvent.Day6EndedMessageId = await RsEventDayEnded(guild, rsEvent, 6);
                        SetRsEventInfo(guild.Id, rsEvent);
                    }

                    if (rsEvent.Day7EndedMessageId == null && now >= rsEvent.StartedOn.AddDays(7))
                    {
                        rsEvent.Day7EndedMessageId = await RsEventDayEnded(guild, rsEvent, 7);
                        SetRsEventInfo(guild.Id, rsEvent);
                    }
                }
            }
            catch (Exception)
            {
            }

            Thread.Sleep(10000);
        }
    }

    private static async Task<ulong> RsEventDayEnded(SocketGuild guild, RsEventInfo rsEvent, int dayIndex)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        var msg = "Private Red Star Event Season " + rsEvent.Season.ToStr() + " Day " + dayIndex.ToStr() + " ended.";
        if (guild.GetTextChannel(alliance.RsEventLogChannelId) is SocketTextChannel logChannel)
            await logChannel.SendMessageAsync(msg);

        var announceChannel = guild.GetTextChannel(alliance.RsEventAnnounceChannelId);
        if (announceChannel != null)
        {
            msg = "Private Red Star Event Season " + rsEvent.Season.ToStr() + " Day " + dayIndex.ToStr() + " ended.";
            var sent = await announceChannel.SendMessageAsync(msg);
            return sent.Id;
        }

        return ulong.MaxValue;
    }

    internal static async Task SetRsEvent(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, int season, string startsIn)
    {
        var now = DateTime.UtcNow;
        var rsEvent = GetRsEventInfo(guild.Id);
        rsEvent.Active = true;
        rsEvent.Season = season;
        rsEvent.StartedOn = startsIn.AddToDateTime(now);
        rsEvent.EndsOn = rsEvent.StartedOn.AddDays(7);
        SetRsEventInfo(guild.Id, rsEvent);

        var alliance = AllianceLogic.GetAlliance(guild.Id);
        var msg = guild.GetRole(alliance.RoleId).Mention + " Private Red Star Event Season " + rsEvent.Season.ToStr() + " starts in " + rsEvent.StartedOn.Subtract(now.AddSeconds(-15)).ToIntervalStr(true, false) + ".";
        if (guild.GetTextChannel(alliance.RsEventAnnounceChannelId) is SocketTextChannel announceChannel)
            await announceChannel.SendMessageAsync(msg);
    }

    internal static async Task PostLeaderboard(SocketGuild guild, ISocketMessageChannel channel, string title, int startDayIndex, int endDayIndex, int limitCount)
    {
        var rsEvent = GetRsEventInfo(guild.Id);
        if (rsEvent == null)
            return;

        var runIds = StateService.ListIds(guild.Id, "rs-log-");
        var runs = runIds
            .Select(runStateId => StateService.Get<Rs.RsQueueEntry>(guild.Id, runStateId))
            .Where(x => x.RsEventSeason == rsEvent.Season && x.RsEventScore != null)
            .Where(x =>
            {
                var dayIndex = (int)Math.Floor(x.StartedOn.Subtract(rsEvent.StartedOn).TotalDays) + 1;
                return dayIndex >= startDayIndex && dayIndex <= endDayIndex;
            })
            .OrderBy(x => x.StartedOn)
            .ToList();

        var users = runs.SelectMany(x => x.Users)
            .Distinct()
            .Select(x => new UserStat
            {
                UserId = x,
                User = guild.GetUser(x),
            })
            .Where(x => x.User != null)
            .ToDictionary(x => x.UserId);

        foreach (var run in runs)
        {
            foreach (var user in run.Users)
            {
                var userStat = users[user];
                userStat.RunCount++;
                userStat.Score += run.RsEventScore.Value / (double)run.Users.Count;
            }
        }

        var results = users.Values
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.RunCount)
            .Take(limitCount)
            .ToList();

        if (results.Count == 0)
        {
            await channel.BotResponse("Leaderboard is empty yet.", ResponseType.error);
            return;
        }

        var alliance = AllianceLogic.GetAlliance(guild.Id);
        var batchSize = 20;
        var batchCount = (results.Count / batchSize) + (results.Count % batchSize == 0 ? 0 : 1);
        var index = 0;
        for (var batch = 0; batch < batchCount; batch++)
        {
            var sb = new StringBuilder();
            sb
                .Append(title
                    .Replace("{season}", rsEvent.Season.ToStr(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{page}", (batch + 1).ToStr(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{pageCount}", batchCount.ToStr(), StringComparison.InvariantCultureIgnoreCase))
                .Append("```");

            foreach (var userStat in results.Skip(batch * batchSize).Take(batchSize))
            {
                sb
                    .Append('#').Append((index + 1).ToStr().PadLeft(3, ' '))
                    .Append(' ').Append(Convert.ToInt32(Math.Round(userStat.Score)).ToStr().PadLeft(7))
                    .Append(' ').Append(userStat.User.DisplayName);

                var corpName = alliance.GetUserCorpName(userStat.User, true);
                if (corpName != null)
                    sb.Append(" (").Append(corpName).Append(')');

                sb.AppendLine();

                index++;
            }

            sb.Append("```");
            await channel.SendMessageAsync(sb.ToString());
        }
    }

    private class UserStat
    {
        public ulong UserId { get; set; }
        public SocketGuildUser User { get; set; }
        public int RunCount { get; set; }
        public double Score { get; set; }
    }
}