using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
		public DbSet<QuizAnswer> QuizAnswers => Set<QuizAnswer>();
		public DbSet<Answer> Answers => Set<Answer>();
        public DbSet<VoiceTrackerSession> VoiceTrackerSessions => Set<VoiceTrackerSession>();
        public DbSet<VoiceTrackerMember> VoiceTrackerMembers => Set<VoiceTrackerMember>();
        public DbSet<VoiceTrackerMessage> VoiceTrackerMessages => Set<VoiceTrackerMessage>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
            //.UseLoggerFactory(LoggerFactory.Create(builder => { builder.AddConsole(); }))
            //.EnableSensitiveDataLogging()
            //.EnableDetailedErrors()
                .UseSqlite(new SqliteConnectionStringBuilder()
                {
                    DataSource = "Database.db",
                    DefaultTimeout = 60,
                    Cache = SqliteCacheMode.Shared,
                }.ToString());
        }

    }
}
