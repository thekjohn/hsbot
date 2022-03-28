namespace HsBot.Logic;

using Discord;

internal static class EmoteExtensions
{
    public static string GetReference(this GuildEmote emote)
    {
        return "<:" + emote.Name + ":" + emote.Id.ToStr() + ">";
    }
}
