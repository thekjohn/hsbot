namespace HsBot.Logic;

public static class GreeterLogic
{
    public static async Task UserJoined(SocketGuild guild, SocketGuildUser user)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        if (alliance.PublicChannelId == 0)
            return;

        var channel = guild.GetTextChannel(alliance.PublicChannelId);
        if (channel == null)
            return;

        if (alliance.GuestRoleId != 0)
        {
            var role = guild.GetRole(alliance.GuestRoleId);
            if (role != null)
                await user.AddRoleAsync(role);
        }

        await channel.SendMessageAsync("Welcome " + user.Mention + "!");

        var panel = new GreeterPanel()
        {
            UserId = user.Id,
            CreatedOn = DateTime.UtcNow,
        };

        StateService.Set(guild.Id, "greeter-panel-" + user.Id, panel);

        await RepostGreeterPanel(guild, channel, user.Id);
    }

    internal static async Task RepostGreeterPanel(SocketGuild guild, ISocketMessageChannel channel, ulong userId)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var entryId = "greeter-panel-" + userId.ToStr();
        var panel = StateService.Get<GreeterPanel>(guild.Id, entryId);
        if (panel == null)
            return;

        try
        {
            await guild.GetTextChannel(panel.ChannelId).DeleteMessageAsync(panel.MessageId);
        }
        catch (Exception)
        {
        }

        var user = guild.GetUser(userId);
        if (user == null)
            return;

        switch (panel.FirstResponse)
        {
            case null:
                {
                    var eb = new EmbedBuilder()
                        .WithTitle(user.DisplayName + ", what brought you here on this beautiful day?")
                        .WithColor(Color.Red)
                        .WithFooter(DiscordBot.FunFooter, guild.CurrentUser.GetAvatarUrl())
                        .WithCurrentTimestamp()
                        .WithDescription(
                              ":one: looking for a corp and I heard " + alliance.Name + " (" + alliance.Abbreviation + ") is the best of all!"
                              + "\n:two: looking for a Red Star queue"
                              + "\n:three: looking for a White Star team"
                              + "\n:four: trade"
                              + "\n:five: WS diplomacy"
                              );

                    var sent = await channel.SendMessageAsync(embed: eb.Build());
                    panel.ChannelId = channel.Id;
                    panel.MessageId = sent.Id;
                    StateService.Set(guild.Id, "greeter-panel-" + user.Id, panel);

                    await sent.AddReactionsAsync(AltsLogic.NumberEmoteNames
                        .Take(5)
                        .Select(x => guild.GetEmote(x))
                        .ToArray());
                }
                break;
            case GreeterFirstResponse.recruit when panel.CorpToRecruit == null:
                {
                    var corps = alliance.Corporations.OrderBy(x => x.FullName).ToArray();

                    var idx = 0;
                    var list = "";
                    foreach (var corp in corps)
                    {
                        list += AltsLogic.NumberEmoteNames[idx] + " " + corp.IconMention + " " + corp.FullName + " (" + corp.Abbreviation + ")\n";
                        idx++;
                    }

                    var eb = new EmbedBuilder()
                        .WithTitle(user.DisplayName + " which " + alliance.Name + " corp you would like to join?")
                        .WithColor(Color.Red)
                        .WithFooter(DiscordBot.FunFooter, guild.CurrentUser.GetAvatarUrl())
                        .WithDescription(list);

                    var sent = await channel.SendMessageAsync(embed: eb.Build());
                    panel.ChannelId = channel.Id;
                    panel.MessageId = sent.Id;
                    StateService.Set(guild.Id, "greeter-panel-" + user.Id, panel);

                    await sent.AddReactionsAsync(AltsLogic.NumberEmoteNames
                        .Take(corps.Length)
                        .Select(x => guild.GetEmote(x))
                        .ToArray());
                }
                break;
            case GreeterFirstResponse.recruit when panel.CorpToRecruit != null:
            case GreeterFirstResponse.rsqueue:
            case GreeterFirstResponse.wsteam:
            case GreeterFirstResponse.trade:
                {
                    var validRsLevels = Enumerable.Range(4, 12)
                        .Where(level => guild.Roles.Any(x => x.Name == "RS" + level.ToStr()))
                        .ToList();

                    var eb = new EmbedBuilder()
                        .WithTitle(user.DisplayName + " which Red Star level you are playing currently?")
                        .WithColor(Color.Red)
                        .WithFooter(DiscordBot.FunFooter, guild.CurrentUser.GetAvatarUrl());

                    var sent = await channel.SendMessageAsync(embed: eb.Build());
                    panel.ChannelId = channel.Id;
                    panel.MessageId = sent.Id;
                    StateService.Set(guild.Id, "greeter-panel-" + user.Id, panel);

                    await sent.AddReactionsAsync(validRsLevels
                        .Select(level => guild.GetEmote(AltsLogic.NumberEmoteNames[level - 1]))
                        .ToArray());
                }
                break;
        }
    }

    internal static async Task HandleReactions(SocketReaction reaction, bool added)
    {
        if (reaction.User.Value.IsBot)
            return;

        var channel = reaction.Channel as SocketGuildChannel;
        var user = reaction.User.Value as SocketGuildUser;

        var entryId = "greeter-panel-" + reaction.UserId.ToStr();
        var panel = StateService.Get<GreeterPanel>(channel.Guild.Id, entryId);
        if (panel == null || panel.MessageId != reaction.MessageId)
            return;

        var alliance = AllianceLogic.GetAlliance(channel.Guild.Id);
        if (alliance == null)
            return;

        switch (panel.FirstResponse)
        {
            case null:
                {
                    var index = Array.IndexOf(AltsLogic.NumberEmoteNames.Select(x => channel.Guild.GetEmote(x).Name).ToArray(), reaction.Emote.Name);
                    if (index == -1)
                        return;

                    switch (index)
                    {
                        case 0:
                            panel.FirstResponse = GreeterFirstResponse.recruit;
                            panel.LastResponseOn = DateTime.UtcNow;
                            StateService.Set(channel.Guild.Id, "greeter-panel-" + user.Id, panel);
                            await RepostGreeterPanel(channel.Guild, reaction.Channel, user.Id);
                            break;
                        case 1:
                            panel.FirstResponse = GreeterFirstResponse.rsqueue;
                            panel.LastResponseOn = DateTime.UtcNow;
                            StateService.Set(channel.Guild.Id, "greeter-panel-" + user.Id, panel);
                            await RepostGreeterPanel(channel.Guild, reaction.Channel, user.Id);
                            break;
                        case 2:
                            {
                                var allyRole = channel.Guild.GetRole(alliance.AllyRoleId);
                                var wsGuestRole = channel.Guild.GetRole(alliance.WsGuestRoleId);
                                if (allyRole != null && wsGuestRole != null)
                                {
                                    if (!user.Roles.Any(x => x.Id == allyRole.Id))
                                        await user.AddRoleAsync(allyRole);

                                    if (!user.Roles.Any(x => x.Id == wsGuestRole.Id))
                                        await user.AddRoleAsync(wsGuestRole);

                                    await reaction.Channel.BotResponse(user.Mention + " you are promoted to " + allyRole.Name + " and " + wsGuestRole.Name + ".", ResponseType.successStay);
                                }

                                panel.FirstResponse = GreeterFirstResponse.wsteam;
                                panel.LastResponseOn = DateTime.UtcNow;
                                StateService.Set(channel.Guild.Id, "greeter-panel-" + user.Id, panel);
                                await RepostGreeterPanel(channel.Guild, reaction.Channel, user.Id);
                            }
                            break;
                        case 3:
                            {
                                var allyRole = channel.Guild.GetRole(alliance.AllyRoleId);
                                if (allyRole != null)
                                {
                                    if (!user.Roles.Any(x => x.Id == allyRole.Id))
                                        await user.AddRoleAsync(allyRole);

                                    await reaction.Channel.BotResponse(user.DisplayName + " is promoted to " + allyRole.Name
                                        + "\nPlease use `!setmyname <yourInGameName>` and `!setmycorp <yourInGameCorp>` commands to give yourself a proper name.", ResponseType.successStay);
                                }

                                panel.FirstResponse = GreeterFirstResponse.trade;
                                panel.LastResponseOn = DateTime.UtcNow;
                                StateService.Set(channel.Guild.Id, "greeter-panel-" + user.Id, panel);
                                await RepostGreeterPanel(channel.Guild, reaction.Channel, user.Id);
                            }
                            break;
                        case 4:
                            panel.FirstResponse = GreeterFirstResponse.wsdiplomacy;

                            var admiralRole = channel.Guild.GetRole(alliance.AdmiralRoleId);
                            if (admiralRole != null)
                            {
                                var ch = await channel.Guild.CreateTextChannelAsync("ws-diplo-" + DateTime.UtcNow.ToString("MM-dd-HH-mm", CultureInfo.InvariantCulture), f =>
                                {
                                    f.Position = 0;
                                });

                                await ch.AddPermissionOverwriteAsync(channel.Guild.EveryoneRole, new OverwritePermissions(viewChannel: PermValue.Deny));
                                await ch.AddPermissionOverwriteAsync(admiralRole, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, manageChannel: PermValue.Allow));
                                await ch.AddPermissionOverwriteAsync(user, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow));

                                await ch.SendMessageAsync(admiralRole.Mention + " we have " + user.Mention + " with a WS diplomacy request!");
                            }

                            StateService.Delete(channel.Guild.Id, "greeter-panel-" + user.Id);
                            try
                            {
                                await reaction.Channel.DeleteMessageAsync(reaction.MessageId);
                            }
                            catch (Exception)
                            {
                            }
                            break;
                    }
                }
                break;
            case GreeterFirstResponse.recruit when panel.CorpToRecruit == null:
                {
                    var corps = alliance.Corporations.OrderBy(x => x.FullName).ToArray();
                    var index = Array.IndexOf(AltsLogic.NumberEmoteNames.Select(x => channel.Guild.GetEmote(x).Name).ToArray(), reaction.Emote.Name);
                    if (index == -1)
                        return;

                    var corp = corps[index];
                    panel.CorpToRecruit = corp.RoleId;

                    var greeterRole = channel.Guild.GetRole(alliance.GreeterRoleId);
                    if (greeterRole != null)
                    {
                        await reaction.Channel.BotResponse(user.Mention + " a random " + greeterRole.Mention + " will be here shorty to recruit you to " + corp.FullName + ".", ResponseType.infoStay);
                    }

                    panel.LastResponseOn = DateTime.UtcNow;
                    StateService.Set(channel.Guild.Id, "greeter-panel-" + user.Id, panel);
                    await RepostGreeterPanel(channel.Guild, reaction.Channel, user.Id);
                }
                break;
            case GreeterFirstResponse.recruit when panel.CorpToRecruit != null:
                {
                    var index = Array.IndexOf(AltsLogic.NumberEmoteNames.Select(x => channel.Guild.GetEmote(x).Name).ToArray(), reaction.Emote.Name);
                    if (index == -1)
                        return;

                    var rsRole = channel.Guild.Roles.FirstOrDefault(x => x.Name == "RS" + (index + 1).ToStr());
                    if (rsRole != null)
                    {
                        if (!user.Roles.Any(x => x.Id == rsRole.Id))
                            await user.AddRoleAsync(rsRole);

                        var rsQueueRole = channel.Guild.GetRole(AfkLogic.GetRsQueueRole(channel.Guild.Id));
                        if (rsQueueRole != null && !user.Roles.Any(x => x.Id == rsQueueRole.Id))
                            await user.AddRoleAsync(rsQueueRole);

                        await reaction.Channel.BotResponse(user.Mention + " you got " + rsRole.Mention + " access!"
                            + "\nPlease use `!setmyname <yourInGameName>` command to give yourself a proper name.", ResponseType.successStay);
                    }

                    StateService.Delete(channel.Guild.Id, "greeter-panel-" + user.Id);
                    try
                    {
                        await reaction.Channel.DeleteMessageAsync(reaction.MessageId);
                    }
                    catch (Exception)
                    {
                    }
                }
                break;
            case GreeterFirstResponse.rsqueue:
            case GreeterFirstResponse.wsteam:
            case GreeterFirstResponse.trade:
                {
                    var index = Array.IndexOf(AltsLogic.NumberEmoteNames.Select(x => channel.Guild.GetEmote(x).Name).ToArray(), reaction.Emote.Name);
                    if (index == -1)
                        return;

                    var rsRole = channel.Guild.Roles.FirstOrDefault(x => x.Name == "RS" + (index + 1).ToStr());
                    if (rsRole != null)
                    {
                        if (!user.Roles.Any(x => x.Id == rsRole.Id))
                            await user.AddRoleAsync(rsRole);

                        var rsQueueRole = channel.Guild.GetRole(AfkLogic.GetRsQueueRole(channel.Guild.Id));
                        if (rsQueueRole != null && !user.Roles.Any(x => x.Id == rsQueueRole.Id))
                            await user.AddRoleAsync(rsQueueRole);

                        await reaction.Channel.BotResponse(user.Mention + " you got " + rsRole.Mention + " access!"
                            + "\nPlease use `!setmyname <yourInGameName>` and `!setmycorp <yourInGameCorp>` commands to give yourself a proper name.", ResponseType.successStay);
                    }

                    StateService.Delete(channel.Guild.Id, "greeter-panel-" + user.Id);
                    try
                    {
                        await reaction.Channel.DeleteMessageAsync(reaction.MessageId);
                    }
                    catch (Exception)
                    {
                    }
                }
                break;
        }
    }

    private enum GreeterFirstResponse { recruit, rsqueue, wsteam, trade, wsdiplomacy }

    private class GreeterPanel
    {
        public ulong UserId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime LastResponseOn { get; set; }
        public ulong? CorpToRecruit { get; set; }
        public GreeterFirstResponse? FirstResponse { get; set; }
    }
}