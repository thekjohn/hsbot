namespace HsBot.Logic
{
    using System.Linq;
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;

    [Summary("help")]
    public class HelpCommandModule : BaseModule
    {
        [Command("help")]
        [Summary("help|list of the available commands")]
        public async Task Help()
        {
            foreach (var module in DiscordBot.Commands.Modules)
            {
                var commands = module.Commands.OrderBy(x => x.Name).ToList();
                var embedBuilder = new EmbedBuilder()
                    .WithTitle(module.Summary.ToUpper());

                foreach (var command in commands)
                {
                    var txt = command.Summary ?? command.Name;
                    if (txt.Contains('|'))
                    {
                        var parts = txt.Split('|');
                        embedBuilder.AddField(parts[0], parts[1], false);
                    }
                    else
                    {
                        embedBuilder.AddField(txt, "-", false);
                    }
                }

                await ReplyAsync(embed: embedBuilder.Build());
            }
        }

        [Command("help")]
        [Summary("help <command>|details of a specific command")]
        public async Task Help(string command)
        {
            var commands = DiscordBot.Commands.Commands.ToList();
            var embedBuilder = new EmbedBuilder();

            var cmd = DiscordBot.Commands.Commands.FirstOrDefault(x => x.Name == command || x.Aliases.Any(y => y == command));
            if (cmd != null)
            {
                if (!string.IsNullOrEmpty(cmd.Summary))
                {
                    var parts = cmd.Summary.Split('|');
                    if (parts.Length == 1)
                        embedBuilder.AddField("summary", cmd.Summary).WithColor(Color.Green);
                    else
                        embedBuilder.AddField("summary", parts[1]).WithColor(Color.Green);
                }

                if (cmd.Aliases.Count > 1)
                {
                    embedBuilder.AddField("aliases", string.Join(", ", cmd.Aliases.Where(x => x != cmd.Name))).WithColor(Color.Magenta);
                }

                foreach (var p in cmd.Parameters)
                {
                    embedBuilder.AddField(p.Name, !string.IsNullOrEmpty(p.Summary) ? p.Summary : p.Type.ToString()).WithColor(Color.DarkBlue);
                }

                await ReplyAsync("description of the " + cmd.Name + " command", false, embedBuilder.Build());
            }
            else
            {
                await ReplyAsync("Unknown command");
            }
        }
    }
}