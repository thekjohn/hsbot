namespace HsBot.Logic;

public static class WsSignupLogic
{
    private static readonly List<AccountSelectionEntry> _accountSelectionEntries = new();

    public static async Task RepostSignupWhenAllTeamsMatched(SocketGuild guild)
    {
        if (HasAnyActiveSignup(guild.Id))
            return;

        var teamIds = StateService.ListIds(guild.Id, "ws-team-");
        foreach (var teamId in teamIds)
        {
            var team = StateService.Get<WsTeam>(guild.Id, teamId);
            if (team != null && string.IsNullOrEmpty(team.Opponent))
                return;
        }

        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var channel = guild.GetTextChannel(alliance.WsSignupChannelId);
        if (channel == null)
        {
            // todo: log error
            return;
        }

        var now = DateTime.UtcNow;
        var diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
        var endsOn = now
            .AddDays(-1 * diff).Date // Monday 00:00
            .AddDays(6) // Sunday 00:00
            .AddHours(7); // Sunday 07:00

        await StartNew(guild, endsOn);
    }

    internal static async Task StartNew(SocketGuild guild, DateTime endsOn)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var channel = guild.GetTextChannel(alliance.WsSignupChannelId);

        var now = DateTime.UtcNow;
        var signup = new WsSignup()
        {
            StartedOn = now,
            EndsOn = endsOn,
            ChannelId = channel.Id,
            MessageId = 0,
        };

        var signupStateId = "ws-signup-active-" + now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        StateService.Set(guild.Id, signupStateId, signup);

        var memberRole = guild.GetRole(alliance.RoleId);
        if (memberRole != null)
        {
            await channel.SendMessageAsync(memberRole.Mention + " New signup form is online, we count on you! :point_down:");
        }

        await ShowSignupInfo(guild);

        await RepostSignups(guild);
    }

    internal static bool HasAnyActiveSignup(ulong guildId)
    {
        var signupStateIds = StateService.ListIds(guildId, "ws-signup-active-");
        return signupStateIds.Length > 0;
    }

    internal static async Task RepostSignups(SocketGuild guild)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var channel = guild.GetTextChannel(alliance.WsSignupChannelId);

        var ids = StateService.ListIds(guild.Id, "ws-signup-active-");
        foreach (var signupStateId in ids)
        {
            var signup = StateService.Get<WsSignup>(guild.Id, signupStateId);
            if (signup == null)
                continue;

            var content = BuildSignupContent(guild, signup, alliance);

            if (signup.MessageId != 0)
            {
                try
                {
                    await guild.GetTextChannel(signup.ChannelId).DeleteMessageAsync(signup.MessageId);
                }
                catch (Exception)
                {
                }
            }

            if (signup.EndsOn > DateTime.UtcNow && (signup.GuildEventId == null || !guild.Events.Any(x => x.Id == signup.GuildEventId)))
            {
                var evt = await guild.CreateEventAsync("WS signup", DateTime.UtcNow.AddMinutes(1), GuildScheduledEventType.External, GuildScheduledEventPrivacyLevel.Private, null, signup.EndsOn, null, "#" + channel.Name);
                signup.GuildEventId = evt.Id;
            }

            var sent = await channel.SendMessageAsync(embed: content);
            signup.ChannelId = channel.Id;
            signup.MessageId = sent.Id;
            StateService.Set(guild.Id, signupStateId, signup);

            if (signup.EndsOn > DateTime.UtcNow)
            {
                await sent.AddReactionsAsync(new IEmote[]
                {
                    new Emoji("💪"),
                    new Emoji("👍"),
                    new Emoji("😴"),
                    new Emoji("❌"),
                });
            }
        }
    }

    internal static async void AutomaticallyCloseThreadWorker(object obj)
    {
        while (true)
        {
            try
            {
                var now = DateTime.UtcNow;
                foreach (var guild in DiscordBot.Discord.Guilds)
                {
                    var ids = StateService.ListIds(guild.Id, "ws-signup-active-");
                    foreach (var signupStateId in ids)
                    {
                        var signup = StateService.Get<WsSignup>(guild.Id, signupStateId);
                        if (signup == null)
                            continue;

                        if (signup.EndsOn <= now.AddMinutes(-5))
                        {
                            var channel = guild.GetTextChannel(signup.ChannelId);
                            if (channel != null)
                            {
                                var alliance = AllianceLogic.GetAlliance(guild.Id);
                                if (alliance.WsDraftChannelId != 0)
                                {
                                    var draft = new WsDraftLogic.WsDraft()
                                    {
                                        OriginalSignup = signup,
                                        ChannelId = alliance.WsDraftChannelId,
                                        MessageId = 0,
                                        Teams = new List<WsTeam>(),
                                    };

                                    WsDraftLogic.SaveWsDraft(guild.Id, draft);
                                    await WsDraftLogic.RepostDraft(guild);
                                }

                                await RefreshSignup(guild, channel, signup.MessageId);
                            }

                            var newId = signupStateId.Replace("active", "archive", StringComparison.InvariantCultureIgnoreCase);
                            StateService.Rename(guild.Id, signupStateId, newId);
                        }
                        else
                        {
                            if (signup.Notify1dLeftMessageId == null
                                && signup.EndsOn.AddDays(-1) <= now)
                            {
                                var channel = guild.GetTextChannel(signup.ChannelId);
                                if (channel != null)
                                {
                                    var alliance = AllianceLogic.GetAlliance(guild.Id);
                                    if (alliance != null)
                                    {
                                        var memberRole = guild.GetRole(alliance.RoleId);
                                        var wsGuestRole = alliance.WsGuestRoleId != 0 ? guild.GetRole(alliance.WsGuestRoleId) : null;
                                        if (memberRole != null)
                                        {
                                            var sent = await channel.SendMessageAsync(memberRole.Mention
                                                + (wsGuestRole != null ? " " + wsGuestRole.Mention : "")
                                                + " WS signup ends in "
                                                + signup.EndsOn.Subtract(now.AddSeconds(-15)).ToIntervalStr(true, false) + "!");
                                            signup.Notify1dLeftMessageId = sent.Id;
                                            StateService.Set(guild.Id, signupStateId, signup);
                                            CleanupService.RegisterForDeletion(2 * 60 * 60, sent);
                                        }
                                    }
                                }
                            }

                            if (signup.Notify12hLeftMessageId == null
                                && signup.EndsOn.AddHours(-12) <= now)
                            {
                                var channel = guild.GetTextChannel(signup.ChannelId);
                                if (channel != null)
                                {
                                    var alliance = AllianceLogic.GetAlliance(guild.Id);
                                    if (alliance != null)
                                    {
                                        var memberRole = guild.GetRole(alliance.RoleId);
                                        var wsGuestRole = alliance.WsGuestRoleId != 0 ? guild.GetRole(alliance.WsGuestRoleId) : null;
                                        if (memberRole != null)
                                        {
                                            var sent = await channel.SendMessageAsync(memberRole.Mention
                                                + (wsGuestRole != null ? " " + wsGuestRole.Mention : "")
                                                + " WS signup ends in "
                                                + signup.EndsOn.Subtract(now.AddSeconds(-15)).ToIntervalStr(true, false) + "!");
                                            signup.Notify12hLeftMessageId = sent.Id;
                                            StateService.Set(guild.Id, signupStateId, signup);
                                            CleanupService.RegisterForDeletion(2 * 60 * 60, sent);
                                        }
                                    }
                                }
                            }

                            if (signup.Notify2hLeftMessageId == null
                                && signup.EndsOn.AddHours(-2) <= now)
                            {
                                var channel = guild.GetTextChannel(signup.ChannelId);
                                if (channel != null)
                                {
                                    var alliance = AllianceLogic.GetAlliance(guild.Id);
                                    if (alliance != null)
                                    {
                                        var memberRole = guild.GetRole(alliance.RoleId);
                                        var wsGuestRole = alliance.WsGuestRoleId != 0 ? guild.GetRole(alliance.WsGuestRoleId) : null;
                                        if (memberRole != null)
                                        {
                                            var sent = await channel.SendMessageAsync(memberRole.Mention
                                                + (wsGuestRole != null ? " " + wsGuestRole.Mention : "")
                                                + " WS signup ends in "
                                                + signup.EndsOn.Subtract(now.AddSeconds(-15)).ToIntervalStr(true, false) + "!");
                                            signup.Notify2hLeftMessageId = sent.Id;
                                            StateService.Set(guild.Id, signupStateId, signup);
                                            CleanupService.RegisterForDeletion(2 * 60 * 60, sent);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            Thread.Sleep(10000);
        }
    }

    internal static async Task RefreshSignup(SocketGuild guild, ISocketMessageChannel channel, ulong messageId)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);

        var ids = StateService.ListIds(guild.Id, "ws-signup-active-");
        foreach (var signupStateId in ids)
        {
            var signup = StateService.Get<WsSignup>(guild.Id, signupStateId);
            if (signup == null)
                continue;

            if (signup.MessageId != messageId)
                continue;

            var content = BuildSignupContent(guild, signup, alliance);
            var sent = await channel.ModifyMessageAsync(messageId, x => x.Embed = content);

            if (signup.EndsOn < DateTime.UtcNow)
                await sent.RemoveAllReactionsAsync();

            /*await sent.RemoveAllReactionsAsync();
            sent = await channel.GetMessageAsync(messageId) as IUserMessage;
            await sent.AddReactionsAsync(new IEmote[]
            {
                new Emoji("💪"),
                new Emoji("👍"),
                new Emoji("😴"),
                new Emoji("❌"),
            });*/

            return;
        }
    }

    internal static Embed BuildSignupContent(SocketGuild guild, WsSignup signup, AllianceLogic.AllianceInfo alliance)
    {
        var compMain = "";
        var compMainCnt = 0;
        foreach (var user in signup.CompetitiveUsers.Select(x => guild.GetUser(x)).Where(x => x != null).OrderBy(x => x.DisplayName))
        {
            compMainCnt++;
            compMain += (compMain == "" ? "" : "\n")
                + (user.Roles.Any(x => x.Id == alliance.WsGuestRoleId) ? alliance.AllyIcon + " " : "")
                + user.DisplayName;
        }

        var compAlt = "";
        var compAltCnt = 0;
        foreach (var alt in signup.CompetitiveAlts.Select(x => x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name).OrderBy(x => x))
        {
            compAltCnt++;
            compAlt += (compAlt == "" ? "" : "\n") + alt;
        }

        var casualMain = "";
        var casualMainCnt = 0;
        foreach (var user in signup.CasualUsers.Select(x => guild.GetUser(x)).Where(x => x != null).OrderBy(x => x.DisplayName))
        {
            casualMainCnt++;
            casualMain += (casualMain == "" ? "" : "\n")
                + (user.Roles.Any(x => x.Id == alliance.WsGuestRoleId) ? alliance.AllyIcon + " " : "")
                + user.DisplayName;
        }

        var casualAlt = "";
        var casualAltCnt = 0;
        foreach (var alt in signup.CasualAlts.Select(x => x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name).OrderBy(x => x))
        {
            casualAltCnt++;
            casualAlt += (casualAlt == "" ? "" : "\n") + alt;
        }

        var inactiveMain = "";
        var inactiveMainCnt = 0;
        foreach (var user in signup.InactiveUsers.Select(x => guild.GetUser(x)).Where(x => x != null).OrderBy(x => x.DisplayName))
        {
            inactiveMainCnt++;
            inactiveMain += (inactiveMain == "" ? "" : "\n")
                + (user.Roles.Any(x => x.Id == alliance.WsGuestRoleId) ? alliance.AllyIcon + " " : "")
                + user.DisplayName;
        }

        var inactiveAlt = "";
        var inactiveAltCnt = 0;
        foreach (var alt in signup.InactiveAlts.Select(x => x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name).OrderBy(x => x))
        {
            inactiveAltCnt++;
            inactiveAlt += (inactiveAlt == "" ? "" : "\n") + alt;
        }

        var eb = new EmbedBuilder()
            .WithTitle(signup.EndsOn > DateTime.UtcNow
                ? "WS signup - ends on " + signup.EndsOn.ToString("yyyy MMMM dd. HH:mm", CultureInfo.InvariantCulture) + " UTC"
                : "WS signup - ended on " + signup.EndsOn.ToString("yyyy MMMM dd. HH:mm", CultureInfo.InvariantCulture) + " UTC")
            .WithColor(Color.Orange)
            .AddField("Please set your commitment level during this White Star event. Your team will count on you, so please choose wisely!", "We promise you don't get into a team stronger than your commitment level, but you can still end up in a lower commitment level team.", false)
            .AddField((compMainCnt + compAltCnt).ToStr() + " Competitive", "💪", true)
            .AddField((casualMainCnt + casualAltCnt).ToStr() + " Casual", "👍", true)
            .AddField((inactiveMainCnt + inactiveAltCnt).ToStr() + " Inactive", "😴", true)
            .AddField(compMainCnt.ToStr() + " Competitive Main", compMain != "" ? compMain : "-", true)
            .AddField(casualMainCnt.ToStr() + " Casual Main", casualMain != "" ? casualMain : "-", true)
            .AddField(inactiveMainCnt.ToStr() + " Inactive Main", inactiveMain != "" ? inactiveMain : "-", true)
            .AddField(compAltCnt.ToStr() + " Competitive Alt", compAlt != "" ? compAlt : "-", true)
            .AddField(casualAltCnt.ToStr() + " Casual Alt", casualAlt != "" ? casualAlt : "-", true)
            .AddField(inactiveAltCnt.ToStr() + " Inactive Alt", inactiveAlt != "" ? inactiveAlt : "-", true)
            .WithFooter(DiscordBot.FunFooter, guild.CurrentUser.GetAvatarUrl())
            .WithThumbnailUrl(guild.Emotes.FirstOrDefault(x => x.Name == "whitestar")?.Url);

        return eb.Build();
    }

    internal static async Task HandleReactions(SocketReaction reaction, bool added)
    {
        if (!added)
            return;

        if (reaction.User.Value.IsBot)
            return;

        var channel = reaction.Channel as SocketGuildChannel;
        var guild = channel.Guild;

        AccountSelectionEntry entry = null;
        lock (_accountSelectionEntries)
        {
            entry = _accountSelectionEntries.Find(x => x.Channelid == reaction.Channel.Id && x.MessageId == reaction.MessageId);
            if (entry != null)
                _accountSelectionEntries.Remove(entry);
        }

        if (entry != null)
        {
            await reaction.Channel.DeleteMessageAsync(reaction.MessageId);

            if (reaction.Emote.Name == "Ⓜ️")
            {
                await ApplyReaction(entry.OriginalEmoteName, reaction.Channel, reaction.UserId, guild, entry.SignupStateId, null);
            }
            else if (reaction.Emote.Name == "✅")
            {
                await ApplyReaction(entry.OriginalEmoteName, reaction.Channel, reaction.UserId, guild, entry.SignupStateId, null);
                foreach (var alt in entry.Alts)
                {
                    await ApplyReaction(entry.OriginalEmoteName, reaction.Channel, reaction.UserId, guild, entry.SignupStateId, alt);
                }
            }
            else
            {
                var index = Array.IndexOf(AltsLogic.NumberEmoteNames.Select(x => guild.GetEmote(x).Name).ToArray(), reaction.Emote.Name);
                if (index != -1)
                {
                    var alt = entry.Alts[index];
                    await ApplyReaction(entry.OriginalEmoteName, reaction.Channel, reaction.UserId, guild, entry.SignupStateId, alt);
                }
            }

            return;
        }

        string signupStateId = null;
        var ids = StateService.ListIds(guild.Id, "ws-signup-active-");
        WsSignup signup = null;
        foreach (var ssid in ids)
        {
            signup = StateService.Get<WsSignup>(guild.Id, ssid);
            if (signup != null && signup.MessageId == reaction.MessageId)
            {
                signupStateId = ssid;
                break;
            }

            signup = null;
        }

        if (signup == null)
            return;

        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        if (signup.EndsOn <= DateTime.UtcNow)
        {
            await RepostSignups(guild);
            await reaction.Channel.BotResponse("Signup is already closed!", ResponseType.error);
            return;
        }

        var msg = await reaction.Channel.GetMessageAsync(reaction.MessageId);
        await msg.RemoveReactionAsync(reaction.Emote, reaction.UserId);

        var isAlt = alliance.Alts.Find(x => x.AltUserId == reaction.UserId);
        if (isAlt != null)
        {
            await reaction.Channel.BotResponse("This alt has to be signed up by using the main's discord account: " + (guild.GetUser(isAlt.OwnerUserId)?.Mention ?? "<unknown discord user>"), ResponseType.error);
            return;
        }

        var timeZone = TimeZoneLogic.GetUserTimeZone(guild.Id, reaction.UserId);
        if (timeZone == null)
        {
            await reaction.Channel.BotResponse("You have to set your timezone with `" + DiscordBot.CommandPrefix + "timezone-set` command before WS signup!", ResponseType.error);
            return;
        }

        var altCount = alliance.Alts
            .Count(x => x.OwnerUserId == reaction.UserId);

        if (altCount == 0 || reaction.Emote.Name == "❌")
        {
            await ApplyReaction(reaction.Emote.Name, reaction.Channel, reaction.UserId, guild, signupStateId, null);
        }
        else
        {
            await ShowAltsPanel(guild, reaction.Channel, signupStateId, reaction.Emote.Name, alliance, guild.GetUser(reaction.UserId));
        }
    }

    private static async Task ApplyReaction(string emoteName, ISocketMessageChannel channel, ulong userId, SocketGuild guild, string signupStateId, AllianceLogic.Alt alt)
    {
        var signup = StateService.Get<WsSignup>(guild.Id, signupStateId);
        if (emoteName != "❌")
        {
            if (alt == null)
            {
                signup.CompetitiveUsers.Remove(userId);
                signup.CasualUsers.Remove(userId);
                signup.InactiveUsers.Remove(userId);

                List<ulong> list = null;
                switch (emoteName)
                {
                    case "💪":
                        list = signup.CompetitiveUsers;
                        break;
                    case "👍":
                        list = signup.CasualUsers;
                        break;
                    case "😴":
                        list = signup.InactiveUsers;
                        break;
                }

                if (!list.Contains(userId))
                    list.Add(userId);
            }
            else
            {
                signup.CompetitiveAlts.RemoveAll(x =>
                    x.OwnerUserId == alt.OwnerUserId
                    && ((x.AltUserId != null && x.AltUserId == alt.AltUserId)
                        || (x.Name != null && x.Name == alt.Name)));

                signup.CasualAlts.RemoveAll(x =>
                    x.OwnerUserId == alt.OwnerUserId
                    && ((x.AltUserId != null && x.AltUserId == alt.AltUserId)
                        || (x.Name != null && x.Name == alt.Name)));

                signup.InactiveAlts.RemoveAll(x =>
                    x.OwnerUserId == alt.OwnerUserId
                    && ((x.AltUserId != null && x.AltUserId == alt.AltUserId)
                        || (x.Name != null && x.Name == alt.Name)));

                List<AllianceLogic.Alt> list = null;
                switch (emoteName)
                {
                    case "💪":
                        list = signup.CompetitiveAlts;
                        break;
                    case "👍":
                        list = signup.CasualAlts;
                        break;
                    case "😴":
                        list = signup.InactiveAlts;
                        break;
                }

                var existingMatch = list.Find(x =>
                    x.OwnerUserId == alt.OwnerUserId
                    && ((x.AltUserId != null && x.AltUserId == alt.AltUserId)
                        || (x.Name != null && x.Name == alt.Name)));

                if (existingMatch == null)
                    list.Add(alt);
            }
        }
        else
        {
            signup.CompetitiveUsers.Remove(userId);
            signup.CasualUsers.Remove(userId);
            signup.InactiveUsers.Remove(userId);
            signup.CompetitiveAlts.RemoveAll(x => x.OwnerUserId == userId);
            signup.CasualAlts.RemoveAll(x => x.OwnerUserId == userId);
            signup.InactiveAlts.RemoveAll(x => x.OwnerUserId == userId);
        }

        StateService.Set(guild.Id, signupStateId, signup);
        await RefreshSignup(guild, channel, signup.MessageId);
    }

    internal static async Task ShowSignupInfo(SocketGuild guild)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var channel = guild.GetTextChannel(alliance.WsSignupChannelId);

        var eb = new EmbedBuilder()
            .WithTitle("WS signup info")
            .WithColor(Color.Magenta)
            .WithFooter(DiscordBot.FunFooter, guild.CurrentUser.GetAvatarUrl())
            .WithThumbnailUrl(guild.Emotes.FirstOrDefault(x => x.Name == "whitestar")?.Url);

        var info = StateService.Get<SignupInfo>(guild.Id, "wssignup-info") ?? new SignupInfo();
        if (info != null)
        {
            if (!string.IsNullOrEmpty(info.GenericInfo))
                eb.AddField("Generic rules", info.GenericInfo);

            if (!string.IsNullOrEmpty(info.CompetitiveInfo))
                eb.AddField("Competitive", info.CompetitiveInfo);

            if (!string.IsNullOrEmpty(info.CasualInfo))
                eb.AddField("Casual", info.CasualInfo);

            if (!string.IsNullOrEmpty(info.InactiveInfo))
                eb.AddField("Inactive", info.InactiveInfo);
        }

        if (info.MessageId != 0)
        {
            try
            {
                await guild.GetTextChannel(info.ChannelId).DeleteMessageAsync(info.MessageId);
            }
            catch (Exception)
            {
            }
        }

        var sent = await channel.SendMessageAsync(null, embed: eb.Build());
        info.ChannelId = channel.Id;
        info.MessageId = sent.Id;
        StateService.Set(guild.Id, "wssignup-info", info);
    }

    internal static async Task SetSignupInfo(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, WsTeamCommitmentLevel teamCommitmentLevel, string text)
    {
        var info = StateService.Get<SignupInfo>(guild.Id, "wssignup-info") ?? new SignupInfo();
        string oldValue;
        switch (teamCommitmentLevel)
        {
            case WsTeamCommitmentLevel.Competitive:
                oldValue = info.CompetitiveInfo;
                info.CompetitiveInfo = text;
                await channel.BotResponse(teamCommitmentLevel.ToString() + " signup info is changed from ```" + oldValue + "``` to ```" + text + "```", ResponseType.successStay);
                break;
            case WsTeamCommitmentLevel.Casual:
                oldValue = info.CasualInfo;
                info.CasualInfo = text;
                await channel.BotResponse(teamCommitmentLevel.ToString() + " signup info is changed from ```" + oldValue + "``` to ```" + text + "```", ResponseType.successStay);
                break;
            case WsTeamCommitmentLevel.Inactive:
                oldValue = info.InactiveInfo;
                info.InactiveInfo = text;
                await channel.BotResponse(teamCommitmentLevel.ToString() + " signup info is changed from ```" + oldValue + "``` to ```" + text + "```", ResponseType.successStay);
                break;
            case WsTeamCommitmentLevel.Unknown:
                oldValue = info.GenericInfo;
                info.GenericInfo = text;
                await channel.BotResponse("Generic signup info is changed from ```" + oldValue + "``` to ```" + text + "```", ResponseType.successStay);
                break;
        }

        StateService.Set(guild.Id, "wssignup-info", info);
    }

    private static async Task ShowAltsPanel(SocketGuild guild, ISocketMessageChannel channel, string signupStateId, string originalEmoteName, AllianceLogic.AllianceInfo alliance, SocketGuildUser user)
    {
        var description = new StringBuilder();
        var alts = alliance.Alts
            .Where(x => x.OwnerUserId == user.Id)
            .ToList();

        description
            .AppendLine(":white_check_mark: all accounts")
            .Append(":m: ")
            .AppendLine(user.DisplayName);

        for (var i = 0; i < alts.Count; i++)
        {
            var displayName = alts[i].AltUserId != null
                ? guild.GetUser(alts[i].AltUserId.Value)?.DisplayName ?? "<unknown discord user>"
                : alts[i].Name;

            description
                .Append(AltsLogic.NumberEmoteNames[i])
                .Append(' ')
                .AppendLine(displayName);
        }

        if (alts.Count == 0)
            description.Append("<none>");

        var eb = new EmbedBuilder()
            .WithTitle(":point_right: " + user.DisplayName + "'s accounts")
            .WithDescription("Use the reactions to sign up all or one of your accounts.\n\n" + description.ToString())
            .WithColor(Color.Red)
            .WithFooter("This message will self-destruct in 30 seconds.");

        var sent = await channel.SendMessageAsync(embed: eb.Build());
        CleanupService.RegisterForDeletion(30, sent);

        lock (_accountSelectionEntries)
        {
            var now = DateTime.UtcNow;
            _accountSelectionEntries.RemoveAll(x => x.AddedOn.AddMinutes(2) < now);
            _accountSelectionEntries.Add(new AccountSelectionEntry()
            {
                AddedOn = now,
                Channelid = channel.Id,
                MessageId = sent.Id,
                User = user,
                SignupStateId = signupStateId,
                Alts = alts,
                OriginalEmoteName = originalEmoteName,
            });
        }

        var reactions = new List<IEmote>
        {
            Emoji.Parse(":white_check_mark:"),
            Emoji.Parse(":m:")
        };

        reactions.AddRange(AltsLogic.NumberEmoteNames
            .Take(alts.Count)
            .Select(x => guild.GetEmote(x)));

        await sent.AddReactionsAsync(reactions);
    }

    private class AccountSelectionEntry
    {
        public DateTime AddedOn { get; init; }
        public ulong Channelid { get; init; }
        public ulong MessageId { get; init; }
        public SocketGuildUser User { get; init; }
        public string SignupStateId { get; init; }
        public string OriginalEmoteName { get; init; }
        public List<AllianceLogic.Alt> Alts { get; init; }
    }

    public class SignupInfo
    {
        public string GenericInfo { get; set; }
        public string CompetitiveInfo { get; set; }
        public string CasualInfo { get; set; }
        public string InactiveInfo { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
    }

    public class WsSignup
    {
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public DateTime StartedOn { get; init; }
        public DateTime EndsOn { get; init; }

        public List<ulong> CompetitiveUsers { get; init; } = new();
        public List<ulong> CasualUsers { get; init; } = new();
        public List<ulong> InactiveUsers { get; init; } = new();

        public List<AllianceLogic.Alt> CompetitiveAlts { get; init; } = new();
        public List<AllianceLogic.Alt> CasualAlts { get; init; } = new();
        public List<AllianceLogic.Alt> InactiveAlts { get; init; } = new();

        public ulong? GuildEventId { get; set; }

        public ulong? Notify1dLeftMessageId { get; set; }
        public ulong? Notify12hLeftMessageId { get; set; }
        public ulong? Notify2hLeftMessageId { get; set; }
    }
}
