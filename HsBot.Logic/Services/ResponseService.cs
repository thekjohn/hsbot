namespace HsBot.Logic;

using Discord;
using Discord.WebSocket;

public enum ResponseType { successStay, success, error, errorStay, question, info, infoStay }

internal static class ResponseService
{
    public static async Task BotResponse(this ISocketMessageChannel channel, string message, ResponseType type, Embed embed = null)
    {
        switch (type)
        {
            case ResponseType.errorStay:
                await channel.SendMessageAsync(":x: " + message, embed: embed);
                break;
            case ResponseType.error:
                CleanupService.RegisterForDeletion(10,
                    await channel.SendMessageAsync(":x: " + message, embed: embed));
                break;
            case ResponseType.successStay:
                await channel.SendMessageAsync(":white_check_mark: " + message, embed: embed);
                break;
            case ResponseType.success:
                CleanupService.RegisterForDeletion(10,
                    await channel.SendMessageAsync(":white_check_mark: " + message, embed: embed));
                break;
            case ResponseType.info:
                CleanupService.RegisterForDeletion(10,
                    await channel.SendMessageAsync(":information_source: " + message, embed: embed));
                break;
            case ResponseType.infoStay:
                await channel.SendMessageAsync(":information_source: " + message, embed: embed);
                break;
            case ResponseType.question:
                await channel.SendMessageAsync(":grey_question: " + message, embed: embed);
                break;
        }
    }
}
