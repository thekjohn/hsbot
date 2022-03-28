﻿namespace HsBot.Logic;

internal static class SocketGuildUserExtensions
{
    public static string GetShortDisplayName(this SocketGuildUser user)
    {
        var dn = user.DisplayName;
        var idx1 = dn.IndexOf("[");
        if (idx1 == 0)
        {
            var idx2 = dn.IndexOf("]");
            if (idx2 > -1)
            {
                dn = dn[(idx2 + 1)..].Trim();
            }
        }

        return dn;
    }

    public static IEnumerable<SocketRole> GetRsRolesDescending(this SocketGuildUser user)
    {
        return user.Roles
            .Where(x =>
                x.Name.StartsWith("RS", StringComparison.InvariantCultureIgnoreCase)
                && int.TryParse(x.Name.Replace("RS", string.Empty, StringComparison.InvariantCultureIgnoreCase), out var num))
            .OrderByDescending(x => int.Parse(x.Name.Replace("RS", string.Empty, StringComparison.InvariantCultureIgnoreCase)));
    }

    public static int? GetHighestRsRoleNumber(this SocketGuildUser user)
    {
        var role = user.GetRsRolesDescending()
            .FirstOrDefault();

        return role != null
            ? int.Parse(role.Name.Replace("RS", string.Empty, StringComparison.InvariantCultureIgnoreCase))
            : null;
    }
}
