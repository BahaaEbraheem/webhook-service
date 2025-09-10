using System.ComponentModel.DataAnnotations;

namespace WebhookService.Core.DTOs;

/// <summary>
/// طلب إنشاء مشترك جديد
/// Create subscriber request
/// </summary>
public class CreateSubscriberRequest
{
    [Required]
    [MaxLength(100)]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [Url]
    [MaxLength(500)]
    public string CallbackUrl { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<string> EventTypes { get; set; } = new();
}
