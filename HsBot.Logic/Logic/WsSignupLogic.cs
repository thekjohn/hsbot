namespace HsBot.Logic
{
    using System.Globalization;
    using System.Text;
    using System.Threading.Tasks;
    using Discord;
    using Discord.WebSocket;

    public static class WsSignupLogic
    {
        private static readonly List<AccountSelectionEntry> _accountSelectionEntries = new();

        internal static async Task StartNew(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, DateTime ends)
        {
            var now = DateTime.UtcNow;
            var signup = new WsSignup()
            {
                StartedOn = now,
                EndsOn = ends,
                ChannelId = channel.Id,
                MessageId = 0,
            };

            var signupStateId = "ws-signup-active-" + now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            Services.State.Set(guild.Id, signupStateId, signup);
            await RepostSignups(guild, channel, currentUser);
        }

        internal static async Task RepostSignups(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser)
        {
            if (currentUser != null && !AllianceLogic.IsMember(guild.Id, currentUser))
            {
                Services.Cleanup.RegisterForDeletion(10,
                    await channel.SendMessageAsync(":x: Only alliance members can use this command."));
                return;
            }

            var ids = Services.State.ListIds(guild.Id, "ws-signup-active-");
            foreach (var signupStateId in ids)
            {
                var signup = Services.State.Get<WsSignup>(guild.Id, signupStateId);
                if (signup == null)
                    continue;

                var alliance = AllianceLogic.GetAlliance(guild.Id);

                var content = BuildSignupContent(guild, signup);

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

                if (signup.GuildEventId == null || !guild.Events.Any(x => x.Id == signup.GuildEventId))
                {
                    var evt = await guild.CreateEventAsync("WS signup", DateTime.UtcNow.AddMinutes(1), GuildScheduledEventType.External, GuildScheduledEventPrivacyLevel.Private, null, signup.EndsOn, null, "#" + channel.Name);
                    signup.GuildEventId = evt.Id;
                }

                var sent = await channel.SendMessageAsync(embed: content);
                await sent.AddReactionsAsync(new IEmote[]
                {
                    new Emoji("💪"),
                    new Emoji("👍"),
                    new Emoji("😴"),
                    new Emoji("❌"),
                });

                signup.ChannelId = channel.Id;
                signup.MessageId = sent.Id;
                Services.State.Set(guild.Id, signupStateId, signup);
            }
        }

        internal static async Task RefreshSignup(SocketGuild guild, ISocketMessageChannel channel, ulong messageId)
        {
            var ids = Services.State.ListIds(guild.Id, "ws-signup-active-");
            foreach (var signupStateId in ids)
            {
                var signup = Services.State.Get<WsSignup>(guild.Id, signupStateId);
                if (signup == null)
                    continue;

                var content = BuildSignupContent(guild, signup);

                if (signup.MessageId != messageId)
                    continue;

                var sent = await channel.ModifyMessageAsync(messageId, x => x.Embed = content);
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

        internal static Embed BuildSignupContent(SocketGuild guild, WsSignup signup)
        {
            var compMain = "";
            foreach (var user in signup.CompetitiveUsers.Select(x => guild.GetUser(x)).Where(x => x != null).OrderBy(x => x.DisplayName))
            {
                compMain += (compMain == "" ? "" : "\n") + user.DisplayName;
            }

            var compAlt = "";
            foreach (var alt in signup.CompetitiveAlts.Select(x => x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name).OrderBy(x => x))
            {
                compAlt += (compAlt == "" ? "" : "\n") + alt;
            }

            var casualMain = "";
            foreach (var user in signup.CasualUsers.Select(x => guild.GetUser(x)).Where(x => x != null).OrderBy(x => x.DisplayName))
            {
                casualMain += (casualMain == "" ? "" : "\n") + user.DisplayName;
            }

            var casualAlt = "";
            foreach (var alt in signup.CasualAlts.Select(x => x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name).OrderBy(x => x))
            {
                casualAlt += (casualAlt == "" ? "" : "\n") + alt;
            }

            var inactiveMain = "";
            foreach (var user in signup.InactiveUsers.Select(x => guild.GetUser(x)).Where(x => x != null).OrderBy(x => x.DisplayName))
            {
                inactiveMain += (inactiveMain == "" ? "" : "\n") + user.DisplayName;
            }

            var inactiveAlt = "";
            foreach (var alt in signup.InactiveAlts.Select(x => x.AltUserId != null ? guild.GetUser(x.AltUserId.Value)?.DisplayName ?? "<unknown discord user>" : x.Name).OrderBy(x => x))
            {
                inactiveAlt += (inactiveAlt == "" ? "" : "\n") + alt;
            }

            var embedBuilder = new EmbedBuilder()
                .WithTitle("WS signup - ends on " + signup.EndsOn.ToString("yyyy MMMM dd. HH:mm", CultureInfo.InvariantCulture) + " UTC")
                .AddField("Please express your commitment level during this White Star event. Your team will count on you, so please choose wisely!", "We promise you don't get into a stronger team than your commitment level, but you can still end up in a lower commitment level team.", false)
                .AddField("Competitive 💪", "Highly responsive, focused, no sanc!", true)
                .AddField("Casual 👍", "Responsive, but no commitments. Sanc allowed.", true)
                .AddField("Inactive 😴", "Only bar filling. Sanc recommended.", true)
                .AddField("Competitive Main 💪", compMain != "" ? compMain : "-", true)
                .AddField("Casual Main 👍", casualMain != "" ? casualMain : "-", true)
                .AddField("Inactive Main 😴", inactiveMain != "" ? inactiveMain : "-", true)
                .AddField("Competitive Alt 💪", compAlt != "" ? compAlt : "-", true)
                .AddField("Casual Alt 👍", casualAlt != "" ? casualAlt : "-", true)
                .AddField("Inactive Alt 😴", inactiveAlt != "" ? inactiveAlt : "-", true);

            if (signup.ClosedOn != null)
            {
                embedBuilder.WithFooter("*Form closed on " + signup.ClosedOn.Value.ToString("yyyy MMMM dd. HH:mm:ss", CultureInfo.InvariantCulture) + " UTC*");
            }

            return embedBuilder.Build();
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
                    var index = Array.IndexOf(AltsLogic.NumberEmoteNames.Select(x => Emoji.Parse(x).Name).ToArray(), reaction.Emote.Name);
                    if (index != -1)
                    {
                        var alt = entry.Alts[index];
                        await ApplyReaction(entry.OriginalEmoteName, reaction.Channel, reaction.UserId, guild, entry.SignupStateId, alt);
                    }
                }

                return;
            }

            string signupStateId = null;
            var ids = Services.State.ListIds(guild.Id, "ws-signup-active-");
            WsSignup signup = null;
            foreach (var ssid in ids)
            {
                signup = Services.State.Get<WsSignup>(guild.Id, ssid);
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

            var altCount = alliance.Alts
                .Count(x => x.OwnerUserId == reaction.UserId);

            var msg = await reaction.Channel.GetMessageAsync(reaction.MessageId);
            await msg.RemoveReactionAsync(reaction.Emote, reaction.UserId);

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
            var signup = Services.State.Get<WsSignup>(guild.Id, signupStateId);
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

            Services.State.Set(guild.Id, signupStateId, signup);
            await RefreshSignup(guild, channel, signup.MessageId);
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

            var embed = new EmbedBuilder()
                .WithTitle(":point_right: " + user.DisplayName + "'s accounts")
                .WithDescription("Use the reactions to sign up all or one of your accoutns.\n\n" + description.ToString())
                .WithFooter("This message will be automatically deleted after 30 seconds.");

            var sent = await channel.SendMessageAsync(embed: embed.Build());

            var reactions = new List<IEmote>
            {
                Emoji.Parse(":white_check_mark:"),
                Emoji.Parse(":m:")
            };

            reactions.AddRange(AltsLogic.NumberEmoteNames
                .Take(alts.Count)
                .Select(x => Emoji.Parse(x)));

            await sent.AddReactionsAsync(reactions);

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

            Services.Cleanup.RegisterForDeletion(30, sent);
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

        internal class WsSignup
        {
            public ulong ChannelId { get; set; }
            public ulong MessageId { get; set; }
            public DateTime StartedOn { get; init; }
            public DateTime EndsOn { get; init; }
            public DateTime? ClosedOn { get; init; }

            public List<ulong> CompetitiveUsers { get; init; } = new();
            public List<ulong> CasualUsers { get; init; } = new();
            public List<ulong> InactiveUsers { get; init; } = new();

            public List<AllianceLogic.Alt> CompetitiveAlts { get; init; } = new();
            public List<AllianceLogic.Alt> CasualAlts { get; init; } = new();
            public List<AllianceLogic.Alt> InactiveAlts { get; init; } = new();

            public ulong? GuildEventId { get; set; }
        }
    }
}