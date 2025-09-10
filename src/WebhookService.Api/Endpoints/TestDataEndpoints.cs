using Microsoft.EntityFrameworkCore;
using WebhookService.Core.Entities;
using WebhookService.Infrastructure.Data;

namespace WebhookService.Api.Endpoints;

public static class TestDataEndpoints
{
    public static void MapTestDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/test-data")
            .WithTags("Test Data")
            .WithOpenApi();

        // إنشاء بيانات تجريبية - Create test data
        group.MapPost("/create", CreateTestData)
            .WithName("CreateTestData")
            .WithSummary("إنشاء بيانات تجريبية - Create test data")
            .Produces<string>(200);

        // حذف جميع البيانات - Clear all data
        group.MapDelete("/clear", ClearAllData)
            .WithName("ClearAllData")
            .WithSummary("حذف جميع البيانات - Clear all data")
            .Produces<string>(200);
    }

    private static async Task<IResult> CreateTestData(WebhookDbContext context)
    {
        try
        {
            // إنشاء مشترك تجريبي - Create test subscriber
            var subscriber = new Subscriber
            {
                Id = Guid.NewGuid(),
                TenantId = "tenant-123",
                CallbackUrl = "http://localhost:8080/api/webhook/receive",
                EventTypes = ["user.created", "order.completed", "payment.processed"],
                EncryptedSecret = "encrypted-secret-123",
                KeyId = "key-123",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Subscribers.Add(subscriber);
            await context.SaveChangesAsync();

            // إنشاء أحداث وتسليمات تجريبية - Create test events and deliveries
            var random = new Random();
            var eventTypes = new[] { "user.created", "order.completed", "payment.processed" };
            var statuses = new[] { DeliveryStatus.Success, DeliveryStatus.Failed, DeliveryStatus.Pending, DeliveryStatus.Retrying };

            for (int i = 1; i <= 50; i++)
            {
                // إنشاء حدث - Create event
                var eventEntity = new Event
                {
                    Id = Guid.NewGuid(),
                    TenantId = "tenant-123",
                    EventType = eventTypes[random.Next(eventTypes.Length)],
                    Payload = $"{{\"id\": {i}, \"name\": \"Test Item {i}\", \"timestamp\": \"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\"}}",
                    IdempotencyKey = $"test-key-{i}",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-random.Next(1440)) // Random time in last 24 hours
                };

                context.Events.Add(eventEntity);

                // إنشاء تسليم - Create delivery
                var status = statuses[random.Next(statuses.Length)];
                var delivery = new Delivery
                {
                    Id = Guid.NewGuid(),
                    EventId = eventEntity.Id,
                    SubscriberId = subscriber.Id,
                    Status = status,
                    AttemptNumber = status == DeliveryStatus.Retrying ? random.Next(1, 4) : 1,
                    HttpStatusCode = status == DeliveryStatus.Success ? 200 : 
                                   status == DeliveryStatus.Failed ? 500 : null,
                    ErrorMessage = status == DeliveryStatus.Failed ? "Connection timeout" : null,
                    DurationMs = random.Next(100, 2000),
                    CreatedAt = eventEntity.CreatedAt,
                    DeliveredAt = status == DeliveryStatus.Success ? eventEntity.CreatedAt.AddSeconds(random.Next(1, 10)) : null,
                    NextRetryAt = status == DeliveryStatus.Retrying ? DateTime.UtcNow.AddMinutes(random.Next(1, 60)) : null
                };

                context.Deliveries.Add(delivery);
            }

            await context.SaveChangesAsync();

            return Results.Ok($"تم إنشاء 50 حدث وتسليم تجريبي بنجاح - Successfully created 50 test events and deliveries");
        }
        catch (Exception ex)
        {
            return Results.Problem($"خطأ في إنشاء البيانات التجريبية - Error creating test data: {ex.Message}");
        }
    }

    private static async Task<IResult> ClearAllData(WebhookDbContext context)
    {
        try
        {
            // حذف جميع البيانات - Clear all data
            context.Deliveries.RemoveRange(context.Deliveries);
            context.Events.RemoveRange(context.Events);
            context.Subscribers.RemoveRange(context.Subscribers);

            await context.SaveChangesAsync();

            return Results.Ok("تم حذف جميع البيانات بنجاح - All data cleared successfully");
        }
        catch (Exception ex)
        {
            return Results.Problem($"خطأ في حذف البيانات - Error clearing data: {ex.Message}");
        }
    }
}
