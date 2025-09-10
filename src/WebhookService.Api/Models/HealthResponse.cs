namespace WebhookService.Api.Models;

/// <summary>
/// استجابة فحص الصحة
/// Health check response
/// </summary>
public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, HealthCheck> Checks { get; set; } = new();
}
