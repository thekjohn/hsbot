﻿namespace HsBot.Logic
{
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

            var eb = new EmbedBuilder()
                .WithTitle("JARVIS ONBOARDING")
                .AddField("get the list of available timezones", "`" + DiscordBot.CommandPrefix + "timezone-list`")
                .AddField("set your own timezone", "`" + DiscordBot.CommandPrefix + "timezone-set 55` where 55 is the # number of the timezone you looked up previously")
                .AddField("flag when you are AFK (mainly during WS)", "`" + DiscordBot.CommandPrefix + "afk 5h10m` During AFK, you lose access to the RS queue channel.")
                .AddField("flag when you are no longer AFK", "`" + DiscordBot.CommandPrefix + "back` You get back your RS queue access.")
                .AddField("enter an RS queue", "`" + DiscordBot.CommandPrefix + "in 10` Where 10 is the RS level. Short form is `" + DiscordBot.CommandPrefix + "i 10`")
                .AddField("leave an RS queue", "`" + DiscordBot.CommandPrefix + "out 10` Where 10 is the RS level. Short form is `" + DiscordBot.CommandPrefix + "o 10`")
                .AddField("leave all RS queue", "`" + DiscordBot.CommandPrefix + "out`. Short form is `" + DiscordBot.CommandPrefix + "o`")
                .AddField("get the list of commands", "`" + DiscordBot.CommandPrefix + "help`")
                .AddField("get the defails of a commands", "`" + DiscordBot.CommandPrefix + "help sga`")
                .AddField("get the overview of the alliance (corps)", "`" + DiscordBot.CommandPrefix + "sga`")
                .AddField("get the overview of all alts in alliance", "`" + DiscordBot.CommandPrefix + "sga alts`")
                .AddField("get the list of the members of a corp", "`" + DiscordBot.CommandPrefix + "sga ge`")
                .AddField("get the list of the members of a role", "`" + DiscordBot.CommandPrefix + "sga ally`")
                .AddField("list your alts", "`" + DiscordBot.CommandPrefix + "alts`")
                ;
            await ReplyAsync(embed: eb.Build());
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
        [Summary("whois [user]|display information of a specific a user")]
        public async Task WhoIs(string userName = null)
        {
            await CleanupService.DeleteCommand(Context.Message);

            var user = Context.Guild.FindUser(CurrentUser, userName);
            if (user == null)
            {
                await Context.Channel.BotResponse("Unknown user: " + userName, ResponseType.error);
                return;
            }

            await HelpLogic.ShowMember(Context.Guild, Context.Channel, user);
        }
    }
}