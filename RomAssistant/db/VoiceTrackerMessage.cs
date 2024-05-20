using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.db;
public class VoiceTrackerMessage
{
    [Key]
    public int Id { get; set; }
    public int SessionId { get; set; }
    [ForeignKey(nameof(SessionId))]
    public VoiceTrackerSession Session { get; set; }
    public ulong DiscordMemberId { get; set; }
    public string Message { get; set; } = string.Empty;
    public long Time { get; set; } = DateTimeOffset.Now.ToUnixTimeSeconds();
}
