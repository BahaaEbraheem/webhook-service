using WebhookService.Core.Entities;

namespace WebhookService.Core.DTOs;

/// <summary>
/// استجابة عملية التسليم
/// Delivery response
/// </summary>
public class DeliveryResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid SubscriberId { get; set; }
    public DeliveryStatus Status { get; set; }
    public int AttemptNumber { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
    
    // معلومات إضافية - Additional info
    public string EventType { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
}
