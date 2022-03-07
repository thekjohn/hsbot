namespace HsBot.Logic
{
    using Discord.WebSocket;

    public enum ResponseType { successStay, success, error, question, info, infoStay }

    internal static class ResponseService
    {
        public static async Task BotResponse(this ISocketMessageChannel channel, string message, ResponseType type)
        {
            switch (type)
            {
                case ResponseType.error:
                    CleanupService.RegisterForDeletion(5,
                        await channel.SendMessageAsync(":x: " + message));
                    break;
                case ResponseType.successStay:
                    await channel.SendMessageAsync(":white_check_mark: " + message);
                    break;
                case ResponseType.success:
                    CleanupService.RegisterForDeletion(5,
                        await channel.SendMessageAsync(":white_check_mark: " + message));
                    break;
                case ResponseType.info:
                    CleanupService.RegisterForDeletion(5,
                        await channel.SendMessageAsync(":information_source: " + message));
                    break;
                case ResponseType.infoStay:
                    await channel.SendMessageAsync(":information_source: " + message);
                    break;
                case ResponseType.question:
                    await channel.SendMessageAsync(":grey_question: " + message);
                    break;
            }
        }
    }
}