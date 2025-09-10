using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Text;
using WebhookService.Core.Entities;
using WebhookService.Core.Interfaces;
using WebhookService.Infrastructure.Data;

namespace WebhookService.Infrastructure.Services;

/// <summary>
/// محرك إرسال الويب هوك
/// Webhook dispatch engine
/// </summary>
public class WebhookDispatcher : IWebhookDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly WebhookDbContext _context;
    private readonly ISignatureService _signatureService;
    private readonly ILogger<WebhookDispatcher> _logger;
    private readonly HashSet<Guid> _processedEventIds = new(); // تمنع إعادة معالجة نفس الحدث في نفس الجلسة (Replay Prevention

    public WebhookDispatcher(
        HttpClient httpClient,
        WebhookDbContext context,
        ISignatureService signatureService,
        ILogger<WebhookDispatcher> logger)
    {
        _httpClient = httpClient;
        _context = context;
        _signatureService = signatureService;
        _logger = logger;

        // إعداد HttpClient - Configure HttpClient
        //•	Timeout per delivery: 5s.
        _httpClient.Timeout = TimeSpan.FromSeconds(5); // مهلة زمنية 5 ثوان
    }

    /// <summary>
    /// إرسال الويب هوك للمشترك
    /// Dispatch webhook to subscriber
    /// </summary>
    public async Task DispatchWebhookAsync(Event eventEntity, Subscriber subscriber)
    {
        //  traceId يستخدم لتتبع العملية في السجلات (logs) لتسهيل debugging.
        var traceId = Guid.NewGuid().ToString("N")[..8];
        
        _logger.LogInformation("بدء إرسال الويب هوك - TraceId: {TraceId}, EventId: {EventId}, SubscriberId: {SubscriberId}",
            traceId, eventEntity.Id, subscriber.Id);

        // التحقق من منع إعادة المعالجة - Check replay prevention
        var eventKey = eventEntity.Id;
        if (_processedEventIds.Contains(eventKey))
        {
            _logger.LogWarning("تم منع إعادة معالجة الحدث - TraceId: {TraceId}, EventId: {EventId}", 
                traceId, eventEntity.Id);
            return;
        }

        _processedEventIds.Add(eventKey);

        // إنشاء سجل التسليم - Create delivery record
        var delivery = new Delivery
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            SubscriberId = subscriber.Id,
            Status = DeliveryStatus.Pending,
            AttemptNumber = 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.Deliveries.Add(delivery);
        await _context.SaveChangesAsync();

        try
        {
            await SendWebhookAsync(delivery, eventEntity, subscriber, traceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في إرسال الويب هوك - TraceId: {TraceId}, EventId: {EventId}, SubscriberId: {SubscriberId}",
                traceId, eventEntity.Id, subscriber.Id);
            
            await UpdateDeliveryStatusAsync(delivery.Id, DeliveryStatus.Failed, 
                httpStatusCode: null, errorMessage: ex.Message, durationMs: 0);
        }
    }

    /// <summary>
    /// معالجة إعادة المحاولة
    /// Process retry attempt
    /// </summary>
    public async Task ProcessRetryAsync(Delivery delivery)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        
        _logger.LogInformation("بدء إعادة المحاولة - TraceId: {TraceId}, DeliveryId: {DeliveryId}, Attempt: {AttemptNumber}",
            traceId, delivery.Id, delivery.AttemptNumber + 1);

        // تحديث رقم المحاولة - Update attempt number
        delivery.AttemptNumber++;
        delivery.Status = DeliveryStatus.Retrying;
        delivery.NextRetryAt = null;

        await _context.SaveChangesAsync();

        try
        {
            // الحصول على بيانات الحدث والمشترك - Get event and subscriber data
            var eventEntity = await _context.Events.FindAsync(delivery.EventId);
            var subscriber = await _context.Subscribers.FindAsync(delivery.SubscriberId);

            if (eventEntity == null || subscriber == null)
            {
                _logger.LogError("بيانات الحدث أو المشترك غير موجودة - TraceId: {TraceId}, DeliveryId: {DeliveryId}",
                    traceId, delivery.Id);
                return;
            }

            await SendWebhookAsync(delivery, eventEntity, subscriber, traceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في إعادة المحاولة - TraceId: {TraceId}, DeliveryId: {DeliveryId}",
                traceId, delivery.Id);
            
            await HandleRetryFailureAsync(delivery, ex.Message);
        }
    }

    /// <summary>
    /// إرسال الويب هوك الفعلي
    /// Send actual webhook
    /// </summary>
    private async Task SendWebhookAsync(Delivery delivery, Event eventEntity, Subscriber subscriber, string traceId)
    {
        //   بدء توقيت التنفيذ 
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // فك تشفير المفتاح السري - Decrypt secret
            var secret = _signatureService.DecryptSecret(subscriber.EncryptedSecret);
            
            // إعداد البيانات للتوقيع - Prepare data for signature
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var version = "v1";
            var body = eventEntity.Payload;
            
            // توليد التوقيع - Generate signature
            var signature = _signatureService.GenerateSignature(version, timestamp, eventEntity.Id, body, secret);
            
            // إعداد الطلب - Prepare request
            var request = new HttpRequestMessage(HttpMethod.Post, subscriber.CallbackUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            // إضافة الهيدرز المطلوبة - Add required headers
            //التوقيع لتأكيد صحة الرسالة.
            request.Headers.Add("X-SWR-Signature", $"v1,ts={timestamp},kid={subscriber.KeyId},sig={signature}");
            //معرف الحدث لتسهيل التعقب على طرف المستقبل
            request.Headers.Add("X-SWR-Event-Id", eventEntity.Id.ToString());
            //تعريف الخدمة المرسلة.
            request.Headers.Add("User-Agent", "WebhookService/1.0");
            
            _logger.LogDebug("إرسال الطلب - TraceId: {TraceId}, URL: {Url}", traceId, subscriber.CallbackUrl);
            
            // إرسال الطلب - Send request
            var response = await _httpClient.SendAsync(request);
            //نوقف العد الزمني بعد انتهاء الطلب.
            stopwatch.Stop();
            
            var statusCode = (int)response.StatusCode;
            var durationMs = stopwatch.ElapsedMilliseconds;
            
            _logger.LogInformation("تم الإرسال - TraceId: {TraceId}, StatusCode: {StatusCode}, Duration: {Duration}ms",
                traceId, statusCode, durationMs);
            
            // تحديد حالة التسليم - Determine delivery status
            var status = response.IsSuccessStatusCode ? DeliveryStatus.Success : DeliveryStatus.Failed;
            string? errorMessage = null;
            
            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                errorMessage = $"HTTP {statusCode}: {responseContent}";
            }
            
            await UpdateDeliveryStatusAsync(delivery.Id, status, statusCode, errorMessage, durationMs);
            
            // جدولة إعادة المحاولة إذا فشل - Schedule retry if failed
            if (!response.IsSuccessStatusCode)
            {
                await ScheduleRetryAsync(delivery);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationMs = stopwatch.ElapsedMilliseconds;
            
            _logger.LogError(ex, "خطأ في الإرسال - TraceId: {TraceId}, Duration: {Duration}ms", 
                traceId, durationMs);
            
            await UpdateDeliveryStatusAsync(delivery.Id, DeliveryStatus.Failed, 
                httpStatusCode: null, errorMessage: ex.Message, durationMs);
            
            await ScheduleRetryAsync(delivery);
        }
    }

    /// <summary>
    /// تحديث حالة التسليم
    /// Update delivery status
    /// </summary>
    private async Task UpdateDeliveryStatusAsync(Guid deliveryId, DeliveryStatus status, 
        int? httpStatusCode, string? errorMessage, long durationMs)
    {
        var delivery = await _context.Deliveries.FindAsync(deliveryId);
        if (delivery == null) return;

        delivery.Status = status;
        delivery.HttpStatusCode = httpStatusCode;
        delivery.ErrorMessage = errorMessage;
        delivery.DurationMs = durationMs;
        
        if (status == DeliveryStatus.Success)
        {
            delivery.DeliveredAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// جدولة إعادة المحاولة
    /// Schedule retry attempt
    /// </summary>
    private async Task ScheduleRetryAsync(Delivery delivery)
    {
        const int maxAttempts = 5;
        
        if (delivery.AttemptNumber >= maxAttempts)
        {
            _logger.LogWarning("تم الوصول للحد الأقصى من المحاولات - DeliveryId: {DeliveryId}", delivery.Id);
            await UpdateDeliveryStatusAsync(delivery.Id, DeliveryStatus.DLQ, null, "Max attempts reached", 0);
            return;
        }
        //•	Retry policy: exponential backoff + jitter (~2s, ~10s, ~30s, ~2m, ~10m).
        // حساب وقت إعادة المحاولة التالية - Calculate next retry time
        var retryDelays = new[] { 2, 10, 30, 120, 600 }; // seconds: ~2s, ~10s, ~30s, ~2m, ~10m
        var delaySeconds = retryDelays[Math.Min(delivery.AttemptNumber - 1, retryDelays.Length - 1)];
        
        // إضافة عشوائية لتجنب التحميل الزائد - Add jitter to avoid thundering herd
        var jitter = new Random().Next(0, delaySeconds / 2);
        var totalDelay = delaySeconds + jitter;
        
        var nextRetryAt = DateTime.UtcNow.AddSeconds(totalDelay);
        
        delivery.NextRetryAt = nextRetryAt;
        delivery.Status = DeliveryStatus.Failed;
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("تم جدولة إعادة المحاولة - DeliveryId: {DeliveryId}, NextRetry: {NextRetry}",
            delivery.Id, nextRetryAt);
    }

    /// <summary>
    /// معالجة فشل إعادة المحاولة
    /// Handle retry failure
    /// </summary>
    private async Task HandleRetryFailureAsync(Delivery delivery, string errorMessage)
    {
        await UpdateDeliveryStatusAsync(delivery.Id, DeliveryStatus.Failed, 
            httpStatusCode: null, errorMessage, durationMs: 0);
        
        await ScheduleRetryAsync(delivery);
    }
}
