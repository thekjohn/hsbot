namespace HsBot.Logic;

[Summary("Red Star Events")]
[RequireContext(ContextType.Guild)]
public class RsEvent : BaseModule
{
    [Command("logrs")]
    [Summary("logrs <runNumber> <score>|save the score of a run during an RS Event")]
    [RequireMinimumAllianceRole(AllianceRole.Ally)]
    public async Task LogRsScore(int runNumber, int score)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await RsEventLogic.LogRsScore(Context.Guild, Context.Channel, CurrentUser, runNumber, score, false);
    }

    [Command("logrsfix")]
    [Summary("logrsfix <runNumber> <score>|save the score of a run during an RS Event")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task LogRsScoreLeader(int runNumber, int score)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await RsEventLogic.LogRsScore(Context.Guild, Context.Channel, CurrentUser, runNumber, score, true);
    }

    [Command("set-rs-event")]
    [Summary("set-rs-event <season> <startsIn> <endsIn>|set the state of the current RS event")]
    [RequireMinimumAllianceRole(AllianceRole.Leader)]
    public async Task SetRsEvent(int season, string startsIn)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await RsEventLogic.SetRsEvent(Context.Guild, Context.Channel, CurrentUser, season, startsIn);
    }

    [Command("rs-event-leaderboard")]
    [Summary("rs-event-leaderboard|display the full RS event leaderboard")]
    [RequireMinimumAllianceRole(AllianceRole.Leader)]
    public async Task FullRsEventLeaderboard()
    {
        await CleanupService.DeleteCommand(Context.Message);
        await RsEventLogic.PostLeaderboard(Context.Guild, Context.Channel, "Leaderboard - S{season} [{page}/{pageCount}]", -365, 365, 100000, null, "current-full");
    }

    [Command("rs-event-leaderboard-day")]
    [Summary("rs-event-leaderboard-day <day>|display the RS event leaderboard for a specific day")]
    [RequireMinimumAllianceRole(AllianceRole.Leader)]
    public async Task SpecificDayRsEventLeaderboard(int day, int? rsLevel = null)
    {
        await CleanupService.DeleteCommand(Context.Message);
        var groupId = "current-day-" + day.ToStr()
            + (rsLevel != null ? "-rs" + rsLevel.Value.ToStr() : "");
        await RsEventLogic.PostLeaderboard(Context.Guild, Context.Channel, "Leaderboard - S{season} / day " + day.ToStr() + " [{page}/{pageCount}]", day, day, 100000, rsLevel, groupId);
    }

    [Command("rs-event-summary")]
    [Summary("rs-event-summary|display the summary of the current RS event")]
    [RequireMinimumAllianceRole(AllianceRole.Leader)]
    public async Task SummaryRsEventLeaderboard()
    {
        await CleanupService.DeleteCommand(Context.Message);
        var groupId = "summary";
        await RsEventLogic.PostSummaryLeaderboard(Context.Guild, Context.Channel, "SUMMARY - S{season}", groupId);
    }
}