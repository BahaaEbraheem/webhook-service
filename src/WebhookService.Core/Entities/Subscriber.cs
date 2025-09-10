using System.ComponentModel.DataAnnotations;

namespace WebhookService.Core.Entities;

public class Subscriber
{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string TenantId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string CallbackUrl { get; set; } = string.Empty;
    
    [Required]
    public string EncryptedSecret { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string KeyId { get; set; } = string.Empty;
    
    public List<string> EventTypes { get; set; } = new();
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
}
