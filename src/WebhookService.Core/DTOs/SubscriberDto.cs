using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebhookService.Core.DTOs
{
    public class SubscriberDto
    {
        public Guid Id { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public string CallbackUrl { get; set; } = string.Empty;
        public string KeyId { get; set; } = string.Empty;
        public List<string> EventTypes { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
