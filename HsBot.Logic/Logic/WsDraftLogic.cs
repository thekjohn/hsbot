namespace HsBot.Logic;

public static class WsDraftLogic
{
    internal static async Task AddDraftTeam(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, SocketRole role, AllianceLogic.Corp corp, WsTeamCommitmentLevel commitmentLevel)
    {
        var draft = GetWsDraft(guild.Id);
        if (draft == null)
        {
            await channel.BotResponse("There is no active WS draft.", ResponseType.error);
            return;
        }

        var team = draft.Teams.Find(x => x.RoleId == role.Id);
        if (team == null)
        {
            team = new WsTeam()
            {
                RoleId = role.Id,
            };

            draft.Teams.Add(team);
        }

        team.CommitmentLevel = commitmentLevel;
        team.Name = role.Name;
        team.Members = new WsTeamMembers()
        {
            CorpAbbreviation = corp.Abbreviation,
            Alts = new List<AllianceLogic.Alt>(),
            Mains = new List<ulong>(),
        };

        foreach (var user in guild.Users.Where(x => x.Roles.Any(r => r.Id == role.Id)))
        {
            await user.RemoveRoleAsync(role);
        }

        SaveWsDraft(guild.Id, draft);
        await RepostDraft(guild);
    }

    internal static async Task RemoveDraftTeam(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, SocketRole role)
    {
        var draft = GetWsDraft(guild.Id);
        if (draft == null)
        {
            await channel.BotResponse("There is no active WS draft.", ResponseType.error);
            return;
        }

        var team = draft.Teams.Find(x => x.RoleId == role.Id);
        if (team == null)
        {
            await channel.BotResponse("Specified team doesn't exist in the WS draft.", ResponseType.error);
            return;
        }

        draft.Teams.Remove(team);

        SaveWsDraft(guild.Id, draft);
        await RepostDraft(guild);
    }

    internal static async Task CloseDraft(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser)
    {
        var draft = GetWsDraft(guild.Id);
        if (draft == null)
        {
            await channel.BotResponse("There is no active WS draft.", ResponseType.error);
            return;
        }

        var alliance = AllianceLogic.GetAlliance(guild.Id);

        foreach (var team in draft.Teams)
        {
            var cnt = team.Members.Mains.Count + team.Members.Alts.Count;
            if (cnt != 5 && cnt != 10 && cnt != 15)
            {
                await channel.BotResponse("Cannot close draft because all teams must have 5, 10 or 15 members.", ResponseType.error);
                return;
            }

            /*if (!team.Members.Mains.Any(x => guild.GetUser(x)?.Roles.Any(r => r.Id == alliance.AdmiralRoleId) == true))
            {
                await channel.BotResponse("Cannot close draft because " + team.Name + " has no admiral.", ResponseType.error);
                return;
            }*/
        }

        StateService.Rename(guild.Id, "ws-draft", "ws-draft-archive-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssffff", CultureInfo.InvariantCulture));

        var admiralRole = alliance.AdmiralRoleId != 0 ? guild.GetRole(alliance.AdmiralRoleId) : null;

        var ch = channel;
        if (alliance.WsAnnounceChannelId != 0)
        {
            var ac = guild.GetTextChannel(alliance.WsAnnounceChannelId);
            if (ac != null)
                ch = ac;
        }

        var teams = draft.Teams
            .Where(x => guild.GetRole(x.RoleId) != null && guild.FindCorp(alliance, x.Members.CorpAbbreviation) != null)
            .OrderBy(x => guild.GetRole(x.RoleId).Name)
            .ToList();

        var channelIndex = 0;

        foreach (var team in teams)
        {
            var teamRole = guild.GetRole(team.RoleId);
            foreach (var existingUser in guild.Users.Where(x => x.Roles.Any(y => y.Id == teamRole.Id)))
            {
                await existingUser.RemoveRoleAsync(teamRole);
            }
        }

        foreach (var team in teams)
        {
            var teamRole = guild.GetRole(team.RoleId);
            var corp = guild.FindCorp(alliance, team.Members.CorpAbbreviation);

            var newUsersWithRole = team.Members.Mains
                .Concat(team.Members.Alts.Select(x => x.OwnerUserId))
                .Concat(team.Members.Alts.Where(x => x.AltUserId != null).Select(x => x.AltUserId.Value))
                .Distinct();

            foreach (var userId in newUsersWithRole)
            {
                var user = guild.GetUser(userId);
                if (user?.Roles.Any(x => x.Id == teamRole.Id) == false)
                    await user.AddRoleAsync(teamRole);
            }

            var usersToMention = team.Members.Mains
                .Concat(team.Members.Alts.Select(x => x.OwnerUserId))
                .Select(x => guild.GetUser(x))
                .Where(x => x != null)
                .OrderBy(x => x.DisplayName)
                .Distinct();

            var eb = new EmbedBuilder()
                .WithTitle(teamRole.Name);

            if (team.Members.Mains.Count > 0)
            {
                eb.AddField("Main", string.Join("\n",
                     team.Members.Mains
                        .Select(x => guild.GetUser(x))
                        .Where(x => x != null)
                        .OrderBy(x => x.DisplayName)
                        .Select(x => alliance.GetUserCorpIcon(x) + x.DisplayName)
                    ));
            }

            if (team.Members.Alts.Count > 0)
            {
                eb.AddField("Alt", string.Join("\n",
                    team.Members.Alts
                        .Select(x => x.AltUserId != null
                            ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>"
                            : x.Name)
                        .OrderBy(x => x)
                    ));
            }

            await AnnounceWS(guild, channel, teamRole.Name + " is ready, please head to " + corp.FullName + " (" + corp.Abbreviation + ") for scan!", embed: eb.Build());

            var battleRoom = await guild.CreateTextChannelAsync(teamRole.Name.ToLower(), f =>
            {
                f.Position = channelIndex;
            });

            team.BattleRoomChannelId = battleRoom.Id;

            await battleRoom.AddPermissionOverwriteAsync(guild.EveryoneRole, new OverwritePermissions(viewChannel: PermValue.Deny));
            await battleRoom.AddPermissionOverwriteAsync(teamRole, new OverwritePermissions(viewChannel: PermValue.Allow));

            var compendiumRole = guild.Roles.FirstOrDefault(x => x.Tags?.BotId == 548952307384188949);
            if (compendiumRole != null)
                await battleRoom.AddPermissionOverwriteAsync(compendiumRole, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow));

            await battleRoom.SendMessageAsync(teamRole.Name + " battleroom is ready, please head to " + corp.FullName + " (" + corp.Abbreviation + ") for scan!",
                embed: eb.Build());

            var maxDuration = ThreadArchiveDuration.OneDay;
            if (guild.PremiumTier == PremiumTier.Tier1)
                maxDuration = ThreadArchiveDuration.ThreeDays;
            else if (guild.PremiumTier == PremiumTier.Tier2 || guild.PremiumTier == PremiumTier.Tier3)
                maxDuration = ThreadArchiveDuration.OneWeek;

            var ordersMsg = await battleRoom.SendMessageAsync("Orders will be posted in the #orders thread."
                + "\n" + string.Join(" ", usersToMention.Select(x => x.Mention)));
            var ordersThread = await battleRoom.CreateThreadAsync("orders", ThreadType.PublicThread, maxDuration, ordersMsg);
            team.OrdersChannelId = ordersThread.Id;

            if (admiralRole != null)
            {
                await battleRoom.AddPermissionOverwriteAsync(admiralRole, new OverwritePermissions(manageMessages: PermValue.Allow));

                var admiralMsg = await battleRoom.SendMessageAsync("Admirals will plan in the #admiral thread."
                    + "\n" + string.Join(" ", usersToMention.Select(x => x.Mention)));

                var admiralThread = await battleRoom.CreateThreadAsync("admiral", ThreadType.PublicThread, maxDuration, admiralMsg);
                team.AdmiralChannelId = admiralThread.Id;

                await admiralThread.SendMessageAsync(
                    ":information_source: Type " + DiscordBot.CommandPrefix + "`wsscan` here when scan is started in " + corp.FullName + "."
                    + "\n:information_source: Make sure the corp is closed before the scan to prevent a high influence pilot joining to the corp and ruining the scan."
                    + "\n:information_source: Please give the pilots a convenient amount of time (up to 18 hours) to show up in " + corp.FullName + " (" + corp.Abbreviation + ").");
            }

            channelIndex++;

            WsLogic.AddWsTeam(guild.Id, team);
        }

        await HelpLogic.ShowAllianceInfo(guild, ch, alliance);
    }

    internal static async Task ManageDraft(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, SocketRole role, bool add, List<SocketGuildUser> mains, List<AllianceLogic.Alt> alts, List<string> unknownNames)
    {
        var draft = GetWsDraft(guild.Id);
        if (draft == null)
        {
            await channel.BotResponse("There is no active WS draft.", ResponseType.error);
            return;
        }

        var team = draft.Teams.Find(x => x.RoleId == role.Id);
        if (team == null)
        {
            await channel.BotResponse("WS team `" + role.Name + "` is not initialized yet! Use `" + DiscordBot.CommandPrefix + "draft-add-team` first!", ResponseType.error);
            return;
        }

        if (add)
        {
            foreach (var main in mains)
            {
                if (!team.Members.Mains.Contains(main.Id))
                    team.Members.Mains.Add(main.Id);
            }

            foreach (var alt in alts)
            {
                if (!team.Members.Alts.Any(x => x.Equals(alt)))
                    team.Members.Alts.Add(alt);
            }
        }

        foreach (var t in add ? draft.Teams.Where(x => x != team) : draft.Teams.Where(x => x == team))
        {
            t.Members.Mains.RemoveAll(x => mains.Any(y => y.Id == x));
            t.Members.Alts.RemoveAll(x => alts.Any(y => y.Equals(x)));
        }

        SaveWsDraft(guild.Id, draft);

        await RepostDraft(guild);
    }

    public static async Task RepostDraft(SocketGuild guild)
    {
        var draft = GetWsDraft(guild.Id);
        if (draft == null)
            return;

        var channel = guild.GetTextChannel(draft.ChannelId);
        if (draft.MessageId != 0)
        {
            try
            {
                await channel.DeleteMessageAsync(draft.MessageId);
            }
            catch (Exception)
            {
            }
        }

        var eb = new EmbedBuilder()
            .WithTitle("DRAFT")
            .WithDescription(":point_right: add a new team: `" + DiscordBot.CommandPrefix + "draft-add-team <roleName> <corpName> <commitmentLevel>`"
                + "\n:point_right: remove a team: `" + DiscordBot.CommandPrefix + "draft-remove-team <roleName>`"
                + "\n:point_right: add users to a team: `" + DiscordBot.CommandPrefix + "draft add <roleName> <userNames>`"
                + "\n:point_right: remove users from a team: `" + DiscordBot.CommandPrefix + "draft remove <roleName> <userNames>`"
                + "\n:point_right: close draft and create teams, ready to scan: `" + DiscordBot.CommandPrefix + "close-draft`"
                + "\nAdding users to a team will remove them from all other teams automatically.")
            .WithColor(new Color(0, 255, 0))
            .WithFooter(DiscordBot.FunFooter, guild.CurrentUser.GetAvatarUrl())
            .WithCurrentTimestamp();

        var userList = draft.OriginalSignup.CompetitiveUsers.Where(x => !draft.Contains(x)).ToList();
        if (userList.Count > 0)
        {
            eb.AddField(userList.Count.ToStr() + " Competitive Main", string.Join(" ", userList
                  .Select(x => guild.GetUser(x))
                  .OrderBy(x => x?.DisplayName ?? "<unknown discord user>")
                  .Select(x => "`" + x.DisplayName + "`")));
        }

        var altList = draft.OriginalSignup.CompetitiveAlts.Where(x => !draft.Contains(x)).ToList();
        if (altList.Count > 0)
        {
            eb
              .AddField(altList.Count.ToStr() + " Competitive Alts", string.Join(" ", altList
                  .Select(x => x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name)
                  .OrderBy(x => x)
                  .Select(x => "`" + x + "`")));
        }

        userList = draft.OriginalSignup.CasualUsers.Where(x => !draft.Contains(x)).ToList();
        if (userList.Count > 0)
        {
            eb.AddField(userList.Count.ToStr() + " Casual Main", string.Join(" ", userList
                  .Select(x => guild.GetUser(x))
                  .Where(x => x != null)
                  .OrderBy(x => x.DisplayName)
                  .Select(x => "`" + x.DisplayName + "`")));
        }

        altList = draft.OriginalSignup.CasualAlts.Where(x => !draft.Contains(x)).ToList();
        if (altList.Count > 0)
        {
            eb.AddField(altList.Count.ToStr() + " Casual Alt", string.Join(" ", altList
                .Select(x => x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name)
                .OrderBy(x => x)
                .Select(x => "`" + x + "`")));
        }

        userList = draft.OriginalSignup.InactiveUsers.Where(x => !draft.Contains(x)).ToList();
        if (userList.Count > 0)
        {
            eb.AddField(userList.Count.ToStr() + " Inactive Main", string.Join(" ", userList
              .Select(x => guild.GetUser(x))
              .Where(x => x != null)
              .OrderBy(x => x.DisplayName)
              .Select(x => "`" + x.DisplayName + "`")));
        }

        altList = draft.OriginalSignup.InactiveAlts.Where(x => !draft.Contains(x)).ToList();
        if (altList.Count > 0)
        {
            eb.AddField(altList.Count.ToStr() + " Inactive Alt", string.Join(" ", altList
                .Select(x => x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name)
                .OrderBy(x => x)
                .Select(x => "`" + x + "`")));
        }

        foreach (var team in draft.Teams)
        {
            var role = guild.GetRole(team.RoleId);
            if (role == null)
                continue;

            eb.AddField((team.Members.Mains.Count + team.Members.Alts.Count).ToStr() + " " + role.Name + " (" + team.Members.CorpAbbreviation + ", " + team.CommitmentLevel.ToString().ToLowerInvariant() + ")",
                "mains: " + string.Join(" ", team.Members.Mains.Select(x => "`" + (guild.GetUser(x)?.DisplayName ?? "<unknown discord user>") + "`")) +
                "\nalts: " + string.Join(" ", team.Members.Alts.Select(x => "`" + (x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name) + "`"))
                );
        }

        var sent = await channel.SendMessageAsync(null, embed: eb.Build());
        draft.MessageId = sent.Id;
        SaveWsDraft(guild.Id, draft);
    }

    public static async Task AnnounceWS(SocketGuild guild, ISocketMessageChannel channel, string message, Embed embed = null)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);

        if (alliance.WsAnnounceChannelId != 0)
        {
            var ac = guild.GetTextChannel(alliance.WsAnnounceChannelId);
            if (ac != null)
                channel = ac;
        }

        await channel.SendMessageAsync(message, embed: embed);
    }

    public static WsDraft GetWsDraft(ulong guildId)
    {
        var res = StateService.Get<WsDraft>(guildId, "ws-draft");
        if (res != null)
        {
            foreach (var team in res.Teams)
            {
                if (team.Members.Mains == null)
                    team.Members.Mains = new List<ulong>();

                if (team.Members.Alts == null)
                    team.Members.Alts = new List<AllianceLogic.Alt>();
            }
        }
        return res;
    }

    public static void SaveWsDraft(ulong guildId, WsDraft draft)
    {
        StateService.Set(guildId, "ws-draft", draft);
    }

    public class WsDraft
    {
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public WsSignupLogic.WsSignup OriginalSignup { get; set; }
        public List<WsTeam> Teams { get; set; } = new List<WsTeam>();

        public bool Contains(ulong userId)
        {
            return Teams.Any(x => x.Members.Mains.Contains(userId));
        }

        public bool Contains(AllianceLogic.Alt alt)
        {
            return Teams.Any(x => x.Members.Alts.Any(y => y.Equals(alt)));
        }
    }
}
