using WebhookService.Core.DTOs;

namespace WebhookService.Core.Interfaces;

public interface IDeliveryService
{
    Task<DeliveryPagedResponse> GetDeliveriesAsync(DeliveryQueryRequest request);
}
