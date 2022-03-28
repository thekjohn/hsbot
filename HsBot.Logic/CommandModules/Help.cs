namespace HsBot.Logic;

using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

[Summary("help")]
[RequireContext(ContextType.Guild)]
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

            if (module.Preconditions.OfType<RequireMinimumAllianceRoleAttribute>().Any(x => !x.Test(CurrentUser)))
                continue;

            var commands = module.Commands
                .Where(command =>
                {
                    if (command.Preconditions.OfType<RequireUserPermissionAttribute>().Any(x => x.GuildPermission != null && !CurrentUser.GuildPermissions.Has(x.GuildPermission.Value)))
                        return false;

                    if (command.Preconditions.OfType<RequireMinimumAllianceRoleAttribute>().Any(x => !x.Test(CurrentUser)))
                        return false;

                    return true;
                })
                .ToList();

            if (commands.Count == 0)
                continue;

            var batchSize = 25;
            var batchCount = (commands.Count / batchSize) + (commands.Count % batchSize == 0 ? 0 : 1);
            for (var batch = 0; batch < batchCount; batch++)
            {
                var eb = new EmbedBuilder()
                    .WithTitle(module.Summary.ToUpper());

                foreach (var command in commands.Skip(batch * batchSize).Take(batchSize))
                {
                    var txt = (command.Summary ?? command.Name).Replace("{cmdPrefix}", DiscordBot.CommandPrefix.ToString(), StringComparison.InvariantCultureIgnoreCase);
                    if (txt.Contains('|', StringComparison.InvariantCultureIgnoreCase))
                    {
                        var parts = txt.Split('|');
                        eb.AddField(DiscordBot.CommandPrefix + parts[0], parts[1], true);
                    }
                    else
                    {
                        eb.AddField(DiscordBot.CommandPrefix + txt, "-", true);
                    }
                }

                eb.WithFooter("This message will self-destruct in 60 seconds.");

                CleanupService.RegisterForDeletion(60,
                    await ReplyAsync(embed: eb.Build()));
            }
        }
    }

    [Command("help")]
    [Summary("help <command>|details of a specific command")]
    public async Task Help(string command)
    {
        await CleanupService.DeleteCommand(Context.Message);

        await ShowHelp(Context.Channel, command);
    }

    public static async Task ShowHelp(ISocketMessageChannel channel, string command)
    {
        var alliance = AllianceLogic.GetAlliance((channel as SocketGuildChannel).Guild.Id);
        if (alliance == null)
            return;

        var commands = DiscordBot.Commands.Commands.ToList();
        var eb = new EmbedBuilder();

        var cmd = DiscordBot.Commands.Commands.FirstOrDefault(x => x.Name == command || x.Aliases.Any(y => y == command));
        if (cmd != null)
        {
            if (!string.IsNullOrEmpty(cmd.Summary))
            {
                var parts = cmd.Summary.Replace("{cmdPrefix}", DiscordBot.CommandPrefix.ToString(), StringComparison.InvariantCultureIgnoreCase).Split('|');
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
                    if (p.Type.Name.Contains("Nullable", StringComparison.InvariantCultureIgnoreCase))
                    {
                        type = p.Type.GenericTypeArguments[0];
                        nullable = true;
                    }

                    var desc = "";
                    if (type == typeof(int))
                        desc = "number";
                    else if (type == typeof(string))
                        desc = "text";
                    else if (type == typeof(SocketRole))
                        desc = "role name";

                    if (desc == "")
                        desc = type.ToString();

                    if (nullable)
                        desc += " (optional)";

                    eb.AddField(p.Name, desc);
                }
            }

            var requireUserPermission = cmd.Module.Preconditions.OfType<RequireUserPermissionAttribute>().FirstOrDefault(x => x.GuildPermission != null)
                ?? cmd.Preconditions.OfType<RequireUserPermissionAttribute>().FirstOrDefault(x => x.GuildPermission != null);

            if (requireUserPermission != null)
                eb.AddField("Required permissions", requireUserPermission.GuildPermission.ToString());

            var requireMinimumAllianceRole = cmd.Module.Preconditions.OfType<RequireMinimumAllianceRoleAttribute>().FirstOrDefault()
                ?? cmd.Preconditions.OfType<RequireMinimumAllianceRoleAttribute>().FirstOrDefault();

            if (requireMinimumAllianceRole != null)
                eb.AddField("Required minimum role", (channel as SocketGuildChannel).Guild.GetRole(alliance.GetAllianceRoleId(requireMinimumAllianceRole.AllianceRole)));

            eb.WithFooter("This message will self-destruct in 30 seconds.");

            CleanupService.RegisterForDeletion(30,
                await channel.SendMessageAsync("Description of the `" + DiscordBot.CommandPrefix + cmd.Name + "` command", false, eb.Build()));
        }
        else
        {
            await channel.BotResponse("Unknown command: " + command, ResponseType.error);
        }
    }

    [Command("jarvis")]
    [Summary("jarvis|get some overview of the most commonly used commands")]
    public async Task Jarvis()
    {
        await CleanupService.DeleteCommand(Context.Message);
        await HelpLogic.ShowMostUsedCommands(Context.Guild, Context.Channel);
    }

    [Command("alliance")]
    [Alias("sga")]
    [Summary("alliance [alts/corpName/roleName]|display the information for the entire alliance, alts, a specific corp or a specific role")]
    public async Task ShowAllianceInfo(string corpOrRole = null)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
        if (alliance == null)
            return;

        if ((corpOrRole ?? "").Trim() == "")
        {
            await HelpLogic.ShowAllianceInfo(Context.Guild, Context.Channel, alliance);
            return;
        }

        if (corpOrRole != null && string.Equals(corpOrRole.Trim(), "alts", StringComparison.InvariantCultureIgnoreCase))
        {
            await HelpLogic.ShowAllianceAlts(Context.Guild, Context.Channel, alliance);
            return;
        }

        var corporation = corpOrRole != null ? Context.Guild.FindCorp(alliance, corpOrRole) : null;
        if (corporation != null)
        {
            await HelpLogic.ShowCorpMembers(Context.Guild, Context.Channel, alliance, corporation);
            return;
        }

        var role = Context.Guild.FindRole(corpOrRole);
        if (role != null)
        {
            await HelpLogic.ShowRoleMembers(Context.Guild, Context.Channel, alliance, role);
            return;
        }

        await Context.Channel.BotResponse("Unknown corp or role: " + corpOrRole, ResponseType.error);
    }

    [Command("whois")]
    [Alias("whois")]
    [Summary("whois [userOrAltName]|display information of a specific a user or alt")]
    [RequireMinimumAllianceRole(AllianceRole.Member)]
    public async Task WhoIs(string userOrAltName = null)
    {
        await CleanupService.DeleteCommand(Context.Message);

        if ((Context.Channel as SocketGuildChannel)?.IsPubliclyAccessible() == true)
        {
            await Context.Channel.BotResponse("Sorry, you can't use this command in public channels.", ResponseType.error);
            return;
        }

        var user = Context.Guild.FindUser(CurrentUser, userOrAltName);
        if (user != null)
        {
            await HelpLogic.ShowUser(Context.Guild, Context.Channel, user);
            return;
        }

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);

        var alt = alliance.FindAlt(userOrAltName);
        user = alt != null
            ? Context.Guild.GetUser(alt.OwnerUserId)
            : null;

        if (user != null)
        {
            await HelpLogic.ShowUser(Context.Guild, Context.Channel, user);
            return;
        }

        await Context.Channel.BotResponse("Unknown user or alt: " + userOrAltName, ResponseType.error);
    }
}
