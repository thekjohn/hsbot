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
            await CleanupService.DeleteCommand(Context.Message);
            foreach (var module in DiscordBot.Commands.Modules)
            {
                if (module.Preconditions.OfType<RequireUserPermissionAttribute>().Any(x => x.GuildPermission != null && !CurrentUser.GuildPermissions.Has(x.GuildPermission.Value)))
                    continue;

                var commands = module.Commands.ToList();
                var eb = new EmbedBuilder()
                    .WithTitle(module.Summary.ToUpper());

                foreach (var command in commands)
                {
                    if (command.Preconditions.OfType<RequireUserPermissionAttribute>().Any(x => x.GuildPermission != null && !CurrentUser.GuildPermissions.Has(x.GuildPermission.Value)))
                        continue;

                    var txt = (command.Summary ?? command.Name).Replace("{cmdPrefix}", DiscordBot.CommandPrefix.ToString());
                    if (txt.Contains('|'))
                    {
                        var parts = txt.Split('|');
                        eb.AddField(DiscordBot.CommandPrefix + parts[0], parts[1], true);
                    }
                    else
                    {
                        eb.AddField(DiscordBot.CommandPrefix + txt, "-", true);
                    }
                }

                await ReplyAsync(embed: eb.Build());
            }
        }

        [Command("help")]
        [Summary("help <command>|details of a specific command")]
        public async Task Help(string command)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var commands = DiscordBot.Commands.Commands.ToList();
            var eb = new EmbedBuilder();

            var cmd = DiscordBot.Commands.Commands.FirstOrDefault(x => x.Name == command || x.Aliases.Any(y => y == command));
            if (cmd != null)
            {
                if (!string.IsNullOrEmpty(cmd.Summary))
                {
                    var parts = cmd.Summary.Replace("{cmdPrefix}", DiscordBot.CommandPrefix.ToString()).Split('|');
                    if (parts.Length == 1)
                    {
                        eb.AddField("summary", parts[0]);
                    }
                    else
                    {
                        eb
                            .AddField("summary", parts[1])
                            .AddField("usage", parts[0]);
                    }
                }

                if (cmd.Aliases.Count > 1)
                {
                    eb.AddField("aliases", string.Join(", ", cmd.Aliases.Where(x => x != cmd.Name))).WithColor(Color.Magenta);
                }

                foreach (var p in cmd.Parameters)
                {
                    if (!string.IsNullOrEmpty(p.Summary))
                    {
                        eb.AddField(p.Name, p.Summary);
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

                        eb.AddField(p.Name, desc);
                    }
                }

                await ReplyAsync("Description of the `" + DiscordBot.CommandPrefix + cmd.Name + "` command", false, eb.Build());
            }
            else
            {
                await Context.Channel.BotResponse("Unknown command: " + command, ResponseType.error);
            }
        }

        [Command("alliance")]
        [Alias("sga")]
        [Summary("alliance [corp]|display the information for the entire alliance, a specific corp or a specific role")]
        public async Task ShowAllianceInfo(string corpOrRole = null)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            if (alliance == null)
                return;

            var corporation = corpOrRole != null ? Context.Guild.FindCorp(alliance, corpOrRole) : null;
            if (corporation == null)
            {
                var role = Context.Guild.FindRole(corpOrRole);
                if (role == null)
                {
                    await HelpLogic.ShowAllianceInfo(Context.Guild, Context.Channel, alliance);
                    await HelpLogic.ShowAllianceAlts(Context.Guild, Context.Channel, alliance);
                }
                else
                {
                    await HelpLogic.ShowAllianceInfo(Context.Guild, Context.Channel, alliance);
                    await HelpLogic.ShowRoleMembers(Context.Guild, Context.Channel, alliance, role);
                }
            }
            else
            {
                await HelpLogic.ShowCorpInfo(Context.Guild, Context.Channel, alliance, corporation);
                await HelpLogic.ShowCorpMembers(Context.Guild, Context.Channel, alliance, corporation);
            }
        }
    }
}