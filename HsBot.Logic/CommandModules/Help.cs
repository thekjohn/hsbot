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
                if (module.Preconditions.OfType<RequireUserPermissionAttribute>().Any(x => x.GuildPermission != null && !CurrentUser.GuildPermissions.Has(x.GuildPermission.Value)))
                    continue;

                var commands = module.Commands.ToList();
                var embedBuilder = new EmbedBuilder()
                    .WithTitle(module.Summary.ToUpper());

                foreach (var command in commands)
                {
                    if (command.Preconditions.OfType<RequireUserPermissionAttribute>().Any(x => x.GuildPermission != null && !CurrentUser.GuildPermissions.Has(x.GuildPermission.Value)))
                        continue;

                    var txt = command.Summary ?? command.Name;
                    if (txt.Contains('|'))
                    {
                        var parts = txt.Split('|');
                        embedBuilder.AddField(DiscordBot.CommandPrefix + parts[0], parts[1], true);
                    }
                    else
                    {
                        embedBuilder.AddField(DiscordBot.CommandPrefix + txt, "-", true);
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
                        embedBuilder.AddField("summary", cmd.Summary);
                    else
                        embedBuilder.AddField("summary", parts[1]);
                }

                if (cmd.Aliases.Count > 1)
                {
                    embedBuilder.AddField("aliases", string.Join(", ", cmd.Aliases.Where(x => x != cmd.Name))).WithColor(Color.Magenta);
                }

                foreach (var p in cmd.Parameters)
                {
                    if (!string.IsNullOrEmpty(p.Summary))
                    {
                        embedBuilder.AddField(p.Name, p.Summary);
                    }
                    else
                    {
                        var type = p.Type;
                        var nullable = false;
                        if (p.Type.Name.Contains("Nullable"))
                        {
                            type = p.Type.GenericTypeArguments[0];
                            nullable = true;
                        }

                        var desc = "";
                        if (type == typeof(int))
                            desc = "number";
                        else if (type == typeof(string))
                            desc = "text";
                        else if (type == typeof(Discord.WebSocket.SocketRole))
                            desc = "role name";

                        if (desc == "")
                            desc = type.ToString();

                        if (nullable)
                            desc += " (optional)";

                        embedBuilder.AddField(p.Name, desc);
                    }
                }

                await ReplyAsync("description of the `" + DiscordBot.CommandPrefix + cmd.Name + "` command", false, embedBuilder.Build());
            }
            else
            {
                await ReplyAsync("Unknown command");
            }
        }

        [Command("alliance")]
        [Summary("alliance|display alliance info, corps, levels")]
        public async Task ShowAllianceInfo()
        {
            var alliance = Alliance.GetAlliance(Context.Guild.Id);

            var allianceRole = Context.Guild.GetRole(alliance.RoleId);

            var msg = new EmbedBuilder()
                .WithTitle(alliance.Name ?? allianceRole.Name)
            ;

            foreach (var corp in alliance.Corporations.OrderByDescending(x => x.CurrentRelicCount))
            {
                var role = Context.Guild.GetRole(corp.RoleId);
                if (role != null)
                {
                    msg.AddField(corp.IconMention + " " + (corp.FullName ?? role.Name) + " [" + corp.Abbreviation + "]", "level: " + corp.CurrentLevel + ", relics: " + corp.CurrentRelicCount);
                }
            }

            await ReplyAsync(embed: msg.Build());
        }
    }
}