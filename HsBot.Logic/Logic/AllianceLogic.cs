namespace HsBot.Logic;

public enum AllianceRole { Leader, Officer, Greeter, Admiral, Member, WSGuest, Ally, Guest }

public static class AllianceLogic
{
    public static AllianceInfo GetAlliance(ulong guildId)
    {
        return StateService.Get<AllianceInfo>(guildId, "alliance")
            ?? new AllianceInfo();
    }

    public static void SaveAlliance(ulong guildId, AllianceInfo alliance)
    {
        StateService.Set(guildId, "alliance", alliance);
    }

    public static bool IsMember(ulong guildId, SocketGuildUser user)
    {
        var alliance = GetAlliance(guildId);
        if (alliance == null)
            return false;

        return user.Roles.Any(x => x.Id == alliance.RoleId);
    }

    public class AllianceInfo
    {
        public ulong RoleId { get; set; }
        public string Abbreviation { get; set; }
        public string Name { get; set; }

        public List<Corp> Corporations { get; set; } = new List<Corp>();
        public List<Alt> Alts { get; set; } = new List<Alt>();

        public ulong AllyRoleId { get; set; }
        public ulong WsGuestRoleId { get; set; }
        public string AllyIcon { get; set; }
        public ulong GreeterRoleId { get; set; }
        public ulong PublicChannelId { get; set; }
        public ulong GuestRoleId { get; set; }
        public ulong CompendiumRoleId { get; set; }
        public ulong AdmiralRoleId { get; set; }
        public ulong WsSignupChannelId { get; set; }
        public ulong WsDraftChannelId { get; set; }
        public ulong WsAnnounceChannelId { get; set; }
        public ulong RsEventAnnounceChannelId { get; set; }
        public ulong RsEventLogChannelId { get; set; }
        public string GuestIcon { get; set; } = ":bust_in_silhouette:";
        public ulong LeaderRoleId { get; set; }
        public ulong OfficerRoleId { get; set; }

        public string CompendiumApiKey { get; set; }

        public ulong GetAllianceRoleId(AllianceRole role)
        {
            return role switch
            {
                AllianceRole.Leader => LeaderRoleId,
                AllianceRole.Officer => OfficerRoleId,
                AllianceRole.Greeter => GreeterRoleId,
                AllianceRole.Admiral => AdmiralRoleId,
                AllianceRole.Member => RoleId,
                AllianceRole.WSGuest => WsGuestRoleId,
                AllianceRole.Ally => AllyRoleId,
                AllianceRole.Guest => GuestRoleId,
                _ => 0UL,
            };
        }

        public string GetUserCorpIcon(SocketGuildUser user, bool extraSpace = true, bool corpName = false)
        {
            var corp = Corporations.Find(c => user.Roles.Any(r => r.Id == c.RoleId));
            if (corp != null && !string.IsNullOrEmpty(corp.IconMention))
            {
                return corp.IconMention
                    + (corpName ? " " + corp.FullName : "")
                    + (extraSpace ? " " : "");
            }

            if (AllyIcon != null && user.Roles.Any(r => r.Id == AllyRoleId))
            {
                return AllyIcon
                    + (corpName ? " ally" : "")
                    + (extraSpace ? " " : "");
            }

            if (GuestIcon != null && user.Roles.Any(r => r.Id == GuestRoleId))
            {
                return GuestIcon
                    + (corpName ? " guest" : "")
                    + (extraSpace ? " " : "");
            }

            return null;
        }

        public string GetUserCorpName(SocketGuildUser user, bool abbreviation)
        {
            var corp = Corporations.Find(c => user.Roles.Any(r => r.Id == c.RoleId));
            if (corp != null && !string.IsNullOrEmpty(corp.IconMention))
                return abbreviation ? corp.Abbreviation : corp.FullName;

            if (user.Roles.Any(r => r.Id == AllyRoleId))
                return "ally";

            if (user.Roles.Any(r => r.Id == GuestRoleId))
                return "guest";

            return null;
        }

        public Alt FindAlt(string name)
        {
            if (name == null)
                return null;

            var alt = Alts.Find(x => string.Equals(x.Name, name, StringComparison.InvariantCultureIgnoreCase));
            if (alt == null)
            {
                var alts = Alts
                    .Where(x => x.Name?
                        .Replace(".", "", StringComparison.InvariantCultureIgnoreCase)
                        .StartsWith(name, StringComparison.InvariantCultureIgnoreCase) == true)
                    .ToArray();

                if (alts.Length == 1)
                    alt = alts[0];
            }

            if (alt == null)
            {
                var alts = Alts
                    .Where(x => x.Name?
                        .Replace(".", "", StringComparison.InvariantCultureIgnoreCase)
                        .Contains(name, StringComparison.InvariantCultureIgnoreCase) == true)
                    .ToArray();

                if (alts.Length == 1)
                    alt = alts[0];
            }

            return alt;
        }
    }

    public class Corp
    {
        public ulong RoleId { get; set; }
        public string IconMention { get; set; }
        public string FullName { get; set; }
        public string Abbreviation { get; set; }
        public int CurrentRelicCount { get; set; }
    }

    public class Alt
    {
        public ulong OwnerUserId { get; set; }
        public string Name { get; set; }
        public ulong? AltUserId { get; set; }

        public bool Equals(Alt alt)
        {
            return
                (AltUserId != null && alt.AltUserId != null && AltUserId.Value == alt.AltUserId.Value)
                || (AltUserId == null && alt.AltUserId == null && string.Equals(Name, alt.Name, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
