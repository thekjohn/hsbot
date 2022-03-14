namespace HsBot.Logic
{
    using Discord.Commands;

    [Summary("White Stars")]
    [RequireContext(ContextType.Guild)]
    public class Ws : BaseModule
    {
        [Command("wssignup")]
        [Summary("wssignup|show active signup form(s)")]
        [RequireMinimumAllianceRole(AllianceRole.Member)]
        public async Task ShowSignup()
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsSignupLogic.RepostSignups(Context.Guild, Context.Channel, CurrentUser);
        }

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
    }
}