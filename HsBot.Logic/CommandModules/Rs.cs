namespace HsBot.Logic
{
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;

    [Summary("Red Stars")]
    [RequireContext(ContextType.Guild)]
    public class Rs : BaseModule
    {
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
            await RemoveQueue(Context.Guild, Context.Channel, level, CurrentUser, null);
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

            var found = false;
            for (var i = 1; i <= 12; i++)
            {
                if (level != null && i != level.Value)
                    continue;

                var queueStateId = StateService.GetId("rs-queue", (ulong)i);
                if (StateService.Exists(Context.Guild.Id, queueStateId))
                {
                    await RefreshQueue(Context.Guild, Context.Channel, i);
                    found = true;
                }
            }

            if (!found)
            {
                await Context.Channel.BotResponse("All queues are empty, sorry.", ResponseType.info);
            }
        }

        /*[Command("in")]
        [Summary("in level user|debug only")]
        public async Task I(int level, SocketGuildUser targetUser)
        {
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
            await RemoveQueue(Context.Guild, Context.Channel, level, targetUser, null);
        }*/

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

            var queueStateId = StateService.GetId("rs-queue", (ulong)level);
            var queue = StateService.Get<RsQueueEntry>(guild.Id, queueStateId);
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

            var queueStateId = StateService.GetId("rs-queue", (ulong)selectedLevel);
            var queue = StateService.Get<RsQueueEntry>(Context.Guild.Id, queueStateId)
                ?? new RsQueueEntry()
                {
                    ChannelId = Context.Channel.Id,
                    Level = selectedLevel,
                    StartedOn = DateTime.UtcNow,
                };

            StateService.Set(Context.Guild.Id, "rs-queue-activity-" + user.Id.ToStr(), DateTime.UtcNow);
            StateService.Set(Context.Guild.Id, "rs-queue-activity-" + user.Id.ToStr() + "-" + selectedLevel.ToStr(), DateTime.UtcNow);
            StateService.Delete(Context.Guild.Id, "rs-queue-activity-" + user.Id.ToStr() + "-" + selectedLevel.ToStr() + "-asked");

            if (queue.Users.Contains(user.Id))
            {
                //await ReplyAsync(user.Mention + " is already in RS" + selectedLevel.ToStr() + " queue.");
                await RefreshQueue(Context.Guild, Context.Channel, selectedLevel);
                return;
            }

            queue.Users.Add(user.Id);

            if (queue.Users.Count == 4)
            {
                var runCountStateId = "rs-run-count";
                queue.RunId = StateService.Get<int>(Context.Guild.Id, runCountStateId) + 1;
                StateService.Set(Context.Guild.Id, runCountStateId, queue.RunId);
            }

            StateService.Set(Context.Guild.Id, queueStateId, queue);

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
                    await RemoveQueue(Context.Guild, Context.Channel, null, Context.Guild.GetUser(userId), selectedLevel);
                }
            }

            string response;
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

            response += " (" + queue.Users.Count.ToStr() + "/4), " + user.Mention + " joined.";
            await ReplyAsync(response);

            await RefreshQueue(Context.Guild, Context.Channel, selectedLevel);

            var alliance = AllianceLogic.GetAlliance(Context.Guild.Id);

            if (queue.Users.Count == 4)
            {
                StateService.Rename(Context.Guild.Id, queueStateId, "rs-log-" + queue.RunId.Value.ToStr());

                response = "RS" + selectedLevel.ToStr() + " ready! Meet where? (4/4)"
                    + "\n" + string.Join(" ", queue.Users.Select(x =>
                        {
                            var user = Context.Guild.GetUser(x);
                            return alliance.GetUserCorpIcon(user) + user.Mention;
                        }));

                CleanupService.RegisterForDeletion(10 * 60,
                    await ReplyAsync(response));
            }
        }

        private async Task StartQueue(int level)
        {
            var queueStateId = StateService.GetId("rs-queue", (ulong)level);

            var queue = StateService.Get<RsQueueEntry>(Context.Guild.Id, queueStateId);
            if (queue == null)
            {
                await Context.Channel.BotResponse("RS" + level.ToStr() + " queue is empty, there is nothing to start...", ResponseType.error);
                return;
            }

            var runCountStateId = "rs-run-count";
            queue.RunId = StateService.Get<int>(Context.Guild.Id, runCountStateId) + 1;
            StateService.Set(Context.Guild.Id, runCountStateId, queue.RunId);
            StateService.Set(Context.Guild.Id, queueStateId, queue);

            foreach (var userId in queue.Users)
            {
                runCountStateId = StateService.GetId("rs-run-count", userId, (ulong)queue.Level);
                var cnt = StateService.Get<int>(Context.Guild.Id, runCountStateId);
                cnt++;
                StateService.Set(Context.Guild.Id, runCountStateId, cnt);

                await RemoveQueue(Context.Guild, Context.Channel, null, Context.Guild.GetUser(userId), level);
            }

            await RefreshQueue(Context.Guild, Context.Channel, level);
            StateService.Rename(Context.Guild.Id, queueStateId, "rs-log-" + queue.RunId.Value.ToStr());

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

        private static async Task RefreshQueue(SocketGuild guild, ISocketMessageChannel channel, int level)
        {
            var alliance = AllianceLogic.GetAlliance(guild.Id);

            var role = guild.Roles.FirstOrDefault(x => x.Name == "RS" + level.ToStr());
            if (role == null)
            {
                await channel.BotResponse("There is no role for RS" + level.ToStr() + ".", ResponseType.error);
                return;
            }

            var queueStateId = StateService.GetId("rs-queue", (ulong)level);
            var queue = StateService.Get<RsQueueEntry>(guild.Id, queueStateId);
            if (queue == null)
            {
                await channel.BotResponse("RS" + level.ToStr() + " queue is empty.", ResponseType.info);
                return;
            }

            if (queue.MessageId != 0)
            {
                try
                {
                    await guild.GetTextChannel(queue.ChannelId).DeleteMessageAsync(queue.MessageId);
                }
                catch (Exception)
                {
                }
            }

            var roleMentionStateId = StateService.GetId("rs-queue-last-role-mention", role.Id);
            var lastRsMention = StateService.Get<DateTime?>(guild.Id, roleMentionStateId);

            var eb = new EmbedBuilder();
            if (queue.RunId == null)
            {
                eb.WithTitle("RS" + queue.Level.ToStr() + " queue (" + queue.Users.Count.ToStr() + "/4)");
            }
            else
            {
                eb.WithTitle("RS" + queue.Level.ToStr() + " run #" + queue.RunId.Value.ToStr() + " (" + queue.Users.Count.ToStr() + "/" + queue.Users.Count.ToStr() + ")");
            }

            eb.WithDescription(string.Join("\n",
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
                                modList += guild.Emotes.FirstOrDefault(x => x.Name == "rse").GetReference();
                            if (mods.NoSanc)
                                modList += guild.Emotes.FirstOrDefault(x => x.Name == "nosanc").GetReference();
                            if (mods.NoTele)
                                modList += guild.Emotes.FirstOrDefault(x => x.Name == "notele").GetReference();
                            if (mods.Dart)
                                modList += guild.Emotes.FirstOrDefault(x => x.Name == "dart").GetReference();
                            if (mods.Vengeance)
                                modList += guild.Emotes.FirstOrDefault(x => x.Name == "vengeance").GetReference();
                            if (mods.Strong)
                                modList += "💪";
                        }

                        return alliance.GetUserCorpIcon(user) + user.DisplayName
                            + modList
                            + " [" + runCount + " runs]"
                            + " :watch: " + DateTime.UtcNow.Subtract(StateService.Get<DateTime>(guild.Id, "rs-queue-activity-" + userId.ToStr() + "-" + queue.Level.ToStr()))
                                    .ToIntervalStr();
                    }
                )));

            if (queue.RunId == null)
            {
                eb.WithFooter("Queue created " + DateTime.UtcNow.Subtract(queue.StartedOn).ToIntervalStr() + " ago. "
                    + (lastRsMention != null
                        ? role.Name + " was mentioned " + DateTime.UtcNow.Subtract(lastRsMention.Value).ToIntervalStr() + " ago."
                        : ""));
            }
            else
            {
                eb.WithFooter("Run started after " + DateTime.UtcNow.Subtract(queue.StartedOn).ToIntervalStr() + ".");
            }

            queue.ChannelId = channel.Id;
            queue.MessageId = (await channel.SendMessageAsync(embed: eb.Build())).Id;
            StateService.Set(guild.Id, queueStateId, queue);
        }

        public static async Task RemoveQueue(SocketGuild guild, ISocketMessageChannel channel, int? specificLevel, SocketGuildUser user, int? exceptLevel)
        {
            var nonEmptyQueuesLeft = new List<int>();

            for (var level = 1; level <= 12; level++)
            {
                var queueStateId = StateService.GetId("rs-queue", (ulong)level);

                if (specificLevel != null && specificLevel.Value != level)
                    continue;

                if (exceptLevel == level)
                    continue;

                var queue = StateService.Get<RsQueueEntry>(guild.Id, queueStateId);
                if (queue == null)
                    continue;

                if (!queue.Users.Contains(user.Id))
                {
                    if (specificLevel != null)
                    {
                        await channel.BotResponse(user.Mention + " wasn't in RS" + level.ToStr() + " queue.", ResponseType.error);
                        return;
                    }

                    continue;
                }

                queue.Users.Remove(user.Id);
                if (queue.Users.Count > 0)
                {
                    StateService.Set(guild.Id, queueStateId, queue);
                }
                else
                {
                    if (queue.MessageId != 0)
                    {
                        try
                        {
                            await guild.GetTextChannel(queue.ChannelId).DeleteMessageAsync(queue.MessageId);
                        }
                        catch (Exception)
                        {
                        }
                    }

                    StateService.Delete(guild.Id, queueStateId);
                }

                var msg = ":x: " + user.Mention + " left RS" + queue.Level.ToStr() + " queue (" + queue.Users.Count.ToStr() + "/4)";
                await channel.SendMessageAsync(msg);

                if (queue.Users.Count > 0)
                    nonEmptyQueuesLeft.Add(level);
            }

            foreach (var level in nonEmptyQueuesLeft)
            {
                await RefreshQueue(guild, channel, level);
            }
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
                .WithDescription("Please react according to your preferences in the RS queue.");

            var sent = await channel.SendMessageAsync(embed: eb.Build());
            await sent.AddReactionsAsync(new IEmote[]
            {
                guild.Emotes.FirstOrDefault(x =>x.Name == "rse"),
                guild.Emotes.FirstOrDefault(x =>x.Name == "nosanc"),
                guild.Emotes.FirstOrDefault(x =>x.Name == "notele"),
                guild.Emotes.FirstOrDefault(x =>x.Name == "dart"),
                guild.Emotes.FirstOrDefault(x =>x.Name == "vengeance"),
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
            public ulong ChannelId { get; set; }
            public ulong MessageId { get; set; }
            public int? RunId { get; set; }
            public DateTime? FalseStart { get; set; }
        }
    }
}