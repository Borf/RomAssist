using Google.Apis.Sheets.v4;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.Models;


class DiscordMessage
{
    [Key]
    public ulong MessageId { get; set; }
    public DateTimeOffset Date { get; set; }
    public ulong ChannelId { get; set; }
    public string ChannelName { get; set; }
    public ulong AuthorId { get; set; }
    public string Author { get; set; }
    public string Content { get; set; }
    public string Attachment0 { get; set; } = string.Empty;
    public string Attachment1 { get; set; } = string.Empty;
    public string Attachment2 { get; set; } = string.Empty;
    public string Attachment3 { get; set; } = string.Empty;
    public DateTimeOffset? EditedTimestamp { get; set; }
    public string Reactions { get; set; } = string.Empty;
    public bool MessageDeleted { get; set; } = false;
}

