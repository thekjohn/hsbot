namespace HsBot.Logic;

using System.Threading.Tasks;
using Discord.WebSocket;

public static class TimeZoneLogic
{
    public static async Task SetTimeZone(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, TimeZoneInfo timeZone)
    {
        StateService.Set(user.Guild.Id, "timezone-user-" + user.Id, timeZone.Id);

        await channel.BotResponse(user.Mention + "'s timezone is set to " + timeZone.StandardName + ", UTC" + (timeZone.BaseUtcOffset.TotalMilliseconds >= 0 ? "+" : "")
            + (timeZone.BaseUtcOffset.Minutes == 0
                ? timeZone.BaseUtcOffset.Hours.ToStr()
                : timeZone.BaseUtcOffset.ToString(@"h\:mm"))
                + ", current time is " + TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).ToString("HH:mm")
                , ResponseType.success);
    }

    public static TimeZoneInfo GetUserTimeZone(ulong guildId, ulong userId)
    {
        var tzId = StateService.Get<string>(guildId, "timezone-user-" + userId);
        if (tzId == null)
            return null;

        return TimeZoneInfo.FindSystemTimeZoneById(tzId);
    }
}
