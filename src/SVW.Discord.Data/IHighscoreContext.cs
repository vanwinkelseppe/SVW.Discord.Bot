using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SVW.Discord.Domain;

namespace SVW.Discord.Data
{
    public interface IHighscoreContext
    {
        DbSet<Failure> Failures { get; set; }
        DbSet<NumberCount> NumberCounts { get; set; }
        DbSet<Log> Logs { get; set; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
        Task<Log> LastMessage();
    }
}