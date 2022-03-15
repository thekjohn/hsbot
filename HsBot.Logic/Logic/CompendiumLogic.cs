namespace HsBot.Logic
{
    using System.Text.Json;
    using Discord.WebSocket;

    public static class CompendiumLogic
    {
        internal static async Task SetCompendiumApiKey(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, string apiKey)
        {
            var alliance = AllianceLogic.GetAlliance(guild.Id);
            if (alliance == null)
                return;

            alliance.CompendiumApiKey = apiKey;
            AllianceLogic.SaveAlliance(guild.Id, alliance);
        }

        internal static async void ImportThreadWorker(object obj)
        {
            Thread.Sleep(5 * 60 * 1000); // prevent downloading while debugging
            while (true)
            {
                foreach (var guild in DiscordBot.Discord.Guilds)
                {
                    var alliance = AllianceLogic.GetAlliance(guild.Id);
                    if (alliance == null)
                        return;

                    var client = new HttpClient();
                    var users = guild.Users
                        .Where(x => x.Roles.Any(y => y.Id == alliance.CompendiumRoleId)
                                    && x.Roles.Any(y => y.Id == alliance.RoleId || y.Id == alliance.WsGuestRoleId))
                        .ToList();

                    await LogService.LogToChannel(guild, "downloading compendium data for " + users.Count.ToStr() + " users.", null);

                    foreach (var guildUser in users)
                    {
                        var url = "https://bot.hs-compendium.com/compendium/api/tech?token=" + alliance.CompendiumApiKey + "&userid=" + guildUser.Id;
                        try
                        {
                            var result = await client.GetStringAsync(url);
                            var response = JsonSerializer.Deserialize<CompendiumResponse>(result);
                            StateService.Set(guild.Id, "compendium-" + guildUser.Id.ToStr(), response);
                            await LogService.LogToChannel(guild, "compendium data successfully downloaded from " + url, null);
                            Thread.Sleep(12 * 1000);
                        }
                        catch (HttpRequestException ex)
                        {
                            await LogService.LogToChannel(guild, "downloading compendium data failed with [" + ex.StatusCode.ToString() + "] from " + url, null);
                            if (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                Thread.Sleep(5 * 60 * 1000);
                            }
                        }
                    }
                }

                Thread.Sleep(60 * 60 * 1000);
            }
        }

        internal static CompendiumResponse GetUserData(ulong guildId, ulong userId)
        {
            return StateService.Get<CompendiumResponse>(guildId, "compendium-" + userId.ToStr());
        }
    }

    public class CompendiumResponse
    {
        public CompendiumResponseMap map { get; set; }
        public CompendiumResponseModuleWithName[] array { get; set; }
    }

    public class CompendiumResponseMap
    {
        public CompendiumResponseModule rs { get; set; }
        public CompendiumResponseModule shipmentrelay { get; set; }
        public CompendiumResponseModule corplevel { get; set; }
        public CompendiumResponseModule transp { get; set; }
        public CompendiumResponseModule miner { get; set; }
        public CompendiumResponseModule bs { get; set; }
        public CompendiumResponseModule cargobay { get; set; }
        public CompendiumResponseModule computer { get; set; }
        public CompendiumResponseModule tradeboost { get; set; }
        public CompendiumResponseModule rush { get; set; }
        public CompendiumResponseModule dispatch { get; set; }
        public CompendiumResponseModule relicdrone { get; set; }
        public CompendiumResponseModule miningboost { get; set; }
        public CompendiumResponseModule hydrobay { get; set; }
        public CompendiumResponseModule enrich { get; set; }
        public CompendiumResponseModule remote { get; set; }
        public CompendiumResponseModule hydroupload { get; set; }
        public CompendiumResponseModule miningunity { get; set; }
        public CompendiumResponseModule crunch { get; set; }
        public CompendiumResponseModule genesis { get; set; }
        public CompendiumResponseModule battery { get; set; }
        public CompendiumResponseModule laser { get; set; }
        public CompendiumResponseModule mass { get; set; }
        public CompendiumResponseModule dual { get; set; }
        public CompendiumResponseModule barrage { get; set; }
        public CompendiumResponseModule dart { get; set; }
        public CompendiumResponseModule alpha { get; set; }
        public CompendiumResponseModule delta { get; set; }
        public CompendiumResponseModule passive { get; set; }
        public CompendiumResponseModule omega { get; set; }
        public CompendiumResponseModule blast { get; set; }
        public CompendiumResponseModule area { get; set; }
        public CompendiumResponseModule emp { get; set; }
        public CompendiumResponseModule teleport { get; set; }
        public CompendiumResponseModule rsextender { get; set; }
        public CompendiumResponseModule repair { get; set; }
        public CompendiumResponseModule warp { get; set; }
        public CompendiumResponseModule unity { get; set; }
        public CompendiumResponseModule sanctuary { get; set; }
        public CompendiumResponseModule stealth { get; set; }
        public CompendiumResponseModule fortify { get; set; }
        public CompendiumResponseModule impulse { get; set; }
        public CompendiumResponseModule rocket { get; set; }
        public CompendiumResponseModule salvage { get; set; }
        public CompendiumResponseModule suppress { get; set; }
        public CompendiumResponseModule destiny { get; set; }
        public CompendiumResponseModule barrier { get; set; }
        public CompendiumResponseModule leap { get; set; }
        public CompendiumResponseModule bond { get; set; }
        public CompendiumResponseModule laserturret { get; set; }
    }

    public class CompendiumResponseModule
    {
        public int level { get; set; }
        public int ws { get; set; }
    }

    public class CompendiumResponseModuleWithName : CompendiumResponseModule
    {
        public string type { get; set; }
    }
}