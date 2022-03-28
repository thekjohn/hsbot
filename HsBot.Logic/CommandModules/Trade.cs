namespace HsBot.Logic;

[Summary("trade")]
[RequireContext(ContextType.Guild)]
[RequireMinimumAllianceRole(AllianceRole.Ally)]
public class Trade : BaseModule
{
    [Command("rates")]
    [Summary("rates|show trade info panel")]
    public async Task ShowRates(string code = null)
    {
        await CleanupService.DeleteCommand(Context.Message);
        await TradeLogic.ShowInfoPanel(Context.Guild, Context.Channel, CurrentUser);
    }

    [Command("add-seller")]
    [Summary("add-seller <userName> <rsLevel>|add seller to a specific RS level")]
    public async Task AddSeller(string userName, int rsLevel)
    {
        await CleanupService.DeleteCommand(Context.Message);
        var seller = Context.Guild.FindUser(CurrentUser, userName);
        if (seller == null)
        {
            await Context.Channel.BotResponse("Can't find user: " + userName + ".", ResponseType.error);
            return;
        }

        if (rsLevel < 4 || rsLevel > 12)
        {
            await Context.Channel.BotResponse("Level must be between 4 and 12.", ResponseType.error);
            return;
        }

        await TradeLogic.AddSeller(Context.Guild, Context.Channel, CurrentUser, rsLevel, seller);
    }

    [Command("add-rate")]
    [Summary("add-rate <sellerLevel> <buyerLevel> <blueRate> <orbRate> <tetraRate> <mixRate>|add rate to a specific seller and buyer level")]
    public async Task AddRate(int sellerLevel, int buyerLevel, double blueRate, double orbRate, double tetraRate, double mixRate)
    {
        await CleanupService.DeleteCommand(Context.Message);
        if (sellerLevel < 4 || sellerLevel > 12)
        {
            await Context.Channel.BotResponse("Seller level must be between 4 and 12.", ResponseType.error);
            return;
        }

        if (buyerLevel < 4 || buyerLevel > 12)
        {
            await Context.Channel.BotResponse("Buyer level must be between 4 and 12.", ResponseType.error);
            return;
        }

        await TradeLogic.AddRate(Context.Guild, Context.Channel, CurrentUser, sellerLevel, buyerLevel, blueRate, orbRate, tetraRate, mixRate);
    }
}
