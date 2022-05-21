namespace HsBot.Logic;

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
            try
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
                            if (response.array?.Length >= 5)
                            {
                                StateService.Set(guild.Id, "compendium-" + guildUser.Id.ToStr(), response);
                            }

                            await LogService.LogToChannel(guild, "compendium data successfully downloaded from " + url, null);
                            Thread.Sleep(15 * 1000);
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
            }
            catch (Exception)
            {
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

    public bool TestFilter(ModuleFilter filter, out int score)
    {
        score = 0;

        if (filter == null)
            return true;

        if (map == null)
            return false;

        if ((array?.Length ?? 0) < 5)
            return false;

        var mapTypeProperties = typeof(CompendiumResponseMap).GetProperties();
        foreach (var filterModule in filter.Modules)
        {
            var property = Array.Find(mapTypeProperties, p => string.Equals(p.Name, filterModule.Name, StringComparison.InvariantCultureIgnoreCase));
            if (property == null)
                continue;

            var value = (CompendiumResponseModule)property.GetValue(map);
            var level = value?.level ?? 0;

            if (level < filterModule.Level)
                return false;

            score += value?.ws ?? 0;
        }

        return true;
    }

    public string GetClassification(ModuleFilter filter)
    {
        if (map == null)
            return null;

        var needShortNames = filter.Modules.Count > 1;
        var twoCharShortNames = false;
        if (needShortNames)
        {
            var shortNames = filter.Modules.Select(m => CompendiumResponseMap.GetShortName(m.Name)[0]).ToArray();
            twoCharShortNames = shortNames.Distinct().Count() != shortNames.Length;
        }

        var modLevels = string.Join('/', filter.Modules.Select(m =>
        {
            var property = CompendiumResponseMap.GetByName(m.Name);
            var level = (property?.GetValue(map) as CompendiumResponseModule)?.level ?? 0;

            var shortName = "";
            if (needShortNames)
            {
                shortName = CompendiumResponseMap.GetShortName(m.Name);
                if (shortName.Length > 0)
                    shortName = shortName[..(twoCharShortNames ? 2 : 1)];
            }

            return shortName + level.ToEmptyStr();
        }));

        return filter.Name + " (" + modLevels + ")";
    }
}

public class CompendiumResponseMap
{
    public CompendiumResponseModule rs { get; set; }
    public CompendiumResponseModule shipmentrelay { get; set; }
    public CompendiumResponseModule corplevel { get; set; }
    [ModuleName("ts", "ts")]
    public CompendiumResponseModule transp { get; set; }
    [ModuleName("ms", "ms")]
    public CompendiumResponseModule miner { get; set; }
    [ModuleName("bs", "bs")]
    public CompendiumResponseModule bs { get; set; }
    [ModuleName("cargobay", "cbe")]
    public CompendiumResponseModule cargobay { get; set; }
    public CompendiumResponseModule computer { get; set; }
    public CompendiumResponseModule tradeboost { get; set; }
    public CompendiumResponseModule rush { get; set; }
    public CompendiumResponseModule tradeburst { get; set; }
    public CompendiumResponseModule offload { get; set; }
    public CompendiumResponseModule beam { get; set; }
    public CompendiumResponseModule entrust { get; set; }
    public CompendiumResponseModule recall { get; set; }
    public CompendiumResponseModule shipdrone { get; set; }
    public CompendiumResponseModule dispatch { get; set; }
    [ModuleName("relicdrone", "rdr")]
    public CompendiumResponseModule relicdrone { get; set; }
    [ModuleName("miningboost", "mbst")]
    public CompendiumResponseModule miningboost { get; set; }
    [ModuleName("hydrobay", "hbe")]
    public CompendiumResponseModule hydrobay { get; set; }
    [ModuleName("enrich", "enr")]
    public CompendiumResponseModule enrich { get; set; }
    [ModuleName("remotemining", "rmin")]
    public CompendiumResponseModule remote { get; set; }
    [ModuleName("hydroupload", "upl")]
    public CompendiumResponseModule hydroupload { get; set; }
    [ModuleName("miningunity", "uni")]
    public CompendiumResponseModule miningunity { get; set; }
    [ModuleName("crunch", "cru")]
    public CompendiumResponseModule crunch { get; set; }
    [ModuleName("genesis", "gen")]
    public CompendiumResponseModule genesis { get; set; }
    [ModuleName("battery", "BATT")]
    public CompendiumResponseModule battery { get; set; }
    [ModuleName("laser", "LSR")]
    public CompendiumResponseModule laser { get; set; }
    [ModuleName("mbatt", "MBATT")]
    public CompendiumResponseModule mass { get; set; }
    [ModuleName("duallaser", "DL")]
    public CompendiumResponseModule dual { get; set; }
    [ModuleName("barrage", "BRGE")]
    public CompendiumResponseModule barrage { get; set; }
    [ModuleName("dart", "DART")]
    public CompendiumResponseModule dart { get; set; }
    [ModuleName("alphashield", "aSh")]
    public CompendiumResponseModule alpha { get; set; } // SHIELD
    [ModuleName("deltashield", "dSh")]
    public CompendiumResponseModule delta { get; set; } // SHIELD
    [ModuleName("passiveshield", "pSh")]
    public CompendiumResponseModule passive { get; set; }
    [ModuleName("omegashield", "oSh")]
    public CompendiumResponseModule omega { get; set; } // SHIELD
    [ModuleName("blastshield", "bSh")]
    public CompendiumResponseModule blast { get; set; }
    [ModuleName("areashield", "aSh")]
    public CompendiumResponseModule area { get; set; }
    public CompendiumResponseModule emp { get; set; }
    [ModuleName("teleport", "tele")]
    public CompendiumResponseModule teleport { get; set; }
    [ModuleName("rse", "rse")]
    public CompendiumResponseModule rsextender { get; set; }
    [ModuleName("remoterepair", "rep")]
    public CompendiumResponseModule repair { get; set; }
    [ModuleName("tw", "tw")]
    public CompendiumResponseModule warp { get; set; }
    [ModuleName("unity", "uni")]
    public CompendiumResponseModule unity { get; set; }
    [ModuleName("sanc", "sanc")]
    public CompendiumResponseModule sanctuary { get; set; }
    public CompendiumResponseModule stealth { get; set; }
    [ModuleName("fortify", "fort")]
    public CompendiumResponseModule fortify { get; set; }
    [ModuleName("impulse", "imp")]
    public CompendiumResponseModule impulse { get; set; }
    [ModuleName("alpharocket", "aR")]
    public CompendiumResponseModule rocket { get; set; }
    public CompendiumResponseModule salvage { get; set; }
    [ModuleName("suppress", "sup")]
    public CompendiumResponseModule suppress { get; set; }
    [ModuleName("destiny", "dest")]
    public CompendiumResponseModule destiny { get; set; }
    [ModuleName("barrier", "barr")]
    public CompendiumResponseModule barrier { get; set; }
    [ModuleName("vengeance", "veng")]
    public CompendiumResponseModule vengeance { get; set; }
    [ModuleName("deltarocket", "dR")]
    public CompendiumResponseModule deltarocket { get; set; }
    public CompendiumResponseModule leap { get; set; }
    public CompendiumResponseModule bond { get; set; }
    [ModuleName("alphadrone", "aDrone")]
    public CompendiumResponseModule alphadrone { get; set; }
    [ModuleName("omegarocket", "oR")]
    public CompendiumResponseModule omegarocket { get; set; }
    public CompendiumResponseModule suspend { get; set; }
    [ModuleName("remotebomb", "rBomb")]
    public CompendiumResponseModule remotebomb { get; set; }
    [ModuleName("laserturret", "turr")]
    public CompendiumResponseModule laserturret { get; set; }

    public static PropertyInfo Find(string searchName)
    {
        return Array.Find(typeof(CompendiumResponseMap).GetProperties(), x =>
        {
            if (x.GetCustomAttributes(typeof(ModuleNameAttribute), false).FirstOrDefault() is ModuleNameAttribute attrib)
            {
                return string.Equals(attrib.DisplayName, searchName, StringComparison.InvariantCultureIgnoreCase)
                || string.Equals(attrib.ShortName, searchName, StringComparison.InvariantCultureIgnoreCase);
            }

            return string.Equals(x.Name, searchName, StringComparison.InvariantCultureIgnoreCase);
        });
    }

    public static PropertyInfo GetByName(string name)
    {
        return Array.Find(typeof(CompendiumResponseMap).GetProperties(), x => string.Equals(x.Name, name, StringComparison.InvariantCultureIgnoreCase));
    }

    public static string GetShortName(string name)
    {
        if (name == "mscap")
            return "m.cap";

        if (name == "mscaphbe")
            return "m.cap+";

        var property = GetByName(name);
        if (property == null)
            return null;

        if (property.GetCustomAttributes(typeof(ModuleNameAttribute), false).FirstOrDefault() is ModuleNameAttribute attrib)
            return attrib.ShortName;

        return property.Name;
    }

    public static string GetDisplayName(string name)
    {
        var property = GetByName(name);
        if (property == null)
            return null;

        if (property.GetCustomAttributes(typeof(ModuleNameAttribute), false).FirstOrDefault() is ModuleNameAttribute attrib)
            return attrib.DisplayName;

        return property.Name;
    }

    public static string GetDisplayNameWithShort(string name)
    {
        var property = GetByName(name);
        if (property == null)
            return null;

        if (property.GetCustomAttributes(typeof(ModuleNameAttribute), false).FirstOrDefault() is ModuleNameAttribute attrib)
        {
            return (attrib.ShortName != attrib.DisplayName ? attrib.ShortName + "/" : "")
                + attrib.DisplayName;
        }

        return property.Name;
    }
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

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class ModuleNameAttribute : Attribute
{
    public string DisplayName { get; }
    public string ShortName { get; }

    public ModuleNameAttribute(string displayName, string shortName)
    {
        DisplayName = displayName;
        ShortName = shortName;
    }
}
