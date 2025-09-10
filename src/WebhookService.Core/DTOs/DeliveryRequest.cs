using WebhookService.Core.Entities;

namespace WebhookService.Core.DTOs;

/// <summary>
/// طلب البحث في عمليات التسليم
/// Delivery query request
/// </summary>
public class DeliveryQueryRequest
{
    public Guid? EventId { get; set; }
    public Guid? SubscriberId { get; set; }
    public DeliveryStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
