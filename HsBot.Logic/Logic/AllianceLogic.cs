namespace HsBot.Logic
{
    using Discord.WebSocket;

    public static class AllianceLogic
    {
        public static AllianceInfo GetAlliance(ulong guildId)
        {
            return Services.State.Get<AllianceInfo>(guildId, "alliance")
                ?? new AllianceInfo();
        }

        public static void SaveAlliance(ulong guildId, AllianceInfo alliance)
        {
            Services.State.Set(guildId, "alliance", alliance);
        }

        public static bool IsMember(ulong guildId, SocketGuildUser user)
        {
            var alliance = AllianceLogic.GetAlliance(guildId);
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
            public string AllyIcon { get; set; }
            public ulong GreeterRoleId { get; set; }
            public ulong PublicChannelId { get; set; }
            public ulong GuestRoleId { get; set; }
            public ulong CompendiumRoleId { get; set; }
            public ulong AdmiralRoleId { get; set; }
            public ulong WsDraftChannelId { get; set; }
            public ulong WsAnnounceChannelId { get; set; }
            public string GuestIcon { get; set; } = ":bust_in_silhouette:";

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
}