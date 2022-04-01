namespace HsBot.Logic;

public static class AltsLogic
{
    public static string[] NumberEmoteNames { get; } = new[] { ":one:", ":two:", ":three:", ":four:", ":five:", ":six:", ":seven:", ":eight:", ":nine:", ":keycap_ten:", "11", "12" };
    public static string[] NumberEmoteMentions { get; } = new[] { ":one:", ":two:", ":three:", ":four:", ":five:", ":six:", ":seven:", ":eight:", ":nine:", ":keycap_ten:", "<:11:958004577838432276>", "<:12:958004713310261259>" };

    internal static async Task ShowAlts(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, SocketGuildUser user)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null || user == null)
            return;

        if (!user.Roles.Any(x => x.Id == alliance.RoleId))
        {
            await channel.BotResponse("Only members of " + alliance.Name + " can query the alts.", ResponseType.error);
            return;
        }

        var entry = StateService.Get<MessageEntry>(guild.Id, "myalts-panel-" + user.Id.ToStr());
        if (entry != null)
        {
            try
            {
                await guild.GetTextChannel(entry.ChannelId).DeleteMessageAsync(entry.MessageId);
            }
            catch (Exception)
            {
            }
        }

        var description = new StringBuilder();
        var alts = alliance.Alts
            .Where(x => x.OwnerUserId == user.Id)
            .ToList();

        for (var i = 0; i < alts.Count; i++)
        {
            var displayName = alts[i].AltUserId != null
                ? guild.GetUser(alts[i].AltUserId.Value)?.DisplayName ?? "<unknown discord user>"
                : alts[i].Name;

            description
                .Append(":x: ")
                .Append(NumberEmoteNames[i])
                .Append(' ')
                .AppendLine(displayName);
        }

        if (alts.Count == 0)
            description.Append("<none>");

        var eb = new EmbedBuilder()
            .WithTitle(":point_right: " + user.DisplayName + "'s registered alt(s)");

        eb = (currentUser.Id == user.Id)
            ? eb.WithDescription(
                (alts.Count > 0
                    ? "React with a number to remove an alt, or use "
                    : "Use ")
                + "`" + DiscordBot.CommandPrefix + "addalt <altname>` to add a new alt. If your alt has a discord account, you should use `.addalt @<altdiscordaccount>` mention for better bot experience in the future."
                + "\n\n" + description.ToString())
            : eb.WithDescription(description.ToString());

        eb.WithFooter("This message will self-destruct in 30 seconds.");

        var sent = await channel.SendMessageAsync(embed: eb.Build());

        if (currentUser.Id == user.Id)
        {
            await sent.AddReactionsAsync(NumberEmoteNames
            .Take(alts.Count)
            .Select(x => Emoji.Parse(x))
            .ToArray());
        }

        StateService.Set(guild.Id, "myalts-panel-" + user.Id.ToStr(), new MessageEntry()
        {
            ChannelId = channel.Id,
            MessageId = sent.Id,
        });

        CleanupService.RegisterForDeletion(30, sent);
    }

    internal static async Task HandleReactions(SocketReaction reaction, bool added)
    {
        if (reaction.User.Value.IsBot)
            return;

        var channel = reaction.Channel as SocketGuildChannel;
        var user = reaction.User.Value as SocketGuildUser;

        var entryId = "myalts-panel-" + reaction.UserId.ToStr();
        var entry = StateService.Get<MessageEntry>(channel.Guild.Id, entryId);
        if (entry == null || entry.MessageId != reaction.MessageId)
            return;

        var index = Array.IndexOf(NumberEmoteNames.Select(x => channel.Guild.GetEmote(x).Name).ToArray(), reaction.Emote.Name);
        if (index == -1)
            return;

        var alliance = AllianceLogic.GetAlliance(channel.Guild.Id);
        if (alliance == null)
            return;

        var alts = alliance.Alts
            .Where(x => x.OwnerUserId == reaction.UserId)
            .ToList();

        if (index >= alts.Count)
            return;

        var alt = alts[index];
        alliance.Alts.Remove(alt);
        AllianceLogic.SaveAlliance(channel.Guild.Id, alliance);

        try
        {
            await channel.Guild.GetTextChannel(entry.ChannelId).DeleteMessageAsync(entry.MessageId);
        }
        catch (Exception)
        {
        }

        StateService.Delete(channel.Guild.Id, entryId);

        await reaction.Channel.SendMessageAsync(":white_check_mark: " + channel.Guild.GetUser(reaction.UserId).Mention + "'s alt is removed: `"
            + (alt.AltUserId != null
                ? channel.Guild.GetUser(alt.AltUserId.Value)?.DisplayName ?? "<unknown discord user>"
                : alt.Name) + "`");

        await ShowAlts(channel.Guild, reaction.Channel, user, user);
    }

    public static async Task AddAlt(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser ownerUser, string altName)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var altUser = guild.FindUser(null, altName);

        var alt = new AllianceLogic.Alt()
        {
            OwnerUserId = ownerUser.Id,
            Name = altUser == null ? altName : null,
            AltUserId = altUser?.Id,
        };

        if (alliance.Alts.Any(x =>
            x.OwnerUserId == alt.OwnerUserId
            && ((x.AltUserId != null && x.AltUserId == alt.AltUserId)
                || (x.Name != null && x.Name == alt.Name))))
        {
            await channel.BotResponse(ownerUser.Mention + " already registered this alt: " + altName, ResponseType.error);
            return;
        }

        alliance.Alts.Add(alt);
        AllianceLogic.SaveAlliance(guild.Id, alliance);

        await channel.SendMessageAsync(":white_check_mark: " + ownerUser.Mention + "'s new alt is registered: `" + (altUser?.DisplayName ?? altName) + "`");

        await ShowAlts(guild, channel, ownerUser, ownerUser);
    }

    private class MessageEntry
    {
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
    }
}
