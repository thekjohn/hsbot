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

        public static AllianceLogic.Corp FindCorp(this SocketGuild guild, AllianceLogic.AllianceInfo alliance, string corpToFind)
        {
            if (corpToFind == null || alliance == null)
                return null;

            var corp = alliance.Corporations.Find(x => string.Equals(x.FullName, corpToFind, StringComparison.InvariantCultureIgnoreCase));
            if (corp != null)
                return corp;

            corp = alliance.Corporations.Find(x => string.Equals(x.Abbreviation, corpToFind, StringComparison.InvariantCultureIgnoreCase));
            if (corp != null)
                return corp;

            corp = MentionUtils.TryParseRole(corpToFind, out var id)
                ? alliance.Corporations.Find(x => x.RoleId == id)
                : null;

            if (corp != null)
                return corp;

            corp = ulong.TryParse(corpToFind, out id)
                ? alliance.Corporations.Find(x => x.RoleId == id)
                : null;

            if (corp != null)
                return corp;

            var roles = guild.Roles.Where(x => x.Name.StartsWith(corpToFind, StringComparison.InvariantCultureIgnoreCase)).ToArray();
            if (roles.Length == 1)
                corp = alliance.Corporations.Find(x => x.RoleId == roles[0].Id);

            return corp;
        }

        public static SocketRole FindRole(this SocketGuild guild, string roleToFind)
        {
            if (roleToFind == null)
                return null;

            var role = guild.Roles.FirstOrDefault(x => string.Equals(x.Name, roleToFind, StringComparison.InvariantCultureIgnoreCase));

            if (role == null && MentionUtils.TryParseRole(roleToFind, out var id))
                role = guild.GetRole(id);

            if (role == null && ulong.TryParse(roleToFind, out id))
                role = guild.GetRole(id);

            if (role == null)
            {
                var roles = guild.Roles.Where(x => x.Name.StartsWith(roleToFind, StringComparison.InvariantCultureIgnoreCase)).ToArray();
                if (roles.Length == 1)
                    role = roles[0];
            }

            return role;
        }
    }
}