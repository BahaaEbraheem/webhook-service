namespace WebhookService.Core.DTOs;

/// <summary>
/// استجابة إنشاء مشترك جديد
/// Create subscriber response
/// </summary>
public class CreateSubscriberResponse
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public List<string> EventTypes { get; set; } = new();
    public string KeyId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty; // Only returned on creation
    public DateTime CreatedAt { get; set; }
}
