namespace HsBot.Logic
{
    using Discord;
    using Discord.WebSocket;

    public static class RoleLogic
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

            var msg = "Welcome " + user.Mention + "!";
            if (alliance.GreeterRoleId != 0 && guild.GetRole(alliance.GreeterRoleId) != null)
            {
                msg += " A random " + guild.GetRole(alliance.GreeterRoleId).Mention + " will be here shortly to greet you!";
            }

            var eb = new EmbedBuilder()
                .WithTitle("What brought you here on this beautiful day?")
                .WithDescription(
                    ":one: looking for a corp and I heard " + alliance.Name + " (" + alliance.Abbreviation + ") is the best of all!"
                    + "\n:two: looking for an Red Star queue"
                    + "\n:three: looking for a White Star team"
                    + "\n:four: trade");

            if (alliance.GuestRoleId != 0)
            {
                var role = guild.GetRole(alliance.GuestRoleId);
                if (role != null)
                    await user.AddRoleAsync(role);
            }

            var sent = await channel.SendMessageAsync(msg, embed: eb.Build());
            await sent.AddReactionsAsync(AltsLogic.NumberEmoteNames
                .Take(4)
                .Select(x => Emoji.Parse(x))
                .ToArray());
        }

        public static async Task Recruit(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, AllianceLogic.AllianceInfo alliance, AllianceLogic.Corp corp, int? rsLevel)
        {
            var rolesToRemove = user.Roles.Where(x => !x.IsEveryone).ToArray();
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
                {
                    rolesToAdd.Add(role.Id);
                }
            }

            if (rsLevel != null)
            {
                var role = guild.Roles.FirstOrDefault(x => x.Name.StartsWith("RS" + rsLevel.Value.ToStr(), StringComparison.InvariantCultureIgnoreCase));
                if (role != null)
                {
                    rolesToAdd.Add(role.Id);
                }
            }

            var rsQueueRoleId = AfkLogic.GetRsQueueRole(guild.Id);
            if (rsQueueRoleId != 0)
            {
                var role = guild.GetRole(rsQueueRoleId);
                if (role != null)
                {
                    rolesToAdd.Add(role.Id);
                }
            }

            await user.AddRolesAsync(rolesToAdd.Where(x => x != 0));

            await channel.BotResponse(user.Mention + " is successfully recruited to `" + corp.FullName + "`."
                + "\nNew roles: " + string.Join(", ", rolesToAdd.Select(x => "`" + guild.GetRole(x).Name + "`")) + "."
                + "\nRemoved roles: " + string.Join(", ", rolesToRemove.Select(x => "`" + x.Name + "`"))
                , ResponseType.infoStay);
        }

        public static async Task Guestify(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, AllianceLogic.AllianceInfo alliance)
        {
            var rolesToRemove = user.Roles.Where(x => !x.IsEveryone).ToArray();
            if (rolesToRemove.Length > 0)
                await user.RemoveRolesAsync(rolesToRemove);

            var rolesToAdd = new List<ulong>
            {
                alliance.GuestRoleId,
            };

            await user.AddRolesAsync(rolesToAdd.Where(x => x != 0));

            await channel.BotResponse(user.Mention + " is successfully guestified."
                + "\nNew roles: " + string.Join(", ", rolesToAdd.Select(x => "`" + guild.GetRole(x).Name + "`")) + "."
                + "\nRemoved roles: " + string.Join(", ", rolesToRemove.Select(x => "`" + x.Name + "`"))
                , ResponseType.infoStay);
        }
    }
}