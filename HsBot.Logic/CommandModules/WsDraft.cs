﻿namespace HsBot.Logic
{
    using Discord.Commands;
    using Discord.WebSocket;

    [Summary("White Star Draft")]
    public class WsDraft : BaseModule
    {
        [Command("wsresults")]
        [Summary("wsresults <teamName>|list the previous results of a WS team")]
        public async Task ShowWsWesults(string teamName)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsDraftLogic.ShowWsWesults(Context.Guild, Context.Channel, CurrentUser, teamName);
        }

        [Command("draft-add-team")]
        [Summary("draft-add-team <teamName> <corpName>|Create a team in the draft. Team name must be an existing role, like 'WS1'. Corp is where the scan will happen.")]
        public async Task AddTeamToDraft(string teamName, string corpName)
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

            await WsDraftLogic.AddDraftTeam(Context.Guild, Context.Channel, CurrentUser, role, corp);
        }

        [Command("draft-remove-team")]
        [Summary("draft-remove-team <teamName>|Remove a team from the draft.")]
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
        public async Task AddToWsTeam(string operation, string teamName, [Remainder] string userNames)
        {
            await CleanupService.DeleteCommand(Context.Message);

            if (operation != "add" && operation != "remove")
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

            var mains = new List<SocketGuildUser>();
            var alts = new List<AllianceLogic.Alt>();
            var unknownNames = new List<string>();
            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
            foreach (var userName in userNames.Split(' '))
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

        [Command("draft-close")]
        [Summary("draft-close|close the draft and create the teams")]
        public async Task CloseDraft()
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsDraftLogic.CloseDraft(Context.Guild, Context.Channel, CurrentUser);
        }

        [Command("wsscan")]
        [Summary("wsscan <teamName>|indicates as WS team is scanning")]
        public async Task SetWsScan(string teamName)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsDraftLogic.WsScanStarted(Context.Guild, Context.Channel, CurrentUser, teamName);
        }

        [Command("wsmatched")]
        [Summary("wsmatched <teamName> <ends_in>|indicates as WS team is matched and ends in a specific amount of time (ex: 4d22h)")]
        public async Task SetWsScan(string teamName, string opponentName, string endsIn)
        {
            await CleanupService.DeleteCommand(Context.Message);
            await WsDraftLogic.WsMatched(Context.Guild, Context.Channel, CurrentUser, teamName, opponentName, endsIn);
        }
    }
}