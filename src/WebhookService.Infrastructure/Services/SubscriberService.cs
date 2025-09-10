using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebhookService.Core.DTOs;
using WebhookService.Core.Entities;
using WebhookService.Core.Interfaces;
using WebhookService.Infrastructure.Data;

namespace WebhookService.Infrastructure.Services;

/// <summary>
/// خدمة إدارة المشتركين في الويب هوك
/// Service for managing webhook subscribers
/// </summary>
public class SubscriberService : ISubscriberService
{
    private readonly WebhookDbContext _context;
    private readonly ISignatureService _signatureService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<SubscriberService> _logger;

    public SubscriberService(
        WebhookDbContext context,
        ISignatureService signatureService,
        ICacheService cacheService,
        ILogger<SubscriberService> logger)
    {
        _context = context;
        _signatureService = signatureService;
        _cacheService = cacheService;
        _logger = logger;
    }
    public async Task<List<SubscriberDto>> GetAllSubscribersAsync()
    {
        var subscribers = await _context.Subscribers.ToListAsync();

        return subscribers.Select(s => new SubscriberDto
        {
            Id = s.Id,
            TenantId = s.TenantId,
            CallbackUrl = s.CallbackUrl,
            KeyId = s.KeyId,
            EventTypes = s.EventTypes,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt
        }).ToList();
    }
    /// <summary>
    /// إنشاء مشترك جديد
    /// Create a new subscriber
    /// </summary>
    public async Task<CreateSubscriberResponse> CreateSubscriberAsync(CreateSubscriberRequest request)
    {
        _logger.LogInformation("إنشاء مشترك جديد للمستأجر {TenantId}", request.TenantId);
        // توليد مفتاح سري جديد - Generate new secret key
        var secret = GenerateSecret();


        var keyId = Guid.NewGuid()     //   يولد معرّف عالمي فريد (UUID
            .ToString("N")             //   يحول UUID إلى سلسلة مكونة من 32 حرفًا بدون شرطات. 
            [..16];                    //   يأخذ أول 16 حرف فقط

        //تشفير المفتاح السري
        var encryptedSecret = _signatureService.EncryptSecret(secret);

        var subscriber = new Subscriber
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            CallbackUrl = request.CallbackUrl,
            EventTypes = request.EventTypes,
            EncryptedSecret = encryptedSecret,
            KeyId = keyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Subscribers.Add(subscriber);
        await _context.SaveChangesAsync();

        // إبطال الكاش للمستأجر - Invalidate cache for tenant
        await InvalidateCacheAsync(request.TenantId);

        _logger.LogInformation("تم إنشاء المشترك {SubscriberId} بنجاح", subscriber.Id);

        return new CreateSubscriberResponse
        {
            Id = subscriber.Id,
            TenantId = subscriber.TenantId,
            CallbackUrl = subscriber.CallbackUrl,
            EventTypes = subscriber.EventTypes,
            KeyId = subscriber.KeyId,
            Secret = secret, // يُعاد فقط عند الإنشاء - Only returned on creation
            CreatedAt = subscriber.CreatedAt
        };
    }

    /// <summary>
    /// تدوير المفتاح السري للمشترك
    /// Rotate subscriber secret key
    /// </summary>
    public async Task<RotateSecretResponse> RotateSecretAsync(Guid subscriberId)
    {
        _logger.LogInformation("تدوير المفتاح السري للمشترك {SubscriberId}", subscriberId);

        var subscriber = await _context.Subscribers.FindAsync(subscriberId);
        if (subscriber == null)
        {
            throw new ArgumentException("المشترك غير موجود - Subscriber not found");
        }

        // توليد مفتاح سري جديد - Generate new secret key
        var newSecret = GenerateSecret();
        var newKeyId = Guid.NewGuid().ToString("N")[..16];
        var encryptedSecret = _signatureService.EncryptSecret(newSecret);

        subscriber.EncryptedSecret = encryptedSecret;
        subscriber.KeyId = newKeyId;
        subscriber.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // إبطال الكاش للمستأجر - Invalidate cache for tenant
        await InvalidateCacheAsync(subscriber.TenantId);

        _logger.LogInformation("تم تدوير المفتاح السري للمشترك {SubscriberId}", subscriberId);

        return new RotateSecretResponse
        {
            KeyId = newKeyId,
            Secret = newSecret,
            UpdatedAt = subscriber.UpdatedAt
        };
    }

    /// <summary>
    /// الحصول على حالة المشترك
    /// Get subscriber status
    /// </summary>
    public async Task<SubscriberStatusResponse?> GetSubscriberStatusAsync(Guid subscriberId)
    {
        var subscriber = await _context.Subscribers.FindAsync(subscriberId);
        if (subscriber == null)
        {
            return null;
        }

        return new SubscriberStatusResponse
        {
            Id = subscriber.Id,
            TenantId = subscriber.TenantId,
            CallbackUrl = subscriber.CallbackUrl,
            EventTypes = subscriber.EventTypes,
            KeyId = subscriber.KeyId,
            IsActive = subscriber.IsActive,
            CreatedAt = subscriber.CreatedAt,
            UpdatedAt = subscriber.UpdatedAt
        };
    }

    /// <summary>
    /// الحصول على المشتركين حسب المستأجر ونوع الحدث
    /// Get subscribers by tenant and event type
    /// </summary>
    public async Task<List<Subscriber>> GetSubscribersByTenantAndEventTypeAsync(string tenantId, string eventType)
    {
        // محاولة الحصول من الكاش أولاً - Try to get from cache first
        //•	Redis caching for subscriber configs (subs:{tenantId}) with TTL 60s.
        var cacheKey = $"subs:{tenantId}";
        var cachedSubscribers = await _cacheService.GetAsync<List<Subscriber>>(cacheKey);
        
        if (cachedSubscribers != null)
        {
            return cachedSubscribers.Where(s => s.EventTypes.Contains(eventType) && s.IsActive).ToList();
        }

        // الحصول من قاعدة البيانات - Get from database
        var subscribers = await _context.Subscribers
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .ToListAsync();

        // حفظ في الكاش لمدة 60 ثانية - Cache for 60 seconds
        await _cacheService.SetAsync(cacheKey, subscribers, TimeSpan.FromSeconds(60));

        return subscribers.Where(s => s.EventTypes.Contains(eventType)).ToList();
    }

    /// <summary>
    /// إبطال الكاش للمستأجر
    /// Invalidate cache for tenant
    /// </summary>
    public async Task InvalidateCacheAsync(string tenantId)
    {
        var cacheKey = $"subs:{tenantId}";
        await _cacheService.RemoveAsync(cacheKey);
        _logger.LogDebug("تم إبطال الكاش للمستأجر {TenantId}", tenantId);
    }

    /// <summary>
    /// توليد مفتاح سري عشوائي
    /// Generate random secret key
    /// </summary>
    private static string GenerateSecret()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
