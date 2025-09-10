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
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

        // البحث عن التسليمات التي تحتاج إعادة محاولة - Find deliveries that need retry
        var now = DateTime.UtcNow;
        var pendingRetries = await context.Deliveries
            .Where(d => d.Status == DeliveryStatus.Failed &&
                       d.NextRetryAt.HasValue &&
                       d.NextRetryAt.Value <= now &&   //لديها NextRetryAt أقل من الوقت الحالي
                       d.AttemptNumber < 5) // الحد الأقصى 5 محاولات
            .Take(100) // معالجة 100 في المرة الواحدة
            .ToListAsync();
        if (!pendingRetries.Any()) return;

        _logger.LogInformation("معالجة {Count} إعادة محاولة معلقة", pendingRetries.Count);
        // المعالجة بالتوازي لكل Delivery
        var retryTasks = pendingRetries.Select(async delivery =>
        {
            //CircuitBreaker لكل Delivery: يمنع فشل متكرر في عملية واحدة من تعطيل باقي العمليات.
            // CircuitBreaker منفصل لكل Delivery
            var cbLogger = loggerFactory.CreateLogger<CircuitBreaker>();
            var circuitBreaker = new CircuitBreaker(3, TimeSpan.FromSeconds(30), cbLogger);

            try
            {
                await circuitBreaker.ExecuteAsync(async () =>
                {
                    await dispatcher.ProcessRetryAsync(delivery);
                    return true; // ExecuteAsync يحتاج لإرجاع قيمة من النوع T
                });
            }
            catch (InvalidOperationException)
            {
                _logger.LogWarning("Delivery {DeliveryId} محجوبة مؤقتًا بسبب قاطع الدائرة", delivery.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "فشل تنفيذ إعادة محاولة للتسليم {DeliveryId}", delivery.Id);
            }
        });

        await Task.WhenAll(retryTasks);
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



