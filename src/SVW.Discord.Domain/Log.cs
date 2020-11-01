using System;

namespace SVW.Discord.Domain
{
    public class Log
    {
        public int Id { get; set; }
        public ulong UserId { get; set; }
        public ulong MessageId { get; set; }
        public string Message { get; set; }
        public bool WasValid { get; set; }
        public int Number { get; set; }

        public DateTime DateTime { get; set; }
    }
}