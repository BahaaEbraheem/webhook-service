using WebhookService.Core.DTOs;
using WebhookService.Core.Entities;

namespace WebhookService.Core.Interfaces;

public interface ISubscriberService
{
    Task<CreateSubscriberResponse> CreateSubscriberAsync(CreateSubscriberRequest request);
    Task<RotateSecretResponse> RotateSecretAsync(Guid subscriberId);
    Task<SubscriberStatusResponse?> GetSubscriberStatusAsync(Guid subscriberId);
    Task<List<Subscriber>> GetSubscribersByTenantAndEventTypeAsync(string tenantId, string eventType);
    Task InvalidateCacheAsync(string tenantId);
    Task<List<SubscriberDto>> GetAllSubscribersAsync();
}
