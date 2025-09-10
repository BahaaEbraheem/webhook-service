namespace WebhookService.Core.DTOs;

/// <summary>
/// استجابة تدوير السر
/// Rotate secret response
/// </summary>
public class RotateSecretResponse
{
    public string KeyId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
