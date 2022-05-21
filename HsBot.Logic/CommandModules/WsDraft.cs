namespace HsBot.Logic;

[Summary("White Star Draft")]
[RequireContext(ContextType.Guild)]
public class WsDraft : BaseModule
{
    [Command("wssignup")]
    [Summary("wssignup|show active signup form(s)")]
    [RequireMinimumAllianceRole(AllianceRole.Member)]
    public async Task ShowSignup()
    {
        await CleanupService.DeleteCommand(Context.Message);
        await WsSignupLogic.RepostSignups(Context.Guild);
    }

    [Command("draft-add-team")]
    [Summary("draft-add-team <teamName> <corpName> <commitmentLevel>|Create a team in the draft. Team name must be an existing role, like 'WS1'. Corp is where the scan will happen.")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task AddTeamToDraft(string teamName, string corpName, string commitmentLevel)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var role = Context.Guild.FindRole(teamName);
        if (role == null)
        {
            await Context.Channel.BotResponse("Unknown role: " + teamName, ResponseType.error);
            return;
        }

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
        var corp = Context.Guild.FindCorp(alliance, corpName);
        if (corp == null)
        {
            await Context.Channel.BotResponse("Unknown corporation: " + corpName, ResponseType.error);
            return;
        }

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

        await WsDraftLogic.AddDraftTeam(Context.Guild, Context.Channel, CurrentUser, role, corp, teamCommitmentLevel);
    }

    [Command("draft-remove-team")]
    [Summary("draft-remove-team <teamName>|Remove a team from the draft.")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task RemoveTeamFromDraft(string teamName)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var role = Context.Guild.FindRole(teamName);
        if (role == null)
        {
            await Context.Channel.BotResponse("Unknown role: " + teamName, ResponseType.error);
            return;
        }

        await WsDraftLogic.RemoveDraftTeam(Context.Guild, Context.Channel, CurrentUser, role);
    }

    [Command("draft")]
    [Summary("draft <add/remove> <teamName> <list of user names>|add/remove one or more users to a WS team.")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task AddToWsTeam(string operation, string teamName, [Remainder] string userNames)
    {
        await CleanupService.DeleteCommand(Context.Message);

        if (operation is not "add" and not "remove")
        {
            await Context.Channel.BotResponse("Operation must be `add` or `remove`.", ResponseType.error);
            return;
        }

        var role = Context.Guild.FindRole(teamName);
        if (role == null)
        {
            await Context.Channel.BotResponse("Unknown role: " + teamName, ResponseType.error);
            return;
        }

        var names = userNames.Split(' ');

        var mains = new List<SocketGuildUser>();
        var alts = new List<AllianceLogic.Alt>();
        var unknownNames = new List<string>();
        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
        foreach (var userName in names)
        {
            SocketGuildUser main = null;
            AllianceLogic.Alt alt = null;

            var user = Context.Guild.FindUser(CurrentUser, userName);
            if (user != null)
            {
                var a = alliance.Alts.Find(x => x.AltUserId == user.Id);
                if (a != null)
                {
                    alt = a;
                }
                else
                {
                    main = user;
                }
            }
            else
            {
                var matchingAlts = alliance.Alts
                    .Where(x => x.AltUserId == null && x.Name?.StartsWith(userName, StringComparison.InvariantCultureIgnoreCase) == true)
                    .ToList();

                if (matchingAlts.Count == 1)
                    alt = matchingAlts[0];
            }

            if (main == null && alt == null)
                unknownNames.Add(userName);
            else if (main != null)
                mains.Add(main);
            else
                alts.Add(alt);
        }

        if (unknownNames.Count > 0)
            await Context.Channel.BotResponse("Uknown names: " + string.Join(", ", unknownNames.Select(x => "`" + x + "`")), ResponseType.error);

        await WsDraftLogic.ManageDraft(Context.Guild, Context.Channel, CurrentUser, role, operation == "add", mains, alts, unknownNames);
    }

    [Command("close-draft")]
    [Summary("close-draft|close the draft and create the teams")]
    [RequireMinimumAllianceRole(AllianceRole.Leader)]
    public async Task CloseDraft()
    {
        await CleanupService.DeleteCommand(Context.Message);
        await WsDraftLogic.CloseDraft(Context.Guild, Context.Channel, CurrentUser);
    }

    [Command("set-wssignup-info")]
    [Summary("set-wssignup-info|set the signup info text, posted when a new signup is created")]
    [RequireMinimumAllianceRole(AllianceRole.Leader)]
    public async Task SetSignupInfo(string commitmentLevel, [Remainder] string text)
    {
        await CleanupService.DeleteCommand(Context.Message);
        var teamCommitmentLevel = commitmentLevel.ToLowerInvariant() switch
        {
            "competitive" => WsTeamCommitmentLevel.Competitive,
            "casual" => WsTeamCommitmentLevel.Casual,
            "inactive" => WsTeamCommitmentLevel.Inactive,
            "generic" => WsTeamCommitmentLevel.Unknown,
            _ => WsTeamCommitmentLevel.Unknown,
        };

        await WsSignupLogic.SetSignupInfo(Context.Guild, Context.Channel, CurrentUser, teamCommitmentLevel, text);
    }

    [Command("wssignup-info")]
    [Summary("wssignup-info|diplay the signup info text")]
    [RequireMinimumAllianceRole(AllianceRole.Admiral)]
    public async Task ShowSignupInfo()
    {
        await CleanupService.DeleteCommand(Context.Message);
        await WsSignupLogic.ShowSignupInfo(Context.Guild);
    }
}
