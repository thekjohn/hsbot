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
                msg += " A random " + guild.GetRole(alliance.GreeterRoleId).Mention + " will be here shortly to greet you! Please tell us your RS level (and corporation name) while waiting.";
            }

            var eb = new EmbedBuilder()
                .WithTitle("What brought you here on this beautiful day?")
                .WithDescription(
                    ":one: looking for a corp and I heard " + alliance.Name + " (" + alliance.Abbreviation + ") is the best of all!"
                    + "\n:two: looking for a Red Star queue"
                    + "\n:three: looking for a White Star team"
                    + "\n:four: trade"
                    + "\n:five: WS diplomacy"
                    );

            if (alliance.GuestRoleId != 0)
            {
                var role = guild.GetRole(alliance.GuestRoleId);
                if (role != null)
                    await user.AddRoleAsync(role);
            }

            var sent = await channel.SendMessageAsync(msg, embed: eb.Build());
            await sent.AddReactionsAsync(AltsLogic.NumberEmoteNames
                .Take(5)
                .Select(x => Emoji.Parse(x))
                .ToArray());

            await HelpLogic.ShowAllianceInfo(guild, channel, alliance);
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
                    rolesToAdd.Add(role.Id);
            }

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

            await channel.BotResponse(user.DisplayName + " is successfully recruited to `" + corp.FullName + "`."
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

            await channel.BotResponse(user.DisplayName + " is successfully guestified."
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

            await channel.BotResponse(user.DisplayName + " is successfully configured as WS guest."
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

            await channel.BotResponse(user.DisplayName + " is successfully configured as Ally."
                + "\nNew roles: " + string.Join(", ", rolesToAdd.Select(x => "`" + guild.GetRole(x).Name + "`"))
                , ResponseType.infoStay);
        }

        internal static async Task ChangeName(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, string name, string corpName)
        {
            var oldName = user.DisplayName;
            var newName = "[" + (corpName ?? "").Trim() + "] " + name;

            await user.ModifyAsync(x => x.Nickname = newName);

            await channel.BotResponse(oldName + " is successfully renamed to " + newName, ResponseType.infoStay);
        }

        internal static async Task GiveRole(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, SocketRole role)
        {
            if (user.Roles.Any(x => x.Id == role.Id))
            {
                await channel.BotResponse(user.DisplayName + " already has this role: " + role.Name, ResponseType.error);
                return;
            }

            await user.AddRoleAsync(role);
            await channel.BotResponse(user.DisplayName + " successfully got this role: " + role.Name, ResponseType.success);
        }

        internal static async Task TakeRole(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, SocketRole role)
        {
            if (!user.Roles.Any(x => x.Id == role.Id))
            {
                await channel.BotResponse(user.DisplayName + " doesn't have this role: " + role.Name, ResponseType.error);
                return;
            }

            await user.RemoveRoleAsync(role);
            await channel.BotResponse(user.DisplayName + " successfully lost this role: " + role.Name, ResponseType.success);
        }
    }
}