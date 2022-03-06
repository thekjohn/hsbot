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

            Discord.ReactionAdded += (message, channel, reaction) => Rs.HandleReactions(reaction, true);
            Discord.ReactionRemoved += (message, channel, reaction) => Rs.HandleReactions(reaction, false);

            Discord.ReactionAdded += (message, channel, reaction) => WsSignupLogic.HandleReactions(reaction, true);
            Discord.ReactionRemoved += (message, channel, reaction) => WsSignupLogic.HandleReactions(reaction, false);

            Discord.ReactionAdded += (message, channel, reaction) => AltsLogic.HandleReactions(reaction, true);
            Discord.ReactionRemoved += (message, channel, reaction) => AltsLogic.HandleReactions(reaction, false);

            LogService.Log(null, "folder: " + Services.State.Folder, ConsoleColor.Magenta);

            Commands = new CommandService();
            Commands.Log += OnLog;

            Commands.CommandExecuted += CommandExecutedAsync;

            CommandPrefix = File.ReadAllText(@"c:\HsBot\Bot.CommandPrefix.txt")[0];
        }

        private async Task OnReady()
        {
            new Thread(MessageCleanupThreadWorker).Start();
            new Thread(RsCleanupThreadWorker).Start();
            new Thread(RemindLogic.SendRemindersThreadWorker).Start();

            //var msg = await Discord.Guilds.First().GetTextChannel(830622786396618772).GetMessageAsync(943863908253438002);

        }

        private async void MessageCleanupThreadWorker()
        {
            while (true)
            {
                var messagesToDelete = CleanupService.GetMessagesToDelete();
                if (messagesToDelete != null)
                {
                    foreach (var msg in messagesToDelete)
                    {
                        try
                        {
                            await msg.DeleteAsync();
                        }
                        catch (Exception)
                        {
                        }

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
                        var timeoutStateId = Services.State.GetId("rs-queue-timeout", (ulong)level);
                        var timeoutMinutes = Services.State.Get<int>(guild.Id, timeoutStateId);
                        if (timeoutMinutes == 0)
                            timeoutMinutes = 10;

                        var queueStateId = Services.State.GetId("rs-queue", (ulong)level);
                        var queue = Services.State.Get<Rs.RsQueueEntry>(guild.Id, queueStateId);
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
                                            await Rs.RemoveQueue(guild, channel, level, user, null);
                                            break; // skip the check and removal of other users until next cycle
                                        }

                                        continue;
                                    }

                                    Services.State.Set(guild.Id, userActivityConfirmationAskedStateId, DateTime.UtcNow);

                                    var confirmTimeoutMinutes = 2;
                                    CleanupService.RegisterForDeletion(confirmTimeoutMinutes * 60,
                                        await channel.SendMessageAsync(":grey_question: " + user.Mention + ", still in for RS" + level.ToStr() + "? Type `" + DiscordBot.CommandPrefix + "in " + level.ToStr() + "` to confirm within the next " + confirmTimeoutMinutes.ToStr() + " minutes."));
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
                    LogService.Log(null, "unhandled exception: " + ex.Message, ConsoleColor.Red);
                }
            };

            var token = File.ReadAllText(@"c:\HsBot\Bot.Token.txt");

            await Discord.LoginAsync(TokenType.Bot, token);
            await Discord.StartAsync();

            await Commands.AddModuleAsync(typeof(HelpCommandModule), null);
            await Commands.AddModuleAsync(typeof(Rs), null);
            await Commands.AddModuleAsync(typeof(Ws), null);
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
                    LogService.Log(null, rawMessage.Content, ConsoleColor.Yellow);
                    return;
                }

                if (message.Source == MessageSource.System)
                {
                    LogService.Log(null, rawMessage.Content, ConsoleColor.Red);
                    return;
                }

                if (message.Source == MessageSource.User && message.Channel is SocketTextChannel channel && message.Content.Contains("<@"))
                {
                    var afkList = AfkLogic.GetAfkList(channel.Guild.Id);
                    foreach (var afk in afkList)
                    {
                        if (message.Content.Contains(MentionUtils.MentionUser(afk.UserId)))
                        {
                            await channel.BotResponse(channel.Guild.GetUser(afk.UserId).DisplayName + " is AFK for " + afk.EndsOn.Subtract(DateTime.UtcNow).ToIntervalStr() + ".", ResponseType.infoStay);
                        }
                    }
                }

                var argPos = 0;

                if (!message.HasCharPrefix(CommandPrefix, ref argPos))
                    return;

                if (message.Content[1] == ' ')
                    argPos++;

                var context = new SocketCommandContext(Discord, message);
                await Commands.ExecuteAsync(context, argPos, null);
            }
            catch (Exception ex)
            {
                LogService.Log(null, "error in " + nameof(OnMessageReceived) + ":" + ex.ToString());
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
            LogService.Log(null, log.Message, ConsoleColor.Magenta);
            return Task.CompletedTask;
        }
    }
}