using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebhookService.Core.DTOs;
using WebhookService.Core.Interfaces;
using WebhookService.Infrastructure.Data;

namespace WebhookService.Infrastructure.Services;

/// <summary>
/// خدمة إدارة عمليات التسليم
/// Service for managing deliveries
/// </summary>
public class DeliveryService : IDeliveryService
{
    private readonly WebhookDbContext _context;
    private readonly ILogger<DeliveryService> _logger;

    public DeliveryService(WebhookDbContext context, ILogger<DeliveryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// الحصول على عمليات التسليم مع التصفية والترقيم
    /// Get deliveries with filtering and pagination
    /// </summary>
    public async Task<DeliveryPagedResponse> GetDeliveriesAsync(DeliveryQueryRequest request)
    {
        _logger.LogInformation("البحث في عمليات التسليم مع المعايير: EventId={EventId}, SubscriberId={SubscriberId}, Status={Status}",
            request.EventId, request.SubscriberId, request.Status);

        // بناء الاستعلام الأساسي - Build base query
        var query = _context.Deliveries
            .Include(d => d.Event)
            .Include(d => d.Subscriber)
            .AsQueryable();

        // تطبيق المرشحات - Apply filters
        if (request.EventId.HasValue)
        {
            query = query.Where(d => d.EventId == request.EventId.Value);
        }

        if (request.SubscriberId.HasValue)
        {
            query = query.Where(d => d.SubscriberId == request.SubscriberId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(d => d.Status == request.Status.Value);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(d => d.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(d => d.CreatedAt <= request.ToDate.Value);
        }

        // حساب العدد الإجمالي - Calculate total count
        var totalCount = await query.CountAsync();

        // تطبيق الترقيم والترتيب - Apply pagination and ordering
        var deliveries = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(d => new DeliveryResponse
            {
                Id = d.Id,
                EventId = d.EventId,
                SubscriberId = d.SubscriberId,
                Status = d.Status,
                AttemptNumber = d.AttemptNumber,
                HttpStatusCode = d.HttpStatusCode,
                ErrorMessage = d.ErrorMessage,
                DurationMs = d.DurationMs,
                CreatedAt = d.CreatedAt,
                DeliveredAt = d.DeliveredAt,
                NextRetryAt = d.NextRetryAt,
                EventType = d.Event.EventType,
                TenantId = d.Event.TenantId,
                CallbackUrl = d.Subscriber.CallbackUrl
            })
            .ToListAsync();

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        _logger.LogInformation("تم العثور على {Count} عملية تسليم من أصل {Total}", 
            deliveries.Count, totalCount);

        return new DeliveryPagedResponse
        {
            Deliveries = deliveries,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages
        };
    }
}
