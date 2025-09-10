using System.ComponentModel.DataAnnotations;

namespace WebhookService.Core.Entities;

public class Event
{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string TenantId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;
    
    [Required]
    public string Payload { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
}
