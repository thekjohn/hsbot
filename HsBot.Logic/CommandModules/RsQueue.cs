namespace HsBot.Logic
{
    using System.Collections.Generic;

    internal class RsQueue
    {
        public int Level { get; init; }
        public List<ulong> Users { get; init; } = new();
        public DateTime StartedOn { get; init; }
        public ulong MessageId { get; set; }
    }
}