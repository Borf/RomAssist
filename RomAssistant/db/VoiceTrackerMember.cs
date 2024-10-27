using Microsoft.EntityFrameworkCore;
using RomAssistant.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.db;
[PrimaryKey(nameof(SessionId), nameof(Id))]
public class VoiceTrackerMember
{
    public int SessionId { get; set; }
    [ForeignKey(nameof(SessionId))]
    public VoiceTrackerSession Session { get; set; } = null!;
    public ulong Id { get; set; }
    public ulong Cid { get; set; }
    public Server Server { get; set; } = Server.Unknown;
    public long AccumulatedTime { get; set; }
    public long LastJoinTime { get; set; }
}
