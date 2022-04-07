namespace HsBot.Logic;

[Summary("WS Battleroom")]
[RequireContext(ContextType.Guild)]
public class Ws : BaseModule
{
    [Command("remind")]
    [Summary("remind <who> <when> <message>|remind you about something at a given time\nex.: 'remind me 25m drone' or 'remind @User 2h16m drone'")]
    [RequireMinimumAllianceRole(AllianceRole.WSGuest)]
    public async Task Remind(string who, string when, [Remainder] string message)
    {
        await CleanupService.DeleteCommand(Context.Message);
        if (string.Equals(who, "ws", StringComparison.InvariantCultureIgnoreCase))
        {
            await RemindLogic.AddReminderWS(Context.Guild, Context.Channel, CurrentUser, when, message);
        }
        else
        {
            await RemindLogic.AddReminder(Context.Guild, Context.Channel, CurrentUser, who, when, message);
        }
    }

    [Command("remind")]
    [Summary("remind <who> list|list of reminders\nex.: 'remind me list'")]
    [RequireMinimumAllianceRole(AllianceRole.WSGuest)]
    public async Task Remind(string who, string operation)
    {
        await CleanupService.DeleteCommand(Context.Message);
        if (string.Equals(operation, "list", StringComparison.InvariantCultureIgnoreCase))
        {
            if (string.Equals(who, "ws", StringComparison.InvariantCultureIgnoreCase))
            {
                await RemindLogic.RemindListWS(Context.Guild, Context.Channel, CurrentUser);
            }
            else
            {
                await RemindLogic.RemindList(Context.Guild, Context.Channel, CurrentUser, who);
            }
        }
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

    [Command("wsresults-team")]
    [Summary("wsresults-team <teamName>|list the previous results of a WS team")]
    [RequireMinimumAllianceRole(AllianceRole.Member)]
    public async Task ShowWsResultsOfTeam(string teamName)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await WsResultsLogic.ShowWsResultsOfTeam(Context.Guild, Context.Channel, CurrentUser, teamName);
    }

    [Command("wsresults-opponent")]
    [Summary("wsresults-opponent <opponentName>|list the previous results of an opponent")]
    [RequireMinimumAllianceRole(AllianceRole.Member)]
    public async Task ShowWsWesults(string opponentName)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await WsResultsLogic.ShowWsResultsOfOpponent(Context.Guild, Context.Channel, CurrentUser, opponentName);
    }

    [Command("wsscan")]
    [Summary("wsscan|indicates as WS team is scanning")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task WsTeamScanning()
    {
        await CleanupService.DeleteCommand(Context.Message);
        await WsLogic.WsTeamScanning(Context.Guild, Context.Channel, CurrentUser);
    }

    [Command("wssetcommitmentlevel")]
    [Summary("wssetcommitmentlevel <commitmentLevel>|Change the commitment level of an existing WS team.")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task WsSetTeamCommitmentLevel(string commitmentLevel)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var teamCommitmentLevel = commitmentLevel.ToLowerInvariant() switch
        {
            "competitive" => WsTeamCommitmentLevel.Competitive,
            "casual" => WsTeamCommitmentLevel.Casual,
            "inactive" => WsTeamCommitmentLevel.Inactive,
            _ => WsTeamCommitmentLevel.Unknown,
        };

        if (teamCommitmentLevel == WsTeamCommitmentLevel.Unknown)
        {
            await Context.Channel.BotResponse("Team commitment level must be one of the following values: `competitive`, `casual`, `inactive`.", ResponseType.error);
            return;
        }

        await WsLogic.SetWsTeamCommitmentLevel(Context.Guild, Context.Channel, CurrentUser, teamCommitmentLevel);
    }

    [Command("wsmatched")]
    [Summary("wsmatched <ends_in> <opponent_name>|indicates the WS scan finished and the WS ends in a specific amount of time (ex: 4d22h)")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task WsTeamMatched(string endsIn, [Remainder] string opponentName)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await WsLogic.WsTeamMatched(Context.Guild, Context.Channel, CurrentUser, opponentName, endsIn);
    }

    [Command("wssetend")]
    [Summary("wssetend <ends_in>|changes the end time of the WS (ex: 4d22h)")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task SetWsEnd(string endsIn)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await WsLogic.SetWsTeamEnd(Context.Guild, Context.Channel, CurrentUser, endsIn);
    }

    [Command("wsclose")]
    [Summary("wsclose <score> <opponentScore>|record the result of the WS, and destroy the related channels")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task WsTeamClosed(int score, int opponentScore)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await WsLogic.CloseWsTeam(Context.Guild, Context.Channel, CurrentUser, score, opponentScore);
    }

    [Command("wsmod-mining")]
    [Alias("mining")]
    [Summary("mining [filterName]|list the mining modules of the team. filter is optional.")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task WsModMining(string filterName = null)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await WsModLogic.WsModMining(Context.Guild, Context.Channel, CurrentUser, filterName);
    }

    [Command("wsmod-defense")]
    [Alias("defense")]
    [Summary("defense [filterName]|list the defensive modules of the team. filter is optional.")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task WsModDefense(string filterName = null)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await WsModLogic.WsModDefense(Context.Guild, Context.Channel, CurrentUser, filterName);
    }

    [Command("wsmod-rocket")]
    [Alias("rocket")]
    [Summary("rocket [filterName]|list the rocket modules of the team. filter is optional.")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task WsModRocket(string filterName = null)
    {
        await CleanupService.DeleteCommand(Context.Message);

        await WsModLogic.WsModRocket(Context.Guild, Context.Channel, CurrentUser, filterName);
    }

    [Command("wsclassify")]
    [Summary("wsclassify|list members of the WS team and all the module filters they match")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task WsClassify()
    {
        await CleanupService.DeleteCommand(Context.Message);

        await WsModLogic.Classify(Context.Guild, Context.Channel, CurrentUser);
    }

    [Command("mfadd")]
    [Summary("mfadd <filterName> <moduleList>|create a predefined, named filter. Example: `wsmod-create kidnapper bond 8 deltashield 8 tw 8 impulse 8`")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task CreateModFilter(string filterName, [Remainder] string moduleList)
    {
        await CleanupService.DeleteCommand(Context.Message);

        filterName = filterName.Trim();
        if (filterName.Contains(' ', StringComparison.InvariantCultureIgnoreCase))
        {
            await Context.Channel.BotResponse("Filter name cannot contain spaces.", ResponseType.error);
            return;
        }

        var parts = moduleList.Split(' ');
        if (parts.Length % 2 != 0)
        {
            await Context.Channel.BotResponse("The number of the module list arguments must be even: a list of module name + module level pairs.", ResponseType.error);
            return;
        }

        var filter = new ModuleFilter()
        {
            Name = filterName,
        };

        for (var i = 0; i < parts.Length; i += 2)
        {
            var name = parts[i].Trim();

            var property = CompendiumResponseMap.Find(name);
            if (property == null)
            {
                await Context.Channel.BotResponse("Unknown module name: `" + parts[i] + "`", ResponseType.error);
                return;
            }

            if (!int.TryParse(parts[i + 1], out var level) || level < 0 || level > 12)
            {
                await Context.Channel.BotResponse("Module level must be between 0 and 12: `" + parts[i + 1] + "`", ResponseType.error);
                return;
            }

            filter.Modules.Add(new ModuleFilterEntry()
            {
                Name = property.Name,
                Level = level,
            });
        }

        await ModuleFilterLogic.CreateModuleFilter(Context.Guild, Context.Channel, CurrentUser, filter);
    }

    [Command("mflist")]
    [Summary("mflist|list all registered module filters")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task ListModFilters()
    {
        await CleanupService.DeleteCommand(Context.Message);
        await ModuleFilterLogic.ListModuleFilters(Context.Guild, Context.Channel, CurrentUser);
    }
}
