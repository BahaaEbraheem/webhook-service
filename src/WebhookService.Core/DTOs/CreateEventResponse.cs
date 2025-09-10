namespace WebhookService.Core.DTOs;

/// <summary>
/// استجابة إنشاء حدث جديد
/// Create event response
/// </summary>
public class CreateEventResponse
{
    public Guid EventId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int MatchedSubscribers { get; set; }
}
