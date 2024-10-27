using RomAssistant.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.db
{

	public class User
	{
		[Key]
		public ulong DiscordId { get; set; }
		public ulong CharacterId { get; set; } = 0;
		public Server Server { get; set; } = 0;
        public string CharacterName { get; set; } = "";
		public string Guild { get; set; } = "";
		public ulong AccountId { get; set; } = 0;
        public DateTimeOffset? LastCheckTime { get; set; } = null;
        public DateTimeOffset? LastUpdateTime { get; set; } = null;
    }
}
