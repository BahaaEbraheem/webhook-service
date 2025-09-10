using System.ComponentModel.DataAnnotations;

namespace WebhookService.Core.Entities;

public class Delivery
{
    public Guid Id { get; set; }
    
    public Guid EventId { get; set; }
    
    public Guid SubscriberId { get; set; }
    
    public DeliveryStatus Status { get; set; }
        
    public int AttemptNumber { get; set; }    //  ⁄œœ «·„Õ«Ê·«  «· Ì  „  · Ê’Ì· «·—”«·…

    public int? HttpStatusCode { get; set; }
    
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }
    
    public long DurationMs { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? DeliveredAt { get; set; }
    
    public DateTime? NextRetryAt { get; set; }
    
    // Navigation properties
    public Event Event { get; set; } = null!;
    public Subscriber Subscriber { get; set; } = null!;
}

public enum DeliveryStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2,
    Retrying = 3,
    DLQ = 4 // Dead Letter Queue
}
