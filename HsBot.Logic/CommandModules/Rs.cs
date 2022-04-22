namespace HsBot.Logic;

[Summary("Red Stars")]
[RequireContext(ContextType.Guild)]
public class Rs : BaseModule
{
    [Command("rsrole")]
    [Summary("rsrole|show RS role selector")]
    [RequireMinimumAllianceRole(AllianceRole.Ally)]
    public async Task SelectRsRole()
    {
        await CleanupService.DeleteCommand(Context.Message);
        await RsRoleSelectorLogic.ShowPanel(Context.Guild, Context.Channel, CurrentUser);
    }

    [Command("in")]
    [Alias("i")]
    [Summary("in [level]|enqueue to your highest, or a specific level queue")]
    [RequireMinimumAllianceRole(AllianceRole.Ally)]
    public async Task In(int? level = null)
    {
        await AddQueue(level, CurrentUser);
    }

    [Command("out")]
    [Alias("o")]
    [Summary("out [level]|dequeue from a specific, or all queues")]
    [RequireMinimumAllianceRole(AllianceRole.Ally)]
    public async Task Out(int? level = null)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await RemoveFromQueue(Context.Guild, Context.Channel, level, CurrentUser, null);
        await RefreshQueueList(Context.Guild, Context.Channel, false);
    }

    [Command("ping")]
    [Summary("ping <level>|ping an RS role")]
    [RequireMinimumAllianceRole(AllianceRole.Member)]
    public async Task Ping(int level)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await Ping(Context.Guild, Context.Channel, level);
    }

    [Command("start")]
    [Summary("start <level>|force start on a queue")]
    [RequireMinimumAllianceRole(AllianceRole.Member)]
    public async Task Start(int level)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await StartQueue(level);
    }

    [Command("rsmod")]
    [Summary("rsmod|allow setting RS queue related mods")]
    [RequireMinimumAllianceRole(AllianceRole.Ally)]
    public async Task RsMod()
    {
        await CleanupService.DeleteCommand(Context.Message);
        await ShowRsMod(Context.Guild, Context.Channel);
    }

    [Command("q")]
    [Summary("q|query active queues")]
    [RequireMinimumAllianceRole(AllianceRole.Ally)]
    public async Task QueryQueues(int? level = null)
    {
        await CleanupService.DeleteCommand(Context.Message);

        await RefreshQueueList(Context.Guild, Context.Channel, false);
    }

    [Command("in")]
    [Summary("in level user|debug only")]
    public async Task I(int level, SocketGuildUser targetUser)
    {
        await CleanupService.DeleteCommand(Context.Message);

        if (!CurrentUser.Roles.Any(x => x.Permissions.Administrator))
        {
            await ReplyAsync("only administrators can use this command");
            return;
        }

        await AddQueue(level, targetUser);
    }

    [Command("out")]
    [Summary("out level user|debug only")]
    public async Task O(int level, SocketGuildUser targetUser)
    {
        await CleanupService.DeleteCommand(Context.Message);

        await RemoveFromQueue(Context.Guild, Context.Channel, level, targetUser, null);
        await RefreshQueueList(Context.Guild, Context.Channel, false);
    }

    [Command("setrsruncount")]
    [Summary("setrsruncount <rsLevel> <count> <userName>|set the RS run counter for a specific user")]
    [RequireMinimumAllianceRole(AllianceRole.Greeter)]
    public async Task SetRsRunCounter(int rsLevel, int count, [Remainder] string userName)
    {
        await CleanupService.DeleteCommand(Context.Message);

        var user = Context.Guild.FindUser(CurrentUser, userName);
        if (user == null)
        {
            await Context.Channel.BotResponse("Can't find user: " + userName, ResponseType.error);
            return;
        }

        await RsLogic.SetRsRunCounter(Context.Guild, Context.Channel, user, rsLevel, count);
    }

    private static async Task Ping(SocketGuild guild, ISocketMessageChannel channel, int level)
    {
        var role = guild.Roles.FirstOrDefault(x => x.Name == "RS" + level.ToStr());
        if (role == null)
        {
            await channel.BotResponse("There is no role for RS" + level.ToStr() + ".", ResponseType.error);
            return;
        }

        var panel = GetQueue(guild.Id);
        var queue = panel.Queues.Find(x => x.Level == level);
        if (queue == null)
        {
            await channel.BotResponse("You can't ping an empty queue.", ResponseType.error);
            return;
        }

        var roleMentionStateId = StateService.GetId("rs-queue-last-role-mention", role.Id);
        var lastRsMention = StateService.Get<DateTime?>(guild.Id, roleMentionStateId);

        var text = role.Name;
        if (lastRsMention == null || DateTime.UtcNow > lastRsMention.Value.AddMinutes(5))
        {
            lastRsMention = DateTime.UtcNow;
            StateService.Set(guild.Id, roleMentionStateId, lastRsMention);
            text = role.Mention;
        }

        var alliance = AllianceLogic.GetAlliance(guild.Id);

        await channel.SendMessageAsync(
            ":question: " + text + " anyone?\n  (" + queue.Users.Count.ToStr() + "/4) :point_right: "
            + string.Join(" ", queue.Users
                .Select(x => guild.GetUser(x))
                .Where(x => x != null)
                .Select(x => alliance.GetUserCorpIcon(x) + x.DisplayName)
                ));
    }

    private async Task AddQueue(int? level, SocketGuildUser user)
    {
        await CleanupService.DeleteCommand(Context.Message);

        if (user == null)
            return;

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);
        if (alliance == null)
            return;

        int selectedLevel;
        if (level == null)
        {
            var highestRsRoleNumber = user.GetHighestRsRoleNumber();
            if (highestRsRoleNumber == null)
            {
                await Context.Channel.BotResponse("RS role of " + user.Mention + " cannot be determined: " + user.DisplayName, ResponseType.error);
                return;
            }

            selectedLevel = highestRsRoleNumber.Value;
        }
        else
        {
            selectedLevel = level.Value;
        }

        var role = Context.Guild.Roles.FirstOrDefault(x => x.Name == "RS" + selectedLevel.ToStr());
        if (role == null)
        {
            await Context.Channel.BotResponse("There is no role for RS" + selectedLevel.ToStr() + ".", ResponseType.error);
            return;
        }

        var panel = GetQueue(Context.Guild.Id);

        var queue = panel.Queues.Find(x => x.Level == selectedLevel);
        if (queue == null)
        {
            queue = new RsQueueEntry()
            {
                Level = selectedLevel,
                StartedOn = DateTime.UtcNow,
            };

            panel.Queues.Add(queue);
        }

        StateService.Set(Context.Guild.Id, "rs-queue-activity-" + user.Id.ToStr(), DateTime.UtcNow);
        StateService.Set(Context.Guild.Id, "rs-queue-activity-" + user.Id.ToStr() + "-" + selectedLevel.ToStr(), DateTime.UtcNow);
        StateService.Delete(Context.Guild.Id, "rs-queue-activity-" + user.Id.ToStr() + "-" + selectedLevel.ToStr() + "-asked");

        if (queue.Users.Contains(user.Id))
        {
            await RefreshQueueList(Context.Guild, Context.Channel, false);
            return;
        }

        queue.Users.Add(user.Id);

        queue.Users.RemoveAll(x => Context.Guild.GetUser(x) == null);

        var runId = 0;
        if (queue.Users.Count == 4)
        {
            panel.Queues.Remove(queue);

            var runCountStateId = "rs-run-count";
            runId = StateService.Get<int>(Context.Guild.Id, runCountStateId) + 1;
            StateService.Set(Context.Guild.Id, runCountStateId, runId);
        }

        StateService.Set(Context.Guild.Id, "rs-queue", panel);

        if (queue.Users.Count == 4)
        {
            foreach (var userId in queue.Users)
            {
                var runCountStateId = StateService.GetId("rs-run-count", userId, (ulong)queue.Level);
                var cnt = StateService.Get<int>(Context.Guild.Id, runCountStateId);
                cnt++;
                StateService.Set(Context.Guild.Id, runCountStateId, cnt);
            }

            foreach (var userId in queue.Users)
            {
                await RemoveFromQueue(Context.Guild, Context.Channel, null, Context.Guild.GetUser(userId), selectedLevel);
            }
        }

        string response;
        if (queue.Users.Count < 4)
        {
            var roleMentionStateId = StateService.GetId("rs-queue-last-role-mention", role.Id);
            var lastRsMention = StateService.Get<DateTime?>(Context.Guild.Id, roleMentionStateId);
            if (lastRsMention == null || DateTime.UtcNow > lastRsMention.Value.AddMinutes(5))
            {
                lastRsMention = DateTime.UtcNow;
                StateService.Set(Context.Guild.Id, roleMentionStateId, lastRsMention);
                response = ":white_check_mark: " + role.Mention;
            }
            else
            {
                response = ":white_check_mark: " + role.Name;
            }
        }
        else
        {
            response = ":white_check_mark: " + role.Name;
        }

        if (queue.Users.Count == 3)
        {
            var threeOfFourRole = Context.Guild.Roles.FirstOrDefault(x => x.Name == "RS" + selectedLevel.ToStr() + "¾");
            if (threeOfFourRole != null)
                response += " " + threeOfFourRole.Mention;

            var rsQueueRoleId = AfkLogic.GetRsQueueRole(Context.Guild.Id);
            var users = Context.Guild.Users.Where(x =>
                x.Id != user.Id
                && x.Roles.Any(r => r.Id == threeOfFourRole.Id)
                && x.Roles.Any(r => r.Id == rsQueueRoleId
                && !AfkLogic.IsUserAfk(Context.Guild, x)
                && !queue.Users.Contains(x.Id)));

            foreach (var usr in users)
            {
                await usr.SendMessageAsync("RS" + selectedLevel.ToStr() + " queue is 3/4 in https://discord.com/channels/" + Context.Guild.Id.ToStr() + "/" + Context.Channel.Id.ToStr()
                    + "\n" + string.Join(" ", queue.Users.Select(x =>
                    {
                        var user = Context.Guild.GetUser(x);
                        return user != null
                            ? alliance.GetUserCorpIcon(user) + user.DisplayName
                            : "<unknown discord user>";
                    })));
            }
        }

        response += " (" + queue.Users.Count.ToStr() + "/4), " + user.Mention + " joined.";
        await ReplyAsync(response);

        await RefreshQueueList(Context.Guild, Context.Channel, true);

        if (queue.Users.Count == 4)
        {
            await PostStartedQueue(Context.Guild, Context.Channel, queue, runId);
            StateService.Set(Context.Guild.Id, "rs-log-" + runId.ToStr(), queue);

            response = "RS" + selectedLevel.ToStr() + " ready! Meet where? (4/4)"
                + "\n" + string.Join(" ", queue.Users.Select(x =>
                    {
                        var user = Context.Guild.GetUser(x);
                        return user != null
                            ? alliance.GetUserCorpIcon(user) + user.Mention
                            : "<unknown discord user>";
                    }));

            CleanupService.RegisterForDeletion(10 * 60,
                await ReplyAsync(response));

            foreach (var queueUserId in queue.Users)
            {
                var usr = Context.Guild.GetUser(queueUserId);
                if (usr != null)
                {
                    await usr.SendMessageAsync("RS" + selectedLevel.ToStr() + " is ready! (4/4) in https://discord.com/channels/" + Context.Guild.Id.ToStr() + "/" + Context.Channel.Id.ToStr()
                        + "\n" + string.Join(" ", queue.Users.Select(x =>
                        {
                            var user = Context.Guild.GetUser(x);
                            return user != null
                                ? alliance.GetUserCorpIcon(user) + user.DisplayName
                                : "<unknown discord user>";
                        })));
                }
            }
        }
    }

    private static RsQueue GetQueue(ulong guildId)
    {
        return StateService.Get<RsQueue>(guildId, "rs-queue") ?? new RsQueue();
    }

    private async Task StartQueue(int level)
    {
        var panel = GetQueue(Context.Guild.Id);

        var queue = panel.Queues.Find(x => x.Level == level);
        if (queue == null)
        {
            await Context.Channel.BotResponse("RS" + level.ToStr() + " queue is empty, there is nothing to start...", ResponseType.error);
            return;
        }

        var runCountStateId = "rs-run-count";
        var runId = StateService.Get<int>(Context.Guild.Id, runCountStateId) + 1;
        StateService.Set(Context.Guild.Id, runCountStateId, runId);

        panel.Queues.Remove(queue);
        StateService.Set(Context.Guild.Id, "rs-queue", panel);

        foreach (var userId in queue.Users)
        {
            runCountStateId = StateService.GetId("rs-run-count", userId, (ulong)queue.Level);
            var cnt = StateService.Get<int>(Context.Guild.Id, runCountStateId);
            cnt++;
            StateService.Set(Context.Guild.Id, runCountStateId, cnt);

            await RemoveFromQueue(Context.Guild, Context.Channel, null, Context.Guild.GetUser(userId), level);
        }

        await RefreshQueueList(Context.Guild, Context.Channel, true);
        await PostStartedQueue(Context.Guild, Context.Channel, queue, runId);
        StateService.Set(Context.Guild.Id, "rs-log-" + runId.ToStr(), queue);

        var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);

        var response = "RS" + level.ToStr() + " force started!";
        if (queue.Users.Count > 1)
        {
            response += " Meet where?";
        }

        response += " (" + queue.Users.Count.ToStr() + "/" + queue.Users.Count.ToStr() + ")"
            + "\n" + string.Join(" ", queue.Users.Select(x =>
            {
                var user = Context.Guild.GetUser(x);
                return alliance.GetUserCorpIcon(user) + user.Mention;
            }));

        CleanupService.RegisterForDeletion(10 * 60,
            await ReplyAsync(response));
    }

    private static async Task PostStartedQueue(SocketGuild guild, ISocketMessageChannel channel, RsQueueEntry queue, int runId)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);

        var role = guild.Roles.FirstOrDefault(x => x.Name == "RS" + queue.Level.ToStr());
        if (role == null)
        {
            await channel.BotResponse("There is no role for RS" + queue.Level.ToStr() + ".", ResponseType.error);
            return;
        }

        var roleMentionStateId = StateService.GetId("rs-queue-last-role-mention", role.Id);
        var lastRsMention = StateService.Get<DateTime?>(guild.Id, roleMentionStateId);

        var eb = new EmbedBuilder();
        eb
            .WithTitle("RS" + queue.Level.ToStr() + " run (" + queue.Users.Count.ToStr() + "/" + queue.Users.Count.ToStr() + ")")
            .WithColor(Color.Blue)
            .WithFooter(DiscordBot.FunFooter, guild.CurrentUser.GetAvatarUrl())
            .WithThumbnailUrl(guild.Emotes.FirstOrDefault(x => x.Name == "bs6")?.Url)
            .WithDescription(string.Join("\n",
                queue.Users.Select(userId =>
                {
                    var user = guild.GetUser(userId);
                    var runCountStateId = StateService.GetId("rs-run-count", userId, (ulong)queue.Level);
                    var runCount = StateService.Get<int>(guild.Id, runCountStateId);

                    var modList = "";
                    var mods = StateService.Get<UserRsMod>(guild.Id, StateService.GetId("rs-mod", user.Id));
                    if (mods != null)
                    {
                        if (mods.Rse)
                            modList += guild.GetEmoteReference("rse");
                        if (mods.NoSanc)
                            modList += guild.GetEmoteReference("nosanc");
                        if (mods.NoTele)
                            modList += guild.GetEmoteReference("notele");
                        if (mods.Dart)
                            modList += guild.GetEmoteReference("dart");
                        if (mods.Vengeance)
                            modList += guild.GetEmoteReference("vengeance");
                        if (mods.Strong)
                            modList += "💪";
                    }

                    return alliance.GetUserCorpIcon(user) + user.DisplayName
                        + modList
                        + " [" + runCount + " runs]"
                        + " :watch: " + DateTime.UtcNow.Subtract(StateService.Get<DateTime>(guild.Id, "rs-queue-activity-" + userId.ToStr() + "-" + queue.Level.ToStr()))
                                .ToIntervalStr();
                }))
                 + "\n\n#" + runId.ToStr() + " started after " + DateTime.UtcNow.Subtract(queue.StartedOn).ToIntervalStr() + "."
            );

        await channel.SendMessageAsync(embed: eb.Build());
    }

    public static async Task RefreshQueueList(SocketGuild guild, ISocketMessageChannel channel, bool ignoreEmpty)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var panel = GetQueue(guild.Id);

        if (panel.MessageId != 0)
        {
            try
            {
                await guild.GetTextChannel(panel.ChannelId).DeleteMessageAsync(panel.MessageId);
            }
            catch (Exception)
            {
            }
        }

        var eb = new EmbedBuilder()
            .WithTitle("Active Red Star queues")
            .WithColor(new Color(0, 255, 0))
            .WithFooter(DiscordBot.FunFooter, guild.CurrentUser.GetAvatarUrl())
            .WithThumbnailUrl(guild.Emotes.FirstOrDefault(x => x.Name == "redstar")?.Url);

        var sb = new StringBuilder();

        for (var level = 1; level <= 12; level++)
        {
            var role = guild.Roles.FirstOrDefault(x => x.Name == "RS" + level.ToStr());
            if (role == null)
                continue;

            var queue = panel.Queues.Find(x => x.Level == level);
            if (queue == null)
                continue;

            var roleMentionStateId = StateService.GetId("rs-queue-last-role-mention", role.Id);
            var lastRsMention = StateService.Get<DateTime?>(guild.Id, roleMentionStateId);

            sb
                .Append("**")
                .Append(role.Mention)
                .Append(" (")
                .Append(queue.Users.Count.ToStr())
                .Append("/4)**\n")
                .AppendJoin("\n", queue.Users.Select(userId =>
                {
                    var user = guild.GetUser(userId);
                    var runCountStateId = StateService.GetId("rs-run-count", userId, (ulong)queue.Level);
                    var runCount = StateService.Get<int>(guild.Id, runCountStateId);

                    var modList = "";
                    var mods = StateService.Get<UserRsMod>(guild.Id, StateService.GetId("rs-mod", user.Id));
                    if (mods != null)
                    {
                        if (mods.Rse)
                            modList += guild.GetEmoteReference("rse");
                        if (mods.NoSanc)
                            modList += guild.GetEmoteReference("nosanc");
                        if (mods.NoTele)
                            modList += guild.GetEmoteReference("notele");
                        if (mods.Dart)
                            modList += guild.GetEmoteReference("dart");
                        if (mods.Vengeance)
                            modList += guild.GetEmoteReference("vengeance");
                        if (mods.Strong)
                            modList += "💪";
                    }

                    return alliance.GetUserCorpIcon(user) + user.Mention
                        + (!string.IsNullOrEmpty(modList) ? " " + modList : "")
                        + " [" + runCount + "x]"
                        + " :watch: " + DateTime.UtcNow.Subtract(StateService.Get<DateTime>(guild.Id, "rs-queue-activity-" + userId.ToStr() + "-" + queue.Level.ToStr()))
                                .ToIntervalStr(true, false);
                }))
                .AppendLine()
                .AppendLine();
        }

        var description = sb.ToString();
        if (string.IsNullOrEmpty(description))
        {
            if (ignoreEmpty)
                return;

            eb.WithDescription("All queues are empty.");
        }
        else
        {
            eb.WithDescription(description);
        }

        panel.ChannelId = channel.Id;
        panel.MessageId = (await channel.SendMessageAsync(embed: eb.Build())).Id;
        StateService.Set(guild.Id, "rs-queue", panel);
    }

    public static async Task RemoveFromQueue(SocketGuild guild, ISocketMessageChannel channel, int? specificLevel, SocketGuildUser user, int? exceptLevel)
    {
        var panel = GetQueue(guild.Id);
        foreach (var queue in panel.Queues)
        {
            if (specificLevel != null && specificLevel.Value != queue.Level)
                continue;

            if (exceptLevel == queue.Level)
                continue;

            if (!queue.Users.Contains(user.Id))
            {
                if (specificLevel != null)
                {
                    await channel.BotResponse(user.Mention + " wasn't in RS" + queue.Level.ToStr() + " queue.", ResponseType.error);
                    return;
                }

                continue;
            }

            queue.Users.Remove(user.Id);
            var msg = ":x: " + user.Mention + " left RS" + queue.Level.ToStr() + " queue (" + queue.Users.Count.ToStr() + "/4)";
            await channel.SendMessageAsync(msg);
        }

        panel.RemoveEmtpyQueues();

        StateService.Set(guild.Id, "rs-queue", panel);
    }

    private async Task ShowRsMod(SocketGuild guild, ISocketMessageChannel channel)
    {
        var rsModStateId = "rs-mod-state";
        var rsMod = StateService.Get<RsModState>(guild.Id, rsModStateId);
        if (rsMod != null)
        {
            try
            {
                await guild.GetTextChannel(rsMod.ChannelId).DeleteMessageAsync(rsMod.MessageId);
            }
            catch (Exception)
            {
            }
        }

        var eb = new EmbedBuilder()
            .WithTitle("RS Mod Editor")
            .WithDescription("Please react according to your preferences in the RS queue.")
            .WithColor(Color.Red)
            .WithFooter(DiscordBot.FunFooter, guild.CurrentUser.GetAvatarUrl());

        var sent = await channel.SendMessageAsync(embed: eb.Build());
        await sent.AddReactionsAsync(new IEmote[]
        {
            guild.Emotes.FirstOrDefault(x => x.Name == "rse"),
            guild.Emotes.FirstOrDefault(x => x.Name == "nosanc"),
            guild.Emotes.FirstOrDefault(x => x.Name == "notele"),
            guild.Emotes.FirstOrDefault(x => x.Name == "dart"),
            guild.Emotes.FirstOrDefault(x => x.Name == "vengeance"),
            new Emoji("💪"),
        });

        rsMod = new RsModState()
        {
            ChannelId = channel.Id,
            MessageId = sent.Id,
            LastShown = DateTime.UtcNow,
        };

        StateService.Set(guild.Id, rsModStateId, rsMod);
    }

    internal static async Task HandleReactions(SocketReaction reaction, bool added)
    {
        if (reaction.User.Value.IsBot)
            return;

        var channel = reaction.Channel as SocketGuildChannel;

        var rsModStateId = "rs-mod-state";
        var rsMod = StateService.Get<RsModState>(channel.Guild.Id, rsModStateId);
        if (rsMod == null)
            return;

        var userStateId = StateService.GetId("rs-mod", reaction.UserId);
        var userState = StateService.Get<UserRsMod>(channel.Guild.Id, userStateId)
            ?? new UserRsMod()
            {
                UserID = reaction.UserId,
            };

        switch (reaction.Emote.Name)
        {
            case "rse":
                userState.Rse = added;
                break;
            case "nosanc":
                userState.NoSanc = added;
                break;
            case "notele":
                userState.NoTele = added;
                break;
            case "dart":
                userState.Dart = added;
                break;
            case "veng":
                userState.Vengeance = added;
                break;
            case "💪":
                userState.Strong = added;
                break;
        }

        StateService.Set(channel.Guild.Id, userStateId, userState);
    }

    internal class RsModState
    {
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; init; }
        public DateTime LastShown { get; set; }
    }

    internal class UserRsMod
    {
        public ulong UserID { get; set; }
        public bool NoSanc { get; set; }
        public bool NoTele { get; set; }
        public bool Rse { get; set; }
        public bool Strong { get; set; }
        public bool Vengeance { get; set; }
        public bool Dart { get; set; }
    }

    internal class RsQueueEntry
    {
        public int Level { get; init; }
        public List<ulong> Users { get; init; } = new();
        public DateTime StartedOn { get; init; }
        public DateTime? FalseStart { get; set; }
    }

    internal class RsQueue
    {
        public List<RsQueueEntry> Queues { get; set; } = new List<RsQueueEntry>();
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }

        public void RemoveEmtpyQueues()
        {
            foreach (var queue in Queues.ToList())
            {
                if (queue.Users.Count == 0)
                {
                    Queues.Remove(queue);
                }
            }
        }
    }
}