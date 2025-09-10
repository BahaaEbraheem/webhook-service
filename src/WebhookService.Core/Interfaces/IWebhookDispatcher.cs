using WebhookService.Core.Entities;

namespace WebhookService.Core.Interfaces;

public interface IWebhookDispatcher
{
    Task DispatchWebhookAsync(Event eventEntity, Subscriber subscriber);
    Task ProcessRetryAsync(Delivery delivery);
}

public interface ISignatureService
{
    string GenerateSignature(string version, long timestamp, Guid eventId, string body, string secret);
    bool ValidateSignature(string signature, string version, long timestamp, Guid eventId, string body, string secret);
    string EncryptSecret(string secret);
    string DecryptSecret(string encryptedSecret);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;
    Task RemoveAsync(string key);
    Task RemovePatternAsync(string pattern);
}
