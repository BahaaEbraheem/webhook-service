using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using WebhookService.Core.Interfaces;

namespace WebhookService.Infrastructure.Services;

/// <summary>
/// خدمة التخزين المؤقت باستخدام Redis
/// Redis caching service
/// </summary>
public class CacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly IServer _server;
    private readonly ILogger<CacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public CacheService(IConnectionMultiplexer redis, ILogger<CacheService> logger)
    {
        _database = redis.GetDatabase();
        _server = redis.GetServer(redis.GetEndPoints().First());
        _logger = logger;
        
        // إعدادات JSON للتسلسل - JSON serialization options
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// الحصول على قيمة من الكاش
    /// Get value from cache
    /// </summary>
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            _logger.LogDebug("محاولة الحصول على القيمة من الكاش: {Key}", key);
            
            var value = await _database.StringGetAsync(key);
            
            if (!value.HasValue)
            {
                _logger.LogDebug("القيمة غير موجودة في الكاش: {Key}", key);
                return null;
            }

            var result = JsonSerializer.Deserialize<T>(value!, _jsonOptions);
            _logger.LogDebug("تم الحصول على القيمة من الكاش بنجاح: {Key}", key);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في الحصول على القيمة من الكاش: {Key}", key);
            return null;
        }
    }

    /// <summary>
    /// حفظ قيمة في الكاش
    /// Set value in cache
    /// </summary>
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        try
        {
            _logger.LogDebug("حفظ القيمة في الكاش: {Key} مع انتهاء صلاحية: {Expiry}", 
                key, expiry?.TotalSeconds);
            
            var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
            
            await _database.StringSetAsync(key, serializedValue, expiry);
            
            _logger.LogDebug("تم حفظ القيمة في الكاش بنجاح: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في حفظ القيمة في الكاش: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// حذف قيمة من الكاش
    /// Remove value from cache
    /// </summary>
    public async Task RemoveAsync(string key)
    {
        try
        {
            _logger.LogDebug("حذف القيمة من الكاش: {Key}", key);
            
            await _database.KeyDeleteAsync(key);
            
            _logger.LogDebug("تم حذف القيمة من الكاش بنجاح: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في حذف القيمة من الكاش: {Key}", key);
            throw;
        }
    }

}
