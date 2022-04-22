namespace HsBot.Logic;

public static class AfkLogic
{
    public static ulong GetRsQueueRole(ulong guildId)
    {
        return StateService.Get<ulong>(guildId, "rs-queue-role");
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

        StateService.Set(guild.Id, "afk-user-" + user.Id, entry);

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
            await channel.BotResponse(user.DisplayName + " is unavailable for " + entry.EndsOn.Subtract(now).ToIntervalStr() + "." + postMessage, ResponseType.afkStay);
        }
        else
        {
            await channel.BotResponse(user.DisplayName + " is unavailable for " + entry.EndsOn.Subtract(now).ToIntervalStr() + ". Local time will be " + TimeZoneInfo.ConvertTimeFromUtc(entry.EndsOn, timeZone).ToString("yyyy.MM.dd HH:mm", CultureInfo.InvariantCulture) + " (" + timeZone.StandardName + ")." + postMessage, ResponseType.afkStay);
        }
    }

    public static async Task<AfkEntry> GetUserAfk(SocketGuild guild, SocketGuildUser user)
    {
        var entry = StateService.Get<AfkEntry>(guild.Id, "afk-user-" + user.Id);
        if (entry == null)
            return null;

        if (entry.EndsOn <= DateTime.UtcNow)
        {
            await RemoveAfk(guild, user);
            return null;
        }

        return entry;
    }

    public static bool IsUserAfk(SocketGuild guild, SocketGuildUser user)
    {
        var entry = StateService.Get<AfkEntry>(guild.Id, "afk-user-" + user.Id);
        if (entry == null)
            return false;

        return entry.EndsOn > DateTime.UtcNow;
    }

    public static async Task<List<AfkEntry>> GetAfkList(SocketGuild guild)
    {
        var ids = StateService.ListIds(guild.Id, "afk-user-");
        if (ids.Length == 0)
            return null;

        var result = new List<AfkEntry>();
        var now = DateTime.UtcNow;
        foreach (var id in ids)
        {
            var entry = StateService.Get<AfkEntry>(guild.Id, id);
            if (entry.EndsOn <= now)
            {
                var user = guild.GetUser(entry.UserId);
                if (user != null)
                {
                    await RemoveAfk(guild, user);
                }
                else
                {
                    StateService.Delete(guild.Id, id);
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
            try
            {
                foreach (var guild in DiscordBot.Discord.Guilds)
                {
                    await GetAfkList(guild);
                }
            }
            catch (Exception)
            {
            }

            Thread.Sleep(10000);
        }
    }

    public static async Task RemoveAfk(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user)
    {
        await RemoveAfk(guild, user);

        await channel.BotResponse(user.DisplayName + " is available again", ResponseType.afk);
    }

    public static async Task RemoveAfk(SocketGuild guild, SocketGuildUser user)
    {
        var stateId = "afk-user-" + user.Id;
        if (!StateService.Exists(guild.Id, stateId))
            return;

        StateService.Delete(guild.Id, stateId);

        var eb = new EmbedBuilder()
            .WithTitle("AFK status removed")
            .AddField("user", user.DisplayName + " (" + user.Id.ToStr() + ")");

        await LogService.LogToChannel(guild, null, eb.Build());

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
