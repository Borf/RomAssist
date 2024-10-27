using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.db;
public class Raffle
{
    [Key]
    public int Id { get; set; }
    public ulong DiscordServerId { get; set; }
    public ulong DiscordChannelid { get; set; }
    public ulong DiscordMessageId { get; set; }
    public RaffleType Type { get; set; }
    public string AnswerRegex { get; set; } = string.Empty;
    public int RaffleCount { get; set; }
}

public enum RaffleType
{
    Answer,
    Picture
}