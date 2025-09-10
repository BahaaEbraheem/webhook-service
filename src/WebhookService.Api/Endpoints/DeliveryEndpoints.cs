using Microsoft.AspNetCore.Mvc;
using WebhookService.Core.DTOs;
using WebhookService.Core.Entities;
using WebhookService.Core.Interfaces;

namespace WebhookService.Api.Endpoints;

/// <summary>
/// نقاط النهاية الخاصة بعمليات التسليم
/// Delivery endpoints
/// </summary>
public static class DeliveryEndpoints
{
    /// <summary>
    /// تسجيل نقاط النهاية الخاصة بعمليات التسليم
    /// Register delivery endpoints
    /// </summary>
    public static void MapDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deliveries")
            .WithTags("Deliveries")
            .WithOpenApi();

        // البحث في عمليات التسليم - Search deliveries
        group.MapGet("/", GetDeliveries)
            .WithName("GetDeliveries")
            .WithSummary("البحث في عمليات التسليم - Search deliveries")
            .Produces<DeliveryPagedResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// البحث في عمليات التسليم مع التصفية والترقيم
    /// Search deliveries with filtering and pagination
    /// </summary>
    private static async Task<IResult> GetDeliveries(
        IDeliveryService deliveryService,
        ILogger<Program> logger,
        [FromQuery] Guid? eventId = null,
        [FromQuery] Guid? subscriberId = null,
        [FromQuery] DeliveryStatus? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            // التحقق من صحة المعاملات - Validate parameters
            if (page < 1)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid Page Number",
                    Detail = "رقم الصفحة يجب أن يكون أكبر من 0 - Page number must be greater than 0",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid Page Size",
                    Detail = "حجم الصفحة يجب أن يكون بين 1 و 100 - Page size must be between 1 and 100",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid Date Range",
                    Detail = "تاريخ البداية يجب أن يكون قبل تاريخ النهاية - From date must be before to date",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            logger.LogInformation("البحث في عمليات التسليم - EventId: {EventId}, SubscriberId: {SubscriberId}, Status: {Status}, Page: {Page}",
                eventId, subscriberId, status, page);

            var request = new DeliveryQueryRequest
            {
                EventId = eventId,
                SubscriberId = subscriberId,
                Status = status,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = pageSize
            };

            var response = await deliveryService.GetDeliveriesAsync(request);

            logger.LogInformation("تم العثور على {Count} عملية تسليم من أصل {Total}",
                response.Deliveries.Count, response.TotalCount);

            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("طلب غير صحيح للبحث في التسليمات: {Error}", ex.Message);
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "خطأ في البحث في عمليات التسليم");
            return Results.Problem(
                title: "Internal Server Error",
                detail: "حدث خطأ أثناء البحث في عمليات التسليم - An error occurred while searching deliveries",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
