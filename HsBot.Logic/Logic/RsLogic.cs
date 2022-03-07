namespace HsBot.Logic
{
    using System.Threading.Tasks;
    using Discord.WebSocket;

    public static class RsLogic
    {
        internal static async Task SetRsRunCounter(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, int rsLevel, int count)
        {
            var stateId = Services.State.GetId("rs-run-count", user.Id, (ulong)rsLevel);
            var cnt = Services.State.Get<int>(guild.Id, stateId);
            Services.State.Set(guild.Id, stateId, count);
            await channel.BotResponse(user.DisplayName + "'s run counter for RS" + rsLevel.ToStr() + " is changed to " + count.ToStr() + ". Original value was " + cnt.ToStr(), ResponseType.success);
        }
    }
}