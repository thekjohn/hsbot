﻿namespace HsBot.Logic
{
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;

    [Summary("RS queue")]
    public class RsQueue : BaseModule
    {
        [Command("in")]
        [Alias("i")]
        [Summary("in [level]|enqueue to your highest, or a specific level queue")]
        public async Task In(int? level = null)
        {
            await AddQueue(level, CurrentUser);
        }

        [Command("out")]
        [Alias("o")]
        [Summary("out [level]|dequeue from a specific, or all queues")]
        public async Task Out(int? level = null)
        {
            await Context.Message.DeleteAsync();
            await RemoveQueue(Context.Guild, Context.Channel, level, CurrentUser, null);
        }

        [Command("ping")]
        [Summary("ping <level>|ping an RS role")]
        public async Task Ping(int level)
        {
            await Context.Message.DeleteAsync();
            await Ping(Context.Guild, Context.Channel, level);
        }

        [Command("start")]
        [Summary("start <level>|force start on a queue")]
        public async Task Start(int level)
        {
            await StartQueue(level);
        }

        [Command("qq")]
        [Summary("qq|query active queues")]
        public async Task QueryQueues(int? level = null)
        {
            await Context.Message.DeleteAsync();

            var found = false;
            for (var i = 1; i <= 12; i++)
            {
                if (level != null && i != level.Value)
                    continue;

                var queueStateId = Services.State.GetId("rs-queue", (ulong)i);
                if (Services.State.Exists(Context.Guild.Id, queueStateId))
                {
                    await RefreshQueue(Context.Guild, Context.Channel, i);
                    found = true;
                }
            }

            if (!found)
            {
                await ReplyAsync("All queues are empty, sorry.");
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

        private static async Task Ping(SocketGuild guild, ISocketMessageChannel channel, int level)
        {
            var role = guild.Roles.FirstOrDefault(x => x.Name == "RS" + level.ToStr());
            if (role == null)
            {
                await channel.SendMessageAsync("There is no role for RS" + level.ToStr() + ".");
                return;
            }

            var queueStateId = Services.State.GetId("rs-queue", (ulong)level);
            var queue = Services.State.Get<RsQueueEntry>(guild.Id, queueStateId);
            if (queue == null)
            {
                await channel.SendMessageAsync("You can't ping an empty queue.");
                return;
            }

            var roleMentionStateId = Services.State.GetId("rs-queue-last-role-mention", role.Id);
            var lastRsMention = Services.State.Get<DateTime?>(guild.Id, roleMentionStateId);
            if (lastRsMention == null || DateTime.UtcNow > lastRsMention.Value.AddMinutes(5))
            {
                lastRsMention = DateTime.UtcNow;
                Services.State.Set(guild.Id, roleMentionStateId, lastRsMention);
                await channel.SendMessageAsync(role.Mention + " anyone? (" + queue.Users.Count.ToStr() + "/4)");
            }
            else
            {
                await channel.SendMessageAsync(role.Name + " anyone? (" + queue.Users.Count.ToStr() + "/4)");
            }
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
            if (role == null)
            {
                await ReplyAsync("There is no role for RS" + selectedLevel.ToStr() + ".");
                return;
            }

            var queueStateId = Services.State.GetId("rs-queue", (ulong)selectedLevel);
            var queue = Services.State.Get<RsQueueEntry>(Context.Guild.Id, queueStateId)
                ?? new RsQueueEntry()
                {
                    ChannelId = Context.Channel.Id,
                    Level = selectedLevel,
                    StartedOn = DateTime.UtcNow,
                };

            Services.State.Set(Context.Guild.Id, "rs-queue-activity-" + user.Id.ToStr(), DateTime.UtcNow);
            Services.State.Set(Context.Guild.Id, "rs-queue-activity-" + user.Id.ToStr() + "-" + selectedLevel.ToStr(), DateTime.UtcNow);
            Services.State.Delete(Context.Guild.Id, "rs-queue-activity-" + user.Id.ToStr() + "-" + selectedLevel.ToStr() + "-asked");

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
                queue.RunId = Services.State.Get<int>(Context.Guild.Id, runCountStateId) + 1;
                Services.State.Set(Context.Guild.Id, runCountStateId, queue.RunId);
            }

            Services.State.Set(Context.Guild.Id, queueStateId, queue);

            if (queue.Users.Count == 4)
            {
                foreach (var userId in queue.Users)
                {
                    var runCountStateId = Services.State.GetId("rs-run-count", userId, (ulong)queue.Level);
                    var cnt = Services.State.Get<int>(Context.Guild.Id, runCountStateId);
                    cnt++;
                    Services.State.Set(Context.Guild.Id, runCountStateId, cnt);
                }

                foreach (var userId in queue.Users)
                {
                    await RemoveQueue(Context.Guild, Context.Channel, null, Context.Guild.GetUser(userId), level);
                }
            }

            string response;
            var roleMentionStateId = Services.State.GetId("rs-queue-last-role-mention", role.Id);
            var lastRsMention = Services.State.Get<DateTime?>(Context.Guild.Id, roleMentionStateId);
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

            await RefreshQueue(Context.Guild, Context.Channel, selectedLevel);

            var alliance = Alliance.GetAlliance(Context.Guild.Id);

            if (queue.Users.Count == 4)
            {
                Services.State.Rename(Context.Guild.Id, queueStateId, "rs-log-" + queue.RunId.Value.ToStr());

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

            var queueStateId = Services.State.GetId("rs-queue", (ulong)level);

            var queue = Services.State.Get<RsQueueEntry>(Context.Guild.Id, queueStateId);
            if (queue == null)
            {
                Services.Cleanup.RegisterForDeletion(10,
                    await ReplyAsync("RS" + level.ToStr() + " queue is empty, there is nothing to start..."));

                return;
            }

            var runCountStateId = "rs-run-count";
            queue.RunId = Services.State.Get<int>(Context.Guild.Id, runCountStateId) + 1;
            Services.State.Set(Context.Guild.Id, runCountStateId, queue.RunId);
            Services.State.Set(Context.Guild.Id, queueStateId, queue);

            foreach (var userId in queue.Users)
            {
                runCountStateId = Services.State.GetId("rs-run-count", userId, (ulong)queue.Level);
                var cnt = Services.State.Get<int>(Context.Guild.Id, runCountStateId);
                cnt++;
                Services.State.Set(Context.Guild.Id, runCountStateId, cnt);

                await RemoveQueue(Context.Guild, Context.Channel, null, Context.Guild.GetUser(userId), level);
            }

            await RefreshQueue(Context.Guild, Context.Channel, level);
            Services.State.Rename(Context.Guild.Id, queueStateId, "rs-log-" + queue.RunId.Value.ToStr());

            var alliance = Alliance.GetAlliance(Context.Guild.Id);

            var response = "RS" + level.ToStr() + " force started!";
            if (queue.Users.Count > 1)
            {
                response += " Meet where?";
            }

            response += " (" + queue.Users.Count.ToStr() + "/" + queue.Users.Count.ToStr() + ")"
                + " " + string.Join(" ", queue.Users.Select(x =>
                {
                    var user = Context.Guild.GetUser(x);
                    return alliance.GetUserCorpIcon(user) + " " + user.Mention;
                }));

            await ReplyAsync(response);
        }

        private static async Task RefreshQueue(SocketGuild guild, ISocketMessageChannel channel, int level)
        {
            var alliance = Alliance.GetAlliance(guild.Id);

            var role = guild.Roles.FirstOrDefault(x => x.Name == "RS" + level.ToStr());
            if (role == null)
            {
                await channel.SendMessageAsync("There is no role for RS" + level.ToStr() + ".");
                return;
            }

            var queueStateId = Services.State.GetId("rs-queue", (ulong)level);
            var queue = Services.State.Get<RsQueueEntry>(guild.Id, queueStateId);
            if (queue == null)
            {
                await channel.SendMessageAsync("There's nobody in the RS" + level.ToStr() + " queue just now.");
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

            var roleMentionStateId = Services.State.GetId("rs-queue-last-role-mention", role.Id);
            var lastRsMention = Services.State.Get<DateTime?>(guild.Id, roleMentionStateId);

            var msg = new EmbedBuilder();
            if (queue.RunId == null)
            {
                msg.WithTitle("RS" + queue.Level.ToStr() + " queue (" + queue.Users.Count.ToStr() + "/4)");
            }
            else
            {
                msg.WithTitle("RS" + queue.Level.ToStr() + " run #" + queue.RunId.Value.ToStr() + " (" + queue.Users.Count.ToStr() + "/" + queue.Users.Count.ToStr() + ")");
            }

            msg.WithDescription(string.Join("\n",
                queue.Users.Select(userId =>
                    {
                        var user = guild.GetUser(userId);
                        var runCountStateId = Services.State.GetId("rs-run-count", userId, (ulong)queue.Level);

                        return alliance.GetUserCorpIcon(user) + " " + user.Nickname
                            + " [" + Services.State.Get<int>(guild.Id, runCountStateId) + " runs]"
                            + " :watch: " + Services.State.Get<DateTime>(guild.Id, "rs-queue-activity-" + userId.ToStr() + "-" + queue.Level.ToStr())
                                    .GetAgoString();
                    }
                )));

            if (queue.RunId == null)
            {
                msg.WithFooter("Queue created " + queue.StartedOn.GetAgoString() + " ago. "
                    + (lastRsMention != null
                        ? role.Name + " was mentioned " + lastRsMention.Value.GetAgoString() + " ago."
                        : ""));
            }
            else
            {
                msg.WithFooter("Run started after " + queue.StartedOn.GetAgoString() + ".");
            }

            queue.ChannelId = channel.Id;
            queue.MessageId = (await channel.SendMessageAsync(embed: msg.Build())).Id;
            Services.State.Set(guild.Id, queueStateId, queue);
        }

        public static async Task RemoveQueue(SocketGuild guild, ISocketMessageChannel channel, int? specificLevel, SocketGuildUser user, int? exceptLevel)
        {
            var nonEmptyQueuesLeft = new List<int>();

            for (var level = 1; level <= 12; level++)
            {
                var queueStateId = Services.State.GetId("rs-queue", (ulong)level);

                if (specificLevel != null && specificLevel.Value != level)
                    continue;

                if (exceptLevel == level)
                    continue;

                var queue = Services.State.Get<RsQueueEntry>(guild.Id, queueStateId);
                if (queue == null)
                {
                    if (specificLevel != null)
                    {
                        Services.Cleanup.RegisterForDeletion(10,
                            await channel.SendMessageAsync("RS" + level.ToStr() + " queue was already empty..."));

                        return;
                    }

                    continue;
                }

                if (!queue.Users.Contains(user.Id))
                {
                    if (specificLevel != null)
                    {
                        Services.Cleanup.RegisterForDeletion(10,
                            await channel.SendMessageAsync(user.Mention + " wasn't even in RS" + level.ToStr() + " queue..."));

                        return;
                    }

                    continue;
                }

                queue.Users.Remove(user.Id);
                if (queue.Users.Count > 0)
                {
                    Services.State.Set(guild.Id, queueStateId, queue);
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

                    Services.State.Delete(guild.Id, queueStateId);
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

        internal class RsQueueEntry
        {
            public int Level { get; init; }
            public List<ulong> Users { get; init; } = new();
            public DateTime StartedOn { get; init; }
            public ulong ChannelId { get; set; }
            public ulong MessageId { get; set; }
            public int? RunId { get; set; }
        }
    }
}