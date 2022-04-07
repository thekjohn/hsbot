namespace HsBot.Logic;

public class DiscordBot
{
    public static DiscordSocketClient Discord { get; private set; }
    public static CommandService Commands { get; private set; }

    public static char CommandPrefix { get; private set; }

    public static string FunFooter { get; } = "Jarvis Services Ltd.";

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

        Discord.ReactionAdded += (message, channel, reaction) => GreeterLogic.HandleReactions(reaction, true);
        Discord.ReactionRemoved += (message, channel, reaction) => GreeterLogic.HandleReactions(reaction, false);

        Discord.ReactionAdded += (message, channel, reaction) => RsRoleSelectorLogic.HandleReactions(reaction, true);
        Discord.ReactionRemoved += (message, channel, reaction) => RsRoleSelectorLogic.HandleReactions(reaction, false);

        Discord.UserJoined += Discord_UserJoined;

        LogService.Log(null, "folder: " + StateService.Folder, ConsoleColor.Magenta);

        Commands = new CommandService();
        Commands.Log += OnLog;

        Commands.CommandExecuted += CommandExecutedAsync;

        CommandPrefix = File.ReadAllText(@"c:\HsBot\Bot.CommandPrefix.txt")[0];

        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception ex)
            {
                Console.Write(ex.ToString());
                StateService.Set(0, "exception-" + DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss-fff", CultureInfo.InvariantCulture),
                    ex.Message + "\n" + ex.ToString());
            }
        };
    }

    private async Task Discord_UserJoined(SocketGuildUser user)
    {
        await GreeterLogic.UserJoined(user.Guild, user);
    }

    private async Task OnReady()
    {
        new Thread(MessageCleanupThreadWorker).Start();
        new Thread(RsCleanupThreadWorker).Start();
        new Thread(RemindLogic.SendRemindersThreadWorker).Start();
        new Thread(WsSignupLogic.AutomaticallyCloseThreadWorker).Start();
        new Thread(WsLogic.NotifyThreadWorker).Start();
        new Thread(AfkLogic.AutomaticallyRemoveAfkThreadWorker).Start();
        new Thread(CompendiumLogic.ImportThreadWorker).Start();
    }

    private async void MessageCleanupThreadWorker()
    {
        while (true)
        {
            try
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
            }
            catch (Exception)
            {
            }

            Thread.Sleep(100);
        }
    }

    private async void RsCleanupThreadWorker()
    {
        while (true)
        {
            try
            {
                var now = DateTime.UtcNow;

                foreach (var guild in Discord.Guilds)
                {
                    var panel = StateService.Get<Rs.RsQueue>(guild.Id, "rs-queue");
                    var channel = guild.GetTextChannel(panel.ChannelId);
                    if (channel == null)
                        continue;

                    var repost = false;

                    foreach (var queue in panel.Queues)
                    {
                        var timeoutStateId = StateService.GetId("rs-queue-timeout", (ulong)queue.Level);
                        var timeoutMinutes = StateService.Get<int>(guild.Id, timeoutStateId);
                        if (timeoutMinutes == 0)
                            timeoutMinutes = 10;

                        foreach (var userId in queue.Users.ToList())
                        {
                            var userActivityStateId = "rs-queue-activity-" + userId.ToStr() + "-" + queue.Level.ToStr();
                            var userActivity = StateService.Get<DateTime>(guild.Id, userActivityStateId);
                            if (userActivity.Year == 0)
                                continue;

                            if (userActivity.AddMinutes(timeoutMinutes) < now)
                            {
                                var user = guild.GetUser(userId);
                                if (user != null)
                                {
                                    var userActivityConfirmationAskedStateId = userActivityStateId + "-asked";
                                    if (StateService.Exists(guild.Id, userActivityConfirmationAskedStateId))
                                    {
                                        var askedOn = StateService.Get<DateTime>(guild.Id, userActivityConfirmationAskedStateId);
                                        if (askedOn.AddMinutes(2) < now)
                                        {
                                            await Rs.RemoveFromQueue(guild, channel, queue.Level, user, null);
                                            repost = true;
                                            break; // skip the check and removal of other users until next cycle
                                        }

                                        continue;
                                    }

                                    StateService.Set(guild.Id, userActivityConfirmationAskedStateId, DateTime.UtcNow);

                                    var confirmTimeoutMinutes = 2;
                                    CleanupService.RegisterForDeletion(confirmTimeoutMinutes * 60,
                                        await channel.SendMessageAsync(":grey_question: " + user.Mention + ", still in for RS" + queue.Level.ToStr() + "? Type `" + CommandPrefix + "in " + queue.Level.ToStr() + "` to confirm within the next " + confirmTimeoutMinutes.ToStr() + " minutes."));
                                }
                            }
                        }
                    }

                    if (repost)
                    {
                        await Rs.RefreshQueueList(guild, channel, false);
                    }
                }
            }
            catch (Exception)
            {
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
        await Commands.AddModuleAsync(typeof(WsDraft), null);
        await Commands.AddModuleAsync(typeof(Trade), null);
        await Commands.AddModuleAsync(typeof(Wiki), null);
        await Commands.AddModuleAsync(typeof(Greeter), null);
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

            if (message.Source == MessageSource.Bot)
            {
                return;
            }

            if (message.Source != MessageSource.User)
                return;

            if (message.Channel is not SocketTextChannel textChannel)
                return;

            if (message.Author is not SocketGuildUser user)
                return;

            if (!textChannel.IsPubliclyAccessible())
            {
                if (user != null && message.MentionedUsers?.Count > 0)
                {
                    var afkList = await AfkLogic.GetAfkList(textChannel.Guild);
                    if (afkList != null)
                    {
                        foreach (var afk in afkList)
                        {
                            if (message.MentionedUsers.FirstOrDefault(x => x.Id == afk.UserId) is SocketGuildUser muser)
                            {
                                await textChannel.BotResponse(muser.DisplayName + " is AFK for " + afk.EndsOn.Subtract(DateTime.UtcNow).ToIntervalStr() + ".", ResponseType.info);
                            }
                        }
                    }
                }
            }

            var argPos = 0;

            if (!message.HasCharPrefix(CommandPrefix, ref argPos))
                return;

            if (message.Content[1] == ' ')
                argPos++;

            var context = new SocketCommandContext(Discord, message);
            var result = await Commands.ExecuteAsync(context, argPos, null);
            if (message.Channel is SocketTextChannel tc)
            {
                var eb = new EmbedBuilder()
                    .WithTitle("new command issued")
                    .WithDescription(message.Content)
                    .AddField("sender", user.DisplayName + " (" + user.Id.ToStr() + ")")
                    .AddField("channel", textChannel.Name)
                    .AddField("guild permissions", string.Join(", ", user.GuildPermissions.ToList()));
                await LogService.LogToChannel(tc.Guild, null, eb.Build());
            }
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
            CleanupService.RegisterForDeletion(30,
                await context.Channel.SendMessageAsync("Parameter count doesn't match."));
            await HelpCommandModule.ShowHelp(context.Channel as ISocketMessageChannel, command.Value.Name);
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
