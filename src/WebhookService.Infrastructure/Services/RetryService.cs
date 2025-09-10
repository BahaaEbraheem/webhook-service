using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebhookService.Core.Entities;
using WebhookService.Core.Interfaces;
using WebhookService.Infrastructure.Data;

namespace WebhookService.Infrastructure.Services;

/// <summary>
/// خدمة إعادة المحاولة والموثوقية
/// Retry and reliability service
/// </summary>
public class RetryService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RetryService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // فحص كل دقيقة

    public RetryService(IServiceProvider serviceProvider, ILogger<RetryService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// تشغيل الخدمة في الخلفية
    /// Run background service
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("بدء تشغيل خدمة إعادة المحاولة");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingRetriesAsync();
                await CleanupOldDeliveriesAsync();

                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("تم إيقاف خدمة إعادة المحاولة");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في خدمة إعادة المحاولة");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    /// <summary>
    /// معالجة إعادة المحاولات المعلقة
    /// Process pending retries
    /// </summary>
    private async Task ProcessPendingRetriesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IWebhookDispatcher>();

        // البحث عن التسليمات التي تحتاج إعادة محاولة - Find deliveries that need retry
        var now = DateTime.UtcNow;
        var pendingRetries = await context.Deliveries
            .Where(d => d.Status == DeliveryStatus.Failed &&
                       d.NextRetryAt.HasValue &&
                       d.NextRetryAt.Value <= now &&
                       d.AttemptNumber < 5) // الحد الأقصى 5 محاولات
            .Take(100) // معالجة 100 في المرة الواحدة
            .ToListAsync();

        if (pendingRetries.Count > 0)
        {
            _logger.LogInformation("معالجة {Count} إعادة محاولة معلقة", pendingRetries.Count);

            // معالجة إعادة المحاولات بالتوازي - Process retries in parallel
            var retryTasks = pendingRetries.Select(async delivery =>
            {
                try
                {
                    await dispatcher.ProcessRetryAsync(delivery);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطأ في معالجة إعادة المحاولة للتسليم {DeliveryId}", delivery.Id);
                }
            });

            await Task.WhenAll(retryTasks);
        }
    }

    /// <summary>
    /// تنظيف التسليمات القديمة
    /// Cleanup old deliveries
    /// </summary>
    private async Task CleanupOldDeliveriesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();

        // حذف التسليمات الناجحة الأقدم من 30 يوم - Delete successful deliveries older than 30 days
        var cutoffDate = DateTime.UtcNow.AddDays(-30);

        var oldDeliveries = await context.Deliveries
            .Where(d => d.Status == DeliveryStatus.Success && d.CreatedAt < cutoffDate)
            .Take(1000) // حذف 1000 في المرة الواحدة
            .ToListAsync();

        if (oldDeliveries.Count > 0)
        {
            _logger.LogInformation("تنظيف {Count} تسليم قديم", oldDeliveries.Count);

            context.Deliveries.RemoveRange(oldDeliveries);
            await context.SaveChangesAsync();
        }

        // نقل التسليمات الفاشلة نهائياً إلى DLQ - Move permanently failed deliveries to DLQ
        var failedDeliveries = await context.Deliveries
            .Where(d => d.Status == DeliveryStatus.Failed &&
                       d.AttemptNumber >= 5 &&
                       !d.NextRetryAt.HasValue)
            .Take(100)
            .ToListAsync();

        if (failedDeliveries.Count > 0)
        {
            _logger.LogInformation("نقل {Count} تسليم إلى DLQ", failedDeliveries.Count);

            foreach (var delivery in failedDeliveries)
            {
                delivery.Status = DeliveryStatus.DLQ;
            }

            await context.SaveChangesAsync();
        }
    }
}

/// <summary>
/// قاطع الدائرة للحماية من الأحمال الزائدة
/// Circuit breaker for overload protection
/// </summary>
public class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _timeout;
    private readonly ILogger<CircuitBreaker> _logger;

    private int _failureCount;
    private DateTime _lastFailureTime;
    private CircuitBreakerState _state = CircuitBreakerState.Closed;

    public CircuitBreaker(int failureThreshold, TimeSpan timeout, ILogger<CircuitBreaker> logger)
    {
        _failureThreshold = failureThreshold;
        _timeout = timeout;
        _logger = logger;
    }

    /// <summary>
    /// تنفيذ العملية مع حماية قاطع الدائرة
    /// Execute operation with circuit breaker protection
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (_state == CircuitBreakerState.Open)
        {
            if (DateTime.UtcNow - _lastFailureTime > _timeout)
            {
                _state = CircuitBreakerState.HalfOpen;
                _logger.LogInformation("قاطع الدائرة في حالة نصف مفتوح");
            }
            else
            {
                throw new InvalidOperationException("قاطع الدائرة مفتوح - Circuit breaker is open");
            }
        }

        try
        {
            var result = await operation();
            OnSuccess();
            return result;
        }
        catch (Exception)
        {
            OnFailure();
            throw;
        }
    }

    /// <summary>
    /// معالجة النجاح
    /// Handle success
    /// </summary>
    private void OnSuccess()
    {
        _failureCount = 0;
        _state = CircuitBreakerState.Closed;
    }

    /// <summary>
    /// معالجة الفشل
    /// Handle failure
    /// </summary>
    private void OnFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;

        if (_failureCount >= _failureThreshold)
        {
            _state = CircuitBreakerState.Open;
            _logger.LogWarning("تم فتح قاطع الدائرة بعد {FailureCount} فشل", _failureCount);
        }
    }

    public bool IsOpen => _state == CircuitBreakerState.Open;
}

/// <summary>
/// حالات قاطع الدائرة
/// Circuit breaker states
/// </summary>
public enum CircuitBreakerState
{
    Closed,   // مغلق - العمليات تعمل بشكل طبيعي
    Open,     // مفتوح - العمليات محظورة
    HalfOpen  // نصف مفتوح - اختبار العمليات
}
