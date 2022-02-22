namespace HsBot.Logic
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;

    public class DiscordBot
    {
        public static DiscordSocketClient Discord { get; private set; }
        public static CommandService Commands { get; private set; }

        public static char CommandPrefix { get; private set; }

        public DiscordBot()
        {
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All,
            };

            Discord = new DiscordSocketClient(config);
            Discord.Log += OnLog;
            Discord.MessageReceived += OnMessageReceived;
            Discord.Ready += OnReady;

            Services.Log.Log(null, "folder: " + Services.State.Folder, ConsoleColor.Magenta);

            Commands = new CommandService();
            Commands.Log += OnLog;

            Commands.CommandExecuted += CommandExecutedAsync;

            CommandPrefix = File.ReadAllText(@"c:\HsBot\Bot.CommandPrefix.txt")[0];
        }

        private async Task OnReady()
        {
            new Thread(MessageCleanupThreadWorker).Start();
            new Thread(RsCleanupThreadWorker).Start();
        }

        private async void MessageCleanupThreadWorker()
        {
            while (true)
            {
                var messagesToDelete = Services.Cleanup.GetMessagesToDelete();
                if (messagesToDelete != null)
                {
                    foreach (var msg in messagesToDelete)
                    {
                        await msg.DeleteAsync();
                        Thread.Sleep(1000);
                    }
                }

                Thread.Sleep(100);
            }
        }

        private async void RsCleanupThreadWorker()
        {
            while (true)
            {
                var now = DateTime.UtcNow;

                foreach (var guild in Discord.Guilds)
                {
                    for (var level = 1; level <= 12; level++)
                    {
                        var timeoutStateId = Services.State.GetId("rs-queue-timout", (ulong)level);
                        var timeoutMinutes = Services.State.Get<int>(guild.Id, timeoutStateId);
                        if (timeoutMinutes == 0)
                            timeoutMinutes = 10;

                        var queueStateId = Services.State.GetId("rs-queue", (ulong)level);
                        var queue = Services.State.Get<RsQueue.RsQueueEntry>(guild.Id, queueStateId);
                        if (queue == null)
                            continue;

                        var channel = guild.GetTextChannel(queue.ChannelId);
                        if (channel == null)
                            continue;

                        foreach (var userId in queue.Users.ToList())
                        {
                            var userActivityStateId = "rs-queue-activity-" + userId.ToStr() + "-" + level.ToStr();
                            var userActivity = Services.State.Get<DateTime>(guild.Id, userActivityStateId);
                            if (userActivity.Year == 0)
                                continue;

                            if (userActivity.AddMinutes(timeoutMinutes) < now)
                            {
                                var user = guild.GetUser(userId);
                                if (user != null)
                                {
                                    var userActivityConfirmationAskedStateId = userActivityStateId + "-asked";
                                    if (Services.State.Exists(guild.Id, userActivityConfirmationAskedStateId))
                                    {
                                        var askedOn = Services.State.Get<DateTime>(guild.Id, userActivityConfirmationAskedStateId);
                                        if (askedOn.AddMinutes(2) < now)
                                        {
                                            await RsQueue.RemoveQueue(guild, channel, level, user, null);
                                            break; // skip the check and removal of other users until next cycle
                                        }

                                        continue;
                                    }

                                    Services.State.Set(guild.Id, userActivityConfirmationAskedStateId, DateTime.UtcNow);
                                    await channel.SendMessageAsync(user.Mention + ", still in for RS" + level.ToStr() + "? Type `!in " + level.ToStr() + "` to confirm within the next 2 minutes.");
                                }
                            }
                        }
                    }
                }

                Thread.Sleep(5000);
            }
        }

        public async Task MainAsync()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    Services.Log.Log(null, "unhandled exception: " + ex.Message, ConsoleColor.Red);
                }
            };

            var token = File.ReadAllText(@"c:\HsBot\Bot.Token.txt");

            await Discord.LoginAsync(TokenType.Bot, token);
            await Discord.StartAsync();

            await Commands.AddModuleAsync(typeof(HelpCommandModule), null);
            await Commands.AddModuleAsync(typeof(RsQueue), null);
            await Commands.AddModuleAsync(typeof(Wiki), null);
            await Commands.AddModuleAsync(typeof(Admin), null);

            await Task.Delay(Timeout.Infinite);
        }

        private async Task OnMessageReceived(SocketMessage rawMessage)
        {
            try
            {
                if (rawMessage is not SocketUserMessage message)
                {
                    Services.Log.Log(null, rawMessage.Content, ConsoleColor.Yellow);
                    return;
                }

                if (message.Source == MessageSource.System)
                {
                    Services.Log.Log(null, rawMessage.Content, ConsoleColor.Red);
                    return;
                }

                var context = new SocketCommandContext(Discord, message);

                var argPos = 0;

                if (!message.HasCharPrefix(CommandPrefix, ref argPos))
                {
                    return;
                }

                if (message.Content[1] == ' ')
                    argPos++;

                await Commands.ExecuteAsync(context, argPos, null);
            }
            catch (Exception ex)
            {
                Services.Log.Log(null, "error in " + nameof(OnMessageReceived) + ":" + ex.ToString());
            }
        }

        public async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (!command.IsSpecified)
                return;

            if (result.IsSuccess)
                return;

            if (result.Error == CommandError.BadArgCount)
            {
                await context.Channel.SendMessageAsync("Parameter count does not match any command's. Use " + CommandPrefix + "help to get some help.");
                return;
            }

            await context.Channel.SendMessageAsync("error: " + result);
        }

        private Task OnLog(LogMessage log)
        {
            Services.Log.Log(null, log.Message, ConsoleColor.Magenta);
            return Task.CompletedTask;
        }
    }
}