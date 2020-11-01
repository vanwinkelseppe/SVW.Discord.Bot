using System;

namespace SVW.Discord.Domain
{
    public class Failure
    {
        public int Id { get; set; }
        public ulong UserId { get; set; }
        public DateTime DateTime { get; set; }
        public int HighestCount { get; set; }
    }
}