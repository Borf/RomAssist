using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.db
{
	public enum Regions
	{
		None,
		EU_EL,
		NA_EL,
		NA_DP,
		SEA_EL,
		SEA_MP,
		SEA_MOF,
		SEA_VG
	}
	public class User
	{
		[Key]
		public ulong Id { get; set; }
		public string DiscordName { get; set; } = "";
		public ulong CharacterId { get; set; } = 0;
		public string CharacterName { get; set; } = "";
		public Regions Region { get; set; } = Regions.None;
	}
}
