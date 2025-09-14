using Microsoft.AspNetCore.Mvc;
using Prometheus;
using WebhookService.Core.DTOs;
using WebhookService.Core.Entities;
using WebhookService.Core.Interfaces;

namespace WebhookService.Api.Endpoints;

/// <summary>
/// نقاط النهاية الخاصة بالمشتركين
/// Subscriber endpoints
/// </summary>
public static class SubscriberEndpoints
{
    private static readonly Counter EventsCounter = Metrics.CreateCounter("swr_events_total", "Total number of events");
    private static readonly Counter DeliveriesCounter = Metrics.CreateCounter("swr_deliveries_total", "Total number of deliveries", "status");

    /// <summary>
    /// تسجيل نقاط النهاية الخاصة بالمشتركين
    /// Register subscriber endpoints
    /// </summary>
    public static void MapSubscriberEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/subscribers")
            .WithTags("Subscribers")
            .WithOpenApi();
        // الحصول على جميع المشتركين - Get all subscribers
        group.MapGet("/", GetAllSubscribers)
            .WithName("GetAllSubscribers")
            .WithSummary("الحصول على جميع المشتركين - Get all subscribers")
            .Produces<List<SubscriberDto>>(StatusCodes.Status200OK);

        // إنشاء مشترك جديد - Create new subscriber
        group.MapPost("/", CreateSubscriber)
            .WithName("CreateSubscriber")
            .WithSummary("إنشاء مشترك جديد - Create new subscriber")
            .Produces<CreateSubscriberResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // تدوير المفتاح السري - Rotate secret key
        group.MapPost("/{id:guid}/rotate-secret", RotateSecret)
            .WithName("RotateSecret")
            .WithSummary("تدوير المفتاح السري - Rotate secret key")
            .Produces<RotateSecretResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // الحصول على حالة المشترك - Get subscriber status
        group.MapGet("/{id:guid}/status", GetSubscriberStatus)
            .WithName("GetSubscriberStatus")
            .WithSummary("الحصول على حالة المشترك - Get subscriber status")
            .Produces<SubscriberStatusResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
        // الحصول على المشترك الكامل (Full Subscriber) - includes EncryptedSecret
        group.MapGet("/{id:guid}/full", GetFullSubscriber)
            .WithName("GetFullSubscriber")
            .WithSummary("الحصول على بيانات المشترك الكامل بما في ذلك السر المشفر - Get full subscriber data including encrypted secret")
            .Produces<Subscriber>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
        // الحصول على المشترك بواسطة KeyId - Get subscriber by KeyId
        group.MapGet("/key/{keyId}", GetSubscriberByKeyId)
            .WithName("GetSubscriberByKeyId")
            .WithSummary("الحصول على المشترك بواسطة KeyId - Get subscriber by KeyId")
            .Produces<Subscriber>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

    }
    private static async Task<IResult> GetSubscriberByKeyId(
    string keyId,
    ISubscriberService subscriberService,
    ILogger<Program> logger)
    {
        try
        {
            logger.LogDebug("طلب المشترك بواسطة KeyId {KeyId}", keyId);

            var subscriber = await subscriberService.GetSubscriberByKeyIdAsync(keyId);
            if (subscriber == null)
            {
                logger.LogWarning("المشترك غير موجود بـ KeyId {KeyId}", keyId);
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Subscriber Not Found",
                    Detail = $"المشترك بـ KeyId {keyId} غير موجود",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Results.Ok(subscriber);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "خطأ في الحصول على المشترك بـ KeyId {KeyId}", keyId);
            return Results.Problem(
                title: "Internal Server Error",
                detail: "حدث خطأ أثناء الحصول على المشترك - An error occurred while getting subscriber",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetFullSubscriber(
    Guid id,
    ISubscriberService subscriberService,
    ILogger<Program> logger)
    {
        try
        {
            logger.LogDebug("طلب البيانات الكاملة للمشترك {SubscriberId}", id);

            // You need a service method that returns Subscriber (with EncryptedSecret)
            var subscriber = await subscriberService.GetSubscriberByIdAsync(id);
            if (subscriber == null)
            {
                logger.LogWarning("المشترك غير موجود: {SubscriberId}", id);
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Subscriber Not Found",
                    Detail = $"المشترك {id} غير موجود - Subscriber {id} not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Results.Ok(subscriber);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "خطأ في الحصول على البيانات الكاملة للمشترك {SubscriberId}", id);
            return Results.Problem(
                title: "Internal Server Error",
                detail: "حدث خطأ أثناء الحصول على البيانات الكاملة للمشترك - An error occurred while getting full subscriber",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// جلب كل المشتركين
    /// Create new subscriber
    /// </summary>
    private static async Task<IResult> GetAllSubscribers(
    ISubscriberService subscriberService,
    ILogger<Program> logger)
    {
        try
        {
            logger.LogInformation("طلب الحصول على جميع المشتركين");

            var subscribers = await subscriberService.GetAllSubscribersAsync();

            return Results.Ok(subscribers);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "خطأ أثناء الحصول على جميع المشتركين");
            return Results.Problem(
                title: "Internal Server Error",
                detail: "حدث خطأ أثناء جلب كل المشتركين - An error occurred while getting all subscribers",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// إنشاء مشترك جديد
    /// Create new subscriber
    /// </summary>
    private static async Task<IResult> CreateSubscriber(
        [FromBody] CreateSubscriberRequest request,
        ISubscriberService subscriberService,
        ILogger<Program> logger)
    {
        try
        {
            logger.LogInformation("طلب إنشاء مشترك جديد للمستأجر {TenantId}", request.TenantId);

            var response = await subscriberService.CreateSubscriberAsync(request);
            
            logger.LogInformation("تم إنشاء المشترك {SubscriberId} بنجاح", response.Id);
            
            return Results.Created($"/api/subscribers/{response.Id}/status", response);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("طلب غير صحيح لإنشاء مشترك: {Error}", ex.Message);
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "خطأ في إنشاء المشترك للمستأجر {TenantId}", request.TenantId);
            return Results.Problem(
                title: "Internal Server Error",
                detail: "حدث خطأ أثناء إنشاء المشترك - An error occurred while creating subscriber",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// تدوير المفتاح السري
    /// Rotate secret key
    /// </summary>
    private static async Task<IResult> RotateSecret(
        Guid id,
        ISubscriberService subscriberService,
        ILogger<Program> logger)
    {
        try
        {
            logger.LogInformation("طلب تدوير المفتاح السري للمشترك {SubscriberId}", id);

            var response = await subscriberService.RotateSecretAsync(id);
            
            logger.LogInformation("تم تدوير المفتاح السري للمشترك {SubscriberId}", id);
            
            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("المشترك غير موجود: {SubscriberId}", id);
            return Results.NotFound(new ProblemDetails
            {
                Title = "Subscriber Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "خطأ في تدوير المفتاح السري للمشترك {SubscriberId}", id);
            return Results.Problem(
                title: "Internal Server Error",
                detail: "حدث خطأ أثناء تدوير المفتاح السري - An error occurred while rotating secret",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// الحصول على حالة المشترك
    /// Get subscriber status
    /// </summary>
    private static async Task<IResult> GetSubscriberStatus(
        Guid id,
        ISubscriberService subscriberService,
        ILogger<Program> logger)
    {
        try
        {
            logger.LogDebug("طلب حالة المشترك {SubscriberId}", id);

            var response = await subscriberService.GetSubscriberStatusAsync(id);
            
            if (response == null)
            {
                logger.LogWarning("المشترك غير موجود: {SubscriberId}", id);
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Subscriber Not Found",
                    Detail = $"المشترك {id} غير موجود - Subscriber {id} not found",
                    Status = StatusCodes.Status404NotFound
                });
            }
            
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "خطأ في الحصول على حالة المشترك {SubscriberId}", id);
            return Results.Problem(
                title: "Internal Server Error",
                detail: "حدث خطأ أثناء الحصول على حالة المشترك - An error occurred while getting subscriber status",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
