namespace HsBot.Logic
{
    // yes, service locator pattern, I don't care
    internal static class Services
    {
        public static LogService Log { get; } = new LogService();
        public static StateService State { get; } = new StateService();
        public static CleanupService Cleanup { get; } = new CleanupService();
    }
}