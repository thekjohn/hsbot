namespace HsBot.Logic
{
    using Discord;

    internal class CleanupService
    {
        private readonly List<Entry> _messagesToDelete = new();

        public void RegisterForDeletion(int afterSeconds, IUserMessage message)
        {
            lock (_messagesToDelete)
            {
                _messagesToDelete.Add(new Entry()
                {
                    After = DateTime.UtcNow.AddSeconds(afterSeconds),
                    Message = message,
                });
            }
        }

        public List<IUserMessage> GetMessagesToDelete()
        {
            lock (_messagesToDelete)
            {
                var now = DateTime.UtcNow;

                List<IUserMessage> result = null;
                foreach (var entry in _messagesToDelete)
                {
                    if (now > entry.After)
                    {
                        (result ??= new()).Add(entry.Message);
                    }
                }

                if (result != null)
                    _messagesToDelete.RemoveAll(x => result.Contains(x.Message));

                return result;
            }
        }

        private class Entry
        {
            public DateTime After { get; init; }
            public IUserMessage Message { get; init; }
        }
    }
}