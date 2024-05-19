using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.db;
public class VoiceTrackerSession
{
    [Key]
    public int Id { get; set; }
    public ulong Channel { get; set; }
    public bool Active { get; set; } = false;
    public string Title { get; set; } = string.Empty;
    public long StartTime { get; set; } = DateTimeOffset.Now.ToUnixTimeSeconds();
    public string TriggerWord { get; set; } = string.Empty;
    public List<VoiceTrackerMember> Members { get; set; } = new();
}
