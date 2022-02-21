namespace HsBot.Logic
{
    using System.Threading.Tasks;
    using Discord.Commands;

    [Summary("wiki")]
    public class WikiCommandModule : BaseModule
    {
        [Command("setwiki")]
        [Summary("setwiki|set a wiki page - requires 'manage channels' role")]
        public async Task SetWiki(string code, string text)
        {
            if (!CurrentUser.Roles.Any(x => x.Permissions.ManageChannels))
            {
                await ReplyAsync("only members with 'manage channels' role can use this command");
                return;
            }

            await ReplyAsync("pong");
        }

        [Command("getwiki")]
        [Summary("getwiki|query a wiki page")]
        public async Task GetWiki(string code)
        {
            await ReplyAsync("pong");
        }

        [Command("wiki")]
        [Summary("wiki|query the list of wiki pages")]
        public async Task ListWiki()
        {
            await ReplyAsync("pong");
        }
    }
}