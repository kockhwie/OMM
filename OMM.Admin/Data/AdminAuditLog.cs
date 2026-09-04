using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OMM.Admin.Data;

[Table("AdminAuditLogs", Schema = "admin")]
public class AdminAuditLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string ActorUserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ActorUserName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? TargetUserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Detail { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
