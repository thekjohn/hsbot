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

    internal static async Task SetRsEvent(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, int season, string startsIn, string endsIn)
    {
        var rsEvent = GetRsEventInfo(guild.Id);
        rsEvent.Active = true;
        rsEvent.Season = season;
        rsEvent.StartedOn = startsIn.AddToDateTime(DateTime.UtcNow);
        rsEvent.EndsOn = endsIn.AddToDateTime(DateTime.UtcNow);
        SetRsEventInfo(guild.Id, rsEvent);
    }
}