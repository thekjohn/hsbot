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
    [Summary("set-rs-event <active> <startsIn> <endsIn>|set the state of the current RS event")]
    [RequireMinimumAllianceRole(AllianceRole.Leader)]
    public async Task SetRsEvent(bool active, string startsIn, string endsIn)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await RsEventLogic.SetRsEvent(Context.Guild, Context.Channel, CurrentUser, active, startsIn, endsIn);
    }
}