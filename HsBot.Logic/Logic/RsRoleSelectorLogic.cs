namespace HsBot.Logic;

public static class RsRoleSelectorLogic
{
    public static async Task ShowPanel(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var panel = new RsRolePanel()
        {
            UserId = user.Id,
            CreatedOn = DateTime.UtcNow,
        };

        StateService.Set(guild.Id, "rs-role-panel-" + user.Id, panel);

        await RepostRsRoleSelectorPanel(guild, channel, user.Id);
    }

    internal static async Task RepostRsRoleSelectorPanel(SocketGuild guild, ISocketMessageChannel channel, ulong userId)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var entryId = "rs-role-panel-" + userId.ToStr();
        var panel = StateService.Get<RsRolePanel>(guild.Id, entryId);
        if (panel == null)
            return;

        try
        {
            await guild.GetTextChannel(panel.ChannelId).DeleteMessageAsync(panel.MainRoleMessageId);
        }
        catch (Exception)
        {
        }

        try
        {
            await guild.GetTextChannel(panel.ChannelId).DeleteMessageAsync(panel.ThreeOfFourRoleMessageId);
        }
        catch (Exception)
        {
        }

        var user = guild.GetUser(userId);
        if (user == null)
            return;

        var eb = new EmbedBuilder()
            .WithTitle(user.DisplayName + " please specify which RS level you want to get a ping every time somebody join the queue?")
            .WithDescription("Current roles: " + (user.Roles.GetMainRsRolesDescending().Any()
                ? string.Join(" ", user.Roles.GetMainRsRolesDescending().Select(x => x.Mention))
                : "none"))
            .WithColor(Color.Green)
            .WithFooter("This message will self-destruct in 60 seconds.");

        var mainMessage = await channel.SendMessageAsync(embed: eb.Build());

        eb = new EmbedBuilder()
            .WithTitle(user.DisplayName + " please specify which RS level you want to get a DM and ping when there are exactly 3/4 players in the queue?")
            .WithDescription("Current roles: " + (user.Roles.GetThreeOfFourRsRolesDescending().Any()
                ? string.Join(" ", user.Roles.GetThreeOfFourRsRolesDescending().Select(x => x.Mention))
                : "none"))
            .WithColor(Color.Blue)
            .WithFooter("This message will self-destruct in 60 seconds.");

        var threeOfFourMessage = await channel.SendMessageAsync(embed: eb.Build());

        panel.ChannelId = channel.Id;
        panel.MainRoleMessageId = mainMessage.Id;
        panel.ThreeOfFourRoleMessageId = threeOfFourMessage.Id;
        StateService.Set(guild.Id, "rs-role-panel-" + user.Id, panel);

        var validRsLevels = Enumerable.Range(4, 12)
            .Where(level => guild.Roles.Any(x => x.Name == "RS" + level.ToStr()))
            .ToList();

        await mainMessage.AddReactionsAsync(validRsLevels
            .Select(level => guild.GetEmote(AltsLogic.NumberEmoteNames[level - 1]))
            .ToArray());

        await threeOfFourMessage.AddReactionsAsync(validRsLevels
            .Select(level => guild.GetEmote(AltsLogic.NumberEmoteNames[level - 1]))
            .ToArray());

        CleanupService.RegisterForDeletion(60, mainMessage);
        CleanupService.RegisterForDeletion(60, threeOfFourMessage);
    }

    internal static async Task RefreshRsRoleSelectorPanel(SocketGuild guild, ISocketMessageChannel channel, ulong userId, List<SocketRole> roles)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var user = guild.GetUser(userId);
        if (user == null)
            return;

        var entryId = "rs-role-panel-" + user.Id.ToStr();
        var panel = StateService.Get<RsRolePanel>(guild.Id, entryId);
        if (panel == null)
            return;

        var eb = new EmbedBuilder()
            .WithTitle(user.DisplayName + " please specify which RS level you want to get notifications every time somebody join the queue?")
            .WithDescription("Current roles: " + (roles.GetMainRsRolesDescending().Any()
                ? string.Join(" ", roles.GetMainRsRolesDescending().Select(x => x.Mention))
                : "none"))
            .WithThumbnailUrl(guild.Emotes.FirstOrDefault(x => x.Name == "redstar")?.Url)
            .WithColor(Color.Green)
            .WithFooter("This message will self-destruct in 60 seconds.");

        await channel.ModifyMessageAsync(panel.MainRoleMessageId, x => x.Embed = eb.Build());

        eb = new EmbedBuilder()
            .WithTitle(user.DisplayName + " please specify which RS level you want to get notifications only when there are 3/4 players in the queue?")
            .WithDescription("Current roles: " + (roles.GetThreeOfFourRsRolesDescending().Any()
                ? string.Join(" ", roles.GetThreeOfFourRsRolesDescending().Select(x => x.Mention))
                : "none"))
            .WithThumbnailUrl(guild.Emotes.FirstOrDefault(x => x.Name == "redstar")?.Url)
            .WithColor(Color.Blue)
            .WithFooter("This message will self-destruct in 60 seconds.");

        await channel.ModifyMessageAsync(panel.ThreeOfFourRoleMessageId, x => x.Embed = eb.Build());
    }

    internal static async Task HandleReactions(SocketReaction reaction, bool added)
    {
        if (!added)
            return;

        if (reaction.User.Value.IsBot)
            return;

        var msg = await reaction.Channel.GetMessageAsync(reaction.MessageId);
        await msg.RemoveReactionAsync(reaction.Emote, reaction.UserId);

        var channel = reaction.Channel as SocketGuildChannel;
        var user = reaction.User.Value as SocketGuildUser;

        var entryId = "rs-role-panel-" + reaction.UserId.ToStr();
        var panel = StateService.Get<RsRolePanel>(channel.Guild.Id, entryId);
        if (panel == null || (panel.MainRoleMessageId != reaction.MessageId && panel.ThreeOfFourRoleMessageId != reaction.MessageId))
            return;

        var alliance = AllianceLogic.GetAlliance(channel.Guild.Id);
        if (alliance == null)
            return;

        var index = Array.IndexOf(AltsLogic.NumberEmoteNames.Select(x => channel.Guild.GetEmote(x).Name).ToArray(), reaction.Emote.Name);
        if (index == -1)
            return;

        var roleName = "RS" + (index + 1).ToStr();
        if (panel.ThreeOfFourRoleMessageId == reaction.MessageId)
            roleName += "¾";

        var roles = user.Roles.ToList();
        var rsRole = channel.Guild.Roles.FirstOrDefault(x => x.Name == roleName);
        if (rsRole != null)
        {
            if (!roles.Any(x => x.Id == rsRole.Id))
            {
                await user.AddRoleAsync(rsRole);

                var rsQueueRole = channel.Guild.GetRole(AfkLogic.GetRsQueueRole(channel.Guild.Id));
                if (rsQueueRole != null && !user.Roles.Any(x => x.Id == rsQueueRole.Id))
                    await user.AddRoleAsync(rsQueueRole);

                roles.Add(rsRole);
            }
            else
            {
                await user.RemoveRoleAsync(rsRole);
                roles.Remove(rsRole);
            }
        }

        await RefreshRsRoleSelectorPanel(channel.Guild, reaction.Channel, user.Id, roles);
    }

    private class RsRolePanel
    {
        public ulong UserId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MainRoleMessageId { get; set; }
        public ulong ThreeOfFourRoleMessageId { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime LastResponseOn { get; set; }
    }
}