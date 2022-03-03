namespace HsBot.Logic
{
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
                await channel.SendMessageAsync("Can't find user: " + who + ".");
                return;
            }

            var entry = new Entry
            {
                GuildId = guild.Id,
                UserId = user.Id,
                ChannelId = channel.Id,
                When = when.AddToNow(),
                Message = message,
            };

            lock (_lock)
            {
                if (_entries == null)
                    LoadEntries();

                _entries.Add(entry);
            }

            Services.State.Set(guild.Id, entry.GetStateId(), entry);
            var sent = await channel.SendMessageAsync(":white_check_mark: I will DM **" + user.DisplayName + "** in " + when + " with the following message: `" + message + "`");
            Services.Cleanup.RegisterForDeletion(10, sent);
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
                        Services.State.Delete(entry.GuildId, entry.GetStateId());
                    }
                }

                if (entry != null)
                {
                    var user = DiscordBot.Discord.GetUser(entry.UserId);
                    if (user != null)
                    {
                        var alliance = AllianceLogic.GetAlliance(entry.GuildId);

                        var embed = new EmbedBuilder()
                            .WithTitle("Reminder - " + alliance.Name)
                            .WithDescription(entry.Message)
                            .Build();

                        await user.SendMessageAsync(embed: embed);
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
                var idList = Services.State.ListIds(guild.Id, "reminder-");
                foreach (var id in idList)
                {
                    var entry = Services.State.Get<Entry>(guild.Id, id);
                    _entries.Add(entry);
                }
            }
        }

        private class Entry
        {
            public ulong GuildId { get; init; }
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
}