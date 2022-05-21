using TimeZoneNames;

namespace HsBot.Logic;

[Summary("admin")]
[RequireContext(ContextType.Guild)]
public class Admin : BaseModule
{
    [Command("setmyname")]
    [Summary("setmyname <ingameName>|Set the ingame name of a user.")]
    [RequireMinimumAllianceRole(AllianceRole.Ally)]
    public async Task SetMyName([Remainder] string ingameName)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await RoleLogic.SetMyName(Context.Guild, Context.Channel, CurrentUser, ingameName);
    }

    [Command("setmycorp")]
    [Summary("setmycorp <corpName>|Set the ingame name of a user.")]
    [RequireMinimumAllianceRole(AllianceRole.Ally)]
    public async Task SetMyCorp([Remainder] string corpName)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await RoleLogic.SetMyCorp(Context.Guild, Context.Channel, CurrentUser, corpName);
    }

    [Command("alts")]
    [Summary("alts [user]|display alts")]
    [RequireMinimumAllianceRole(AllianceRole.Member)]
    public async Task Alts(string user = null)
    {
        await CleanupService.DeleteCommand(Context.Message);

        SocketGuildUser otherUser = null;
        if (user != null)
        {
            otherUser = Context.Guild.FindUser(CurrentUser, user);
            if (otherUser == null)
            {
                await Context.Channel.BotResponse("Can't find user: " + user + ".", ResponseType.error);
                return;
            }
        }

        await AltsLogic.ShowAlts(Context.Guild, Context.Channel, CurrentUser, otherUser ?? CurrentUser);
    }

    [Command("addalt")]
    [Summary("addalt name|add a new alt for yourself")]
    [RequireMinimumAllianceRole(AllianceRole.Member)]
    public async Task AddAlt(string name)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await AltsLogic.AddAlt(Context.Guild, Context.Channel, CurrentUser, name);
    }

    [Command("set-alliance")]
    [Summary("set-alliance <name> <abbrev>|set the main parameters of the alliance")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetAlliance(SocketRole role, string name, string abbrev)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
        alliance.RoleId = role.Id;
        alliance.Name = name;
        alliance.Abbreviation = abbrev;
        AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

        await Context.Channel.BotResponse("Alliance updated: " + role.Name, ResponseType.success);
    }

    [Command("set-alliance-corp")]
    [Summary("set-alliance-corp <nameToFind> <newName> <icon> <abbrev>|set the main parameters of a corp")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetAllianceCorp(string nameToFind, string newName, string icon, string abbrev)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
        var corp = Context.Guild.FindCorp(alliance, nameToFind);
        if (corp != null)
        {
            corp.FullName = newName;
            corp.IconMention = icon;
            corp.Abbreviation = abbrev;
            AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

            await Context.Channel.BotResponse("Corp updated: " + nameToFind, ResponseType.success);
        }
        else
        {
            await Context.Channel.BotResponse("Unknown corp: " + nameToFind, ResponseType.error);
        }
    }

    [Command("add-alliance-corp")]
    [Summary("add-alliance-corp <role> <abbreviation>|add new corp to the alliance")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task AddAllianceCorp(SocketRole role, string abbreviation)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
        var corp = alliance.Corporations.Find(x => x.RoleId == role.Id);
        if (corp == null)
        {
            corp = new AllianceLogic.Corp
            {
                RoleId = role.Id,
                Abbreviation = abbreviation,
            };

            alliance.Corporations.Add(corp);
            AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

            await Context.Channel.BotResponse("Corp created: " + role.Name, ResponseType.success);
        }
        else
        {
            await Context.Channel.BotResponse("Corp already created: " + role.Name, ResponseType.error);
        }
    }

    [Command("set-corp-relics")]
    [Summary("set-corp-relics <corp> <amount>|change the relic count of a corp")]
    [RequireMinimumAllianceRole(AllianceRole.Officer)]
    public async Task SetCorpRelics(string corp, int relicCount)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
        if (alliance == null)
            return;

        var corporation = Context.Guild.FindCorp(alliance, corp);
        if (corporation == null)
        {
            await Context.Channel.BotResponse("Unknown corp: " + corp, ResponseType.error);
            return;
        }

        if (!CurrentUser.GuildPermissions.Administrator && !CurrentUser.Roles.Any(x => x.Id == corporation.RoleId))
        {
            await Context.Channel.BotResponse("Only members of the specified corp can use this command!", ResponseType.error);
            return;
        }

        corporation.CurrentRelicCount = relicCount;
        AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

        await Context.Channel.BotResponse("Corp updated: " + corporation.FullName, ResponseType.success);
    }

    [Command("set-rs-timeout")]
    [Summary("set-rs-timeout <level> <minutes>|change the activity timeout of a specific RS queue")]
    [RequireMinimumAllianceRole(AllianceRole.Leader)]
    public async Task SetRsTimeout(int level, int minutes)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var stateId = StateService.GetId("rs-queue-timeout", (ulong)level);
        var currentValue = StateService.Get<int>(Context.Guild.Id, stateId);

        StateService.Set(Context.Guild.Id, stateId, minutes);
        await Context.Channel.BotResponse("Timeout for RS" + level.ToStr() + " has been changed to " + minutes.ToStr() + "."
                + (currentValue != 0 ? " Previous value was " + currentValue.ToStr() + "." : ""), ResponseType.success);
    }

    [Command("set-rs-timeout")]
    [Summary("set-rs-timeout <minutes>|change the activity timeout of a all RS queues")]
    [RequireMinimumAllianceRole(AllianceRole.Leader)]
    public async Task SetRsTimeout(int minutes)
    {
        await CleanupService.DeleteCommand(Context.Message);

        for (var level = 1; level <= 12; level++)
        {
            var stateId = StateService.GetId("rs-queue-timeout", (ulong)level);
            var currentValue = StateService.Get<int>(Context.Guild.Id, stateId);
            if (currentValue != minutes)
            {
                StateService.Set(Context.Guild.Id, stateId, minutes);
            }

            await Context.Channel.BotResponse("Timeout for RS" + level.ToStr() + " has been changed to " + minutes.ToStr() + "."
                + (currentValue != 0 ? " Previous value was " + currentValue.ToStr() + "." : ""), ResponseType.success);
        }
    }

    [Command("set-alliance-icon")]
    [Summary("set-alliance-icon <icon>|set key icons of the alliance")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetAllianceIcon(string icon, string iconMention)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);

        switch (icon)
        {
            case "ally":
                alliance.AllyIcon = iconMention;
                break;
        }

        AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

        await Context.Channel.BotResponse("Icon '" + icon + "' is changed changed to " + iconMention, ResponseType.success);
    }

    [Command("set-alliance-role")]
    [Summary("set-alliance-role <greeter|leader|officer|guest|wsguest|compendium|admiral|ally> <discordRoleName>|set key roles of the alliance")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetAllianceRole(string role, string discordRoleName)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var discordRole = Context.Guild.FindRole(discordRoleName);
        if (discordRole == null)
        {
            await Context.Channel.BotResponse("Unknown role: " + discordRoleName, ResponseType.error);
            return;
        }

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);

        switch (role)
        {
            case "greeter":
                alliance.GreeterRoleId = discordRole.Id;
                break;
            case "leader":
                alliance.LeaderRoleId = discordRole.Id;
                break;
            case "officer":
                alliance.OfficerRoleId = discordRole.Id;
                break;
            case "guest":
                alliance.GuestRoleId = discordRole.Id;
                break;
            case "wsguest":
                alliance.WsGuestRoleId = discordRole.Id;
                break;
            case "compendium":
                alliance.CompendiumRoleId = discordRole.Id;
                break;
            case "admiral":
                alliance.AdmiralRoleId = discordRole.Id;
                break;
            case "ally":
                alliance.AllyRoleId = discordRole.Id;
                break;
            default:
                await Context.Channel.BotResponse("First parameter must be one of these values: greeter, leader, officer, guest, wsguest, compendium, admiral, ally", ResponseType.error);
                return;
        }

        AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);
        await Context.Channel.BotResponse("Role '" + role + "' is changed changed to " + discordRole.Name, ResponseType.success);
    }

    [Command("set-public-channel")]
    [Summary("set-public-channel <channel>|set the public chat channel")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetPublicChannel(SocketTextChannel channel)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
        alliance.PublicChannelId = channel.Id;
        AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

        await Context.Channel.BotResponse("Public chat channel changed: " + channel.Name, ResponseType.success);
    }

    [Command("set-ws-draft-channel")]
    [Summary("set-ws-draft-channel <channel>|set ws draft chat channel")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetWsDraftChannel(SocketTextChannel channel)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
        alliance.WsDraftChannelId = channel.Id;
        AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

        await Context.Channel.BotResponse("WS draft channel changed: " + channel.Name, ResponseType.success);
    }

    [Command("set-ws-signup-channel")]
    [Summary("set-ws-signup-channel <channel>|set ws signup chat channel")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetWsSignupChannel(SocketTextChannel channel)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
        alliance.WsSignupChannelId = channel.Id;
        AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

        await Context.Channel.BotResponse("WS signup channel changed: " + channel.Name, ResponseType.success);
    }

    [Command("set-ws-announce-channel")]
    [Summary("set-ws-announce-channel <channel>|set ws announcement chat channel")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetWsAnnounceChannel(SocketTextChannel channel)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
        alliance.WsAnnounceChannelId = channel.Id;
        AllianceLogic.SaveAlliance(Context.Guild.Id, alliance);

        await Context.Channel.BotResponse("WS announce channel changed: " + channel.Name, ResponseType.success);
    }

    [Command("falsestart")]
    [Summary("falsestart <runNumber>|invalidate an RS run")]
    [RequireMinimumAllianceRole(AllianceRole.Officer)]
    public async Task FalseStartRun(int runNumber)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await FalseStartRun(Context.Guild, Context.Channel, runNumber);
    }

    private static async Task FalseStartRun(SocketGuild guild, ISocketMessageChannel channel, int runNumber)
    {
        var queueStateId = StateService.GetId("rs-log", (ulong)runNumber);
        var queue = StateService.Get<Rs.RsQueueEntry>(guild.Id, queueStateId);
        if (queue == null)
        {
            await channel.BotResponse("Can't find run #" + runNumber.ToStr(), ResponseType.error);
            return;
        }

        foreach (var userId in queue.Users)
        {
            var runCountStateId = StateService.GetId("rs-run-count", userId, (ulong)queue.Level);
            var runCount = StateService.Get<int>(guild.Id, runCountStateId);
            runCount--;
            StateService.Set(guild.Id, runCountStateId, runCount);
        }

        queue.FalseStart = DateTime.UtcNow;
        StateService.Set(guild.Id, queueStateId, queue);

        await channel.BotResponse("Run #" + runNumber.ToStr() + " is successfuly reset.", ResponseType.success);
    }

    [Command("ws-start-signup")]
    [Summary("ws-start-signup 5d3h|start a new WS signup which ends in 5d3h from now")]
    [RequireMinimumAllianceRole(AllianceRole.Leader)]
    public async Task StartWsSignup(string endsFromNow)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await WsSignupLogic.StartNew(Context.Guild, endsFromNow.AddToDateTime(DateTime.UtcNow));
    }

    [Command("timezone-list")]
    [Summary("timezone-list|list all time zones")]
    [RequireMinimumAllianceRole(AllianceRole.Ally)]
    public async Task TimezoneList()
    {
        await CleanupService.DeleteCommand(Context.Message);
        var timezones = TimeZoneInfo.GetSystemTimeZones();
        var index = 0;

        var eb = new EmbedBuilder()
            .WithTitle("time zones")
            .WithFooter("This message will self-destruct in 60 seconds.");

        var now = DateTime.UtcNow;

        foreach (var tz in timezones)
        {
            var name = TZNames.GetDisplayNameForTimeZone(tz.Id, "en-US");
            index++;
            //var desc = "#" + index.ToStr() + " : UTC" + (tz.BaseUtcOffset.TotalMilliseconds >= 0 ? "+" : "-") + tz.BaseUtcOffset.ToString(@"hh\:mm");
            var desc = "#" + index.ToStr() + " [" + TimeZoneInfo.ConvertTimeFromUtc(now, tz).ToString("HH:mm") + "] [UTC" + (tz.BaseUtcOffset.TotalMilliseconds >= 0 ? "+" : "")
                + (tz.BaseUtcOffset.Minutes == 0
                    ? tz.BaseUtcOffset.Hours.ToStr()
                    : tz.BaseUtcOffset.ToString(@"h\:mm")
                    ) + "]";
            eb.AddField(tz.StandardName, desc, true);

            if (eb.Fields.Count == 25)
            {
                CleanupService.RegisterForDeletion(60,
                    await ReplyAsync(embed: eb.Build()));

                eb = new EmbedBuilder()
                    .WithTitle("time zones");
            }
        }

        if (eb.Fields.Count > 0)
        {
            CleanupService.RegisterForDeletion(60,
                await ReplyAsync(embed: eb.Build()));
        }
    }

    [Command("timezone-set")]
    [Summary("timezone-set <identifier>|set your own timezone. Get a list of identifiers with the `{cmdPrefix}timezone-list` command")]
    [RequireMinimumAllianceRole(AllianceRole.Ally)]
    public async Task TimezoneSet(int identifier)
    {
        await CleanupService.DeleteCommand(Context.Message);
        var timeZones = TimeZoneInfo.GetSystemTimeZones();
        if (identifier < 1 || identifier > timeZones.Count)
        {
            await Context.Channel.BotResponse("Wrong timezone index", ResponseType.error);
            return;
        }

        var tz = timeZones[identifier - 1];
        await TimeZoneLogic.SetTimeZone(Context.Guild, Context.Channel, CurrentUser, tz);
    }

    [Command("timezone-set")]
    [Summary("timezone-set <identifier> <user>|set timezone of another user")]
    [RequireMinimumAllianceRole(AllianceRole.Officer)]
    public async Task TimezoneSet(int identifier, string otherUser)
    {
        if (!CurrentUser.GuildPermissions.Administrator)
        {
            await Context.Channel.BotResponse("Only Administrators can set the timezone for other users.", ResponseType.error);
            return;
        }

        await CleanupService.DeleteCommand(Context.Message);
        var timeZones = TimeZoneInfo.GetSystemTimeZones();
        if (identifier < 1 || identifier > timeZones.Count)
        {
            await Context.Channel.BotResponse("Wrong timezone index", ResponseType.error);
            return;
        }

        var user = Context.Guild.FindUser(CurrentUser, otherUser);
        if (user == null)
        {
            await Context.Channel.BotResponse("Unknown user: " + otherUser, ResponseType.error);
            return;
        }

        var tz = timeZones[identifier - 1];
        await TimeZoneLogic.SetTimeZone(Context.Guild, Context.Channel, user, tz);
    }

    [Command("set-rs-queue-role")]
    [Summary("set-rs-queue-role <role>|set the role for RS queue access (used by afk command)")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetRsQueueRole(string role)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var rsRole = Context.Guild.FindRole(role);
        if (rsRole == null)
        {
            await Context.Channel.BotResponse("Unknown role: " + role, ResponseType.error);
            return;
        }

        StateService.Set(Context.Guild.Id, "rs-queue-role", rsRole.Id);
        await Context.Channel.BotResponse("RS Queue role is set to: " + rsRole.Name, ResponseType.success);
    }

    [Command("set-bot-log-channel")]
    [Summary("set-bot-log-channel <channel>|log channel for the bot")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetBotLogChannel(SocketTextChannel channel)
    {
        await CleanupService.DeleteCommand(Context.Message);

        StateService.Set(Context.Guild.Id, "bot-log-channel", channel.Id);
        await Context.Channel.BotResponse("Bot log channel is set to: " + channel.Name, ResponseType.success);
    }

    [Command("set-compendium-apikey")]
    [Summary("set-compendium-apikey|connect Jarvis and the Compendium bot together")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetCompendiumApiKey([Remainder] string apiKey)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await CompendiumLogic.SetCompendiumApiKey(Context.Guild, Context.Channel, CurrentUser, apiKey);
        await Context.Channel.BotResponse("Compendium API key is changed.", ResponseType.success);
    }

    [Command("create-backup")]
    [Summary("create-backup|create and upload a backup")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task CreateBackup()
    {
        await CleanupService.DeleteCommand(Context.Message);
        await BackupLogic.UploadBackupToChannel(Context.Guild, Context.Channel);
    }
}
