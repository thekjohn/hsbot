namespace HsBot.Logic;

using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

public static class RemindLogic
{
    private static List<Entry> _entries = null;
    private static readonly object _lock = new();

    internal static async Task Remind(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string who, string when, string message)
    {
        var user = guild.FindUser(currentUser, who);
        if (user == null)
        {
            await channel.BotResponse("Can't find user: " + who + ".", ResponseType.error);
            return;
        }

        var now = DateTime.UtcNow;

        var entry = new Entry
        {
            GuildId = guild.Id,
            RegistratorUserId = currentUser.Id,
            UserId = user.Id,
            ChannelId = channel.Id,
            When = when.AddToDateTime(now),
            Message = message,
        };

        if (entry.When.Subtract(now).TotalSeconds <= 1)
        {
            await channel.BotResponse("Invalid interval: " + when, ResponseType.error);
            return;
        }

        lock (_lock)
        {
            if (_entries == null)
                LoadEntries();

            _entries.Add(entry);
        }

        StateService.Set(guild.Id, entry.GetStateId(), entry);

        await channel.BotResponse("I will DM **" + user.DisplayName + "** in " + entry.When.Subtract(now).ToIntervalStr() + " with the following message: `" + message + "`", ResponseType.successStay);
    }

    public static async void SendRemindersThreadWorker()
    {
        while (true)
        {
            var now = DateTime.UtcNow;

            Entry entry;
            lock (_lock)
            {
                if (_entries == null)
                    LoadEntries();

                entry = _entries.Find(x => x.When <= now);
                if (entry != null)
                {
                    _entries.Remove(entry);
                    StateService.Delete(entry.GuildId, entry.GetStateId());
                }
            }

            if (entry != null)
            {
                var user = DiscordBot.Discord.GetUser(entry.UserId);
                if (user != null)
                {
                    var alliance = AllianceLogic.GetAlliance(entry.GuildId);

                    var eb = new EmbedBuilder()
                        .WithTitle("reminder")
                        .AddField("alliance", alliance.Name)
                        .AddField("message", entry.Message)
                        .WithColor(Color.Gold);

                    var registrator = entry.RegistratorUserId != entry.UserId
                        ? DiscordBot.Discord.GetGuild(entry.GuildId)?.GetUser(entry.RegistratorUserId)
                        : null;

                    if (registrator != null)
                        eb.AddField("sender", registrator.DisplayName);

                    await user.SendMessageAsync(embed: eb.Build());
                }
            }

            Thread.Sleep(1000);
        }
    }

    private static void LoadEntries()
    {
        _entries = new List<Entry>();
        foreach (var guild in DiscordBot.Discord.Guilds)
        {
            var idList = StateService.ListIds(guild.Id, "reminder-");
            foreach (var id in idList)
            {
                var entry = StateService.Get<Entry>(guild.Id, id);
                _entries.Add(entry);
            }
        }
    }

    private class Entry
    {
        public ulong GuildId { get; init; }
        public ulong RegistratorUserId { get; init; }
        public ulong UserId { get; init; }
        public ulong ChannelId { get; init; }
        public DateTime When { get; init; }
        public string Message { get; init; }

        internal string GetStateId()
        {
            return "reminder-" + UserId.ToStr() + "-" + When.Ticks.ToStr();
        }
    }
}
