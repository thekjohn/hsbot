﻿namespace HsBot.Logic
{
    using System.Globalization;
    using Discord;
    using Discord.WebSocket;

    public static class AfkLogic
    {
        public static ulong GetRsQueueRole(ulong guildId)
        {
            return Services.State.Get<ulong>(guildId, "rs-queue-role");
        }

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

            var rsQueueRole = GetRsQueueRole(guild.Id);
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

        public static async Task<AfkEntry> GetUserAfk(SocketGuild guild, SocketGuildUser user)
        {
            var entry = Services.State.Get<AfkEntry>(guild.Id, "afk-user-" + user.Id);
            if (entry == null)
                return null;

            if (entry.EndsOn <= DateTime.UtcNow)
            {
                await RemoveAfk(guild, user);
                return null;
            }

            return entry;
        }

        public static async Task<List<AfkEntry>> GetAfkList(SocketGuild guild)
        {
            var ids = Services.State.ListIds(guild.Id, "afk-user-");
            if (ids.Length == 0)
                return null;

            var result = new List<AfkEntry>();
            var now = DateTime.UtcNow;
            foreach (var id in ids)
            {
                var entry = Services.State.Get<AfkEntry>(guild.Id, id);
                if (entry.EndsOn <= now)
                {
                    var user = guild.GetUser(entry.UserId);
                    if (user != null)
                    {
                        await RemoveAfk(guild, user);
                    }
                    else
                    {
                        Services.State.Delete(guild.Id, id);
                    }
                }
                else
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        internal static async void AutomaticallyRemoveAfkThreadWorker(object obj)
        {
            while (true)
            {
                foreach (var guild in DiscordBot.Discord.Guilds)
                {
                    await GetAfkList(guild);
                }

                Thread.Sleep(10000);
            }
        }

        public static async Task RemoveAfk(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user)
        {
            await RemoveAfk(guild, user);

            await channel.BotResponse(user.DisplayName + " is no longer AFK. RS access enabled.", ResponseType.infoStay);
        }

        public static async Task RemoveAfk(SocketGuild guild, SocketGuildUser user)
        {
            var stateId = "afk-user-" + user.Id;
            if (!Services.State.Exists(guild.Id, stateId))
                return;

            Services.State.Delete(guild.Id, stateId);

            var eb = new EmbedBuilder()
                .WithTitle("AFK status removed")
                .AddField("user", user.DisplayName + " (" + user.Id.ToStr() + ")");

            LogService.LogToChannel(guild, null, eb.Build());

            var rsQueueRoleId = GetRsQueueRole(guild.Id);
            if (rsQueueRoleId != 0 && !user.Roles.Any(x => x.Id == rsQueueRoleId))
            {
                var role = guild.Roles.FirstOrDefault(x => x.Id == rsQueueRoleId);
                if (role != null)
                    await user.AddRoleAsync(role);
            }
        }

        public class AfkEntry
        {
            public ulong UserId { get; set; }
            public DateTime SetOn { get; set; }
            public DateTime EndsOn { get; set; }
        }
    }
}