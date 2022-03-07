namespace HsBot.Logic
{
    using Discord.Commands;

    [Summary("White Stars")]
    public class Ws : BaseModule
    {
        [Command("wssignup")]
        [Summary("wssignup|show active signup form(s)")]
        public async Task ShowSignup()
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsSignupLogic.RepostSignups(Context.Guild, Context.Channel, CurrentUser);
        }

        [Command("remind")]
        [Summary("remind <who> <when> <message>|remind you about something at a given time\nex.: 'remind me 25m drone' or 'remind @User 2h16m drone'")]
        public async Task Remind(string who, string when, [Remainder] string message)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await RemindLogic.Remind(Context.Guild, Context.Channel, CurrentUser, who, when, message);
        }

        [Command("afk")]
        [Summary("afk <interval>|flag yourself as AFK for a specific amount of time: 'afk 10h25m'")]
        public async Task Afk(string interval)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await AfkLogic.SetAfk(Context.Guild, Context.Channel, CurrentUser, interval);
        }

        [Command("back")]
        [Summary("back|remove the AFK flag from yourself and get back RS access")]
        public async Task Back()
        {
            await CleanupService.DeleteCommand(Context.Message);
            await AfkLogic.RemoveAfk(Context.Guild, Context.Channel, CurrentUser);
        }

        [Command("wsresults")]
        [Summary("wsresults <teamName>|list the previous results of a WS team")]
        public async Task ShowWsWesults(string teamName)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsLogic.ShowWsWesults(Context.Guild, Context.Channel, CurrentUser, teamName);
        }

        [Command("wsscan")]
        [Summary("wsscan <teamName>|indicates as WS team is scanning")]
        public async Task SetWsScan(string teamName)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsLogic.WsScanStarted(Context.Guild, Context.Channel, CurrentUser, teamName);
        }

        [Command("wsmatched")]
        [Summary("wsmatched <teamName> <ends_in>|indicates as WS team is matched and ends in a specific amount of time (ex: 4d22h)")]
        public async Task SetWsScan(string teamName, string opponentName, string endsIn)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsLogic.WsMatched(Context.Guild, Context.Channel, CurrentUser, teamName, opponentName, endsIn);
        }
    }
}