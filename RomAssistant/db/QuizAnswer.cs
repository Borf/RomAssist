using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.db
{
	public class QuizAnswer
	{
		[Key]
		public int Id { get; set; }

		public DateTime DateTime { get; set; } = DateTime.Now;
		public string Answer { get; set; } = "";
		public User User { get; set; } = null!;
		public string Answered { get; set; } = "";
	}
}
