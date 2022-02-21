namespace HsBot.Logic
{
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;

    [Summary("RS bot")]
    public class RsBotCommandModule : BaseModule
    {
        [Command("start")]
        [Summary("start level|force start on a queue")]
        public async Task Start(int level)
        {
            await StartQueue(level);
        }

        [Command("qq")]
        [Summary("qq|query actvive queues")]
        public async Task QueryQueues(int? level = null)
        {
            await Context.Message.DeleteAsync();

            var found = false;
            for (var i = 1; i <= 11; i++)
            {
                if (level != null && i != level.Value)
                    continue;

                var queueStateId = Services.State.GetId("rs-queue", Context.Guild.Id, Context.Channel.Id, (ulong)i);
                if (Services.State.Exists(Context.Guild.Id, queueStateId))
                {
                    await RefreshQueue(i, false);
                    found = true;
                }
            }

            if (!found)
            {
                await ReplyAsync("All queues are empty, sorry.");
            }
        }

        [Command("in")]
        [Alias("i")]
        [Summary("in level|enqueue. if level is empty, then it is decided based on the user's role")]
        public async Task In(int? level = null)
        {
            await AddQueue(level, CurrentUser);
        }

        [Command("out")]
        [Alias("o")]
        [Summary("out level|dequeue. if level is empty, then it is decided based on the user's role")]
        public async Task Out(int? level = null)
        {
            await RemoveQueue(level, CurrentUser);
        }

        [Command("in")]
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
            await RemoveQueue(level, targetUser);
        }

        private async Task AddQueue(int? level, SocketGuildUser user)
        {
            if (user == null)
                return;

            await Context.Message.DeleteAsync();

            int selectedLevel;
            if (level == null)
            {
                var highestRsRoleNumber = user.GetHighestRsRoleNumber();
                if (highestRsRoleNumber == null)
                {
                    await ReplyAsync("RS role of " + user.Mention + " cannot be determined: " + user.Nickname);
                    return;
                }

                selectedLevel = highestRsRoleNumber.Value;
            }
            else
            {
                selectedLevel = level.Value;
            }

            var role = Context.Guild.Roles.FirstOrDefault(x => x.Name == "RS" + selectedLevel.ToStr());

            var queueStateId = Services.State.GetId("rs-queue", Context.Guild.Id, Context.Channel.Id, (ulong)selectedLevel);

            var queue = Services.State.Get<RsQueue>(Context.Guild.Id, queueStateId)
                ?? new RsQueue()
                {
                    Level = selectedLevel,
                    StartedOn = DateTime.UtcNow,
                };

            Services.State.Set(Context.Guild.Id, "rs-queue-activity-" + user.Id.ToStr(), DateTime.UtcNow);
            Services.State.Set(Context.Guild.Id, "rs-queue-activity-" + user.Id.ToStr() + "-" + selectedLevel.ToStr(), DateTime.UtcNow);

            if (queue.Users.Contains(user.Id))
            {
                await ReplyAsync(user.Mention + " is already in RS" + selectedLevel.ToStr() + " queue.");
                await RefreshQueue(selectedLevel, false);
                return;
            }

            queue.Users.Add(user.Id);
            Services.State.Set(Context.Guild.Id, queueStateId, queue);

            var roleMentionStateId = Services.State.GetId("rs-queue-last-role-mention", role.Id);
            var lastRsMention = Services.State.Get<DateTime?>(Context.Guild.Id, roleMentionStateId);

            string response;
            if (lastRsMention == null || DateTime.UtcNow > lastRsMention.Value.AddMinutes(5))
            {
                lastRsMention = DateTime.UtcNow;
                Services.State.Set(Context.Guild.Id, roleMentionStateId, lastRsMention);
                response = role.Mention;
            }
            else
            {
                response = role.Name;
            }

            response += " (" + queue.Users.Count.ToStr() + "/4), " + user.Mention + " joined.";

            await ReplyAsync(response);

            if (queue.Users.Count == 4)
            {
                foreach (var userId in queue.Users)
                {
                    var runCountStateId = Services.State.GetId("rs-run-count", userId, (ulong)queue.Level);
                    var cnt = Services.State.Get<int>(Context.Guild.Id, runCountStateId);
                    cnt++;
                    Services.State.Set(Context.Guild.Id, runCountStateId, cnt);
                }
            }

            await RefreshQueue(selectedLevel, queue.Users.Count == 4);

            var alliance = AllianceCommandModule.GetAlliance(Context.Guild.Id);

            if (queue.Users.Count == 4)
            {
                Services.State.Delete(Context.Guild.Id, queueStateId);

                response = "RS" + selectedLevel.ToStr() + " ready! Meet where? (4/4)"
                    + "\n" + string.Join(" ", queue.Users.Select(x =>
                        {
                            var user = Context.Guild.GetUser(x);
                            return alliance.GetUserCorpIcon(user) + " " + user.Mention;
                        }));

                await ReplyAsync(response);
            }
        }

        private async Task StartQueue(int level)
        {
            await Context.Message.DeleteAsync();

            var role = Context.Guild.Roles.FirstOrDefault(x => x.Name == "RS" + level.ToStr());

            var queueStateId = Services.State.GetId("rs-queue", Context.Guild.Id, Context.Channel.Id, (ulong)level);

            var queue = Services.State.Get<RsQueue>(Context.Guild.Id, queueStateId);
            if (queue == null)
            {
                Services.Cleanup.RegisterForDeletion(10,
                    await ReplyAsync("RS" + level.ToStr() + " queue is empty, there is nothing to start..."));

                return;
            }

            foreach (var userId in queue.Users)
            {
                var runCountStateId = Services.State.GetId("rs-run-count", userId, (ulong)queue.Level);
                var cnt = Services.State.Get<int>(Context.Guild.Id, runCountStateId);
                cnt++;
                Services.State.Set(Context.Guild.Id, runCountStateId, cnt);
            }

            await RefreshQueue(level, true);

            var alliance = AllianceCommandModule.GetAlliance(Context.Guild.Id);
            Services.State.Delete(Context.Guild.Id, queueStateId);

            var response = "RS" + level.ToStr() + " ready! Meet where? (" + queue.Users.Count.ToStr() + "/" + queue.Users.Count.ToStr() + ")"
                + "\n" + string.Join(" ", queue.Users.Select(x =>
                {
                    var user = Context.Guild.GetUser(x);
                    return alliance.GetUserCorpIcon(user) + " " + user.Mention;
                }));

            await ReplyAsync(response);
        }

        private async Task RefreshQueue(int level, bool started)
        {
            var alliance = AllianceCommandModule.GetAlliance(Context.Guild.Id);

            var queueStateId = Services.State.GetId("rs-queue", Context.Guild.Id, Context.Channel.Id, (ulong)level);
            var queue = Services.State.Get<RsQueue>(Context.Guild.Id, queueStateId);
            if (queue == null)
            {
                await ReplyAsync("There's nobody in the RS" + level.ToStr() + " queue just now.");
                return;
            }

            if (queue.MessageId != 0)
                await Context.Channel.DeleteMessageAsync(queue.MessageId);

            var role = Context.Guild.Roles.FirstOrDefault(x => x.Name == "RS" + queue.Level.ToStr());

            var roleMentionStateId = Services.State.GetId("rs-queue-last-role-mention", role.Id);
            var lastRsMention = Services.State.Get<DateTime?>(Context.Guild.Id, roleMentionStateId);

            var msg = new EmbedBuilder()
                .WithTitle("RS" + queue.Level.ToStr() + " queue (" + queue.Users.Count.ToStr() + "/" + (started ? queue.Users.Count.ToStr() : "4") + ")")
                .WithDescription(string.Join("\n",
                    queue.Users.Select(userId =>
                        {
                            var user = Context.Guild.GetUser(userId);
                            var runCountStateId = Services.State.GetId("rs-run-count", userId, (ulong)queue.Level);

                            return alliance.GetUserCorpIcon(user) + " " + user.Nickname
                                + " [" + Services.State.Get<int>(Context.Guild.Id, runCountStateId) + " runs]"
                                + " :watch: " + Services.State.Get<DateTime>(Context.Guild.Id, "rs-queue-activity-" + userId.ToStr() + "-" + queue.Level.ToStr())
                                        .GetAgoString();
                        }
                    )));

            if (!started)
            {
                msg.WithFooter("Queue created " + queue.StartedOn.GetAgoString() + " ago. "
                    + role.Name + " was mentioned " + lastRsMention.Value.GetAgoString() + " ago.");
            }
            else
            {
                msg.WithFooter("Queue started after " + queue.StartedOn.GetAgoString() + ".");
            }

            queue.MessageId = (await ReplyAsync(embed: msg.Build())).Id;
            Services.State.Set(Context.Guild.Id, queueStateId, queue);
        }

        private async Task RemoveQueue(int? level, SocketGuildUser user)
        {
            if (user == null)
                return;

            await Context.Message.DeleteAsync();

            int selectedLevel;
            if (level == null)
            {
                var highestRsRoleNumber = user.GetHighestRsRoleNumber();
                if (highestRsRoleNumber == null)
                {
                    await ReplyAsync("RS role of " + user.Mention + " cannot be determined: " + user.Nickname);
                    return;
                }

                selectedLevel = highestRsRoleNumber.Value;
            }
            else
            {
                selectedLevel = level.Value;
            }

            var queueStateId = Services.State.GetId("rs-queue", Context.Guild.Id, Context.Channel.Id, (ulong)selectedLevel);

            var queue = Services.State.Get<RsQueue>(Context.Guild.Id, queueStateId);

            if (queue == null)
            {
                Services.Cleanup.RegisterForDeletion(10,
                    await ReplyAsync("RS" + selectedLevel.ToStr() + " queue was already empty..."));

                return;
            }

            if (!queue.Users.Contains(user.Id))
            {
                Services.Cleanup.RegisterForDeletion(10,
                    await ReplyAsync(user.Mention + " wasn't even in RS" + selectedLevel.ToStr() + " queue..."));

                return;
            }

            queue.Users.Remove(user.Id);
            if (queue.Users.Count > 0)
            {
                Services.State.Set(Context.Guild.Id, queueStateId, queue);
            }
            else
            {
                Services.State.Delete(Context.Guild.Id, queueStateId);
            }

            var msg = ":x: " + user.Mention + " left RS" + queue.Level.ToStr() + " queue (" + queue.Users.Count.ToStr() + "/4)";
            await ReplyAsync(msg);

            if (queue.Users.Count > 0)
                await RefreshQueue(selectedLevel, false);
        }
    }
}