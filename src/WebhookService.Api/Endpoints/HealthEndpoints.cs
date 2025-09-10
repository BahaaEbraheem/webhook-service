using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using WebhookService.Api.Models;
using WebhookService.Infrastructure.Data;

namespace WebhookService.Api.Endpoints;

/// <summary>
/// نقاط النهاية الخاصة بالصحة والمقاييس
/// Health and metrics endpoints
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// تسجيل نقاط النهاية الخاصة بالصحة والمقاييس
    /// Register health and metrics endpoints
    /// </summary>
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // فحص الصحة - Health check
        app.MapGet("/health", GetHealth)
            .WithName("GetHealth")
            .WithSummary("فحص صحة النظام - System health check")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable)
            .WithTags("Health");

        // مقاييس Prometheus - Prometheus metrics
        app.MapGet("/metrics", GetMetrics)
            .WithName("GetMetrics")
            .WithSummary("مقاييس Prometheus - Prometheus metrics")
            .Produces(StatusCodes.Status200OK, contentType: "text/plain")
            .WithTags("Metrics");
    }

    /// <summary>
    /// فحص صحة النظام
    /// System health check
    /// </summary>
    private static async Task<IResult> GetHealth(
        WebhookDbContext dbContext,
        IConnectionMultiplexer redis,
        ILogger<Program> logger)
    {
        var health = new HealthResponse
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Checks = new Dictionary<string, HealthCheck>()
        };

        try
        {
            // فحص قاعدة البيانات - Check database
            var dbStartTime = DateTime.UtcNow;
            await dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
            var dbDuration = DateTime.UtcNow - dbStartTime;

            health.Checks["database"] = new HealthCheck
            {
                Status = "Healthy",
                Duration = dbDuration,
                Description = "قاعدة البيانات تعمل بشكل طبيعي - Database is working normally"
            };

            logger.LogDebug("فحص قاعدة البيانات نجح في {Duration}ms", dbDuration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            health.Status = "Unhealthy";
            health.Checks["database"] = new HealthCheck
            {
                Status = "Unhealthy",
                Duration = TimeSpan.Zero,
                Description = $"خطأ في قاعدة البيانات - Database error: {ex.Message}"
            };

            logger.LogError(ex, "فحص قاعدة البيانات فشل");
        }

        try
        {
            // فحص Redis - Check Redis
            var redisStartTime = DateTime.UtcNow;
            var database = redis.GetDatabase();
            await database.PingAsync();
            var redisDuration = DateTime.UtcNow - redisStartTime;

            health.Checks["redis"] = new HealthCheck
            {
                Status = "Healthy",
                Duration = redisDuration,
                Description = "Redis يعمل بشكل طبيعي - Redis is working normally"
            };

            logger.LogDebug("فحص Redis نجح في {Duration}ms", redisDuration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            health.Status = "Unhealthy";
            health.Checks["redis"] = new HealthCheck
            {
                Status = "Unhealthy",
                Duration = TimeSpan.Zero,
                Description = $"خطأ في Redis - Redis error: {ex.Message}"
            };

            logger.LogError(ex, "فحص Redis فشل");
        }

        // إضافة معلومات النظام - Add system information
        health.Checks["system"] = new HealthCheck
        {
            Status = "Healthy",
            Duration = TimeSpan.Zero,
            Description = $"الذاكرة المستخدمة: {GC.GetTotalMemory(false) / 1024 / 1024} MB - Memory usage: {GC.GetTotalMemory(false) / 1024 / 1024} MB"
        };

        var statusCode = health.Status == "Healthy" ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;

        logger.LogInformation("فحص الصحة العام: {Status}", health.Status);

        return Results.Json(health, statusCode: statusCode);
    }

    /// <summary>
    /// الحصول على مقاييس Prometheus
    /// Get Prometheus metrics
    /// </summary>
    private static IResult GetMetrics()
    {
        // استخدام MetricServer المدمج - Use built-in MetricServer
        return Results.Redirect("/metrics");
    }
}
