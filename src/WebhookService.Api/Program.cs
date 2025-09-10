using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;
using WebhookService.Api.Endpoints;
using WebhookService.Core.Interfaces;
using WebhookService.Infrastructure.Data;
using WebhookService.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// إضافة الخدمات للحاوي - Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

// إعداد قاعدة البيانات - Configure database
builder.Services.AddDbContext<WebhookDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    try
    {
        // Try SQL Server first
        options.UseSqlServer(connectionString);
    }
    catch
    {
        // Fallback to In-Memory database for development
        options.UseInMemoryDatabase("WebhookServiceDb");
    }
});

// إعداد Redis - Configure Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    try
    {
        var connectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false; // Allow retries
        options.ConnectTimeout = 5000; // 5 seconds timeout
        return ConnectionMultiplexer.Connect(options);
    }
    catch (Exception ex)
    {
        var logger = provider.GetService<ILogger<Program>>();
        logger?.LogWarning(ex, "فشل الاتصال بـ Redis، سيتم استخدام ذاكرة محلية - Failed to connect to Redis, using in-memory cache");

        // Return a mock connection that won't be used
        return ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false");
    }
});
// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200") // Angular dev server
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
// إعداد HttpClient - Configure HttpClient
builder.Services.AddHttpClient<IWebhookDispatcher, WebhookDispatcher>();

// تسجيل الخدمات - Register services
builder.Services.AddScoped<ISubscriberService, SubscriberService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IDeliveryService, DeliveryService>();
builder.Services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();
builder.Services.AddScoped<ISignatureService, SignatureService>();
builder.Services.AddScoped<ICacheService, CacheService>();

// إضافة خدمة إعادة المحاولة في الخلفية - Add background retry service
builder.Services.AddHostedService<RetryService>();

// إضافة مقاييس Prometheus - Add Prometheus metrics
builder.Services.AddSingleton(Metrics.CreateCounter("swr_events_total", "Total number of events"));
builder.Services.AddSingleton(Metrics.CreateCounter("swr_deliveries_total", "Total number of deliveries", "status"));
builder.Services.AddSingleton(Metrics.CreateCounter("swr_retries_total", "Total number of retries"));
builder.Services.AddSingleton(Metrics.CreateHistogram("swr_delivery_latency_ms", "Delivery latency in milliseconds"));
builder.Services.AddSingleton(Metrics.CreateGauge("swr_circuit_open_total", "Number of open circuit breakers"));

// إضافة التحقق من الصحة - Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// تكوين pipeline الطلبات HTTP - Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Webhook Service API V1");
        c.RoutePrefix = "swagger";
    });
}
// Use CORS
app.UseCors("AllowAngularDev");
// إضافة مقاييس Prometheus - Add Prometheus metrics
app.UseMetricServer();
app.UseHttpMetrics();

// تسجيل نقاط النهاية - Register endpoints
app.MapSubscriberEndpoints();
app.MapEventEndpoints();
app.MapDeliveryEndpoints();
app.MapHealthEndpoints();
app.MapWebhookReceiverEndpoints();
app.MapTestDataEndpoints();

// تشغيل الترحيلات - Run migrations
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();
    try
    {
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "فشل في تشغيل الترحيلات، سيتم إنشاء قاعدة البيانات - Migration failed, ensuring database is created");
        context.Database.EnsureCreated();
    }
}

app.Run();
