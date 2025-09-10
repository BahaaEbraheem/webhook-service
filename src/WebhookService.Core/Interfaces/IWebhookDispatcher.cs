using WebhookService.Core.Entities;

namespace WebhookService.Core.Interfaces;

public interface IWebhookDispatcher
{
    Task DispatchWebhookAsync(Event eventEntity, Subscriber subscriber);
    Task ProcessRetryAsync(Delivery delivery);
}



