using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.db;
public class RaffleAnswer
{
    [Key]
    public int Id { get; set; }
    public int RaffleId { get; set; }
    [ForeignKey(nameof(RaffleId))]
    public Raffle Raffle { get; set; } = null!;
    public ulong DiscordUserId { get; set; }
    [ForeignKey(nameof(DiscordUserId))]
    public User User { get; set; } = null!;
    public string Answer { get; set; } = string.Empty;
    public long Timestamp { get; set; }
}
