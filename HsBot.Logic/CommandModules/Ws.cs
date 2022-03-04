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
            await Context.Message.DeleteAsync();
            await WsSignupLogic.RepostSignups(Context.Guild, Context.Channel, CurrentUser);
        }

        [Command("remind")]
        [Summary("remind <who> <when> <message>|remind you about something at a given time\nex.: 'remind me 25m drone' or 'remind @User 2h16m drone'")]
        public async Task Remind(string who, string when, [Remainder] string message)
        {
            await Context.Message.DeleteAsync();
            await RemindLogic.Remind(Context.Guild, Context.Channel, CurrentUser, who, when, message);
        }
    }
}