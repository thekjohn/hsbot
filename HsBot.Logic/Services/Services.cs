namespace HsBot.Logic
{
    // yes, service locator pattern, I don't care
    internal static class Services
    {
        public static StateService State { get; } = new StateService();
    }
}