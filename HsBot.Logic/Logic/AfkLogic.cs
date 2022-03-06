namespace HsBot.Logic
{
    using System.Globalization;
    using Discord.WebSocket;

    public static class AfkLogic
    {
        public static async Task SetAfk(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, string hourMinuteNotation)
        {
            var now = DateTime.UtcNow;
            var entry = new AfkEntry()
            {
                UserId = user.Id,
                SetOn = now,
                EndsOn = hourMinuteNotation.AddToDateTime(now),
            };

            Services.State.Set(guild.Id, "afk-user-" + user.Id, entry);

            var rsQueueRole = Services.State.Get<ulong>(guild.Id, "rs-queue-role");
            var postMessage = "";
            if (rsQueueRole != 0 && user.Roles.Any(x => x.Id == rsQueueRole))
            {
                await user.RemoveRoleAsync(user.Roles.First(x => x.Id == rsQueueRole));
                postMessage += " RS access revoked.";
            }

            var timeZone = TimeZoneLogic.GetUserTimeZone(guild.Id, user.Id);
            if (timeZone == null)
            {
                await channel.BotResponse(user.DisplayName + " is set to AFK until " + entry.EndsOn.ToString("yyyy.MM.dd HH:mm", CultureInfo.InvariantCulture) + " UTC." + postMessage, ResponseType.infoStay);
            }
            else
            {
                await channel.BotResponse(user.DisplayName + " is set to AFK until " + entry.EndsOn.ToString("yyyy.MM.dd HH:mm", CultureInfo.InvariantCulture) + " UTC, local time will be " + TimeZoneInfo.ConvertTimeFromUtc(entry.EndsOn, timeZone).ToString("yyyy.MM.dd HH:mm", CultureInfo.InvariantCulture) + " (" + timeZone.StandardName + ")." + postMessage, ResponseType.infoStay);
            }
        }

        public static AfkEntry GetAfk(ulong guildId, ulong userId)
        {
            return Services.State.Get<AfkEntry>(guildId, "afk-user-" + userId);
        }

        public static List<AfkEntry> GetAfkList(ulong guildId)
        {
            var result = new List<AfkEntry>();
            var ids = Services.State.ListIds(guildId, "afk-user-");
            var now = DateTime.UtcNow;
            foreach (var id in ids)
            {
                var entry = Services.State.Get<AfkEntry>(guildId, id);
                if (entry.EndsOn <= now)
                {
                    Services.State.Delete(guildId, id);
                }
                else
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        public static async Task RemoveAfk(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user)
        {
            Services.State.Delete(guild.Id, "afk-user-" + user.Id);

            var rsQueueRole = Services.State.Get<ulong>(guild.Id, "rs-queue-role");
            if (rsQueueRole != 0 && !user.Roles.Any(x => x.Id == rsQueueRole))
            {
                var role = guild.Roles.FirstOrDefault(x => x.Id == rsQueueRole);
                if (role == null)
                    await channel.BotResponse("The role set by `!set-rs-queue-role` doesn't exist!", ResponseType.error);

                await user.AddRoleAsync(role);
            }

            await channel.BotResponse(user.DisplayName + " is no longer AFK. RS access enabled.", ResponseType.infoStay);
        }

        public class AfkEntry
        {
            public ulong UserId { get; set; }
            public DateTime SetOn { get; set; }
            public DateTime EndsOn { get; set; }
        }
    }
}