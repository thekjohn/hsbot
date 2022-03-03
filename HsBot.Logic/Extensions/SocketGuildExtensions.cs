namespace HsBot.Logic
{
    using Discord;
    using Discord.WebSocket;

    internal static class SocketGuildExtensions
    {
        public static SocketGuildUser FindUser(this SocketGuild guild, SocketGuildUser currentUser, string userToFind)
        {
            if (userToFind == null)
                return null;

            if (string.Equals((userToFind ?? "").Trim(), "me", StringComparison.InvariantCultureIgnoreCase))
                return currentUser;

            var user = guild.Users.FirstOrDefault(x => string.Equals(x.DisplayName, userToFind, StringComparison.InvariantCultureIgnoreCase));

            if (user == null && MentionUtils.TryParseUser(userToFind, out var id))
                user = guild.GetUser(id);

            if (user == null && ulong.TryParse(userToFind, out id))
                user = guild.GetUser(id);

            if (user == null)
            {
                var users = guild.Users.Where(x => x.DisplayName.StartsWith(userToFind, StringComparison.InvariantCultureIgnoreCase)).ToArray();
                if (users.Length == 1)
                    user = users[0];
            }

            return user;
        }
    }
}