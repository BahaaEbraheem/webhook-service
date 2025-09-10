using Microsoft.AspNetCore.Mvc;
using System.Text;
using WebhookService.Core.Interfaces;

namespace WebhookService.Api.Endpoints;

/// <summary>
/// نقاط النهاية لاستقبال الويب هوك
/// Webhook receiver endpoints
/// </summary>
public static class WebhookReceiverEndpoints
{
    /// <summary>
    /// تسجيل نقاط النهاية لاستقبال الويب هوك
    /// Register webhook receiver endpoints
    /// </summary>
    public static void MapWebhookReceiverEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhook")
            .WithTags("WebhookReceiver")
            .WithOpenApi();

        // استقبال الويب هوك - Receive webhook
        group.MapPost("/receive", ReceiveWebhook)
            .WithName("ReceiveWebhook")
            .WithSummary("استقبال الويب هوك - Receive webhook")
            .Produces<WebhookReceiveResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        // استقبال الويب هوك مع معرف المشترك - Receive webhook with subscriber ID
        group.MapPost("/receive/{subscriberId:guid}", ReceiveWebhookWithId)
            .WithName("ReceiveWebhookWithId")
            .WithSummary("استقبال الويب هوك مع معرف المشترك - Receive webhook with subscriber ID")
            .Produces<WebhookReceiveResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// استقبال الويب هوك العام
    /// Receive general webhook
    /// </summary>
    private static async Task<IResult> ReceiveWebhook(
        HttpRequest request,
        ISignatureService signatureService,
        ILogger<Program> logger)
    {
        try
        {
            logger.LogInformation("تم استقبال ويب هوك جديد من {RemoteIpAddress}", 
                request.HttpContext.Connection.RemoteIpAddress);

            // قراءة محتوى الطلب - Read request content
            var body = await ReadRequestBodyAsync(request);
            
            // استخراج الهيدرز - Extract headers
            var headers = ExtractHeaders(request);
            
            // تسجيل البيانات المستقبلة - Log received data
            logger.LogInformation("تم استقبال ويب هوك: Headers={@Headers}, BodyLength={BodyLength}", 
                headers, body.Length);

            // إنشاء الاستجابة - Create response
            var response = new WebhookReceiveResponse
            {
                Success = true,
                Message = "تم استقبال الويب هوك بنجاح - Webhook received successfully",
                ReceivedAt = DateTime.UtcNow,
                RequestId = Guid.NewGuid().ToString(),
                Headers = headers,
                BodyLength = body.Length
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "خطأ في استقبال الويب هوك");
            return Results.Problem(
                title: "Webhook Reception Error",
                detail: "حدث خطأ أثناء استقبال الويب هوك - An error occurred while receiving webhook",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// استقبال الويب هوك مع معرف المشترك
    /// Receive webhook with subscriber ID
    /// </summary>
    private static async Task<IResult> ReceiveWebhookWithId(
        Guid subscriberId,
        HttpRequest request,
        ISubscriberService subscriberService,
        ISignatureService signatureService,
        ILogger<Program> logger)
    {
        try
        {
            logger.LogInformation("تم استقبال ويب هوك للمشترك {SubscriberId} من {RemoteIpAddress}", 
                subscriberId, request.HttpContext.Connection.RemoteIpAddress);

            // التحقق من وجود المشترك - Check if subscriber exists
            var subscriber = await subscriberService.GetSubscriberStatusAsync(subscriberId);
            if (subscriber == null)
            {
                logger.LogWarning("المشترك غير موجود: {SubscriberId}", subscriberId);
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Subscriber Not Found",
                    Detail = $"المشترك {subscriberId} غير موجود - Subscriber {subscriberId} not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            // قراءة محتوى الطلب - Read request content
            var body = await ReadRequestBodyAsync(request);
            
            // استخراج الهيدرز - Extract headers
            var headers = ExtractHeaders(request);

            // التحقق من التوقيع إذا كان موجوداً - Verify signature if present
            var signatureHeader = request.Headers["X-SWR-Signature"].FirstOrDefault();
            var eventIdHeader = request.Headers["X-SWR-Event-Id"].FirstOrDefault();
            
            bool signatureValid = true;
            if (!string.IsNullOrEmpty(signatureHeader) && !string.IsNullOrEmpty(eventIdHeader))
            {
                // TODO: Implement signature verification
                // This would require decrypting the subscriber's secret and validating
                logger.LogDebug("تم العثور على توقيع، سيتم التحقق منه لاحقاً");
            }

            // تسجيل البيانات المستقبلة - Log received data
            logger.LogInformation("تم استقبال ويب هوك للمشترك {SubscriberId}: Headers={@Headers}, BodyLength={BodyLength}", 
                subscriberId, headers, body.Length);

            // إنشاء الاستجابة - Create response
            var response = new WebhookReceiveResponse
            {
                Success = true,
                Message = $"تم استقبال الويب هوك للمشترك {subscriberId} بنجاح - Webhook received successfully for subscriber {subscriberId}",
                ReceivedAt = DateTime.UtcNow,
                RequestId = Guid.NewGuid().ToString(),
                SubscriberId = subscriberId,
                Headers = headers,
                BodyLength = body.Length,
                SignatureValid = signatureValid
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "خطأ في استقبال الويب هوك للمشترك {SubscriberId}", subscriberId);
            return Results.Problem(
                title: "Webhook Reception Error",
                detail: "حدث خطأ أثناء استقبال الويب هوك - An error occurred while receiving webhook",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// قراءة محتوى الطلب
    /// Read request body
    /// </summary>
    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        request.Body.Position = 0;
        
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        
        request.Body.Position = 0;
        return body;
    }

    /// <summary>
    /// استخراج الهيدرز المهمة
    /// Extract important headers
    /// </summary>
    private static Dictionary<string, string> ExtractHeaders(HttpRequest request)
    {
        var headers = new Dictionary<string, string>();
        
        // الهيدرز المهمة - Important headers
        var importantHeaders = new[]
        {
            "Content-Type",
            "User-Agent",
            "X-SWR-Signature",
            "X-SWR-Event-Id",
            "Authorization",
            "X-Forwarded-For",
            "X-Real-IP"
        };

        foreach (var headerName in importantHeaders)
        {
            if (request.Headers.ContainsKey(headerName))
            {
                headers[headerName] = request.Headers[headerName].ToString();
            }
        }

        return headers;
    }
}

/// <summary>
/// استجابة استقبال الويب هوك
/// Webhook receive response
/// </summary>
public class WebhookReceiveResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public Guid? SubscriberId { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public int BodyLength { get; set; }
    public bool? SignatureValid { get; set; }
}
