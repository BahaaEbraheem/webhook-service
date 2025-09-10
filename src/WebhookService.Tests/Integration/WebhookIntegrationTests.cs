using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System.Net.Http.Json;
using System.Text.Json;
using WebhookService.Core.DTOs;
using Xunit;

namespace WebhookService.Tests.Integration;

/// <summary>
/// اختبارات التكامل لخدمة الويب هوك
/// Integration tests for webhook service
/// </summary>
public class WebhookIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public WebhookIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnHealthy()
    {
        // ترتيب - Arrange
        // لا يوجد ترتيب مطلوب - No arrangement needed

        // تنفيذ - Act
        var response = await _client.GetAsync("/health");

        // تأكيد - Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
    }

    [Fact]
    public async Task CreateSubscriber_ShouldReturnCreatedSubscriber()
    {
        // ترتيب - Arrange
        var request = new CreateSubscriberRequest
        {
            TenantId = "test-tenant",
            CallbackUrl = "https://webhook.site/test",
            EventTypes = new List<string> { "user.created", "order.completed" }
        };

        // تنفيذ - Act
        var response = await _client.PostAsJsonAsync("/api/subscribers", request);

        // تأكيد - Assert
        response.EnsureSuccessStatusCode();
        var subscriber = await response.Content.ReadFromJsonAsync<CreateSubscriberResponse>();
        
        Assert.NotNull(subscriber);
        Assert.Equal(request.TenantId, subscriber.TenantId);
        Assert.Equal(request.CallbackUrl, subscriber.CallbackUrl);
        Assert.Equal(request.EventTypes, subscriber.EventTypes);
        Assert.NotEmpty(subscriber.KeyId);
        Assert.NotEmpty(subscriber.Secret);
    }

    [Fact]
    public async Task CreateEvent_WithValidSubscriber_ShouldDispatchWebhook()
    {
        // ترتيب - Arrange
        // إنشاء مشترك أولاً - Create subscriber first
        var subscriberRequest = new CreateSubscriberRequest
        {
            TenantId = "test-tenant",
            CallbackUrl = "https://webhook.site/test-event",
            EventTypes = new List<string> { "user.created" }
        };

        var subscriberResponse = await _client.PostAsJsonAsync("/api/subscribers", subscriberRequest);
        subscriberResponse.EnsureSuccessStatusCode();

        // إنشاء حدث - Create event
        var eventRequest = new CreateEventRequest
        {
            TenantId = "test-tenant",
            EventType = "user.created",
            Payload = new { userId = "123", email = "test@example.com" },
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        // تنفيذ - Act
        var eventResponse = await _client.PostAsJsonAsync("/api/events", eventRequest);

        // تأكيد - Assert
        eventResponse.EnsureSuccessStatusCode();
        var eventResult = await eventResponse.Content.ReadFromJsonAsync<CreateEventResponse>();
        
        Assert.NotNull(eventResult);
        Assert.Equal(eventRequest.TenantId, eventResult.TenantId);
        Assert.Equal(eventRequest.EventType, eventResult.EventType);
        Assert.True(eventResult.MatchedSubscribers > 0);
    }

    [Fact]
    public async Task GetDeliveries_ShouldReturnPagedResults()
    {
        // ترتيب - Arrange
        // لا يوجد ترتيب مطلوب - No arrangement needed

        // تنفيذ - Act
        var response = await _client.GetAsync("/api/deliveries?page=1&pageSize=10");

        // تأكيد - Assert
        response.EnsureSuccessStatusCode();
        var deliveries = await response.Content.ReadFromJsonAsync<DeliveryPagedResponse>();
        
        Assert.NotNull(deliveries);
        Assert.True(deliveries.Page >= 1);
        Assert.True(deliveries.PageSize > 0);
        Assert.True(deliveries.TotalCount >= 0);
    }

    [Fact]
    public async Task CreateEvent_WithDuplicateIdempotencyKey_ShouldReturnConflict()
    {
        // ترتيب - Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var eventRequest = new CreateEventRequest
        {
            TenantId = "test-tenant",
            EventType = "user.created",
            Payload = new { userId = "123" },
            IdempotencyKey = idempotencyKey
        };

        // إرسال الحدث الأول - Send first event
        await _client.PostAsJsonAsync("/api/events", eventRequest);

        // تنفيذ - Act
        // إرسال نفس الحدث مرة أخرى - Send same event again
        var duplicateResponse = await _client.PostAsJsonAsync("/api/events", eventRequest);

        // تأكيد - Assert
        Assert.Equal(System.Net.HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task Metrics_ShouldReturnPrometheusFormat()
    {
        // ترتيب - Arrange
        // لا يوجد ترتيب مطلوب - No arrangement needed

        // تنفيذ - Act
        var response = await _client.GetAsync("/metrics");

        // تأكيد - Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        // التحقق من وجود مقاييس Prometheus - Check for Prometheus metrics
        Assert.Contains("# HELP", content);
        Assert.Contains("# TYPE", content);
    }
}
