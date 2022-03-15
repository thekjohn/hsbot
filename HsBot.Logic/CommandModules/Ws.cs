namespace HsBot.Logic
{
    using Discord.Commands;

    [Summary("White Stars")]
    [RequireContext(ContextType.Guild)]
    public class Ws : BaseModule
    {
        [Command("remind")]
        [Summary("remind <who> <when> <message>|remind you about something at a given time\nex.: 'remind me 25m drone' or 'remind @User 2h16m drone'")]
        [RequireMinimumAllianceRole(AllianceRole.WSGuest)]
        public async Task Remind(string who, string when, [Remainder] string message)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await RemindLogic.Remind(Context.Guild, Context.Channel, CurrentUser, who, when, message);
        }

        [Command("afk")]
        [Summary("afk <interval>|flag yourself as AFK for a specific amount of time: 'afk 10h25m'")]
        [RequireMinimumAllianceRole(AllianceRole.WSGuest)]
        public async Task Afk(string interval)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await AfkLogic.SetAfk(Context.Guild, Context.Channel, CurrentUser, interval);
        }

        [Command("back")]
        [Summary("back|remove the AFK flag from yourself and get back RS access")]
        [RequireMinimumAllianceRole(AllianceRole.WSGuest)]
        public async Task Back()
        {
            await CleanupService.DeleteCommand(Context.Message);
            await AfkLogic.RemoveAfk(Context.Guild, Context.Channel, CurrentUser);
        }

        [Command("wsresults-team")]
        [Summary("wsresults-team <teamName>|list the previous results of a WS team")]
        [RequireMinimumAllianceRole(AllianceRole.Member)]
        public async Task ShowWsResultsOfTeam(string teamName)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsResultsLogic.ShowWsResultsOfTeam(Context.Guild, Context.Channel, CurrentUser, teamName);
        }

        [Command("wsresults-opponent")]
        [Summary("wsresults-opponent <opponentName>|list the previous results of an opponent")]
        [RequireMinimumAllianceRole(AllianceRole.Member)]
        public async Task ShowWsWesults(string opponentName)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsResultsLogic.ShowWsResultsOfOpponent(Context.Guild, Context.Channel, CurrentUser, opponentName);
        }

        [Command("wsscan")]
        [Summary("wsscan|indicates as WS team is scanning")]
        [RequireMinimumAllianceRole(AllianceRole.Admiral)]
        public async Task WsTeamScanning()
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsLogic.WsTeamScanning(Context.Guild, Context.Channel, CurrentUser);
        }

        [Command("wssetcommitmentlevel")]
        [Summary("wssetcommitmentlevel <commitmentLevel>|Change the commitment level of an existing WS team.")]
        [RequireMinimumAllianceRole(AllianceRole.Admiral)]
        public async Task WsSetTeamCommitmentLevel(string commitmentLevel)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var teamCommitmentLevel = commitmentLevel.ToLowerInvariant() switch
            {
                "competitive" => WsTeamCommitmentLevel.Competitive,
                "casual" => WsTeamCommitmentLevel.Casual,
                "inactive" => WsTeamCommitmentLevel.Inactive,
                _ => WsTeamCommitmentLevel.Unknown,
            };

            if (teamCommitmentLevel == WsTeamCommitmentLevel.Unknown)
            {
                await Context.Channel.BotResponse("Team commitment level must be one of the following values: `competitive`, `casual`, `inactive`.", ResponseType.error);
                return;
            }

            await WsLogic.SetWsTeamCommitmentLevel(Context.Guild, Context.Channel, CurrentUser, teamCommitmentLevel);
        }

        [Command("wsmatched")]
        [Summary("wsmatched <ends_in> <opponent_name>|indicates as WS team matched and ends in a specific amount of time (ex: 4d22h)")]
        [RequireMinimumAllianceRole(AllianceRole.Admiral)]
        public async Task WsTeamMatched(string endsIn, [Remainder] string opponentName)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsLogic.WsTeamMatched(Context.Guild, Context.Channel, CurrentUser, opponentName, endsIn);
        }

        [Command("wssetend")]
        [Summary("wssetend <ends_in>|changes the end time of the WS (ex: 4d22h)")]
        [RequireMinimumAllianceRole(AllianceRole.Admiral)]
        public async Task SetWsEnd(string endsIn)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsLogic.SetWsTeamEnd(Context.Guild, Context.Channel, CurrentUser, endsIn);
        }

        [Command("wsops")]
        [Summary("wsops|list the squishy operations of the WS")]
        [RequireMinimumAllianceRole(AllianceRole.Admiral)]
        public async Task WsOps()
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsLogic.WsOps(Context.Guild, Context.Channel, CurrentUser);
        }
    }
}