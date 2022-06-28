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
        public ulong? FinalSpotsMessageId { get; set; }
        public ulong? Day7EndedMessageId { get; set; }
        public List<RsEventMessageGroup> MessageGroups { get; set; } = new List<RsEventMessageGroup>();
    }

    public class RsEventMessageGroup
    {
        public string Id { get; set; }
        public ulong ChannelId { get; set; }
        public List<ulong> MessageIds { get; set; }
        public DateTime LastPosted { get; set; }
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

        if (!leader && !run.Users.Any(userId =>
        {
            if (userId == currentUser.Id)
                return true;

            var alt = alliance.Alts.Find(alt => alt.AltUserId == userId);
            return alt != null && alt.OwnerUserId == currentUser.Id;
        }))
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

        await PostSummaryLeaderboard(guild, logChannel, "LIVE SUMMARY - S{season}", "auto-log-summary");
        await PostLeaderboard(guild, logChannel, "LIVE Leaderboard - S{season} [{page}/{pageCount}]", -365, 365, 100000, null, "auto-log-full");
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

                    if (rsEvent.FinalSpotsMessageId == null && now >= rsEvent.StartedOn.AddDays(6).AddHours(12))
                    {
                        rsEvent.FinalSpotsMessageId = await RsEventFinalSpots(guild, rsEvent);
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

            await PostSummaryLeaderboard(guild, announceChannel, "SUMMARY - S{season}", null);
            await PostLeaderboard(guild, announceChannel, "DAY " + dayIndex.ToStr() + " Leaderboard - S{season} [{page}/{pageCount}]", dayIndex, dayIndex, 100000, null, null);

            /*for (var rsLevel = 4; rsLevel <= 12; rsLevel++)
            {
                await PostLeaderboard(guild, announceChannel, "DAY " + dayIndex.ToStr() + " RS" + rsLevel.ToStr() + " Leaderboard - S{season} [{page}/{pageCount}]", dayIndex, dayIndex, 100000, rsLevel, null);
            }*/

            return sent.Id;
        }

        return ulong.MaxValue;
    }

    private static async Task<ulong> RsEventFinalSpots(SocketGuild guild, RsEventInfo rsEvent)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);

        var announceChannel = guild.GetTextChannel(alliance.RsEventAnnounceChannelId);
        if (announceChannel == null)
            return 0;

        var msg = "Private Red Star Event Season " + rsEvent.Season.ToStr() + " ends soon. Here is the list of the final spots";
        var sent = await announceChannel.SendMessageAsync(msg);

        var runIds = StateService.ListIds(guild.Id, "rs-log-");
        var runs = runIds
            .Select(runStateId => StateService.Get<Rs.RsQueueEntry>(guild.Id, runStateId))
            .Where(x => x.RsEventSeason == rsEvent.Season && x.RsEventScore != null)
            .OrderBy(x => x.StartedOn)
            .ToList();

        var users = runs.SelectMany(x => x.Users)
            .Distinct()
            .Select(userId => new UserStat
            {
                UserId = userId,
                User = guild.GetUser(userId),
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

        // todo: hardcoded
        var corp1 = alliance.Corporations.Find(x => x.Abbreviation == "TF");
        var corp1size = 37;
        var corp2 = alliance.Corporations.Find(x => x.Abbreviation == "RST");
        var corp2size = 36;

        var results = users.Values
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.RunCount)
            .ToList();

        var results1 = results.Take(corp1size).ToList();
        var results2 = results.Skip(corp1size).Take(corp2size).ToList();
        var index = 0;

        var sb = new StringBuilder();
        sb
            .Append("The following pilots should join ").Append(corp1.FullName).Append(" (").Append(corp1.CurrentRelicCount.ToStr()).Append(" relics) before the event ends:")
            .Append("```");

        foreach (var userStat in results1)
        {
            sb
                .Append('#').Append((index + 1).ToStr().PadRight(3, ' '))
                .Append("  ").Append(Convert.ToInt32(Math.Round(userStat.Score)).ToStr().PadLeft(7))
                .Append("  ").Append(userStat.User.DisplayName);

            var corpName = alliance.GetUserCorpName(userStat.User, true);
            if (corpName != null)
                sb.Append(" [").Append(corpName).Append(']');

            sb.AppendLine();
            index++;
        }

        sb.Append("```");
        await announceChannel.SendMessageAsync(sb.ToString());

        sb = new StringBuilder();
        foreach (var userStat in results1)
            sb.AppendJoin(' ', userStat.User.Mention);

        await announceChannel.SendMessageAsync(sb.ToString());

        sb = new StringBuilder();
        sb
            .Append("The following pilots should join ").Append(corp2.FullName).Append(" (").Append(corp2.CurrentRelicCount.ToStr()).Append(" relics) before the event ends:")
            .Append("```");

        foreach (var userStat in results2)
        {
            sb
                .Append('#').Append((index + 1).ToStr().PadRight(3, ' '))
                .Append("  ").Append(Convert.ToInt32(Math.Round(userStat.Score)).ToStr().PadLeft(7))
                .Append("  ").Append(userStat.User.DisplayName);

            var corpName = alliance.GetUserCorpName(userStat.User, true);
            if (corpName != null)
                sb.Append(" [").Append(corpName).Append(']');

            sb.AppendLine();
            index++;
        }

        sb.Append("```");
        await announceChannel.SendMessageAsync(sb.ToString());

        sb = new StringBuilder();
        foreach (var userStat in results2)
            sb.AppendJoin(' ', userStat.User.Mention);

        await announceChannel.SendMessageAsync(sb.ToString());

        return sent.Id;
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

    internal static async Task PostLeaderboard(SocketGuild guild, ISocketMessageChannel channel, string title, int startDayIndex, int endDayIndex, int limitCount, int? userRsLevelFilter, string messageGroupId)
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
            .Select(userId =>
            {
                var user = guild.GetUser(userId);
                var rsLevel = 0;
                var highestRsRole = HelpLogic.GetHighestRsRole(user);
                if (highestRsRole.Count > 0)
                {
                    rsLevel = int.Parse(highestRsRole[0].Name
                        .Replace("RS", "", StringComparison.InvariantCultureIgnoreCase)
                        .Replace("¾", "", StringComparison.InvariantCultureIgnoreCase));
                }

                return new UserStat
                {
                    UserId = userId,
                    User = user,
                    RsLevel = rsLevel,
                };
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
            .Where(x => userRsLevelFilter == null || x.RsLevel == userRsLevelFilter.Value)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.RunCount)
            .Take(limitCount)
            .ToList();

        if (results.Count == 0)
        {
            if (userRsLevelFilter == null)
                await channel.BotResponse("Leaderboard is empty yet.", ResponseType.error);

            return;
        }

        if (messageGroupId != null)
        {
            try
            {
                var group = rsEvent.MessageGroups.Find(x => x.Id == messageGroupId);
                if (group != null)
                {
                    var groupChannel = guild.GetTextChannel(group.ChannelId);
                    if (groupChannel != null)
                    {
                        foreach (var msgId in group.MessageIds)
                            await groupChannel.DeleteMessageAsync(msgId);
                    }
                }
            }
            catch (Exception) { }
        }

        var alliance = AllianceLogic.GetAlliance(guild.Id);
        var batchSize = 30;
        var batchCount = (results.Count / batchSize) + (results.Count % batchSize == 0 ? 0 : 1);
        var index = 0;
        var msgIds = new List<ulong>();

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
                    .Append('#').Append((index + 1).ToStr().PadRight(3, ' '))
                    .Append("  ").Append(userStat.RunCount.ToStr().PadLeft(3)).Append('x')
                    .Append("  ").Append(Convert.ToInt32(Math.Round(userStat.Score)).ToStr().PadLeft(7))
                    .Append("  ").Append(userStat.RsLevel.ToStr().PadRight(4))
                    .Append("  ").Append(userStat.User.DisplayName);

                var corpName = alliance.GetUserCorpName(userStat.User, true);
                if (corpName != null)
                    sb.Append(" [").Append(corpName).Append(']');

                sb.AppendLine();

                index++;
            }

            sb.Append("```");

            var sent = await channel.SendMessageAsync(sb.ToString());
            msgIds.Add(sent.Id);
        }

        if (messageGroupId != null)
        {
            rsEvent = GetRsEventInfo(guild.Id);
            if (rsEvent == null)
                return;
            rsEvent.MessageGroups.RemoveAll(x => x.Id == messageGroupId);
            rsEvent.MessageGroups.Add(new RsEventMessageGroup()
            {
                Id = messageGroupId,
                LastPosted = DateTime.UtcNow,
                ChannelId = channel.Id,
                MessageIds = msgIds,
            });

            SetRsEventInfo(guild.Id, rsEvent);
        }
    }

    internal static async Task PostSummaryLeaderboard(SocketGuild guild, ISocketMessageChannel channel, string title, string messageGroupId)
    {
        var rsEvent = GetRsEventInfo(guild.Id);
        if (rsEvent == null)
            return;

        var runIds = StateService.ListIds(guild.Id, "rs-log-");
        var runs = runIds
            .Select(runStateId => StateService.Get<Rs.RsQueueEntry>(guild.Id, runStateId))
            .Where(x => x.RsEventSeason == rsEvent.Season && x.RsEventScore != null)
            .OrderBy(x => x.StartedOn)
            .ToList();

        var days = runs
            .Select(run => (int)Math.Floor(run.StartedOn.Subtract(rsEvent.StartedOn).TotalDays) + 1)
            .Distinct()
            .Select(dayIndex =>
            {
                return new DayStat
                {
                    DayIndex = dayIndex,
                };
            })
            .ToDictionary(x => x.DayIndex);

        foreach (var run in runs)
        {
            var dayIndex = (int)Math.Floor(run.StartedOn.Subtract(rsEvent.StartedOn).TotalDays) + 1;
            var dayStat = days[dayIndex];
            dayStat.RunCount++;
            dayStat.Score += run.RsEventScore.Value;
        }

        var results = days.Values
            .OrderBy(x => x.DayIndex)
            .ToList();

        if (results.Count == 0)
        {
            await channel.BotResponse("Leaderboard is empty yet.", ResponseType.error);
            return;
        }

        if (messageGroupId != null)
        {
            try
            {
                var group = rsEvent.MessageGroups.Find(x => x.Id == messageGroupId);
                if (group != null)
                {
                    var groupChannel = guild.GetTextChannel(group.ChannelId);
                    if (groupChannel != null)
                    {
                        foreach (var msgId in group.MessageIds)
                            await groupChannel.DeleteMessageAsync(msgId);
                    }
                }
            }
            catch (Exception) { }
        }

        var alliance = AllianceLogic.GetAlliance(guild.Id);
        var msgIds = new List<ulong>();

        var sb = new StringBuilder();
        sb
            .Append(title
                .Replace("{season}", rsEvent.Season.ToStr(), StringComparison.InvariantCultureIgnoreCase))
                .Append("```");

        foreach (var dayStat in results)
        {
            sb
                .Append("day #").Append(dayStat.DayIndex.ToStr().PadRight(3, ' '))
                .Append("  ").Append(dayStat.RunCount.ToStr().PadLeft(3)).Append('x')
                .Append("  ").Append(Convert.ToInt32(Math.Round(dayStat.Score)).ToStr().PadLeft(7));

            sb.AppendLine();
        }

        sb.Append("```");

        var sent = await channel.SendMessageAsync(sb.ToString());
        msgIds.Add(sent.Id);

        if (messageGroupId != null)
        {
            rsEvent = GetRsEventInfo(guild.Id);
            if (rsEvent == null)
                return;

            rsEvent.MessageGroups.RemoveAll(x => x.Id == messageGroupId);
            rsEvent.MessageGroups.Add(new RsEventMessageGroup()
            {
                Id = messageGroupId,
                LastPosted = DateTime.UtcNow,
                ChannelId = channel.Id,
                MessageIds = msgIds,
            });

            SetRsEventInfo(guild.Id, rsEvent);
        }
    }

    private class UserStat
    {
        public ulong UserId { get; set; }
        public SocketGuildUser User { get; set; }
        public int RsLevel { get; set; }
        public int RunCount { get; set; }
        public double Score { get; set; }
    }

    private class DayStat
    {
        public int DayIndex { get; set; }
        public int RunCount { get; set; }
        public double Score { get; set; }
    }
}