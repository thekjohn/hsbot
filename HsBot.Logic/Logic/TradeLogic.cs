namespace HsBot.Logic;

public static class TradeLogic
{
    internal static async Task AddSeller(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, int level, SocketGuildUser seller)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var info = StateService.Get<TradeInfo>(guild.Id, "trade-info") ?? new TradeInfo();
        var rsLevel = info.Levels.Find(x => x.Level == level);
        if (rsLevel == null)
        {
            rsLevel = new TradeRsInfo()
            {
                Level = level,
            };

            info.Levels.Add(rsLevel);
        }

        if (!rsLevel.Sellers.Contains(seller.Id))
        {
            rsLevel.Sellers.Add(seller.Id);
            await channel.BotResponse(seller.DisplayName + " is registered as RS" + level.ToStr() + " seller", ResponseType.success);
            StateService.Set(guild.Id, "trade-info", info);
        }
        else
        {
            await channel.BotResponse(seller.DisplayName + " is already registered as RS" + level.ToStr() + " seller", ResponseType.error);
        }
    }

    internal static async Task SetRate(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user, int sellerLevel, int buyerLevel, double b, double o, double t, double x, double ix)
    {
        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var info = StateService.Get<TradeInfo>(guild.Id, "trade-info") ?? new TradeInfo();
        var rsLevel = info.Levels.Find(x => x.Level == sellerLevel);
        if (rsLevel == null)
        {
            rsLevel = new TradeRsInfo()
            {
                Level = sellerLevel,
            };

            info.Levels.Add(rsLevel);
        }

        if (rsLevel.BRates == null)
            rsLevel.BRates = new double[12];

        if (rsLevel.ORates == null)
            rsLevel.ORates = new double[12];

        if (rsLevel.TRates == null)
            rsLevel.TRates = new double[12];

        if (rsLevel.XRates == null)
            rsLevel.XRates = new double[12];

        if (rsLevel.IXRates == null)
            rsLevel.IXRates = new double[12];

        if (rsLevel.BRates.Length < buyerLevel)
        {
            var r = rsLevel.BRates;
            Array.Resize(ref r, buyerLevel + 1);
            rsLevel.BRates = r;
        }

        rsLevel.BRates[buyerLevel] = b;

        if (rsLevel.ORates.Length < buyerLevel)
        {
            var r = rsLevel.ORates;
            Array.Resize(ref r, buyerLevel + 1);
            rsLevel.ORates = r;
        }

        rsLevel.ORates[buyerLevel] = o;

        if (rsLevel.TRates.Length < buyerLevel)
        {
            var r = rsLevel.TRates;
            Array.Resize(ref r, buyerLevel + 1);
            rsLevel.TRates = r;
        }

        rsLevel.TRates[buyerLevel] = t;

        if (rsLevel.XRates.Length < buyerLevel)
        {
            var r = rsLevel.XRates;
            Array.Resize(ref r, buyerLevel + 1);
            rsLevel.XRates = r;
        }

        rsLevel.XRates[buyerLevel] = x;

        if (rsLevel.IXRates.Length < buyerLevel)
        {
            var r = rsLevel.IXRates;
            Array.Resize(ref r, buyerLevel + 1);
            rsLevel.IXRates = r;
        }

        rsLevel.IXRates[buyerLevel] = ix;

        await channel.BotResponse("R" + buyerLevel.ToStr() + " -> R" + sellerLevel.ToStr() + " is registered rates registered.", ResponseType.success);
        StateService.Set(guild.Id, "trade-info", info);
    }

    internal static async Task ShowInfoPanel(SocketGuild guild, ISocketMessageChannel channel, SocketGuildUser user)
    {
        var info = StateService.Get<TradeInfo>(guild.Id, "trade-info");
        if (info == null)
            return;

        if (info.PanelMessageId != 0)
        {
            try
            {
                await guild.GetTextChannel(info.PanelChannelId).DeleteMessageAsync(info.PanelMessageId);
            }
            catch (Exception)
            {
            }
        }

        var alliance = AllianceLogic.GetAlliance(guild.Id);
        if (alliance == null)
            return;

        var eb = new EmbedBuilder()
            .WithTitle(alliance.Name + " trading");

        var sb = new StringBuilder();

        foreach (var rsInfo in info.Levels.OrderByDescending(x => x.Level))
        {
            sb.Clear()
                .Append("sellers: ")
                .AppendJoin(" ", rsInfo.Sellers
                    .Select(x => guild.GetUser(x))
                    .Where(x => x != null)
                    .OrderBy(x => x.GetShortDisplayName())
                    .Select(x => alliance.GetUserCorpIcon(x) + x.GetShortDisplayName()))
                .AppendLine();

            var hasRates = false;
            for (var i = 12; i >= 1; i--)
            {
                var b = rsInfo.BRates != null && i < rsInfo.BRates.Length ? rsInfo.BRates[i] : 0.0d;
                var o = rsInfo.ORates != null && i < rsInfo.ORates.Length ? rsInfo.ORates[i] : 0.0d;
                var t = rsInfo.TRates != null && i < rsInfo.TRates.Length ? rsInfo.TRates[i] : 0.0d;
                var x = rsInfo.XRates != null && i < rsInfo.XRates.Length ? rsInfo.XRates[i] : 0.0d;
                var ix = rsInfo.IXRates != null && i < rsInfo.IXRates.Length ? rsInfo.IXRates[i] : 0.0d;
                if (b != 0 || o != 0 || t != 0 || x != 0 || ix != 0)
                {
                    if (!hasRates)
                    {
                        sb
                            .Append("```")
                            .Append("ARTIFACT |BLUE| ORB| TET| MIX| ")
                            .AppendLine(alliance.Abbreviation);
                        hasRates = true;
                    }

                    sb
                        .Append(("R" + i.ToStr()).PadRight(9))
                        .Append((b != 0 ? b.ToString("F1", CultureInfo.InvariantCulture) : "-").PadLeft(5))
                        .Append((o != 0 ? o.ToString("F1", CultureInfo.InvariantCulture) : "-").PadLeft(5))
                        .Append((t != 0 ? t.ToString("F1", CultureInfo.InvariantCulture) : "-").PadLeft(5))
                        .Append((x != 0 ? x.ToString("F1", CultureInfo.InvariantCulture) : "-").PadLeft(5))
                        .AppendLine((ix != 0 ? ix.ToString("F1", CultureInfo.InvariantCulture) : "-").PadLeft(5));
                }
            }

            if (hasRates)
                sb.Append("```");

            eb.AddField("level " + rsInfo.Level + " artifacts", sb.ToString());
        }

        var sent = await channel.SendMessageAsync(null, embed: eb.Build());
        info.PanelChannelId = channel.Id;
        info.PanelMessageId = sent.Id;

        StateService.Set(guild.Id, "trade-info", info);
    }

    public class TradeInfo
    {
        public List<TradeRsInfo> Levels { get; set; } = new();
        public ulong PanelChannelId { get; set; }
        public ulong PanelMessageId { get; set; }
    }

    public class TradeRsInfo
    {
        public int Level { get; set; }
        public List<ulong> Sellers { get; set; } = new();
        public double[] BRates { get; set; } = new double[12];
        public double[] ORates { get; set; } = new double[12];
        public double[] TRates { get; set; } = new double[12];
        public double[] XRates { get; set; } = new double[12];
        public double[] IXRates { get; set; } = new double[12];
    }
}
