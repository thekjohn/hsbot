namespace HsBot.Logic
{
    using System.Globalization;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;

    [Summary("White Stars")]
    public class Ws : BaseModule
    {
        [Command("wssignup")]
        [Summary("wssignup|show active signup form(s)")]
        public async Task ShowSignup()
        {
            await Context.Message.DeleteAsync();
            await RepostSignups(Context.Guild, Context.Channel, CurrentUser);
        }

        [Command("remind")]
        [Summary("remind <who> <when> <message>|remind you about something at a given time\nex.: 'remind me 25m drone' or 'remind @User 2h16m drone'")]
        public async Task Remind(string who, string when, [Remainder] string message)
        {
            await Context.Message.DeleteAsync();
            await RemindLogic.Remind(Context.Guild, Context.Channel, CurrentUser, who, when, message);
        }

        internal static async Task RepostSignups(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser)
        {
            if (currentUser != null && !AllianceLogic.IsMember(guild.Id, currentUser))
            {
                await channel.SendMessageAsync("Only alliance members can use this command.");
                return;
            }

            var ids = Services.State.ListIds(guild.Id, "ws-signup-active-");
            foreach (var signupStateId in ids)
            {
                var signup = Services.State.Get<WsSignup>(guild.Id, signupStateId);
                if (signup == null)
                    continue;

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

                await channel.ModifyMessageAsync(messageId, x => x.Embed = content);
                return;
            }
        }

        internal static Embed BuildSignupContent(SocketGuild guild, WsSignup signup)
        {
            var comp = "***Pilots***";
            var casual = "***Pilots***";
            var inactive = "***Pilots***";

            foreach (var userId in signup.CompetitiveUsers)
            {
                var user = guild.GetUser(userId);
                if (user == null)
                    continue;

                comp += (comp == "" ? "" : "\n") + user.Nickname;
            }

            comp += "\n\n***Alts***";

            foreach (var alt in signup.CompetitiveAlts)
            {
                comp += (comp == "" ? "" : "\n") + alt.NickName;
            }

            foreach (var userId in signup.CasualUsers)
            {
                var user = guild.GetUser(userId);
                if (user == null)
                    continue;

                casual += (casual == "" ? "" : "\n") + user.Nickname;
            }

            casual += "\n\n***Alts***";

            foreach (var alt in signup.CasualAlts)
            {
                casual += (casual == "" ? "" : "\n") + alt.NickName;
            }

            foreach (var userId in signup.InactiveUsers)
            {
                var user = guild.GetUser(userId);
                if (user == null)
                    continue;

                inactive += (inactive == "" ? "" : "\n") + user.Nickname;
            }

            inactive += "\n\n***Alts***";

            foreach (var alt in signup.InactiveAlts)
            {
                inactive += (inactive == "" ? "" : "\n") + alt.NickName;
            }

            var embedBuilder = new EmbedBuilder()
                .WithTitle("WS signup - " + signup.StartedOn.ToString("yyyy MMMM dd. HH:mm:ss", CultureInfo.InvariantCulture) + " UTC")
                .AddField("Please express your MINIMUM commitment level during this White Star event. Your team will count on you, so please choose wisely!", "Do not forget: based on the draft, the only thing guaranteed is you don't get into a stronger team than your commitment level, but you can still end up in a lower commitment level team.", false)
                .AddField("Competitive 💪", comp, true)
                .AddField("Casual 👍", casual, true)
                .AddField("Inactive 😴", inactive, true);

            if (signup.ClosedOn != null)
            {
                embedBuilder.WithFooter("*Form closed on " + signup.ClosedOn.Value.ToString("yyyy MMMM dd. HH:mm:ss", CultureInfo.InvariantCulture) + " UTC*");
            }

            return embedBuilder.Build();
        }

        internal static async Task HandleReactions(SocketReaction reaction, bool added)
        {
            if (reaction.User.Value.IsBot)
                return;

            var channel = reaction.Channel as SocketGuildChannel;

            WsSignup signup = null;
            string signupStateId = null;
            var ids = Services.State.ListIds(channel.Guild.Id, "ws-signup-active-");
            foreach (var ssid in ids)
            {
                signup = Services.State.Get<WsSignup>(channel.Guild.Id, ssid);
                if (signup != null && signup.MessageId == reaction.MessageId)
                {
                    signupStateId = ssid;
                    break;
                }

                signup = null;
            }

            if (signup == null)
                return;

            switch (reaction.Emote.Name)
            {
                case "💪":
                    if (added && !signup.CompetitiveUsers.Contains(reaction.UserId))
                        signup.CompetitiveUsers.Add(reaction.UserId);
                    if (!added)
                        signup.CompetitiveUsers.Remove(reaction.UserId);
                    break;
                case "👍":
                    if (added && !signup.CasualUsers.Contains(reaction.UserId))
                        signup.CasualUsers.Add(reaction.UserId);
                    if (!added)
                        signup.CasualUsers.Remove(reaction.UserId);
                    break;
                case "😴":
                    if (added && !signup.InactiveUsers.Contains(reaction.UserId))
                        signup.InactiveUsers.Add(reaction.UserId);
                    if (!added)
                        signup.InactiveUsers.Remove(reaction.UserId);
                    break;
                case "❌":
                    signup.CompetitiveUsers.Remove(reaction.UserId);
                    signup.CasualUsers.Remove(reaction.UserId);
                    signup.InactiveUsers.Remove(reaction.UserId);
                    signup.CompetitiveAlts.RemoveAll(x => x.UserId == reaction.UserId);
                    signup.CasualAlts.RemoveAll(x => x.UserId == reaction.UserId);
                    signup.InactiveAlts.RemoveAll(x => x.UserId == reaction.UserId);
                    break;
            }

            Services.State.Set(channel.Guild.Id, signupStateId, signup);
            await RefreshSignup(channel.Guild, channel as ISocketMessageChannel, signup.MessageId);
        }

        internal class WsSignup
        {
            public ulong ChannelId { get; set; }
            public ulong MessageId { get; set; }
            public DateTime StartedOn { get; init; }
            public DateTime? ClosedOn { get; init; }

            public List<ulong> CompetitiveUsers { get; init; } = new();
            public List<ulong> CasualUsers { get; init; } = new();
            public List<ulong> InactiveUsers { get; init; } = new();

            public List<WsAlt> CompetitiveAlts { get; init; } = new();
            public List<WsAlt> CasualAlts { get; init; } = new();
            public List<WsAlt> InactiveAlts { get; init; } = new();

            public class WsAlt
            {
                public string NickName { get; set; }
                public ulong UserId { get; set; }
            }
        }
    }
}