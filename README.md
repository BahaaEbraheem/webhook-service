# Webhook Service

خدمة ويب هوك متقدمة مبنية بـ .NET 8 لاستقبال الأحداث ومطابقة المشتركين وإرسال الويب هوك بشكل آمن.

A comprehensive webhook service built with .NET 8 for receiving events, matching subscribers, and dispatching secure webhooks.

## المميزات - Features

- **استقبال الأحداث** - Event ingestion with idempotency support
- **مطابقة المشتركين** - Intelligent subscriber matching by event type and tenant
- **إرسال آمن** - Secure webhook delivery with HMAC-SHA256 signatures
- **إعادة المحاولة** - Exponential backoff retry mechanism with circuit breaker
- **التخزين المؤقت** - Redis caching for subscriber configurations
- **المراقبة** - Prometheus metrics and health checks
- **السجلات** - Structured logging with Arabic comments
- **الحاويات** - Docker containerization with Docker Compose

## البنية التقنية - Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Angular UI    │    │   ASP.NET API   │    │   SQL Server    │
│   (Port 4200)   │◄──►│   (Port 8080)   │◄──►│   (Port 1433)   │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                              │
                              ▼
                       ┌─────────────────┐
                       │      Redis      │
                       │   (Port 6379)   │
                       └─────────────────┘
```

## المتطلبات - Requirements

- .NET 8 SDK
- Docker & Docker Compose
- SQL Server 2019+
- Redis 6.0+

## التشغيل السريع - Quick Start

### 1. استنساخ المشروع - Clone Repository
```bash
git clone https://github.com/BahaaEbraheem/webhook-service.git
cd webhook-service
```

### 2. تشغيل الخدمات - Start Services
```bash
docker-compose up -d
```

### 3. التحقق من الصحة - Health Check
```bash
curl http://localhost:8080/health
```

## نقاط النهاية - API Endpoints

### المشتركين - Subscribers
- `POST /api/subscribers` - إنشاء مشترك جديد
- `GET /api/subscribers` - قائمة المشتركين
- `PUT /api/subscribers/{id}` - تحديث مشترك
- `DELETE /api/subscribers/{id}` - حذف مشترك

### الأحداث - Events
- `POST /api/events` - إرسال حدث جديد

### التسليمات - Deliveries
- `GET /api/deliveries` - البحث في عمليات التسليم

### المراقبة - Monitoring
- `GET /health` - فحص الصحة
- `GET /metrics` - مقاييس Prometheus

## أمثلة الاستخدام - Usage Examples

### إنشاء مشترك - Create Subscriber
```bash
curl -X POST http://localhost:8080/api/subscribers \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "tenant-123",
    "url": "https://example.com/webhook",
    "eventTypes": ["user.created", "order.completed"],
    "isActive": true
  }'
```

### إرسال حدث - Send Event
```bash
curl -X POST http://localhost:8080/api/events \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: unique-key-123" \
  -d '{
    "tenantId": "tenant-123",
    "eventType": "user.created",
    "payload": {
      "userId": "user-456",
      "email": "user@example.com"
    }
  }'
```

## الأمان - Security

### التوقيع - Signature Verification
كل ويب هوك يحتوي على توقيع HMAC-SHA256 في الهيدر:
```
X-Webhook-Signature: v1:timestamp:eventId:signature
```

### التشفير - Encryption
أسرار المشتركين مشفرة باستخدام AES-256 في قاعدة البيانات.

## المراقبة - Monitoring

### المقاييس - Metrics
- `swr_events_total` - إجمالي الأحداث
- `swr_delivery_latency_ms` - زمن التسليم
- `swr_circuit_open_total` - عدد الدوائر المفتوحة

### السجلات - Logs
السجلات منظمة بصيغة JSON مع التعليقات باللغة العربية.

## التطوير - Development

### تشغيل محلي - Local Development
```bash
# تشغيل قاعدة البيانات والريديس
docker-compose up -d sqlserver redis

# تشغيل API
cd src/WebhookService.Api
dotnet run

# تشغيل الاختبارات
dotnet test
```

### الترحيلات - Migrations
```bash
# إنشاء ترحيل جديد
dotnet ef migrations add MigrationName --project src/WebhookService.Infrastructure --startup-project src/WebhookService.Api

# تطبيق الترحيلات
dotnet ef database update --project src/WebhookService.Infrastructure --startup-project src/WebhookService.Api
```

## الافتراضات - Assumptions

1. **الأمان**: المشتركون موثوقون ولديهم نقاط نهاية HTTPS صالحة
2. **الشبكة**: اتصال مستقر بين الخدمات
3. **الحجم**: يدعم حتى 10,000 ويب هوك في الثانية
4. **التخزين**: قاعدة البيانات تدعم 1TB من البيانات

## التحسينات المستقبلية - Future Improvements

1. **التوسع الأفقي** - Horizontal scaling with message queues
2. **التحليلات** - Advanced analytics dashboard
3. **الأمان المتقدم** - OAuth 2.0 authentication
4. **الأداء** - GraphQL API support
5. **المرونة** - Multi-region deployment

## المساهمة - Contributing

1. Fork المشروع
2. إنشاء فرع للميزة (`git checkout -b feature/AmazingFeature`)
3. Commit التغييرات (`git commit -m 'Add AmazingFeature'`)
4. Push للفرع (`git push origin feature/AmazingFeature`)
5. فتح Pull Request

## الترخيص - License

هذا المشروع مرخص تحت رخصة MIT - انظر ملف [LICENSE](LICENSE) للتفاصيل.
