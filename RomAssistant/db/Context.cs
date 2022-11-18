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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                //.UseLoggerFactory(LoggerFactory.Create(builder => { builder.AddConsole(); }))
                //.EnableSensitiveDataLogging()
                //.EnableDetailedErrors()
                .UseSqlite("Data Source=data/Database.db");
        }

    }
}
