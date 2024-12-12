using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModelLayer.Models;

public class Log {
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string LogLevel { get; set; }
    public string Message { get; set; }
    public string? Exception { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}