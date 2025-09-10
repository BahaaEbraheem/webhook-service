using Microsoft.AspNetCore.Mvc;
using Prometheus;
using WebhookService.Core.DTOs;
using WebhookService.Core.Interfaces;

namespace WebhookService.Api.Endpoints;

/// <summary>
/// نقاط النهاية الخاصة بالأحداث
/// Event endpoints
/// </summary>
public static class EventEndpoints
{
    private static readonly Counter EventsCounter = Metrics.CreateCounter("swr_events_total", "Total number of events");
    private static readonly Histogram DeliveryLatency = Metrics.CreateHistogram("swr_delivery_latency_ms", "Delivery latency in milliseconds");

    /// <summary>
    /// تسجيل نقاط النهاية الخاصة بالأحداث
    /// Register event endpoints
    /// </summary>
    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events")
            .WithTags("Events")
            .WithOpenApi();

        // إنشاء حدث جديد - Create new event
        group.MapPost("/", CreateEvent)
            .WithName("CreateEvent")
            .WithSummary("إنشاء حدث جديد - Create new event")
            .Produces<CreateEventResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    /// <summary>
    /// إنشاء حدث جديد
    /// Create new event
    /// </summary>
    private static async Task<IResult> CreateEvent(
        [FromBody] CreateEventRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        IEventService eventService,
        ILogger<Program> logger)
    {
        //لقياس الزمن يقوم بفتح مؤقت تلقائي
        using var timer = DeliveryLatency.NewTimer();
        
        try
        {
            logger.LogInformation("طلب إنشاء حدث جديد من النوع {EventType} للمستأجر {TenantId}", 
                request.EventType, request.TenantId);

            // استخدام مفتاح التكرار من الهيدر إذا لم يتم توفيره في الطلب
            // Use idempotency key from header if not provided in request
            if (!string.IsNullOrEmpty(idempotencyKey) && string.IsNullOrEmpty(request.IdempotencyKey))
            {
                request.IdempotencyKey = idempotencyKey;
            }

            // التحقق من التكرار - Check for idempotency
            if (!string.IsNullOrEmpty(request.IdempotencyKey))
            {
                var isIdempotent = await eventService.IsIdempotentEventAsync(request.IdempotencyKey);
                if (isIdempotent)
                {
                    logger.LogInformation("الحدث موجود مسبقاً بمفتاح التكرار {IdempotencyKey}", request.IdempotencyKey);
                    return Results.Conflict(new ProblemDetails
                    {
                        Title = "Duplicate Event",
                        Detail = $"حدث بمفتاح التكرار {request.IdempotencyKey} موجود مسبقاً - Event with idempotency key {request.IdempotencyKey} already exists",
                        Status = StatusCodes.Status409Conflict
                    });
                }
            }

            var response = await eventService.CreateEventAsync(request);

            // تحديث المقاييس - Update metrics بعد أن يتم إنشاء الحدث بنجاح يزيد قيمة العداد بمقدار 1.
            EventsCounter.Inc();
            
            logger.LogInformation("تم إنشاء الحدث {EventId} وإرساله لـ {MatchedSubscribers} مشترك", 
                response.EventId, response.MatchedSubscribers);
            
            return Results.Created($"/api/events/{response.EventId}", response);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("طلب غير صحيح لإنشاء حدث: {Error}", ex.Message);
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "خطأ في إنشاء الحدث من النوع {EventType} للمستأجر {TenantId}", 
                request.EventType, request.TenantId);
            return Results.Problem(
                title: "Internal Server Error",
                detail: "حدث خطأ أثناء إنشاء الحدث - An error occurred while creating event",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
