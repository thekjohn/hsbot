namespace HsBot.Logic
{
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;

    [Summary("wiki")]
    [RequireContext(ContextType.Guild)]
    [RequireMinimumAllianceRole(AllianceRole.Ally)]
    public class Wiki : BaseModule
    {
        private Embed FormatEntry(WikiEntry entry)
        {
            return new EmbedBuilder()
                .WithTitle(entry.Title)
                .WithDescription(entry.Text)
                .Build();
        }

        [Command("wiki")]
        [Summary("wiki|query wiki pages")]
        public async Task GetWiki(string code = null)
        {
            await CleanupService.DeleteCommand(Context.Message);

            if (code != null)
            {
                var stateId = "wiki-" + code;
                var current = Services.State.Get<WikiEntry>(Context.Guild.Id, stateId);
                if (current == null)
                {
                    await Context.Channel.BotResponse("WIKI entry not found: '" + code + "'.", ResponseType.error);
                    return;
                }

                await ReplyAsync(embed: FormatEntry(current));
                return;
            }

            var prefix = "wiki-";
            var ids = Services.State
                .ListIds(Context.Guild.Id, prefix)
                .OrderBy(x => x);

            var eb = new EmbedBuilder()
                .WithTitle("WIKI pages")
                .WithDescription("use `" + DiscordBot.CommandPrefix + "wiki <code>` to read a specific page");

            var description = "";
            var prefixLength = prefix.Length;
            foreach (var id in ids)
            {
                code = id[prefixLength..];
                description += (description == "" ? "" : "\n") + code;
            }

            eb.AddField("codes", description);

            await ReplyAsync(embed: eb.Build());
        }

        [Command("setwiki")]
        [Summary("setwiki|set a wiki page - requires 'manage channels' role")]
        [RequireMinimumAllianceRole(AllianceRole.Greeter)]
        public async Task SetWiki(string code, string title, [Remainder] string text)
        {
            var stateId = "wiki-" + code;
            var current = Services.State.Get<WikiEntry>(Context.Guild.Id, stateId);
            if (current != null)
                await ReplyAsync("Previous version", embed: FormatEntry(current));

            var entry = new WikiEntry()
            {
                Code = code,
                Title = title,
                Text = text,
            };

            Services.State.Set(Context.Guild.Id, stateId, entry);
            await ReplyAsync("WIKI entry has been recoded", embed: FormatEntry(entry));
        }

        public class WikiEntry
        {
            public string Code { get; set; }
            public string Title { get; set; }
            public string Text { get; set; }
        }
    }
}