namespace WebhookService.Api.Models;

/// <summary>
/// فحص صحة فردي
/// Individual health check
/// </summary>
public class HealthCheck
{
    public string Status { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string Description { get; set; } = string.Empty;
}
