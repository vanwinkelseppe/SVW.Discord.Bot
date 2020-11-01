using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SVW.Discord.Domain;

namespace SVW.Discord.Data
{
    public class HighscoreDatabaseContext : DbContext, IHighscoreContext
    {
        public HighscoreDatabaseContext(DbContextOptions<HighscoreDatabaseContext> options)
            : base(options)
        {

        }

        public HighscoreDatabaseContext()
            : base(options: new DbContextOptionsBuilder().UseSqlite(new SqliteConnectionStringBuilder
                {
                    DataSource = "Discord.db"
                }.ToString()
            ).Options)
        {


        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Failure>().HasKey(_ => _.Id);
            modelBuilder.Entity<NumberCount>().HasKey(_ => _.Id);
            modelBuilder.Entity<Log>().HasKey(_ => _.Id);
            base.OnModelCreating(modelBuilder);
        }

        public DbSet<Failure> Failures { get; set; }
        public DbSet<NumberCount> NumberCounts { get; set; }
        public DbSet<Log> Logs { get; set; }




        public async Task<Log> LastMessage()
        {
            if (await Logs.AnyAsync())
            {
                return await Logs.OrderByDescending(_ => _.Id).FirstAsync();
            }

            return null;
        }
    }
}