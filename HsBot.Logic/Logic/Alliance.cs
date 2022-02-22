namespace HsBot.Logic
{
    using Discord.WebSocket;

    public static class Alliance
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

        public class AllianceInfo
        {
            public ulong RoleId { get; set; }
            public string Abbreviation { get; set; }
            public string Name { get; set; }

            public List<Corp> Corporations { get; set; } = new List<Corp>();

            public ulong AllyRoleId { get; set; }
            public string AllyIcon { get; set; }

            public string GetUserCorpIcon(SocketGuildUser user)
            {
                var corp = Corporations.Find(c => user.Roles.Any(r => r.Id == c.RoleId));
                if (corp != null && !string.IsNullOrEmpty(corp.IconMention))
                    return corp.IconMention;

                if (user.Roles.Any(r => r.Id == AllyRoleId))
                    return AllyIcon;

                return null;
            }
        }

        public class Corp
        {
            public ulong RoleId { get; set; }
            public string IconMention { get; set; }
            public string FullName { get; set; }
            public string Abbreviation { get; set; }
            public int CurrentLevel { get; set; }
            public int CurrentRelicCount { get; set; }
        }
    }
}