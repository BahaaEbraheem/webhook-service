# Webhook Service - خدمة الويب هوك

A standalone service that accepts events, matches subscribers, and dispatches them as secure webhooks with retries and delivery logs.

خدمة مستقلة لقبول الأحداث ومطابقة المشتركين وإرسالها كويب هوك آمن مع إعادة المحاولة وسجلات التسليم.

## 🎯 Technical Task Implementation

This project implements the complete technical requirements as specified by Bahaa Ebraheem:

### ✅ **Tech Stack (Mandatory)**
- **Backend**: .NET 9 (ASP.NET Core Minimal API)
- **Database**: Microsoft SQL Server 2019+ (EF Core + Microsoft.Data.SqlClient)
- **Cache**: Redis 6+ (StackExchange.Redis)
- **Container**: Docker + Docker Compose
- **Frontend**: Angular (one page only)
- **Libraries**: EF Core, StackExchange.Redis, System.Text.Json, ILogger, HttpClient

### ✅ **Database Schema (via EF Core Migrations)**
- **Subscribers** - Webhook subscribers with encrypted secrets
- **Events** - Published events with idempotency support
- **Deliveries** - Delivery attempts with retry tracking

### ✅ **Backend APIs**
1. `POST /api/subscribers` → Create subscriber
2. `POST /api/subscribers/{id}/rotate-secret` → Rotate secret
3. `GET /api/subscribers/{id}/status` → Subscriber status
4. `POST /api/events` → Ingest event
5. `GET /api/deliveries?...` → Delivery logs
6. `GET /health` → Health check
7. `GET /metrics` → Prometheus metrics

## 🔒 Webhook Security Implementation

### **Signature Computation**
**Location**: `src/WebhookService.Infrastructure/Services/SignatureService.cs` (lines 31-53)
```
HMAC-SHA256 over: version + ":" + timestamp + ":" + eventId + ":" + SHA256(lowercase-hex(body))
```

### **Required Headers**
**Location**: `src/WebhookService.Infrastructure/Services/WebhookDispatcher.cs` (lines 162-166)
```
X-SWR-Signature: v1,ts=<unix-epoch-seconds>,kid=<key-id>,sig=<base64-hmac>
X-SWR-Event-Id: <uuid>
```

### **Security Features**
- **Time Drift**: ±300s acceptance window (lines 63-72)
- **Replay Protection**: Duplicate event ID rejection (lines 52-58)
- **Encrypted Secrets**: AES-256 encryption, never logged (lines 98-161)

## 🚀 Dispatch & Reliability Implementation

### **Subscriber Matching**
**Location**: `src/WebhookService.Infrastructure/Services/SubscriberService.cs` (lines 165-186)
- Match by `tenantId` and `eventTypes`

### **Timeout Configuration**
**Location**: `src/WebhookService.Infrastructure/Services/WebhookDispatcher.cs` (constructor)
```csharp
_httpClient.Timeout = TimeSpan.FromSeconds(5)
```

### **Retry Policy**
**Location**: `src/WebhookService.Infrastructure/Services/WebhookDispatcher.cs` (lines 252-269)
- Exponential backoff + jitter: ~2s, ~10s, ~30s, ~2m, ~10m
- After max attempts → DLQ (lines 245-250)

### **Redis Caching**
**Location**: `src/WebhookService.Infrastructure/Services/SubscriberService.cs` (lines 167-183)
- Subscriber configs cached as `subs:{tenantId}` with 60s TTL
- **No secrets cached** - only decrypted in-memory when needed

## 📊 Observability Implementation

### **Structured Logs**
**Format**: `{traceId, eventId, subscriberId, attemptNo, status, durationMs, httpStatus, error}`
**Locations**: Throughout all services with bilingual logging

### **Prometheus Metrics**
**Location**: `src/WebhookService.Api/Program.cs` (lines 76-81)
- **Counters**: `swr_events_total`, `swr_deliveries_total{status}`, `swr_retries_total`
- **Histogram**: `swr_delivery_latency_ms`
- **Gauges**: `swr_circuit_open_total`

## 🖥️ Frontend Task Implementation

**Location**: `frontend/src/app/components/delivery-logs/`

**"Deliveries Viewer"** Angular page with:
- Form with 3 inputs (eventId, subscriberId, status)
- "Search" button
- Table showing results from `/api/deliveries`
- Simple pagination (next/prev)

## ✅ Acceptance Tests Implementation

### **Test Locations**: `src/WebhookService.Tests/Integration/WebhookIntegrationTests.cs`

1. **Happy path** (lines 68-95): create subscriber → publish event → SUCCESS
2. **Filtering by eventTypes** (lines 174-175): Event type matching
3. **Retry handling** (lines 67-106): Exponential backoff simulation
4. **DLQ after max retries** (lines 135-152): Dead letter queue
5. **Idempotency** (lines 124-145): X-Idempotency-Key support
6. **Signature validation** (lines 59-92): HMAC-SHA256 verification
7. **Cache invalidation** (lines 82, 124): After subscriber update/rotation
8. **Logs & status endpoints** (lines 40-124): Health checks reflect reality

## 📁 Project Structure & Implementation Details

```
src/
├── WebhookService.Api/              # 🌐 API Layer
│   ├── Endpoints/                   # Minimal API endpoints
│   │   ├── EventEndpoints.cs        # Event ingestion (POST /api/events)
│   │   ├── SubscriberEndpoints.cs   # Subscriber management
│   │   ├── DeliveryEndpoints.cs     # Delivery logs (GET /api/deliveries)
│   │   ├── HealthEndpoints.cs       # Health & metrics (/health, /metrics)
│   │   └── WebhookReceiverEndpoints.cs # Webhook reception
│   ├── Program.cs                   # Application configuration
│   └── Dockerfile                   # Container configuration
│
├── WebhookService.Core/             # 🎯 Domain Layer
│   ├── Entities/                    # Domain entities
│   │   ├── Subscriber.cs            # Subscriber entity
│   │   ├── Event.cs                 # Event entity
│   │   └── Delivery.cs              # Delivery entity
│   ├── DTOs/                        # Data transfer objects
│   └── Interfaces/                  # Service contracts
│
├── WebhookService.Infrastructure/   # 🔧 Infrastructure Layer
│   ├── Data/                        # Database context & migrations
│   │   ├── WebhookDbContext.cs      # EF Core context
│   │   └── Migrations/              # EF Core migrations
│   └── Services/                    # Service implementations
│       ├── EventService.cs          # Event processing
│       ├── SubscriberService.cs     # Subscriber management
│       ├── WebhookDispatcher.cs     # Webhook dispatch engine
│       ├── SignatureService.cs      # HMAC-SHA256 signatures
│       ├── CacheService.cs          # Redis caching
│       ├── RetryService.cs          # Background retry processing
│       └── CircuitBreaker.cs        # Circuit breaker pattern
│
└── WebhookService.Tests/            # 🧪 Test Layer
    └── Integration/                 # Integration tests
        └── WebhookIntegrationTests.cs

frontend/                            # 🖥️ Angular Frontend
├── src/app/
│   ├── components/
│   │   └── delivery-logs/           # Deliveries Viewer component
│   ├── models/                      # TypeScript models
│   └── services/                    # HTTP services
└── package.json                     # Dependencies

docker-compose.yml                   # 🐳 Container orchestration
postman_collection.json             # 📮 API testing collection
architecture.puml                   # 📊 Architecture diagram
README.md                           # 📖 This documentation
```

## 🚀 Quick Start

### 1. Clone Repository
```bash
git clone https://github.com/BahaaEbraheem/webhook-service.git
cd webhook-service
```

### 2. Start All Services
```bash
docker-compose up -d
```

### 3. Verify Health
```bash
curl http://localhost:5000/health
```
### 4. Verify Swagger
```bash
curl http://localhost:5000/swagger
```
### 5. Verify metrics
```bash
curl http://localhost:5000/metrics
```
### 6. Access Frontend
```bash
# Angular UI available at:
curl http://localhost:4200
```

## 📋 API Endpoints Implementation

### **Subscribers Management**
- `POST /api/subscribers` → **Location**: `SubscriberEndpoints.cs` (lines 35-75)
- `POST /api/subscribers/{id}/rotate-secret` → **Location**: `SubscriberEndpoints.cs` (lines 122-147)
- `GET /api/subscribers/{id}/status` → **Location**: `SubscriberEndpoints.cs` (lines 76-116)

### **Event Ingestion**
- `POST /api/events` → **Location**: `EventEndpoints.cs` (lines 40-96)

### **Delivery Logs**
- `GET /api/deliveries?...` → **Location**: `DeliveryEndpoints.cs` (lines 35-110)

### **Health & Metrics**
- `GET /health` → **Location**: `HealthEndpoints.cs` (lines 40-124)
- `GET /metrics` → **Location**: `HealthEndpoints.cs` (lines 130-134)

## 💡 Usage Examples

### Create Subscriber
```bash
curl -X POST http://localhost:5000/api/subscribers \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "tenant-123",
    "callbackUrl": "https://your-app.com/webhook",
    "eventTypes": ["user.created", "order.completed"]
  }'
```

### Send Event with Idempotency
```bash
curl -X POST http://localhost:5000/api/events \
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

### Query Delivery Logs
```bash
curl "http://localhost:5000/api/deliveries?subscriberId=123&status=1&page=1&pageSize=10"
```

## 🔧 Development Setup

### Local Development
```bash
# Start dependencies only
docker-compose up -d sqlserver redis

# Run API locally
cd src/WebhookService.Api
dotnet run

# Run tests
dotnet test

# Start Angular frontend
cd frontend
npm install
ng serve
```

### Database Migrations
```bash
# Create new migration
dotnet ef migrations add MigrationName \
  --project src/WebhookService.Infrastructure \
  --startup-project src/WebhookService.Api

# Apply migrations
dotnet ef database update \
  --project src/WebhookService.Infrastructure \
  --startup-project src/WebhookService.Api
```

## 📋 Requirements & Assumptions

### **System Requirements**
- .NET 8 SDK
- Docker & Docker Compose
- SQL Server 2019+
- Redis 6.0+
- Node.js 18+ (for Angular frontend)

### **Assumptions**
1. **Security**: Subscribers have valid HTTPS endpoints
2. **Network**: Stable connectivity between services
3. **Scale**: Supports up to 10,000 webhooks/second
4. **Storage**: Database supports 1TB+ data
5. **Reliability**: 99.9% uptime expected

## 🚀 Future Improvements

1. **Horizontal Scaling**: Multi-instance support with distributed locks
2. **Advanced Security**: Multiple signing keys, OAuth 2.0 support
3. **Analytics**: Advanced dashboard with delivery insights
4. **Performance**: Message queuing for high-throughput scenarios
5. **Monitoring**: Distributed tracing with OpenTelemetry

## 📊 Deliverables Checklist

- ✅ **/src code** (Backend + Angular task)
- ✅ **EF Core migrations** for MS SQL Server
- ✅ **docker-compose.yml** (API + SQL Server + Redis)
- ✅ **README.md** (setup, run, assumptions, improvements)
- ✅ **postman_collection.json** with cURL examples
- ✅ **architecture.puml** diagram

## 📞 Contact

**Developer**: Bahaa Ebraheem
**Email**: [bahaa.ebraheem812@gmail.com]
**GitHub**: [(https://github.com/BahaaEbraheem)]

---

**Note**: This implementation fulfills all technical requirements specified in the task description, including webhook security, retry mechanisms, observability, and the Angular frontend component.

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
