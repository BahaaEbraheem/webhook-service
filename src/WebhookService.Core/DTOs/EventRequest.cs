using System.ComponentModel.DataAnnotations;

namespace WebhookService.Core.DTOs;

/// <summary>
/// طلب إنشاء حدث جديد
/// Create event request
/// </summary>
public class CreateEventRequest
{
    [Required]
    [MaxLength(100)]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    public object Payload { get; set; } = new();

    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }
}
