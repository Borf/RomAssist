using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.db;
public class Answer
{
    [Key]
    public int Id { get; set; }
    [ForeignKey(nameof(User))]
    public ulong UserId { get; set; }
    public User User { get; set; } = null!;
    public string QuestionId { get; set; } = string.Empty;
    public DateTimeOffset Time { get; set; } = DateTimeOffset.Now;
    public string UserAnswer { get; set; } = string.Empty;
}
