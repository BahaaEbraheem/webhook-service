namespace WebhookService.Core.DTOs;

/// <summary>
/// استجابة عمليات التسليم مع الترقيم
/// Paged delivery response
/// </summary>
public class DeliveryPagedResponse
{
    public List<DeliveryResponse> Deliveries { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
