namespace HsBot.Logic;

public static class RemindLogic
{
    private static List<Entry> _entries = null;
    private static readonly object _lock = new();

    internal static async Task RemindList(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string who)
    {
        var user = guild.FindUser(currentUser, who);
        if (user == null)
        {
            await channel.BotResponse("Can't find user: " + who + ".", ResponseType.error);
            return;
        }

        await PostRemindList(guild, channel, user.DisplayName, user.Id, null);
    }

    internal static async Task RemindListWS(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser)
    {
        if (!WsLogic.GetWsTeamByChannel(guild, channel, out _, out var teamRole))
        {
            await channel.BotResponse("You have to use this command in a WS battleroom!", ResponseType.error);
            return;
        }

        await PostRemindList(guild, channel, teamRole.Name, teamRole.Id, null);
    }

    internal static async Task PostRemindList(SocketGuild guild, ISocketMessageChannel channel, string displayName, ulong? userId, ulong? roleId)
    {
        var now = DateTime.UtcNow;

        var sb = new StringBuilder();
        lock (_lock)
        {
            if (_entries == null)
                LoadEntries();

            var idx = 0;
            foreach (var entry in _entries)
            {
                if (entry.GuildId == guild.Id &&
                    ((userId != null && entry.UserId != null && entry.UserId.Value == userId.Value)
                    || (roleId != null && entry.RoleId != null && entry.RoleId.Value == roleId.Value))
                    )
                {
                    idx++;
                    sb
                        .Append("`#")
                        .Append(idx.ToStr())
                        .Append(" [")
                        .Append(entry.When.Subtract(now).ToIntervalStr(true, true)).Append("] ")
                        .Append(entry.Message)
                        .AppendLine("`");
                }
            }
        }

        var eb = new EmbedBuilder()
            .WithTitle("The following reminders are set for " + displayName)
            .WithDescription(sb.ToString())
            .WithColor(Color.Green)
            .WithFooter("This message will self-destruct in 60 seconds.");

        CleanupService.RegisterForDeletion(60,
            await channel.SendMessageAsync(embed: eb.Build()));
    }

    internal static async Task AddReminder(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string who, string when, string message)
    {
        var user = guild.FindUser(currentUser, who);
        if (user == null)
        {
            await channel.BotResponse("Can't find user: " + who + ".", ResponseType.error);
            return;
        }

        var now = DateTime.UtcNow;

        var whenTime = when.AddToDateTime(now);
        if (whenTime.Subtract(now).TotalSeconds <= 1)
        {
            await channel.BotResponse("Invalid interval: " + when, ResponseType.error);
            return;
        }

        var entry = new Entry
        {
            GuildId = guild.Id,
            RegistratorUserId = currentUser.Id,
            UserId = user.Id,
            ChannelId = channel.Id,
            When = whenTime,
            Message = message,
        };

        lock (_lock)
        {
            if (_entries == null)
                LoadEntries();

            _entries.Add(entry);
        }

        StateService.Set(guild.Id, entry.GetStateId(), entry);

        await channel.BotResponse("I will DM **" + user.DisplayName + "** in " + entry.When.Subtract(now).ToIntervalStr() + " with the following message: `" + message + "`", ResponseType.successStay);
    }

    internal static async Task AddReminderWS(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, string when, string message)
    {
        if (!WsLogic.GetWsTeamByChannel(guild, channel, out var team, out var teamRole) || team.BattleRoomChannelId != channel.Id)
        {
            await channel.BotResponse("You have to use this command in a WS battleroom!", ResponseType.error);
            return;
        }

        var now = DateTime.UtcNow;

        var whenTime = when.AddToDateTime(now);
        if (whenTime.Subtract(now).TotalSeconds <= 1)
        {
            await channel.BotResponse("Invalid interval: " + when, ResponseType.error);
            return;
        }

        var entry = new Entry
        {
            GuildId = guild.Id,
            RegistratorUserId = currentUser.Id,
            RoleId = teamRole.Id,
            ChannelId = channel.Id,
            When = whenTime,
            Message = message,
        };

        lock (_lock)
        {
            if (_entries == null)
                LoadEntries();

            _entries.Add(entry);
        }

        StateService.Set(guild.Id, entry.GetStateId(), entry);

        await channel.BotResponse("I will ping **" + teamRole.Name + "** here in " + entry.When.Subtract(now).ToIntervalStr() + " with the following message: `" + message + "`", ResponseType.successStay);
    }

    public static async void SendRemindersThreadWorker()
    {
        while (true)
        {
            try
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
                    var guild = DiscordBot.Discord.GetGuild(entry.GuildId);
                    if (guild == null)
                        continue;

                    var alliance = AllianceLogic.GetAlliance(guild.Id);
                    if (alliance == null)
                        continue;

                    var channel = guild.GetTextChannel(entry.ChannelId);
                    if (channel == null)
                        continue;

                    var registrator = entry.UserId == null || entry.RegistratorUserId != entry.UserId.Value
                        ? guild.GetUser(entry.RegistratorUserId)
                         : null;

                    if (entry.UserId != null)
                    {
                        var user = DiscordBot.Discord.GetUser(entry.UserId.Value);
                        if (user != null)
                        {
                            var eb = new EmbedBuilder()
                                .WithTitle("REMINDER")
                                .AddField("Alliance", alliance.Name)
                                .AddField("Message", entry.Message)
                                .WithColor(Color.Red)
                                .WithFooter(DiscordBot.FunFooter, guild.CurrentUser.GetAvatarUrl())
                                .WithCurrentTimestamp();

                            if (registrator != null)
                                eb.AddField("Sender", registrator.DisplayName);

                            await user.SendMessageAsync(embed: eb.Build());
                        }
                    }

                    if (entry.RoleId != null)
                    {
                        var role = guild.GetRole(entry.RoleId.Value);
                        if (role != null)
                        {
                            var eb = new EmbedBuilder()
                                .WithTitle("REMINDER")
                                .WithThumbnailUrl(guild.Emotes.FirstOrDefault(x => x.Name == "reminder")?.Url)
                                .AddField((registrator?.DisplayName ?? "<unknown discord user>") + " wrote", entry.Message)
                                .WithColor(Color.Red)
                                .WithFooter(DiscordBot.FunFooter, guild.CurrentUser.GetAvatarUrl())
                                .WithCurrentTimestamp();

                            var msg = ":point_down: " + role.Mention;
                            await channel.SendMessageAsync(msg, embed: eb.Build());
                        }
                    }
                }
            }
            catch (Exception)
            {
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
        public ulong? UserId { get; init; }
        public ulong? RoleId { get; init; }
        public ulong ChannelId { get; init; }
        public DateTime When { get; init; }
        public string Message { get; init; }

        internal string GetStateId()
        {
            return "reminder-" + (UserId ?? RoleId.Value).ToStr() + "-" + When.Ticks.ToStr();
        }
    }
}