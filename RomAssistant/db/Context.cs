using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.db
{
	public class Context : DbContext
	{
		public DbSet<User> Users => Set<User>();
        public DbSet<VoiceTrackerSession> VoiceTrackerSessions => Set<VoiceTrackerSession>();
        public DbSet<VoiceTrackerMember> VoiceTrackerMembers => Set<VoiceTrackerMember>();
        public DbSet<VoiceTrackerMessage> VoiceTrackerMessages => Set<VoiceTrackerMessage>();
        public DbSet<Raffle> Raffles => Set<Raffle>();
        public DbSet<RaffleAnswer> RaffleAnswers => Set<RaffleAnswer>();

        private IConfiguration Configuration;

        public Context(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                //.UseLoggerFactory(LoggerFactory.Create(builder => { builder.AddConsole(); }))
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
                .UseMySql(Configuration["DB"], ServerVersion.AutoDetect(Configuration["DB"]), options => options.EnableRetryOnFailure())
                ;

        }

    }
}
