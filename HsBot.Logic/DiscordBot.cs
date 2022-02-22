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

        private static Thread cleanupThread;

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
            cleanupThread = new Thread(CleanupThreadWorker);
            cleanupThread.Start();
        }

        private async void CleanupThreadWorker()
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
            await Commands.AddModuleAsync(typeof(Alliance), null);

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