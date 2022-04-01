namespace HsBot.Logic;

public static class RoleLogic
{
    public static async Task Recruit(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, AllianceLogic.AllianceInfo alliance, AllianceLogic.Corp corp)
    {
        var rolesToRemove = user.Roles.Where(x => x.Id == alliance.GuestRoleId || x.Id == alliance.AllyRoleId).ToArray();
        if (rolesToRemove.Length > 0)
            await user.RemoveRolesAsync(rolesToRemove);

        var rolesToAdd = new List<ulong>
        {
            corp.RoleId,
            alliance.RoleId,
        };

        if (alliance.CompendiumRoleId != 0)
        {
            var role = guild.GetRole(alliance.CompendiumRoleId);
            if (role != null)
                rolesToAdd.Add(role.Id);
        }

        await user.AddRolesAsync(rolesToAdd.Where(x => x != 0));

        var oldName = user.DisplayName;
        var newName = user.DisplayName;
        if (user.DisplayName.StartsWith("[]", StringComparison.InvariantCultureIgnoreCase))
        {
            newName = user.DisplayName.Substring(2).Trim();
            await user.ModifyAsync(x => x.Nickname = newName);
            await channel.BotResponse(oldName + " is renamed to " + newName, ResponseType.infoStay);
        }
        else
        {
            var idx = user.DisplayName.IndexOf('[', StringComparison.InvariantCultureIgnoreCase);
            if (idx == 0)
            {
                var idx2 = user.DisplayName.IndexOf(']', StringComparison.InvariantCultureIgnoreCase);
                if (idx2 > idx)
                {
                    newName = user.DisplayName.Substring(idx2 + 1).Trim();
                    await user.ModifyAsync(x => x.Nickname = newName);
                    await channel.BotResponse(oldName + " is renamed to " + newName, ResponseType.infoStay);
                }
            }
        }

        await channel.BotResponse(newName + " is recruited to `" + corp.FullName + "`."
            + "\nNew roles: " + string.Join(", ", rolesToAdd.Select(x => "`" + guild.GetRole(x).Name + "`")) + "."
            + "\nRemoved roles: " + string.Join(", ", rolesToRemove.Select(x => "`" + x.Name + "`"))
            , ResponseType.infoStay);
    }

    public static async Task DemoteToGuest(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, AllianceLogic.AllianceInfo alliance)
    {
        var rolesToRemove = user.Roles.Where(x => !x.IsEveryone).ToArray();
        if (rolesToRemove.Length > 0)
            await user.RemoveRolesAsync(rolesToRemove);

        var rolesToAdd = new List<ulong>
        {
            alliance.GuestRoleId,
        };

        await user.AddRolesAsync(rolesToAdd.Where(x => x != 0));

        await channel.BotResponse(user.DisplayName + " is guestified."
            + "\nNew roles: " + string.Join(", ", rolesToAdd.Select(x => "`" + guild.GetRole(x).Name + "`"))
            + "\nRemoved roles: " + string.Join(", ", rolesToRemove.Select(x => "`" + x.Name + "`"))
            , ResponseType.infoStay);
    }

    public static async Task PromoteToWsGuest(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, AllianceLogic.AllianceInfo alliance)
    {
        var rolesToAdd = new List<ulong>
        {
            alliance.WsGuestRoleId,
        };

        if (alliance.CompendiumRoleId != 0)
        {
            var role = guild.GetRole(alliance.CompendiumRoleId);
            if (role != null)
                rolesToAdd.Add(role.Id);
        }

        await user.AddRolesAsync(rolesToAdd.Where(x => x != 0));

        await channel.BotResponse(user.DisplayName + " is configured as WS guest. `" + DiscordBot.CommandPrefix + "setname " + user.DisplayName + " <newName> <corpName>` can be used to set the ingame-name and corp."
            + "\nNew roles: " + string.Join(", ", rolesToAdd.Select(x => "`" + guild.GetRole(x).Name + "`"))
            , ResponseType.infoStay);
    }

    public static async Task PromoteToAlly(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, AllianceLogic.AllianceInfo alliance, int? rsLevel)
    {
        var rolesToAdd = new List<ulong>
        {
            alliance.AllyRoleId,
        };

        if (rsLevel != null)
        {
            var role = guild.Roles.FirstOrDefault(x => x.Name.StartsWith("RS" + rsLevel.Value.ToStr(), StringComparison.InvariantCultureIgnoreCase));
            if (role != null)
                rolesToAdd.Add(role.Id);

            var rsQueueRoleId = AfkLogic.GetRsQueueRole(guild.Id);
            if (rsQueueRoleId != 0)
            {
                role = guild.GetRole(rsQueueRoleId);
                if (role != null)
                    rolesToAdd.Add(role.Id);
            }
        }
        await user.AddRolesAsync(rolesToAdd.Where(x => x != 0));

        await channel.BotResponse(user.DisplayName + " is configured as Ally. `" + DiscordBot.CommandPrefix + "setname " + user.DisplayName + " <newName> <corpName>` can be used to set the ingame-name and corp."
            + "\nNew roles: " + string.Join(", ", rolesToAdd.Select(x => "`" + guild.GetRole(x).Name + "`"))
            , ResponseType.info);
    }

    internal static async Task ChangeName(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, string name, string corpName)
    {
        var oldName = user.DisplayName;
        var newName = "[" + (corpName ?? "").Trim() + "] " + name;

        await user.ModifyAsync(x => x.Nickname = newName);

        await channel.BotResponse(oldName + " is renamed to " + newName, ResponseType.infoStay);
    }

    internal static async Task SetMyName(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, string ingameName)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        if (ingameName.Contains('[', StringComparison.InvariantCultureIgnoreCase) || ingameName.Contains(']', StringComparison.InvariantCultureIgnoreCase))
        {
            await channel.BotResponse("Name can't contain [ or ] characters!", ResponseType.error);
            return;
        }

        var oldName = user.DisplayName;

        string oldCorpName = null;
        var idx = user.DisplayName.IndexOf('[', StringComparison.InvariantCultureIgnoreCase);
        if (idx == 0)
        {
            var idx2 = user.DisplayName.IndexOf(']', StringComparison.InvariantCultureIgnoreCase);
            if (idx2 > idx)
            {
                oldCorpName = user.DisplayName.Substring(idx + 1, idx2 - idx - 2).Trim();
            }
        }

        var newName = user.Roles.Any(x => x.Id == alliance.RoleId)
            ? ingameName
            : "[" + (oldCorpName ?? "").Trim() + "] " + ingameName;

        await user.ModifyAsync(x => x.Nickname = newName);

        await channel.BotResponse(oldName + " is renamed to " + newName, ResponseType.infoStay);
    }

    internal static async Task SetMyCorp(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, string corpName)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        if (corpName.Contains('[', StringComparison.InvariantCultureIgnoreCase) || corpName.Contains(']', StringComparison.InvariantCultureIgnoreCase))
        {
            await channel.BotResponse("Name can't contain [ or ] characters!", ResponseType.error);
            return;
        }

        if (user.Roles.Any(x => x.Id == alliance.RoleId))
        {
            await channel.BotResponse("Alliance members can't set their corp name!", ResponseType.error);
            return;
        }

        var oldName = user.DisplayName;

        var oldIngameName = oldName;
        var idx = user.DisplayName.IndexOf('[', StringComparison.InvariantCultureIgnoreCase);
        if (idx == 0)
        {
            var idx2 = user.DisplayName.IndexOf(']', StringComparison.InvariantCultureIgnoreCase);
            if (idx2 > idx)
            {
                oldIngameName = user.DisplayName.Substring(idx2 + 1).Trim();
            }
        }

        var newName = "[" + (corpName ?? "").Trim() + "] " + oldIngameName;

        await user.ModifyAsync(x => x.Nickname = newName);

        await channel.BotResponse(oldName + " is renamed to " + newName, ResponseType.infoStay);
    }

    internal static async Task GiveRole(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, SocketRole role)
    {
        if (user.Roles.Any(x => x.Id == role.Id))
        {
            await channel.BotResponse(user.DisplayName + " already has this role: " + role.Name, ResponseType.error);
            return;
        }

        await user.AddRoleAsync(role);
        await channel.BotResponse(user.DisplayName + " got the following role: " + role.Name, ResponseType.success);
    }

    internal static async Task TakeRole(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, SocketRole role)
    {
        if (!user.Roles.Any(x => x.Id == role.Id))
        {
            await channel.BotResponse(user.DisplayName + " doesn't have this role: " + role.Name, ResponseType.error);
            return;
        }

        await user.RemoveRoleAsync(role);
        await channel.BotResponse(user.DisplayName + " lost the following role: " + role.Name, ResponseType.success);
    }
}
