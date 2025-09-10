using WebhookService.Core.DTOs;

namespace WebhookService.Core.Interfaces;

public interface IEventService
{
    Task<CreateEventResponse> CreateEventAsync(CreateEventRequest request);
    Task<bool> IsIdempotentEventAsync(string idempotencyKey);
}
