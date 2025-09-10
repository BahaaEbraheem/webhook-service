namespace WebhookService.Core.DTOs;

/// <summary>
/// استجابة حالة المشترك
/// Subscriber status response
/// </summary>
public class SubscriberStatusResponse
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public List<string> EventTypes { get; set; } = new();
    public string KeyId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
