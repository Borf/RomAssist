using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.db;
public class RaffleAnswer
{
    [Key]
    public int Id { get; set; }
    public ulong DiscordUserId { get; set; }
    public string Answer { get; set; } = string.Empty;
}
