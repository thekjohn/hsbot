namespace HsBot.Logic
{
    using Discord.WebSocket;

    internal static class SocketGuildUserExtensions
    {
        public static IEnumerable<SocketRole> GetRsRolesDescending(this SocketGuildUser user)
        {
            return user.Roles
                .Where(x =>
                    x.Name.StartsWith("RS", StringComparison.InvariantCultureIgnoreCase)
                    && int.TryParse(x.Name.Replace("RS", string.Empty), out var num))
                .OrderByDescending(x => int.Parse(x.Name.Replace("RS", string.Empty)));
        }

        public static int? GetHighestRsRoleNumber(this SocketGuildUser user)
        {
            var role = user.GetRsRolesDescending()
                .FirstOrDefault();

            return role != null
                ? int.Parse(role.Name.Replace("RS", string.Empty))
                : null;
        }
    }
}