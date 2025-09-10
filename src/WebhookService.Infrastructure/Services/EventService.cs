using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using WebhookService.Core.DTOs;
using WebhookService.Core.Entities;
using WebhookService.Core.Interfaces;
using WebhookService.Infrastructure.Data;

namespace WebhookService.Infrastructure.Services;

/// <summary>
/// خدمة إدارة الأحداث
/// Service for managing events
/// </summary>
public class EventService : IEventService
{
    private readonly WebhookDbContext _context;
    private readonly ISubscriberService _subscriberService;
    private readonly IWebhookDispatcher _webhookDispatcher;
    private readonly ILogger<EventService> _logger;

    public EventService(
        WebhookDbContext context,
        ISubscriberService subscriberService,
        IWebhookDispatcher webhookDispatcher,
        ILogger<EventService> logger)
    {
        _context = context;
        _subscriberService = subscriberService;
        _webhookDispatcher = webhookDispatcher;
        _logger = logger;
    }

    /// <summary>
    /// إنشاء حدث جديد وإرساله للمشتركين
    /// Create new event and dispatch to subscribers
    /// </summary>
    public async Task<CreateEventResponse> CreateEventAsync(CreateEventRequest request)
    {
        _logger.LogInformation("إنشاء حدث جديد من النوع {EventType} للمستأجر {TenantId}", 
            request.EventType, request.TenantId);

        // التحقق من التكرار إذا تم توفير مفتاح التكرار - Check idempotency if key provided
        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var existingEvent = await _context.Events
                .FirstOrDefaultAsync(e => e.IdempotencyKey == request.IdempotencyKey);
            
            if (existingEvent != null)
            {
                _logger.LogInformation("الحدث موجود مسبقاً بمفتاح التكرار {IdempotencyKey}", request.IdempotencyKey);
                
                var matchedCount = await _context.Deliveries
                    .CountAsync(d => d.EventId == existingEvent.Id);

                return new CreateEventResponse
                {
                    EventId = existingEvent.Id,
                    TenantId = existingEvent.TenantId,
                    EventType = existingEvent.EventType,
                    CreatedAt = existingEvent.CreatedAt,
                    MatchedSubscribers = matchedCount
                };
            }
        }

        // إنشاء الحدث الجديد - Create new event
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            EventType = request.EventType,
            Payload = JsonSerializer.Serialize(request.Payload),
            IdempotencyKey = request.IdempotencyKey,
            CreatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // البحث عن المشتركين المطابقين - Find matching subscribers
        var subscribers = await _subscriberService.GetSubscribersByTenantAndEventTypeAsync(
            request.TenantId, request.EventType);

        _logger.LogInformation("تم العثور على {Count} مشترك مطابق للحدث {EventId}", 
            subscribers.Count, eventEntity.Id);

        // إرسال الويب هوك للمشتركين بشكل غير متزامن - Dispatch webhooks asynchronously
        var dispatchTasks = subscribers.Select(subscriber => 
            _webhookDispatcher.DispatchWebhookAsync(eventEntity, subscriber));
        
        // تشغيل المهام بالتوازي - Run tasks in parallel
        await Task.WhenAll(dispatchTasks);

        return new CreateEventResponse
        {
            EventId = eventEntity.Id,
            TenantId = eventEntity.TenantId,
            EventType = eventEntity.EventType,
            CreatedAt = eventEntity.CreatedAt,
            MatchedSubscribers = subscribers.Count
        };
    }

    /// <summary>
    /// التحقق من وجود حدث بمفتاح التكرار
    /// Check if event exists with idempotency key
    /// </summary>
    public async Task<bool> IsIdempotentEventAsync(string idempotencyKey)
    {
        return await _context.Events
            .AnyAsync(e => e.IdempotencyKey == idempotencyKey);
    }
}
