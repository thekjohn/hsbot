namespace HsBot.Logic;

using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

public static class ModuleFilterLogic
{
    internal static async Task ListModuleFilters(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser)
    {
        var filters = GetAllModuleFilters(guild.Id).OrderBy(x => x.Name).ToList();

        var batchSize = 25;
        var batchCount = (filters.Count / batchSize) + (filters.Count % batchSize == 0 ? 0 : 1);
        for (var batch = 0; batch < batchCount; batch++)
        {
            var eb = new EmbedBuilder()
                .WithTitle("Module filters")
                .WithFooter("Use `" + DiscordBot.CommandPrefix + "mfadd` to register a new filter.")
                .WithColor(Color.Red);

            foreach (var filter in filters.Skip(batch * batchSize).Take(batchSize))
            {
                eb.AddField(filter.Name,
                    string.Join("\n", filter.Modules
                        .Select(x => CompendiumResponseMap.GetDisplayNameWithShort(x.Name) + " " + x.Level.ToStr() + "+"))
                    , true
                    );
            }

            await channel.SendMessageAsync(null, embed: eb.Build());
        }
    }

    internal static async Task CreateModuleFilter(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser currentUser, ModuleFilter filter)
    {
        StateService.Set(guild.Id, "module-filter-" + filter.Name, filter);
        await channel.BotResponse("Named filter `" + filter.Name + "`successfully saved: " + string.Join(", ", filter.Modules.Select(x => CompendiumResponseMap.GetDisplayNameWithShort(x.Name) + " " + x.Level.ToStr() + "+")), ResponseType.success);
    }

    public static ModuleFilter GetModuleFilter(ulong guildId, string name)
    {
        return StateService.Get<ModuleFilter>(guildId, "module-filter-" + name);
    }

    public static List<ModuleFilter> GetAllModuleFilters(ulong guildId)
    {
        return StateService.ListIds(guildId, "module-filter-")
            .Select(id => StateService.Get<ModuleFilter>(guildId, id))
            .ToList();
    }
}

public class ModuleFilter
{
    public string Name { get; set; }
    public List<ModuleFilterEntry> Modules { get; set; } = new();
}

public class ModuleFilterEntry
{
    public string Name { get; set; }
    public int Level { get; set; }
}
